using UnityEngine;
using System.Collections;

[RequireComponent(typeof(VehiclePerformanceSystem))]
[RequireComponent(typeof(Rigidbody))]
public class ExplosionSystem : MonoBehaviour
{
    [Header("--- ��ը/������Ч�������գ��Զ����������壩---")]
    public GameObject explosionEffect;
    public GameObject fireEffect;

    [Header("--- ��Чê�㣨�������ó��� Rigidbody ���ڵ㣩---")]
    [Tooltip("��ѡ���ڳ�������λ�ý��������Ͻ�����΢��������")]
    public Transform effectAnchor;
    public Vector3 explosionLocalOffset = new Vector3(0f, 1.2f, 0f);
    public Vector3 fireLocalOffset = new Vector3(0f, 0.5f, 0f);
    public Vector3 fireWorldScale = new Vector3(2.5f, 2.5f, 2.5f);

    [Header("--- �������� ---")]
    [Range(0f, 1f)]
    public float healthTriggerRatio = 0.95f;
    public float rolloverUpDot = 0.2f;
    [Tooltip("��������ʱ������Ҫ����һ�����ˣ������Ѫ��ը/���ú���ը")]
    public float rolloverMinDamage = 30f;

    [Header("--- �����ӳ٣���ʵ�룬���� timeScale Ӱ�죩---")]
    public float fireDelayRealtime = 1.5f;

    [Header("--- ����ʱ�� ---")]
    [Tooltip("�ȴ�һ֡�ٲ���ը��ȷ����ײ����/����ģ���л����")]
    public bool waitFrameBeforeExplosion = true;

    private VehiclePerformanceSystem vps;
    private Rigidbody rb;
    private VehicleTruckImpactVfx truckImpactVfx;
    private bool hasTriggered;
    private bool explosionPlayed;
    private bool firePlayed;

    private ParticleSystem explosionRootPs;
    private ParticleSystem fireRootPs;
    private Coroutine effectRoutine;

    private Transform explosionOriginalParent;
    private Vector3 explosionOriginalLocalPos;
    private Quaternion explosionOriginalLocalRot;
    private Vector3 explosionOriginalLocalScale;

    private Transform fireOriginalParent;
    private Vector3 fireOriginalLocalPos;
    private Quaternion fireOriginalLocalRot;
    private Vector3 fireOriginalLocalScale;

    void Awake()
    {
        foreach (ExplosionSystem other in GetComponents<ExplosionSystem>())
        {
            if (other != this)
                Destroy(other);
        }

        vps = GetComponent<VehiclePerformanceSystem>();
        rb = GetComponent<Rigidbody>();
        truckImpactVfx = GetComponent<VehicleTruckImpactVfx>();
        ResolveEffectReferences();
        CacheOriginalTransform(explosionEffect, ref explosionOriginalParent,
            ref explosionOriginalLocalPos, ref explosionOriginalLocalRot, ref explosionOriginalLocalScale);
        CacheOriginalTransform(fireEffect, ref fireOriginalParent,
            ref fireOriginalLocalPos, ref fireOriginalLocalRot, ref fireOriginalLocalScale);
        SuppressAutoPlay();
    }

    void Update()
    {
        if (hasTriggered)
            return;

        if (ShouldDeferToTruckImpactVfx())
            return;

        bool healthDepleted = vps.damage >= vps.maxDamage * healthTriggerRatio;
        bool rolledOver = Vector3.Dot(transform.up, Vector3.up) < rolloverUpDot;
        bool rolloverWithDamage = rolledOver && vps.damage >= rolloverMinDamage;

        if (healthDepleted || rolloverWithDamage)
            TriggerExplosion();
    }

    void LateUpdate()
    {
        if (!hasTriggered || vps.damage > 0f)
            return;

        ResetExplosionState();
    }

    private bool ShouldDeferToTruckImpactVfx()
    {
        if (truckImpactVfx == null)
            truckImpactVfx = GetComponent<VehicleTruckImpactVfx>();

        return truckImpactVfx != null && truckImpactVfx.replaceExplosionSystemEffects;
    }

    private void TriggerExplosion()
    {
        if (hasTriggered || effectRoutine != null)
            return;

        hasTriggered = true;

        if (vps.damage < vps.maxDamage)
            vps.AddDamage(vps.maxDamage - vps.damage);

        rb.drag = 10f;
        rb.angularDrag = 10f;

        effectRoutine = StartCoroutine(PlayExplosionAndFire());
    }

    private IEnumerator PlayExplosionAndFire()
    {
        if (waitFrameBeforeExplosion)
            yield return null;

        ResolveEffectReferences();
        PlayExplosionOnce();

        if (fireEffect == null)
        {
            effectRoutine = null;
            yield break;
        }

        if (fireDelayRealtime > 0f)
            yield return new WaitForSecondsRealtime(fireDelayRealtime);

        PlayFireEffect();
        effectRoutine = null;
    }

    private void PlayExplosionOnce()
    {
        if (explosionPlayed || explosionEffect == null || explosionRootPs == null)
            return;

        explosionPlayed = true;

        ActivateHierarchy(explosionEffect);
        AlignEffectToVehicle(explosionEffect.transform, explosionLocalOffset, useVehicleRotation: true);
        DetachToWorld(explosionEffect.transform);

        StopAllParticlesUnder(explosionEffect.transform);
        PrepareForManualPlay(explosionRootPs, loop: false, isFire: false);
        explosionRootPs.Play(true);
    }

