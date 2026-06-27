using UnityEngine;

public class VehicleController : MonoBehaviour
{
    public VehicleConfig config;

    public WheelCollider FL;
    public WheelCollider FR;
    public WheelCollider RL;
    public WheelCollider RR;

    private Rigidbody rb;

    [HideInInspector] public float currentMotorTorque;
    [HideInInspector] public float currentSteerAngle;
    [HideInInspector] public float currentMaxSpeed;

    private float motorInput;
    private float steerInput;
    private float smoothMotor;
    private float smoothSteer;
    private float inputBlockedUntil;
    private bool requireKeyRelease;

    [Header("Input Tuning")]
    public float motorSmoothSpeed = 14f;
    public bool useRawSteerInput = true;
    public float steerSmoothSpeed = 28f;

    [HideInInspector] public bool externalControlLock = false;

    private void Start()
    {
        rb = GetComponent<Rigidbody>();
        if (config != null)
        {
            rb.mass = config.mass;
            rb.drag = 0.04f;
            rb.centerOfMass = new Vector3(0, -0.5f, 0);
            currentMotorTorque = config.motorTorque;
            currentSteerAngle = config.steerAngle;
            currentMaxSpeed = config.maxSpeed;
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

        if (externalControlLock || Time.unscaledTime < inputBlockedUntil)
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
        if (externalControlLock || Time.unscaledTime < inputBlockedUntil || requireKeyRelease)
        {
            ClearWheelTorque();
            ApplyHandbrake();
            if (externalControlLock)
                ZeroRigidbodyMotionIfNeeded();
            return;
        }

        Drive();
        Steer();
        Brake();
        ClampSpeed();
    }

    private void Drive()
    {
        if (currentMotorTorque <= 0f || currentMaxSpeed <= 0f)
        {
            ClearWheelTorque();
            return;
        }

        if (Input.GetKey(KeyCode.Space))
        {
            ClearWheelTorque();
            return;
        }

        float torqueMultiplier = (motorInput < 0) ? 0.6f : 1f;
        float finalTorque = motorInput * currentMotorTorque * torqueMultiplier;

        RL.motorTorque = finalTorque;
        RR.motorTorque = finalTorque;
    }

    private void Steer()
    {
        float speedKmh = rb.velocity.magnitude * 3.6f;
        float highSpeedFactor = speedKmh <= 30f
            ? 1f
            : Mathf.Lerp(1f, 0.55f, (speedKmh - 30f) / 120f);
        float steer = steerInput * currentSteerAngle * highSpeedFactor;

        FL.steerAngle = steer;
        FR.steerAngle = steer;
    }

    private void Brake()
    {
        if (Input.GetKey(KeyCode.Space))
        {
            FL.brakeTorque = config.brakeForce;
            FR.brakeTorque = config.brakeForce;
            RL.brakeTorque = config.brakeForce;
            RR.brakeTorque = config.brakeForce;
        }
        else if (!externalControlLock && Time.unscaledTime >= inputBlockedUntil && !requireKeyRelease)
        {
            FL.brakeTorque = 0;
            FR.brakeTorque = 0;
            RL.brakeTorque = 0;
            RR.brakeTorque = 0;
        }
    }

    private void ClampSpeed()
    {
        if (currentMaxSpeed <= 0f)
        {
            rb.velocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
            ClearWheelTorque();
            return;
        }

        float maxMs = currentMaxSpeed / 3.6f;
        if (rb.velocity.magnitude > maxMs)
            rb.velocity = rb.velocity.normalized * maxMs;
    }

    private void ZeroRigidbodyMotionIfNeeded()
    {
        if (rb.velocity.sqrMagnitude > 0.0001f || rb.angularVelocity.sqrMagnitude > 0.0001f)
        {
            rb.velocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
        }
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
        smoothMotor = 0f;
        smoothSteer = 0f;
        motorInput = 0f;
        steerInput = 0f;
    }

    private void ApplyHandbrake()
    {
        if (FL == null || config == null) return;
        float brake = config.brakeForce;
        FL.brakeTorque = FR.brakeTorque = RL.brakeTorque = RR.brakeTorque = brake;
    }

    public void ApplyPerformance(float factor)
    {
        currentMotorTorque = config.motorTorque * factor;
        currentSteerAngle = config.steerAngle * factor;
        currentMaxSpeed = config.maxSpeed * factor;
    }

    public void ClearWheelTorque()
    {
        if (FL == null) return;
        FL.motorTorque = 0;
        FR.motorTorque = 0;
        RL.motorTorque = 0;
        RR.motorTorque = 0;
    }

    public void LockDrivingUntilReset()
    {
        ZeroInputs();
        externalControlLock = true;
        requireKeyRelease = false;
        ClearWheelTorque();
        ApplyHandbrake();
        ZeroRigidbodyMotionIfNeeded();
    }

    public void UnlockDriving()
    {
        externalControlLock = false;
    }

    public void PrepareForDriving()
    {
        ZeroInputs();
        externalControlLock = false;
        requireKeyRelease = false;
        inputBlockedUntil = 0f;
        ClearWheelTorque();
        if (FL == null) return;
        FL.steerAngle = FR.steerAngle = 0f;
        FL.brakeTorque = FR.brakeTorque = RL.brakeTorque = RR.brakeTorque = 0f;
    }

    public void ResetInputState()
    {
        ZeroInputs();
        externalControlLock = false;
        requireKeyRelease = true;
        inputBlockedUntil = Time.unscaledTime + 0.5f;
        ClearWheelTorque();
        if (FL == null) return;
        FL.steerAngle = FR.steerAngle = 0f;
        ApplyHandbrake();
    }
}
