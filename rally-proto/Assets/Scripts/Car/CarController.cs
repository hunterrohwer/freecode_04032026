using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public class CarController : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Rigidbody rb;

    [Header("Speed")]
    [SerializeField] private float maxForwardSpeed = 24f;
    [SerializeField] private float maxReverseSpeed = 9f;
    [SerializeField] private float acceleration = 28f;
    [SerializeField] private float reverseAcceleration = 14f;
    [SerializeField] private float brakingForce = 30f;
    [SerializeField] private float accelerationFalloff = 1.35f;

    [Header("Steering")]
    [SerializeField] private float groundedYawTorque = 22f;
    [SerializeField] private float minSteerSpeed = 0.75f;
    [SerializeField] private float fullSteerSpeed = 12f;
    [SerializeField] private float highSpeedSteerReduction = 0.4f;
    [SerializeField] private float throttleSteerReduction = 0.3f;
    [SerializeField] private float steerYawDamping = 0.85f;

    [Header("Side Grip")]
    [SerializeField] private float baseSideGrip = 5.2f;
    [SerializeField] private float speedGripLoss = 0.65f;
    [SerializeField] private float throttleGripLoss = 0.35f;
    [SerializeField] private float steerGripReduction = 0.55f;
    [SerializeField] private float slipGripLoss = 0.35f;
    [SerializeField] private float slipYawAssist = 2.8f;
    [SerializeField] private float slipDeadzone = 0.6f;

    [Header("Weight Transfer")]
    [SerializeField] private float throttleFrontBiteLoss = 0.35f;
    [SerializeField] private float liftRotationGain = 0.3f;
    [SerializeField] private float brakeRotationGain = 0.4f;

    [Header("Drag")]
    [SerializeField] private float linearDrag = 0.45f;
    [SerializeField] private float angularDrag = 2.2f;
    [SerializeField] private float coastDrag = 0.75f;

    private float throttleInput;
    private float steeringInput;

    private void Reset()
    {
        rb = GetComponent<Rigidbody>();
    }

    private void Awake()
    {
        if (rb == null)
        {
            rb = GetComponent<Rigidbody>();
        }

        rb.centerOfMass = new Vector3(0f, -0.4f, 0f);
        rb.interpolation = RigidbodyInterpolation.Interpolate;
    }

    private void Update()
    {
        throttleInput = Input.GetAxis("Vertical");
        steeringInput = Input.GetAxis("Horizontal");
    }

    private void FixedUpdate()
    {
        Vector3 localVelocity = transform.InverseTransformDirection(rb.linearVelocity);

        ApplyDrag();
        ApplyLongitudinalForce(localVelocity);
        ApplySteering(localVelocity);
        ApplyLateralGrip(localVelocity);
    }

    private void ApplyDrag()
    {
        rb.linearDamping = Mathf.Abs(throttleInput) > 0.05f ? linearDrag : linearDrag + coastDrag;
        rb.angularDamping = angularDrag;
    }

    private void ApplyLongitudinalForce(Vector3 localVelocity)
    {
        float forwardSpeed = localVelocity.z;
        float inputDirection = Mathf.Sign(throttleInput);
        bool hasThrottle = Mathf.Abs(throttleInput) > 0.01f;

        if (!hasThrottle)
        {
            return;
        }

        bool isBrakingAgainstMotion = Mathf.Abs(forwardSpeed) > 0.5f && Mathf.Sign(forwardSpeed) != inputDirection;
        if (isBrakingAgainstMotion)
        {
            rb.AddForce(-rb.linearVelocity.normalized * brakingForce * Mathf.Abs(throttleInput), ForceMode.Acceleration);
            return;
        }

        float maxSpeed = throttleInput >= 0f ? maxForwardSpeed : maxReverseSpeed;
        float accelForce = throttleInput >= 0f ? acceleration : reverseAcceleration;
        float speedPercent = Mathf.Clamp01(Mathf.Abs(forwardSpeed) / maxSpeed);
        float availableAcceleration = 1f - Mathf.Pow(speedPercent, accelerationFalloff);

        if (availableAcceleration <= 0f)
        {
            return;
        }

        float driveForce = throttleInput * accelForce * availableAcceleration;
        rb.AddForce(transform.forward * driveForce, ForceMode.Acceleration);
    }

    private void ApplySteering(Vector3 localVelocity)
    {
        float forwardSpeed = localVelocity.z;
        float absForwardSpeed = Mathf.Abs(forwardSpeed);
        if (Mathf.Abs(steeringInput) < 0.01f || absForwardSpeed < minSteerSpeed)
        {
            return;
        }

        float steerDirection = Mathf.Sign(forwardSpeed);
        float speedAuthority = Mathf.InverseLerp(minSteerSpeed, fullSteerSpeed, absForwardSpeed);
        float highSpeedReduction = Mathf.Lerp(1f, highSpeedSteerReduction, Mathf.Clamp01(absForwardSpeed / maxForwardSpeed));

        // Under throttle the front washes out more; lifting or braking helps the car rotate.
        float frontBite = 1f;
        if (throttleInput > 0f)
        {
            frontBite -= throttleInput * throttleFrontBiteLoss;
            frontBite -= throttleInput * throttleSteerReduction * Mathf.Clamp01(absForwardSpeed / maxForwardSpeed);
        }
        else if (throttleInput < 0f)
        {
            frontBite += Mathf.Abs(throttleInput) * brakeRotationGain;
        }
        else
        {
            frontBite += liftRotationGain;
        }

        frontBite = Mathf.Max(0.15f, frontBite);

        float steerTorqueAmount =
            steeringInput *
            steerDirection *
            groundedYawTorque *
            speedAuthority *
            highSpeedReduction *
            frontBite;

        rb.AddTorque(Vector3.up * steerTorqueAmount, ForceMode.Acceleration);

        // Light yaw damping keeps rotation readable without making the car feel locked.
        Vector3 localAngularVelocity = transform.InverseTransformDirection(rb.angularVelocity);
        float yawDamping = Mathf.Clamp01(steerYawDamping * Time.fixedDeltaTime);
        localAngularVelocity.y = Mathf.Lerp(localAngularVelocity.y, localAngularVelocity.y * 0.92f, yawDamping);
        rb.angularVelocity = transform.TransformDirection(localAngularVelocity);
    }

    private void ApplyLateralGrip(Vector3 localVelocity)
    {
        float forwardSpeed = Mathf.Abs(localVelocity.z);
        float slipSpeed = localVelocity.x;
        float slipAmount = Mathf.Abs(slipSpeed);
        float speedPercent = Mathf.Clamp01(forwardSpeed / maxForwardSpeed);
        float throttleAmount = Mathf.Clamp01(Mathf.Max(0f, throttleInput));
        float steerAmount = Mathf.Abs(steeringInput);

        // Grip falls away hard with speed and throttle so momentum carries the car wider on dirt.
        float grip = baseSideGrip;
        grip *= 1f - (speedPercent * speedGripLoss);
        grip *= 1f - (throttleAmount * throttleGripLoss);
        grip *= 1f - (steerAmount * steerGripReduction);

        float slipBeyondDeadzone = Mathf.Max(0f, slipAmount - slipDeadzone);
        grip /= 1f + (slipBeyondDeadzone * slipGripLoss);
        grip = Mathf.Max(0.15f, grip);

        float lateralCorrection = -slipSpeed * grip;
        rb.AddForce(transform.right * lateralCorrection, ForceMode.Acceleration);

        // Slip still helps the rear come around so the car stays dangerous instead of pure understeer.
        float yawFromSlip = -slipSpeed * slipYawAssist * (0.35f + speedPercent);
        rb.AddTorque(Vector3.up * yawFromSlip, ForceMode.Acceleration);
    }
}
