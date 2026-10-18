using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// SculptingLab dialogue look: cream rounded bar, overlapping pink name badge,
/// thin pink choice pills. Sprites are Simple (not sliced) so the green stroke
/// stays a thin outline instead of filling the panel.
/// </summary>
public sealed class SculptingLabDialogueStyle : MonoBehaviour
{
    private static readonly Color PanelCream = new Color32(255, 245, 200, 255);
    private static readonly Color GardenGreen = new Color32(28, 68, 18, 255);
    private static readonly Color BadgePink = new Color32(252, 184, 241, 255);

    private const float PanelWidth = 900f;
    private const float PanelHeight = 270f;
    private const float PanelRadius = 58f;
    private const float PanelStroke = 15f;
    private const float BadgeWidth = 148f;
    private const float BadgeHeight = 70f;
    private const float PillHeight = 50f;

    private Sprite panelSprite;
    private Sprite badgeSprite;
    private Sprite pillSprite;

    private void OnEnable()
    {
        ApplyStyle();
    }

    private void Start()
    {
        ApplyStyle();
    }

    private void OnDestroy()
    {
        DestroySprite(ref panelSprite);
        DestroySprite(ref badgeSprite);
        DestroySprite(ref pillSprite);
    }

    private void ApplyStyle()
    {
        DestroySprite(ref panelSprite);
        DestroySprite(ref badgeSprite);
        DestroySprite(ref pillSprite);
        panelSprite = CreateRoundedSprite(
            PanelCream, GardenGreen, PanelStroke, PanelRadius, (int)PanelWidth, (int)PanelHeight);
        badgeSprite = CreateRoundedSprite(
            BadgePink, GardenGreen, 8f, BadgeHeight * 0.5f, (int)BadgeWidth, (int)BadgeHeight);
        pillSprite = CreateRoundedSprite(
            BadgePink, GardenGreen, 10f, PillHeight * 0.5f, 512, (int)PillHeight);

        RectTransform panelRect = transform as RectTransform;
        if (panelRect != null)
        {
            panelRect.anchorMin = new Vector2(0.5f, 0f);
            panelRect.anchorMax = new Vector2(0.5f, 0f);
            panelRect.pivot = new Vector2(0.5f, 0f);
            panelRect.anchoredPosition = new Vector2(0f, 28f);
            panelRect.sizeDelta = new Vector2(PanelWidth, PanelHeight);
        }

        Image panelImage = GetComponent<Image>();
        if (panelImage != null)
        {
            panelImage.sprite = panelSprite;
            panelImage.type = Image.Type.Simple;
            panelImage.preserveAspect = false;
            panelImage.color = Color.white;
            panelImage.raycastTarget = true;
        }

        Outline panelOutline = GetComponent<Outline>();
        if (panelOutline != null)
            panelOutline.enabled = false;

        StyleSpeakerBadge();
        StyleChoicesPanel();
        StyleDialogueText();
    }

