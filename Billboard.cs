using UnityEngine;

public class Billboard : MonoBehaviour
{
    // 在 Unity 启动时缓存主摄像机
    private Transform mainCameraTransform;

    void Start()
    {
        // 查找主摄像机
        if (Camera.main != null)
        {
            mainCameraTransform = Camera.main.transform;
        }
        else
        {
            Debug.LogError("场景中没有找到带有 'MainCamera' 标签的摄像机!");
            enabled = false; // 禁用脚本
        }
    }

    // 在 LateUpdate 中更新以避免角色移动带来的抖动
    void LateUpdate()
    {
        if (mainCameraTransform == null) return;

        // 使血条的 Z 轴（前方）始终指向摄像机
        // LookAt 会使血条正面面向摄像机，但是可能会上下颠倒
        // 因此我们使用更精确的方法：

        // 旋转血条，使其Y轴（上）保持垂直，X/Z平面（水平）始终面向摄像机
        transform.rotation = Quaternion.LookRotation(
            transform.position - mainCameraTransform.position, Vector3.up);

        // 如果您希望血条完全保持垂直，不随摄像机倾斜，可以使用以下代码：
        // transform.rotation = Quaternion.Euler(0f, mainCameraTransform.eulerAngles.y, 0f);
    }
}