using UnityEngine;

/// <summary>
/// 挂载到树木根物体，自动根据名称设置 Branch / Leaf 标签
/// </summary>
[ExecuteAlways]
public class TreeTagInitializer : MonoBehaviour
{
    [Header("名称关键字（不区分大小写）")]
    public string branchKeyword = "branch";
    public string leafKeyword = "leaf";

    [Header("执行设置")]
    public bool runOnStart = true;
    public bool includeInactive = true;
    [Tooltip("设置完成后自动销毁本脚本（仅在运行时有效，编辑模式下不会自动销毁）")]
    public bool destroyAfterSetup = true;

    void Start()
    {
        if (runOnStart)
            SetTags();
    }

    [ContextMenu("立即设置标签（并销毁脚本）")]
    public void SetTagsAndDestroy()
    {
        SetTags();
        if (Application.isPlaying)
            Destroy(this);
        else
            DestroyImmediate(this);
    }

    [ContextMenu("立即设置标签（保留脚本）")]
    public void SetTags()
    {
        int branchCount = 0, leafCount = 0;
        Transform[] allChildren = GetComponentsInChildren<Transform>(includeInactive);
        foreach (Transform child in allChildren)
        {
            if (child == transform) continue;
            string nameLower = child.name.ToLower();
            if (nameLower.Contains(branchKeyword.ToLower()))
            {
                child.tag = "Branch";
                branchCount++;
            }
            else if (nameLower.Contains(leafKeyword.ToLower()))
            {
                child.tag = "Leaf";
                leafCount++;
            }
        }
        Debug.Log($"树木标签设置完成 - Branch: {branchCount}, Leaf: {leafCount}");
        if (destroyAfterSetup && Application.isPlaying)
            Destroy(this);
    }

    [ContextMenu("清除所有标签")]
    public void ClearTags()
    {
        int cleared = 0;
        foreach (Transform child in GetComponentsInChildren<Transform>(includeInactive))
        {
            if (child == transform) continue;
            if (child.tag == "Branch" || child.tag == "Leaf")
            {
                child.tag = "Untagged";
                cleared++;
            }
        }
        Debug.Log($"已清除 {cleared} 个标签");
    }
}