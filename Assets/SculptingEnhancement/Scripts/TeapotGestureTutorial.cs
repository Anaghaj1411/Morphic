using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Practice screens shown after India sculpting starts and before the
// teapot color palette. Skip jumps straight to sculpting.
public sealed class TeapotGestureTutorial : MonoBehaviour
{
    private struct Step
    {
        public string Title;
        public string Instruction;
        public TeapotGestureController.PracticeGesture Gesture;
        public string ImagePath;
    }

    private const string ImageFolder =
        "Assets/SculptingEnhancement/Art/TutorialGestures/";

    private static readonly Step[] Steps =
    {
        new Step
        {
            Title = "WIDTH & HEIGHT",
            Instruction = "Point both index fingers. Move them apart or together, and up or down.",
            Gesture = TeapotGestureController.PracticeGesture.TwoIndex,
            ImagePath = ImageFolder + "gesture_two_index.jpg"
        },
        new Step
        {
            Title = "LID",
            Instruction = "Pinch with your right hand, then lift to raise the lid.",
            Gesture = TeapotGestureController.PracticeGesture.RightPinch,
            ImagePath = ImageFolder + "gesture_right_pinch.jpg"
        },
        new Step
        {
            Title = "SPOUT",
            Instruction = "Pinch with your left hand, then lift to pull the spout.",
            Gesture = TeapotGestureController.PracticeGesture.LeftPinch,
            ImagePath = ImageFolder + "gesture_left_pinch.jpg"
        },
        new Step
        {
            Title = "HANDLE",
            Instruction = "Make two fists and pull them down.",
            Gesture = TeapotGestureController.PracticeGesture.TwoFists,
            ImagePath = ImageFolder + "gesture_two_fists.jpg"
        },
        new Step
        {
            Title = "MIDDLE",
            Instruction = "Make a V with both hands and spread them to the sides.",
            Gesture = TeapotGestureController.PracticeGesture.TwoV,
            ImagePath = ImageFolder + "gesture_two_v.jpg"
        },
        new Step
        {
            Title = "ROTATE",
            Instruction = "Make one fist and move it left or right.",
            Gesture = TeapotGestureController.PracticeGesture.SingleFist,
            ImagePath = ImageFolder + "gesture_one_fist.jpg"
        },
        new Step
        {
            Title = "BEVEL",
            Instruction = "Make a V with your right hand and raise or lower it.",
            Gesture = TeapotGestureController.PracticeGesture.RightV,
            ImagePath = ImageFolder + "gesture_right_v.jpg"
        },
        new Step
        {
            Title = "LOCK",
            Instruction = "After you change the teapot, open your palm to rest.",
            Gesture = TeapotGestureController.PracticeGesture.OpenPalm,
            ImagePath = ImageFolder + "gesture_open_palm.jpg"
        }
    };

    private TeapotGestureController gestureController;
    private Action onFinished;
    private int stepIndex;
    private float recognizedHold;
    private bool stepRecognized;

    private GameObject root;
    private TMP_Text titleText;
    private TMP_Text instructionText;
    private TMP_Text statusText;
    private TMP_Text stepText;
    private RawImage gestureImage;
    private Button nextButton;

    public void Begin(TeapotGestureController controller, Action finished)
    {
        gestureController = controller;
        onFinished = finished;
        stepIndex = 0;
        if (gestureController != null)
            gestureController.CaptureRestState();
        EnsureUi();
        ShowStep();
        if (root != null)
            root.SetActive(true);
    }

