using UnityEngine;
using System.Collections;
using UnityEngine.Serialization;

[RequireComponent(typeof(VehiclePerformanceSystem))]
[RequireComponent(typeof(Rigidbody))]
public class TruckCollisionManager : MonoBehaviour
{
    private VehiclePerformanceSystem vps;
    private Rigidbody rb;
    private VehicleAppearance vehicleAppearance;

    [Header("--- 碰撞后退参数 ---")]
    public float backwardBoost = 1.5f;
    public float bounciness = 0.2f;
    public float destroyedBackwardDistance = 3f;

    [Header("--- Speed limits by damage state (km/h) ---")]
    [FormerlySerializedAs("\u8f7b\u635f\u72b6\u6001\u901f\u5ea6")]
    [Tooltip("轻伤状态最高速度，约为原始速度 80%")]
    public float lightDamageMaxSpeed = 95f;
    [FormerlySerializedAs("\u91cd\u635f\u72b6\u6001\u901f\u5ea6")]
    public float heavyDamageMaxSpeed = 50f;
    [FormerlySerializedAs("\u62a5\u5e9f\u72b6\u6001\u901f\u5ea6")]
    public float destroyedMaxSpeed = 0f;

    [Header("--- 撞墙单次追加伤害（按速度 m/s，累计制）---")]
    public float wallLightSpeed = 8f;
    public float wallHeavySpeed = 15f;
    public float wallDestroySpeed = 25f;

    private float lastHitTime = 0f;
    private bool isBouncing = false;
    private bool isWaitingForTab = false;
    private Coroutine pauseCoroutine;

    private float originalMaxSpeed;
    private Vector3 startPosition;
    private Quaternion startRotation;
    private VehicleTruckImpactVfx truckImpactVfx;

    void Awake()
    {
        startPosition = transform.position;
        startRotation = transform.rotation;
    }

    void Start()
    {
        vps = GetComponent<VehiclePerformanceSystem>();
        rb = GetComponent<Rigidbody>();
        vehicleAppearance = GetComponent<VehicleAppearance>();
        truckImpactVfx = VehicleTruckImpactVfx.EnsureOn(gameObject);

        if (vps != null)
        {
            if (vps.controller != null && vps.controller.config != null)
                originalMaxSpeed = vps.controller.config.maxSpeed;
            else if (vps.truckController != null && vps.truckController.config != null)
                originalMaxSpeed = vps.truckController.config.maxSpeed;
        }
    }

    public void ResetVehicle()
    {
        Time.timeScale = 1f;
        isWaitingForTab = false;
        isBouncing = false;

        if (pauseCoroutine != null)
        {
            StopCoroutine(pauseCoroutine);
            pauseCoroutine = null;
        }

        StopAllCoroutines();

        rb.velocity = Vector3.zero;
        rb.angularVelocity = Vector3.zero;
        rb.position = startPosition;
        rb.rotation = startRotation;

        vps.Repair();
        RestoreOriginalSpeedConfig();
        ApplySpeedLimitByState();

        if (vehicleAppearance != null)
            vehicleAppearance.ResetAppearance();

        if (truckImpactVfx != null)
            truckImpactVfx.ResetEffects();

        if (vps.controller != null)
        {
            vps.controller.PrepareForDriving();
            vps.controller.enabled = true;
        }
        if (vps.truckController != null)
        {
            vps.truckController.PrepareForDriving();
            vps.truckController.enabled = true;
        }

        rb.Sleep();
        lastHitTime = 0f;
    }

    void OnCollisionEnter(Collision collision)
    {
        if (Time.time - lastHitTime < 1f || isBouncing || isWaitingForTab)
            return;

        string targetTag = VehicleOpponentUtility.ResolveOpponentTag(collision.gameObject);
        float damageToAdd = 0f;
        bool validHit = false;
        bool isTruckOpponentHit = false;
        float pauseDelay = -1f;

        if (targetTag == "Wall")
        {
            float impactSpeed = collision.relativeVelocity.magnitude;
            if (impactSpeed >= 2f)
            {
                damageToAdd = CalculateWallDamage(impactSpeed);
                validHit = true;
            }
        }
        else if (VehicleOpponentUtility.IsTruckOpponent(collision.gameObject))
        {
            isTruckOpponentHit = true;
            float targetDamage = vps.damage < 65f ? 65f : 100f;
            damageToAdd = Mathf.Max(0f, targetDamage - vps.damage);
            validHit = true;
            pauseDelay = 0f;

            VehicleTruckImpactVfx.PlayForCollisionPair(collision, gameObject);
        }

        if (!validHit)
            return;

        if (damageToAdd <= 0f && !isTruckOpponentHit)
            return;

        lastHitTime = Time.time;

        DamageData data = new DamageData
        {
            impactForce = collision.impulse.magnitude / Time.fixedDeltaTime,
            hitPoint = collision.contacts[0].point,
            hitNormal = collision.contacts[0].normal,
            victim = gameObject,
            hitPart = collision.contacts[0].thisCollider
        };
        SimulationEvents.TriggerVehicleDamage(data);

        if (damageToAdd > 0f)
            vps.AddDamage(damageToAdd);

        StartCoroutine(HandlePostCollision(collision, pauseDelay));
    }

