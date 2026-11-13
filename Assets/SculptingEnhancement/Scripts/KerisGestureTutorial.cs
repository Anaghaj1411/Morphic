using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Practice screens shown after Indonesia sculpting starts and before the
// keris color palette. Skip jumps straight to sculpting.
public sealed class KerisGestureTutorial : MonoBehaviour
{
    private struct Step
    {
        public string Title;
        public string Instruction;
        public KerisGestureController.PracticeGesture Gesture;
        public string ImagePath;
        public string Preview;
    }

    private const string ImageFolder =
        "MorphicGestures/";

    private static readonly Step[] Steps =
    {
        new Step
        {
            Title = "WIDTH & HEIGHT",
            Instruction = "Point both index fingers. Move them apart or together, and up or down.",
            Gesture = KerisGestureController.PracticeGesture.TwoIndex,
            ImagePath = ImageFolder + "gesture_two_index.jpg"
        },
        new Step
        {
            Title = "SHARPNESS",
            Instruction = "Pinch with your right hand, then lift to sharpen the blade.",
            Gesture = KerisGestureController.PracticeGesture.RightPinch,
            ImagePath = ImageFolder + "gesture_right_pinch.jpg"
        },
        new Step
        {
            Title = "CURVE",
            Instruction = "Pinch with your left hand, then lift to curve the blade.",
            Gesture = KerisGestureController.PracticeGesture.LeftPinch,
            ImagePath = ImageFolder + "gesture_left_pinch.jpg"
        },
        new Step
        {
            Title = "HANDLE",
            Instruction = "Make two fists and pull them down.",
            Gesture = KerisGestureController.PracticeGesture.TwoFists,
            ImagePath = ImageFolder + "gesture_two_fists.jpg"
        },
        new Step
        {
            Title = "CARVING",
            Instruction = "Make a V with both hands and spread them to the sides.",
            Gesture = KerisGestureController.PracticeGesture.TwoV,
            ImagePath = ImageFolder + "gesture_two_v.jpg"
        },
        new Step
        {
            Title = "ROTATE",
            Instruction = "Make one fist and move it left, right, up, or down.",
            Gesture = KerisGestureController.PracticeGesture.SingleFist,
            ImagePath = ImageFolder + "gesture_one_fist.jpg"
        },
        new Step
        {
            Title = "VERTEX SCULPT",
            Instruction = "Point your right index finger at the keris and move it to push the clay.",
            Gesture = KerisGestureController.PracticeGesture.PointIndex,
            ImagePath = ImageFolder + "gesture_two_index.jpg"
        },
        new Step
        {
            Title = "DRAW BAND",
            Instruction = "Point your right index finger and trace a band. Open your palm to lock.",
            Gesture = KerisGestureController.PracticeGesture.PointIndex,
            ImagePath = ImageFolder + "gesture_two_index.jpg",
            Preview = "DrawBand"
        },
        new Step
        {
            Title = "LOCK",
            Instruction = "After you change the keris, open your palm to rest.",
            Gesture = KerisGestureController.PracticeGesture.OpenPalm,
            ImagePath = ImageFolder + "gesture_open_palm.jpg"
        }
    };

    private KerisGestureController gestureController;
    private Action onFinished;
    private int stepIndex;
    private float recognizedHold;
    private bool stepRecognized;
    private bool preserveSculpt;

    private GameObject root;
    private TMP_Text titleText;
    private TMP_Text instructionText;
    private TMP_Text statusText;
    private TMP_Text stepText;
    private RawImage gestureImage;
    private MorphicGesturePreview gesturePreview;
    private Button previousButton;
    private Button nextButton;

    public bool IsShowing => root != null && root.activeSelf;

    public void RestartCards()
    {
        stepIndex = 0;
        EnsureUi();
        ShowStep();
        if (root != null)
        {
            root.SetActive(true);
            root.transform.SetAsLastSibling();
        }
    }

