using UnityEngine;

public class FractureMaterialFixer : MonoBehaviour
{
    [Header("原始未破碎模型（有材质的那个）")]
    public GameObject originalObject;

    [Header("破碎后的根节点（所有碎片父物体）")]
    public GameObject fractureRoot;

    [ContextMenu("修复碎片材质")]
    public void FixMaterials()
    {
        if (originalObject == null || fractureRoot == null)
        {
            Debug.LogError("请先指定 originalObject 和 fractureRoot");
            return;
        }

        MeshRenderer originalRenderer =
            originalObject.GetComponent<MeshRenderer>();

        if (originalRenderer == null)
        {
            Debug.LogError("原物体没有MeshRenderer");
            return;
        }

        Material[] mats = originalRenderer.sharedMaterials;

        int count = 0;

        foreach (Transform child in fractureRoot.transform)
        {
            MeshRenderer mr = child.GetComponent<MeshRenderer>();

            if (mr != null)
            {
                mr.sharedMaterials = mats;
                count++;
            }
        }

        Debug.Log($"材质修复完成，共修复碎片：{count}");
    }
}