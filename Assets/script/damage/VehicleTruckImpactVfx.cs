using UnityEngine;
using System.Collections;
#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>
/// Truck 与 AITruck 对撞时的分阶段特效：摩擦火星 → 冒烟 → 缓慢起火。
/// 烟雾/火焰为运行时实例化，挂在车辆下的隐藏节点，不会出现在场景根层级。
/// </summary>
[RequireComponent(typeof(Rigidbody))]
[RequireComponent(typeof(VehicleWallFrictionEffect))]
public class VehicleTruckImpactVfx : MonoBehaviour
{
    [Header("--- Hovl 特效模板（留空则自动从 Resources 加载）---")]
    public GameObject smokeEffectTemplate;
    public GameObject fireEffectTemplate;

    [Header("--- 锚点 ---")]
    public Transform effectAnchor;
    public Vector3 smokeWorldOffset = new Vector3(0f, 0.6f, 0f);
    public Vector3 fireLocalOffset = new Vector3(0f, 0.5f, 0f);
    public Vector3 fireWorldScale = new Vector3(2.5f, 2.5f, 2.5f);

    [Header("--- 时间轴（真实秒，不受 timeScale 影响）---")]
    [Tooltip("碰撞后持续摩擦出火星的时长，与撞墙摩擦类似")]
    public float sparkPhaseDuration = 1.2f;
    [Tooltip("火星阶段结束后，再等多久开始冒烟")]
    public float smokeDelayAfterSparks = 0.5f;
    [Tooltip("从碰撞开始算起，多久后开始起火（应大于火星+冒烟阶段）")]
    public float fireDelayFromImpact = 3.5f;
    [Tooltip("火焰从弱到强的渐增时长")]
    public float fireRampDuration = 2f;
    [Tooltip("冒烟实例自动销毁时间")]
    public float smokeLifetime = 5f;

    [Header("--- 与 ExplosionSystem 协调 ---")]
    public bool replaceExplosionSystemEffects = true;

    public bool HasPlayedFire => firePlayed;

    private static int lastPairKey;
    private static float lastPairTime;

    private VehicleWallFrictionEffect frictionEffect;
    private Rigidbody rb;
    private Coroutine sequenceRoutine;
    private Coroutine fireRampRoutine;
    private Transform vfxContainer;
    private GameObject runtimeSmokeInstance;
    private GameObject runtimeFireInstance;
    private bool firePlayed;

    void Awake()
    {
        rb = GetComponent<Rigidbody>();
        frictionEffect = GetComponent<VehicleWallFrictionEffect>()
            ?? GetComponentInParent<VehicleWallFrictionEffect>()
            ?? GetComponentInChildren<VehicleWallFrictionEffect>();

        ResolveEffectReferences();
    }

    /// <summary>
    /// 双方车辆各播一套特效（Truck + AITruck）。
    /// </summary>
    public static void PlayForCollisionPair(Collision collision, GameObject self)
    {
        if (collision == null || self == null)
            return;

        GameObject selfRoot = VehicleOpponentUtility.GetVehicleRoot(self);
        GameObject otherRoot = collision.rigidbody != null
            ? collision.rigidbody.gameObject
            : VehicleOpponentUtility.GetVehicleRoot(collision.gameObject);

        int pairKey = BuildPairKey(selfRoot, otherRoot);
        float now = Time.unscaledTime;
        if (pairKey == lastPairKey && now - lastPairTime < 0.2f)
            return;

        lastPairKey = pairKey;
        lastPairTime = now;

        if (selfRoot != null)
            PlayOnVehicle(selfRoot, collision);

        if (otherRoot != null && otherRoot != selfRoot)
            PlayOnVehicle(otherRoot, collision);
    }

    private static int BuildPairKey(GameObject a, GameObject b)
    {
        int idA = a != null ? a.GetInstanceID() : 0;
        int idB = b != null ? b.GetInstanceID() : 0;
        if (idA > idB)
            (idA, idB) = (idB, idA);

        return idA ^ (idB << 16);
    }

    public static VehicleTruckImpactVfx EnsureOn(GameObject vehicleRoot)
    {
        if (vehicleRoot == null)
            return null;

        VehicleTruckImpactVfx vfx = vehicleRoot.GetComponent<VehicleTruckImpactVfx>()
            ?? vehicleRoot.GetComponentInChildren<VehicleTruckImpactVfx>();

        if (vfx == null)
            vfx = vehicleRoot.gameObject.AddComponent<VehicleTruckImpactVfx>();

        return vfx;
    }

