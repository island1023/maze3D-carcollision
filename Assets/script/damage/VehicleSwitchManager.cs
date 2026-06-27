using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

[System.Serializable]
public class PlayableVehicle
{
    [FormerlySerializedAs("\u8f66\u8f86\u540d\u79f0")]
    public string vehicleName;

    [FormerlySerializedAs("\u8f66\u8f86\u63a7\u5236\u5668")]
    public VehicleController vehicleController;

    [FormerlySerializedAs("\u5361\u8f66\u63a7\u5236\u5668")]
    public Truck_VehicleController truckController;

    [FormerlySerializedAs("\u4e13\u5c5e\u6444\u50cf\u673a")]
    public Camera dedicatedCamera;
}

public class VehicleSwitchManager : MonoBehaviour
{
    [FormerlySerializedAs("\u8f66\u8f86\u5217\u8868")]
    public PlayableVehicle[] vehicleList;

    private int currentIndex = 0;
    private Vector3 lastVelocity;
    private float currentAcceleration;
    private float currentGForce;
    private float lastImpactForce;
    private int totalCollisionCount;
    private readonly List<float> recentCollisionTimes = new List<float>();
    private float forwardFrictionStiffness = 1f;
    private float sidewaysFrictionStiffness = 1f;
    private bool frictionInitialized;
    private Vector2 statsScrollPosition;

    private const float CollisionRateWindowSeconds = 60f;

    void OnEnable()
    {
        SimulationEvents.OnVehicleDamaged += OnVehicleDamaged;
    }

    void OnDisable()
    {
        SimulationEvents.OnVehicleDamaged -= OnVehicleDamaged;
    }

    void Start()
    {
        if (vehicleList.Length > 0)
            SwitchVehicle(0);
    }

    void Update()
    {
        for (int i = 0; i < vehicleList.Length; i++)
        {
            if (Input.GetKeyDown(KeyCode.Alpha1 + i))
                SwitchVehicle(i);
        }

        if (Input.GetKeyDown(KeyCode.Tab))
            ResetAllToInitialState();

        PruneRecentCollisions();
        UpdateMotionMetrics();
    }

    public void SwitchVehicle(int index)
    {
        if (index < 0 || index >= vehicleList.Length) return;

        // 被撞车辆可能已将全局 timeScale 设为 0，切换时必须恢复，否则新车无法行驶
        Time.timeScale = 1f;
        currentIndex = index;
        frictionInitialized = false;
        ResetCollisionStats();

        for (int i = 0; i < vehicleList.Length; i++)
        {
            bool isActive = (i == currentIndex);
            PlayableVehicle pv = vehicleList[i];
            GameObject vehicle = GetVehicleObject(pv);

            if (!isActive)
                CancelGlobalPauseForVehicle(vehicle);

            MonoBehaviour activeController = GetActiveController(pv);
            if (activeController != null)
            {
                VehiclePerformanceSystem vps = activeController.GetComponent<VehiclePerformanceSystem>();
                bool canDrive = vps == null || vps.state != VehicleState.Destroyed;
                activeController.enabled = isActive && canDrive;

                if (isActive && canDrive)
                {
                    if (activeController is VehicleController vehicleController)
                        vehicleController.PrepareForDriving();
                    else if (activeController is Truck_VehicleController truckController)
                        truckController.PrepareForDriving();
                }

                Rigidbody rb = activeController.GetComponent<Rigidbody>();
                if (rb != null)
                    rb.drag = isActive ? 0.05f : 1f;
            }

            if (pv.dedicatedCamera != null)
            {
                pv.dedicatedCamera.gameObject.SetActive(isActive);
                AudioListener listener = pv.dedicatedCamera.GetComponent<AudioListener>();
                if (listener != null)
                    listener.enabled = isActive;
            }
        }
    }