    private void StyleSpeakerBadge()
    {
        Transform badge = transform.Find("DialogueSpeakerBackground");
        if (badge == null)
        {
            GameObject badgeObject = new GameObject(
                "DialogueSpeakerBackground",
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(Image));
            badgeObject.layer = gameObject.layer;
            badgeObject.transform.SetParent(transform, false);
            badge = badgeObject.transform;
        }

        RectTransform badgeRect = badge as RectTransform;
        badgeRect.anchorMin = new Vector2(0f, 1f);
        badgeRect.anchorMax = new Vector2(0f, 1f);
        badgeRect.pivot = new Vector2(0f, 0.5f);
        badgeRect.anchoredPosition = new Vector2(58f, 2f);
        badgeRect.sizeDelta = new Vector2(BadgeWidth, BadgeHeight);

        Image badgeImage = badge.GetComponent<Image>();
        badgeImage.sprite = badgeSprite;
        badgeImage.type = Image.Type.Simple;
        badgeImage.preserveAspect = false;
        badgeImage.color = Color.white;
        badgeImage.raycastTarget = false;

        Transform speaker = FindNamedChild("Speaker Name");
        if (speaker == null)
            return;

        speaker.SetParent(transform, true);

        RectTransform speakerRect = speaker as RectTransform;
        speakerRect.anchorMin = new Vector2(0f, 1f);
        speakerRect.anchorMax = new Vector2(0f, 1f);
        speakerRect.pivot = new Vector2(0f, 0.5f);
        speakerRect.anchoredPosition = new Vector2(58f, 2f);
        speakerRect.sizeDelta = new Vector2(BadgeWidth, BadgeHeight);

        badge.SetAsLastSibling();
        speaker.SetAsLastSibling();

        TMP_Text speakerText = speaker.GetComponent<TMP_Text>();
        if (speakerText == null)
            return;

        speakerText.color = GardenGreen;
        speakerText.fontStyle = FontStyles.Bold;
        speakerText.alignment = TextAlignmentOptions.Center;
        speakerText.enableAutoSizing = true;
        speakerText.fontSizeMin = 18;
        speakerText.fontSizeMax = 26;
        speakerText.characterSpacing = 1.5f;
        speakerText.textWrappingMode = TextWrappingModes.NoWrap;
        speakerText.margin = Vector4.zero;
        speakerText.raycastTarget = false;
        if (!string.IsNullOrEmpty(speakerText.text))
            speakerText.text = speakerText.text.Trim().ToUpperInvariant();
    }

    private void StyleChoicesPanel()
    {
        Transform choices = transform.Find("Choices Panel");
        if (choices == null)
            return;

        RectTransform choicesRect = choices as RectTransform;
        choicesRect.anchorMin = new Vector2(0f, 0f);
        choicesRect.anchorMax = new Vector2(1f, 0f);
        choicesRect.pivot = new Vector2(0.5f, 0f);
        choicesRect.anchoredPosition = new Vector2(0f, 28f);
        choicesRect.sizeDelta = new Vector2(-48f, PillHeight);

        Image choicesImage = choices.GetComponent<Image>();
        if (choicesImage != null)
        {
            choicesImage.sprite = null;
            choicesImage.color = Color.clear;
            choicesImage.raycastTarget = false;
        }

        HorizontalLayoutGroup layout = choices.GetComponent<HorizontalLayoutGroup>();
        if (layout != null)
        {
            layout.padding = new RectOffset(0, 0, 0, 0);
            layout.spacing = 24f;
            layout.childAlignment = TextAnchor.MiddleCenter;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
        }

        Button[] buttons = choices.GetComponentsInChildren<Button>(true);
        foreach (Button choice in buttons)
            StyleChoice(choice);
    }

    private void StyleDialogueText()
    {
        Transform dialogue = transform.Find("Dialogue Text");
        if (dialogue == null)
            return;

        RectTransform dialogueRect = dialogue as RectTransform;
        dialogueRect.anchorMin = new Vector2(0f, 0f);
        dialogueRect.anchorMax = new Vector2(1f, 1f);
        dialogueRect.offsetMin = new Vector2(48f, 128f);
        dialogueRect.offsetMax = new Vector2(-48f, -46f);

        TMP_Text dialogueText = dialogue.GetComponent<TMP_Text>();
        if (dialogueText == null)
            return;

        dialogueText.color = GardenGreen;
        dialogueText.fontStyle = FontStyles.Bold;
        dialogueText.enableAutoSizing = false;
        dialogueText.fontSize = 28;
        dialogueText.lineSpacing = -8f;
        dialogueText.textWrappingMode = TextWrappingModes.Normal;
        dialogueText.alignment = TextAlignmentOptions.TopLeft;
        dialogueText.raycastTarget = false;
    }

