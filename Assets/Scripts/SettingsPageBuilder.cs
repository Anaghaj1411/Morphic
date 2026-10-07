using System.Collections.Generic;
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
///   Fullscreen   [ ON / OFF ]
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
        float topY = 92f;
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

        y -= RowHeight + RowGap;

        rows.Add(BuildToggleRow(
            root,
            controller,
            "Fullscreen",
            controller.IsFullscreen,
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
            new Vector2(760f, 700f);

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
            new Vector2(0f, 268f);

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
            20,
            LanguagePageBuilder.Green);

        valuePill.rectTransform.sizeDelta =
            new Vector2(150f, 52f);

        valuePill.rectTransform.anchoredPosition =
            new Vector2(130f, 0f);

        Image valueInner = LanguagePageBuilder.CreateRoundedImage(
            "Value Inner",
            valuePill.transform,
            16,
            LanguagePageBuilder.PanelFill);

        LanguagePageBuilder.Stretch(
            valueInner.rectTransform,
            new Vector2(5f, 5f));

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
    /// A circular -/+ button.
    /// </summary>
    private static void BuildStepper(
        Transform parent,
        string symbol,
        Vector2 position,
        System.Action onClick)
    {
        Image disc = LanguagePageBuilder.CreateImage(
            symbol == "-" ? "Minus" : "Plus",
            parent,
            LanguagePageBuilder.Circle(96));

        disc.rectTransform.sizeDelta =
            new Vector2(52f, 52f);

        disc.rectTransform.anchoredPosition = position;

        disc.color = LanguagePageBuilder.Green;

        LanguagePageBuilder.TextLabel glyph =
            LanguagePageBuilder.CreateLabel(
                "Glyph",
                disc.transform,
                symbol,
                40);

        LanguagePageBuilder.Stretch(glyph.Rect);
        glyph.SetColor(LanguagePageBuilder.Pink);

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
    /// A row with a label and an ON / OFF pill that toggles.
    /// </summary>
    private static Image BuildToggleRow(
        RectTransform root,
        SettingsPageController controller,
        string label,
        bool value,
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

        LanguagePageBuilder.TextLabel name =
            LanguagePageBuilder.CreateLabel(
                "Label",
                fill.transform,
                label,
                30);

        RectTransform nameRect = name.Rect;
        nameRect.anchorMin = new Vector2(0.5f, 0.5f);
        nameRect.anchorMax = new Vector2(0.5f, 0.5f);
        nameRect.pivot = new Vector2(1f, 0.5f);
        nameRect.sizeDelta = new Vector2(210f, RowHeight);
        nameRect.anchoredPosition = new Vector2(-40f, 0f);
        name.SetColor(LanguagePageBuilder.Green);

        Image pill = LanguagePageBuilder.CreateRoundedImage(
            "Toggle",
            fill.transform,
            20,
            LanguagePageBuilder.Green);

        pill.rectTransform.sizeDelta =
            new Vector2(150f, 52f);

        pill.rectTransform.anchoredPosition =
            new Vector2(130f, 0f);

        Image pillInner = LanguagePageBuilder.CreateRoundedImage(
            "Toggle Inner",
            pill.transform,
            16,
            LanguagePageBuilder.PanelFill);

        LanguagePageBuilder.Stretch(
            pillInner.rectTransform,
            new Vector2(5f, 5f));

        LanguagePageBuilder.TextLabel state =
            LanguagePageBuilder.CreateLabel(
                "State Text",
                pill.transform,
                value ? "ON" : "OFF",
                24);

        LanguagePageBuilder.Stretch(state.Rect);
        state.SetColor(LanguagePageBuilder.Green);

        // The whole row toggles, which is easier to hit than the pill.
        Button button = fill.gameObject.AddComponent<Button>();
        button.targetGraphic = fill;
        button.colors = LanguagePageBuilder.ButtonColors();

        MenuButtonHover hover =
            row.gameObject.AddComponent<MenuButtonHover>();

        LanguagePageBuilder.SetField(
            hover,
            "hoverArea",
            row.rectTransform);

        LanguagePageBuilder.SetField(
            hover,
            "visualTarget",
            row.rectTransform);

        button.onClick.AddListener(() =>
        {
            controller.ToggleFullscreen();

            // Flip the text in place; rebuilding would restart the
            // reveal animation and hide every row again.
            state.Label.text =
                controller.IsFullscreen ? "ON" : "OFF";

            state.SetColor(LanguagePageBuilder.Green);
        });

        return row;
    }

    /// <summary>
    /// Circular back button, matching the Language Page style.
    /// </summary>
    private static void BuildBackButton(
        RectTransform root,
        SettingsPageController controller)
    {
        Image back = LanguagePageBuilder.CreateImage(
            "Back Button",
            root,
            LanguagePageBuilder.Circle(128));

        RectTransform rt = back.rectTransform;

        rt.sizeDelta = new Vector2(104f, 104f);
        rt.anchorMin = new Vector2(1f, 1f);
        rt.anchorMax = new Vector2(1f, 1f);
        rt.pivot = new Vector2(1f, 1f);
        rt.anchoredPosition = new Vector2(96f, 84f);

        back.color = LanguagePageBuilder.Pink;

        Image arrow = LanguagePageBuilder.CreateImage(
            "Arrow",
            back.transform,
            LanguagePageBuilder.ArrowSprite(128, 96));

        arrow.rectTransform.sizeDelta =
            new Vector2(58f, 44f);

        arrow.color = LanguagePageBuilder.Green;

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