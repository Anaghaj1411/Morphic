using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// Pink/green EXIT pill used on the language page and craft-select screen.
public static class MorphicExitButton
{
    public static GameObject Create(Transform parent)
    {
        Image frame = LanguagePageBuilder.CreateRoundedImage(
            "MorphicExitButton",
            parent,
            28,
            LanguagePageBuilder.Green);
        PlaceTopRight(frame.rectTransform);
        frame.raycastTarget = true;

        Image fill = LanguagePageBuilder.CreateRoundedImage(
            "Fill",
            frame.transform,
            24,
            LanguagePageBuilder.Pink);
        LanguagePageBuilder.Stretch(fill.rectTransform, new Vector2(5f, 5f));
        fill.raycastTarget = true;

        LanguagePageBuilder.TextLabel label = LanguagePageBuilder.CreateLabel(
            "Label",
            fill.transform,
            "EXIT",
            22);
        LanguagePageBuilder.Stretch(label.Rect);
        if (label.Label != null)
        {
            label.Label.fontStyle = FontStyles.Bold;
            label.Label.characterSpacing = 1.5f;
        }

        Button button = frame.gameObject.AddComponent<Button>();
        button.targetGraphic = fill;
        button.colors = LanguagePageBuilder.ButtonColors();
        button.onClick.AddListener(Quit);
        return frame.gameObject;
    }

    public static void PlaceTopRight(RectTransform button)
    {
        if (button == null)
            return;

        button.anchorMin = button.anchorMax = new Vector2(1f, 1f);
        button.pivot = new Vector2(1f, 1f);
        button.anchoredPosition = new Vector2(-24f, -24f);
        button.sizeDelta = new Vector2(150f, 54f);
    }

    public static void Quit()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }
}
