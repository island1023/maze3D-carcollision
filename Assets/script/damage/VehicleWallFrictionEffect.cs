using UnityEngine;

/// <summary>
/// 挂载位置：可驾驶车辆根节点（与 Rigidbody 同物体）
/// 在车辆与墙面撞击/摩擦时生成火星粒子，并在墙面上留下划痕。
/// </summary>
[RequireComponent(typeof(Rigidbody))]
public class VehicleWallFrictionEffect : MonoBehaviour
{
    [Header("--- 检测 ---")]
    public string[] wallTags = { "Wall" };
    [Header("--- 车辆对撞火星（与墙面同款）---")]
    public bool enableVehicleSparks = true;
    public string[] vehicleTags = { "AITruck1", "Truck", "TruckBack" };
    public float minTangentSpeed = 1f;
    public float minImpactSpeed = 2f;
    [Range(0f, 1f)]
    public float impactSparkFactor = 0.65f;

    [Header("--- 火星粒子（真实火花：小而快、橙白发光）---")]
    [Tooltip("留空即可；SparkTrail4 是拖尾材质，不适合做火星，建议不填")]
    public Material sparkMaterial;
    public float sparkRatePerSpeed = 5f;
    public int maxSparksPerFrame = 60;
    public int impactBurstMin = 15;
    public int impactBurstMax = 80;

    [Tooltip("单颗火星存活时间（秒），真实火花很短，0.08~0.2")]
    public float sparkLifetime = 0.15f;
    [Tooltip("火星飞出初速度，越大飞溅越远")]
    public float sparkSpeed = 14f;
    [Tooltip("火星大小，真实火花很小，0.02~0.06")]
    public float sparkSize = 0.04f;
    [Tooltip("拉伸倍率，越大越像细长火星屑")]
    public float sparkStretch = 3.5f;

    [Header("--- 墙面划痕 ---")]
    public bool enableWallScratches = true;

    [Header("--- 摩擦损伤 ---")]
    [Tooltip("车辆沿墙摩擦时缓慢累积少量损伤，并计入碰撞统计")]
    public bool enableFrictionDamage = true;
    [Tooltip("摩擦强度达到阈值时，每秒造成的损伤")]
    public float frictionDamagePerSecond = 0.8f;
    [Tooltip("损伤结算间隔（秒）")]
    public float frictionDamageInterval = 0.4f;
    [Tooltip("计入碰撞统计的最小间隔（秒）")]
    public float frictionStatInterval = 2f;

    private ParticleSystem sparkPs;
    private ParticleSystem.MainModule sparkMain;
    private Material runtimeSparkMaterial;
    private bool sparkPlaying;

    private Rigidbody rb;
    private VehiclePerformanceSystem vps;

    private Vector3 lastWallScratchPoint;
    private bool hasWallScratchPoint;
    private float lockedScratchHeight;
    private float lastFrictionDamageTime;
    private float lastFrictionStatTime;

    void Awake()
    {
        rb = GetComponent<Rigidbody>();
        vps = GetComponent<VehiclePerformanceSystem>();
        CreateSparkSystem();
    }

    void OnCollisionEnter(Collision collision)
    {
        if (IsWall(collision.gameObject))
            HandleWallImpactEnter(collision);
        else if (enableVehicleSparks && IsVehicle(collision.gameObject))
            HandleVehicleImpactEnter(collision);
    }

    void OnCollisionStay(Collision collision)
    {
        if (IsWall(collision.gameObject))
            HandleWallImpactStay(collision);
        else if (enableVehicleSparks && IsVehicle(collision.gameObject))
            HandleVehicleImpactStay(collision);
    }

    public void PlayVehicleImpactBurst(Collision collision)
    {
        if (!enableVehicleSparks || collision == null || collision.contactCount == 0)
            return;

        HandleVehicleImpactEnter(collision);
    }

    public void EmitVehicleSparks(ContactPoint contact, Vector3 relativeVelocity, float intensityScale = 1f)
    {
        EmitVehicleSparksAt(contact.point, contact.normal, relativeVelocity, intensityScale);
    }

