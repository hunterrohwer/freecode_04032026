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
    [SerializeField] private float yawTorque = 10f;
    [SerializeField] private float steerResponse = 2.2f;
    [SerializeField] private float minSteerSpeed = 1.5f;
    [SerializeField] private float fullSteerSpeed = 12f;
    [SerializeField] private float highSpeedSteerReduction = 0.45f;

    [Header("Grip")]
    [SerializeField] private float baseSideGrip = 8f;
    [SerializeField] private float speedGripLoss = 0.35f;
    [SerializeField] private float slipGripLoss = 0.9f;
    [SerializeField] private float slipYawAssist = 1.8f;

    [Header("Weight Transfer")]
    [SerializeField] private float throttleFrontBiteLoss = 0.18f;
    [SerializeField] private float liftRotationGain = 0.22f;
    [SerializeField] private float brakeRotationGain = 0.3f;

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
        if (absForwardSpeed < minSteerSpeed || Mathf.Abs(steeringInput) < 0.01f)
        {
            return;
        }

        // Steering authority ramps in with speed, then softens again at higher speed.
        float steerAuthority = Mathf.InverseLerp(minSteerSpeed, fullSteerSpeed, absForwardSpeed);
        float highSpeedSoftening = Mathf.Lerp(1f, highSpeedSteerReduction, Mathf.Clamp01(absForwardSpeed / maxForwardSpeed));

        // Throttle loads the rear and makes the front push a bit; lift/brake helps the car rotate.
        float frontBite = 1f;
        if (throttleInput > 0f)
        {
            frontBite -= throttleInput * throttleFrontBiteLoss;
        }
        else if (throttleInput < 0f)
        {
            frontBite += Mathf.Abs(throttleInput) * brakeRotationGain;
        }
        else
        {
            frontBite += 0.1f;
        }

        if (Mathf.Abs(throttleInput) < 0.05f && absForwardSpeed > minSteerSpeed)
        {
            frontBite += liftRotationGain;
        }

        float steerDirection = Mathf.Sign(forwardSpeed);
        float steerTorqueAmount =
            steeringInput *
            steerDirection *
            yawTorque *
            steerAuthority *
            highSpeedSoftening *
            Mathf.Max(0f, frontBite);

        rb.AddTorque(Vector3.up * steerTorqueAmount, ForceMode.Acceleration);

        // A touch of yaw damping keeps the rotation readable rather than twitchy.
        Vector3 localAngularVelocity = transform.InverseTransformDirection(rb.angularVelocity);
        localAngularVelocity.y = Mathf.Lerp(localAngularVelocity.y, localAngularVelocity.y * 0.94f, steerResponse * Time.fixedDeltaTime);
        rb.angularVelocity = transform.TransformDirection(localAngularVelocity);
    }

    private void ApplyLateralGrip(Vector3 localVelocity)
    {
        float forwardSpeed = Mathf.Abs(localVelocity.z);
        float slipSpeed = localVelocity.x;
        float slipAmount = Mathf.Abs(slipSpeed);
        float speedPercent = Mathf.Clamp01(forwardSpeed / maxForwardSpeed);

        // Grip fades with speed and slip so the car can move around instead of snapping straight.
        float grip = baseSideGrip;
        grip *= 1f - (speedPercent * speedGripLoss);
        grip /= 1f + (slipAmount * slipGripLoss);
        grip = Mathf.Max(0f, grip);

        float lateralCorrection = -slipSpeed * grip;
        rb.AddForce(transform.right * lateralCorrection, ForceMode.Acceleration);

        // Slip adds a little self-rotation so fast corner entries feel looser.
        float yawFromSlip = -slipSpeed * slipYawAssist * speedPercent;
        rb.AddTorque(Vector3.up * yawFromSlip, ForceMode.Acceleration);
    }
}
