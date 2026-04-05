using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public class CarController : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Rigidbody rb;

    [Header("Ground Check")]
    [SerializeField] private float groundCheckDistance = 0.85f;
    [SerializeField] private LayerMask groundLayers = ~0;
    [SerializeField] private Vector3 groundProbeExtents = new Vector3(0.9f, 0f, 1.2f);

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

    [Header("Bump Reaction")]
    [SerializeField] private float bumpPitchStrength = 10f;
    [SerializeField] private float bumpRollStrength = 14f;
    [SerializeField] private float bumpYawStrength = 3f;
    [SerializeField] private float roughGroundVelocityKick = 1.2f;

    [Header("Landing Damping")]
    [SerializeField] private float landingAngularDamping = 5f;
    [SerializeField] private float landingVelocityDamping = 2f;
    [SerializeField] private float landingSettleTime = 0.2f;

    private float throttleInput;
    private float steeringInput;
    private bool isGrounded;
    private bool wasGrounded;
    private float lastLandingTime = -1f;

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
        wasGrounded = isGrounded;
        isGrounded = CheckGrounded();

        if (!wasGrounded && isGrounded)
        {
            lastLandingTime = Time.time;
        }

        Vector3 localVelocity = transform.InverseTransformDirection(rb.linearVelocity);

        ApplyDrag();
        ApplyDrive(localVelocity);
        ApplySteering(localVelocity);
        ApplySideGrip(localVelocity);
        ApplyBumpReaction(localVelocity);
        ApplyLandingDamping();
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

    private void ApplyBumpReaction(Vector3 localVelocity)
    {
        if (!isGrounded)
        {
            return;
        }

        bool frontLeftHit = SampleGroundHeight(new Vector3(-groundProbeExtents.x, 0f, groundProbeExtents.z), out float frontLeftHeight);
        bool frontRightHit = SampleGroundHeight(new Vector3(groundProbeExtents.x, 0f, groundProbeExtents.z), out float frontRightHeight);
        bool rearLeftHit = SampleGroundHeight(new Vector3(-groundProbeExtents.x, 0f, -groundProbeExtents.z), out float rearLeftHeight);
        bool rearRightHit = SampleGroundHeight(new Vector3(groundProbeExtents.x, 0f, -groundProbeExtents.z), out float rearRightHeight);

        if (!(frontLeftHit && frontRightHit && rearLeftHit && rearRightHit))
        {
            return;
        }

        float leftAverage = (frontLeftHeight + rearLeftHeight) * 0.5f;
        float rightAverage = (frontRightHeight + rearRightHeight) * 0.5f;
        float frontAverage = (frontLeftHeight + frontRightHeight) * 0.5f;
        float rearAverage = (rearLeftHeight + rearRightHeight) * 0.5f;

        float sideDifference = leftAverage - rightAverage;
        float foreAftDifference = frontAverage - rearAverage;
        float speedPercent = Mathf.Clamp01(Mathf.Abs(localVelocity.z) / maxForwardSpeed);

        // Uneven ground gives the body a small shove in pitch/roll/yaw without changing the core steering model.
        Vector3 bumpTorque = new Vector3(
            foreAftDifference * bumpPitchStrength,
            -sideDifference * bumpYawStrength * (0.35f + speedPercent),
            sideDifference * bumpRollStrength);

        rb.AddRelativeTorque(bumpTorque, ForceMode.Acceleration);

        float sideKick = -sideDifference * roughGroundVelocityKick * (0.25f + speedPercent);
        rb.AddForce(transform.right * sideKick, ForceMode.Acceleration);
    }

    private void ApplyLandingDamping()
    {
        if (!isGrounded || lastLandingTime < 0f)
        {
            return;
        }

        float timeSinceLanding = Time.time - lastLandingTime;
        if (timeSinceLanding > landingSettleTime)
        {
            return;
        }

        float settleFactor = 1f - Mathf.Clamp01(timeSinceLanding / landingSettleTime);
        rb.AddTorque(-rb.angularVelocity * landingAngularDamping * settleFactor, ForceMode.Acceleration);

        Vector3 localVelocity = transform.InverseTransformDirection(rb.linearVelocity);
        Vector3 landingCorrection = new Vector3(localVelocity.x, 0f, 0f);
        rb.AddForce(-transform.TransformDirection(landingCorrection) * landingVelocityDamping * settleFactor, ForceMode.Acceleration);
    }

    private bool SampleGroundHeight(Vector3 localOffset, out float height)
    {
        Vector3 rayOrigin = transform.TransformPoint(localOffset + Vector3.up * 1.5f);
        if (Physics.Raycast(rayOrigin, Vector3.down, out RaycastHit hit, groundCheckDistance + 2f, groundLayers, QueryTriggerInteraction.Ignore))
        {
            height = rayOrigin.y - hit.point.y;
            return true;
        }

        height = 0f;
        return false;
    }
}