    public void EmitVehicleSparksAt(Vector3 point, Vector3 normal, Vector3 relativeVelocity, float intensityScale = 1f)
    {
        if (!enableVehicleSparks)
            return;

        float relativeSpeed = relativeVelocity.magnitude;
        if (relativeSpeed < minImpactSpeed * 0.25f && intensityScale < 0.5f)
            return;

        Vector3 n = normal.sqrMagnitude > 0.0001f ? normal.normalized : Vector3.up;
        float normalSpeed = Vector3.Dot(relativeVelocity, n);
        Vector3 tangentVel = relativeVelocity - n * normalSpeed;

        Vector3 sprayDir = tangentVel.sqrMagnitude > 0.04f
            ? tangentVel.normalized
            : BuildFallbackSprayDirection(n, relativeVelocity);

        float intensity = Mathf.Max(tangentVel.magnitude, relativeSpeed * impactSparkFactor) * intensityScale;
        int count = Mathf.Clamp(
            Mathf.RoundToInt(intensity * sparkRatePerSpeed * 0.55f),
            impactBurstMin / 3,
            maxSparksPerFrame);

        EmitSparksAt(point, n, sprayDir, intensity, count);
    }

    public void StopSparkEmission()
    {
        StopSparks();
    }

    /// <summary>
    /// 持续摩擦火星，逻辑与撞墙 OnCollisionStay 一致（供卡车对撞时间轴使用）。
    /// </summary>
    public void EmitFrictionSparksLikeWall(Vector3 worldPoint, Vector3 normal, Vector3 worldVelocity, float intensityScale = 1f)
    {
        if (!enableVehicleSparks || intensityScale <= 0f)
            return;

        Vector3 n = normal.sqrMagnitude > 0.0001f ? normal.normalized : Vector3.up;
        float normalSpeed = Vector3.Dot(worldVelocity, n);
        Vector3 tangentVel = worldVelocity - n * normalSpeed;
        float tangentSpeed = tangentVel.magnitude;
        float relativeSpeed = worldVelocity.magnitude;
        float sparkIntensity = Mathf.Max(tangentSpeed, relativeSpeed * impactSparkFactor) * intensityScale;

        float minIntensity = Mathf.Min(minTangentSpeed, minImpactSpeed) * 0.25f;
        if (sparkIntensity < minIntensity)
            return;

        Vector3 sprayDir = tangentSpeed > 0.2f
            ? tangentVel.normalized
            : BuildFallbackSprayDirection(n, worldVelocity);

        int count = Mathf.Clamp(
            Mathf.RoundToInt(sparkIntensity * sparkRatePerSpeed),
            2,
            maxSparksPerFrame);

        EmitSparksAt(worldPoint, n, sprayDir, sparkIntensity, count);
    }

    private void HandleWallImpactEnter(Collision collision)
    {
        float relativeSpeed = collision.relativeVelocity.magnitude;
        if (relativeSpeed < minImpactSpeed)
            return;

        ContactPoint contact = GetBestContact(collision);
        Vector3 tangentVel = GetTangentVelocity(collision, contact);
        Vector3 sprayDir = tangentVel.sqrMagnitude > 0.04f
            ? tangentVel.normalized
            : BuildFallbackSprayDirection(contact.normal, collision.relativeVelocity);

        int burst = Mathf.Clamp(
            Mathf.RoundToInt(relativeSpeed * sparkRatePerSpeed),
            impactBurstMin,
            impactBurstMax);

        EmitSparks(contact, sprayDir, relativeSpeed, burst);

        float scratchSpeed = Mathf.Max(tangentVel.magnitude, relativeSpeed * impactSparkFactor);
        if (scratchSpeed >= minTangentSpeed * 0.5f)
        {
            Vector3 scratchDir = tangentVel.sqrMagnitude > 0.04f
                ? tangentVel
                : BuildFallbackSprayDirection(contact.normal, collision.relativeVelocity) * scratchSpeed;
            AddScratches(collision, contact, scratchDir, scratchSpeed);
        }
    }

    private void HandleVehicleImpactEnter(Collision collision)
    {
        float relativeSpeed = collision.relativeVelocity.magnitude;
        if (relativeSpeed < minImpactSpeed)
            return;

        ContactPoint contact = GetBestContact(collision);
        Vector3 tangentVel = GetTangentVelocity(collision, contact);
        Vector3 sprayDir = tangentVel.sqrMagnitude > 0.04f
            ? tangentVel.normalized
            : BuildFallbackSprayDirection(contact.normal, collision.relativeVelocity);

        int burst = Mathf.Clamp(
            Mathf.RoundToInt(relativeSpeed * sparkRatePerSpeed),
            impactBurstMin,
            impactBurstMax);

        EmitSparks(contact, sprayDir, relativeSpeed, burst);
    }

