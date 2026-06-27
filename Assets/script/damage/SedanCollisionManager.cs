using UnityEngine;
using System.Collections;
using System.Collections.Generic;

[RequireComponent(typeof(VehiclePerformanceSystem))]
[RequireComponent(typeof(Rigidbody))]
public class SedanCollisionManager : MonoBehaviour
{
    private VehiclePerformanceSystem vps;
    private Rigidbody rb;
    private VehicleController controller;

    [Header("--- 碰撞后退参数 ---")]
    public float backwardBoost = 1.5f;
    public float bounciness = 0.2f;
    public float destroyedBackwardDistance = 3f;
    public float extraBackwardDistance = 0.5f;

    [Header("--- 树木碰撞专用参数 ---")]
    public float treeExtraBackwardDistance = 1.5f;

    [Header("--- 卡车碰撞专用参数 ---")]
    [Tooltip("撞击卡车车厢时的强制后退距离（米）")]
    public float truckBackwardDistance = 2.5f;
    [Tooltip("撞击卡车车厢时的后退速度倍数")]
    public float truckBackwardBoost = 3f;
    [Tooltip("撞击卡车的最低伤害（进入报废/显示 car_rollover）")]
    public float truckMinDamage = 90f;
    [Tooltip("撞击卡车的最高伤害")]
    public float truckMaxDamage = 100f;
    [Tooltip("两次卡车碰撞之间的最短间隔（秒）")]
    public float truckHitCooldown = 2f;

    [Header("--- 碰撞后临时忽略碰撞 ---")]
    public float postCollisionIgnoreTime = 0.8f;

    [Header("--- 碰撞后控制器禁用时间 ---")]
    public float controllerDisableTime = 0.6f;

    [Header("--- 状态最高速度 ---")]
    public float lightDamageMaxSpeed = 70f;
    public float heavyDamageMaxSpeed = 30f;
    public float destroyedMaxSpeed = 0f;

    [Header("--- 伤害计算参数 ---")]
    public float minSpeedForDamage = 2f;
    public float maxSpeedForDamage = 20f;
    public float minDamage = 5f;
    public float maxDamagePerHit = 40f;

    [Header("调试")]
    public bool showDebugLog = true;
    public bool verboseDebug = false;

    private float originalMaxSpeed;
    private float originalTorque;
    private bool isProcessingCollision = false;
    private float lastHitTime = 0f;

    private Collider carCollider;
    private Collider treeCollider;
    private bool isIgnoring = false;
    private bool isLimitingForwardSpeed = false;
    private bool lockDrivingAfterCollision = false;
    private bool collisionFrozenUntilReset = false;
    private bool wasKinematicBeforeFreeze = false;
    private Transform ignoredTruckRoot;
    private int frameCounter = 0;

    private Vector3 startPosition;
    private Quaternion startRotation;
    private VehicleAppearance vehicleAppearance;

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
        carCollider = GetComponent<Collider>();
        vehicleAppearance = GetComponent<VehicleAppearance>();

        if (controller != null && controller.config != null)
        {
            originalMaxSpeed = controller.config.maxSpeed;
            originalTorque = controller.config.motorTorque;
        }

        if (controller == null)
            Debug.LogWarning("VehicleController 未找到，碰撞管理器将无法正确控制车辆");
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

        if (controller != null)
        {
            controller.PrepareForDriving();
            controller.enabled = true;
        }

        rb.Sleep();
        isProcessingCollision = false;
        isLimitingForwardSpeed = false;
        isIgnoring = false;
        lockDrivingAfterCollision = false;
        collisionFrozenUntilReset = false;
        RestoreTruckCollisions();
        if (rb != null)
            rb.isKinematic = wasKinematicBeforeFreeze;
        lastHitTime = 0f;
        treeCollider = null;

