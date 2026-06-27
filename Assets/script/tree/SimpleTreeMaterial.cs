using UnityEngine;

public class SimpleTreeMaterial : MonoBehaviour
{
    [Header("请直接把做好的材质球拖到这里：")]
    public Material trunkMaterial;   // 主干材质
    public Material branchMaterial;  // 枝干材质
    public Material leafMaterial;    // 树叶材质

    // 这个属性会在脚本组件的右键菜单里生成一个按钮
    [ContextMenu("⚡ 点击这里一键替换材质 ⚡")]
    public void ReplaceAllMaterials()
    {
        // 瞬间获取树木下的所有子物体渲染器
        MeshRenderer[] allRenderers = GetComponentsInChildren<MeshRenderer>(true);
        int count = 0;

        foreach (MeshRenderer render in allRenderers)
        {
            // 根据名字直接塞材质
            if (render.gameObject.name.Contains("Leaf") && leafMaterial != null)
            {
                render.sharedMaterial = leafMaterial;
                count++;
            }
            else if (render.gameObject.name.Contains("Branch") && branchMaterial != null)
            {
                render.sharedMaterial = branchMaterial;
                count++;
            }
            else if (render.gameObject.name.Contains("Trunk") && trunkMaterial != null)
            {
                render.sharedMaterial = trunkMaterial;
                count++;
            }
        }

        Debug.Log($"✅ 替换完成！瞬间搞定 {count} 个碎块的材质。");
    }
}