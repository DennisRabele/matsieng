using System.Collections;
using UnityEngine;

public class OrbitAroundPivot : MonoBehaviour
{
    [Header("Orbit")]
    [SerializeField] private Transform pivot;
    [SerializeField] private Vector3 axis = Vector3.up;

    [Header("Timing")]
    [Min(0.01f)]
    [SerializeField] private float durationSeconds = 6f;

    [Tooltip("Total degrees rotated over the whole duration. 360 = one full orbit.")]
    [SerializeField] private float totalDegrees = 360f;

    [Header("Playback")]
    [SerializeField] private bool playOnStart = true;

    [Header("Message")]
    [SerializeField] private bool showMessageWhileOrbiting = true;
    [TextArea(2, 6)]
    [SerializeField] private string waitMessage = "";
    [Tooltip("Higher priority messages override lower priority ones while both are active. (Cameras get +10 automatically.)")]
    [SerializeField] private int messagePriority = 0;

    [Header("Easing")]
    [SerializeField] private AnimationCurve orbitEase = AnimationCurve.Linear(0f, 0f, 1f, 1f);

    private Coroutine orbitRoutine;
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
        if (orbitRoutine != null)
        {
            StopCoroutine(orbitRoutine);
            ClearMessage();
        }

        orbitRoutine = StartCoroutine(Orbit());
    }

    public void Stop()
    {
        if (orbitRoutine == null)
            return;

        StopCoroutine(orbitRoutine);
        orbitRoutine = null;
        ClearMessage();
    }

    private IEnumerator Orbit()
    {
        if (pivot == null)
        {
            Debug.LogWarning($"{nameof(OrbitAroundPivot)} on '{name}' is missing a pivot.", this);
            yield break;
        }

        if (durationSeconds <= 0f)
        {
            Debug.LogWarning($"{nameof(OrbitAroundPivot)} on '{name}' has invalid duration.", this);
            yield break;
        }

        Vector3 orbitAxis = axis.sqrMagnitude < 0.0001f ? Vector3.up : axis.normalized;

        if (showMessageWhileOrbiting)
            ShowMessageForSeconds(waitMessage, durationSeconds);

        float elapsed = 0f;
        float lastDegrees = 0f;

        while (elapsed < durationSeconds)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / durationSeconds);
            float eased = orbitEase != null ? orbitEase.Evaluate(t) : t;

            float currentDegrees = totalDegrees * eased;
            float delta = currentDegrees - lastDegrees;
            lastDegrees = currentDegrees;

            transform.RotateAround(pivot.position, orbitAxis, delta);
            yield return null;
        }

        orbitRoutine = null;
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