    public void Begin(KerisGestureController controller, Action finished, bool keepSculpt = false)
    {
        gestureController = controller;
        onFinished = finished;
        preserveSculpt = keepSculpt;
        stepIndex = 0;
        if (!preserveSculpt && gestureController != null)
            gestureController.CaptureRestState();
        EnsureUi();
        ShowStep();
        if (root != null)
        {
            root.SetActive(true);
            root.transform.SetAsLastSibling();
        }
    }

    private void Update()
    {
        if (root == null || !root.activeSelf || gestureController == null)
            return;

        KerisGestureController.PracticeGesture current =
            gestureController.ReadPracticeGesture();
        if (current == Steps[stepIndex].Gesture)
        {
            recognizedHold += Time.unscaledDeltaTime;
            if (recognizedHold >= 0.45f)
                MarkRecognized();
        }
        else if (!stepRecognized)
        {
            recognizedHold = 0f;
        }
    }

    private void EnsureUi()
    {
        if (root != null)
            return;

        Canvas canvas = FindFirstObjectByType<Canvas>();
        if (canvas == null)
            return;

        root = new GameObject("KerisGestureTutorial", typeof(RectTransform));
        RectTransform rootRect = root.GetComponent<RectTransform>();
        rootRect.SetParent(canvas.transform, false);
        Stretch(rootRect);

        Image dim = CreateImage(rootRect, "Dim", new Color(0.08f, 0.06f, 0.04f, 0.52f));
        Stretch(dim.rectTransform);

        Image frame = LanguagePageBuilder.CreateRoundedImage(
            "Card", rootRect, 36, LanguagePageBuilder.Green);
        RectTransform cardRect = frame.rectTransform;
        cardRect.anchorMin = cardRect.anchorMax = new Vector2(0.5f, 0.5f);
        cardRect.pivot = new Vector2(0.5f, 0.5f);
        cardRect.sizeDelta = new Vector2(760f, 680f);
        frame.raycastTarget = true;

        Image fill = LanguagePageBuilder.CreateRoundedImage(
            "Fill", frame.transform, 32, new Color32(255, 239, 214, 255));
        LanguagePageBuilder.Stretch(fill.rectTransform, new Vector2(10f, 10f));
        fill.raycastTarget = false;

        stepText = CreateChip(cardRect, "Step", new Vector2(0f, 258f), new Vector2(360f, 44f), 16f);
        titleText = CreateLabel(cardRect, "Title", new Vector2(0f, 200f), 640f, 52f, 32);
        MorphicText.FitTitle(titleText);

        Image artFrame = LanguagePageBuilder.CreateRoundedImage(
            "ArtFrame", cardRect, 28, LanguagePageBuilder.Green);
        RectTransform artRect = artFrame.rectTransform;
        artRect.anchorMin = artRect.anchorMax = new Vector2(0.5f, 0.5f);
        artRect.pivot = new Vector2(0.5f, 0.5f);
        artRect.anchoredPosition = new Vector2(0f, 48f);
        artRect.sizeDelta = new Vector2(248f, 248f);
        artFrame.raycastTarget = false;
        Image artFill = LanguagePageBuilder.CreateRoundedImage(
            "ArtFill", artFrame.transform, 24, new Color(0.97f, 0.76f, 0.82f, 1f));
        LanguagePageBuilder.Stretch(artFill.rectTransform, new Vector2(7f, 7f));
        artFill.raycastTarget = false;
        gestureImage = CreateRawImage(artFill.transform, "GestureImage", Vector2.zero, new Vector2(196f, 196f));
        gesturePreview = MorphicGesturePreview.Create(gestureImage);

        instructionText = CreateLabel(cardRect, "Instruction", new Vector2(0f, -152f), 640f, 90f, 20);
        MorphicText.FitBody(instructionText);
        statusText = CreateLabel(cardRect, "Status", new Vector2(0f, -248f), 400f, 36f, 22);
        MorphicText.Style(statusText);
        statusText.gameObject.SetActive(false);

        CreateMorphicButton(cardRect, "Skip", new Vector2(278f, 258f), new Vector2(160f, 48f),
            "SKIP", LanguagePageBuilder.Pink, Skip);
        CreateMorphicButton(cardRect, "Replay", new Vector2(-226f, -268f), new Vector2(152f, 56f),
            "REPLAY", LanguagePageBuilder.Pink, Replay);
        previousButton = CreateMorphicButton(cardRect, "Previous", new Vector2(0f, -268f), new Vector2(164f, 56f),
            "PREVIOUS", LanguagePageBuilder.Pink, Back);
        nextButton = CreateMorphicButton(cardRect, "Next", new Vector2(226f, -268f), new Vector2(188f, 56f),
            "NEXT", new Color32(236, 64, 168, 255), Next);
    }

