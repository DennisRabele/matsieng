using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem.UI;
#endif
using UnityEngine.UI;

public class TourCameraMenu : MonoBehaviour
{
    private const int ActiveButtonFontSize = 32;
    private const int ButtonFontSize = 30;

    [Header("Camera Names")]
    [SerializeField] private string wholeViewCameraName = "WholeViewCamera";
    [SerializeField] private string lakeCameraName = "LakeCamera";
    [SerializeField] private string villageCameraName = "VillageCamera";
    [SerializeField] private string personCameraName = "personAround";
    [SerializeField] private string personTargetName = "Ch12_nonPBR (1)";
    [SerializeField] private bool villageCameraFollowsPerson = true;

    [Header("Labels")]
    [SerializeField] private string wholeViewButtonText = "See whole of Matsieng place";
    [SerializeField] private string lakeButtonText = "View Foothill and Dam";
    [SerializeField] private string villageButtonText = "Village";
    [SerializeField] private string personButtonText = "Person Around";

    [Header("Style")]
    [SerializeField] private Color panelColor = new Color(0.04f, 0.06f, 0.08f, 0.84f);
    [SerializeField] private Color buttonColor = new Color(0.12f, 0.17f, 0.22f, 0.94f);
    [SerializeField] private Color activeButtonColor = new Color(0.9f, 0.58f, 0.18f, 1f);
    [SerializeField] private Color buttonTextColor = Color.white;
    [SerializeField] private Color activeButtonTextColor = new Color(0.05f, 0.04f, 0.03f, 1f);

    [Header("Audio")]
    [SerializeField] private AudioClip introClip;
    [SerializeField] private AudioClip wholeViewClip;
    [SerializeField] private AudioClip lakeClip;
    [SerializeField] private AudioClip villageClip;
    [SerializeField] private string introClipResourcePath = "TourAudio/marker-intro";
    [SerializeField] private string wholeViewClipResourcePath = "TourAudio/whole-view";
    [SerializeField] private string lakeClipResourcePath = "TourAudio/foothill-dam";
    [SerializeField] private string villageClipResourcePath = "TourAudio/village";
    [Range(0f, 1f)]
    [SerializeField] private float audioVolume = 0.75f;
    [SerializeField] private bool loopButtonAudio = true;

    private readonly List<Button> buttons = new List<Button>();
    private readonly List<Text> buttonTexts = new List<Text>();

    private GameObject wholeViewCamera;
    private GameObject lakeCamera;
    private GameObject villageCamera;
    private GameObject personCamera;
    private Font uiFont;
    private AudioSource audioSource;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void CreateForScene()
    {
        if (FindFirstObjectByType<TourCameraMenu>() != null)
            return;

        GameObject menuGo = new GameObject("TourCameraMenu");
        DontDestroyOnLoad(menuGo);
        menuGo.AddComponent<TourCameraMenu>();
    }

    private void Awake()
    {
        uiFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        LoadAudioConfig();
        audioSource = gameObject.AddComponent<AudioSource>();
        audioSource.playOnAwake = false;
        audioSource.volume = audioVolume;
        FindCameras();
        EnsureEventSystem();
        BuildMenu();
    }

    public void PlayIntroAudio()
    {
        PlayAudioClip(introClip, true);
    }

    private void LoadAudioConfig()
    {
        TourCameraMenuAudioConfig config = FindFirstObjectByType<TourCameraMenuAudioConfig>();
        if (config != null)
        {
            introClip = config.introClip != null ? config.introClip : introClip;
            wholeViewClip = config.wholeViewClip != null ? config.wholeViewClip : wholeViewClip;
            lakeClip = config.lakeClip != null ? config.lakeClip : lakeClip;
            villageClip = config.villageClip != null ? config.villageClip : villageClip;
            audioVolume = config.audioVolume;
            loopButtonAudio = config.loopButtonAudio;
        }

        introClip = LoadClipIfMissing(introClip, introClipResourcePath);
        wholeViewClip = LoadClipIfMissing(wholeViewClip, wholeViewClipResourcePath);
        lakeClip = LoadClipIfMissing(lakeClip, lakeClipResourcePath);
        lakeClip = LoadClipIfMissing(lakeClip, "foothill");
        villageClip = LoadClipIfMissing(villageClip, villageClipResourcePath);
    }

