using UnityEngine;

/// <summary>
/// 挂到车厢根物体上，Play 时自动检查变形前置条件。
/// </summary>
public class MeshDeformChecker : MonoBehaviour
{
    public TruckTrailerDeformation deformation;

    void Start()
    {
        if (deformation == null)
            deformation = GetComponent<TruckTrailerDeformation>();

        if (deformation == null)
        {
            Debug.LogError("[检查] 缺少 TruckTrailerDeformation 组件！");
            return;
        }

        var mf = deformation.targetMeshFilter;
        if (mf == null || mf.sharedMesh == null)
        {
            Debug.LogError("[检查] Target Mesh Filter 未设置或网格为空！");
            return;
        }

        var mesh = mf.sharedMesh;
        Debug.Log($"[检查] 目标网格 = {mf.name}");
        Debug.Log($"[检查] 顶点数 = {mesh.vertexCount}");
        Debug.Log($"[检查] 可读 = {mesh.isReadable}");

        if (!mesh.isReadable)
            Debug.LogError("[检查] 网格不可写！请在 truck.fbx 勾选 Read/Write Enabled");

        if (mesh.vertexCount < 3000)
            Debug.LogWarning($"[检查] 顶点数偏少({mesh.vertexCount})，凹陷可能不圆滑，请再细分一次");

        if (mesh.vertexCount >= 5000)
            Debug.Log("[检查] 顶点数充足，适合变形");

        if (GetComponent<MeshCollider>() == null)
            Debug.LogWarning("[检查] 缺少 MeshCollider");

        Debug.Log("[检查] 完成。用 Sedan/SUV 撞车测试");
    }
}
