using UnityEngine;
using System.Collections;

public class CollisionDebugger : MonoBehaviour
{
    [Header("监控对象")]
    public SedanCollisionManager sedanCollision;
    public VehicleController controller;
    public Rigidbody rb;

    [Header("调试输出")]
    public bool showGUI = true;
    public Vector2 guiPosition = new Vector2(20, 20);

    private bool forceUnstuck = false;

    void Start()
    {
        if (sedanCollision == null)
            sedanCollision = GetComponent<SedanCollisionManager>();
        if (controller == null)
            controller = GetComponent<VehicleController>();
        if (rb == null)
            rb = GetComponent<Rigidbody>();
    }

    void Update()
    {
        // 按 R 键强制解除卡死
        if (Input.GetKeyDown(KeyCode.R))
        {
            ForceUnstuck();
        }
    }

    void ForceUnstuck()
    {
        Debug.LogWarning("=== 强制解除卡死 ===");
        // 尝试恢复时间
        Time.timeScale = 1f;
        // 尝试重新启用控制器
        if (controller != null)
        {
            controller.enabled = true;
            Debug.Log("已重新启用 VehicleController");
        }
        // 通过反射重置 SedanCollisionManager 中的 isProcessingCollision 标志
        if (sedanCollision != null)
        {
            var flags = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
            var field = typeof(SedanCollisionManager).GetField("isProcessingCollision", flags);
            if (field != null)
            {
                field.SetValue(sedanCollision, false);
                Debug.Log("已重置 SedanCollisionManager.isProcessingCollision");
            }
            else
            {
                Debug.LogError("未找到 isProcessingCollision 字段");
            }
        }
        // 如果 rigidbody 被冻结，解除
        if (rb != null)
        {
            rb.constraints = RigidbodyConstraints.None;
            rb.constraints = RigidbodyConstraints.FreezeRotation | RigidbodyConstraints.FreezePositionY; // 根据需要
            Debug.Log("已重置刚体约束");
        }
        forceUnstuck = true;
    }

    void OnGUI()
    {
        if (!showGUI) return;
        GUILayout.BeginArea(new Rect(guiPosition.x, guiPosition.y, 350, 300));
        GUILayout.Box("=== 碰撞调试 ===");
        if (sedanCollision != null)
        {
            // 获取私有字段值
            var flags = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
            var field = typeof(SedanCollisionManager).GetField("isProcessingCollision", flags);
            bool isProcessing = false;
            if (field != null) isProcessing = (bool)field.GetValue(sedanCollision);
            GUILayout.Label($"isProcessingCollision: {isProcessing}");
        }
        if (controller != null)
            GUILayout.Label($"Controller enabled: {controller.enabled}");
        GUILayout.Label($"Time.timeScale: {Time.timeScale}");
        if (rb != null)
            GUILayout.Label($"Rigidbody velocity: {rb.velocity}");
        GUILayout.Space(10);
        GUILayout.Label("按 R 键强制解除卡死");
        GUILayout.EndArea();
    }
}