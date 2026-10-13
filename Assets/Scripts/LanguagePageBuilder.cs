using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Builds the Language Page UI in code: rounded card with a green
/// outline, pink buttons with flag discs, and a back button.
///
/// Doing this in code rather than hand-authored scene objects keeps
/// the layout reproducible and guarantees every button is wired to
/// a real handler.
/// </summary>
public static class LanguagePageBuilder
{
    // Palette taken from the reference design.
    public static readonly Color PanelFill =
        new Color32(0xFD, 0xF2, 0xFA, 0xFF);

    public static readonly Color Pink =
        new Color32(0xFC, 0xB8, 0xF1, 0xFF);

    public static readonly Color Green =
        new Color32(0x34, 0x65, 0x1D, 0xFF);

    // Generated sprites are kept alive here and released on quit.
    private static readonly List<Object> Cache =
        new List<Object>();

    public struct LanguageOption
    {
        public string Code;
        public string Label;
        public string Flag;
    }

    public static readonly LanguageOption[] Options =
    {
        new LanguageOption
        {
            Code = "en",
            Label = "ENGLISH",
            Flag = "UK"
        },
        new LanguageOption
        {
            Code = "zh-TW",
            Label = "TAIWANESE",
            Flag = "TW"
        },
        new LanguageOption
        {
            Code = "id",
            Label = "INDONESIAN",
            Flag = "ID"
        }
    };

    public static void Build(
        RectTransform root,
        LanguagePageController controller
    )
    {
        if (root == null ||
            controller == null)
        {
            return;
        }

        // Remove anything authored in the scene so the built
        // layout is the only thing on screen.
        for (
            int i = root.childCount - 1;
            i >= 0;
            i--)
        {
            Object.Destroy(root.GetChild(i).gameObject);
        }

        root.gameObject.SetActive(true);

        // Dim the screen behind the card.
        Image backdrop =
            CreateImage("Backdrop", root, null);

        Stretch(backdrop.rectTransform);
        backdrop.color = new Color(0f, 0f, 0f, 0.45f);

        BuildCard(root);
        BuildTitle(root);
        BuildOptionButtons(root, controller);
        BuildBackButton(root, controller);
    }

    private static void BuildCard(RectTransform root)
    {
        Image card =
            CreateRoundedImage(
                "Card",
                root,
                34,
                Green);

        card.rectTransform.sizeDelta =
            new Vector2(760f, 640f);

        Image fill =
            CreateRoundedImage(
                "Card Fill",
                card.transform,
                30,
                PanelFill);

        Stretch(fill.rectTransform, new Vector2(10f, 10f));
    }

    private static void BuildTitle(RectTransform root)
    {
        Image pill =
            CreateRoundedImage(
                "Title",
                root,
                30,
                Green);

        pill.rectTransform.sizeDelta =
            new Vector2(430f, 96f);

        pill.rectTransform.anchoredPosition =
            new Vector2(0f, 236f);

        Image inner =
            CreateRoundedImage(
                "Title Inner",
                pill.transform,
                26,
                Pink);

        Stretch(inner.rectTransform, new Vector2(7f, 7f));

        TextLabel text =
            CreateLabel(
                "Title Text",
                pill.transform,
                "LANGUAGE",
                54);

        Stretch(text.Rect);
        text.SetColor(Green);
    }

    private static void BuildOptionButtons(
        RectTransform root,
        LanguagePageController controller
    )
    {
        float gap = 130f;
        float startY = 60f;

        List<Image> built = new List<Image>();

        for (
            int i = 0;
            i < Options.Length;
            i++)
        {
            built.Add(BuildOptionButton(
                root,
                controller,
                Options[i],
                startY - gap * i
            ));
        }

        // Reveal one button at a time, top to bottom, so the list
        // reads as a sequence rather than appearing all at once.
        LanguagePageReveal.Play(root, built);
    }

    private static Image BuildOptionButton(
        RectTransform root,
        LanguagePageController controller,
        LanguageOption option,
        float y
    )
    {
        Image button = CreateRoundedImage(
            option.Code.Replace("-", string.Empty) +
            " Button",
            root,
            30,
            Green);

        button.rectTransform.sizeDelta =
            new Vector2(540f, 104f);

        button.rectTransform.anchoredPosition =
            new Vector2(0f, y);

        Image fill =
            CreateRoundedImage(
                "Fill",
                button.transform,
                26,
                Pink);

        Stretch(fill.rectTransform, new Vector2(8f, 8f));

        BuildFlag(fill.transform, option.Flag);
        BuildOptionLabel(fill.transform, option.Label);
        WireButton(fill, button, controller, option);

        return button;
    }

