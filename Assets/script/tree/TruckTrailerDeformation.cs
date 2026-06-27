using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// ????? SedenAItruck ???????Sedan ????????????????????????
/// ??????????? AITruck????? AITruck1????
/// </summary>
public class TruckTrailerDeformation : MonoBehaviour
{
    const string ExteriorMeshToken = "exterior_LOD0";

    [Header("???????")]
    public MeshFilter targetMeshFilter;
    public List<MeshFilter> syncMeshFilters;

    [Header("????????")]
    public float maxDepth = 1.2f;
    public float minSpeed = 0.3f;
    public float maxSpeed = 15f;
    [Tooltip("????????????")]
    public float radius = 3f;
    public float falloffPower = 2f;
    public float maxDisplacementPerHit = 1.2f;

    [Header("????????")]
    public bool updateCollider = false;
    public bool showDebugLog = true;

    private Mesh currentMesh;
    private Vector3[] currentVertices;
    private MeshCollider meshCollider;
    private bool initialized;

    void Awake()
    {
        if (!IsDeformationTarget(transform))
        {
            enabled = false;
            return;
        }

        EnsureCarriageTag();
        EnsurePhysicsSetup();
        EnsureBoxCollider();
    }

    void Start()
    {
        if (!TryInitializeMesh())
            enabled = false;
    }

    void OnCollisionEnter(Collision collision)
    {
        TryApplyImpact(collision);
    }

    public bool TryApplyImpact(Collision collision)
    {
        if (collision == null || !enabled)
            return false;

        if (!initialized && !TryInitializeMesh())
            return false;

        if (!IsVehicleCollision(collision.gameObject))
            return false;

        float impactSpeed = collision.relativeVelocity.magnitude;
        if (impactSpeed < minSpeed)
            return false;

        float t = Mathf.InverseLerp(minSpeed, maxSpeed, impactSpeed);
        float depth = Mathf.Lerp(0, maxDepth, t);
        if (maxDisplacementPerHit > 0f)
            depth = Mathf.Min(depth, maxDisplacementPerHit);

        ContactPoint contact = collision.contacts[0];
        int changedCount = DeformMesh(contact.point, contact.normal, depth);

        if (showDebugLog)
            Debug.Log($"[???????] {name} ???={impactSpeed:F1}m/s ???={depth:F2}m ????={changedCount} ??={contact.point}");

        if (changedCount > 0 && updateCollider && meshCollider != null)
        {
            meshCollider.sharedMesh = null;
            meshCollider.sharedMesh = currentMesh;
        }

        return changedCount > 0;
    }

    public static bool IsVehicleCollision(GameObject hitObject)
    {
        Transform current = hitObject.transform;
        while (current != null)
        {
            if (current.CompareTag("Sedan") || current.CompareTag("SUV"))
                return true;
            current = current.parent;
        }
        return false;
    }

    public static TruckTrailerDeformation FindOnHierarchy(GameObject hitObject)
    {
        if (hitObject == null) return null;

        var direct = hitObject.GetComponent<TruckTrailerDeformation>();
        if (direct != null && direct.enabled && IsDeformationTarget(direct.transform))
            return direct;

        var inParent = hitObject.GetComponentInParent<TruckTrailerDeformation>();
        if (inParent != null && inParent.enabled && IsDeformationTarget(inParent.transform))
            return inParent;

        foreach (var def in hitObject.transform.root.GetComponentsInChildren<TruckTrailerDeformation>(true))
        {
            if (def != null && def.enabled && IsDeformationTarget(def.transform))
                return def;
        }

        return null;
    }

    public static bool IsCarriageObject(GameObject obj)
    {
        if (obj == null) return false;

        Transform current = obj.transform;
        while (current != null)
        {
            if (SafeHasTag(current, "AITruck1"))
                return false;
            if (current.GetComponent<AITruckWaypointController>() != null)
                return false;

            if (SafeHasTag(current, "TruckBack"))
                return true;

            var deformation = current.GetComponent<TruckTrailerDeformation>();
            if (deformation != null && deformation.enabled && IsDeformationTarget(current))
                return true;

            string n = current.name;
            if ((n.Contains("SedenAItruck") || n.Contains("SedenAITruck")) && IsDeformationTarget(current))
                return true;

            current = current.parent;
        }
        return false;
    }

    public static bool IsDeformationTarget(Transform t)
    {
        if (t == null) return false;

        if (t.GetComponentInParent<AITruckWaypointController>() != null)
            return false;

        Transform root = t.root;
        if (SafeHasTag(root, "AITruck1"))
            return false;

        Transform cur = t;
        while (cur != null)
        {
            if (SafeHasTag(cur, "AITruck1"))
                return false;
            if (cur.name.Contains("org_AITruck"))
                return false;
            cur = cur.parent;
        }

        if (SafeHasTag(t, "TruckBack"))
            return true;

        string n = t.name;
        return n.Contains("SedenAItruck") || n.Contains("SedenAITruck");
    }

    static bool SafeHasTag(Transform t, string tagName)
    {
        return t != null && !string.IsNullOrEmpty(tagName) && t.tag == tagName;
    }

    void EnsureCarriageTag()
    {
        if (!SafeHasTag(transform, "TruckBack") && (name.Contains("SedenAItruck") || name.Contains("SedenAITruck")))
            tag = "TruckBack";
    }

