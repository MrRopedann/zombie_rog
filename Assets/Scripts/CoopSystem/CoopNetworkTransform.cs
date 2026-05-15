using UnityEngine;

[DisallowMultipleComponent]
public class CoopNetworkTransform : MonoBehaviour
{
    [SerializeField] private float positionLerp = 16f;
    [SerializeField] private float rotationLerp = 18f;
    [SerializeField] private float snapDistance = 8f;
    [SerializeField] private bool useVelocityPrediction = true;
    [SerializeField] private float maxExtrapolationTime = 0.12f;

    private Vector3 targetPosition;
    private Vector3 targetVelocity;
    private Quaternion targetRotation = Quaternion.identity;
    private bool hasTarget;
    private float lastTargetTime;

    public void ConfigureInterpolation(float newPositionLerp, float newRotationLerp, float newSnapDistance, float newMaxExtrapolationTime)
    {
        positionLerp = Mathf.Max(0.1f, newPositionLerp);
        rotationLerp = Mathf.Max(0.1f, newRotationLerp);
        snapDistance = Mathf.Max(0.1f, newSnapDistance);
        maxExtrapolationTime = Mathf.Max(0f, newMaxExtrapolationTime);
    }

    public void SetTarget(Vector3 position, Quaternion rotation, bool snap = false)
    {
        float now = Time.time;
        if (hasTarget)
        {
            float deltaTime = Mathf.Max(0.001f, now - lastTargetTime);
            targetVelocity = (position - targetPosition) / deltaTime;
        }
        else
        {
            targetVelocity = Vector3.zero;
        }

        targetPosition = position;
        targetRotation = rotation;
        hasTarget = true;
        lastTargetTime = now;

        if (snap || Vector3.Distance(transform.position, targetPosition) > snapDistance)
        {
            transform.SetPositionAndRotation(targetPosition, targetRotation);
            targetVelocity = Vector3.zero;
        }
    }

    private void Update()
    {
        if (!hasTarget)
            return;

        float positionT = 1f - Mathf.Exp(-positionLerp * Time.deltaTime);
        float rotationT = 1f - Mathf.Exp(-rotationLerp * Time.deltaTime);
        Vector3 predictedTarget = targetPosition;

        if (useVelocityPrediction && targetVelocity.sqrMagnitude > 0.0001f)
        {
            float extrapolationTime = Mathf.Clamp(Time.time - lastTargetTime, 0f, maxExtrapolationTime);
            predictedTarget += targetVelocity * extrapolationTime;
        }

        transform.position = Vector3.Lerp(transform.position, predictedTarget, positionT);
        transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, rotationT);
    }
}