    private IEnumerator HandlePostCollision(Collision collision, float pauseDelay)
    {
        isBouncing = true;

        if (vps.controller != null) vps.controller.enabled = false;
        if (vps.truckController != null) vps.truckController.enabled = false;

        bool isDestroyed = vps.state == VehicleState.Destroyed;
        Vector3 normal = collision.contacts[0].normal;

        if (!isDestroyed)
        {
            ApplyCollisionKnockback(collision, normal, backwardBoost);

            float waitTime = Mathf.Clamp(rb.velocity.magnitude * 0.15f, 0.3f, 0.8f);
            yield return new WaitForSeconds(waitTime);
        }
        else
        {
            rb.velocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;

            Vector3 knockbackDir = normal.sqrMagnitude > 0.0001f ? normal.normalized : -transform.forward;
            if (Vector3.Dot(knockbackDir, transform.forward) > 0f)
                knockbackDir = -transform.forward;
            rb.position = rb.position + knockbackDir * destroyedBackwardDistance;

            yield return new WaitForFixedUpdate();
        }

        rb.velocity = Vector3.zero;
        rb.angularVelocity = Vector3.zero;
        ApplySpeedLimitByState();

        if (pauseDelay >= 0f || vps.state == VehicleState.Destroyed)
            LockVehicleDriving();
        else
            ResumeVehicleDriving();

        isBouncing = false;

        if (pauseDelay >= 0f)
        {
            if (pauseCoroutine != null) StopCoroutine(pauseCoroutine);
            pauseCoroutine = StartCoroutine(DelayedPause(pauseDelay));
        }
    }

    private void ApplyCollisionKnockback(Collision collision, Vector3 normal, float boost)
    {
        Vector3 impulse = collision.impulse;
        Vector3 deltaV = impulse / rb.mass;
        Vector3 incomingVelocity = rb.velocity;
        float vN = Vector3.Dot(incomingVelocity, normal);
        Vector3 vT = incomingVelocity - normal * vN;
        Vector3 newNormalVelocity = -normal * vN * bounciness;
        Vector3 finalVelocity = vT + newNormalVelocity + deltaV * 0.5f;

        Vector3 localVel = transform.InverseTransformDirection(finalVelocity);
        if (localVel.z > 0f)
            localVel.z = 0f;
        localVel.z -= boost * Mathf.Abs(vN) * 0.5f;
        rb.velocity = transform.TransformDirection(localVel);
    }

    private IEnumerator DelayedPause(float delay)
    {
        if (delay > 0f)
            yield return new WaitForSeconds(delay);

        isWaitingForTab = true;
        Time.timeScale = 0f;
    }

    public void CancelPendingGlobalPause()
    {
        isWaitingForTab = false;

        if (pauseCoroutine != null)
        {
            StopCoroutine(pauseCoroutine);
            pauseCoroutine = null;
        }
    }

    private void LockVehicleDriving()
    {
        if (vps.controller != null)
        {
            vps.controller.enabled = true;
            vps.controller.LockDrivingUntilReset();
        }
        if (vps.truckController != null)
        {
            vps.truckController.enabled = true;
            vps.truckController.LockDrivingUntilReset();
        }
    }

    private void ResumeVehicleDriving()
    {
        if (vps.controller != null)
        {
            vps.controller.enabled = true;
            vps.controller.UnlockDriving();
            vps.controller.ClearWheelTorque();
        }
        if (vps.truckController != null)
        {
            vps.truckController.enabled = true;
            vps.truckController.UnlockDriving();
        }
    }

    private float CalculateWallDamage(float impactSpeedMs)
    {
        // 累计伤害：首次撞墙只会轻伤，不会立刻切到解体/翻车模型
        if (impactSpeedMs < wallLightSpeed) return 8f;
        if (impactSpeedMs < wallHeavySpeed) return 12f;
        if (impactSpeedMs < wallDestroySpeed) return 18f;
        return 25f;
    }

    private void RestoreOriginalSpeedConfig()
    {
        if (vps.controller != null && vps.controller.config != null)
            vps.controller.config.maxSpeed = originalMaxSpeed;
        if (vps.truckController != null && vps.truckController.config != null)
            vps.truckController.config.maxSpeed = originalMaxSpeed;
    }

    private void ApplySpeedLimitByState()
    {
        if (vps.state == VehicleState.Normal)
        {
            RestoreOriginalSpeedConfig();
            vps.SetSpeedLimit(-1f);
            vps.SetTorqueMultiplier(1f);
            return;
        }

        float speedLimit = -1f;
        float torqueMultiplier = 1f;

        switch (vps.state)
        {
            case VehicleState.Damaged:
                speedLimit = Mathf.Min(originalMaxSpeed, Mathf.Max(lightDamageMaxSpeed, originalMaxSpeed * 0.75f));
                torqueMultiplier = 0.85f;
                break;
            case VehicleState.HeavyDamaged:
                speedLimit = heavyDamageMaxSpeed;
                torqueMultiplier = 0.5f;
                break;
            case VehicleState.Destroyed:
                speedLimit = destroyedMaxSpeed;
                torqueMultiplier = 0f;
                break;
        }

        if (speedLimit >= 0f)
        {
            vps.SetSpeedLimit(speedLimit);
            vps.SetTorqueMultiplier(torqueMultiplier);
        }
    }
}
