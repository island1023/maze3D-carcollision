using UnityEngine;

public class VehiclePerformanceSystem : MonoBehaviour
{
    [Header("--- 车辆控制器 (自动获取，或拖拽对应的一个即可) ---")]
    [Tooltip("原版小轿车控制器（保留原变量名以兼容碰撞系统等其他脚本）")]
    public VehicleController controller;
    public Truck_VehicleController truckController;

    [Header("--- 状态数据 ---")]
    public float damage;
    public VehicleState state;
    public float maxDamage = 100f;

    private float originalMaxSpeed;
    private float originalMotorTorque;
    private float originalSteerAngle;

    private float customSpeedLimit = -1f;
    private float customTorqueMultiplier = 1f;

    private void Start()
    {
        // 自动尝试获取身上挂载的控制器（无需手动拖拽）
        if (controller == null) controller = GetComponent<VehicleController>();
        if (truckController == null) truckController = GetComponent<Truck_VehicleController>();

        // 判断是哪种车，并记录初始配置参数
        if (controller != null && controller.config != null)
        {
            originalMaxSpeed = controller.config.maxSpeed;
            originalMotorTorque = controller.config.motorTorque;
            originalSteerAngle = controller.config.steerAngle;

            controller.currentMaxSpeed = originalMaxSpeed;
            controller.currentMotorTorque = originalMotorTorque;
            controller.currentSteerAngle = originalSteerAngle;
        }
        else if (truckController != null && truckController.config != null)
        {
            originalMaxSpeed = truckController.config.maxSpeed;
            originalMotorTorque = truckController.config.motorTorque;
            originalSteerAngle = truckController.config.steerAngle;

            truckController.currentMaxSpeed = originalMaxSpeed;
            truckController.currentMotorTorque = originalMotorTorque;
            truckController.currentSteerAngle = originalSteerAngle;
        }
    }

    public void AddDamage(float value)
    {
        damage += value;
        damage = Mathf.Clamp(damage, 0, maxDamage);
        UpdateState();
        ApplyPerformancePenalty();
    }

    private void UpdateState()
    {
        if (damage < 30) state = VehicleState.Normal;
        else if (damage < 60) state = VehicleState.Damaged;
        else if (damage < 90) state = VehicleState.HeavyDamaged;
        else state = VehicleState.Destroyed;

        // 如果被摧毁，关闭对应车辆的控制权
        if (state == VehicleState.Destroyed)
        {
            if (controller != null) controller.enabled = false;
            if (truckController != null) truckController.enabled = false;
        }
    }

    private void ApplyPerformancePenalty()
    {
        if (controller == null && truckController == null) return;

        // 计算衰减系数
        float damageFactor = 1f - damage / maxDamage;
        float newTorque = originalMotorTorque * damageFactor;
        float newSteer = originalSteerAngle * damageFactor;
        float newMaxSpeed = originalMaxSpeed * damageFactor;

        if (customSpeedLimit >= 0)
            newMaxSpeed = Mathf.Min(newMaxSpeed, customSpeedLimit);

        newTorque *= customTorqueMultiplier;

        // 将衰减后的数值应用到当前有效的控制器上
        if (controller != null)
        {
            controller.currentMotorTorque = newTorque;
            controller.currentSteerAngle = newSteer;
            controller.currentMaxSpeed = newMaxSpeed;
        }
        else if (truckController != null)
        {
            truckController.currentMotorTorque = newTorque;
            truckController.currentSteerAngle = newSteer;
            truckController.currentMaxSpeed = newMaxSpeed;
        }
    }

    public void Repair()
    {
        damage = 0;
        customSpeedLimit = -1f;
        customTorqueMultiplier = 1f;
        UpdateState();

        // 恢复控制权
        if (controller != null) controller.enabled = true;
        if (truckController != null) truckController.enabled = true;

        ApplyPerformancePenalty();
    }

    public void SetSpeedLimit(float limit)
    {
        customSpeedLimit = limit;
        ApplyPerformancePenalty();
    }

    public void SetTorqueMultiplier(float multiplier)
    {
        customTorqueMultiplier = Mathf.Clamp01(multiplier);
        ApplyPerformancePenalty();
    }

    private void OnApplicationQuit()
    {
        // 退出时恢复原始配置，防止 ScriptableObject 数据被永久污染
        if (controller != null && controller.config != null)
        {
            controller.config.maxSpeed = originalMaxSpeed;
            controller.config.motorTorque = originalMotorTorque;
            controller.config.steerAngle = originalSteerAngle;
        }
        else if (truckController != null && truckController.config != null)
        {
            truckController.config.maxSpeed = originalMaxSpeed;
            truckController.config.motorTorque = originalMotorTorque;
            truckController.config.steerAngle = originalSteerAngle;
        }
    }
}