using UnityEngine;
using System.Collections;

[RequireComponent(typeof(VehiclePerformanceSystem))]
[RequireComponent(typeof(Rigidbody))]
public class SUVCollisionManager : MonoBehaviour
{
    private VehiclePerformanceSystem vps;
    private Rigidbody rb;
    private VehicleController controller;
    private VehicleAppearance vehicleAppearance;

    [Header("--- 碰撞后继续运行时间 ---")]
    public float postCollisionRunTime = 2f;

    [Header("--- 速度区间 → 损伤程度 ---")]
    public float lightDamageSpeed = 10f;
    public float heavyDamageSpeed = 20f;

    [Header("--- 各状态最高速度设置 ---")]
    public float lightDamageMaxSpeed = 60f;
    public float heavyDamageMaxSpeed = 30f;
    public float destroyedMaxSpeed = 0f;

    [Header("--- 碰撞后退增强（非报废状态有效）---")]
    public float backwardBoost = 1.5f;
    public float bounciness = 0.2f;

    [Header("--- 报废状态后退设置 ---")]
    public float destroyedBackwardDistance = 3f;   // 强制后退距离（米）

    // 缓存原始性能值
    private float originalMaxSpeed;
    private float originalTorque;

    // 状态标志
    private bool hasCollided = false;
    private bool isProcessingCollision = false;
    private float lastHitTime = 0f;
    private Coroutine postCollisionCoroutine;

    // 初始变换（用于重置）
    private Vector3 startPosition;
    private Quaternion startRotation;

    void Awake()
    {
        startPosition = transform.position;
        startRotation = transform.rotation;
    }

    void Start()
    {
        vps = GetComponent<VehiclePerformanceSystem>();
        rb = GetComponent<Rigidbody>();
        controller = vps?.controller;
        vehicleAppearance = GetComponent<VehicleAppearance>();

        if (controller != null && controller.config != null)
        {
            originalMaxSpeed = controller.config.maxSpeed;
            originalTorque = controller.config.motorTorque;
        }
    }

    public void ResetVehicle()
    {
        Time.timeScale = 1f;
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

        hasCollided = false;
        isProcessingCollision = false;
        lastHitTime = 0f;
        postCollisionCoroutine = null;

        if (controller != null)
        {
            controller.PrepareForDriving();
            controller.enabled = true;
        }

        rb.Sleep();
    }

    void OnCollisionEnter(Collision collision)
    {
        if (hasCollided) return;
        if (isProcessingCollision) return;
        if (Time.time - lastHitTime < 0.5f) return;
        if (!collision.gameObject.CompareTag("Wall")) return;

        lastHitTime = Time.time;
        isProcessingCollision = true;
        hasCollided = true;

        float relativeSpeed = collision.relativeVelocity.magnitude;
        float damageToAdd = CalculateDamageFromSpeed(relativeSpeed);

        DamageData data = new DamageData
        {
            impactForce = collision.impulse.magnitude / Time.fixedDeltaTime,
            hitPoint = collision.contacts[0].point,
            hitNormal = collision.contacts[0].normal,
            victim = gameObject,
            hitPart = collision.contacts[0].thisCollider
        };
        SimulationEvents.TriggerVehicleDamage(data);

        vps.AddDamage(damageToAdd);

        postCollisionCoroutine = StartCoroutine(HandlePostCollision(collision));
    }

    public void CancelPendingGlobalPause()
    {
        if (postCollisionCoroutine != null)
        {
            StopCoroutine(postCollisionCoroutine);
            postCollisionCoroutine = null;
        }
    }

    private float CalculateDamageFromSpeed(float speed)
    {
        float currentDamage = vps.damage;
        float targetDamage = 0f;

        if (speed <= lightDamageSpeed)
            targetDamage = 30f;
        else if (speed <= heavyDamageSpeed)
            targetDamage = 65f;
        else
            targetDamage = 100f;

        if (targetDamage > currentDamage)
            return targetDamage - currentDamage;
        else
            return 0f;
    }

