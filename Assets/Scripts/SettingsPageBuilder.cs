using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Builds the Settings panel in code, matching the Language Page
/// theme: green-outlined card, pink pill title, pink rounded rows,
/// circular back button, dimmed backdrop.
///
/// Layout follows the reference structure:
///   SETTINGS
///   Music        [ - ] [ Music Volume ] [ + ]
///   Sound Effects[ - ] [ SFX Volume    ] [ + ]
///   BACK
/// </summary>
public static class SettingsPageBuilder
{
    private const float RowWidth = 540f;
    private const float RowHeight = 92f;
    private const float RowGap = 22f;

    public static void Build(
        RectTransform root,
        SettingsPageController controller)
    {
        if (root == null ||
            controller == null)
        {
            return;
        }

        // Clear any previous build so the panel is never duplicated.
        for (int i = root.childCount - 1; i >= 0; i--)
        {
            Object.Destroy(root.GetChild(i).gameObject);
        }

        Image backdrop =
            LanguagePageBuilder.CreateImage(
                "Backdrop",
                root,
                null);

        LanguagePageBuilder.Stretch(backdrop.rectTransform);
        backdrop.color = new Color(0f, 0f, 0f, 0.45f);

        // Clicking outside the card closes the page.
        Button backdropButton =
            backdrop.gameObject.AddComponent<Button>();

        backdropButton.targetGraphic = backdrop;

        backdropButton.onClick.AddListener(
            controller.CloseSettingsPage);

        BuildCard(root);
        BuildTitle(root);

        List<Image> rows = new List<Image>();

        // Rows run downward from just under the title.
        float topY = 48f;
        float y = topY;

        rows.Add(BuildVolumeRow(
            root,
            controller,
            "Music",
            "MUSIC VOLUME",
            controller.MusicVolume,
            () => controller.MusicVolume,
            controller.DecreaseMusic,
            controller.IncreaseMusic,
            y));

        y -= RowHeight + RowGap;

        rows.Add(BuildVolumeRow(
            root,
            controller,
            "Sound Effects",
            "SFX VOLUME",
            controller.SfxVolume,
            () => controller.SfxVolume,
            controller.DecreaseSfx,
            controller.IncreaseSfx,
            y));

        BuildBackButton(root, controller);

        // Same staggered reveal as the language options.
        LanguagePageReveal.Play(root, rows);
    }

    private static void BuildCard(RectTransform root)
    {
        Image card = LanguagePageBuilder.CreateRoundedImage(
            "Card",
            root,
            34,
            LanguagePageBuilder.Green);

        card.rectTransform.sizeDelta =
            new Vector2(760f, 560f);

        Image fill = LanguagePageBuilder.CreateRoundedImage(
            "Card Fill",
            card.transform,
            30,
            LanguagePageBuilder.PanelFill);

        LanguagePageBuilder.Stretch(
            fill.rectTransform,
            new Vector2(10f, 10f));
    }

    private static void BuildTitle(RectTransform root)
    {
        Image pill = LanguagePageBuilder.CreateRoundedImage(
            "Title",
            root,
            30,
            LanguagePageBuilder.Green);

        pill.rectTransform.sizeDelta =
            new Vector2(430f, 96f);

        pill.rectTransform.anchoredPosition =
            new Vector2(0f, 188f);

        Image inner = LanguagePageBuilder.CreateRoundedImage(
            "Title Inner",
            pill.transform,
            26,
            LanguagePageBuilder.Pink);

        LanguagePageBuilder.Stretch(
            inner.rectTransform,
            new Vector2(7f, 7f));

        LanguagePageBuilder.TextLabel text =
            LanguagePageBuilder.CreateLabel(
                "Title Text",
                pill.transform,
                "SETTINGS",
                54);

        LanguagePageBuilder.Stretch(text.Rect);
        text.SetColor(LanguagePageBuilder.Green);
    }