    public void ResetAllToInitialState()
    {
        Time.timeScale = 1f;

        int activeIndex = currentIndex;

        foreach (PlayableVehicle pv in vehicleList)
        {
            GameObject vehicle = GetVehicleObject(pv);
            if (vehicle == null) continue;

            SUVCollisionManager suvManager = vehicle.GetComponentInChildren<SUVCollisionManager>();
            if (suvManager != null)
            {
                suvManager.ResetVehicle();
                continue;
            }

            TruckCollisionManager truckManager = vehicle.GetComponentInChildren<TruckCollisionManager>();
            if (truckManager != null)
            {
                truckManager.ResetVehicle();
                continue;
            }

            SedanCollisionManager sedanManager = vehicle.GetComponentInChildren<SedanCollisionManager>();
            if (sedanManager != null)
                sedanManager.ResetVehicle();
        }

        SwitchVehicle(activeIndex);
        ResetCollisionStats();
    }

    void OnGUI()
    {
        GUIStyle titleStyle = new GUIStyle(GUI.skin.label);
        titleStyle.fontSize = 18;
        titleStyle.fontStyle = FontStyle.Bold;
        titleStyle.normal.textColor = Color.white;

        GUIStyle labelStyle = new GUIStyle(GUI.skin.label);
        labelStyle.fontSize = 14;
        labelStyle.normal.textColor = Color.white;

        GUIStyle hintStyle = new GUIStyle(GUI.skin.label);
        hintStyle.fontSize = 12;
        hintStyle.normal.textColor = new Color(0.8f, 0.8f, 0.8f);

        GUIStyle sectionStyle = new GUIStyle(labelStyle);
        sectionStyle.fontStyle = FontStyle.Bold;

        GUILayout.BeginArea(new Rect(20, 20, 320, Screen.height - 40));
        GUILayout.BeginVertical("box");

        GUILayout.Label("\u8f66\u8f86\u78b0\u649e\u865a\u62df\u4eff\u771f\u7cfb\u7edf", titleStyle);
        GUILayout.Space(8);

        for (int i = 0; i < vehicleList.Length; i++)
        {
            PlayableVehicle pv = vehicleList[i];
            string displayName = string.IsNullOrEmpty(pv.vehicleName) ? $"Vehicle {i + 1}" : pv.vehicleName;
            string buttonText = (i == currentIndex)
                ? $"[ \u9a7e\u9a76\u4e2d ] {displayName}"
                : $"\u6309 {i + 1} \u5207\u6362: {displayName}";

            if (GUILayout.Button(buttonText, GUILayout.Height(36)))
                SwitchVehicle(i);

            GUILayout.Space(4);
        }

        GUILayout.Space(8);
        statsScrollPosition = GUILayout.BeginScrollView(statsScrollPosition, GUILayout.ExpandHeight(true));
        DrawSimulationPanel(sectionStyle, labelStyle, hintStyle);
        GUILayout.EndScrollView();

        GUILayout.Space(6);
        GUILayout.Label("\u6309 Tab \u91cd\u7f6e\u5f53\u524d\u8f66\u8f86", hintStyle);

        GUILayout.EndVertical();
        GUILayout.EndArea();
    }

