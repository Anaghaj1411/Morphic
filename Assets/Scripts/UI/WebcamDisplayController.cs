using UnityEngine;
using UnityEngine.UI;
using Mediapipe.Unity;

public class WebcamDisplayController : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Mediapipe.Unity.Screen webcamScreen;
    [SerializeField] private RectTransform borderPanel;

    [Header("Positioning")]
    [SerializeField] private Vector2 displaySize = new Vector2(380, 286);
    [SerializeField] private Vector2 anchoredPosition = new Vector2(24, 36);

    private const float FramePad = 16f;
    private static readonly Vector2 DefaultSize = new Vector2(380f, 286f);
    private static readonly Vector2 DefaultBottomLeft = new Vector2(24f, 36f);
    private RectTransform videoWell;
    private bool frameBuilt;

    private void Awake()
    {
        SetupDisplay();
        Deactivate();
    }

    private void SetupDisplay()
    {
        if (borderPanel == null)
            borderPanel = GetComponent<RectTransform>();

        PlaceBottomLeft();
        HideHostImage();
        EnsureBubbleFrame();
        PlaceWebcam();
    }

    public void UseScreen(Mediapipe.Unity.Screen screen)
    {
        if (screen == null)
        {
            Debug.LogError("MORPHIC: Cannot display a null MediaPipe Screen.");
            return;
        }

        if (webcamScreen != null && webcamScreen != screen)
            webcamScreen.gameObject.SetActive(false);

        webcamScreen = screen;
        SetupDisplay();
    }

    public void Activate()
    {
        gameObject.SetActive(true);
        if (webcamScreen != null)
            webcamScreen.gameObject.SetActive(true);
        SetupDisplay();
    }

    public void Deactivate()
    {
        if (webcamScreen != null)
            webcamScreen.gameObject.SetActive(false);
    }

    private void LateUpdate()
    {
        if (!isActiveAndEnabled)
            return;

        PlaceBottomLeft();
        KeepWebcamStretched();
    }

    private void PlaceBottomLeft()
    {
        if (borderPanel == null)
            return;

        // Scene copies still store the old top-left coords (y negative),
        // which would push this off the bottom of the canvas.
        Vector2 size = displaySize.x >= 320f ? displaySize : DefaultSize;
        Vector2 position = anchoredPosition.y >= 0f ? anchoredPosition : DefaultBottomLeft;

        borderPanel.anchorMin = Vector2.zero;
        borderPanel.anchorMax = Vector2.zero;
        borderPanel.pivot = Vector2.zero;
        borderPanel.anchoredPosition = position;
        borderPanel.sizeDelta = size + new Vector2(FramePad * 2f, FramePad * 2f);
    }

    private void HideHostImage()
    {
        Image hostImage = GetComponent<Image>();
        if (hostImage == null)
            return;

        hostImage.enabled = false;
        hostImage.raycastTarget = false;
    }

    private void EnsureBubbleFrame()
    {
        if (frameBuilt)
            return;

        frameBuilt = true;

        Image green = LanguagePageBuilder.CreateRoundedImage(
            "BubbleFrame", transform, 36, LanguagePageBuilder.Green);
        LanguagePageBuilder.Stretch(green.rectTransform);
        green.raycastTarget = false;
        green.transform.SetAsFirstSibling();

        Image pink = LanguagePageBuilder.CreateRoundedImage(
            "PinkRing", green.transform, 32, LanguagePageBuilder.Pink);
        LanguagePageBuilder.Stretch(pink.rectTransform, new Vector2(8f, 8f));
        pink.raycastTarget = false;

        Image well = LanguagePageBuilder.CreateRoundedImage(
            "Well", pink.transform, 28, new Color32(255, 239, 214, 255));
        LanguagePageBuilder.Stretch(well.rectTransform, new Vector2(6f, 6f));
        well.raycastTarget = false;

        Image maskImage = LanguagePageBuilder.CreateRoundedImage(
            "VideoMask", well.transform, 24, Color.white);
        LanguagePageBuilder.Stretch(maskImage.rectTransform, new Vector2(5f, 5f));
        maskImage.raycastTarget = false;
        Mask mask = maskImage.gameObject.AddComponent<Mask>();
        mask.showMaskGraphic = false;
        videoWell = maskImage.rectTransform;

        AddBubble(transform, "BubbleTL", new Vector2(0f, 1f), new Vector2(10f, -8f), 22f, LanguagePageBuilder.Pink);
        AddBubble(transform, "BubbleTR", new Vector2(1f, 1f), new Vector2(-8f, -6f), 16f, LanguagePageBuilder.Green);
        AddBubble(transform, "BubbleBL", new Vector2(0f, 0f), new Vector2(12f, 8f), 18f, LanguagePageBuilder.Green);
        AddBubble(transform, "BubbleBR", new Vector2(1f, 0f), new Vector2(-6f, 10f), 20f, LanguagePageBuilder.Pink);
        AddBubble(transform, "BubbleL", new Vector2(0f, 0.55f), new Vector2(-5f, 0f), 13f, LanguagePageBuilder.Pink);
        AddBubble(transform, "BubbleR", new Vector2(1f, 0.42f), new Vector2(6f, 0f), 14f, LanguagePageBuilder.Green);
    }

    private static void AddBubble(
        Transform parent,
        string name,
        Vector2 anchor,
        Vector2 position,
        float size,
        Color color)
    {
        Image bubble = LanguagePageBuilder.CreateImage(
            name,
            parent,
            LanguagePageBuilder.Circle(64));
        RectTransform rect = bubble.rectTransform;
        rect.anchorMin = rect.anchorMax = anchor;
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = position;
        rect.sizeDelta = new Vector2(size, size);
        bubble.color = color;
        bubble.raycastTarget = false;
    }

    private void PlaceWebcam()
    {
        if (webcamScreen == null)
            webcamScreen = GetComponentInChildren<Mediapipe.Unity.Screen>(true);
        if (webcamScreen == null)
            return;

        Transform parent = videoWell != null ? (Transform)videoWell : transform;
        webcamScreen.transform.SetParent(parent, false);
        webcamScreen.transform.SetAsLastSibling();

        KeepWebcamStretched();
    }

    private void KeepWebcamStretched()
    {
        if (webcamScreen == null)
            return;

        RectTransform screenRect = webcamScreen.GetComponent<RectTransform>();
        if (screenRect == null)
            return;

        screenRect.anchorMin = Vector2.zero;
        screenRect.anchorMax = Vector2.one;
        screenRect.pivot = new Vector2(0.5f, 0.5f);
        screenRect.anchoredPosition = Vector2.zero;
        screenRect.sizeDelta = Vector2.zero;
        screenRect.offsetMin = Vector2.zero;
        screenRect.offsetMax = Vector2.zero;
    }
}
