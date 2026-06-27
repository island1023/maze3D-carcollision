using UnityEngine;

[RequireComponent(typeof(MeshFilter))]
public class LightPoleMeasure : MonoBehaviour
{
    [Header("测量结果（只读）")]
    [SerializeField] private Vector3 actualSize;       // 实际大小（米）
    [SerializeField] private float height;             // 高度（米）
    [SerializeField] private float width;              // 宽度（米）
    [SerializeField] private float depth;              // 深度（米）
    [SerializeField] private float diameter;           // 直径（假设为最宽处的近似值）

    void OnValidate()
    {
        Measure();
    }

    [ContextMenu("测量并输出尺寸")]
    public void Measure()
    {
        MeshFilter mf = GetComponent<MeshFilter>();
        if (mf == null || mf.sharedMesh == null)
        {
            Debug.LogError("没有 MeshFilter 或网格！");
            return;
        }

        // 获取局部包围盒大小（原始模型尺寸，不受缩放影响）
        Vector3 localSize = mf.sharedMesh.bounds.size;
        // 考虑物体自身的缩放
        Vector3 worldScale = transform.lossyScale;
        actualSize = new Vector3(localSize.x * worldScale.x, localSize.y * worldScale.y, localSize.z * worldScale.z);

        height = actualSize.y;
        width = actualSize.x;
        depth = actualSize.z;
        // 直径取宽度和深度中的较大值（近似）
        diameter = Mathf.Max(width, depth);

        Debug.Log($"=== 路灯实际尺寸 ===\n" +
                  $"高度(Height): {height:F2} 米\n" +
                  $"宽度(Width): {width:F2} 米\n" +
                  $"深度(Depth): {depth:F2} 米\n" +
                  $"近似直径: {diameter:F2} 米\n" +
                  $"整体包围盒: {actualSize}");
    }
}