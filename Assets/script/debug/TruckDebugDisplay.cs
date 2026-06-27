using UnityEngine;
using System.Collections.Generic;

public class TruckDebugDisplay : MonoBehaviour
{
    [Header("核心组件引用 (自动查找)")]
    public Truck_VehicleController controller;
    public Rigidbody rb;

    [Header("损坏模型 (按顺序拖入)")]
    public GameObject modelUndamaged;       // 完好模型
    public GameObject modelLightDamaged;    // 轻度损坏模型
    public GameObject modelHeavyDamaged;    // 重度损坏模型
    public GameObject modelDestroyed;       // 报废模型

    [Header("当前损坏状态")]
    [Range(0f, 1f)]
    public float damageAmount = 0f;         // 0=完好, 1=报废

    [Header("调试UI设置")]
    public bool showInGameUI = true;
    public Vector2 uiPosition = new Vector2(10, 10);
    public int fontSize = 14;

    [Header("运行时修正工具 (仅用于测试)")]
    [Tooltip("临时调整重心偏移X值（左负右正）")]
    public float debugCenterOfMassX = 0f;
    [Tooltip("输入死区（绝对值小于此值的水平输入将被置零）")]
    public float inputDeadzone = 0.05f;
    [Tooltip("强制同步所有车轴的左右轮摩擦力")]
    public bool forceSyncFrictions = false;
    [Tooltip("强制对称车轮中心X（基于左侧轮镜像）")]
    public bool forceMirrorWheelCenters = false;

    // 原始摩擦力存储
    private Dictionary<WheelCollider, WheelFrictionCurve> originalForwardFrictions = new Dictionary<WheelCollider, WheelFrictionCurve>();
    private Dictionary<WheelCollider, WheelFrictionCurve> originalSidewaysFrictions = new Dictionary<WheelCollider, WheelFrictionCurve>();
    private Vector3 originalCenterOfMass;

    // 损坏状态对应的性能因子 (可根据需求调整)
    private float[] damagePerformanceFactors = { 1f, 0.7f, 0.3f, 0f };
    private int currentDamageLevel = 0;  // 0=完好,1=轻度,2=重度,3=报废

    private void Start()
    {
        // 自动查找组件
        if (controller == null)
            controller = GetComponent<Truck_VehicleController>();
        if (rb == null)
            rb = GetComponent<Rigidbody>();

        if (controller != null && controller.axles != null)
        {
            foreach (var axle in controller.axles)
            {
                RecordWheelFriction(axle.leftWheel);
                RecordWheelFriction(axle.rightWheel);
            }
        }

        if (rb != null)
            originalCenterOfMass = rb.centerOfMass;
        else
            originalCenterOfMass = Vector3.zero;

        // 初始化模型显示
        UpdateDamageState();
    }

    private void Update()
    {
        if (controller == null || rb == null) return;

        // 根据damageAmount自动更新损坏等级和性能
        UpdateDamageState();

        // 运行时调试修正
        ApplyRuntimeFixes();
    }

    // 根据损坏程度切换模型并更新车辆性能
    private void UpdateDamageState()
    {
        int newLevel = 0;
        if (damageAmount >= 0.9f)
            newLevel = 3;
        else if (damageAmount >= 0.6f)
            newLevel = 2;
        else if (damageAmount >= 0.2f)
            newLevel = 1;
        else
            newLevel = 0;

        if (newLevel != currentDamageLevel)
        {
            currentDamageLevel = newLevel;
            // 切换模型
            SetActiveModel(currentDamageLevel);
            // 更新车辆性能 (调用原控制器中的方法)
            float factor = damagePerformanceFactors[currentDamageLevel];
            if (controller != null)
                controller.ApplyPerformance(factor);
        }
    }

    private void SetActiveModel(int level)
    {
        // 根据等级启用对应模型，禁用其他
        if (modelUndamaged) modelUndamaged.SetActive(level == 0);
        if (modelLightDamaged) modelLightDamaged.SetActive(level == 1);
        if (modelHeavyDamaged) modelHeavyDamaged.SetActive(level == 2);
        if (modelDestroyed) modelDestroyed.SetActive(level == 3);
    }

