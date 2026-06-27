using UnityEngine;
using System.Collections.Generic;

[System.Serializable]
public class TruckAxle
{
    public WheelCollider leftWheel;
    public WheelCollider rightWheel;
    public bool isSteering;
    public bool isMotor;
    public bool isBraking = true;
}

public class Truck_VehicleController : MonoBehaviour
{
    public VehicleConfig config;

    [Header("Axle Setup")]
    public List<TruckAxle> axles;

    [Header("Center of Mass")]
    public Vector3 customCenterOfMass = new Vector3(0, -0.5f, 0);

    [Header("Anti-Skid")]
    public bool enableAntiSkid = true;
    public bool lockLateralMovement = false;
    public float skidThreshold = 0.8f;
    public float antiSkidForce = 1.5f;

    private Rigidbody rb;
    public float currentMotorTorque;
    public float currentSteerAngle;
    public float currentMaxSpeed;

    private float motorInput;
    private float steerInput;
    private float smoothMotor;
    private float smoothSteer;
    private float inputBlockedUntil;
    private bool requireKeyRelease;
    private bool drivingLocked;

    [Header("Input Tuning")]
    public float motorSmoothSpeed = 14f;
    public bool useRawSteerInput = true;
    public float steerSmoothSpeed = 28f;

    private void Start()
    {
        rb = GetComponent<Rigidbody>();
        if (config == null) return;

        rb.mass = config.mass;
        rb.drag = 0.04f;
        rb.angularDrag = 0.5f;
        rb.centerOfMass = customCenterOfMass;

        SyncAllWheelParameters();

        currentMotorTorque = config.motorTorque;
        currentSteerAngle = config.steerAngle;
        currentMaxSpeed = config.maxSpeed;
    }

    private void SyncAllWheelParameters()
    {
        foreach (TruckAxle axle in axles)
        {
            if (axle.leftWheel == null || axle.rightWheel == null) continue;
            axle.rightWheel.forwardFriction = axle.leftWheel.forwardFriction;
            axle.rightWheel.sidewaysFriction = axle.leftWheel.sidewaysFriction;
            axle.rightWheel.suspensionSpring = axle.leftWheel.suspensionSpring;
            axle.rightWheel.suspensionDistance = axle.leftWheel.suspensionDistance;
            axle.rightWheel.mass = axle.leftWheel.mass;
            axle.rightWheel.radius = axle.leftWheel.radius;
            Vector3 rightCenter = axle.rightWheel.center;
            rightCenter.x = -axle.leftWheel.center.x;
            axle.rightWheel.center = rightCenter;
        }
    }

    private void Update()
    {
        if (requireKeyRelease)
        {
            if (!IsMovementKeyHeld())
                requireKeyRelease = false;
            else
            {
                ZeroInputs();
                return;
            }
        }

        if (drivingLocked || Time.unscaledTime < inputBlockedUntil)
        {
            ZeroInputs();
            return;
        }

        float rawMotor = Input.GetAxisRaw("Vertical");
        float rawSteer = useRawSteerInput
            ? Input.GetAxisRaw("Horizontal")
            : Input.GetAxis("Horizontal");

        if (Mathf.Abs(rawSteer) < 0.02f) rawSteer = 0f;

        smoothMotor = Mathf.MoveTowards(smoothMotor, rawMotor, Time.deltaTime * motorSmoothSpeed);
        steerInput = useRawSteerInput
            ? rawSteer
            : Mathf.MoveTowards(smoothSteer, rawSteer, Time.deltaTime * steerSmoothSpeed);
        if (!useRawSteerInput) smoothSteer = steerInput;
        motorInput = smoothMotor;
    }

    private void FixedUpdate()
    {
        if (drivingLocked || Time.unscaledTime < inputBlockedUntil || requireKeyRelease)
        {
            ZeroWheelDrive();
            ApplyHandbrake();
            if (drivingLocked)
                ZeroRigidbodyMotionIfNeeded();
            return;
        }

        SyncAllWheelParameters();

        if (lockLateralMovement)
        {
            Vector3 localVel = transform.InverseTransformDirection(rb.velocity);
            localVel.x = 0f;
            rb.velocity = transform.TransformDirection(localVel);
        }

        DriveAndBrake();
        Steer();
        ClampSpeed();

        if (enableAntiSkid && !lockLateralMovement)
            AntiSkid();
    }

    private void DriveAndBrake()
    {
        if (currentMotorTorque <= 0f || currentMaxSpeed <= 0f)
        {
            ZeroWheelDrive();
            return;
        }

        bool isBraking = Input.GetKey(KeyCode.Space);
        float torqueMultiplier = (motorInput < 0) ? 0.6f : 1f;
        float finalTorque = motorInput * currentMotorTorque * torqueMultiplier;

        foreach (TruckAxle axle in axles)
        {
            if (axle.isMotor)
            {
                if (isBraking)
                {
                    axle.leftWheel.motorTorque = 0;
                    axle.rightWheel.motorTorque = 0;
                }
                else
                {
                    axle.leftWheel.motorTorque = finalTorque;
                    axle.rightWheel.motorTorque = finalTorque;
                }
            }

            if (axle.isBraking)
            {
                float appliedBrake = isBraking ? config.brakeForce : 0f;
                axle.leftWheel.brakeTorque = appliedBrake;
                axle.rightWheel.brakeTorque = appliedBrake;
            }
        }
    }