    private void DrawSimulationPanel(GUIStyle sectionStyle, GUIStyle labelStyle, GUIStyle hintStyle)
    {
        if (currentIndex < 0 || currentIndex >= vehicleList.Length) return;

        PlayableVehicle current = vehicleList[currentIndex];
        GameObject vehicle = GetVehicleObject(current);
        if (vehicle == null) return;

        Rigidbody rb = vehicle.GetComponent<Rigidbody>();
        if (rb == null) return;

        VehiclePerformanceSystem vps = vehicle.GetComponent<VehiclePerformanceSystem>();
        VehicleDriveSnapshot drive = GetDriveSnapshot(current);
        ForceSnapshot force = GetForceSnapshot();
        PhysicsSnapshot physics = GetPhysicsSnapshot(current, rb);
        float collisionRate = GetCollisionRatePerMinute();

        DrawSectionHeader(sectionStyle, "\u5f53\u524d\u8f66\u8f86");
        GUILayout.Label($"{current.vehicleName}", labelStyle);

        DrawSectionHeader(sectionStyle, "\u8fd0\u52a8\u9a71\u52a8");
        GUILayout.Label($"\u901f\u5ea6: {drive.speedKmh:F1} km/h", labelStyle);
        GUILayout.Label($"\u6cb9\u95e8\u8f93\u5165: {drive.throttleInput:P0}", labelStyle);
        // GUILayout.Label($"\u7535\u673a\u626d\u77e9: {drive.motorTorque:F0} Nm", labelStyle);
        // GUILayout.Label($"\u5236\u52a8\u626d\u77e9: {drive.brakeTorque:F0} Nm", labelStyle);
        GUILayout.Label($"\u8f6c\u5411\u89d2: {drive.steerAngle:F1}\u00b0", labelStyle);
        GUILayout.Label($"\u6700\u5927\u901f\u5ea6\u9650\u5236: {drive.maxSpeedLimit:F0} km/h", labelStyle);

        DrawSectionHeader(sectionStyle, "\u529b");
        GUILayout.Label($"\u78b0\u649e\u51b2\u51fb\u529b: {lastImpactForce:F0} N", labelStyle);
        GUILayout.Label($"\u5408\u52a0\u901f\u5ea6: {force.acceleration:F2} m/s\u00b2", labelStyle);
        // GUILayout.Label($"\u7efc\u5408 G \u529b: {force.gForce:F2} g", labelStyle);

        DrawSectionHeader(sectionStyle, "\u7269\u7406\u5c5e\u6027\u53c2\u6570");
        GUILayout.Label($"\u8f66\u8eab\u8d28\u91cf: {physics.mass:F0} kg", labelStyle);
        GUILayout.Label($"\u7a7a\u6c14\u963b\u529b: {physics.drag:F2}", labelStyle);
        GUILayout.Label($"\u524d\u5411\u6293\u5730\u529b: {physics.forwardFriction:F2}", labelStyle);
        GUILayout.Label($"\u4fa7\u5411\u6293\u5730\u529b: {physics.sidewaysFriction:F2}", labelStyle);
        GUILayout.Label($"\u914d\u7f6e\u6700\u5927\u901f\u5ea6: {physics.configMaxSpeed:F0} km/h", labelStyle);
        // GUILayout.Label($"\u914d\u7f6e\u7535\u673a\u626d\u77e9: {physics.configMotorTorque:F0} Nm", labelStyle);

        if (!frictionInitialized)
            InitializeFrictionSliders(current);

        GUILayout.Space(4);
        DrawSectionHeader(sectionStyle, "\u8f6e\u80ce\u6293\u5730\u529b");
        GUILayout.Label("\u5f71\u54cd\u52a0\u901f\u4e0e\u6253\u6ed1\uff0c\u4e0d\u6539\u53d8\u7535\u673a\u626d\u77e9\u4e0e\u6700\u5927\u901f\u5ea6\u4e0a\u9650", hintStyle);
        float newForwardFriction = GUILayout.HorizontalSlider(forwardFrictionStiffness, 0.2f, 5f);
        GUILayout.Label($"\u524d\u5411\u6293\u5730\u529b: {newForwardFriction:F2}", labelStyle);
        float newSidewaysFriction = GUILayout.HorizontalSlider(sidewaysFrictionStiffness, 0.2f, 5f);
        GUILayout.Label($"\u4fa7\u5411\u6293\u5730\u529b: {newSidewaysFriction:F2}", labelStyle);

        if (!Mathf.Approximately(newForwardFriction, forwardFrictionStiffness) ||
            !Mathf.Approximately(newSidewaysFriction, sidewaysFrictionStiffness))
        {
            forwardFrictionStiffness = newForwardFriction;
            sidewaysFrictionStiffness = newSidewaysFriction;
            ApplyFrictionStiffness(current, forwardFrictionStiffness, sidewaysFrictionStiffness);
            SimulationEvents.TriggerPhysicsChange(forwardFrictionStiffness, sidewaysFrictionStiffness);
        }

        DrawSectionHeader(sectionStyle, "\u78b0\u649e\u7edf\u8ba1");
        GUILayout.Label($"\u7d2f\u8ba1\u78b0\u649e\u6b21\u6570: {totalCollisionCount}", labelStyle);
        GUILayout.Label($"\u78b0\u649e\u7387: {collisionRate:F1} \u6b21/\u5206\u949f", labelStyle);

        DrawSectionHeader(sectionStyle, "\u5b9e\u65f6\u62a5\u5e9f\u72b6\u6001");
        if (vps != null)
        {
            float damageRatio = vps.maxDamage > 0f ? vps.damage / vps.maxDamage : 0f;
            GUILayout.Label($"\u635f\u4f24\u503c: {vps.damage:F1} / {vps.maxDamage:F0}", labelStyle);
            GUILayout.Label($"\u72b6\u6001: {GetStateDisplayName(vps.state)}", labelStyle);
            DrawDamageBar(damageRatio, vps.state);
        }
        else
        {
            GUILayout.Label("\u72b6\u6001: \u672a\u7ed1\u5b9a\u635f\u4f24\u7cfb\u7edf", labelStyle);
        }
    }