        if (carCollider != null && treeCollider != null)
            Physics.IgnoreCollision(carCollider, treeCollider, false);
    }

    void FixedUpdate()
    {
        if (collisionFrozenUntilReset && rb != null)
        {
            rb.velocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
            return;
        }

        if (!isLimitingForwardSpeed || rb == null)
            return;

        // 碰撞后全程抑制前进动量（不依赖 controller.enabled）
        Vector3 localVel = transform.InverseTransformDirection(rb.velocity);
        if (localVel.z > 0.01f)
        {
            if (showDebugLog && verboseDebug && Time.frameCount - frameCounter > 60)
            {
                Debug.LogWarning($"[限速] 清除前进速度 {localVel.z:F3} m/s");
                frameCounter = Time.frameCount;
            }
            localVel.z = 0f;
            rb.velocity = transform.TransformDirection(localVel);
        }

        bool controllerManagingInput = controller != null
            && controller.enabled
            && !controller.externalControlLock;

        if (!controllerManagingInput)
            ApplyEmergencyBraking();
    }

    void OnCollisionEnter(Collision collision)
    {
        TriggerCarriageDeformation(collision);

        if (collisionFrozenUntilReset)
            return;

        string targetTag = ResolveCollisionTag(collision.gameObject);
        bool isTruckHit = targetTag == "TruckBack";

        if (isTruckHit && Time.time - lastHitTime < truckHitCooldown)
            return;

        if (!isTruckHit)
        {
            if (isProcessingCollision) return;
            if (Time.time - lastHitTime < 0.3f) return;
            if (targetTag != "Streetlight" && targetTag != "Tree") return;
        }
        else if (isProcessingCollision)
        {
            // 已在处理同一次卡车碰撞，避免重复后退导致前后弹动
            return;
        }

        if (showDebugLog)
        {
            Vector3 localVel = transform.InverseTransformDirection(rb.velocity);
            Debug.Log($"[碰撞前] 目标={targetTag} 物体={collision.gameObject.name} 局部速度: ({localVel.x:F2},{localVel.y:F2},{localVel.z:F2})");
        }

        lastHitTime = Time.time;
        isProcessingCollision = true;

        DamageData collisionData = new DamageData
        {
            impactForce = collision.impulse.magnitude / Time.fixedDeltaTime,
            hitPoint = collision.contacts[0].point,
            hitNormal = collision.contacts[0].normal,
            victim = gameObject,
            hitPart = collision.contacts[0].thisCollider
        };
        SimulationEvents.TriggerVehicleDamage(collisionData);

        // 控制器锁定与扭矩清零
        if (controller != null)
        {
            controller.externalControlLock = true;
            controller.ClearWheelTorque();
            controller.enabled = false;
            if (showDebugLog) Debug.Log("[碰撞] 控制器锁定输入，清除扭矩");
        }
        isLimitingForwardSpeed = true;

        if (targetTag == "Tree" || targetTag == "Streetlight")
            treeCollider = collision.collider;

        // 伤害计算
        float damageToAdd = CalculateDamageFromCollision(collision, targetTag);
        if (isTruckHit)
        {
            float speed = collision.relativeVelocity.magnitude;
            float t = Mathf.InverseLerp(minSpeedForDamage, maxSpeedForDamage, Mathf.Max(speed, minSpeedForDamage));
            float targetDamage = Mathf.Lerp(truckMinDamage, truckMaxDamage, t);
            if (vps.damage < targetDamage)
                damageToAdd = targetDamage - vps.damage;
        }

        if (damageToAdd > 0)
        {
            vps.AddDamage(damageToAdd);
            Debug.Log($"[碰撞] 撞击 {targetTag}，速度 {collision.relativeVelocity.magnitude:F1} m/s，增加伤害 {damageToAdd:F1}，总伤害 {vps.damage:F1}，状态 {vps.state}");
        }

        if (isTruckHit && vehicleAppearance != null)
            vehicleAppearance.ForceShowRolloverModel();

        bool isDestroyed = (vps.state == VehicleState.Destroyed);
        lockDrivingAfterCollision = isDestroyed;
        Vector3 normal = collision.contacts[0].normal;

        if (isTruckHit)
        {
            IgnoreCollisionsWithTruck(collision);
        }

        // 根据碰撞类型选择后退距离和冲量系数
        float backwardDist;
        float boost;

        if (targetTag == "TruckBack")
        {
            backwardDist = truckBackwardDistance;
            boost = truckBackwardBoost;
            if (showDebugLog) Debug.Log($"[卡车碰撞] 使用专用后退距离 {backwardDist} 米，冲量系数 {boost}");
        }
        else if (targetTag == "Tree")
        {
            backwardDist = treeExtraBackwardDistance;
            boost = backwardBoost;
        }
        else
        {
            backwardDist = extraBackwardDistance;
            boost = backwardBoost;
        }

        if (!isDestroyed)
        {
            ApplyCollisionKnockback(collision, normal, boost, backwardDist);

            if (showDebugLog)
            {
                Vector3 afterLocal = transform.InverseTransformDirection(rb.velocity);
                Debug.Log($"[碰撞后] 后退速度局部Z: {afterLocal.z:F2}, 后退距离: {backwardDist}");
            }
        }
        else
        {
            rb.velocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
            Vector3 targetPosition = transform.position + normal * destroyedBackwardDistance;
            rb.position = targetPosition;

            wasKinematicBeforeFreeze = rb.isKinematic;
            rb.isKinematic = true;
            collisionFrozenUntilReset = true;

            if (showDebugLog) Debug.Log("[碰撞] 报废，冻结物理并忽略卡车碰撞");
        }

        ApplySpeedLimitByState();
        StartCoroutine(PostCollisionCooldown(isTruckHit, isDestroyed));
    }

    private IEnumerator PostCollisionCooldown(bool isTruckHit, bool keepTruckIgnore)
    {
        if (treeCollider != null && carCollider != null && !isIgnoring)
        {
            isIgnoring = true;
            Physics.IgnoreCollision(carCollider, treeCollider, true);
            if (showDebugLog) Debug.Log("[冷却] 临时忽略与树木/路灯的碰撞");
        }

        yield return new WaitForSeconds(postCollisionIgnoreTime);

        if (treeCollider != null && carCollider != null)
        {
            Physics.IgnoreCollision(carCollider, treeCollider, false);
            isIgnoring = false;
        }

        if (isTruckHit && !keepTruckIgnore)
            RestoreTruckCollisions();

        yield return new WaitForSeconds(controllerDisableTime);

        // 恢复前强制清零速度（报废且已冻结时跳过，避免与 kinematic 冲突）
        if (!collisionFrozenUntilReset)
        {
            rb.velocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
        }

        if (controller != null)
        {
            controller.enabled = true;
            if (lockDrivingAfterCollision)
            {
                controller.LockDrivingUntilReset();
                if (showDebugLog) Debug.Log("[恢复] 车辆报废，锁定驾驶，按 Tab 重置");
            }
            else
            {
                controller.ResetInputState();
                if (showDebugLog) Debug.Log("[恢复] 碰撞结束，请松开按键后再驾驶");
            }
        }

        isLimitingForwardSpeed = false;

        yield return new WaitForSeconds(0.2f);
        isProcessingCollision = false;
        treeCollider = null;
    }

    private void IgnoreCollisionsWithTruck(Collision collision)
    {
        if (collision == null)
            return;

        ignoredTruckRoot = collision.transform.root;
        Collider[] selfColliders = GetComponentsInChildren<Collider>();
        Collider[] truckColliders = ignoredTruckRoot.GetComponentsInChildren<Collider>();

        foreach (Collider self in selfColliders)
        {
            if (self == null || self.isTrigger)
                continue;

            foreach (Collider other in truckColliders)
            {
                if (other == null || other.isTrigger)
                    continue;

                Physics.IgnoreCollision(self, other, true);
            }
        }
    }

    private void RestoreTruckCollisions()
    {
        if (ignoredTruckRoot == null)
            return;

        Collider[] selfColliders = GetComponentsInChildren<Collider>();
        Collider[] truckColliders = ignoredTruckRoot.GetComponentsInChildren<Collider>();

        foreach (Collider self in selfColliders)
        {
            if (self == null || self.isTrigger)
                continue;

            foreach (Collider other in truckColliders)
            {
                if (other == null || other.isTrigger)
                    continue;

                Physics.IgnoreCollision(self, other, false);
            }
        }

        ignoredTruckRoot = null;
    }

    private float CalculateDamageFromCollision(Collision collision, string targetTag)
    {
        float speed = collision.relativeVelocity.magnitude;

        if (targetTag == "TruckBack")
        {
            float t = Mathf.InverseLerp(minSpeedForDamage, maxSpeedForDamage, Mathf.Max(speed, minSpeedForDamage));
            float targetDamage = Mathf.Lerp(truckMinDamage, truckMaxDamage, t);
            return Mathf.Max(0f, targetDamage - vps.damage);
        }

        if (speed < minSpeedForDamage) return 0f;

        float impactT = Mathf.InverseLerp(minSpeedForDamage, maxSpeedForDamage, speed);
        float baseDamage = Mathf.Lerp(minDamage, maxDamagePerHit, impactT);
        if (targetTag == "Tree") baseDamage *= 1.2f;

        return Mathf.Clamp(baseDamage, minDamage, maxDamagePerHit);
    }

    private static string ResolveCollisionTag(GameObject hitObject)
    {
        if (TruckTrailerDeformation.IsCarriageObject(hitObject))
            return "TruckBack";

        Transform current = hitObject.transform;
        while (current != null)
        {
            if (current.CompareTag("TruckBack")) return "TruckBack";
            if (current.CompareTag("Streetlight")) return "Streetlight";
            if (current.CompareTag("Tree")) return "Tree";
            current = current.parent;
        }

        return hitObject.tag;
    }

    private static void TriggerCarriageDeformation(Collision collision)
    {
        var deformation = TruckTrailerDeformation.FindOnHierarchy(collision.gameObject);
        if (deformation == null)
        {
            if (TruckTrailerDeformation.IsCarriageObject(collision.gameObject))
                Debug.LogWarning($"[车厢变形] 撞到了车厢 {collision.gameObject.name}，但未找到 TruckTrailerDeformation 组件！");
            return;
        }

        deformation.TryApplyImpact(collision);
    }

    private void ApplyCollisionKnockback(Collision collision, Vector3 normal, float boost, float backwardDist)
    {
        Vector3 impulse = collision.impulse;
        Vector3 deltaV = impulse / rb.mass;
        Vector3 incomingVelocity = rb.velocity;
        float vN = Vector3.Dot(incomingVelocity, normal);
        Vector3 vT = incomingVelocity - normal * vN;
        Vector3 newNormalVelocity = -normal * vN * bounciness;
        Vector3 finalVelocity = vT + newNormalVelocity + deltaV * 0.5f;

        // 在车辆局部坐标中消除前进分量，并施加后退冲量（避免法线方向错误时反而加速前进）
        Vector3 localVel = transform.InverseTransformDirection(finalVelocity);
        if (localVel.z > 0f)
            localVel.z = 0f;
        localVel.z -= boost * Mathf.Abs(vN) * 0.5f;
        rb.velocity = transform.TransformDirection(localVel);

        Vector3 knockbackDir = normal.sqrMagnitude > 0.0001f ? normal.normalized : -transform.forward;
        if (Vector3.Dot(knockbackDir, transform.forward) > 0f)
            knockbackDir = -transform.forward;
        rb.MovePosition(rb.position + knockbackDir * backwardDist);
    }

    private void ApplyEmergencyBraking()
    {
        if (controller == null || controller.config == null)
            return;

        controller.ClearWheelTorque();
        float brake = controller.config.brakeForce;
        if (controller.FL == null)
            return;

        controller.FL.brakeTorque = brake;
        controller.FR.brakeTorque = brake;
        controller.RL.brakeTorque = brake;
        controller.RR.brakeTorque = brake;
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

        if (vps.state == VehicleState.Normal)
        {
            vps.SetSpeedLimit(-1f);
            vps.SetTorqueMultiplier(1f);
            return;
        }

        float speedLimit = originalMaxSpeed;
        float torqueMultiplier = 1f;
        switch (vps.state)
        {
            case VehicleState.Damaged:
                speedLimit = Mathf.Min(originalMaxSpeed, lightDamageMaxSpeed);
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