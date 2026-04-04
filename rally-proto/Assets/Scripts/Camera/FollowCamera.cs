using UnityEngine;

public class FollowCamera : MonoBehaviour
{
    [Header("Target")]
    [SerializeField] private Transform target;

    [Header("Position")]
    [SerializeField] private Vector3 offset = new Vector3(0f, 4.5f, -7f);
    [SerializeField] private float followSpeed = 6f;

    [Header("Look")]
    [SerializeField] private Vector3 lookOffset = new Vector3(0f, 1.2f, 0f);
    [SerializeField] private float lookSpeed = 8f;

    private void LateUpdate()
    {
        if (target == null)
        {
            return;
        }

        Vector3 desiredPosition = target.TransformPoint(offset);
        transform.position = Vector3.Lerp(
            transform.position,
            desiredPosition,
            followSpeed * Time.deltaTime);

        Vector3 lookPoint = target.position + lookOffset;
        Quaternion desiredRotation = Quaternion.LookRotation(lookPoint - transform.position);
        transform.rotation = Quaternion.Lerp(
            transform.rotation,
            desiredRotation,
            lookSpeed * Time.deltaTime);
    }
}