    /// <summary>
    /// A row with a label on the left and [ - ] value [ + ] steppers.
    /// </summary>
    private static Image BuildVolumeRow(
        RectTransform root,
        SettingsPageController controller,
        string label,
        string valueLabel,
        float value,
        System.Func<float> getValue,
        System.Action decrease,
        System.Action increase,
        float y)
    {
        Image row = LanguagePageBuilder.CreateRoundedImage(
            label + " Row",
            root,
            26,
            LanguagePageBuilder.Green);

        row.rectTransform.sizeDelta =
            new Vector2(RowWidth, RowHeight);

        row.rectTransform.anchoredPosition =
            new Vector2(0f, y);

        Image fill = LanguagePageBuilder.CreateRoundedImage(
            "Fill",
            row.transform,
            22,
            LanguagePageBuilder.Pink);

        LanguagePageBuilder.Stretch(
            fill.rectTransform,
            new Vector2(6f, 6f));

        // Category name sits on the left, right-aligned so it never
        // collides with the stepper group on the right.
        LanguagePageBuilder.TextLabel name =
            LanguagePageBuilder.CreateLabel(
                "Label",
                fill.transform,
                label,
                28);

        RectTransform nameRect = name.Rect;
        nameRect.anchorMin = new Vector2(0.5f, 0.5f);
        nameRect.anchorMax = new Vector2(0.5f, 0.5f);
        nameRect.pivot = new Vector2(1f, 0.5f);
        nameRect.sizeDelta = new Vector2(210f, RowHeight);
        nameRect.anchoredPosition = new Vector2(-40f, 0f);
        name.SetColor(LanguagePageBuilder.Green);

        // The centre pill shows the current setting.
        Image valuePill = LanguagePageBuilder.CreateRoundedImage(
            "Value",
            fill.transform,
            26,
            LanguagePageBuilder.Green);

        valuePill.rectTransform.sizeDelta =
            new Vector2(150f, 54f);

        valuePill.rectTransform.anchoredPosition =
            new Vector2(130f, 0f);

        Image valueInner = LanguagePageBuilder.CreateRoundedImage(
            "Value Inner",
            valuePill.transform,
            22,
            LanguagePageBuilder.PanelFill);

        LanguagePageBuilder.Stretch(
            valueInner.rectTransform,
            new Vector2(4f, 4f));

        LanguagePageBuilder.TextLabel valueText =
            LanguagePageBuilder.CreateLabel(
                "Value Text",
                valuePill.transform,
                VolumeText(value),
                22);

        LanguagePageBuilder.Stretch(valueText.Rect);
        valueText.SetColor(LanguagePageBuilder.Green);

        // Steppers flank the value pill. They refresh the label in
        // place rather than rebuilding, so the row does not re-animate.
        BuildStepper(
            fill.transform,
            "-",
            new Vector2(30f, 0f),
            () =>
            {
                decrease();

                valueText.Label.text = VolumeText(getValue());
                valueText.SetColor(LanguagePageBuilder.Green);
            });

        BuildStepper(
            fill.transform,
            "+",
            new Vector2(230f, 0f),
            () =>
            {
                increase();

                valueText.Label.text = VolumeText(getValue());
                valueText.SetColor(LanguagePageBuilder.Green);
            });

        return row;
    }

    private static string VolumeText(float value)
    {
        return Mathf.RoundToInt(value * 100f) + "%";
    }

