using UnityEngine;

public class VehicleSpeedometer : MonoBehaviour
{
    private Rigidbody rb;

    [Tooltip("单位：m/s (米每秒)")]
    public float currentSpeedMS;
    [Tooltip("单位：km/h (千米每小时)")]
    public float currentSpeedKMH;

    void Start()
    {
        rb = GetComponent<Rigidbody>();
    }

    void Update()
    {
        if (rb != null)
        {
            // 获取当前实际物理速度
            currentSpeedMS = rb.velocity.magnitude;

            // 转换为 km/h (1 m/s = 3.6 km/h)
            currentSpeedKMH = currentSpeedMS * 3.6f;
        }
    }

    // 可视化显示，直接挂在车身上，运行时在 Scene 窗口可见
    void OnDrawGizmos()
    {
        if (rb != null)
        {
            UnityEditor.Handles.Label(transform.position + Vector3.up * 2f,
                $"Speed: {currentSpeedKMH:F1} km/h");
        }
    }
}