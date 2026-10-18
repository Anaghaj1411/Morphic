using System;
using UnityEngine;
using UnityEngine.UI;

/// Shared pink/green back icon used by Language, Settings, and lantern UI.
public static class MorphicBackButton
{
    public const string SpritePath =
        "Assets/Art/Environments/MainMenu/Texture/back.png";

    private static Sprite cachedSprite;

    public static Sprite LoadSprite()
    {
        if (cachedSprite != null)
            return cachedSprite;

        Sprite sprite = null;
#if UNITY_EDITOR
        sprite = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>(SpritePath);
        if (sprite == null)
        {
            Texture2D texture =
                UnityEditor.AssetDatabase.LoadAssetAtPath<Texture2D>(SpritePath);
            if (texture != null)
            {
                sprite = Sprite.Create(
                    texture,
                    new Rect(0f, 0f, texture.width, texture.height),
                    new Vector2(0.5f, 0.5f),
                    100f);
            }
        }
#endif
        cachedSprite = sprite;
        // #region agent log
        AgentLog(
            "A",
            "MorphicBackButton.LoadSprite",
            "Loaded back sprite",
            "{\"found\":" + (sprite != null ? "true" : "false") + "}");
        // #endregion
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

    // #region agent log
    public static void AgentLog(
        string hypothesisId,
        string location,
        string message,
        string dataJson)
    {
        try
        {
            long timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            string path = System.IO.Path.Combine(
                System.IO.Directory.GetParent(Application.dataPath).FullName,
                "debug-9395c5.log");
            System.IO.File.AppendAllText(
                path,
                "{\"sessionId\":\"9395c5\",\"hypothesisId\":\"" + hypothesisId +
                "\",\"location\":\"" + location +
                "\",\"message\":\"" + message +
                "\",\"data\":" + dataJson +
                ",\"timestamp\":" + timestamp + "}\n");
        }
        catch
        {
        }
    }
    // #endregion
}