    private void ApplyRuntimeFixes()
    {
        // 重心修正
        if (Mathf.Abs(debugCenterOfMassX) > 0.001f)
        {
            Vector3 com = rb.centerOfMass;
            com.x = debugCenterOfMassX;
            rb.centerOfMass = com;
        }
        else if (rb.centerOfMass != originalCenterOfMass && originalCenterOfMass != Vector3.zero)
        {
            rb.centerOfMass = originalCenterOfMass;
        }

        // 强制摩擦力同步
        if (forceSyncFrictions)
            SyncAllWheelFrictions();

        // 强制对称车轮中心
        if (forceMirrorWheelCenters)
            MirrorWheelCenters();
    }

    private void RecordWheelFriction(WheelCollider wheel)
    {
        if (wheel == null) return;
        originalForwardFrictions[wheel] = wheel.forwardFriction;
        originalSidewaysFrictions[wheel] = wheel.sidewaysFriction;
    }

    private void SyncAllWheelFrictions()
    {
        if (controller?.axles == null) return;
        foreach (var axle in controller.axles)
        {
            if (axle.leftWheel == null || axle.rightWheel == null) continue;
            axle.rightWheel.forwardFriction = axle.leftWheel.forwardFriction;
            axle.rightWheel.sidewaysFriction = axle.leftWheel.sidewaysFriction;
        }
    }

    private void MirrorWheelCenters()
    {
        if (controller?.axles == null) return;
        foreach (var axle in controller.axles)
        {
            if (axle.leftWheel == null || axle.rightWheel == null) continue;
            Vector3 rightCenter = axle.rightWheel.center;
            rightCenter.x = -axle.leftWheel.center.x;
            axle.rightWheel.center = rightCenter;
        }
    }