    private void HandleWallImpactStay(Collision collision)
    {

        if (collision.contactCount == 0)
            return;

        ContactPoint contact = GetBestSideContact(collision);
        Vector3 tangentVel = GetTangentVelocity(collision, contact);
        float tangentSpeed = tangentVel.magnitude;
        float relativeSpeed = collision.relativeVelocity.magnitude;
        float sparkIntensity = Mathf.Max(tangentSpeed, relativeSpeed * impactSparkFactor);

        if (sparkIntensity < Mathf.Min(minTangentSpeed, minImpactSpeed))
        {
            StopSparks();
            return;
        }

        Vector3 sprayDir = tangentSpeed > 0.2f
            ? tangentVel.normalized
            : BuildFallbackSprayDirection(contact.normal, collision.relativeVelocity);

        int count = Mathf.Clamp(
            Mathf.RoundToInt(sparkIntensity * sparkRatePerSpeed),
            2,
            maxSparksPerFrame);

        EmitSparks(contact, sprayDir, sparkIntensity, count);

        if (sparkIntensity >= minTangentSpeed * 0.5f)
        {
            Vector3 scratchDir = tangentSpeed > 0.15f
                ? tangentVel
                : BuildFallbackSprayDirection(contact.normal, collision.relativeVelocity) * sparkIntensity;
            AddScratches(collision, contact, scratchDir, sparkIntensity);
        }

        ApplyFrictionDamage(collision, contact, sparkIntensity);
    }

    private void HandleVehicleImpactStay(Collision collision)
    {
        if (collision.contactCount == 0)
            return;

        ContactPoint contact = GetBestSideContact(collision);
        Vector3 tangentVel = GetTangentVelocity(collision, contact);
        float tangentSpeed = tangentVel.magnitude;
        float relativeSpeed = collision.relativeVelocity.magnitude;
        float sparkIntensity = Mathf.Max(tangentSpeed, relativeSpeed * impactSparkFactor);

        if (sparkIntensity < Mathf.Min(minTangentSpeed, minImpactSpeed))
        {
            StopSparks();
            return;
        }

        Vector3 sprayDir = tangentSpeed > 0.2f
            ? tangentVel.normalized
            : BuildFallbackSprayDirection(contact.normal, collision.relativeVelocity);

        int count = Mathf.Clamp(
            Mathf.RoundToInt(sparkIntensity * sparkRatePerSpeed),
            2,
            maxSparksPerFrame);

        EmitSparks(contact, sprayDir, sparkIntensity, count);
    }

    private void AddScratches(Collision collision, ContactPoint contact, Vector3 scratchDir, float scratchSpeed)
    {
        Vector3 wallOutward = contact.normal.normalized;
        Vector3 slideDir = scratchDir.sqrMagnitude > 0.0001f
            ? scratchDir
            : BuildFallbackSprayDirection(wallOutward, rb != null ? rb.velocity : Vector3.zero);

        Vector3 tangent = Vector3.ProjectOnPlane(slideDir, wallOutward);
        if (tangent.sqrMagnitude < 0.01f && rb != null)
            tangent = Vector3.ProjectOnPlane(rb.velocity, wallOutward);
        if (tangent.sqrMagnitude < 0.01f)
            tangent = Vector3.Cross(wallOutward, Vector3.up);
        tangent.Normalize();

        Vector3 wallPoint = GetSmoothedWallPoint(contact.point);

        if (!enableWallScratches)
            return;

        WallScratchTracker wallTracker = GetOrCreateWallTracker(collision.collider.gameObject);
        wallTracker.followSurface = false;
        wallTracker.surfaceOffset = 0.006f;
        wallTracker.minPointDistance = 0.025f;
        wallTracker.minSpeedToScratch = 0.3f;
        wallTracker.scratchColor = new Color(0.11f, 0.105f, 0.1f, 1f);
        wallTracker.scratchWidth = 0.013f;
        wallTracker.AddScratchPoint(wallPoint, wallOutward, tangent, scratchSpeed);
    }

