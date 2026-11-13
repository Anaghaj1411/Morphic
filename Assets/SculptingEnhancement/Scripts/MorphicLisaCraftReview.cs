using System.Collections.Generic;
using System.Text;
using UnityEngine;

/// Packs the live lantern, teapot, or keris into the OpenAI request.
/// Lisa's spoken reply still comes from the backend, not from here.
public static class MorphicLisaCraftReview
{
    public static string BackendHint(MorphicCraftHelpState state)
    {
        string summary = BuildSummary(state);
        if (string.IsNullOrEmpty(summary))
            return "If they ask how their work looks, give warm specific praise from the sculpture data. Stay kind.";

        return "Student work now: " + summary +
            ". If they ask how it looks, praise these exact shapes, colors, and designs. " +
            "Do not invent details. Only give how-to steps when they asked how to do something.";
    }

    public static void FillDecor(
        MorphicCraftHelpState state,
        Color mainColor,
        Color[] partColors,
        string[] partNames,
        Texture stamp,
        Texture selfie,
        TeapotSurfacePatternPainter painter,
        VertexSculptController sculpt)
    {
        if (state == null)
            return;

        state.colors = CollectColors(mainColor, partColors, partNames);
        state.designs = CollectDesigns(stamp, selfie);
        state.bands = painter != null ? painter.StrokeCount : 0;
        state.clayCoils = painter != null ? painter.ClayStrokeCount : 0;
        state.vertexSculpt = sculpt != null && sculpt.HasUndo ? 1 : 0;
    }

    public static string BuildSummary(MorphicCraftHelpState state)
    {
        if (state == null)
            return "";

        StringBuilder summary = new StringBuilder();
        string craft = string.IsNullOrEmpty(state.currentTool) ? "craft" : state.currentTool;
        summary.Append(craft);
        if (!string.IsNullOrEmpty(state.currentStage))
            summary.Append(" ").Append(state.currentStage);

        AppendShape(summary, state);
        if (!string.IsNullOrEmpty(state.colors))
            summary.Append("; colors: ").Append(state.colors);
        if (!string.IsNullOrEmpty(state.designs))
            summary.Append("; designs: ").Append(state.designs);
        if (state.hasStamp > 0)
            summary.Append("; stamp");
        if (state.hasSelfie > 0)
            summary.Append("; selfie");
        if (state.bands > 0)
            summary.Append("; bands ").Append(state.bands);
        if (state.clayCoils > 0)
            summary.Append("; clay coils ").Append(state.clayCoils);
        if (state.vertexSculpt > 0)
            summary.Append("; vertex sculpt");
        return summary.ToString();
    }

    public static string ColorName(Color color)
    {
        Color.RGBToHSV(color, out float hue, out float sat, out float val);
        if (val < 0.16f)
            return "deep black";
        if (sat < 0.14f)
        {
            if (val > 0.88f)
                return "cream";
            if (val > 0.62f)
                return "soft gray";
            return "charcoal";
        }

        if (hue < 0.035f || hue > 0.95f)
            return val < 0.45f ? "deep red" : "warm red";
        if (hue < 0.08f)
            return val < 0.62f ? "terracotta" : "coral";
        if (hue < 0.12f)
            return "orange";
        if (hue < 0.16f)
            return "gold";
        if (hue < 0.20f)
            return "amber";
        if (hue < 0.28f)
            return "yellow";
        if (hue < 0.45f)
            return val < 0.42f ? "forest green" : "fresh green";
        if (hue < 0.55f)
            return "teal";
        if (hue < 0.72f)
            return val < 0.48f ? "deep blue" : "bright blue";
        if (hue < 0.82f)
            return "purple";
        return val > 0.72f ? "pink" : "magenta";
    }

    private static void AppendShape(StringBuilder summary, MorphicCraftHelpState state)
    {
        List<string> notes = new List<string>();
        string tool = state.currentTool;
        if (tool == "teapot")
        {
            AddShape(notes, "width", state.width);
            AddShape(notes, "height", state.height);
            AddShape(notes, "middle", state.bamboo);
            AddShape(notes, "bevel", state.topper);
            AddShape(notes, "lid", state.lidTop);
            AddShape(notes, "spout", state.lidBottom);
            AddShape(notes, "handle", state.tassel);
        }
        else if (tool == "keris")
        {
            AddShape(notes, "width", state.width);
            AddShape(notes, "height", state.height);
            AddShape(notes, "carving", state.bamboo);
            int handle = state.kerisUseHandle2 > 0 ? state.kerisHandle2 : state.kerisHandle1;
            AddShape(notes, "handle", handle);
        }
        else
        {
            AddShape(notes, "width", state.width);
            AddShape(notes, "height", state.height);
            AddShape(notes, "topper", state.topper);
            AddShape(notes, "bottom", state.bottom);
            AddShape(notes, "lid top", state.lidTop);
            AddShape(notes, "lid bottom", state.lidBottom);
            AddShape(notes, "bamboo", state.bamboo);
            AddShape(notes, "tassel", state.tassel);
        }

        if (notes.Count > 0)
            summary.Append("; shape: ").Append(string.Join(", ", notes.ToArray()));
    }

    private static void AddShape(List<string> notes, string name, int amount)
    {
        if (amount < 18 || notes == null)
            return;
        string strength = amount >= 70 ? "bold" : amount >= 40 ? "clear" : "gentle";
        notes.Add(name + " " + strength + " " + amount);
    }

    private static string CollectColors(Color main, Color[] parts, string[] partNames)
    {
        List<string> names = new List<string>();
        AddUnique(names, ColorName(main));
        if (parts != null)
        {
            for (int i = 0; i < parts.Length; i++)
            {
                if (IsNearWhite(parts[i]) || ColorsClose(parts[i], main))
                    continue;
                string label = ColorName(parts[i]);
                if (partNames != null && i < partNames.Length && !string.IsNullOrEmpty(partNames[i]))
                    label = partNames[i] + " " + label;
                AddUnique(names, label);
            }
        }

        return string.Join(", ", names.ToArray());
    }

    private static string CollectDesigns(Texture stamp, Texture selfie)
    {
        List<string> notes = new List<string>();
        string stampName = TextureLabel(stamp);
        if (!string.IsNullOrEmpty(stampName))
            notes.Add(stampName);
        if (selfie != null)
            notes.Add("selfie");
        return string.Join(", ", notes.ToArray());
    }

    private static string TextureLabel(Texture texture)
    {
        if (texture == null)
            return "";
        string name = texture.name ?? "";
        name = name.Replace("(Clone)", "").Replace("_", " ").Trim();
        if (name.StartsWith("Morphic "))
            name = name.Substring(8);
        if (name.Length > 28)
            name = name.Substring(0, 28);
        return name;
    }

    private static void AddUnique(List<string> names, string value)
    {
        if (string.IsNullOrEmpty(value) || names == null)
            return;
        for (int i = 0; i < names.Count; i++)
        {
            if (string.Equals(names[i], value, System.StringComparison.OrdinalIgnoreCase))
                return;
        }

        names.Add(value);
    }

    private static bool IsNearWhite(Color color)
    {
        return color.r > 0.92f && color.g > 0.92f && color.b > 0.92f;
    }

    private static bool ColorsClose(Color a, Color b)
    {
        return Mathf.Abs(a.r - b.r) < 0.08f &&
            Mathf.Abs(a.g - b.g) < 0.08f &&
            Mathf.Abs(a.b - b.b) < 0.08f;
    }
}