    private void OnGUI()
    {
        if (!showInGameUI) return;
        if (controller == null || rb == null)
        {
            GUI.Box(new Rect(uiPosition.x, uiPosition.y, 300, 60), "TruckDebugDisplay: 缺少 Controller 或 Rigidbody 引用");
            return;
        }

        GUIStyle labelStyle = new GUIStyle(GUI.skin.label);
        labelStyle.fontSize = fontSize;
        labelStyle.richText = true;

        GUILayout.BeginArea(new Rect(uiPosition.x, uiPosition.y, 600, Screen.height - uiPosition.y - 20));
        GUILayout.BeginVertical("box");

        // ---- 损坏状态 ----
        GUILayout.Label("<b>=== 损坏系统 ===</b>", labelStyle);
        string[] damageNames = { "完好", "轻度损坏", "重度损坏", "报废" };
        GUILayout.Label($"损坏程度: {damageAmount:P0}  等级: {damageNames[currentDamageLevel]}", labelStyle);
        GUILayout.Label($"性能因子: {damagePerformanceFactors[currentDamageLevel]:F2}", labelStyle);

        // ---- 基础信息 ----
        GUILayout.Label("<b>=== 车辆动态 ===</b>", labelStyle);
        float speedKmh = rb.velocity.magnitude * 3.6f;
        GUILayout.Label($"速度: {speedKmh:F1} km/h", labelStyle);

        // 输入死区处理
        float rawSteer = Input.GetAxis("Horizontal");
        float deadSteer = Mathf.Abs(rawSteer) < inputDeadzone ? 0f : rawSteer;
        GUILayout.Label($"转向输入 (原始/死区后): {rawSteer:F2} / {deadSteer:F2}", labelStyle);

        float appliedSteer = 0f;
        foreach (var axle in controller.axles)
        {
            if (axle.isSteering && axle.leftWheel != null)
            {
                appliedSteer = axle.leftWheel.steerAngle;
                break;
            }
        }
        GUILayout.Label($"实际转向角度: {appliedSteer:F1}°", labelStyle);

        // ---- 车轴详细参数 ----
        GUILayout.Label("<b>=== 各车轴实时参数 ===</b>", labelStyle);
        for (int i = 0; i < controller.axles.Count; i++)
        {
            var axle = controller.axles[i];
            if (axle.leftWheel == null || axle.rightWheel == null) continue;

            GUILayout.Label($"--- 轴 {i + 1} {(axle.isMotor ? "[驱动]" : "")}{(axle.isSteering ? "[转向]" : "")}{(axle.isBraking ? "[刹车]" : "")} ---", labelStyle);

            GUILayout.Label($"  左轮扭矩: {axle.leftWheel.motorTorque:F0} Nm  右轮扭矩: {axle.rightWheel.motorTorque:F0} Nm", labelStyle);
            GUILayout.Label($"  左轮刹车: {axle.leftWheel.brakeTorque:F0} Nm  右轮刹车: {axle.rightWheel.brakeTorque:F0} Nm", labelStyle);

            // 悬挂压缩
            WheelHit hitL, hitR;
            float leftCompress = 0f, rightCompress = 0f;
            if (axle.leftWheel.GetGroundHit(out hitL)) leftCompress = (axle.leftWheel.suspensionDistance - hitL.force / axle.leftWheel.suspensionSpring.spring) * 100f;
            if (axle.rightWheel.GetGroundHit(out hitR)) rightCompress = (axle.rightWheel.suspensionDistance - hitR.force / axle.rightWheel.suspensionSpring.spring) * 100f;
            GUILayout.Label($"  悬挂压缩(%): 左 {leftCompress:F1}%  右 {rightCompress:F1}%", labelStyle);

            // 摩擦力刚度对比
            WheelFrictionCurve fwdL = axle.leftWheel.forwardFriction;
            WheelFrictionCurve fwdR = axle.rightWheel.forwardFriction;
            GUILayout.Label($"  前向摩擦力刚度: 左 {fwdL.stiffness:F2}  右 {fwdR.stiffness:F2} {(Mathf.Approximately(fwdL.stiffness, fwdR.stiffness) ? "(相同)" : "(不同)")}", labelStyle);

            WheelFrictionCurve sideL = axle.leftWheel.sidewaysFriction;
            WheelFrictionCurve sideR = axle.rightWheel.sidewaysFriction;
            GUILayout.Label($"  侧向摩擦力刚度: 左 {sideL.stiffness:F2}  右 {sideR.stiffness:F2} {(Mathf.Approximately(sideL.stiffness, sideR.stiffness) ? "(相同)" : "(不同)")}", labelStyle);

            GUILayout.Label($"  车轮中心X: 左 {axle.leftWheel.center.x:F2}  右 {axle.rightWheel.center.x:F2}  对称: {Mathf.Approximately(axle.leftWheel.center.x, -axle.rightWheel.center.x)}", labelStyle);
        }

        // ---- 刚体与重心 ----
        GUILayout.Label("<b>=== 物理参数 ===</b>", labelStyle);
        GUILayout.Label($"质量: {rb.mass:F1} kg", labelStyle);
        GUILayout.Label($"重心 (局部坐标): {rb.centerOfMass:F3}", labelStyle);
        if (rb.velocity.magnitude > 0.1f)
        {
            Vector3 lateralVel = transform.InverseTransformDirection(rb.velocity);
            GUILayout.Label($"横向速度 (右为正): {lateralVel.x:F2} m/s", labelStyle);
        }

        // ---- 控制器参数 ----
        GUILayout.Label("<b>=== 控制器参数 ===</b>", labelStyle);
        GUILayout.Label($"当前电机扭矩: {controller.currentMotorTorque:F0} Nm", labelStyle);
        GUILayout.Label($"当前最大转向角: {controller.currentSteerAngle:F1}°", labelStyle);

        // ---- 调试工具状态 ----
        GUILayout.Label("<b>=== 调试工具状态 ===</b>", labelStyle);
        GUILayout.Label($"输入死区: {inputDeadzone}", labelStyle);
        GUILayout.Label($"临时重心偏移X: {debugCenterOfMassX:F2} (在Inspector中调整)", labelStyle);
        GUILayout.Label($"强制同步摩擦力: {(forceSyncFrictions ? "开启" : "关闭")}", labelStyle);
        GUILayout.Label($"强制对称轮心: {(forceMirrorWheelCenters ? "开启" : "关闭")}", labelStyle);

        GUILayout.EndVertical();
        GUILayout.EndArea();
    }

    private void OnDrawGizmos()
    {
        if (rb == null) return;
        Gizmos.color = Color.cyan;
        Gizmos.DrawSphere(rb.worldCenterOfMass, 0.3f);
        Gizmos.color = Color.white;
        Gizmos.DrawWireSphere(rb.worldCenterOfMass, 0.35f);
    }
}