using UnityEngine;
using System.Collections;

public class CharacterScrpit : MonoBehaviour
{
    [Header("Waypoints (in order)")]
    [Tooltip("Object moves from element 0 -> 1 -> 2 ...")]
    [SerializeField] private Transform[] waypoints;

    [Header("Timing")]
    [Min(0.01f)]
    [Tooltip("If Segment Durations is empty, time is split evenly across segments.")]
    [SerializeField] private float totalDurationSeconds = 3f;

    [Tooltip("Optional per-segment durations (seconds). Length must be waypoints.Length - 1.")]
    [SerializeField] private float[] segmentDurationsSeconds;

    [Header("Rotation")]
    [SerializeField] private bool rotateAtWaypoints = true;
    [Tooltip("If true, only rotates around Y (keeps character upright).")]
    [SerializeField] private bool rotateOnlyOnY = true;
    [Min(0f)]
    [Tooltip("Seconds to rotate in-place at each waypoint before moving to the next. 0 = snap instantly.")]
    [SerializeField] private float rotateDurationSeconds = 0f;
    [SerializeField] private AnimationCurve rotationEase = AnimationCurve.Linear(0f, 0f, 1f, 1f);

    [Header("Camera Pan (Optional)")]
    [Tooltip("If this component is on a Camera, pan/rotate in place at each waypoint before moving to the next.")]
    [SerializeField] private bool panAtWaypointsIfCamera = true;
    [Min(0f)]
    [Tooltip("Seconds to pan/rotate at each waypoint (Camera only).")]
    [SerializeField] private float cameraPanSeconds = 5f;
    [Tooltip("Total yaw degrees to rotate during the pan (Camera only). 360 = full spin.")]
    [SerializeField] private float cameraPanYawDegrees = 360f;
    [Tooltip("If true, also pans at the final waypoint after arriving (Camera only).")]
    [SerializeField] private bool panAtFinalWaypointIfCamera = true;

    [Header("Waiting / Message")]
    [Tooltip("If enabled, the object will pause at waypoints before turning/moving to the next waypoint.")]
    [SerializeField] private bool waitAtWaypoints = false;
    [Min(0f)]
    [Tooltip("Seconds to wait at each intermediate waypoint (PointA/PointB etc). Not applied at the first waypoint.")]
    [SerializeField] private float waitSecondsAtIntermediateWaypoints = 0f;
    [Min(0f)]
    [Tooltip("Seconds to wait at the final waypoint before finishing.")]
    [SerializeField] private float waitSecondsAtFinalWaypoint = 0f;
    [TextArea(2, 6)]
    [SerializeField] private string waitMessage = "";
    [Tooltip("Higher priority messages override lower priority ones while both are active. (Cameras get +10 automatically.)")]
    [SerializeField] private int messagePriority = 0;
    [Tooltip("If enabled, the message is also shown while the character is moving between waypoints.")]
    [SerializeField] private bool showMessageWhileMoving = false;

    [Header("Animation (Optional)")]
    [Tooltip("If assigned, this script will set a bool parameter while moving.")]
    [SerializeField] private Animator animator;
    [SerializeField] private string movingBoolParameter = "IsMoving";

    [Header("Playback")]
    [SerializeField] private bool playOnStart = true;
    [SerializeField] private bool snapToFirstWaypointOnStart = true;
    [Tooltip("If true, disables this component after reaching the last waypoint.")]
    [SerializeField] private bool disableComponentOnComplete = false;

    [Header("Easing")]
    [SerializeField] private AnimationCurve positionEase = AnimationCurve.Linear(0f, 0f, 1f, 1f);

    private Coroutine moveRoutine;
    private TourMessageUI messageUi;
    private uint lastMessageSequence;
    private bool hasCameraComponent;

    private void Awake()
    {
        hasCameraComponent = GetComponent<Camera>() != null;
    }

    private void Start()
    {
        if (!playOnStart)
            return;

        Play();
    }

    public void Play()
    {
        if (moveRoutine != null)
        {
            StopCoroutine(moveRoutine);
            ClearMessage();
        }

        moveRoutine = StartCoroutine(MoveAlongWaypoints());
    }

    public void Stop()
    {
        if (moveRoutine == null)
            return;

        StopCoroutine(moveRoutine);
        moveRoutine = null;
        ClearMessage();
    }