    private static AudioClip LoadClipIfMissing(AudioClip clip, string resourcePath)
    {
        if (clip != null || string.IsNullOrWhiteSpace(resourcePath))
            return clip;

        AudioClip resourceClip = Resources.Load<AudioClip>(resourcePath);
        if (resourceClip != null)
            return resourceClip;

        int slashIndex = resourcePath.LastIndexOf('/');
        if (slashIndex >= 0 && slashIndex < resourcePath.Length - 1)
            return Resources.Load<AudioClip>(resourcePath.Substring(slashIndex + 1));

        return null;
    }

    private void FindCameras()
    {
        wholeViewCamera = FindSceneObject(wholeViewCameraName);
        lakeCamera = FindSceneObject(lakeCameraName);
        villageCamera = FindSceneObject(villageCameraName);
        personCamera = FindSceneObject(personCameraName);
        EnsurePersonCamera();

        if (villageCameraFollowsPerson && villageCamera != null)
            EnsureFollowCamera(villageCamera, true);
    }

    private void BuildMenu()
    {
        Canvas canvas = gameObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 6000;

        CanvasScaler scaler = gameObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        gameObject.AddComponent<GraphicRaycaster>();

        Image panel = CreatePanel(transform);
        RectTransform panelRect = panel.rectTransform;
        panelRect.anchorMin = new Vector2(0.5f, 1f);
        panelRect.anchorMax = new Vector2(0.5f, 1f);
        panelRect.pivot = new Vector2(0.5f, 1f);
        panelRect.anchoredPosition = new Vector2(0f, -28f);
        panelRect.sizeDelta = new Vector2(1680f, 148f);

        HorizontalLayoutGroup layout = panel.gameObject.AddComponent<HorizontalLayoutGroup>();
        layout.padding = new RectOffset(22, 22, 22, 22);
        layout.spacing = 20f;
        layout.childAlignment = TextAnchor.MiddleCenter;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = true;

        ContentSizeFitter fitter = panel.gameObject.AddComponent<ContentSizeFitter>();
        fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
        fitter.verticalFit = ContentSizeFitter.FitMode.Unconstrained;

        AddCameraButton(panel.transform, wholeViewButtonText, () => SelectCamera(0));
        AddCameraButton(panel.transform, lakeButtonText, () => SelectCamera(1));
        AddCameraButton(panel.transform, villageButtonText, () => SelectCamera(2));
        AddCameraButton(panel.transform, personButtonText, () => SelectCamera(3));

        if (personCamera != null && personCamera.activeInHierarchy)
            UpdateButtonStates(3);
        else if (lakeCamera != null && lakeCamera.activeInHierarchy)
            UpdateButtonStates(1);
        else if (wholeViewCamera != null && wholeViewCamera.activeInHierarchy)
            UpdateButtonStates(0);
        else if (villageCamera != null && villageCamera.activeInHierarchy)
            UpdateButtonStates(2);
    }

    private Image CreatePanel(Transform parent)
    {
        GameObject panelGo = new GameObject("Tour Options");
        panelGo.transform.SetParent(parent, false);

        Image panel = panelGo.AddComponent<Image>();
        panel.color = panelColor;
        return panel;
    }

