using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public class CarController : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Rigidbody rb;

    [Header("Ground Check")]
    [SerializeField] private float groundCheckDistance = 0.85f;
    [SerializeField] private LayerMask groundLayers = ~0;

    [Header("Speed")]
    [SerializeField] private float acceleration = 30f;
    [SerializeField] private float reverseAcceleration = 16f;
    [SerializeField] private float brakingForce = 30f;
    [SerializeField] private float maxForwardSpeed = 24f;
    [SerializeField] private float maxReverseSpeed = 10f;
    [SerializeField] private float linearDrag = 0.4f;
    [SerializeField] private float coastDrag = 0.8f;
    [SerializeField] private float angularDrag = 2f;

    [Header("Steering")]
    [SerializeField] private float minSteerSpeed = 0.5f;
    [SerializeField] private float steerDegreesPerSecond = 140f;
    [SerializeField] private float highSpeedSteerReduction = 0.45f;
    [SerializeField] private float throttleSteerReduction = 0.3f;
    [SerializeField] private float airSteerAssist = 2f;

    [Header("Grounded Grip")]
    [SerializeField] private float baseSideGrip = 5f;
    [SerializeField] private float speedGripLoss = 0.55f;
    [SerializeField] private float throttleGripLoss = 0.25f;
    [SerializeField] private float steerGripReduction = 0.55f;

    [Header("Drift")]
    [SerializeField] private float slipDeadzone = 0.75f;
    [SerializeField] private float slipCorrection = 2.2f;
    [SerializeField] private float slipYawAssist = 1.6f;

    private float throttleInput;
    private float steeringInput;
    private bool isGrounded;

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
        isGrounded = CheckGrounded();

        Vector3 localVelocity = transform.InverseTransformDirection(rb.linearVelocity);

        ApplyDrag();
        ApplyDrive(localVelocity);
        ApplySteering(localVelocity);
        ApplySideGrip(localVelocity);
    }

    private bool CheckGrounded()
    {
        Vector3 rayOrigin = transform.position + Vector3.up * 0.1f;
        return Physics.Raycast(rayOrigin, Vector3.down, groundCheckDistance, groundLayers, QueryTriggerInteraction.Ignore);
    }

    private void ApplyDrag()
    {
        rb.linearDamping = Mathf.Abs(throttleInput) > 0.05f ? linearDrag : linearDrag + coastDrag;
        rb.angularDamping = angularDrag;
    }

    private void ApplyDrive(Vector3 localVelocity)
    {
        float forwardSpeed = localVelocity.z;

        if (Mathf.Abs(throttleInput) < 0.01f)
        {
            return;
        }

        bool brakingAgainstMotion = Mathf.Abs(forwardSpeed) > 0.5f && Mathf.Sign(forwardSpeed) != Mathf.Sign(throttleInput);
        if (brakingAgainstMotion)
        {
            rb.AddForce(-rb.linearVelocity.normalized * brakingForce * Mathf.Abs(throttleInput), ForceMode.Acceleration);
            return;
        }

        float maxSpeed = throttleInput >= 0f ? maxForwardSpeed : maxReverseSpeed;
        float driveAcceleration = throttleInput >= 0f ? acceleration : reverseAcceleration;
        float speedPercent = Mathf.Clamp01(Mathf.Abs(forwardSpeed) / maxSpeed);
        float availableDrive = 1f - speedPercent;

        if (availableDrive <= 0f)
        {
            return;
        }

        rb.AddForce(transform.forward * (throttleInput * driveAcceleration * availableDrive), ForceMode.Acceleration);
    }

    private void ApplySteering(Vector3 localVelocity)
    {
        float forwardSpeed = localVelocity.z;
        float absForwardSpeed = Mathf.Abs(forwardSpeed);

        if (Mathf.Abs(steeringInput) < 0.01f || absForwardSpeed < minSteerSpeed)
        {
            return;
        }

        float speedPercent = Mathf.Clamp01(absForwardSpeed / maxForwardSpeed);
        float speedReduction = Mathf.Lerp(1f, highSpeedSteerReduction, speedPercent);
        float throttleReduction = 1f - (Mathf.Max(0f, throttleInput) * throttleSteerReduction);
        float steerDirection = Mathf.Sign(forwardSpeed);

        if (isGrounded)
        {
            float yawStep = steeringInput *
                steerDirection *
                steerDegreesPerSecond *
                speedReduction *
                throttleReduction *
                Time.fixedDeltaTime;

            Quaternion yawRotation = Quaternion.Euler(0f, yawStep, 0f);
            rb.MoveRotation(rb.rotation * yawRotation);
        }
        else
        {
            rb.AddTorque(Vector3.up * steeringInput * steerDirection * airSteerAssist, ForceMode.Acceleration);
        }
    }

    private void ApplySideGrip(Vector3 localVelocity)
    {
        float forwardSpeed = Mathf.Abs(localVelocity.z);
        float slipSpeed = localVelocity.x;
        float slipAmount = Mathf.Abs(slipSpeed);
        float speedPercent = Mathf.Clamp01(forwardSpeed / maxForwardSpeed);
        float throttleAmount = Mathf.Clamp01(Mathf.Max(0f, throttleInput));
        float steerAmount = Mathf.Abs(steeringInput);

        float grip = baseSideGrip;

        if (isGrounded)
        {
            grip *= 1f - (speedPercent * speedGripLoss);
            grip *= 1f - (throttleAmount * throttleGripLoss);
            grip *= 1f - (steerAmount * steerGripReduction);
        }
        else
        {
            grip = 0.1f;
        }

        grip = Mathf.Max(0.1f, grip);

        float slipBeyondDeadzone = Mathf.Max(0f, slipAmount - slipDeadzone);
        float correctionForce = -slipSpeed * slipBeyondDeadzone * grip * slipCorrection;
        rb.AddForce(transform.right * correctionForce, ForceMode.Acceleration);

        if (isGrounded && steerAmount > 0.01f)
        {
            float yawFromSlip = -slipSpeed * slipYawAssist * (0.25f + speedPercent);
            rb.AddTorque(Vector3.up * yawFromSlip, ForceMode.Acceleration);
        }
    }
}