    private static void BuildFlag(
        Transform parent,
        string flag)
    {
        Image disc =
            CreateImage("Flag", parent, Circle(96));

        disc.rectTransform.sizeDelta =
            new Vector2(66f, 66f);

        disc.rectTransform.anchoredPosition =
            new Vector2(-198f, 0f);

        disc.sprite = FlagSprite(flag);
        disc.color = Color.white;
        disc.preserveAspect = true;
    }

    private static void BuildOptionLabel(
        Transform parent,
        string label)
    {
        TextLabel text =
            CreateLabel(
                "Label",
                parent,
                label,
                38);

        RectTransform rt = text.Rect;

        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = new Vector2(360f, 80f);

        // Sit the text to the right of the flag.
        rt.anchoredPosition = new Vector2(48f, 0f);

        text.SetColor(Green);
    }

    private static void WireButton(
        Image fill,
        Image visual,
        LanguagePageController controller,
        LanguageOption option
    )
    {
        Button uiButton =
            fill.gameObject.AddComponent<Button>();

        uiButton.targetGraphic = fill;
        uiButton.colors = ButtonColors();

        // Hover scale, matching the rest of the menu.
        MenuButtonHover hover =
            visual.gameObject.AddComponent<MenuButtonHover>();

        SetField(hover, "hoverArea", visual.rectTransform);
        SetField(hover, "visualTarget", visual.rectTransform);

        LanguagePageController.LanguageChoice choice =
            new LanguagePageController.LanguageChoice
            {
                code = option.Code,
                label = option.Label
            };

        uiButton.onClick.AddListener(
            () => controller.SelectLanguage(choice)
        );
    }

    private static void BuildBackButton(
        RectTransform root,
        LanguagePageController controller
    )
    {
        Image back =
            CreateImage(
                "Back Button",
                root,
                Circle(128));

        RectTransform rt = back.rectTransform;

        rt.sizeDelta = new Vector2(104f, 104f);
        rt.anchorMin = new Vector2(1f, 1f);
        rt.anchorMax = new Vector2(1f, 1f);
        rt.pivot = new Vector2(1f, 1f);
        rt.anchoredPosition = new Vector2(96f, 84f);

        back.color = Pink;

        Image arrow =
            CreateImage(
                "Arrow",
                back.transform,
                ArrowSprite(128, 96));

        arrow.rectTransform.sizeDelta =
            new Vector2(58f, 44f);

        arrow.color = Green;

        Button uiButton =
            back.gameObject.AddComponent<Button>();

        uiButton.targetGraphic = back;
        uiButton.colors = ButtonColors();

        MenuButtonHover hover =
            back.gameObject.AddComponent<MenuButtonHover>();

        SetField(hover, "hoverArea", back.rectTransform);
        SetField(hover, "visualTarget", back.rectTransform);

        uiButton.onClick.AddListener(
            () => controller.CloseLanguagePage()
        );
    }

    public static ColorBlock ButtonColors()
    {
        return new ColorBlock
        {
            normalColor = Color.white,
            highlightedColor = Color.white,
            pressedColor = new Color(0.9f, 0.9f, 0.9f, 1f),
            selectedColor = Color.white,
            disabledColor = new Color(1f, 1f, 1f, 0.4f),
            colorMultiplier = 1f,
            fadeDuration = 0.06f
        };
    }

    // ------------------------------------------------------------
    // UI helpers
    // ------------------------------------------------------------

    public static Image CreateImage(
        string name,
        Transform parent,
        Sprite sprite
    )
    {
        GameObject go = new GameObject(
            name,
            typeof(RectTransform),
            typeof(CanvasRenderer),
            typeof(Image));

        go.layer = parent.gameObject.layer;

        RectTransform rt = go.GetComponent<RectTransform>();

        rt.SetParent(parent, false);
        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = Vector2.zero;
        rt.sizeDelta = new Vector2(100f, 100f);

        Image img = go.GetComponent<Image>();

        // A null sprite renders as a plain white quad, which is
        // what we want for solid fills.
        img.sprite = sprite;
        img.type = Image.Type.Simple;

        return img;
    }