    private void AddCameraButton(Transform parent, string label, UnityEngine.Events.UnityAction onClick)
    {
        GameObject buttonGo = new GameObject(label);
        buttonGo.transform.SetParent(parent, false);

        Image image = buttonGo.AddComponent<Image>();
        image.color = buttonColor;

        Button button = buttonGo.AddComponent<Button>();
        button.targetGraphic = image;
        button.onClick.AddListener(onClick);

        ColorBlock colors = button.colors;
        colors.normalColor = buttonColor;
        colors.highlightedColor = new Color(0.22f, 0.3f, 0.38f, 1f);
        colors.pressedColor = activeButtonColor;
        colors.selectedColor = activeButtonColor;
        colors.colorMultiplier = 1f;
        button.colors = colors;

        LayoutElement layout = buttonGo.AddComponent<LayoutElement>();
        layout.minWidth = 260f;
        layout.preferredWidth = 360f;
        layout.minHeight = 104f;

        Text text = CreateButtonText(buttonGo.transform, label);

        buttons.Add(button);
        buttonTexts.Add(text);
    }

    private Text CreateButtonText(Transform parent, string label)
    {
        GameObject textGo = new GameObject("Label");
        textGo.transform.SetParent(parent, false);

        Text text = textGo.AddComponent<Text>();
        text.font = uiFont;
        text.text = label;
        text.fontSize = ButtonFontSize;
        text.fontStyle = FontStyle.Bold;
        text.color = buttonTextColor;
        text.alignment = TextAnchor.MiddleCenter;
        text.horizontalOverflow = HorizontalWrapMode.Wrap;
        text.verticalOverflow = VerticalWrapMode.Truncate;

        RectTransform rect = text.rectTransform;
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = new Vector2(16f, 8f);
        rect.offsetMax = new Vector2(-16f, -8f);
        return text;
    }

    private void SelectCamera(int selectedButtonIndex)
    {
        FindCameras();

        GameObject selectedCamera = selectedButtonIndex switch
        {
            0 => wholeViewCamera,
            1 => lakeCamera,
            2 => villageCamera,
            3 => EnsurePersonCamera(),
            _ => null
        };

        if (selectedCamera == null)
        {
            Debug.LogWarning($"{nameof(TourCameraMenu)} could not find the selected camera.", this);
            return;
        }

        DisableOtherCameras(selectedCamera);
        SetCameraActive(selectedCamera, true);
        StartCameraBehaviour(selectedCamera);
        PlayAudioClip(GetButtonAudioClip(selectedButtonIndex), loopButtonAudio);
        UpdateButtonStates(selectedButtonIndex);
    }

    private AudioClip GetButtonAudioClip(int selectedButtonIndex)
    {
        return selectedButtonIndex switch
        {
            0 => wholeViewClip,
            1 => lakeClip,
            2 => villageClip,
            3 => lakeClip,
            _ => null
        };
    }

    private void PlayAudioClip(AudioClip clip, bool loop)
    {
        if (audioSource == null)
            return;

        audioSource.Stop();
        audioSource.clip = clip;
        audioSource.loop = loop;
        audioSource.volume = audioVolume;

        if (clip != null)
            audioSource.Play();
    }

    private void DisableOtherCameras(GameObject selectedCamera)
    {
        Camera[] sceneCameras = Resources.FindObjectsOfTypeAll<Camera>();
        foreach (Camera sceneCamera in sceneCameras)
        {
            if (sceneCamera == null || !sceneCamera.gameObject.scene.IsValid())
                continue;

            if (sceneCamera.gameObject != selectedCamera)
                SetCameraActive(sceneCamera.gameObject, false);
        }
    }

    private void SetCameraActive(GameObject cameraGo, bool active)
    {
        if (cameraGo == null)
            return;

        if (!active)
        {
            CharacterScrpit movement = cameraGo.GetComponent<CharacterScrpit>();
            if (movement != null)
                movement.Stop();

            OrbitAroundPivot orbit = cameraGo.GetComponent<OrbitAroundPivot>();
            if (orbit != null)
                orbit.Stop();
        }

        cameraGo.SetActive(active);
    }

