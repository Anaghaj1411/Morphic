using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Short practice screens shown after Taiwan sculpting starts and
// before the color palette. Skip jumps straight to sculpting.
public sealed class SculptingGestureTutorial : MonoBehaviour
{
    private struct Step
    {
        public string Title;
        public string Instruction;
        public TaiwanLanternGestureController.PracticeGesture Gesture;
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
            Gesture = TaiwanLanternGestureController.PracticeGesture.TwoIndex,
            ImagePath = ImageFolder + "gesture_two_index.jpg"
        },
        new Step
        {
            Title = "TOP LID",
            Instruction = "Pinch with your right hand, then lift.",
            Gesture = TaiwanLanternGestureController.PracticeGesture.RightPinch,
            ImagePath = ImageFolder + "gesture_right_pinch.jpg"
        },
        new Step
        {
            Title = "BOTTOM LID",
            Instruction = "Pinch with your left hand, then lift.",
            Gesture = TaiwanLanternGestureController.PracticeGesture.LeftPinch,
            ImagePath = ImageFolder + "gesture_left_pinch.jpg"
        },
        new Step
        {
            Title = "TASSEL",
            Instruction = "Make two fists and pull them down.",
            Gesture = TaiwanLanternGestureController.PracticeGesture.TwoFists,
            ImagePath = ImageFolder + "gesture_two_fists.jpg"
        },
        new Step
        {
            Title = "BAMBOO",
            Instruction = "Make a V with both hands and spread them to the sides.",
            Gesture = TaiwanLanternGestureController.PracticeGesture.TwoV,
            ImagePath = ImageFolder + "gesture_two_v.jpg"
        },
        new Step
        {
            Title = "ROTATE",
            Instruction = "Make one fist and move it left or right.",
            Gesture = TaiwanLanternGestureController.PracticeGesture.SingleFist,
            ImagePath = ImageFolder + "gesture_one_fist.jpg"
        },
        new Step
        {
            Title = "TOPPER",
            Instruction = "Make a V with your right hand and raise or lower it.",
            Gesture = TaiwanLanternGestureController.PracticeGesture.RightV,
            ImagePath = ImageFolder + "gesture_right_v.jpg"
        },
        new Step
        {
            Title = "BOTTOM",
            Instruction = "Make a V with your left hand and raise or lower it.",
            Gesture = TaiwanLanternGestureController.PracticeGesture.LeftV,
            ImagePath = ImageFolder + "gesture_left_v.jpg"
        },
        new Step
        {
            Title = "LOCK",
            Instruction = "After you change the lantern, open your palm to lock the shape.",
            Gesture = TaiwanLanternGestureController.PracticeGesture.OpenPalm,
            ImagePath = ImageFolder + "gesture_open_palm.jpg"
        }
    };

    private TaiwanLanternGestureController gestureController;
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

    public void Begin(
        TaiwanLanternGestureController controller,
        Action finished)
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

        TaiwanLanternGestureController.PracticeGesture current =
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
            statusText.text = "TRY IT";
            statusText.color = new Color32(52, 101, 29, 255);
        }
    }

    private void EnsureUi()
    {
        if (root != null)
            return;

        Canvas canvas = FindFirstObjectByType<Canvas>();
        if (canvas == null)
            return;

        root = new GameObject("GestureTutorial", typeof(RectTransform));
        RectTransform rootRect = root.GetComponent<RectTransform>();
        rootRect.SetParent(canvas.transform, false);
        Stretch(rootRect);

        Image dim = CreateImage(rootRect, "Dim", new Color(0f, 0f, 0f, 0.5f));
        Stretch(dim.rectTransform);

        GameObject card = new GameObject("Card", typeof(RectTransform), typeof(Image));
        card.transform.SetParent(rootRect, false);
        RectTransform cardRect = card.GetComponent<RectTransform>();
        cardRect.anchorMin = cardRect.anchorMax = new Vector2(0.5f, 0.5f);
        cardRect.pivot = new Vector2(0.5f, 0.5f);
        cardRect.sizeDelta = new Vector2(740f, 560f);
        card.GetComponent<Image>().color = new Color32(253, 242, 250, 255);

        stepText = CreateLabel(cardRect, "Step", new Vector2(0f, 242f), 280f, 28f, 18);
        titleText = CreateLabel(cardRect, "Title", new Vector2(0f, 196f), 640f, 44f, 34);
        gestureImage = CreateRawImage(cardRect, "GestureImage", new Vector2(0f, 58f), new Vector2(220f, 220f));
        instructionText = CreateLabel(cardRect, "Instruction", new Vector2(0f, -82f), 640f, 70f, 20);
        statusText = CreateLabel(cardRect, "Status", new Vector2(0f, -148f), 400f, 40f, 26);

        CreateTextButton(cardRect, "Skip", new Vector2(276f, 242f), new Vector2(140f, 44f),
            "SKIP", Skip);
        CreateTextButton(cardRect, "Replay", new Vector2(-150f, -218f), new Vector2(180f, 52f),
            "REPLAY", Replay);
        nextButton = CreateTextButton(
            cardRect, "Next", new Vector2(150f, -218f), new Vector2(180f, 52f),
            "NEXT", Next);
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
        statusText.text = "TRY IT";
        statusText.color = new Color32(52, 101, 29, 255);
        nextButton.GetComponentInChildren<TMP_Text>().text =
            stepIndex == Steps.Length - 1 ? "START SCULPTING" : "NEXT";
    }

    private void MarkRecognized()
    {
        if (stepRecognized)
            return;
        stepRecognized = true;
        statusText.text = "RECOGNIZED";
        statusText.color = new Color32(52, 101, 29, 255);
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

    private static Button CreateTextButton(
        Transform parent,
        string name,
        Vector2 position,
        Vector2 size,
        string label,
        Action onClick)
    {
        GameObject go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
        go.transform.SetParent(parent, false);
        RectTransform rect = go.GetComponent<RectTransform>();
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
        Image image = go.GetComponent<Image>();
        image.color = new Color32(252, 184, 241, 255);
        Button button = go.GetComponent<Button>();
        button.onClick.AddListener(() => onClick());
        TMP_Text text = CreateLabel(go.transform, "Label", Vector2.zero, size.x - 12f, size.y, 20f);
        Stretch(text.rectTransform);
        text.text = label;
        return button;
    }
}
