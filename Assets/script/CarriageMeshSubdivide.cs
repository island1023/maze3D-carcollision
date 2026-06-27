using UnityEngine;

/// <summary>
/// 挂在车厢外壳物体上（Refrigerator_SZ38ft_exterior_LOD0），
/// 在 Inspector 点按钮即可细分网格，不用 Blender。
/// </summary>
[RequireComponent(typeof(MeshFilter))]
[AddComponentMenu("变形/车厢网格细分")]
public class CarriageMeshSubdivide : MonoBehaviour
{
    [Tooltip("细分次数：1=快，2=推荐，3=更圆滑但更卡")]
    [Range(1, 3)]
    public int subdivideIterations = 2;

    [Header("Scene 预览")]
    [Tooltip("选中物体时在 Scene 里画绿色线框，不用找 Shaded Wireframe 菜单")]
    public bool showGreenWireframe = true;

    [Header("状态（只读）")]
    public int vertexCountBefore;
    public int vertexCountAfter;

    void OnDrawGizmosSelected()
    {
        if (!showGreenWireframe) return;

        var mf = GetComponent<MeshFilter>();
        if (mf == null || mf.sharedMesh == null) return;

        Gizmos.color = new Color(0.2f, 1f, 0.2f, 1f);
        Gizmos.matrix = transform.localToWorldMatrix;
        Gizmos.DrawWireMesh(mf.sharedMesh);
    }

    public bool ApplyToMeshFilter(Mesh newMesh)
    {
        var mf = GetComponent<MeshFilter>();
        if (mf == null) return false;
        mf.sharedMesh = newMesh;
        return true;
    }

    public bool TrySubdivide(out string message)
    {
        var mf = GetComponent<MeshFilter>();
        if (mf == null || mf.sharedMesh == null)
        {
            message = "没有 MeshFilter 或网格为空！";
            return false;
        }

        Mesh source = mf.sharedMesh;
        vertexCountBefore = source.vertexCount;

        if (!source.isReadable)
        {
            message = "网格不可读！请选中 truck.fbx → 勾选 Read/Write Enabled → Apply";
            return false;
        }

        Mesh result = CarriageMeshSubdivideUtility.Subdivide(source, subdivideIterations);
        vertexCountAfter = result.vertexCount;

        if (!ApplyToMeshFilter(result))
        {
            message = "应用网格失败！";
            return false;
        }

        message = $"细分成功：{vertexCountBefore} → {vertexCountAfter} 顶点";
        return true;
    }
}
