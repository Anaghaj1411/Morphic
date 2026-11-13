using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// Shared pink/green back icon used by Language, Settings, and lantern UI.
public static class MorphicBackButton
{
    public const float HeaderSize = 58f;
    private static Sprite cachedSprite;

    public static Sprite LoadSprite()
    {
        if (cachedSprite != null)
            return cachedSprite;

        Sprite sprite = Resources.Load<Sprite>("MorphicBackButton");
        cachedSprite = sprite;
        return sprite;
    }

    public static void PlaceOnPanel(RectTransform button, float size)
    {
        if (button == null)
            return;

        button.anchorMin = button.anchorMax = new Vector2(1f, 1f);
        button.pivot = new Vector2(1f, 1f);
        button.anchoredPosition = new Vector2(-18f, -18f);
        button.sizeDelta = new Vector2(size, size);
    }

    public static void PlaceOnPanelLeft(RectTransform button, float size)
    {
        if (button == null)
            return;

        button.anchorMin = button.anchorMax = new Vector2(0f, 1f);
        button.pivot = new Vector2(0f, 1f);
        button.anchoredPosition = new Vector2(18f, -18f);
        button.sizeDelta = new Vector2(size, size);
    }

    public static void ApplyIcon(Image image)
    {
        if (image == null)
            return;

        Sprite sprite = LoadSprite();
        if (sprite == null)
            return;

        image.sprite = sprite;
        image.color = Color.white;
        image.preserveAspect = true;
        image.type = Image.Type.Simple;
        image.raycastTarget = true;
    }

    public static Button CreateHeader(Transform parent, UnityEngine.Events.UnityAction click)
    {
        GameObject go = new GameObject(
            "HeaderBack",
            typeof(RectTransform),
            typeof(CanvasRenderer),
            typeof(Image),
            typeof(Button));
        RectTransform rect = go.GetComponent<RectTransform>();
        rect.SetParent(parent, false);
        PlaceOnPanelLeft(rect, HeaderSize);

        Image image = go.GetComponent<Image>();
        ApplyIcon(image);

        Button button = go.GetComponent<Button>();
        button.targetGraphic = image;
        button.transition = Selectable.Transition.ColorTint;
        if (click != null)
            button.onClick.AddListener(click);
        return button;
    }

    public static void RestyleExisting(Button button)
    {
        if (button == null)
            return;

        for (int i = 0; i < button.transform.childCount; i++)
            button.transform.GetChild(i).gameObject.SetActive(false);

        RectTransform rect = button.GetComponent<RectTransform>();
        PlaceOnPanelLeft(rect, HeaderSize);

        Image image = button.targetGraphic as Image;
        if (image == null)
            image = button.GetComponent<Image>();
        ApplyIcon(image);
        button.targetGraphic = image;
    }
}
