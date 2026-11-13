using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// Shared sculpt-studio card layout so header, instruction, and content
/// never sit on top of each other.
public static class MorphicStudioLayout
{
    public const float TitleY = -16f;
    public const float TitleH = 52f;
    public const float TitleYBack = -16f;
    public const float TitleHBack = 52f;
    public const float InstructionY = -82f;
    public const float InstructionYBack = -88f;
    public const float InstructionH = 76f;
    public const float ContentY = -168f;
    public const float ContentYBack = -180f;
    public const float StampW = 228f;
    public const float StampH = 118f;
    public const float StampGapX = 244f;
    public const float StampGapY = 128f;
    public const float BandTileW = 226f;
    public const float BandTileH = 84f;
    public const float BandTileStride = 92f;
    public const float BandContentHeight = 648f;
    public const float DesignContentHeight = 560f;
    public const float TargetingContentHeight = 228f;
    public const float DesignActionStride = 76f;
    public const float ShelfContentHeight = 156f;

    public static float CardHeight(float contentHeight, bool backVisible)
    {
        float top = backVisible ? -ContentYBack : -ContentY;
        return top + contentHeight + 36f;
    }

    public static void PlaceHeader(TMP_Text header, TMP_Text instruction, bool backVisible)
    {
        if (header != null)
        {
            RectTransform rect = header.rectTransform;
            rect.anchoredPosition = new Vector2(10f, backVisible ? TitleYBack : TitleY);
            rect.sizeDelta = new Vector2(520f, backVisible ? TitleHBack : TitleH);
            MorphicText.FitTitle(header);
        }

        if (instruction == null)
            return;

        RectTransform line = instruction.rectTransform;
        line.anchoredPosition = new Vector2(16f, backVisible ? InstructionYBack : InstructionY);
        line.sizeDelta = new Vector2(508f, InstructionH);
        MorphicText.FitBody(instruction);
    }

    public static void DockContent(RectTransform rect, bool backVisible, float height)
    {
        if (rect == null)
            return;

        rect.anchorMin = new Vector2(0f, 1f);
        rect.anchorMax = new Vector2(1f, 1f);
        rect.pivot = new Vector2(0.5f, 1f);
        rect.anchoredPosition = new Vector2(0f, backVisible ? ContentYBack : ContentY);
        rect.sizeDelta = new Vector2(-40f, height);
    }

    public static float PartListHeight(int partCount, float stride)
    {
        return (partCount + 1) * stride + 8f;
    }

    public static void PlacePartList(Transform partRoot, int partCount, float stride)
    {
        if (partRoot == null)
            return;

        DockContent(partRoot as RectTransform, true, PartListHeight(partCount, stride));
        for (int i = 0; i < partCount; i++)
        {
            Transform part = partRoot.Find("Part" + i);
            RectTransform rect = part as RectTransform;
            if (rect != null)
                rect.anchoredPosition = new Vector2(0f, -i * stride);
        }

        Transform done = partRoot.Find("Done");
        RectTransform doneRect = done as RectTransform;
        if (doneRect != null)
            doneRect.anchoredPosition = new Vector2(0f, -partCount * stride);
    }

    public static void PlaceStampGrid(Button[] stamps)
    {
        if (stamps == null)
            return;

        for (int i = 0; i < stamps.Length; i++)
        {
            if (stamps[i] == null)
                continue;

            int column = i % 2;
            int row = i / 2;
            RectTransform rect = stamps[i].transform as RectTransform;
            if (rect == null)
                continue;

            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 1f);
            rect.pivot = new Vector2(0.5f, 1f);
            rect.anchoredPosition = new Vector2(
                column == 0 ? -StampGapX * 0.5f : StampGapX * 0.5f,
                -row * StampGapY);
            rect.sizeDelta = new Vector2(StampW, StampH);
        }
    }

    public static float StampActionsY(int stampCount)
    {
        int rows = Mathf.Max(1, (stampCount + 1) / 2);
        return -(rows * StampGapY + 16f);
    }

    public static float DesignContentHeightFor(int stampCount)
    {
        int rows = Mathf.Max(1, (stampCount + 1) / 2);
        return rows * StampGapY + 16f + DesignActionStride * 2f + 76f;
    }

    public static void PlaceShelf(RectTransform shelf)
    {
        DockContent(shelf, true, ShelfContentHeight);
        if (shelf == null)
            return;

        SetButton(shelf, "TakeBack", new Vector2(0f, 0f), new Vector2(490f, 60f));
        SetButton(shelf, "Done", new Vector2(0f, -76f), new Vector2(490f, 60f));
    }

    public static void PlaceTargetingBar(Transform root)
    {
        if (root == null)
            return;

        DockContent(root as RectTransform, true, TargetingContentHeight);

        SetButton(root, "TargetingSize", new Vector2(-118f, -8f), new Vector2(236f, 64f));
        SetButton(root, "TargetingColor", new Vector2(118f, -8f), new Vector2(236f, 64f));
        SetButton(root, "TargetingUndo", new Vector2(-156f, -88f), new Vector2(148f, 64f));
        SetButton(root, "TargetingRedo", new Vector2(0f, -88f), new Vector2(148f, 64f));
        SetButton(root, "TargetingClear", new Vector2(156f, -88f), new Vector2(148f, 64f));
    }

    private static void SetButton(Transform root, string childName, Vector2 position, Vector2 size)
    {
        Transform child = root.Find(childName);
        RectTransform rect = child as RectTransform;
        if (rect == null)
            return;

        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 1f);
        rect.pivot = new Vector2(0.5f, 1f);
        rect.anchoredPosition = position;
        rect.sizeDelta = size;

        TMP_Text label = child.GetComponentInChildren<TMP_Text>(true);
        if (label != null)
            MorphicText.FitButton(label);
    }

    public static void PlaceBandLayout(Transform root)
    {
        if (root == null)
            return;

        DockContent(root as RectTransform, true, BandContentHeight);

        for (int i = 0; i < 8; i++)
        {
            Transform tile = root.Find("Band" + i);
            RectTransform rect = tile as RectTransform;
            if (rect == null)
                continue;

            int column = i % 2;
            int row = i / 2;
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 1f);
            rect.pivot = new Vector2(0.5f, 1f);
            rect.anchoredPosition = new Vector2(column == 0 ? -124f : 124f, -row * BandTileStride);
            rect.sizeDelta = new Vector2(BandTileW, BandTileH);
        }

        float afterTiles = -(4f * BandTileStride + 10f);
        SetPos(root, "WidthTitle", new Vector2(0f, afterTiles), new Vector2(490f, 24f));

        float sizesY = afterTiles - 30f;
        for (int s = 0; s < 4; s++)
            SetPos(root, "BandSize" + s, new Vector2(-174f + s * 116f, sizesY), new Vector2(114f, 44f));

        float colorY = sizesY - 58f;
        SetPos(root, "BandColor", new Vector2(0f, colorY), new Vector2(490f, 52f));
        SetPos(root, "ClearStrokes", new Vector2(0f, colorY - 64f), new Vector2(490f, 52f));
        SetPos(root, "Done", new Vector2(0f, colorY - 128f), new Vector2(490f, 56f));
    }

    private static void SetPos(Transform root, string childName, Vector2 position, Vector2 size)
    {
        Transform child = root.Find(childName);
        RectTransform rect = child as RectTransform;
        if (rect == null)
            return;

        rect.anchoredPosition = position;
        if (size.x > 0f)
            rect.sizeDelta = size;
    }
}
