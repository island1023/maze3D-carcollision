using UnityEngine;
using System.Collections.Generic;

public class VehicleAppearance : MonoBehaviour
{
    [Header("外观模型")]
    public GameObject normalModel;
    public GameObject lightDamageModel;
    public GameObject heavyDamageModel;
    public GameObject destroyedModel;

    [Header("可脱落部件")]
    public List<GameObject> debrisParts;

    private VehiclePerformanceSystem vps;
    private VehicleState lastState;
    private bool hasDebrisSeparated = false;
    private bool lockedToRolloverVisual;

    private List<GameObject> detachedDebris = new List<GameObject>();

    void Awake()
    {
        ResolveDestroyedModelIfMissing();
        RemoveNestedRigidbodiesFromAllModels();
    }

    void Start()
    {
        vps = GetComponent<VehiclePerformanceSystem>();
        lastState = vps.state;
        UpdateModelVisibility(vps.state);
    }

    void Update()
    {
        if (lockedToRolloverVisual)
            return;

        if (vps.state != lastState)
        {
            lastState = vps.state;
            UpdateModelVisibility(lastState);
        }
    }

    void UpdateModelVisibility(VehicleState state)
    {
        if (normalModel) normalModel.SetActive(state == VehicleState.Normal);
        if (lightDamageModel) lightDamageModel.SetActive(state == VehicleState.Damaged);
        if (heavyDamageModel) heavyDamageModel.SetActive(state == VehicleState.HeavyDamaged);

        if (destroyedModel)
        {
            destroyedModel.SetActive(state == VehicleState.Destroyed);
        }
        else if (state == VehicleState.Destroyed)
        {
            if (heavyDamageModel) heavyDamageModel.SetActive(true);
            else if (lightDamageModel) lightDamageModel.SetActive(true);
            else if (normalModel) normalModel.SetActive(true);
        }

        if (state == VehicleState.HeavyDamaged && !hasDebrisSeparated)
            SeparateDebris();
    }

    private void RemoveNestedRigidbodiesFromAllModels()
    {
        RemoveNestedRigidbodies(normalModel);
        RemoveNestedRigidbodies(lightDamageModel);
        RemoveNestedRigidbodies(heavyDamageModel);
        RemoveNestedRigidbodies(destroyedModel);
    }

    private void RemoveNestedRigidbodies(GameObject model)
    {
        if (model == null)
            return;

        Rigidbody vehicleRb = GetComponent<Rigidbody>();
        foreach (Rigidbody rb in model.GetComponentsInChildren<Rigidbody>(true))
        {
            if (rb == null || rb == vehicleRb)
                continue;

            Destroy(rb);
        }
    }

    private void ResolveDestroyedModelIfMissing()
    {
        if (destroyedModel != null) return;

        GameObject fallback = null;
        foreach (Transform child in GetComponentsInChildren<Transform>(true))
        {
            string childName = child.name;
            bool isRolloverRoot = childName == "car_rollover"
                || childName == "truck-rollover"
                || childName.Contains("rollover");
            if (!isRolloverRoot)
                continue;

            if (FindChildByName(child, "car_damage4") != null
                || FindChildByName(child, "truck_damage") != null)
            {
                destroyedModel = child.gameObject;
                return;
            }

            if (fallback == null)
                fallback = child.gameObject;
        }

        destroyedModel = fallback;
    }

    public void ForceShowRolloverModel()
    {
        ResolveDestroyedModelIfMissing();
        lockedToRolloverVisual = true;
        lastState = VehicleState.Destroyed;

        if (normalModel) normalModel.SetActive(false);
        if (lightDamageModel) lightDamageModel.SetActive(false);
        if (heavyDamageModel) heavyDamageModel.SetActive(false);

        if (destroyedModel)
        {
            destroyedModel.SetActive(true);
            RemoveNestedRigidbodies(destroyedModel);

            Transform damageVisual = FindChildByName(destroyedModel.transform, "car_damage4")
                ?? FindChildByName(destroyedModel.transform, "truck_damage");
            if (damageVisual != null)
                damageVisual.gameObject.SetActive(true);
        }
        else
        {
            UpdateModelVisibility(VehicleState.Destroyed);
        }
    }

    private static Transform FindChildByName(Transform root, string childName)
    {
        foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
        {
            if (t.name == childName)
                return t;
        }
        return null;
    }

    void SeparateDebris()
    {
        foreach (GameObject part in debrisParts)
        {
            if (part == null) continue;

            part.transform.SetParent(null);
            detachedDebris.Add(part);

            Rigidbody rb = part.AddComponent<Rigidbody>();
            rb.mass = 1f;

            if (part.GetComponent<Collider>() == null)
                part.AddComponent<MeshCollider>().convex = true;

            rb.AddExplosionForce(300f, transform.position, 2f);
        }

        hasDebrisSeparated = true;
    }

    public void ResetAppearance()
    {
        foreach (GameObject debris in detachedDebris)
        {
            if (debris != null)
                Destroy(debris);
        }
        detachedDebris.Clear();

        lockedToRolloverVisual = false;
        hasDebrisSeparated = false;

        VehicleWallFrictionEffect friction = GetComponent<VehicleWallFrictionEffect>();
        if (friction != null)
            friction.ClearVehicleScratches();

        if (vps != null)
            UpdateModelVisibility(vps.state);
        else
            UpdateModelVisibility(VehicleState.Normal);
    }
}
