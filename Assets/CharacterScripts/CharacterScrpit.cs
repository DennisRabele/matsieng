using UnityEngine;
using System.Collections;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

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

    [Header("Keyboard Movement")]
    [Tooltip("Use WASD/arrow keys to move this character freely instead of following waypoints.")]
    [SerializeField] private bool useKeyboardInput = false;
    [Min(0f)]
    [SerializeField] private float keyboardMoveSpeed = 45f;
    [Min(0f)]
    [SerializeField] private float keyboardTurnSpeedDegrees = 540f;
    [Tooltip("If enabled, movement follows the active camera's forward/right directions.")]
    [SerializeField] private bool keyboardMovementRelativeToCamera = true;
    [Tooltip("Keeps the character standing on active Unity Terrains while roaming.")]
    [SerializeField] private bool keepKeyboardMovementOnTerrain = true;
    [Tooltip("Small clearance between the character's lowest visible point and the terrain.")]
    [SerializeField] private float keyboardGroundOffset = 0.05f;

    [Header("Keyboard Collision")]
    [Tooltip("Adds/uses a CharacterController so keyboard movement is stopped by solid colliders.")]
    [SerializeField] private bool useKeyboardCollision = true;
    [Range(0.1f, 1f)]
    [SerializeField] private float keyboardCollisionRadiusScale = 0.35f;
    [Min(0f)]
    [SerializeField] private float keyboardCollisionHeightPadding = 0.1f;

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
    private Renderer[] groundSnapRenderers;
    private CharacterController characterController;

    private void Awake()
    {
        hasCameraComponent = GetComponent<Camera>() != null;
        groundSnapRenderers = GetComponentsInChildren<Renderer>();
        characterController = GetComponent<CharacterController>();
    }

    private void Start()
    {
        if (useKeyboardInput)
        {
            EnsureKeyboardCharacterController();
            SnapToFirstWaypointIfConfigured();
            SnapToTerrainIfNeeded();
            SetMovingAnimation(false);
            return;
        }

        if (!playOnStart)
            return;

        Play();
    }

    private void Update()
    {
        if (!useKeyboardInput)
            return;

        MoveFromKeyboard();
    }

    public void Play()
    {
        if (useKeyboardInput)
        {
            Stop();
            return;
        }

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
            SnapToFirstWaypointIfConfigured();

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

    private void MoveFromKeyboard()
    {
        Vector2 input = ReadKeyboardMovementInput();
        input = Vector2.ClampMagnitude(input, 1f);

        bool isMoving = input.sqrMagnitude > 0.0001f;
        SetMovingAnimation(isMoving);

        if (!isMoving)
            return;

        Vector3 moveDirection = GetWorldMoveDirection(input);
        if (moveDirection.sqrMagnitude < 0.0001f)
            return;

        moveDirection.Normalize();
        MoveKeyboardCharacter(moveDirection * keyboardMoveSpeed * Time.deltaTime);
        SnapToTerrainIfNeeded();

        if (keyboardTurnSpeedDegrees <= 0f)
        {
            transform.rotation = Quaternion.LookRotation(moveDirection, Vector3.up);
            return;
        }

        Quaternion targetRotation = Quaternion.LookRotation(moveDirection, Vector3.up);
        transform.rotation = Quaternion.RotateTowards(
            transform.rotation,
            targetRotation,
            keyboardTurnSpeedDegrees * Time.deltaTime);
    }

    private Vector2 ReadKeyboardMovementInput()
    {
        Vector2 input = Vector2.zero;

#if ENABLE_INPUT_SYSTEM
        Keyboard keyboard = Keyboard.current;
        if (keyboard != null)
        {
            if (keyboard.aKey.isPressed || keyboard.leftArrowKey.isPressed)
                input.x -= 1f;
            if (keyboard.dKey.isPressed || keyboard.rightArrowKey.isPressed)
                input.x += 1f;
            if (keyboard.sKey.isPressed || keyboard.downArrowKey.isPressed)
                input.y -= 1f;
            if (keyboard.wKey.isPressed || keyboard.upArrowKey.isPressed)
                input.y += 1f;
        }
#endif

#if ENABLE_LEGACY_INPUT_MANAGER
        input.x += Input.GetAxisRaw("Horizontal");
        input.y += Input.GetAxisRaw("Vertical");
#endif

        return input;
    }

    private Vector3 GetWorldMoveDirection(Vector2 input)
    {
        if (keyboardMovementRelativeToCamera && TryGetActiveCameraTransform(out Transform cameraTransform))
        {
            Vector3 cameraForward = Vector3.ProjectOnPlane(cameraTransform.forward, Vector3.up);
            Vector3 cameraRight = Vector3.ProjectOnPlane(cameraTransform.right, Vector3.up);

            if (cameraForward.sqrMagnitude > 0.0001f && cameraRight.sqrMagnitude > 0.0001f)
                return cameraForward.normalized * input.y + cameraRight.normalized * input.x;
        }

        return new Vector3(input.x, 0f, input.y);
    }

    private void MoveKeyboardCharacter(Vector3 worldDelta)
    {
        if (useKeyboardCollision && characterController == null)
            EnsureKeyboardCharacterController();

        if (useKeyboardCollision && characterController != null && characterController.enabled)
        {
            characterController.Move(worldDelta);
            return;
        }

        transform.position += worldDelta;
    }

    private void EnsureKeyboardCharacterController()
    {
        if (!useKeyboardCollision)
            return;

        if (characterController == null)
            characterController = GetComponent<CharacterController>();

        if (characterController == null)
            characterController = gameObject.AddComponent<CharacterController>();

        if (!TryGetRendererBounds(out Bounds bounds))
            return;

        float height = Mathf.Max(0.1f, bounds.size.y + keyboardCollisionHeightPadding);
        float horizontalExtent = Mathf.Max(bounds.extents.x, bounds.extents.z);
        float radius = Mathf.Clamp(horizontalExtent * keyboardCollisionRadiusScale, 0.1f, height * 0.45f);

        characterController.height = height;
        characterController.radius = radius;
        characterController.center = transform.InverseTransformPoint(bounds.center);
        characterController.skinWidth = Mathf.Max(0.01f, radius * 0.08f);
        characterController.stepOffset = Mathf.Min(height * 0.25f, 0.5f);
    }

    private bool TryGetActiveCameraTransform(out Transform cameraTransform)
    {
        Camera mainCamera = Camera.main;
        if (mainCamera != null && mainCamera.isActiveAndEnabled)
        {
            cameraTransform = mainCamera.transform;
            return true;
        }

        Camera activeCamera = FindFirstObjectByType<Camera>();
        if (activeCamera != null)
        {
            cameraTransform = activeCamera.transform;
            return true;
        }

        cameraTransform = null;
        return false;
    }

    private void SnapToFirstWaypointIfConfigured()
    {
        if (!snapToFirstWaypointOnStart || waypoints == null || waypoints.Length == 0 || waypoints[0] == null)
            return;

        transform.position = waypoints[0].position;
    }

    private void SnapToTerrainIfNeeded()
    {
        if (!keepKeyboardMovementOnTerrain)
            return;

        if (!TryGetTerrainHeight(transform.position, out float terrainY))
            return;

        float targetBottomY = terrainY + keyboardGroundOffset;
        if (TryGetLowestRendererY(out float currentBottomY))
        {
            transform.position += Vector3.up * (targetBottomY - currentBottomY);
            return;
        }

        Vector3 fallbackPosition = transform.position;
        fallbackPosition.y = targetBottomY;
        transform.position = fallbackPosition;
    }

    private bool TryGetLowestRendererY(out float lowestY)
    {
        if (!TryGetRendererBounds(out Bounds bounds))
        {
            lowestY = float.PositiveInfinity;
            return false;
        }

        lowestY = bounds.min.y;
        return true;
    }

    private bool TryGetRendererBounds(out Bounds bounds)
    {
        if (groundSnapRenderers == null || groundSnapRenderers.Length == 0)
            groundSnapRenderers = GetComponentsInChildren<Renderer>();

        bounds = default;
        bool hasBounds = false;
        foreach (Renderer renderer in groundSnapRenderers)
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

    private bool TryGetTerrainHeight(Vector3 worldPosition, out float terrainY)
    {
        Terrain[] activeTerrains = Terrain.activeTerrains;
        foreach (Terrain terrain in activeTerrains)
        {
            if (terrain == null || terrain.terrainData == null)
                continue;

            Vector3 terrainPosition = terrain.transform.position;
            Vector3 terrainSize = terrain.terrainData.size;
            bool insideX = worldPosition.x >= terrainPosition.x && worldPosition.x <= terrainPosition.x + terrainSize.x;
            bool insideZ = worldPosition.z >= terrainPosition.z && worldPosition.z <= terrainPosition.z + terrainSize.z;

            if (!insideX || !insideZ)
                continue;

            terrainY = terrain.SampleHeight(worldPosition) + terrainPosition.y;
            return true;
        }

        terrainY = worldPosition.y;
        return false;
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