    private Vector3 GetSmoothedWallPoint(Vector3 contactPoint)
    {
        if (!hasWallScratchPoint)
        {
            lastWallScratchPoint = contactPoint;
            lockedScratchHeight = contactPoint.y;
            hasWallScratchPoint = true;
            return contactPoint;
        }

        Vector3 p = contactPoint;

        // 限制竖向跳变，保持划痕在同一高度带
        if (Mathf.Abs(p.y - lockedScratchHeight) > 0.08f)
            p.y = lockedScratchHeight;
        else
            p.y = Mathf.Lerp(p.y, lockedScratchHeight, 0.6f);

        // 小幅平滑，避免帧间抖动
        p = Vector3.Lerp(lastWallScratchPoint, p, 0.45f);
        lastWallScratchPoint = p;
        return p;
    }

    private void ResetScratchAnchor()
    {
        hasWallScratchPoint = false;
        lastFrictionDamageTime = 0f;
        lastFrictionStatTime = 0f;
    }

    private void ApplyFrictionDamage(Collision collision, ContactPoint contact, float frictionIntensity)
    {
        if (!enableFrictionDamage || vps == null || rb == null)
            return;

        if (vps.state == VehicleState.Destroyed)
            return;

        if (frictionIntensity < minTangentSpeed)
            return;

        float now = Time.time;
        if (now - lastFrictionDamageTime < frictionDamageInterval)
            return;

        lastFrictionDamageTime = now;

        float intensityFactor = Mathf.InverseLerp(minTangentSpeed, minTangentSpeed * 4f, frictionIntensity);
        float damage = frictionDamagePerSecond * frictionDamageInterval * Mathf.Lerp(0.35f, 1f, intensityFactor);
        if (damage <= 0f)
            return;

        vps.AddDamage(damage);

        if (now - lastFrictionStatTime >= frictionStatInterval)
        {
            lastFrictionStatTime = now;

            DamageData data = new DamageData
            {
                impactForce = frictionIntensity * rb.mass,
                hitPoint = contact.point,
                hitNormal = contact.normal,
                victim = gameObject,
                hitPart = contact.thisCollider
            };
            SimulationEvents.TriggerVehicleDamage(data);
        }
    }

    private ContactPoint GetBestSideContact(Collision collision)
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