    private void PlayFireEffect()
    {
        if (firePlayed || fireEffect == null || fireRootPs == null)
            return;

        firePlayed = true;

        ActivateHierarchy(fireEffect);
        AlignFireToWorld(fireEffect.transform);
        DetachToWorld(fireEffect.transform);

        StopAllParticlesUnder(fireEffect.transform);
        PrepareAllFireParticles(fireEffect);
        fireRootPs.Play(true);
    }

    private void AlignFireToWorld(Transform effectTransform)
    {
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

        effectTransform.SetPositionAndRotation(worldPos, Quaternion.identity);
        effectTransform.localScale = fireWorldScale;
    }

    private void AlignEffectToVehicle(Transform effectTransform, Vector3 localOffset, bool useVehicleRotation)
    {
        Transform anchor = effectAnchor != null ? effectAnchor : transform;
        effectTransform.SetPositionAndRotation(
            anchor.TransformPoint(localOffset),
            useVehicleRotation ? anchor.rotation : Quaternion.identity
        );
    }

    private static void PrepareForManualPlay(ParticleSystem ps, bool loop, bool isFire)
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
    }

    private static void PrepareAllFireParticles(GameObject fireRoot)
    {
        foreach (ParticleSystem ps in fireRoot.GetComponentsInChildren<ParticleSystem>(true))
        {
            PrepareForManualPlay(ps, loop: true, isFire: true);
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            ps.Clear(true);
            ps.Simulate(0f, true, true);
        }

        foreach (ParticleSystemRenderer renderer in fireRoot.GetComponentsInChildren<ParticleSystemRenderer>(true))
        {
            renderer.renderMode = ParticleSystemRenderMode.Billboard;
            renderer.velocityScale = 0f;
            renderer.lengthScale = 1f;
        }
    }

    private static void ActivateHierarchy(GameObject root)
    {
        foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
            t.gameObject.SetActive(true);
    }

    private void ResolveEffectReferences()
    {
        if (explosionEffect == null)
        {
            Transform t = FindDeepChild(transform, "Explosion9");
            if (t != null) explosionEffect = t.gameObject;
        }

        if (fireEffect == null)
        {
            Transform t = FindDeepChild(transform, "Fire2");
            if (t != null) fireEffect = t.gameObject;
        }

        explosionRootPs = explosionEffect != null
            ? explosionEffect.GetComponent<ParticleSystem>()
            : null;

        fireRootPs = fireEffect != null
            ? fireEffect.GetComponent<ParticleSystem>()
            : null;
    }

    private void SuppressAutoPlay()
    {
        SuppressAutoPlayOnEffect(explosionEffect, forceNoLoop: true, isFire: false);
        SuppressAutoPlayOnEffect(fireEffect, forceNoLoop: false, isFire: true);
    }

    private static void SuppressAutoPlayOnEffect(GameObject effectRoot, bool forceNoLoop, bool isFire)
    {
        if (effectRoot == null) return;

        foreach (ParticleSystem ps in effectRoot.GetComponentsInChildren<ParticleSystem>(true))
        {
            ps.playOnAwake = false;
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            ParticleSystem.MainModule main = ps.main;
            main.useUnscaledTime = true;
            main.prewarm = false;
            if (forceNoLoop)
                main.loop = false;
            if (isFire)
                main.simulationSpace = ParticleSystemSimulationSpace.World;
        }
    }

    private static void StopAllParticlesUnder(Transform root)
    {
        foreach (ParticleSystem ps in root.GetComponentsInChildren<ParticleSystem>(true))
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
    }

    private static Transform FindDeepChild(Transform parent, string childName)
    {
        foreach (Transform t in parent.GetComponentsInChildren<Transform>(true))
        {
            if (t.name == childName)
                return t;
        }
        return null;
    }

    private static void CacheOriginalTransform(
        GameObject go,
        ref Transform parent,
        ref Vector3 localPos,
        ref Quaternion localRot,
        ref Vector3 localScale)
    {
        if (go == null) return;

        Transform t = go.transform;
        parent = t.parent;
        localPos = t.localPosition;
        localRot = t.localRotation;
        localScale = t.localScale;
    }

    private static void DetachToWorld(Transform effectTransform)
    {
        effectTransform.SetParent(null, true);
    }

    private void ResetExplosionState()
    {
        hasTriggered = false;
        explosionPlayed = false;
        firePlayed = false;

        if (effectRoutine != null)
        {
            StopCoroutine(effectRoutine);
            effectRoutine = null;
        }

        RestoreEffect(explosionEffect, explosionOriginalParent,
            explosionOriginalLocalPos, explosionOriginalLocalRot, explosionOriginalLocalScale);
        RestoreEffect(fireEffect, fireOriginalParent,
            fireOriginalLocalPos, fireOriginalLocalRot, fireOriginalLocalScale);

        ResolveEffectReferences();
        SuppressAutoPlay();
    }

    private static void RestoreEffect(
        GameObject effect,
        Transform originalParent,
        Vector3 localPos,
        Quaternion localRot,
        Vector3 localScale)
    {
        if (effect == null) return;

        StopAllParticlesUnder(effect.transform);

        if (originalParent != null)
        {
            Transform t = effect.transform;
            t.SetParent(originalParent, false);
            t.localPosition = localPos;
            t.localRotation = localRot;
            t.localScale = localScale;
        }

        effect.SetActive(false);
    }
}
