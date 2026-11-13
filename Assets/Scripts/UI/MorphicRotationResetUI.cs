using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// Pink/green pill that restores lantern, teapot, or keris to the pose
/// captured before the user started fist rotation. Sculpt, paint, and
/// stamp work is left alone. Hidden until a craft is picked.
public class MorphicRotationResetUI : MonoBehaviour
{
    private GameObject root;
    private Button resetButton;

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

        Transform existing = canvas.transform.Find("MorphicRotationReset");
        if (existing != null)
        {
            root = existing.gameObject;
            resetButton = existing.GetComponentInChildren<Button>(true);
            Transform existingFrame = existing.Find("ResetRotation");
            if (existingFrame != null)
                PlaceBesideHelp(existingFrame.GetComponent<RectTransform>());
            WireButton();
            return;
        }

        root = new GameObject("MorphicRotationReset", typeof(RectTransform));
        RectTransform rootRect = root.GetComponent<RectTransform>();
        rootRect.SetParent(canvas.transform, false);
        StretchFull(rootRect);

        Image frame = LanguagePageBuilder.CreateRoundedImage(
            "ResetRotation", root.transform, 28, LanguagePageBuilder.Green);
        PlaceBesideHelp(frame.rectTransform);
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
            "RESET ROTATION",
            18);
        LanguagePageBuilder.Stretch(label.Rect);
        if (label.Label != null)
        {
            label.Label.characterSpacing = 0.8f;
            label.Label.enableAutoSizing = true;
            label.Label.fontSizeMin = 14f;
            label.Label.fontSizeMax = 18f;
        }

        resetButton = frame.gameObject.AddComponent<Button>();
        resetButton.targetGraphic = fill;
        resetButton.colors = LanguagePageBuilder.ButtonColors();
        WireButton();
    }

    private void WireButton()
    {
        if (resetButton == null)
            return;
        resetButton.onClick.RemoveAllListeners();
        resetButton.onClick.AddListener(ResetActiveCraft);
    }

    public static void ResetActiveCraft()
    {
        ResetActive(FindFirstObjectByType<LanternFistRotationController>(
            FindObjectsInactive.Exclude));
        ResetActive(FindFirstObjectByType<TeapotFistRotationController>(
            FindObjectsInactive.Exclude));
        ResetActive(FindFirstObjectByType<KerisFistRotationController>(
            FindObjectsInactive.Exclude));
    }

    private static void ResetActive(LanternFistRotationController rotator)
    {
        if (rotator != null && rotator.isActiveAndEnabled)
            rotator.ResetToRestPose();
    }

    private static void ResetActive(TeapotFistRotationController rotator)
    {
        if (rotator != null && rotator.isActiveAndEnabled)
            rotator.ResetToRestPose();
    }

    private static void ResetActive(KerisFistRotationController rotator)
    {
        if (rotator != null && rotator.isActiveAndEnabled)
            rotator.ResetToRestPose();
    }

    private static void PlaceBesideHelp(RectTransform button)
    {
        if (button == null)
            return;

        // Top-right, immediately left of the HELP pill (-24, -24, 132x54).
        button.anchorMin = button.anchorMax = new Vector2(1f, 1f);
        button.pivot = new Vector2(1f, 1f);
        button.anchoredPosition = new Vector2(-166f, -24f);
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