    private static void PlayOnVehicle(GameObject vehicleRoot, Collision collision)
    {
        if (!VehicleOpponentUtility.ShouldPlayTruckImpactVfx(vehicleRoot))
            return;

        EnsureOn(vehicleRoot)?.PlayImpactSequence(collision);
    }

    public void PlayImpactSequence(Collision collision)
    {
        if (collision == null || collision.contactCount == 0)
            return;

        if (sequenceRoutine != null)
            StopCoroutine(sequenceRoutine);

        sequenceRoutine = StartCoroutine(ImpactSequenceRoutine(collision));
    }

    public void ResetEffects()
    {
        if (sequenceRoutine != null)
        {
            StopCoroutine(sequenceRoutine);
            sequenceRoutine = null;
        }

        if (fireRampRoutine != null)
        {
            StopCoroutine(fireRampRoutine);
            fireRampRoutine = null;
        }

        frictionEffect?.StopSparkEmission();
        CleanupRuntimeSmoke();
        CleanupRuntimeFire();
        firePlayed = false;
    }

    private IEnumerator ImpactSequenceRoutine(Collision collision)
    {
        ContactPoint contact = GetBestContact(collision);
        Vector3 contactLocalPoint = transform.InverseTransformPoint(contact.point);
        Vector3 contactNormal = contact.normal;

        frictionEffect?.PlayVehicleImpactBurst(collision);

        if (frictionEffect == null)
            Debug.LogWarning("[VehicleTruckImpactVfx] 未找到 VehicleWallFrictionEffect，无法播放摩擦火星。", this);

        float elapsed = 0f;
        while (elapsed < sparkPhaseDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            float fade = 1f - elapsed / sparkPhaseDuration;

            Vector3 livePoint = transform.TransformPoint(contactLocalPoint);
            Vector3 liveVelocity = rb != null ? rb.velocity : collision.relativeVelocity;

            frictionEffect?.EmitFrictionSparksLikeWall(
                livePoint, contactNormal, liveVelocity, fade);

            yield return null;
        }

        frictionEffect?.StopSparkEmission();

        if (smokeDelayAfterSparks > 0f)
            yield return new WaitForSecondsRealtime(smokeDelayAfterSparks);

        PlaySmokeAtContact(transform.TransformPoint(contactLocalPoint));

        float fireWait = fireDelayFromImpact - sparkPhaseDuration - smokeDelayAfterSparks;
        if (fireWait > 0f)
            yield return new WaitForSecondsRealtime(fireWait);

        PlayFireEffect();
        sequenceRoutine = null;
    }

    private static ContactPoint GetBestContact(Collision collision)
    {
        ContactPoint best = collision.contacts[0];
        float bestDepth = -999f;

        for (int i = 0; i < collision.contactCount; i++)
        {
            ContactPoint c = collision.contacts[i];
            float depth = c.separation < 0f ? -c.separation : 0f;
            if (depth > bestDepth)
            {
                bestDepth = depth;
                best = c;
            }
        }

        return best;
    }

    private void PlaySmokeAtContact(Vector3 worldPoint)
    {
        CleanupRuntimeSmoke();

        if (smokeEffectTemplate == null)
        {
            Debug.LogWarning("[VehicleTruckImpactVfx] 未找到 Smoke2 模板。", this);
            return;
        }

        Vector3 spawnPos = worldPoint + smokeWorldOffset;
        runtimeSmokeInstance = InstantiateEffect(smokeEffectTemplate, spawnPos, Quaternion.identity, Vector3.one);
        if (runtimeSmokeInstance == null)
            return;

        PlayAllParticles(runtimeSmokeInstance, loop: false, isFire: false);
        Destroy(runtimeSmokeInstance, smokeLifetime);
    }

    private void PlayFireEffect()
    {
        if (firePlayed || fireEffectTemplate == null)
        {
            if (fireEffectTemplate == null)
                Debug.LogWarning("[VehicleTruckImpactVfx] 未找到 Fire2 模板。", this);
            return;
        }

        firePlayed = true;
        CleanupRuntimeFire();

        Transform anchor = effectAnchor != null ? effectAnchor : transform;
        Vector3 flatForward = Vector3.ProjectOnPlane(anchor.forward, Vector3.up);
        if (flatForward.sqrMagnitude < 0.001f)
            flatForward = Vector3.forward;
        else
            flatForward.Normalize();

        Vector3 flatRight = Vector3.Cross(Vector3.up, flatForward);
        Vector3 worldPos = anchor.position
            + Vector3.up * fireLocalOffset.y
            + flatForward * fireLocalOffset.z
            + flatRight * fireLocalOffset.x;

        runtimeFireInstance = InstantiateEffect(fireEffectTemplate, worldPos, Quaternion.identity, fireWorldScale);
        if (runtimeFireInstance == null)
            return;

        PrepareFireParticles(runtimeFireInstance);
        PlayAllParticles(runtimeFireInstance, loop: true, isFire: true);
        fireRampRoutine = StartCoroutine(RampFireIntensity(runtimeFireInstance));
    }