    private void ShowStep()
    {
        stepRecognized = false;
        recognizedHold = 0f;
        Step step = Steps[stepIndex];
        MorphicLoc.ApplyFont(stepText);
        stepText.text = MorphicLoc.F("GESTURE {0} OF {1}", stepIndex + 1, Steps.Length);
        MorphicLoc.Bind(titleText, step.Title);
        MorphicText.FitTitle(titleText);
        MorphicLoc.Bind(instructionText, step.Instruction);
        MorphicText.FitBody(instructionText);
        MorphicText.FitButton(stepText);
        if (gesturePreview != null)
            gesturePreview.Play(
                string.IsNullOrEmpty(step.Preview) ? step.Gesture.ToString() : step.Preview,
                step.ImagePath);
        if (statusText != null)
        {
            statusText.text = "";
            statusText.gameObject.SetActive(false);
        }
        MorphicLoc.Bind(
            nextButton.GetComponentInChildren<TMP_Text>(),
            stepIndex == Steps.Length - 1
                ? (preserveSculpt ? "DONE" : "START SCULPTING")
                : "NEXT");
        if (previousButton != null)
            previousButton.interactable = stepIndex > 0;
    }

    private void MarkRecognized()
    {
        if (stepRecognized)
            return;
        stepRecognized = true;
        statusText.gameObject.SetActive(true);
        MorphicLoc.Bind(statusText, "GOOD");
        statusText.color = LanguagePageBuilder.Green;
    }

    private void Back()
    {
        if (stepIndex <= 0)
            return;
        stepIndex--;
        ShowStep();
    }

    private void Next()
    {
        if (stepIndex >= Steps.Length - 1)
        {
            Finish();
            return;
        }

        stepIndex++;
        ShowStep();
    }

    private void Replay()
    {
        stepIndex = 0;
        ShowStep();
    }

    private void Skip()
    {
        Finish();
    }

    private void Finish()
    {
        if (!preserveSculpt && gestureController != null)
            gestureController.RestoreRestState();
        if (root != null)
            root.SetActive(false);
        Action done = onFinished;
        onFinished = null;
        done?.Invoke();
    }

    public void Hide()
    {
        if (!preserveSculpt && root != null && root.activeSelf && gestureController != null)
            gestureController.RestoreRestState();
        if (root != null)
            root.SetActive(false);
        onFinished = null;
    }

    private static void Stretch(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
    }

    private static Texture2D LoadTutorialTexture(string path)
    {
        string resourcePath = path.Replace(".jpg", string.Empty);
        return Resources.Load<Texture2D>(resourcePath);
    }

    private static RawImage CreateRawImage(
        Transform parent,
        string name,
        Vector2 position,
        Vector2 size)
    {
        GameObject go = new GameObject(name, typeof(RectTransform), typeof(RawImage));
        go.transform.SetParent(parent, false);
        RectTransform rect = go.GetComponent<RectTransform>();
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
        RawImage image = go.GetComponent<RawImage>();
        image.color = Color.white;
        image.raycastTarget = false;
        return image;
    }

    private static Image CreateImage(Transform parent, string name, Color color)
    {
        GameObject go = new GameObject(name, typeof(RectTransform), typeof(Image));
        go.transform.SetParent(parent, false);
        Image image = go.GetComponent<Image>();
        image.color = color;
        return image;
    }