    /// <summary>
    /// A circular -/+ button with a candy-bubble rim, inner shine, and
    /// a crisp glyph.
    /// </summary>
    private static void BuildStepper(
        Transform parent,
        string symbol,
        Vector2 position,
        System.Action onClick)
    {
        const float size = 56f;
        Sprite circle = LanguagePageBuilder.Circle(192);
        Color bubbleGreen = new Color32(0x4F, 0x96, 0x2C, 0xFF);

        Image shadow = LanguagePageBuilder.CreateImage(
            "Shadow",
            parent,
            circle);
        shadow.rectTransform.sizeDelta = new Vector2(size, size);
        shadow.rectTransform.anchoredPosition = position + new Vector2(0f, -3f);
        shadow.color = new Color(0.08f, 0.16f, 0.04f, 0.28f);
        shadow.raycastTarget = false;

        Image disc = LanguagePageBuilder.CreateImage(
            symbol == "-" ? "Minus" : "Plus",
            parent,
            circle);
        disc.rectTransform.sizeDelta = new Vector2(size, size);
        disc.rectTransform.anchoredPosition = position;
        disc.color = LanguagePageBuilder.Green;

        Image cream = LanguagePageBuilder.CreateImage("Rim", disc.transform, circle);
        LanguagePageBuilder.Stretch(cream.rectTransform, new Vector2(3.5f, 3.5f));
        cream.color = LanguagePageBuilder.PanelFill;
        cream.raycastTarget = false;

        Image core = LanguagePageBuilder.CreateImage("Core", disc.transform, circle);
        LanguagePageBuilder.Stretch(core.rectTransform, new Vector2(6.5f, 6.5f));
        core.color = bubbleGreen;
        core.raycastTarget = false;

        Image shine = LanguagePageBuilder.CreateImage("Shine", disc.transform, circle);
        RectTransform shineRect = shine.rectTransform;
        shineRect.anchorMin = shineRect.anchorMax = new Vector2(0.5f, 0.5f);
        shineRect.pivot = new Vector2(0.5f, 0.5f);
        shineRect.sizeDelta = new Vector2(28f, 14f);
        shineRect.anchoredPosition = new Vector2(-1f, 10f);
        shine.color = new Color(1f, 1f, 1f, 0.42f);
        shine.raycastTarget = false;

        LanguagePageBuilder.TextLabel glyph =
            LanguagePageBuilder.CreateLabel(
                "Glyph",
                disc.transform,
                symbol == "-" ? "−" : "+",
                symbol == "-" ? 38 : 36);
        LanguagePageBuilder.Stretch(glyph.Rect);
        glyph.SetColor(LanguagePageBuilder.PanelFill);
        glyph.Label.fontStyle = FontStyles.Bold;
        glyph.Label.characterSpacing = -4f;
        Outline outline = glyph.Label.gameObject.AddComponent<Outline>();
        outline.effectColor = new Color32(0x22, 0x42, 0x12, 200);
        outline.effectDistance = new Vector2(1.1f, -1.1f);

        Button button = disc.gameObject.AddComponent<Button>();
        button.targetGraphic = disc;
        button.colors = LanguagePageBuilder.ButtonColors();

        MenuButtonHover hover =
            disc.gameObject.AddComponent<MenuButtonHover>();

        LanguagePageBuilder.SetField(
            hover,
            "hoverArea",
            disc.rectTransform);

        LanguagePageBuilder.SetField(
            hover,
            "visualTarget",
            disc.rectTransform);

        button.onClick.AddListener(() =>
        {
            if (onClick != null)
            {
                onClick();
            }
        });
    }

    /// <summary>
    /// Circular back button, matching the Language Page style.
    /// </summary>
    private static void BuildBackButton(
        RectTransform root,
        SettingsPageController controller)
    {
        Transform card = root.Find("Card");
        RectTransform parent = card != null
            ? card as RectTransform
            : root;

        Sprite icon = MorphicBackButton.LoadSprite();
        Image back = LanguagePageBuilder.CreateImage(
            "Back Button",
            parent,
            icon != null ? icon : LanguagePageBuilder.Circle(128));

        RectTransform rt = back.rectTransform;
        MorphicBackButton.PlaceOnPanel(rt, 88f);

        if (icon != null)
            MorphicBackButton.ApplyIcon(back);
        else
            back.color = LanguagePageBuilder.Pink;

        Button button = back.gameObject.AddComponent<Button>();
        button.targetGraphic = back;
        button.colors = LanguagePageBuilder.ButtonColors();

        MenuButtonHover hover =
            back.gameObject.AddComponent<MenuButtonHover>();

        LanguagePageBuilder.SetField(
            hover,
            "hoverArea",
            back.rectTransform);

        LanguagePageBuilder.SetField(
            hover,
            "visualTarget",
            back.rectTransform);

        button.onClick.AddListener(
            controller.CloseSettingsPage);
    }
}