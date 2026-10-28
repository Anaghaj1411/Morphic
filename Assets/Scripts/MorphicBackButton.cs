using UnityEngine;
using UnityEngine.UI;

/// Shared pink/green back icon used by Language, Settings, and lantern UI.
public static class MorphicBackButton
{
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
}