    private void DrawSectionHeader(GUIStyle style, string text)
    {
        GUILayout.Space(8);
        GUILayout.Label($"--- {text} ---", style);
    }

    private void DrawDamageBar(float ratio, VehicleState state)
    {
        ratio = Mathf.Clamp01(ratio);
        Color barColor = state == VehicleState.Destroyed
            ? new Color(0.85f, 0.15f, 0.15f)
            : Color.Lerp(new Color(0.2f, 0.85f, 0.35f), new Color(0.95f, 0.55f, 0.1f), ratio);

        Rect backgroundRect = GUILayoutUtility.GetRect(260, 18);
        Color previousColor = GUI.color;
        GUI.color = new Color(0.15f, 0.15f, 0.15f, 0.9f);
        GUI.DrawTexture(backgroundRect, Texture2D.whiteTexture);
        GUI.color = barColor;
        Rect fillRect = backgroundRect;
        fillRect.width *= ratio;
        GUI.DrawTexture(fillRect, Texture2D.whiteTexture);
        GUI.color = previousColor;

        GUIStyle barLabelStyle = new GUIStyle(GUI.skin.label)
        {
            alignment = TextAnchor.MiddleCenter,
            fontSize = 11,
            normal = { textColor = Color.white }
        };
        GUI.Label(backgroundRect, $"{ratio * 100f:F0}%", barLabelStyle);
    }

    private struct VehicleDriveSnapshot
    {
        public float speedKmh;
        public float throttleInput;
        public float motorTorque;
        public float brakeTorque;
        public float steerAngle;
        public float maxSpeedLimit;
    }

    private struct ForceSnapshot
    {
        public float acceleration;
        public float gForce;
    }

    private struct PhysicsSnapshot
    {
        public float mass;
        public float drag;
        public float forwardFriction;
        public float sidewaysFriction;
        public float configMaxSpeed;
        public float configMotorTorque;
    }

    private VehicleDriveSnapshot GetDriveSnapshot(PlayableVehicle vehicle)
    {
        VehicleDriveSnapshot snapshot = new VehicleDriveSnapshot
        {
            throttleInput = Mathf.Abs(Input.GetAxisRaw("Vertical"))
        };

        GameObject vehicleObject = GetVehicleObject(vehicle);
        Rigidbody rb = vehicleObject != null ? vehicleObject.GetComponent<Rigidbody>() : null;
        if (rb != null)
            snapshot.speedKmh = rb.velocity.magnitude * 3.6f;

        if (vehicle.vehicleController != null)
        {
            snapshot.motorTorque = vehicle.vehicleController.RL.motorTorque;
            snapshot.brakeTorque = vehicle.vehicleController.RL.brakeTorque;
            snapshot.steerAngle = vehicle.vehicleController.FL.steerAngle;
            snapshot.maxSpeedLimit = vehicle.vehicleController.currentMaxSpeed;
        }
        else if (vehicle.truckController != null)
        {
            foreach (TruckAxle axle in vehicle.truckController.axles)
            {
                if (snapshot.motorTorque <= 0f && axle.isMotor && axle.leftWheel != null)
                    snapshot.motorTorque = axle.leftWheel.motorTorque;
                if (snapshot.brakeTorque <= 0f && axle.isBraking && axle.leftWheel != null)
                    snapshot.brakeTorque = axle.leftWheel.brakeTorque;
                if (snapshot.steerAngle == 0f && axle.isSteering && axle.leftWheel != null)
                    snapshot.steerAngle = axle.leftWheel.steerAngle;
            }
            snapshot.maxSpeedLimit = vehicle.truckController.currentMaxSpeed;
        }

        return snapshot;
    }