    private IEnumerator HandlePostCollision(Collision collision)
    {
        if (controller != null) controller.enabled = false;

        // 获取碰撞法线和碰撞点
        Vector3 normal = collision.contacts[0].normal;

        // 判断是否进入报废状态（damage 已更新，vps.state 已变化）
        bool isDestroyed = (vps.state == VehicleState.Destroyed);

        if (!isDestroyed)
        {
            // ===== 非报废：原有增强后退物理 =====
            Vector3 impulse = collision.impulse;
            Vector3 deltaV = impulse / rb.mass;
            Vector3 incomingVelocity = rb.velocity;
            float vN = Vector3.Dot(incomingVelocity, normal);
            Vector3 vT = incomingVelocity - normal * vN;
            Vector3 newNormalVelocity = -normal * vN * bounciness;
            Vector3 finalVelocity = vT + newNormalVelocity + deltaV * 0.5f;
            finalVelocity += normal * (backwardBoost * Mathf.Abs(vN) * 0.5f);

            rb.velocity = finalVelocity;

            // 动态等待后退自然减速
            float backSpeed = Vector3.Dot(finalVelocity, normal);
            float waitTime = Mathf.Clamp(backSpeed * 0.03f, 0.2f, 0.6f);
            yield return new WaitForSeconds(waitTime);
        }
        else
        {
            // ===== 报废状态：强制后退指定距离（沿法线方向）=====
            // 先停掉所有速度，避免物理干扰
            rb.velocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;

            // 计算后退目标位置：当前位置 + 法线方向 * 距离（法线指向碰撞对象，反向即为远离）
            Vector3 backwardDirection = normal;   // 沿法线方向（远离墙体）
            Vector3 targetPosition = transform.position + backwardDirection * destroyedBackwardDistance;

            // 直接设置位置（也可以使用 rb.MovePosition，但需等待物理更新，这里直接设置更可控）
            rb.position = targetPosition;

            // 等待一帧，确保位置更新完成（可选，视觉效果上直接瞬移）
            yield return null;
        }

        // 应用速度/扭矩限制（会根据当前状态改变车辆性能）
        ApplySpeedLimitByState();

        if (controller != null)
        {
            controller.enabled = true;
            controller.LockDrivingUntilReset();
        }

        // 等待观察时间（期间无法加速）
        yield return new WaitForSeconds(postCollisionRunTime);

        // 暂停游戏，方便观察
        Time.timeScale = 0f;
        postCollisionCoroutine = null;

        // 保持 isProcessingCollision = true，防止重置前再次碰撞
    }

    private void RestoreOriginalSpeedConfig()
    {
        if (controller != null && controller.config != null)
        {
            controller.config.maxSpeed = originalMaxSpeed;
            controller.config.motorTorque = originalTorque;
        }
    }

    private void ApplySpeedLimitByState()
    {
        if (vps == null) return;

        float speedLimit = originalMaxSpeed;
        float torqueMultiplier = 1f;

        switch (vps.state)
        {
            case VehicleState.Damaged:
                speedLimit = lightDamageMaxSpeed;
                torqueMultiplier = 0.7f;
                break;
            case VehicleState.HeavyDamaged:
                speedLimit = heavyDamageMaxSpeed;
                torqueMultiplier = 0.4f;
                break;
            case VehicleState.Destroyed:
                speedLimit = destroyedMaxSpeed;
                torqueMultiplier = 0f;
                break;
        }

        vps.SetSpeedLimit(speedLimit);
        vps.SetTorqueMultiplier(torqueMultiplier);
    }

    void OnApplicationQuit()
    {
        if (controller != null && controller.config != null)
        {
            controller.config.maxSpeed = originalMaxSpeed;
            controller.config.motorTorque = originalTorque;
        }
    }
}