        return found ? best : GetBestContact(collision);
    }

    public void ClearVehicleScratches()
    {
    }

    void OnCollisionExit(Collision collision)
    {
        if (IsWall(collision.gameObject))
        {
            StopSparks();
            ResetScratchAnchor();
        }
        else if (enableVehicleSparks && IsVehicle(collision.gameObject))
        {
            StopSparks();
        }
    }

    private bool IsVehicle(GameObject other)
    {
        if (VehicleOpponentUtility.IsTruckOpponent(other))
            return true;

        if (vehicleTags == null)
            return false;

        Transform current = other.transform;
        while (current != null)
        {
            foreach (string tag in vehicleTags)
            {
                if (!string.IsNullOrEmpty(tag) && current.CompareTag(tag))
                    return true;
            }

            current = current.parent;
        }

        return false;
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

    private static ContactPoint GetBestContact(Collision collision)
    {
        ContactPoint best = collision.contacts[0];
        float bestScore = -999f;

        for (int i = 0; i < collision.contactCount; i++)
        {
            ContactPoint c = collision.contacts[i];
            float depth = c.separation < 0f ? -c.separation : 0f;
            if (depth > bestScore)
            {
                bestScore = depth;
                best = c;
            }
        }

        return best;
    }

    private Vector3 GetTangentVelocity(Collision collision, ContactPoint contact)
    {
        return GetTangentVelocityFromRelative(contact, collision.relativeVelocity);
    }

    private static Vector3 GetTangentVelocityFromRelative(ContactPoint contact, Vector3 relativeVelocity)
    {
        Vector3 normal = contact.normal;
        if (normal.sqrMagnitude < 0.0001f)
            normal = Vector3.up;
        else
            normal.Normalize();

        float normalSpeed = Vector3.Dot(relativeVelocity, normal);
        return relativeVelocity - normal * normalSpeed;
    }

    private static Vector3 BuildFallbackSprayDirection(Vector3 contactNormal, Vector3 relativeVelocity)
    {
        Vector3 n = contactNormal.sqrMagnitude > 0.0001f ? contactNormal.normalized : Vector3.up;
        Vector3 fromMotion = Vector3.ProjectOnPlane(-relativeVelocity, n);
        if (fromMotion.sqrMagnitude > 0.04f)
            return fromMotion.normalized;

        Vector3 tangent = Vector3.Cross(n, Vector3.up);
        if (tangent.sqrMagnitude < 0.04f)
            tangent = Vector3.Cross(n, Vector3.right);
        return tangent.normalized;
    }

    private void EmitSparks(ContactPoint contact, Vector3 sprayDir, float intensity, int count)
    {
        Vector3 normal = contact.normal.sqrMagnitude > 0.0001f ? contact.normal : Vector3.up;
        EmitSparksAt(contact.point, normal, sprayDir, intensity, count);
    }

    private void EmitSparksAt(Vector3 point, Vector3 normal, Vector3 sprayDir, float intensity, int count)
    {
        if (sparkPs == null || count <= 0)
            return;

        if (normal.sqrMagnitude < 0.0001f)
            normal = Vector3.up;
        else
            normal.Normalize();

        Vector3 tangent = sprayDir.sqrMagnitude > 0.0001f ? sprayDir.normalized : Vector3.right;
        Vector3 binormal = Vector3.Cross(normal, tangent).normalized;

        if (!sparkPlaying)
        {
            sparkPs.Play();
            sparkPlaying = true;
        }

        var main = sparkPs.main;
        main.useUnscaledTime = true;

        float speedScale = Mathf.Lerp(0.85f, 1.35f, Mathf.Clamp01(intensity / 15f));
        var emitParams = new ParticleSystem.EmitParams();

        for (int i = 0; i < count; i++)
        {
            Vector3 localDir =
                tangent * Random.Range(0.4f, 1f)
                + binormal * Random.Range(-0.7f, 0.7f)
                + normal * Random.Range(-0.15f, 0.55f)
                + Vector3.up * Random.Range(0f, 0.35f);

            if (localDir.sqrMagnitude < 0.0001f)
                localDir = tangent;
            else
                localDir.Normalize();

            float speed = sparkSpeed * speedScale * Random.Range(0.65f, 1.35f);
            float life = sparkLifetime * Random.Range(0.6f, 1.2f);
            float size = sparkSize * Random.Range(0.5f, 1.3f);

            emitParams.position = point + normal * 0.02f + Random.insideUnitSphere * 0.03f;
            emitParams.velocity = localDir * speed;
            emitParams.startLifetime = life;
            emitParams.startSize = size;
            emitParams.startColor = Random.value > 0.35f
                ? new Color(1f, 0.92f, 0.55f, 1f)
                : new Color(1f, 0.55f, 0.12f, 1f);
            emitParams.applyShapeToPosition = false;

            sparkPs.Emit(emitParams, 1);
        }
    }

    private void StopSparks()
    {
        if (sparkPs == null || !sparkPlaying)
            return;

        sparkPs.Stop(true, ParticleSystemStopBehavior.StopEmitting);
        sparkPlaying = false;
    }

    private static WallScratchTracker GetOrCreateWallTracker(GameObject wallColliderObject)
    {
        WallScratchTracker tracker = wallColliderObject.GetComponent<WallScratchTracker>();
        if (tracker == null)
            tracker = wallColliderObject.AddComponent<WallScratchTracker>();

        return tracker;
    }

    private void CreateSparkSystem()
    {
        GameObject sparkObj = new GameObject("WallFrictionSparks");
        sparkObj.transform.SetParent(transform, false);

        sparkPs = sparkObj.AddComponent<ParticleSystem>();
        sparkMain = sparkPs.main;
        sparkMain.loop = true;
        sparkMain.playOnAwake = false;
        sparkMain.useUnscaledTime = true;
        sparkMain.simulationSpace = ParticleSystemSimulationSpace.World;
        sparkMain.maxParticles = 500;
        sparkMain.startLifetime = sparkLifetime;
        sparkMain.startSpeed = 0f;
        sparkMain.startSize = sparkSize;
        sparkMain.gravityModifier = 2.5f;
        sparkMain.startRotation = new ParticleSystem.MinMaxCurve(0f, 360f);
        sparkMain.startColor = new ParticleSystem.MinMaxGradient(
            new Color(1f, 0.95f, 0.7f, 1f),
            new Color(1f, 0.5f, 0.08f, 1f));

        ParticleSystem.EmissionModule emission = sparkPs.emission;
        emission.rateOverTime = 0f;

        ParticleSystem.ShapeModule shape = sparkPs.shape;
        shape.enabled = false;

        ParticleSystem.SizeOverLifetimeModule sizeOverLife = sparkPs.sizeOverLifetime;
        sizeOverLife.enabled = true;
        AnimationCurve sizeCurve = new AnimationCurve(
            new Keyframe(0f, 1f),
            new Keyframe(0.25f, 0.7f),
            new Keyframe(1f, 0f));
        sizeOverLife.size = new ParticleSystem.MinMaxCurve(1f, sizeCurve);

        ParticleSystem.ColorOverLifetimeModule colorOverLife = sparkPs.colorOverLifetime;
        colorOverLife.enabled = true;
        Gradient gradient = new Gradient();
        gradient.SetKeys(
            new[]
            {
                new GradientColorKey(new Color(1f, 1f, 0.85f), 0f),
                new GradientColorKey(new Color(1f, 0.65f, 0.15f), 0.35f),
                new GradientColorKey(new Color(0.8f, 0.2f, 0.02f), 0.7f),
                new GradientColorKey(new Color(0.3f, 0.05f, 0f), 1f)
            },
            new[]
            {
                new GradientAlphaKey(1f, 0f),
                new GradientAlphaKey(0.9f, 0.2f),
                new GradientAlphaKey(0.4f, 0.6f),
                new GradientAlphaKey(0f, 1f)
            });
        colorOverLife.color = gradient;

        ParticleSystemRenderer renderer = sparkObj.GetComponent<ParticleSystemRenderer>();
        renderer.renderMode = ParticleSystemRenderMode.Stretch;
        renderer.lengthScale = sparkStretch;
        renderer.velocityScale = 0.25f;
        renderer.normalDirection = 1f;
        renderer.material = BuildSparkMaterial();
    }

    private Material BuildSparkMaterial()
    {
        if (runtimeSparkMaterial != null)
            return runtimeSparkMaterial;

        Shader shader = Shader.Find("Mobile/Particles/Additive")
            ?? Shader.Find("Legacy Shaders/Particles/Additive")
            ?? Shader.Find("Universal Render Pipeline/Particles/Unlit");

        runtimeSparkMaterial = new Material(shader);
        runtimeSparkMaterial.mainTexture = CreateSparkDotTexture();

        Color tint = new Color(1f, 0.85f, 0.4f, 1f);
        runtimeSparkMaterial.color = tint;
        if (runtimeSparkMaterial.HasProperty("_BaseColor"))
            runtimeSparkMaterial.SetColor("_BaseColor", tint);
        if (runtimeSparkMaterial.HasProperty("_BaseMap"))
            runtimeSparkMaterial.SetTexture("_BaseMap", runtimeSparkMaterial.mainTexture);

        if (runtimeSparkMaterial.HasProperty("_Surface"))
        {
            runtimeSparkMaterial.SetFloat("_Surface", 1f);
            runtimeSparkMaterial.SetFloat("_Blend", 1f);
            runtimeSparkMaterial.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
            runtimeSparkMaterial.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.One);
        }

        runtimeSparkMaterial.renderQueue = 3000;
        return runtimeSparkMaterial;
    }

    private static Texture2D CreateSparkDotTexture()
    {
        const int size = 32;
        Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false)
        {
            wrapMode = TextureWrapMode.Clamp,
            filterMode = FilterMode.Bilinear
        };

        float center = (size - 1) * 0.5f;
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float dx = (x - center) / center;
                float dy = (y - center) / center;
                float dist = Mathf.Sqrt(dx * dx + dy * dy);
                float core = Mathf.Clamp01(1f - dist * 1.8f);
                float glow = Mathf.Clamp01(1f - dist * 0.9f);
                float alpha = core * 0.9f + glow * 0.35f;
                tex.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
            }
        }

        tex.Apply();
        return tex;
    }
}