    void EnsureBoxCollider()
    {
        if (GetComponent<Collider>() != null) return;

        foreach (var col in GetComponentsInChildren<Collider>())
        {
            if (col != null && !col.isTrigger) return;
        }

        var box = gameObject.AddComponent<BoxCollider>();
        box.center = new Vector3(0f, 2f, -3.8f);
        box.size = new Vector3(4f, 2.5f, 11f);
    }

    bool TryInitializeMesh()
    {
        if (initialized) return true;

        if (targetMeshFilter == null)
            targetMeshFilter = FindExteriorMeshFilter();

        if (targetMeshFilter == null || targetMeshFilter.sharedMesh == null)
        {
            Debug.LogError($"[???????] {name} ????? exterior ????");
            return false;
        }

        if (!targetMeshFilter.sharedMesh.isReadable)
        {
            Debug.LogError("[???????] ????????????? FBX ??? Read/Write Enabled");
            return false;
        }

        currentMesh = Instantiate(targetMeshFilter.sharedMesh);
        currentMesh.MarkDynamic();
        targetMeshFilter.mesh = currentMesh;
        currentVertices = currentMesh.vertices;
        CollectSyncMeshFilters();

        meshCollider = GetComponent<MeshCollider>();
        if (showDebugLog)
            Debug.Log($"[???????] {name} ???? ????={targetMeshFilter.name} ????={currentMesh.vertexCount} ??={radius}m");

        initialized = true;
        return true;
    }

    void CollectSyncMeshFilters()
    {
        if (syncMeshFilters == null)
            syncMeshFilters = new List<MeshFilter>();

        foreach (var mf in GetComponentsInChildren<MeshFilter>(true))
        {
            if (mf == null || mf.sharedMesh == null) continue;
            if (!mf.name.Contains("exterior")) continue;
            if (!syncMeshFilters.Contains(mf))
                syncMeshFilters.Add(mf);
            mf.mesh = currentMesh;
        }
    }

    MeshFilter FindExteriorMeshFilter()
    {
        MeshFilter fallback = null;
        int maxVerts = 0;

        foreach (var mf in GetComponentsInChildren<MeshFilter>(true))
        {
            if (mf.sharedMesh == null) continue;
            if (mf.name.Contains(ExteriorMeshToken) || mf.name.Contains("exterior"))
                return mf;
            if (mf.sharedMesh.vertexCount > maxVerts)
            {
                maxVerts = mf.sharedMesh.vertexCount;
                fallback = mf;
            }
        }
        return fallback;
    }

    void EnsurePhysicsSetup()
    {
        if (GetComponentInParent<Rigidbody>() != null && GetComponent<Rigidbody>() == null)
            return;

        foreach (var mc in GetComponents<MeshCollider>())
        {
            if (mc.sharedMesh == null)
                mc.enabled = false;
        }

        Rigidbody rb = GetComponent<Rigidbody>();
        if (rb == null)
            rb = gameObject.AddComponent<Rigidbody>();

        rb.isKinematic = true;
        rb.useGravity = false;
        rb.mass = 5000f;

        foreach (var childRb in GetComponentsInChildren<Rigidbody>(true))
        {
            if (childRb != rb && childRb.transform.IsChildOf(transform))
                Destroy(childRb);
        }
    }

    int DeformMesh(Vector3 worldHitPoint, Vector3 worldHitNormal, float maxDepthOffset)
    {
        Transform meshTransform = targetMeshFilter.transform;

        // ???????????????????????
        Vector3 indentDir = -worldHitNormal.normalized;
        if (indentDir.sqrMagnitude < 0.0001f)
            indentDir = meshTransform.forward;

        // ???? BoxCollider ??????????????????????????
        Vector3 worldCenter = worldHitPoint;
        float closestDist = float.MaxValue;
        for (int i = 0; i < currentVertices.Length; i++)
        {
            Vector3 worldV = meshTransform.TransformPoint(currentVertices[i]);
            float dist = Vector3.Distance(worldV, worldHitPoint);
            if (dist < closestDist)
            {
                closestDist = dist;
                worldCenter = worldV;
            }
        }

        if (showDebugLog && closestDist > 0.3f)
            Debug.Log($"[???????] ?????????? {closestDist:F2}m??????????????????????");

        int changedCount = 0;
        for (int i = 0; i < currentVertices.Length; i++)
        {
            Vector3 worldV = meshTransform.TransformPoint(currentVertices[i]);
            float dist = Vector3.Distance(worldV, worldCenter);
            if (dist > radius)
                continue;

            float factor = 1f - Mathf.Clamp01(dist / radius);
            factor = Mathf.Pow(factor, falloffPower);

            // ??????????????????????????????????????? ????=1??
            worldV += indentDir * (maxDepthOffset * factor);
            currentVertices[i] = meshTransform.InverseTransformPoint(worldV);
            changedCount++;
        }

        if (changedCount > 0)
            ApplyMeshChanges();

        return changedCount;
    }

    void ApplyMeshChanges()
    {
        currentMesh.vertices = currentVertices;
        currentMesh.RecalculateNormals();
        currentMesh.RecalculateBounds();
        currentMesh.RecalculateTangents();

        foreach (var mf in syncMeshFilters)
        {
            if (mf != null)
                mf.mesh = currentMesh;
        }

        targetMeshFilter.mesh = currentMesh;
    }

    void OnDrawGizmosSelected()
    {
        if (targetMeshFilter != null && radius > 0f)
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(targetMeshFilter.transform.position, radius);
        }
    }
}
