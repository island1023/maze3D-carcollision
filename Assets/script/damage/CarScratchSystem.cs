using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering.Universal;

/// <summary>
/// 挂载位置：可驾驶车辆根节点（与 Rigidbody 同物体）。
/// 车辆高速摩擦墙面时，在车身表面动态生成 URP Decal 划痕。
/// </summary>
[RequireComponent(typeof(Rigidbody))]
public class CarScratchSystem : MonoBehaviour
{
    [Header("划痕设置")]
    public DecalProjector scratchPrefab;
    public string[] wallTags = { "Wall" };
    public float minScratchSpeed = 5f;
    public float scratchInterval = 0.5f;
    public float projectionDepth = 0.2f;
    public float surfaceOffset = 0.01f;
    public int maxScratches = 200;

    [Header("可选")]
    [Tooltip("0 表示永久保留；大于 0 时贴花会在指定秒数后自动销毁")]
    public float scratchLifetime;

    private float lastScratchTime;
    private Transform scratchRoot;
    private Rigidbody rb;
    private readonly List<DecalProjector> activeScratches = new List<DecalProjector>();

    void Awake()
    {
        rb = GetComponent<Rigidbody>();
        EnsureScratchRoot();
    }

    void OnCollisionStay(Collision collision)
    {
        if (scratchPrefab == null || !IsWall(collision.gameObject))
            return;

        if (collision.contactCount == 0)
            return;

        float scratchSpeed = GetScratchSpeed(collision);
        if (scratchSpeed < minScratchSpeed)
            return;

        if (Time.time - lastScratchTime < scratchInterval)
            return;

        ContactPoint contact = GetBestSideContact(collision);
        Vector3 wallOutward = contact.normal.sqrMagnitude > 0.0001f
            ? contact.normal.normalized
            : Vector3.up;

        if (!TrySampleBodySurface(contact.point, -wallOutward, out Vector3 bodyPoint, out Vector3 bodyNormal))
            return;

        lastScratchTime = Time.time;
        SpawnScratch(bodyPoint, bodyNormal);
    }

    public void ClearScratches()
    {
        for (int i = activeScratches.Count - 1; i >= 0; i--)
        {
            DecalProjector scratch = activeScratches[i];
            if (scratch != null)
                Destroy(scratch.gameObject);
        }

        activeScratches.Clear();

        if (scratchRoot != null)
        {
            for (int i = scratchRoot.childCount - 1; i >= 0; i--)
                Destroy(scratchRoot.GetChild(i).gameObject);
        }
    }

    private void EnsureScratchRoot()
    {
        if (scratchRoot != null)
            return;

        Transform existing = transform.Find("CarScratches");
        if (existing != null)
        {
            scratchRoot = existing;
            return;
        }

        scratchRoot = new GameObject("CarScratches").transform;
        scratchRoot.SetParent(transform, false);
    }

    private void SpawnScratch(Vector3 bodyPoint, Vector3 bodyNormal)
    {
        EnsureScratchRoot();
        TrimScratchCount();

        Vector3 spawnPos = bodyPoint + bodyNormal * surfaceOffset;
        Quaternion rotation = Quaternion.LookRotation(-bodyNormal, Vector3.up);

        DecalProjector scratch = Instantiate(scratchPrefab, spawnPos, rotation, scratchRoot);

        float randomRotation = Random.Range(-45f, 45f);
        scratch.transform.Rotate(bodyNormal, randomRotation, Space.World);

        float randomSize = Random.Range(0.3f, 0.6f);
        scratch.size = new Vector3(randomSize, projectionDepth, randomSize * 1.5f);

        activeScratches.Add(scratch);

        if (scratchLifetime > 0f)
            Destroy(scratch.gameObject, scratchLifetime);
    }

    private void TrimScratchCount()
    {
        while (activeScratches.Count >= maxScratches)
        {
            DecalProjector oldest = activeScratches[0];
            activeScratches.RemoveAt(0);
            if (oldest != null)
                Destroy(oldest.gameObject);
        }
    }

    private float GetScratchSpeed(Collision collision)
    {
        ContactPoint contact = collision.contacts[0];
        Vector3 tangentVel = GetTangentVelocity(collision, contact);
        return Mathf.Max(tangentVel.magnitude, collision.relativeVelocity.magnitude * 0.65f);
    }

    private bool TrySampleBodySurface(Vector3 probe, Vector3 preferredOutward, out Vector3 point, out Vector3 normal)
    {
        point = default;
        normal = default;

        Vector3 outward = preferredOutward.sqrMagnitude > 0.0001f
            ? preferredOutward.normalized
            : transform.forward;

        Vector3 origin = probe + outward * 0.75f;
        RaycastHit[] hits = Physics.RaycastAll(origin, -outward, 1.6f, ~0, QueryTriggerInteraction.Ignore);

        RaycastHit? best = null;
        float bestScore = float.MaxValue;

        foreach (RaycastHit hit in hits)
        {
            if (!IsOwnVehicleTransform(hit.transform))
                continue;
            if (IsIgnoredBodyCollider(hit.collider))
                continue;

            float score = hit.distance + Vector3.Distance(hit.point, probe) * 0.15f;
            if (score < bestScore)
            {
                bestScore = score;
                best = hit;
            }
        }

        if (!best.HasValue)
            return false;

        RaycastHit bodyHit = best.Value;
        normal = bodyHit.normal.sqrMagnitude > 0.0001f ? bodyHit.normal.normalized : outward;
        point = bodyHit.point;
        return true;
    }

    private bool IsOwnVehicleTransform(Transform t)
    {
        return t == transform || t.IsChildOf(transform);
    }

    private static bool IsIgnoredBodyCollider(Collider col)
    {
        if (col == null || col.isTrigger)
            return true;

        string n = col.name.ToLowerInvariant();
        return n.Contains("wheel") || n.Contains("scratch") || n.Contains("spark");
    }

    private bool IsWall(GameObject other)
    {
        if (wallTags != null)
        {
            foreach (string tag in wallTags)
            {
                if (!string.IsNullOrEmpty(tag) && other.CompareTag(tag))
                    return true;
            }
        }

        string lowerName = other.name.ToLowerInvariant();
        return lowerName.Contains("guardwall") || lowerName.Contains("wall");
    }

    private static ContactPoint GetBestSideContact(Collision collision)
    {
        ContactPoint best = collision.contacts[0];
        float bestScore = -1f;
        bool found = false;

        for (int i = 0; i < collision.contactCount; i++)
        {
            ContactPoint c = collision.contacts[i];
            if (Mathf.Abs(c.normal.y) > 0.75f)
                continue;

            float depth = c.separation < 0f ? -c.separation : 0f;
            float tangent = GetTangentVelocity(collision, c).magnitude;
            float score = depth + tangent * 0.15f;

            if (score > bestScore)
            {
                bestScore = score;
                best = c;
                found = true;
            }
        }

        return found ? best : collision.contacts[0];
    }

    private static Vector3 GetTangentVelocity(Collision collision, ContactPoint contact)
    {
        Vector3 normal = contact.normal.sqrMagnitude < 0.0001f
            ? Vector3.up
            : contact.normal.normalized;

        Vector3 relativeVel = collision.relativeVelocity;
        float normalSpeed = Vector3.Dot(relativeVel, normal);
        return relativeVel - normal * normalSpeed;
    }
}
