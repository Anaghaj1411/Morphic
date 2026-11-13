using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// Morphic type: bold dark ink that stays inside its box.
public static class MorphicText
{
    public static readonly Color Ink = new Color32(0x08, 0x12, 0x08, 0xFF);
    private static TMP_FontAsset boldFont;

    public static float Size(float pt)
    {
        return Mathf.Clamp(pt + 4f, 14f, 52f);
    }

    public static void Style(TMP_Text label)
    {
        if (label == null || IsPlaceholder(label))
            return;

        if (label.gameObject.name == "Dialogue Text")
        {
            FitDialogue(label);
            return;
        }

        ApplyBoldFace(label);
        label.characterSpacing = 0.15f;
        label.color = new Color(Ink.r, Ink.g, Ink.b, 1f);
        ClearGhost(label);
    }

    public static void FitButton(TMP_Text label)
    {
        if (label == null)
            return;

        Style(label);
        label.enableWordWrapping = false;
        label.overflowMode = TextOverflowModes.Ellipsis;
        label.enableAutoSizing = true;
        label.fontSizeMin = 16f;
        label.fontSizeMax = 28f;
        label.alignment = TextAlignmentOptions.Center;
        label.transform.SetAsLastSibling();
        ClearGhost(label);
    }

    public static void FitBody(TMP_Text label)
    {
        if (label == null)
            return;

        Style(label);
        label.enableWordWrapping = true;
        label.textWrappingMode = TextWrappingModes.Normal;
        label.overflowMode = TextOverflowModes.Truncate;
        label.lineSpacing = 0f;
        label.enableAutoSizing = true;
        label.fontSizeMin = 24f;
        label.fontSizeMax = 32f;
        ClearGhost(label);
    }

    public static void FitDialogue(TMP_Text label)
    {
        if (label == null)
            return;

        ApplyBoldFace(label);
        label.characterSpacing = 0f;
        label.color = Color.black;
        label.faceColor = Color.white;
        label.enableWordWrapping = true;
        label.textWrappingMode = TextWrappingModes.Normal;
        label.overflowMode = TextOverflowModes.Overflow;
        label.lineSpacing = 4f;
        label.enableAutoSizing = true;
        label.fontSizeMin = 22f;
        label.fontSizeMax = 30f;
        label.alignment = TextAlignmentOptions.TopLeft;
        label.margin = new Vector4(8f, 4f, 8f, 4f);
        label.maskable = false;
        IsolateFont(label);
        ClearGhost(label);
        label.fontStyle = FontStyles.Normal;
    }

    public static void FitTitle(TMP_Text label)
    {
        if (label == null)
            return;

        Style(label);
        label.enableWordWrapping = false;
        label.overflowMode = TextOverflowModes.Ellipsis;
        label.enableAutoSizing = true;
        label.fontSizeMin = 26f;
        label.fontSizeMax = 38f;
        ClearGhost(label);
    }

    public static void ClearGhost(TMP_Text label)
    {
        if (label == null)
            return;

        label.outlineWidth = 0f;
        label.outlineColor = new Color(0f, 0f, 0f, 0f);
        label.extraPadding = false;

        StripMaterialGhost(label.fontSharedMaterial);
        StripMaterialGhost(label.fontMaterial);

        Outline[] outlines = label.GetComponents<Outline>();
        for (int i = 0; i < outlines.Length; i++)
            Object.DestroyImmediate(outlines[i]);

        Shadow[] shadows = label.GetComponents<Shadow>();
        for (int i = 0; i < shadows.Length; i++)
        {
            if (shadows[i] != null && !(shadows[i] is Outline))
                Object.DestroyImmediate(shadows[i]);
        }

        Text legacy = label.GetComponent<Text>();
        if (legacy != null)
            legacy.enabled = false;
    }

    public static void StripGhostTree(Transform root)
    {
        if (root == null)
            return;

        Outline[] outlines = root.GetComponentsInChildren<Outline>(true);
        for (int i = 0; i < outlines.Length; i++)
            Object.DestroyImmediate(outlines[i]);

        Shadow[] shadows = root.GetComponentsInChildren<Shadow>(true);
        for (int i = 0; i < shadows.Length; i++)
        {
            if (shadows[i] != null)
                Object.DestroyImmediate(shadows[i]);
        }
    }

    public static void ApplyBoldFace(TMP_Text label)
    {
        if (label == null)
            return;

        if (boldFont == null)
            boldFont = Resources.Load<TMP_FontAsset>("Fonts & Materials/Roboto-Bold SDF");
        if (boldFont == null)
            boldFont = Resources.Load<TMP_FontAsset>("Fonts & Materials/Oswald Bold SDF");

        bool cjk = MorphicLoc.Code == "zh-TW";
        if (!cjk && boldFont != null)
            label.font = boldFont;

        // Real bold atlas — simulated FontStyles.Bold paints a second ghost mesh.
        label.fontStyle = FontStyles.Normal;
        label.fontWeight = FontWeight.Bold;
    }

    private static void IsolateFont(TMP_Text label)
    {
        if (label == null || label.font == null)
            return;

        Material current = label.fontMaterial;
        if (current != null && current.name.IndexOf("MorphicClean", System.StringComparison.Ordinal) >= 0)
        {
            StripMaterialGhost(current);
            return;
        }

        Material source = label.font.material;
        if (source == null)
            source = label.fontSharedMaterial;
        if (source == null)
            return;

        Material copy = new Material(source)
        {
            name = source.name + " MorphicClean"
        };
        StripMaterialGhost(copy);
        label.fontMaterial = copy;
    }

    private static void StripMaterialGhost(Material mat)
    {
        if (mat == null)
            return;

        if (mat.HasProperty(ShaderUtilities.ID_OutlineWidth))
            mat.SetFloat(ShaderUtilities.ID_OutlineWidth, 0f);
        if (mat.HasProperty(ShaderUtilities.ID_UnderlayOffsetX))
            mat.SetFloat(ShaderUtilities.ID_UnderlayOffsetX, 0f);
        if (mat.HasProperty(ShaderUtilities.ID_UnderlayOffsetY))
            mat.SetFloat(ShaderUtilities.ID_UnderlayOffsetY, 0f);
        if (mat.HasProperty(ShaderUtilities.ID_UnderlaySoftness))
            mat.SetFloat(ShaderUtilities.ID_UnderlaySoftness, 0f);
        if (mat.HasProperty(ShaderUtilities.ID_UnderlayDilate))
            mat.SetFloat(ShaderUtilities.ID_UnderlayDilate, 0f);
        if (mat.HasProperty(ShaderUtilities.ID_FaceDilate))
            mat.SetFloat(ShaderUtilities.ID_FaceDilate, 0f);
        mat.DisableKeyword(ShaderUtilities.Keyword_Underlay);
        mat.DisableKeyword("OUTLINE_ON");
        mat.DisableKeyword("UNDERLAY_ON");
        mat.DisableKeyword("UNDERLAY_INNER");
    }

    private static bool IsPlaceholder(TMP_Text label)
    {
        return label != null &&
            label.gameObject.name.IndexOf("Placeholder", System.StringComparison.OrdinalIgnoreCase) >= 0;
    }
}