    private IEnumerator MoveAlongWaypoints()
    {
        if (waypoints == null || waypoints.Length < 2)
        {
            Debug.LogWarning($"{nameof(CharacterScrpit)} on '{name}' needs at least 2 waypoints.", this);
            yield break;
        }

        bool isCamera = hasCameraComponent;

        for (int i = 0; i < waypoints.Length; i++)
        {
            if (waypoints[i] == null)
            {
                Debug.LogWarning($"{nameof(CharacterScrpit)} on '{name}' has a missing waypoint at index {i}.", this);
                yield break;
            }
        }

        if (snapToFirstWaypointOnStart)
            transform.position = waypoints[0].position;

        int segmentCount = waypoints.Length - 1;
        bool hasPerSegment = segmentDurationsSeconds != null && segmentDurationsSeconds.Length == segmentCount;

        float evenDuration = totalDurationSeconds / segmentCount;
        if (!hasPerSegment && totalDurationSeconds <= 0f)
        {
            Debug.LogWarning($"{nameof(CharacterScrpit)} on '{name}' has invalid total duration.", this);
            yield break;
        }

        for (int segmentIndex = 0; segmentIndex < segmentCount; segmentIndex++)
        {
            Transform from = waypoints[segmentIndex];
            Transform to = waypoints[segmentIndex + 1];

            if (waitAtWaypoints && segmentIndex > 0 && waitSecondsAtIntermediateWaypoints > 0f)
            {
                yield return WaitWithMessage(waitSecondsAtIntermediateWaypoints);
            }

            if (isCamera && panAtWaypointsIfCamera && cameraPanSeconds > 0f)
            {
                float panSeconds = Mathf.Max(5f, cameraPanSeconds);
                if (showMessageWhileMoving)
                    ShowMessageForSeconds(waitMessage, panSeconds);

                yield return PanYaw(cameraPanYawDegrees, panSeconds);
            }
            else if (rotateAtWaypoints)
                yield return RotateToward(to.position);

            float segmentDuration = hasPerSegment ? Mathf.Max(0.01f, segmentDurationsSeconds[segmentIndex]) : Mathf.Max(0.01f, evenDuration);

            SetMovingAnimation(true);
            if (showMessageWhileMoving)
                ShowMessageForSeconds(waitMessage, segmentDuration);

            float elapsed = 0f;

            Vector3 startPos = from.position;
            Vector3 endPos = to.position;

            while (elapsed < segmentDuration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / segmentDuration);
                float eased = positionEase != null ? positionEase.Evaluate(t) : t;
                transform.position = Vector3.LerpUnclamped(startPos, endPos, eased);
                yield return null;
            }

            transform.position = endPos;
            SetMovingAnimation(false);
        }

        if (waitAtWaypoints && waitSecondsAtFinalWaypoint > 0f)
        {
            yield return WaitWithMessage(waitSecondsAtFinalWaypoint);
        }

        if (isCamera && panAtFinalWaypointIfCamera && cameraPanSeconds > 0f)
        {
            float panSeconds = Mathf.Max(5f, cameraPanSeconds);
            if (showMessageWhileMoving)
                ShowMessageForSeconds(waitMessage, panSeconds);

            yield return PanYaw(cameraPanYawDegrees, panSeconds);
        }

        moveRoutine = null;

        if (disableComponentOnComplete)
            enabled = false;
    }

    private IEnumerator PanYaw(float yawDegrees, float seconds)
    {
        if (seconds <= 0f)
            yield break;

        Quaternion startRotation = transform.rotation;
        float elapsed = 0f;
        while (elapsed < seconds)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / seconds);
            float eased = rotationEase != null ? rotationEase.Evaluate(t) : t;
            float yaw = yawDegrees * eased;
            transform.rotation = Quaternion.AngleAxis(yaw, Vector3.up) * startRotation;
            yield return null;
        }

        transform.rotation = Quaternion.AngleAxis(yawDegrees, Vector3.up) * startRotation;
    }

    private IEnumerator RotateToward(Vector3 worldTargetPosition)
    {
        Vector3 direction = worldTargetPosition - transform.position;
        if (rotateOnlyOnY)
            direction.y = 0f;

        if (direction.sqrMagnitude < 0.0001f)
            yield break;

        Quaternion startRotation = transform.rotation;
        Quaternion targetRotation = Quaternion.LookRotation(direction.normalized, Vector3.up);

        if (rotateDurationSeconds <= 0f)
        {
            transform.rotation = targetRotation;
            yield break;
        }

        float elapsed = 0f;
        while (elapsed < rotateDurationSeconds)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / rotateDurationSeconds);
            float eased = rotationEase != null ? rotationEase.Evaluate(t) : t;
            transform.rotation = Quaternion.SlerpUnclamped(startRotation, targetRotation, eased);
            yield return null;
        }

        transform.rotation = targetRotation;
    }

    private void SetMovingAnimation(bool isMoving)
    {
        if (animator == null || string.IsNullOrWhiteSpace(movingBoolParameter))
            return;

        animator.SetBool(movingBoolParameter, isMoving);
    }

    private IEnumerator WaitWithMessage(float seconds)
    {
        if (seconds <= 0f)
            yield break;

        SetMovingAnimation(false);
        ShowMessageForSeconds(waitMessage, seconds);
        yield return new WaitForSeconds(seconds);
        ClearMessage();
    }

    private void ShowMessageForSeconds(string message, float seconds)
    {
        if (string.IsNullOrWhiteSpace(message) || seconds <= 0f)
            return;

        if (messageUi == null)
            messageUi = TourMessageUI.GetOrCreate();

        int effectivePriority = messagePriority + (hasCameraComponent ? 10 : 0);
        uint messageSequence = messageUi.Show(message, seconds, effectivePriority);
        if (messageSequence != 0)
            lastMessageSequence = messageSequence;
    }

    private void ClearMessage()
    {
        if (messageUi == null || lastMessageSequence == 0)
            return;

        messageUi.Clear(lastMessageSequence);
        lastMessageSequence = 0;
    }
}
