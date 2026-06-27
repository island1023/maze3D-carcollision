using UnityEngine;

/// <summary>
/// 挂载到树木根物体，为树干和树枝（Branch 标签）添加非触发器的碰撞体
/// </summary>
public class BatchAddTreeColliders : MonoBehaviour
{
    [Header("自动添加设置")]
    public bool autoAddOnStart = true;
    public bool verboseLog = true;
    [Tooltip("添加完成后自动销毁本脚本")]
    public bool destroyAfterAdd = true;

    [Header("碰撞体类型")]
    [Tooltip("优先尝试 MeshCollider (Convex)，失败则使用 BoxCollider")]
    public bool preferMeshCollider = true;
    [Tooltip("所有添加的碰撞体是否设为触发器（树枝需要物理碰撞，应设为 false）")]
    public bool setAsTrigger = false;   // 树枝必须为 false

    void Start()
    {
        if (autoAddOnStart) AddCollidersForTrunkAndBranches();
    }

    [ContextMenu("为树干和树枝添加碰撞体")]
    public void AddCollidersForTrunkAndBranches()
    {
        int totalMesh = 0, totalBox = 0, skippedNoMesh = 0, skippedHasCollider = 0;
        Transform root = transform;

        foreach (Transform child in root.GetComponentsInChildren<Transform>())
        {
            if (child == root) continue;

            // 只处理树干（无特殊标签）或 Branch 标签
            bool isTrunk = (child.parent == root && child.name.ToLower().Contains("trunk"));
            if (!isTrunk && !child.CompareTag("Branch")) continue;

            // 已有碰撞体则跳过
            if (child.GetComponent<Collider>() != null)
            {
                if (verboseLog) Debug.Log($"[树木碰撞体] {child.name} 已有碰撞体，跳过");
                skippedHasCollider++;
                continue;
            }

            MeshFilter mf = child.GetComponent<MeshFilter>();
            if (mf == null || mf.sharedMesh == null)
            {
                if (verboseLog) Debug.LogWarning($"[树木碰撞体] {child.name} 无有效网格，跳过");
                skippedNoMesh++;
                continue;
            }

            Mesh mesh = mf.sharedMesh;
            bool added = false;

            if (preferMeshCollider)
            {
                try
                {
                    MeshCollider mc = child.gameObject.AddComponent<MeshCollider>();
                    mc.convex = true;
                    mc.isTrigger = setAsTrigger;
                    if (mc.sharedMesh == null) throw new System.Exception("Convex 失败");
                    totalMesh++;
                    added = true;
                    if (verboseLog) Debug.Log($"[树木碰撞体] {child.name} 添加 MeshCollider (Convex)");
                }
                catch
                {
                    MeshCollider failed = child.GetComponent<MeshCollider>();
                    if (failed != null) DestroyImmediate(failed);
                }
            }

            if (!added)
            {
                BoxCollider box = child.gameObject.AddComponent<BoxCollider>();
                Bounds bounds = mesh.bounds;
                box.center = bounds.center;
                box.size = bounds.size;
                box.isTrigger = setAsTrigger;
                totalBox++;
                if (verboseLog) Debug.Log($"[树木碰撞体] {child.name} 改用 BoxCollider");
            }
        }

        Debug.Log($"===== 树枝碰撞体添加完成 =====\n" +
                  $"MeshCollider: {totalMesh}\n" +
                  $"BoxCollider: {totalBox}\n" +
                  $"已有碰撞体跳过: {skippedHasCollider}\n" +
                  $"无网格跳过: {skippedNoMesh}");

        if (destroyAfterAdd && Application.isPlaying)
            Destroy(this);
        else if (destroyAfterAdd && !Application.isPlaying)
            DestroyImmediate(this);
    }

    [ContextMenu("移除所有子物体的碰撞体")]
    public void RemoveAllColliders()
    {
        int removed = 0;
        foreach (Transform child in GetComponentsInChildren<Transform>())
        {
            if (child == transform) continue;
            Collider col = child.GetComponent<Collider>();
            if (col != null)
            {
                DestroyImmediate(col);
                removed++;
            }
        }
        Debug.Log($"已移除 {removed} 个碰撞体");
    }
}