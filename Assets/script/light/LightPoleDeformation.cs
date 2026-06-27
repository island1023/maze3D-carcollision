using UnityEngine;
using System.Collections;

[RequireComponent(typeof(MeshFilter), typeof(MeshCollider))]
public class LightPoleDeformation : MonoBehaviour
{
    [Header("变形模式")]
    public bool enableBending = true;           // 启用弯曲
    public bool enableIndentation = false;      // 启用局部凹陷（与弯曲可叠加）

    [Header("弯曲参数（沿撞击方向整体弯曲）")]
    [Tooltip("最大弯曲偏移（米），顶部相对于撞击点高度的最大水平位移")]
    public float maxBendOffset = 1.0f;
    [Tooltip("最小触发弯曲的相对速度（m/s）")]
    public float minBendSpeed = 2f;
    [Tooltip("最大弯曲对应的速度（m/s），超过则用 maxBendOffset")]
    public float maxBendSpeed = 15f;
    [Tooltip("弯曲起始点偏移（从底部算起，单位米）。0=从底部开始，>0 表示从该高度以上开始弯曲，以下保持直立。")]
    public float bendStartHeight = 0.5f;
    [Tooltip("弯曲影响的高度范围（米）。从 bendStartHeight 开始向上到该高度结束弯曲，超出部分不再增加偏移。0=一直弯曲到顶部。")]
    public float bendHeightRange = 0f;           // 0表示一直延伸到顶部
    [Tooltip("弯曲曲线指数：1=线性，>1 使弯曲更集中在顶部，<1 使弯曲更集中在底部")]
    public float bendCurvePower = 1.2f;

    [Header("局部凹陷参数（仅当 enableIndentation 为 true 时生效）")]
    public float maxDeformation = 0.5f;
    public float radius = 1.0f;
    public float minIndentSpeed = 2f;
    public float maxIndentSpeed = 10f;

    [Header("物理设置")]
    public bool updateColliderAfterDeformation = true;
    public bool makeKinematicAfterHit = false;

    [Header("调试")]
    public bool showDebugLogs = true;

    private MeshFilter meshFilter;
    private MeshCollider meshCollider;
    private Mesh originalMesh;
    private Mesh deformedMesh;
    private bool hasDeformed = false;

    void Start()
    {
        meshFilter = GetComponent<MeshFilter>();
        meshCollider = GetComponent<MeshCollider>();

        if (meshFilter == null || meshFilter.sharedMesh == null)
        {
            Debug.LogError("LightPoleDeformation: 没有 MeshFilter 或网格！");
            enabled = false;
            return;
        }

        if (!meshFilter.sharedMesh.isReadable)
        {
            Debug.LogError("路灯网格不可写，请在模型导入设置中勾选 Read/Write Enabled！");
            enabled = false;
            return;
        }

        originalMesh = Instantiate(meshFilter.sharedMesh);
        meshFilter.mesh = originalMesh;
        deformedMesh = null;
    }

    void OnCollisionEnter(Collision collision)
    {
        if (hasDeformed) return;

        float impactSpeed = collision.relativeVelocity.magnitude;
        if ((!enableBending || impactSpeed < minBendSpeed) && (!enableIndentation || impactSpeed < minIndentSpeed))
            return;

        ContactPoint contact = collision.contacts[0];
        Vector3 hitPoint = contact.point;
        Vector3 hitNormal = contact.normal;
        Vector3 hitDirection = collision.relativeVelocity.normalized;

        if (showDebugLogs)
            Debug.Log($"[路灯] 碰撞！速度 = {impactSpeed:F2} m/s, 点 = {hitPoint}, 方向 = {hitDirection}");

        bool deformed = false;

        // 弯曲处理
        if (enableBending && impactSpeed >= minBendSpeed)
        {
            float t = Mathf.InverseLerp(minBendSpeed, maxBendSpeed, impactSpeed);
            float bendOffset = Mathf.Lerp(0, maxBendOffset, t);
            BendMesh(hitDirection, hitPoint, bendOffset);
            deformed = true;
        }

        // 局部凹陷处理
        if (enableIndentation && impactSpeed >= minIndentSpeed)
        {
            float t = Mathf.InverseLerp(minIndentSpeed, maxIndentSpeed, impactSpeed);
            float displacement = Mathf.Lerp(0, maxDeformation, t);
            IndentMesh(hitPoint, hitNormal, displacement);
            deformed = true;
        }

        if (!deformed) return;

        if (updateColliderAfterDeformation && meshCollider != null)
        {
            meshCollider.sharedMesh = null;
            meshCollider.sharedMesh = deformedMesh;
        }

        Rigidbody rb = GetComponent<Rigidbody>();
        if (rb != null && makeKinematicAfterHit)
            rb.isKinematic = false;

        hasDeformed = true;
    }