    private ForceSnapshot GetForceSnapshot()
    {
        return new ForceSnapshot
        {
            acceleration = currentAcceleration,
            gForce = currentGForce
        };
    }

    private void UpdateMotionMetrics()
    {
        if (currentIndex < 0 || currentIndex >= vehicleList.Length)
            return;

        GameObject vehicle = GetVehicleObject(vehicleList[currentIndex]);
        if (vehicle == null)
            return;

        Rigidbody rb = vehicle.GetComponent<Rigidbody>();
        if (rb == null)
            return;

        float speed = rb.velocity.magnitude;
        if (speed < 0.5f)
        {
            currentAcceleration = 0f;
            currentGForce = 0f;
        }
        else
        {
            Vector3 acceleration = (rb.velocity - lastVelocity) / Mathf.Max(Time.deltaTime, 0.0001f);
            currentAcceleration = acceleration.magnitude;
            currentGForce = currentAcceleration / 9.81f;
        }

        lastVelocity = rb.velocity;
        lastImpactForce = Mathf.Lerp(lastImpactForce, 0f, Time.deltaTime * 1.5f);
    }

    private PhysicsSnapshot GetPhysicsSnapshot(PlayableVehicle vehicle, Rigidbody rb)
    {
        PhysicsSnapshot snapshot = new PhysicsSnapshot
        {
            mass = rb.mass,
            drag = rb.drag
        };

        List<WheelCollider> wheels = GetWheelColliders(vehicle);
        if (wheels.Count > 0)
        {
            snapshot.forwardFriction = wheels[0].forwardFriction.stiffness;
            snapshot.sidewaysFriction = wheels[0].sidewaysFriction.stiffness;
        }

        VehicleConfig config = vehicle.vehicleController != null
            ? vehicle.vehicleController.config
            : vehicle.truckController != null ? vehicle.truckController.config : null;

        if (config != null)
        {
            snapshot.configMaxSpeed = config.maxSpeed;
            snapshot.configMotorTorque = config.motorTorque;
        }

        return snapshot;
    }

    private float GetCollisionRatePerMinute()
    {
        if (recentCollisionTimes.Count == 0)
            return 0f;

        return recentCollisionTimes.Count * (60f / CollisionRateWindowSeconds);
    }

    private void InitializeFrictionSliders(PlayableVehicle vehicle)
    {
        List<WheelCollider> wheels = GetWheelColliders(vehicle);
        if (wheels.Count == 0)
            return;

        forwardFrictionStiffness = Mathf.Clamp(wheels[0].forwardFriction.stiffness, 0.2f, 5f);
        sidewaysFrictionStiffness = Mathf.Clamp(wheels[0].sidewaysFriction.stiffness, 0.2f, 5f);
        ApplyFrictionStiffness(vehicle, forwardFrictionStiffness, sidewaysFrictionStiffness);
        frictionInitialized = true;
    }

    private void ApplyFrictionStiffness(PlayableVehicle vehicle, float forwardStiffness, float sidewaysStiffness)
    {
        foreach (WheelCollider wheel in GetWheelColliders(vehicle))
        {
            if (wheel == null) continue;

            WheelFrictionCurve forward = wheel.forwardFriction;
            forward.stiffness = forwardStiffness;
            wheel.forwardFriction = forward;

            WheelFrictionCurve sideways = wheel.sidewaysFriction;
            sideways.stiffness = sidewaysStiffness;
            wheel.sidewaysFriction = sideways;
        }
    }