    private void Steer()
    {
        float speedKmh = rb.velocity.magnitude * 3.6f;
        float highSpeedFactor = speedKmh <= 30f
            ? 1f
            : Mathf.Lerp(1f, 0.55f, (speedKmh - 30f) / 120f);
        float steer = steerInput * currentSteerAngle * highSpeedFactor;

        foreach (TruckAxle axle in axles)
        {
            if (axle.isSteering)
            {
                axle.leftWheel.steerAngle = steer;
                axle.rightWheel.steerAngle = steer;
            }
        }
    }

    private void AntiSkid()
    {
        Vector3 localVel = transform.InverseTransformDirection(rb.velocity);
        float lateralSpeed = localVel.x;
        if (Mathf.Abs(lateralSpeed) > skidThreshold)
        {
            float force = -lateralSpeed * rb.mass * antiSkidForce;
            force = Mathf.Clamp(force, -5000f, 5000f);
            rb.AddForce(transform.right * force, ForceMode.Force);
        }
    }

    private void ClampSpeed()
    {
        if (currentMaxSpeed <= 0f)
        {
            rb.velocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
            ZeroWheelDrive();
            return;
        }

        float maxMs = currentMaxSpeed / 3.6f;
        if (rb.velocity.magnitude > maxMs)
            rb.velocity = rb.velocity.normalized * maxMs;
    }

    private static bool IsMovementKeyHeld()
    {
        return Input.GetKey(KeyCode.W) || Input.GetKey(KeyCode.S)
            || Input.GetKey(KeyCode.A) || Input.GetKey(KeyCode.D)
            || Input.GetKey(KeyCode.UpArrow) || Input.GetKey(KeyCode.DownArrow)
            || Input.GetKey(KeyCode.LeftArrow) || Input.GetKey(KeyCode.RightArrow)
            || Mathf.Abs(Input.GetAxisRaw("Vertical")) > 0.01f
            || Mathf.Abs(Input.GetAxisRaw("Horizontal")) > 0.01f;
    }

    private void ZeroInputs()
    {
        motorInput = 0f;
        steerInput = 0f;
        smoothMotor = 0f;
        smoothSteer = 0f;
    }

    private void ZeroWheelDrive()
    {
        if (axles == null) return;
        foreach (var axle in axles)
        {
            if (axle.leftWheel != null)
                axle.leftWheel.motorTorque = 0f;
            if (axle.rightWheel != null)
                axle.rightWheel.motorTorque = 0f;
        }
    }

    private void ApplyHandbrake()
    {
        if (axles == null || config == null) return;
        foreach (var axle in axles)
        {
            if (axle.isBraking)
            {
                if (axle.leftWheel != null) axle.leftWheel.brakeTorque = config.brakeForce;
                if (axle.rightWheel != null) axle.rightWheel.brakeTorque = config.brakeForce;
            }
        }
    }

    private void ZeroRigidbodyMotionIfNeeded()
    {
        if (rb.velocity.sqrMagnitude > 0.0001f || rb.angularVelocity.sqrMagnitude > 0.0001f)
        {
            rb.velocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
        }
    }

    public void ApplyPerformance(float factor)
    {
        currentMotorTorque = config.motorTorque * factor;
        currentSteerAngle = config.steerAngle * factor;
        currentMaxSpeed = config.maxSpeed * factor;
    }

    public void LockDrivingUntilReset()
    {
        ZeroInputs();
        drivingLocked = true;
        requireKeyRelease = false;
        ZeroWheelDrive();
        ApplyHandbrake();
        ZeroRigidbodyMotionIfNeeded();
    }

    public void UnlockDriving()
    {
        drivingLocked = false;
    }

    public void PrepareForDriving()
    {
        ZeroInputs();
        drivingLocked = false;
        requireKeyRelease = false;
        inputBlockedUntil = 0f;
        ZeroWheelDrive();
        if (axles == null) return;
        foreach (var axle in axles)
        {
            if (axle.leftWheel != null) axle.leftWheel.brakeTorque = 0f;
            if (axle.rightWheel != null) axle.rightWheel.brakeTorque = 0f;
            if (axle.isSteering)
            {
                if (axle.leftWheel != null) axle.leftWheel.steerAngle = 0f;
                if (axle.rightWheel != null) axle.rightWheel.steerAngle = 0f;
            }
        }
    }

    public void ResetInputState()
    {
        ZeroInputs();
        drivingLocked = false;
        requireKeyRelease = true;
        inputBlockedUntil = Time.unscaledTime + 0.5f;
        ZeroWheelDrive();
        ApplyHandbrake();
        if (axles == null) return;
        foreach (var axle in axles)
        {
            if (axle.isSteering)
            {
                if (axle.leftWheel != null) axle.leftWheel.steerAngle = 0f;
                if (axle.rightWheel != null) axle.rightWheel.steerAngle = 0f;
            }
        }
    }
}
