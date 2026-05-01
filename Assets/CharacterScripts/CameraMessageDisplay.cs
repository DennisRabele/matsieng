using UnityEngine;

public class CameraMessageDisplay : MonoBehaviour
{
    [Header("Message")]
    [SerializeField] private bool showMessageOnEnable = true;
    [TextArea(2, 6)]
    [SerializeField] private string waitMessage = "";
    [Min(0f)]
    [SerializeField] private float messageSeconds = 30f;
    [Tooltip("Higher priority messages override lower priority ones while both are active. (Cameras get +10 automatically.)")]
    [SerializeField] private int messagePriority = 0;

    private TourMessageUI messageUi;
    private uint lastMessageSequence;
    private bool hasCameraComponent;

    private void Awake()
    {
        hasCameraComponent = GetComponent<Camera>() != null;
    }

    private void OnEnable()
    {
        if (showMessageOnEnable)
            ShowMessage();
    }

    private void OnDisable()
    {
        ClearMessage();
    }

    public void ShowMessage()
    {
        if (string.IsNullOrWhiteSpace(waitMessage) || messageSeconds <= 0f)
            return;

        if (messageUi == null)
            messageUi = TourMessageUI.GetOrCreate();

        int effectivePriority = messagePriority + (hasCameraComponent ? 10 : 0);
        uint messageSequence = messageUi.Show(waitMessage, messageSeconds, effectivePriority);
        if (messageSequence != 0)
            lastMessageSequence = messageSequence;
    }

    public void ClearMessage()
    {
        if (messageUi == null || lastMessageSequence == 0)
            return;

        messageUi.Clear(lastMessageSequence);
        lastMessageSequence = 0;
    }
}