    public static TextLabel CreateLabel(
        string name,
        Transform parent,
        string text,
        int fontSize
    )
    {
        GameObject go = new GameObject(
            name,
            typeof(RectTransform));

        go.layer = parent.gameObject.layer;

        RectTransform rt = go.GetComponent<RectTransform>();

        rt.SetParent(parent, false);
        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = new Vector2(400f, 80f);

        TMPro.TextMeshProUGUI label =
            go.AddComponent<TMPro.TextMeshProUGUI>();

        label.text = text;
        label.fontSize = fontSize;
        label.alignment = TMPro.TextAlignmentOptions.Center;
        label.color = Green;

        // Text must never intercept clicks meant for the button.
        label.raycastTarget = false;

        // TMP builds its mesh on the next layout pass, which can
        // reset the vertex colour to the font material default.
        // Re-applying after a frame keeps the green readable.
        ApplyTextColor(label);

        return new TextLabel(label);
    }

    private static void ApplyTextColor(
        TMPro.TextMeshProUGUI label)
    {
        if (label == null)
        {
            return;
        }

        label.color = Green;

        TextMeshProUGUIRunner.Run(
            label,
            () =>
            {
                if (label != null)
                {
                    label.color = Green;
                    label.ForceMeshUpdate();
                }
            });
    }

    /// <summary>
    /// Wrapper so TMPro types do not leak into every call site.
    /// </summary>
    public struct TextLabel
    {
        public TMPro.TextMeshProUGUI Label;

        public TextLabel(
            TMPro.TextMeshProUGUI label)
        {
            Label = label;
        }

        public RectTransform Rect
        {
            get
            {
                return Label != null
                    ? Label.rectTransform
                    : null;
            }
        }

        public void SetColor(Color c)
        {
            if (Label != null)
            {
                Label.color = c;
            }
        }
    }

    public static void Stretch(
        RectTransform rt,
        Vector2 padding = default(Vector2))
    {
        if (rt == null)
        {
            return;
        }

        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.pivot = new Vector2(0.5f, 0.5f);

        rt.offsetMin = new Vector2(padding.x, padding.y);
        rt.offsetMax = new Vector2(-padding.x, -padding.y);
    }

    public static void SetField(
        object target,
        string field,
        object value)
    {
        if (target == null)
        {
            return;
        }

        System.Reflection.FieldInfo info =
            target.GetType().GetField(
                field,
                System.Reflection.BindingFlags.Instance |
                System.Reflection.BindingFlags.NonPublic);

        if (info != null)
        {
            info.SetValue(target, value);
        }
    }

    // ------------------------------------------------------------
    // Procedural sprites
    // ------------------------------------------------------------