    private void Update()
    {
        if (root == null || !root.activeSelf || gestureController == null)
            return;

        TeapotGestureController.PracticeGesture current =
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

        root = new GameObject("TeapotGestureTutorial", typeof(RectTransform));
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
        cardRect.sizeDelta = new Vector2(760f, 620f);
        frame.raycastTarget = true;

        Image fill = LanguagePageBuilder.CreateRoundedImage(
            "Fill", frame.transform, 32, new Color32(255, 239, 214, 255));
        LanguagePageBuilder.Stretch(fill.rectTransform, new Vector2(10f, 10f));
        fill.raycastTarget = false;

        stepText = CreateChip(cardRect, "Step", new Vector2(0f, 258f), new Vector2(280f, 40f), 18f);
        titleText = CreateLabel(cardRect, "Title", new Vector2(0f, 204f), 640f, 48f, 36);
        titleText.color = LanguagePageBuilder.Pink;
        Outline titleOutline = titleText.gameObject.AddComponent<Outline>();
        titleOutline.effectColor = LanguagePageBuilder.Green;
        titleOutline.effectDistance = new Vector2(3f, -3f);

        Image artFrame = LanguagePageBuilder.CreateRoundedImage(
            "ArtFrame", cardRect, 28, LanguagePageBuilder.Green);
        RectTransform artRect = artFrame.rectTransform;
        artRect.anchorMin = artRect.anchorMax = new Vector2(0.5f, 0.5f);
        artRect.pivot = new Vector2(0.5f, 0.5f);
        artRect.anchoredPosition = new Vector2(0f, 48f);
        artRect.sizeDelta = new Vector2(248f, 248f);
        artFrame.raycastTarget = false;
        Image artFill = LanguagePageBuilder.CreateRoundedImage(
            "ArtFill", artFrame.transform, 24, new Color32(253, 242, 250, 255));
        LanguagePageBuilder.Stretch(artFill.rectTransform, new Vector2(7f, 7f));
        artFill.raycastTarget = false;
        gestureImage = CreateRawImage(artFill.transform, "GestureImage", Vector2.zero, new Vector2(220f, 220f));
        Stretch(gestureImage.rectTransform);
        gestureImage.rectTransform.offsetMin = new Vector2(8f, 8f);
        gestureImage.rectTransform.offsetMax = new Vector2(-8f, -8f);

        instructionText = CreateLabel(cardRect, "Instruction", new Vector2(0f, -112f), 640f, 72f, 20);
        instructionText.enableWordWrapping = true;
        statusText = CreateLabel(cardRect, "Status", new Vector2(0f, -178f), 400f, 40f, 24);
        statusText.gameObject.SetActive(false);

        CreateMorphicButton(cardRect, "Skip", new Vector2(278f, 258f), new Vector2(150f, 48f),
            "SKIP", LanguagePageBuilder.Pink, Skip);
        CreateMorphicButton(cardRect, "Replay", new Vector2(-150f, -246f), new Vector2(200f, 56f),
            "REPLAY", LanguagePageBuilder.Pink, Replay);
        nextButton = CreateMorphicButton(
            cardRect, "Next", new Vector2(150f, -246f), new Vector2(200f, 56f),
            "NEXT", new Color32(236, 64, 168, 255), Next);
    }

    private void ShowStep()
    {
        stepRecognized = false;
        recognizedHold = 0f;
        Step step = Steps[stepIndex];
        stepText.text = "GESTURE " + (stepIndex + 1) + " OF " + Steps.Length;
        titleText.text = step.Title;
        instructionText.text = step.Instruction;
        if (gestureImage != null)
            gestureImage.texture = LoadTutorialTexture(step.ImagePath);
        if (statusText != null)
        {
            statusText.text = "";
            statusText.gameObject.SetActive(false);
        }
        nextButton.GetComponentInChildren<TMP_Text>().text =
            stepIndex == Steps.Length - 1 ? "START SCULPTING" : "NEXT";
    }

    private void MarkRecognized()
    {
        if (stepRecognized)
            return;
        stepRecognized = true;
        statusText.gameObject.SetActive(true);
        statusText.text = "RECOGNIZED";
        statusText.color = LanguagePageBuilder.Green;
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
        if (gestureController != null)
            gestureController.RestoreRestState();
        if (root != null)
            root.SetActive(false);
        Action done = onFinished;
        onFinished = null;
        done?.Invoke();
    }

    public void Hide()
    {
        if (root != null && root.activeSelf && gestureController != null)
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
#if UNITY_EDITOR
        return UnityEditor.AssetDatabase.LoadAssetAtPath<Texture2D>(path);
#else
        return null;
#endif
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
        text.fontSize = size;
        text.alignment = TextAlignmentOptions.Center;
        text.color = new Color32(52, 101, 29, 255);
        text.fontStyle = FontStyles.Bold;
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
        button.onClick.AddListener(() => onClick());
        TMP_Text text = CreateLabel(fill.transform, "Label", Vector2.zero, size.x - 12f, size.y, 20f);
        Stretch(text.rectTransform);
        text.text = label;
        return button;
    }
}