    private void StyleChoice(Button choice)
    {
        Image image = choice.GetComponent<Image>();
        if (image != null)
        {
            image.sprite = pillSprite;
            image.type = Image.Type.Simple;
            image.preserveAspect = false;
            image.color = Color.white;
        }

        Outline outline = choice.GetComponent<Outline>();
        if (outline != null)
            outline.enabled = false;

        LayoutElement layoutElement = choice.GetComponent<LayoutElement>();
        if (layoutElement == null)
            layoutElement = choice.gameObject.AddComponent<LayoutElement>();
        layoutElement.minHeight = PillHeight;
        layoutElement.preferredHeight = PillHeight;
        layoutElement.flexibleHeight = 0f;
        layoutElement.flexibleWidth = 1f;

        ColorBlock colors = choice.colors;
        colors.normalColor = Color.white;
        colors.highlightedColor = new Color32(255, 220, 248, 255);
        colors.pressedColor = new Color32(230, 150, 220, 255);
        colors.selectedColor = Color.white;
        colors.disabledColor = new Color32(220, 190, 214, 180);
        colors.fadeDuration = 0.12f;
        choice.colors = colors;

        TMP_Text label = choice.GetComponentInChildren<TMP_Text>(true);
        if (label == null)
            return;

        label.color = GardenGreen;
        label.fontStyle = FontStyles.Bold;
        label.enableAutoSizing = false;
        label.fontSize = 32;
        label.characterSpacing = 0.4f;
        label.margin = new Vector4(20f, 0f, 20f, 0f);
        label.textWrappingMode = TextWrappingModes.NoWrap;
        label.overflowMode = TextOverflowModes.Ellipsis;
        label.alignment = TextAlignmentOptions.Center;
        label.raycastTarget = false;
    }

    private Transform FindNamedChild(string childName)
    {
        Transform direct = transform.Find(childName);
        if (direct != null)
            return direct;

        Transform[] children = GetComponentsInChildren<Transform>(true);
        for (int i = 0; i < children.Length; i++)
        {
            if (children[i].name == childName)
                return children[i];
        }

        return null;
    }

    private static Sprite CreateRoundedSprite(
        Color fill,
        Color border,
        float borderThickness,
        float radius,
        int width,
        int height)
    {
        var texture = new Texture2D(width, height, TextureFormat.RGBA32, false)
        {
            name = "SculptingLab Dialogue UI",
            filterMode = FilterMode.Bilinear,
            wrapMode = TextureWrapMode.Clamp,
            hideFlags = HideFlags.DontSave
        };

        Color32[] pixels = new Color32[width * height];
        float maxRadius = Mathf.Min(width, height) * 0.5f - 0.5f;
        radius = Mathf.Min(radius, maxRadius);
        Color32 fill32 = fill;
        Color32 border32 = border;
        Color32 clear = new Color32(0, 0, 0, 0);

        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                float dx = Mathf.Abs(x + 0.5f - width * 0.5f) - (width * 0.5f - radius);
                float dy = Mathf.Abs(y + 0.5f - height * 0.5f) - (height * 0.5f - radius);
                float outside = Mathf.Sqrt(
                    Mathf.Max(dx, 0f) * Mathf.Max(dx, 0f) +
                    Mathf.Max(dy, 0f) * Mathf.Max(dy, 0f));
                float distance = outside + Mathf.Min(Mathf.Max(dx, dy), 0f) - radius;

                if (distance > 1f)
                {
                    pixels[y * width + x] = clear;
                    continue;
                }

                Color32 chosen = distance > -borderThickness ? border32 : fill32;
                float alpha = 1f - Mathf.Clamp01(distance + 0.5f);
                chosen.a = (byte)Mathf.RoundToInt(chosen.a * alpha);
                pixels[y * width + x] = chosen;
            }
        }

        texture.SetPixels32(pixels);
        texture.Apply(false, false);
        return Sprite.Create(
            texture,
            new Rect(0, 0, width, height),
            new Vector2(0.5f, 0.5f),
            100f,
            0,
            SpriteMeshType.FullRect);
    }

    private static void DestroySprite(ref Sprite sprite)
    {
        if (sprite == null)
            return;
        Texture2D texture = sprite.texture;
        Destroy(sprite);
        if (texture != null)
            Destroy(texture);
        sprite = null;
    }
}