    private IEnumerator RampFireIntensity(GameObject fireRoot)
    {
        ParticleSystem[] systems = fireRoot.GetComponentsInChildren<ParticleSystem>(true);
        float[] targetRates = new float[systems.Length];

        for (int i = 0; i < systems.Length; i++)
        {
            ParticleSystem.EmissionModule emission = systems[i].emission;
            targetRates[i] = emission.rateOverTime.constant;
            emission.rateOverTime = 0f;
        }

        float elapsed = 0f;
        float duration = Mathf.Max(0.5f, fireRampDuration);

        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.SmoothStep(0f, 1f, elapsed / duration);

            for (int i = 0; i < systems.Length; i++)
            {
                ParticleSystem.EmissionModule emission = systems[i].emission;
                emission.rateOverTime = targetRates[i] * t;
            }

            yield return null;
        }

        fireRampRoutine = null;
    }

    private GameObject InstantiateEffect(GameObject template, Vector3 position, Quaternion rotation, Vector3 scale)
    {
        if (template == null)
            return null;

        GameObject instance = Instantiate(template, position, rotation, GetVfxContainer());
        instance.transform.localScale = scale;
        instance.SetActive(true);
        StopAllParticles(instance.transform);
        return instance;
    }

    private Transform GetVfxContainer()
    {
        if (vfxContainer == null)
        {
            GameObject container = new GameObject("_ImpactVfx");
            container.hideFlags = HideFlags.HideInHierarchy;
            vfxContainer = container.transform;
            vfxContainer.SetParent(transform, false);
        }

        return vfxContainer;
    }

    private void ResolveEffectReferences()
    {
        // 只从 Resources / 工程路径加载模板，避免误用场景里已有的特效节点
        if (smokeEffectTemplate == null)
            smokeEffectTemplate = LoadDefaultEffectPrefab(
                "Vfx/Smoke2",
                "Assets/Hovl Studio/3D Fire and Explosions/Prefabs/Smoke2.prefab");

        if (fireEffectTemplate == null)
            fireEffectTemplate = LoadDefaultEffectPrefab(
                "Vfx/Fire2",
                "Assets/Hovl Studio/3D Fire and Explosions/Prefabs/Fire2.prefab");
    }

    private static GameObject LoadDefaultEffectPrefab(string resourcesPath, string assetPath)
    {
        GameObject fromResources = Resources.Load<GameObject>(resourcesPath);
        if (fromResources != null)
            return fromResources;

#if UNITY_EDITOR
        return AssetDatabase.LoadAssetAtPath<GameObject>(assetPath);
#else
        return null;
#endif
    }

    private void CleanupRuntimeFire()
    {
        if (fireRampRoutine != null)
        {
            StopCoroutine(fireRampRoutine);
            fireRampRoutine = null;
        }

        if (runtimeFireInstance != null)
        {
            Destroy(runtimeFireInstance);
            runtimeFireInstance = null;
        }
    }

    private void CleanupRuntimeSmoke()
    {
        if (runtimeSmokeInstance != null)
        {
            Destroy(runtimeSmokeInstance);
            runtimeSmokeInstance = null;
        }
    }

    private static void StopAllParticles(Transform root)
    {
        foreach (ParticleSystem ps in root.GetComponentsInChildren<ParticleSystem>(true))
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
    }

    private static void PlayAllParticles(GameObject root, bool loop, bool isFire)
    {
        foreach (ParticleSystem ps in root.GetComponentsInChildren<ParticleSystem>(true))
        {
            ParticleSystem.MainModule main = ps.main;
            main.startDelay = 0f;
            main.useUnscaledTime = true;
            main.loop = loop;
            main.prewarm = false;

            if (isFire)
            {
                main.gravityModifier = -0.15f;
                main.simulationSpace = ParticleSystemSimulationSpace.World;
            }

            ps.Play(true);
        }
    }

    private static void PrepareFireParticles(GameObject fireRoot)
    {
        foreach (ParticleSystemRenderer renderer in fireRoot.GetComponentsInChildren<ParticleSystemRenderer>(true))
        {
            renderer.renderMode = ParticleSystemRenderMode.Billboard;
            renderer.velocityScale = 0f;
            renderer.lengthScale = 1f;
        }
    }
}