    /// <summary>
    /// Rounded rectangle with a sprite border, so Unity can
    /// 9-slice it. Stretching a plain rounded sprite to a very wide
    /// rect skews the corner radius and turns pills into pointed
    /// lens shapes; slicing keeps the corners circular at any size.
    /// </summary>
    private static Sprite RoundedRect(
        int size,
        int radius)
    {
        Texture2D tex = NewTexture(size, size);

        Color[] px = new Color[size * size];

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                px[y * size + x] = new Color(
                    1f,
                    1f,
                    1f,
                    RoundedCoverage(
                        x + 0.5f,
                        y + 0.5f,
                        size,
                        radius
                    )
                );
            }
        }

        tex.SetPixels(px);
        tex.Apply();

        float b = radius + 2f;

        return NewSprite(
            tex,
            "RoundedRect",
            new Vector4(b, b, b, b));
    }

    /// <summary>
    /// Creates a rounded image that keeps its corners circular when
    /// stretched, by using a sliced sprite.
    /// </summary>
    public static Image CreateRoundedImage(
        string name,
        Transform parent,
        int radius,
        Color color)
    {
        Image img = CreateImage(
            name,
            parent,
            RoundedRect(96, radius));

        img.type = Image.Type.Sliced;
        img.color = color;

        return img;
    }

    private static float RoundedCoverage(
        float x,
        float y,
        float size,
        float radius)
    {
        // Clamp the sample point toward the nearest corner centre,
        // which is what turns a square into a rounded rectangle.
        float cx = Mathf.Clamp(
            x,
            radius,
            size - radius);

        float cy = Mathf.Clamp(
            y,
            radius,
            size - radius);

        float dx = x - cx;
        float dy = y - cy;

        float d =
            (float)Mathf.Sqrt(dx * dx + dy * dy);

        return Mathf.Clamp01(radius - d + 0.5f);
    }

    public static Sprite Circle(int size)
    {
        Texture2D tex = NewTexture(size, size);

        Color[] px = new Color[size * size];

        float c = size * 0.5f;
        float r = c - 1f;

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float dx = x + 0.5f - c;
                float dy = y + 0.5f - c;

                float d =
                    (float)Mathf.Sqrt(
                        dx * dx + dy * dy);

                // One pixel of antialiasing on the rim.
                px[y * size + x] = new Color(
                    1f,
                    1f,
                    1f,
                    Mathf.Clamp01(r - d + 0.5f)
                );
            }
        }

        tex.SetPixels(px);
        tex.Apply();

        return NewSprite(tex, "Circle");
    }

    /// <summary>
    /// Left-pointing arrow: triangular head plus a shaft.
    /// </summary>
    public static Sprite ArrowSprite(int w, int h)
    {
        Texture2D tex = NewTexture(w, h);

        Color[] px = new Color[w * h];

        float headW = h * 0.6f;
        float shaftH = h * 0.36f;
        float cy = h * 0.5f;

        for (int y = 0; y < h; y++)
        {
            for (int x = 0; x < w; x++)
            {
                float fx = x + 0.5f;
                float fy = y + 0.5f;

                // Triangle tapers to a point on the left.
                bool inHead =
                    fx <= headW &&
                    Mathf.Abs(fy - cy) <=
                    (headW - fx) * (cy / (h * 0.5f));

                bool inShaft =
                    fx > headW * 0.3f &&
                    Mathf.Abs(fy - cy) <= shaftH * 0.5f;

                px[y * w + x] = new Color(
                    1f,
                    1f,
                    1f,
                    inHead || inShaft ? 1f : 0f
                );
            }
        }

        tex.SetPixels(px);
        tex.Apply();

        return NewSprite(tex, "Arrow");
    }

    /// <summary>
    /// Flag discs drawn procedurally, so the page needs no imported
    /// flag artwork.
    /// </summary>
    private static Sprite FlagSprite(string flag)
    {
        int size = 96;
        Texture2D tex = NewTexture(size, size);

        Color[] px = new Color[size * size];

        Color red = new Color32(0xBC, 0x00, 0x2D, 0xFF);
        Color blue = new Color32(0x01, 0x2C, 0x6B, 0xFF);

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float u = x / (float)(size - 1);
                float v = y / (float)(size - 1);

                Color c = SampleFlag(
                    flag,
                    u,
                    v,
                    red,
                    blue,
                    Color.white);

                // Bake the round shape into the texture so the flag
                // reads as a disc. A square mask sprite behind a
                // square texture would not clip anything.
                float dx = x + 0.5f - size * 0.5f;
                float dy = y + 0.5f - size * 0.5f;

                float d =
                    (float)Mathf.Sqrt(
                        dx * dx + dy * dy);

                float r = size * 0.5f - 1f;

                c.a *= Mathf.Clamp01(r - d + 0.5f);

                px[y * size + x] = c;
            }
        }

        tex.SetPixels(px);
        tex.Apply();

        return NewSprite(tex, "Flag_" + flag);
    }

    private static Color SampleFlag(
        string flag,
        float u,
        float v,
        Color red,
        Color blue,
        Color white)
    {
        if (flag == "UK")
        {
            // Blue field, white diagonal saltire, red upright
            // cross in the centre.
            bool saltire =
                Mathf.Abs(u - v) < 0.14f ||
                Mathf.Abs(u + v - 1f) < 0.14f;

            if (saltire)
            {
                return white;
            }

            bool cross =
                Mathf.Abs(u - 0.5f) < 0.10f ||
                Mathf.Abs(v - 0.5f) < 0.12f;

            return cross ? red : blue;
        }

        if (flag == "TW")
        {
            // Red field with a blue canton and white sun.
            if (u > 0.54f || v < 0.56f)
            {
                return red;
            }

            float dx = (u - 0.27f) / 0.17f;
            float dy = (v - 0.27f) / 0.17f;

            float d =
                (float)Mathf.Sqrt(dx * dx + dy * dy);

            return d < 1f ? white : blue;
        }

        // Indonesia: red over white.
        return v > 0.5f ? red : white;
    }

    private static Texture2D NewTexture(
        int w,
        int h)
    {
        Texture2D tex = new Texture2D(
            w,
            h,
            TextureFormat.RGBA32,
            false);

        tex.filterMode = FilterMode.Bilinear;
        tex.wrapMode = TextureWrapMode.Clamp;

        Cache.Add(tex);

        return tex;
    }

    private static Sprite NewSprite(
        Texture2D tex,
        string name,
        Vector4 border = default(Vector4))
    {
        Sprite sprite = Sprite.Create(
            tex,
            new Rect(0f, 0f, tex.width, tex.height),
            new Vector2(0.5f, 0.5f),
            100f,
            0,
            SpriteMeshType.FullRect,
            border);

        sprite.name = name;

        Cache.Add(sprite);

        return sprite;
    }

    [RuntimeInitializeOnLoadMethod(
        RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        Cache.Clear();
    }
}