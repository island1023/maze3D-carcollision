using UnityEngine;
using System.Collections.Generic;

public class TreeDamage : MonoBehaviour
{
    [Header("掉落参数")]
    public float baseRadius = 0.5f;
    public float radiusPerSpeed = 0.15f;
    public float forcePerSpeed = 20f;
    public float randomForcePercent = 0.3f;
    public Vector2 randomAngularVelocity = new Vector2(100f, 300f);
    [Tooltip("最小冲击速度（m/s），低于此值不会触发掉落")]
    public float minImpactSpeedToDrop = 2f;

    [Header("落地设置")]
    [Tooltip("地面高度（Y轴），掉落的部件到达此高度后停止运动")]
    public float groundY = 0f;

    [Header("自动查找设置")]
    public string branchTag = "Branch";
    public bool autoFindParts = true;

    [Header("调试")]
    public bool showDebugLog = true;

    private List<Transform> allBranches = new List<Transform>();
    private HashSet<Transform> droppedBranches = new HashSet<Transform>();
    private float lastHitTime = 0f;
    private float hitCooldown = 0.5f;

    private void Start()
    {
        if (autoFindParts) FindBranchesRecursively(transform);
        if (showDebugLog) Debug.Log($"[树木] 找到 {allBranches.Count} 个可掉落枝干");
        if (allBranches.Count == 0) Debug.LogWarning("[树木] 没有找到带有 Branch 标签的子物体！请确保枝干已有 Branch 标签。");
    }

    private void FindBranchesRecursively(Transform parent)
    {
        foreach (Transform child in parent)
        {
            if (child.CompareTag(branchTag)) allBranches.Add(child);
            FindBranchesRecursively(child);
        }
    }

    private void OnCollisionEnter(Collision collision)
    {
        if (!collision.gameObject.CompareTag("Sedan") && !collision.gameObject.CompareTag("SUV")) return;
        if (Time.time - lastHitTime < hitCooldown) return;
        lastHitTime = Time.time;

        float impactSpeed = collision.relativeVelocity.magnitude;
        if (impactSpeed < minImpactSpeedToDrop) return;

        Vector3 hitPoint = collision.contacts[0].point;
        Vector3 hitDirection = collision.relativeVelocity.normalized;

        float dropRadius = baseRadius + impactSpeed * radiusPerSpeed;
        float baseForce = impactSpeed * forcePerSpeed;

        if (showDebugLog) Debug.Log($"[树木] 撞击速度 {impactSpeed:F1} m/s, 半径 {dropRadius:F2}, 力 {baseForce:F1}");

        List<Transform> branchesToDrop = new List<Transform>();
        foreach (Transform branch in allBranches)
        {
            if (droppedBranches.Contains(branch)) continue;
            float dist = Vector3.Distance(branch.position, hitPoint);
            if (dist <= dropRadius)
            {
                branchesToDrop.Add(branch);
                if (showDebugLog) Debug.Log($"[树木] 枝干 {branch.name} 在掉落半径内，距离 {dist:F2}");
            }
        }

        if (branchesToDrop.Count == 0)
        {
            if (showDebugLog) Debug.Log("[树木] 半径内没有可掉落枝干");
            return;
        }

        branchesToDrop.Sort((a, b) => Vector3.Distance(a.position, hitPoint).CompareTo(Vector3.Distance(b.position, hitPoint)));

        foreach (Transform branch in branchesToDrop)
        {
            float distanceFactor = 1f - Mathf.Clamp01(Vector3.Distance(branch.position, hitPoint) / dropRadius);
            float force = baseForce * (0.5f + distanceFactor * 0.8f);
            DropBranch(branch, hitDirection, force);
        }
    }

    private void DropBranch(Transform branch, Vector3 hitDirection, float forceMagnitude)
    {
        if (droppedBranches.Contains(branch)) return;
        droppedBranches.Add(branch);
        if (showDebugLog) Debug.Log($"[树木] 开始掉落枝干: {branch.name}，力 {forceMagnitude:F1}");
        DetachAndApplyForce(branch, hitDirection, forceMagnitude, true);
    }

    private void DetachAndApplyForce(Transform obj, Vector3 hitDirection, float forceMag, bool recursive = true)
    {
        if (obj == null) return;
        obj.SetParent(null);

        Rigidbody rb = obj.GetComponent<Rigidbody>();
        if (rb == null) rb = obj.gameObject.AddComponent<Rigidbody>();

        if (obj.GetComponent<Collider>() == null)
        {
            MeshCollider mc = obj.gameObject.AddComponent<MeshCollider>();
            mc.convex = true;
        }

        Vector3 randomDir = Random.onUnitSphere;
        Vector3 forceDir = (hitDirection + randomDir * 0.5f).normalized;
        forceDir += Vector3.up * 0.3f;
        forceDir.Normalize();

        float finalForce = forceMag * (1f + Random.Range(-randomForcePercent, randomForcePercent));
        rb.AddForce(forceDir * finalForce, ForceMode.Impulse);
        rb.angularVelocity = new Vector3(
            Random.Range(-randomAngularVelocity.x, randomAngularVelocity.x),
            Random.Range(-randomAngularVelocity.x, randomAngularVelocity.x),
            Random.Range(-randomAngularVelocity.x, randomAngularVelocity.x)
        ) * Mathf.Deg2Rad;

        // 添加落地停止逻辑（直接添加一个简单的脚本，其参数从当前 TreeDamage 获取）
        if (obj.GetComponent<LandingStop>() == null)
        {
            LandingStop landing = obj.gameObject.AddComponent<LandingStop>();
            landing.groundY = this.groundY;
        }

        if (recursive)
        {
            foreach (Transform child in obj.GetComponentsInChildren<Transform>())
            {
                if (child != obj)
                {
                    child.SetParent(null);
                    Rigidbody childRb = child.GetComponent<Rigidbody>();
                    if (childRb == null) childRb = child.gameObject.AddComponent<Rigidbody>();
                    childRb.AddForce((Random.onUnitSphere + Vector3.up) * finalForce * 0.5f, ForceMode.Impulse);
                    if (child.GetComponent<LandingStop>() == null)
                    {
                        LandingStop childLanding = child.gameObject.AddComponent<LandingStop>();
                        childLanding.groundY = this.groundY;
                    }
                }
            }
        }

        if (showDebugLog) Debug.Log($"[树木] 枝干 {obj.name} 已脱落并施加力 {finalForce:F1}");
    }
}

/// <summary>
/// 内部落地停止组件（自动添加，无需手动挂载）
/// </summary>
public class LandingStop : MonoBehaviour
{
    public float groundY = 0f;
    private Rigidbody rb;
    private bool grounded = false;

    void Start()
    {
        rb = GetComponent<Rigidbody>();
    }

    void Update()
    {
        if (!grounded && transform.position.y <= groundY)
        {
            grounded = true;
            rb.velocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
            rb.isKinematic = true;
            Collider col = GetComponent<Collider>();
            if (col != null) col.enabled = false;
            Debug.Log($"[落地] {name} 已落地，停止运动");
        }
    }
}