    private static TMP_Text CreateLabel(
        Transform parent,
        string name,
        Vector2 position,
        float width,
        float height,
        float size)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        RectTransform rect = go.GetComponent<RectTransform>();
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = position;
        rect.sizeDelta = new Vector2(width, height);
        TextMeshProUGUI text = go.AddComponent<TextMeshProUGUI>();
        text.fontSize = MorphicText.Size(size);
        text.alignment = TextAlignmentOptions.Center;
        MorphicText.Style(text);
        MorphicText.FitButton(text);
        return text;
    }

    private static TMP_Text CreateChip(
        Transform parent,
        string name,
        Vector2 position,
        Vector2 size,
        float fontSize)
    {
        Image frame = LanguagePageBuilder.CreateRoundedImage(name, parent, 18, LanguagePageBuilder.Green);
        RectTransform rect = frame.rectTransform;
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
        frame.raycastTarget = false;
        Image fill = LanguagePageBuilder.CreateRoundedImage("Fill", frame.transform, 14, LanguagePageBuilder.Pink);
        LanguagePageBuilder.Stretch(fill.rectTransform, new Vector2(4f, 4f));
        fill.raycastTarget = false;
        TMP_Text text = CreateLabel(fill.transform, "Label", Vector2.zero, size.x, size.y, fontSize);
        Stretch(text.rectTransform);
        return text;
    }

    private static Button CreateIconButton(
        Transform parent,
        string name,
        Vector2 position,
        Vector2 size,
        string iconPath,
        Color fillColor,
        Action onClick)
    {
        Image frame = LanguagePageBuilder.CreateRoundedImage(name, parent, 24, LanguagePageBuilder.Green);
        RectTransform rect = frame.rectTransform;
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
        frame.raycastTarget = true;
        Image fill = LanguagePageBuilder.CreateRoundedImage("Fill", frame.transform, 20, fillColor);
        LanguagePageBuilder.Stretch(fill.rectTransform, new Vector2(5f, 5f));
        fill.raycastTarget = true;

        GameObject iconObj = new GameObject("Icon");
        iconObj.transform.SetParent(fill.transform, false);
        Image icon = iconObj.AddComponent<Image>();
        RectTransform iconRect = icon.rectTransform;
        iconRect.anchorMin = iconRect.anchorMax = new Vector2(0.5f, 0.5f);
        iconRect.pivot = new Vector2(0.5f, 0.5f);
        iconRect.anchoredPosition = Vector2.zero;
        iconRect.sizeDelta = new Vector2(size.x * 0.65f, size.y * 0.65f);

        Texture2D iconTexture = Resources.Load<Texture2D>(iconPath);
        if (iconTexture != null)
        {
            Sprite iconSprite = Sprite.Create(iconTexture,
                new Rect(0, 0, iconTexture.width, iconTexture.height),
                new Vector2(0.5f, 0.5f), 100f);
            icon.sprite = iconSprite;
        }

        Button button = frame.gameObject.AddComponent<Button>();
        button.targetGraphic = fill;
        DialogueButtonClickSound.Instance?.RegisterButton(button);
        button.onClick.AddListener(() => onClick());
        return button;
    }

    private static Button CreateMorphicButton(
        Transform parent,
        string name,
        Vector2 position,
        Vector2 size,
        string label,
        Color fillColor,
        Action onClick)
    {
        Image frame = LanguagePageBuilder.CreateRoundedImage(name, parent, 24, LanguagePageBuilder.Green);
        RectTransform rect = frame.rectTransform;
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
        frame.raycastTarget = true;
        Image fill = LanguagePageBuilder.CreateRoundedImage("Fill", frame.transform, 20, fillColor);
        LanguagePageBuilder.Stretch(fill.rectTransform, new Vector2(5f, 5f));
        fill.raycastTarget = true;
        Button button = frame.gameObject.AddComponent<Button>();
        button.targetGraphic = fill;
        button.colors = LanguagePageBuilder.ButtonColors();
        DialogueButtonClickSound.Instance?.RegisterButton(button);
        button.onClick.AddListener(() => onClick());
        TMP_Text text = CreateLabel(fill.transform, "Label", Vector2.zero, size.x - 12f, size.y, 18f);
        Stretch(text.rectTransform);
        MorphicLoc.Bind(text, label);
        MorphicText.FitButton(text);
        return button;
    }
}
