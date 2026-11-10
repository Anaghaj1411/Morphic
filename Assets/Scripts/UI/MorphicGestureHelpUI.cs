using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// Pink/green pill that reopens the current craft's gesture cards.
/// Hidden until the user picks lantern, teapot, or keris.
public class MorphicGestureHelpUI : MonoBehaviour
{
    private GameObject root;
    private Button helpButton;

    public void SetVisible(bool visible)
    {
        EnsureUi();
        if (root == null)
            return;
        root.SetActive(visible);
        if (visible)
            root.transform.SetAsLastSibling();
    }

    private void Awake()
    {
        EnsureUi();
        SetVisible(false);
    }

    private void EnsureUi()
    {
        if (root != null)
        {
            WireButton();
            return;
        }

        Canvas canvas = FindFirstObjectByType<Canvas>();
        if (canvas == null)
            return;

        Transform existing = canvas.transform.Find("MorphicGestureHelp");
        if (existing != null)
        {
            root = existing.gameObject;
            helpButton = existing.GetComponentInChildren<Button>(true);
            Transform existingFrame = existing.Find("Gestures");
            if (existingFrame != null)
                PlaceTopLeft(existingFrame.GetComponent<RectTransform>());
            WireButton();
            return;
        }

        root = new GameObject("MorphicGestureHelp", typeof(RectTransform));
        RectTransform rootRect = root.GetComponent<RectTransform>();
        rootRect.SetParent(canvas.transform, false);
        StretchFull(rootRect);

        Image frame = LanguagePageBuilder.CreateRoundedImage(
            "Gestures", root.transform, 28, LanguagePageBuilder.Green);
        PlaceTopLeft(frame.rectTransform);
        frame.raycastTarget = true;

        Image fill = LanguagePageBuilder.CreateRoundedImage(
            "Fill", frame.transform, 24, LanguagePageBuilder.Pink);
        RectTransform fillRect = fill.rectTransform;
        fillRect.anchorMin = Vector2.zero;
        fillRect.anchorMax = Vector2.one;
        fillRect.offsetMin = new Vector2(5f, 5f);
        fillRect.offsetMax = new Vector2(-5f, -5f);
        fill.raycastTarget = true;

        LanguagePageBuilder.TextLabel label = LanguagePageBuilder.CreateLabel(
            "Label",
            fill.transform,
            "GESTURES",
            18);
        LanguagePageBuilder.Stretch(label.Rect);
        if (label.Label != null)
        {
            label.Label.fontStyle = FontStyles.Bold;
            label.Label.characterSpacing = 0.8f;
        }

        helpButton = frame.gameObject.AddComponent<Button>();
        helpButton.targetGraphic = fill;
        helpButton.colors = LanguagePageBuilder.ButtonColors();
        WireButton();
    }

    private void WireButton()
    {
        if (helpButton == null)
            return;
        helpButton.onClick.RemoveAllListeners();
        helpButton.onClick.AddListener(OpenGestureCards);
    }

    private static void OpenGestureCards()
    {
        DialogueManager dialogue = FindFirstObjectByType<DialogueManager>(FindObjectsInactive.Include);
        if (dialogue != null)
            dialogue.OpenGestureCards();
    }

    private static void PlaceTopLeft(RectTransform button)
    {
        if (button == null)
            return;

        // Under RESET ROTATION, still in the top-left cluster beside BACK.
        button.anchorMin = button.anchorMax = new Vector2(0f, 1f);
        button.pivot = new Vector2(0f, 1f);
        button.anchoredPosition = new Vector2(122f, -90f);
        button.sizeDelta = new Vector2(236f, 54f);
    }

    private static void StretchFull(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
    }
}