    private static List<WheelCollider> GetWheelColliders(PlayableVehicle vehicle)
    {
        GameObject vehicleObject = GetVehicleObject(vehicle);
        return vehicleObject != null
            ? GetWheelCollidersFromObject(vehicleObject)
            : new List<WheelCollider>();
    }

    private static List<WheelCollider> GetWheelCollidersFromObject(GameObject vehicle)
    {
        List<WheelCollider> wheels = new List<WheelCollider>();

        VehicleController controller = vehicle.GetComponent<VehicleController>();
        if (controller != null)
        {
            AddWheel(wheels, controller.FL);
            AddWheel(wheels, controller.FR);
            AddWheel(wheels, controller.RL);
            AddWheel(wheels, controller.RR);
            return wheels;
        }

        Truck_VehicleController truckController = vehicle.GetComponent<Truck_VehicleController>();
        if (truckController?.axles != null)
        {
            foreach (TruckAxle axle in truckController.axles)
            {
                AddWheel(wheels, axle.leftWheel);
                AddWheel(wheels, axle.rightWheel);
            }
        }

        return wheels;
    }

    private static void AddWheel(List<WheelCollider> wheels, WheelCollider wheel)
    {
        if (wheel != null)
            wheels.Add(wheel);
    }

    private static string GetStateDisplayName(VehicleState state)
    {
        switch (state)
        {
            case VehicleState.Damaged:
                return "\u8f7b\u5ea6\u635f\u4f24";
            case VehicleState.HeavyDamaged:
                return "\u91cd\u5ea6\u635f\u4f24";
            case VehicleState.Destroyed:
                return "\u62a5\u5e9f";
            default:
                return "\u6b63\u5e38";
        }
    }

    private void OnVehicleDamaged(DamageData data)
    {
        if (currentIndex < 0 || currentIndex >= vehicleList.Length)
            return;

        GameObject currentVehicle = GetVehicleObject(vehicleList[currentIndex]);
        if (!IsCurrentVehicleCollision(data.victim, currentVehicle))
            return;

        lastImpactForce = data.impactForce;
        totalCollisionCount++;
        recentCollisionTimes.Add(Time.time);
    }

    private static bool IsCurrentVehicleCollision(GameObject victim, GameObject currentVehicle)
    {
        if (victim == null || currentVehicle == null)
            return false;

        return victim == currentVehicle || victim.transform.IsChildOf(currentVehicle.transform);
    }

    private void PruneRecentCollisions()
    {
        float cutoff = Time.time - CollisionRateWindowSeconds;
        for (int i = recentCollisionTimes.Count - 1; i >= 0; i--)
        {
            if (recentCollisionTimes[i] < cutoff)
                recentCollisionTimes.RemoveAt(i);
        }
    }

    private void ResetCollisionStats()
    {
        lastImpactForce = 0f;
        totalCollisionCount = 0;
        recentCollisionTimes.Clear();
        lastVelocity = Vector3.zero;
        currentAcceleration = 0f;
        currentGForce = 0f;
    }

    private static MonoBehaviour GetActiveController(PlayableVehicle pv)
    {
        if (pv.truckController != null) return pv.truckController;
        if (pv.vehicleController != null) return pv.vehicleController;
        return null;
    }

    private static GameObject GetVehicleObject(PlayableVehicle pv)
    {
        MonoBehaviour controller = GetActiveController(pv);
        return controller != null ? controller.gameObject : null;
    }

    private static void CancelGlobalPauseForVehicle(GameObject vehicle)
    {
        if (vehicle == null)
            return;

        TruckCollisionManager truckManager = vehicle.GetComponentInChildren<TruckCollisionManager>();
        if (truckManager != null)
            truckManager.CancelPendingGlobalPause();

        SUVCollisionManager suvManager = vehicle.GetComponentInChildren<SUVCollisionManager>();
        if (suvManager != null)
            suvManager.CancelPendingGlobalPause();
    }
}
