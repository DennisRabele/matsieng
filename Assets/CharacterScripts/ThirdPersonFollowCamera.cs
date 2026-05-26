using UnityEngine;

public class ThirdPersonFollowCamera : MonoBehaviour
{
    [Header("Target")]
    [SerializeField] private Transform target;

    [Header("Follow")]
    [SerializeField] private float followDistance = 18f;
    [SerializeField] private float followHeight = 8f;
    [SerializeField] private float sideOffset = 0f;
    [SerializeField] private float lookHeightOffset = 2f;
    [SerializeField] private bool scaleWithTargetBounds = true;
    [Tooltip("If enabled, this camera keeps its current offset from the target and moves along with it.")]
    [SerializeField] private bool keepCurrentOffsetFromTarget = false;
    [SerializeField] private bool lookAtTarget = true;

    [Header("Smoothing")]
    [Min(0f)]
    [SerializeField] private float positionSharpness = 8f;
    [Min(0f)]
    [SerializeField] private float rotationSharpness = 12f;

    private Renderer[] targetRenderers;
    private Vector3 worldOffsetFromTarget;
    private bool hasWorldOffsetFromTarget;

    public void SetTarget(Transform newTarget)
    {
        SetTarget(newTarget, false);
    }

    public void SetTarget(Transform newTarget, bool keepCurrentOffset)
    {
        target = newTarget;
        targetRenderers = target != null ? target.GetComponentsInChildren<Renderer>() : null;
        keepCurrentOffsetFromTarget = keepCurrentOffset;
        hasWorldOffsetFromTarget = false;

        if (target != null && keepCurrentOffsetFromTarget)
            CacheWorldOffsetFromTarget(GetTrackedLookTarget());

        SnapToTarget();
    }

    private void OnEnable()
    {
        SnapToTarget();
    }

    private void LateUpdate()
    {
        if (target == null)
            return;

        Vector3 lookTarget = GetTrackedLookTarget();

        Vector3 desiredPosition = keepCurrentOffsetFromTarget
            ? GetOffsetFollowPosition(lookTarget)
            : GetThirdPersonFollowPosition(lookTarget);

        float positionT = 1f - Mathf.Exp(-positionSharpness * Time.deltaTime);
        transform.position = Vector3.Lerp(transform.position, desiredPosition, positionT);

        if (!lookAtTarget)
            return;

        Vector3 lookDirection = lookTarget - transform.position;
        if (lookDirection.sqrMagnitude < 0.0001f)
            return;

        Quaternion desiredRotation = Quaternion.LookRotation(lookDirection.normalized, Vector3.up);
        float rotationT = 1f - Mathf.Exp(-rotationSharpness * Time.deltaTime);
        transform.rotation = Quaternion.Slerp(transform.rotation, desiredRotation, rotationT);
    }

    private void SnapToTarget()
    {
        if (target == null)
            return;

        Vector3 lookTarget = GetTrackedLookTarget();

        transform.position = keepCurrentOffsetFromTarget
            ? GetOffsetFollowPosition(lookTarget)
            : GetThirdPersonFollowPosition(lookTarget);

        if (!lookAtTarget)
            return;

        Vector3 lookDirection = lookTarget - transform.position;
        if (lookDirection.sqrMagnitude > 0.0001f)
            transform.rotation = Quaternion.LookRotation(lookDirection.normalized, Vector3.up);
    }

    private Vector3 GetThirdPersonFollowPosition(Vector3 lookTarget)
    {
        Vector3 followDirection = Vector3.ProjectOnPlane(target.forward, Vector3.up);
        if (followDirection.sqrMagnitude < 0.0001f)
            followDirection = Vector3.ProjectOnPlane(transform.forward, Vector3.up);
        if (followDirection.sqrMagnitude < 0.0001f)
            followDirection = Vector3.forward;

        followDirection.Normalize();

        float distance = followDistance;
        float height = followHeight;
        if (scaleWithTargetBounds && TryGetTargetBounds(out Bounds bounds))
        {
            distance = Mathf.Max(distance, bounds.size.y * 2.2f);
            height = Mathf.Max(height, bounds.size.y * 0.7f);
        }

        return lookTarget - followDirection * distance + Vector3.up * height + target.right * sideOffset;
    }

    private Vector3 GetOffsetFollowPosition(Vector3 lookTarget)
    {
        if (!hasWorldOffsetFromTarget)
            CacheWorldOffsetFromTarget(lookTarget);

        return lookTarget + worldOffsetFromTarget;
    }

    private void CacheWorldOffsetFromTarget(Vector3 lookTarget)
    {
        worldOffsetFromTarget = transform.position - lookTarget;
        hasWorldOffsetFromTarget = true;
    }

    private Vector3 GetLookTarget()
    {
        return target.position + Vector3.up * lookHeightOffset;
    }

    private Vector3 GetTrackedLookTarget()
    {
        Vector3 lookTarget = GetLookTarget();
        if (scaleWithTargetBounds && TryGetTargetBounds(out Bounds bounds))
            lookTarget = bounds.center + Vector3.up * lookHeightOffset;

        return lookTarget;
    }

    private bool TryGetTargetBounds(out Bounds bounds)
    {
        if (targetRenderers == null || targetRenderers.Length == 0)
            targetRenderers = target.GetComponentsInChildren<Renderer>();

        bounds = default;
        bool hasBounds = false;

        foreach (Renderer renderer in targetRenderers)
        {
            if (renderer == null || !renderer.enabled)
                continue;

            if (!hasBounds)
            {
                bounds = renderer.bounds;
                hasBounds = true;
                continue;
            }

            bounds.Encapsulate(renderer.bounds);
        }

        return hasBounds;
    }
}
