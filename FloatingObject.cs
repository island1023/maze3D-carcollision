using UnityEngine;

public class FloatingObject : MonoBehaviour
{
    // --- 旋转设置 ---
    [Header("旋转设置")]
    [Tooltip("每秒绕Y轴旋转的角度")]
    public float rotationSpeed = 50f;

    // --- 悬浮设置 ---
    [Header("悬浮设置")]
    [Tooltip("上下的浮动速度")]
    public float floatSpeed = 1f;
    [Tooltip("最大的浮动距离（从起始位置计算）")]
    public float floatAmplitude = 0.5f;

    // 存储起始位置
    private Vector3 startPosition;

    void Start()
    {
        // 记录物体最初的坐标
        startPosition = transform.position;
    }

    void Update()
    {
        // 1. 旋转
        // Time.deltaTime 确保旋转速度与帧率无关
        transform.Rotate(Vector3.up, rotationSpeed * Time.deltaTime, Space.World);

        // 2. 悬浮
        // Mathf.Sin 函数会返回一个 -1 到 1 之间的平滑周期值
        float newY = Mathf.Sin(Time.time * floatSpeed) * floatAmplitude + startPosition.y;

        // 设置新的位置
        transform.position = new Vector3(startPosition.x, newY, startPosition.z);
    }
}
