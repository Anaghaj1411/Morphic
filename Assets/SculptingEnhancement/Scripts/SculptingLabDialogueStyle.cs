using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Applies the SculptingLab dialogue look without changing the shared dialogue system.
/// This component is attached only to the Dialogue Panel in SculptingLab.
/// </summary>
public sealed class SculptingLabDialogueStyle : MonoBehaviour
{
    private static readonly Color PanelCream = new Color32(255, 245, 199, 255);
    private static readonly Color GardenGreen = new Color32(48, 101, 35, 255);
    private static readonly Color BadgePink = new Color32(244, 174, 231, 255);
    private bool hasAppliedStyle;

    private void OnEnable()
    {
        if (hasAppliedStyle)
            return;

        hasAppliedStyle = true;
        ApplyStyle();
    }

    private void ApplyStyle()
    {
        RectTransform panelRect = transform as RectTransform;
        if (panelRect != null)
        {
            Vector2 panelSize = panelRect.sizeDelta;
            panelSize.y = 250;
            panelRect.sizeDelta = panelSize;
        }

        Image panelImage = GetComponent<Image>();
        if (panelImage != null)
        {
            panelImage.sprite = CreateRoundedSprite(PanelCream, GardenGreen, 10);
            panelImage.type = Image.Type.Sliced;
            panelImage.color = Color.white;
        }

        Outline panelOutline = GetComponent<Outline>();
        if (panelOutline != null)
            panelOutline.enabled = false;

        Transform speaker = transform.Find("Speaker Name");
        if (speaker != null)
            StyleSpeaker(speaker);

        Button[] choices = GetComponentsInChildren<Button>(true);
        foreach (Button choice in choices)
            StyleChoice(choice);

        TMP_Text dialogueText = transform.Find("Dialogue Text")?.GetComponent<TMP_Text>();
        if (dialogueText != null)
        {
            RectTransform dialogueRect = dialogueText.rectTransform;
            dialogueRect.anchorMin = new Vector2(0, 1);
            dialogueRect.anchorMax = new Vector2(1, 1);
            dialogueRect.pivot = new Vector2(0.5f, 1);
            dialogueRect.anchoredPosition = new Vector2(0, -88);
            dialogueRect.sizeDelta = new Vector2(-70, 68);
            dialogueText.color = GardenGreen;
            dialogueText.fontSize = 26;
            dialogueText.enableAutoSizing = true;
            dialogueText.fontSizeMin = 20;
            dialogueText.fontSizeMax = 26;
            dialogueText.alignment = TextAlignmentOptions.TopLeft;
            dialogueText.raycastTarget = false;
        }
    }

    private void StyleSpeaker(Transform speaker)
    {
        RectTransform speakerRect = speaker as RectTransform;
        if (speakerRect != null)
        {
            speakerRect.sizeDelta = new Vector2(250, 58);
            speakerRect.anchoredPosition = new Vector2(35, -18);
        }

        Transform background = transform.Find("DialogueSpeakerBackground");
        if (background == null)
        {
            GameObject backgroundObject = new GameObject("DialogueSpeakerBackground", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            backgroundObject.layer = gameObject.layer;
            backgroundObject.transform.SetParent(transform, false);
            backgroundObject.transform.SetAsFirstSibling();
            background = backgroundObject.transform;
        }

        RectTransform backgroundRect = background as RectTransform;
        if (backgroundRect != null)
        {
            backgroundRect.anchorMin = new Vector2(0, 1);
            backgroundRect.anchorMax = new Vector2(0, 1);
            backgroundRect.pivot = new Vector2(0, 1);
            backgroundRect.anchoredPosition = new Vector2(35, -18);
            backgroundRect.sizeDelta = new Vector2(250, 58);
        }

        Image badgeImage = background.GetComponent<Image>();
        badgeImage.sprite = CreateRoundedSprite(BadgePink, GardenGreen, 10);
        badgeImage.type = Image.Type.Sliced;
        badgeImage.color = Color.white;
        badgeImage.raycastTarget = false;

        TMP_Text speakerText = speaker.GetComponent<TMP_Text>();
        if (speakerText != null)
        {
            speakerText.color = GardenGreen;
            speakerText.fontSize = 30;
            speakerText.fontStyle = FontStyles.Bold;
            speakerText.alignment = TextAlignmentOptions.Center;
            speakerText.raycastTarget = false;
        }
    }

    private void StyleChoice(Button choice)
    {
        Image image = choice.GetComponent<Image>();
        if (image != null)
        {
            image.sprite = CreateRoundedSprite(BadgePink, GardenGreen, 8);
            image.type = Image.Type.Sliced;
            image.color = Color.white;
        }

        Outline outline = choice.GetComponent<Outline>();
        if (outline != null)
            outline.enabled = false;

        ColorBlock colors = choice.colors;
        colors.normalColor = Color.white;
        colors.highlightedColor = new Color32(255, 226, 248, 255);
        colors.pressedColor = new Color32(227, 139, 210, 255);
        colors.selectedColor = Color.white;
        colors.disabledColor = new Color32(220, 190, 214, 180);
        colors.fadeDuration = 0.12f;
        choice.colors = colors;

        TMP_Text label = choice.GetComponentInChildren<TMP_Text>(true);
        if (label != null)
        {
            label.color = GardenGreen;
            label.fontStyle = FontStyles.Bold;
            label.fontSize = 22;
            label.enableAutoSizing = true;
            label.fontSizeMin = 16;
            label.fontSizeMax = 22;
            label.margin = new Vector4(8, 2, 8, 2);
            label.raycastTarget = false;
        }
    }

    private static Sprite CreateRoundedSprite(Color fill, Color border, float borderThickness)
    {
        const int width = 256;
        const int height = 128;
        const float radius = 42f;
        var texture = new Texture2D(width, height, TextureFormat.RGBA32, false)
        {
            name = "SculptingLab Dialogue Rounded UI",
            filterMode = FilterMode.Bilinear,
            wrapMode = TextureWrapMode.Clamp,
            hideFlags = HideFlags.DontSave
        };

        Color32[] pixels = new Color32[width * height];
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                float dx = Mathf.Abs(x + 0.5f - width * 0.5f) - (width * 0.5f - radius);
                float dy = Mathf.Abs(y + 0.5f - height * 0.5f) - (height * 0.5f - radius);
                float outside = Mathf.Sqrt(Mathf.Max(dx, 0) * Mathf.Max(dx, 0) + Mathf.Max(dy, 0) * Mathf.Max(dy, 0));
                float distance = outside + Mathf.Min(Mathf.Max(dx, dy), 0) - radius;

                if (distance > 0.75f)
                {
                    pixels[y * width + x] = new Color32(255, 255, 255, 0);
                    continue;
                }

                Color chosen = distance > -borderThickness ? border : fill;
                byte alpha = (byte)Mathf.RoundToInt(255f * (1f - Mathf.Clamp01(distance + 0.5f)));
                Color32 color = chosen;
                color.a = alpha;
                pixels[y * width + x] = color;
            }
        }

        texture.SetPixels32(pixels);
        texture.Apply(false, true);
        return Sprite.Create(texture, new Rect(0, 0, width, height), new Vector2(0.5f, 0.5f), 100f,
            0, SpriteMeshType.FullRect, new Vector4(radius, radius, radius, radius));
    }
}