    /// <summary>
    /// 弯曲变形：从撞击点高度开始向上弯曲（撞击点以下保持直立）
    /// </summary>
    private void BendMesh(Vector3 worldHitDirection, Vector3 worldHitPoint, float maxOffset)
    {
        if (originalMesh == null) return;

        Vector3[] vertices = originalMesh.vertices;
        if (vertices.Length == 0) return;

        // 获取局部高度范围
        float minY = vertices[0].y, maxY = vertices[0].y;
        foreach (var v in vertices)
        {
            if (v.y < minY) minY = v.y;
            if (v.y > maxY) maxY = v.y;
        }
        float totalHeight = maxY - minY;
        if (totalHeight < 0.01f) return;

        // 将撞击点转换到局部坐标，获取撞击点高度
        Vector3 localHitPoint = transform.InverseTransformPoint(worldHitPoint);
        float hitHeight = localHitPoint.y;
        // 弯曲起始高度（局部坐标）
        float startBendY = Mathf.Clamp(hitHeight + bendStartHeight, minY, maxY);
        // 弯曲结束高度（局部坐标）
        float endBendY = (bendHeightRange > 0) ? Mathf.Clamp(startBendY + bendHeightRange, startBendY, maxY) : maxY;
        float bendRangeHeight = endBendY - startBendY;
        if (bendRangeHeight <= 0) return;

        // 撞击方向局部水平分量
        Vector3 localDir = transform.InverseTransformDirection(worldHitDirection);
        localDir.y = 0;
        if (localDir.magnitude < 0.001f) return;
        localDir.Normalize();

        Vector3[] newVertices = new Vector3[vertices.Length];
        for (int i = 0; i < vertices.Length; i++)
        {
            Vector3 v = vertices[i];
            newVertices[i] = v;

            if (v.y <= startBendY) continue;  // 低于弯曲起始点的保持原样

            // 计算弯曲因子 t：在 [startBendY, endBendY] 范围内从0到1线性插值，超出端值则为1
            float t = (v.y - startBendY) / bendRangeHeight;
            t = Mathf.Clamp01(t);
            // 应用曲线指数
            t = Mathf.Pow(t, bendCurvePower);

            float offsetMag = maxOffset * t;
            newVertices[i] = v + localDir * offsetMag;
        }

        deformedMesh = Instantiate(originalMesh);
        deformedMesh.vertices = newVertices;
        deformedMesh.RecalculateNormals();
        deformedMesh.RecalculateBounds();
        meshFilter.mesh = deformedMesh;

        if (showDebugLogs)
            Debug.Log($"[路灯] 弯曲变形，最大偏移 {maxOffset:F2} 米，起始高度 {startBendY:F2}，结束高度 {endBendY:F2}");
    }

    /// <summary>
    /// 局部凹陷变形（撞击点向内凹陷）
    /// </summary>
    private void IndentMesh(Vector3 worldHitPoint, Vector3 worldHitNormal, float maxOffset)
    {
        if (originalMesh == null) return;

        Vector3[] vertices = originalMesh.vertices;

        Vector3 localHitPoint = transform.InverseTransformPoint(worldHitPoint);
        Vector3 localHitNormal = transform.InverseTransformDirection(worldHitNormal).normalized;

        Vector3[] newVertices = new Vector3[vertices.Length];
        for (int i = 0; i < vertices.Length; i++)
        {
            newVertices[i] = vertices[i];
            float distance = Vector3.Distance(vertices[i], localHitPoint);
            if (distance > radius) continue;

            float factor = 1f - Mathf.Clamp01(distance / radius);
            factor = Mathf.Pow(factor, 1.2f);
            Vector3 offsetDir = -localHitNormal;
            float offset = maxOffset * factor;
            newVertices[i] += offsetDir * offset;
        }

        if (deformedMesh == null)
            deformedMesh = Instantiate(originalMesh);
        deformedMesh.vertices = newVertices;
        deformedMesh.RecalculateNormals();
        deformedMesh.RecalculateBounds();
        meshFilter.mesh = deformedMesh;

        if (showDebugLogs)
            Debug.Log($"[路灯] 局部凹陷，最大偏移 {maxOffset:F2} 米");
    }
}