    private void StartCameraBehaviour(GameObject cameraGo)
    {
        CharacterScrpit movement = cameraGo.GetComponent<CharacterScrpit>();
        if (movement != null)
            movement.Play();

        OrbitAroundPivot orbit = cameraGo.GetComponent<OrbitAroundPivot>();
        if (orbit != null)
            orbit.Play();

        CameraMessageDisplay messageDisplay = cameraGo.GetComponent<CameraMessageDisplay>();
        if (messageDisplay != null)
            messageDisplay.ShowMessage();

        if (cameraGo == personCamera)
            EnsureFollowCamera(cameraGo, false);
        else if (villageCameraFollowsPerson && cameraGo == villageCamera)
            EnsureFollowCamera(cameraGo, true);
    }

    private void UpdateButtonStates(int activeIndex)
    {
        for (int i = 0; i < buttons.Count; i++)
        {
            Image image = buttons[i].targetGraphic as Image;
            if (image != null)
                image.color = i == activeIndex ? activeButtonColor : buttonColor;

            buttonTexts[i].color = i == activeIndex ? activeButtonTextColor : buttonTextColor;
            buttonTexts[i].fontSize = i == activeIndex ? ActiveButtonFontSize : ButtonFontSize;
        }
    }

    private static GameObject FindSceneObject(string objectName)
    {
        if (string.IsNullOrWhiteSpace(objectName))
            return null;

        GameObject[] allObjects = Resources.FindObjectsOfTypeAll<GameObject>();
        foreach (GameObject go in allObjects)
        {
            if (go.name == objectName && go.scene.IsValid() && go.hideFlags == HideFlags.None)
                return go;
        }

        return null;
    }

    private GameObject EnsurePersonCamera()
    {
        if (personCamera == null)
            personCamera = FindSceneObject(personCameraName);

        if (personCamera == null)
        {
            personCamera = new GameObject(personCameraName);
            personCamera.tag = "MainCamera";

            Camera camera = personCamera.AddComponent<Camera>();
            camera.clearFlags = CameraClearFlags.Skybox;
            camera.fieldOfView = 65f;
            camera.nearClipPlane = 0.3f;
            camera.farClipPlane = 10000f;
            camera.depth = 0f;

            personCamera.AddComponent<AudioListener>();
            personCamera.AddComponent<ThirdPersonFollowCamera>();
            personCamera.SetActive(false);
        }

        if (personCamera.GetComponent<Camera>() == null)
            personCamera.AddComponent<Camera>();

        if (personCamera.GetComponent<AudioListener>() == null)
            personCamera.AddComponent<AudioListener>();

        EnsureFollowCamera(personCamera, false);

        return personCamera;
    }

    private void EnsureFollowCamera(GameObject cameraGo, bool keepCurrentOffset)
    {
        if (cameraGo == null)
            return;

        ThirdPersonFollowCamera followCamera = cameraGo.GetComponent<ThirdPersonFollowCamera>();
        if (followCamera == null)
            followCamera = cameraGo.AddComponent<ThirdPersonFollowCamera>();

        ConfigurePersonCameraTarget(followCamera, keepCurrentOffset);
    }

    private void ConfigurePersonCameraTarget(ThirdPersonFollowCamera followCamera, bool keepCurrentOffset)
    {
        if (followCamera == null)
            return;

        GameObject target = FindSceneObject(personTargetName);
        if (target == null)
        {
            Debug.LogWarning($"{nameof(TourCameraMenu)} could not find '{personTargetName}' for the person camera.", this);
            return;
        }

        followCamera.SetTarget(target.transform, keepCurrentOffset);
    }

    private static void EnsureEventSystem()
    {
        if (FindFirstObjectByType<EventSystem>() != null)
            return;

        GameObject eventSystemGo = new GameObject("EventSystem");
        DontDestroyOnLoad(eventSystemGo);
        eventSystemGo.AddComponent<EventSystem>();

#if ENABLE_INPUT_SYSTEM
        eventSystemGo.AddComponent<InputSystemUIInputModule>();
#elif ENABLE_LEGACY_INPUT_MANAGER
        eventSystemGo.AddComponent<StandaloneInputModule>();
#endif
    }
}
