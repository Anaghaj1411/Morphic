using System;
using System.Collections.Generic;
using Mediapipe.Unity.Sample.HandLandmarkDetection;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Enhancement-only colour workflow. It creates its own UI at runtime so the
// student's original scene objects, scripts, materials and prefabs stay intact.
[DefaultExecutionOrder(-200)]
public sealed class SculptingEnhancementLanternColorController : MonoBehaviour
{
    private enum ColorMode
    {
        Sculpt,
        SelectPart,
        SelectColor
    }

    private sealed class HitTarget
    {
        public Button Button;
        public RectTransform Rect;
        public Outline Outline;
        public bool IsEntryButton;
        public bool IsColor;
        public bool IsPersistentChoice;
        public Color PreviewColor;
        public Image[] DwellEdges;
    }

    [Header("References")]
    [SerializeField] private SculptHandLandmarkerRunner handRunner;
    [SerializeField] private SkinnedMeshRenderer lanternRenderer;
    [SerializeField] private TMP_Text gestureIndicator;
    [SerializeField] private TaiwanLanternGestureController originalGestureController;
    [SerializeField] private SculptingEnhancementIndexGestureController indexGestureController;

    [Header("Lantern Material Slots")]
    [Tooltip("Adjust these four indices in the enhancement scene if the imported FBX material order differs.")]
    [SerializeField, Min(0)] private int bodyMaterialIndex = 0;
    [SerializeField, Min(0)] private int topMaterialIndex = 1;
    [SerializeField, Min(0)] private int bottomMaterialIndex = 2;
    [SerializeField, Min(0)] private int tasselMaterialIndex = 3;

    [Header("Hand UI")]
    [SerializeField, Range(0.25f, 2.5f)] private float paletteDwellTime = 1.5f;
    [SerializeField, Range(0.25f, 2.5f)] private float selectionDwellTime = 1.5f;
    [SerializeField, Min(1f)] private float cursorSmoothing = 24f;

    private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
    private static readonly int ColorId = Shader.PropertyToID("_Color");

    private readonly Color[] paletteColors =
    {
        new Color32(213, 69, 58, 255),
        new Color32(232, 145, 48, 255),
        new Color32(232, 197, 73, 255),
        new Color32(70, 151, 91, 255),
        new Color32(55, 143, 172, 255),
        new Color32(73, 83, 153, 255),
        new Color32(151, 79, 139, 255),
        new Color32(235, 225, 205, 255)
    };

    private readonly string[] partNames =
    {
        "Whole Lantern",
        "Lantern Body",
        "Lantern Top",
        "Lantern Bottom",
        "Lantern Tassel"
    };

    private readonly List<HitTarget> hitTargets = new List<HitTarget>();
    private Material[] runtimeMaterials;
    private Color[] committedMaterialColors;

    private Canvas canvas;
    private RectTransform canvasRect;
    private Camera canvasCamera;
    private GameObject uiRoot;
    private GameObject paletteEntry;
    private GameObject panel;
    private GameObject partGroup;
    private GameObject colorGroup;
    private RectTransform cursorRect;
    private TMP_Text cursorText;
    private TMP_Text headerText;
    private TMP_Text headerBackText;
    private TMP_Text instructionText;
    private HitTarget hoveredTarget;
    private HitTarget dwellTarget;
    private HitTarget completedDwellTarget;
    private HitTarget selectedPartTarget;
    private HitTarget selectedColorTarget;
    private ColorMode mode = ColorMode.Sculpt;
    private int selectedPart = -1;
    private float dwellTimer;
    private bool colorsCaptured;
    private bool colorSelectionLocked;
    private bool partSelectionLocked;

    private void Start()
    {
        CaptureInitialColors();
        BuildInterface();
        ShowSculptMode();
    }

    private void OnEnable()
    {
        if (uiRoot != null)
            uiRoot.SetActive(true);
    }

    private void OnDisable()
    {
        RestoreSelectedPreview();
        SetSculptControllersEnabled(true);
        if (uiRoot != null)
            uiRoot.SetActive(false);
        mode = ColorMode.Sculpt;
    }

    private void Update()
    {
        if (uiRoot == null || handRunner == null || canvasRect == null)
            return;

        if (!handRunner.TryGetLatestHand(out SculptHandFrame hand))
        {
            SetCursorVisible(false);
            SetHoveredTarget(null);
            ResetDwell();
            return;
        }

        SetCursorVisible(true);
        Vector2 screenPosition = new Vector2(
            hand.IndexTip.x * Screen.width,
            hand.IndexTip.y * Screen.height);

        MoveCursor(screenPosition);
        HitTarget nextHovered = FindHitTarget(screenPosition);
        SetHoveredTarget(nextHovered);
        UpdateDwellSelection(nextHovered);
    }

    private void BuildInterface()
    {
        canvas = gestureIndicator != null
            ? gestureIndicator.GetComponentInParent<Canvas>()
            : FindFirstObjectByType<Canvas>();

        if (canvas == null)
        {
            Debug.LogWarning("MORPHIC Enhancement: No Canvas found for color UI.", this);
            return;
        }

        canvasRect = canvas.GetComponent<RectTransform>();
        canvasCamera = canvas.renderMode == RenderMode.ScreenSpaceOverlay
            ? null
            : canvas.worldCamera;

        uiRoot = new GameObject(
            "SculptingEnhancementColorUI",
            typeof(RectTransform));
        RectTransform rootRect = uiRoot.GetComponent<RectTransform>();
        rootRect.SetParent(canvas.transform, false);
        Stretch(rootRect);

        BuildPaletteEntry(rootRect);
        BuildColorPanel(rootRect);
        BuildCursor(rootRect);
        cursorRect.SetAsLastSibling();
    }

    private void BuildPaletteEntry(Transform parent)
    {
        paletteEntry = new GameObject("PaletteEntry", typeof(RectTransform));
        paletteEntry.transform.SetParent(parent, false);
        RectTransform holder = paletteEntry.GetComponent<RectTransform>();
        holder.anchorMin = holder.anchorMax = new Vector2(1f, 0.5f);
        holder.pivot = new Vector2(1f, 0.5f);
        holder.anchoredPosition = new Vector2(-24f, 0f);
        holder.sizeDelta = new Vector2(118f, 118f);

        Button button = CreateButton(
            "PaletteButton",
            holder,
            Vector2.zero,
            new Vector2(108f, 108f),
            "COLOR\nPALETTE",
            new Color32(28, 39, 54, 245));
        button.onClick.AddListener(EnterColorMode);

        RegisterHitTarget(button, true, false, false, default);
    }

    private void BuildColorPanel(Transform parent)
    {
        panel = new GameObject(
            "LanternColorPanel",
            typeof(RectTransform), typeof(Image), typeof(Outline));
        panel.transform.SetParent(parent, false);
        RectTransform panelRect = panel.GetComponent<RectTransform>();
        panelRect.anchorMin = panelRect.anchorMax = new Vector2(1f, 0.5f);
        panelRect.pivot = new Vector2(1f, 0.5f);
        panelRect.anchoredPosition = new Vector2(-24f, 0f);
        panelRect.sizeDelta = new Vector2(330f, 500f);
        panel.GetComponent<Image>().color = new Color32(18, 27, 39, 248);
        Outline outline = panel.GetComponent<Outline>();
        outline.effectColor = new Color32(82, 188, 189, 120);
        outline.effectDistance = new Vector2(1f, -1f);

        headerText = CreateText(
            "Header", panelRect, new Vector2(20f, -20f),
            new Vector2(194f, 34f), "COLOR MODE", 21f,
            TextAlignmentOptions.Left, FontStyles.Bold);
        Button headerBackButton = CreateButton(
            "HeaderBack", panelRect, new Vector2(112f, -13f),
            new Vector2(88f, 34f), "← BACK", new Color32(42, 56, 72, 255));
        headerBackText = headerBackButton.GetComponentInChildren<TMP_Text>();
        headerBackButton.onClick.AddListener(NavigateBack);
        RegisterHitTarget(headerBackButton, false, false, false, default);

        instructionText = CreateText(
            "Instruction", panelRect, new Vector2(20f, -58f),
            new Vector2(290f, 52f), "Choose what you want to color", 15f,
            TextAlignmentOptions.Left, FontStyles.Normal);
        instructionText.color = new Color32(173, 196, 207, 255);

        partGroup = new GameObject("PartSelection", typeof(RectTransform));
        partGroup.transform.SetParent(panelRect, false);
        RectTransform partRect = partGroup.GetComponent<RectTransform>();
        partRect.anchorMin = new Vector2(0f, 1f);
        partRect.anchorMax = new Vector2(1f, 1f);
        partRect.pivot = new Vector2(0.5f, 1f);
        partRect.anchoredPosition = new Vector2(0f, -120f);
        partRect.sizeDelta = new Vector2(-36f, 350f);

        for (int i = 0; i < partNames.Length; i++)
        {
            int partIndex = i;
            Button partButton = CreateButton(
                "Part" + i,
                partRect,
                new Vector2(0f, -i * 52f),
                new Vector2(294f, 44f),
                partNames[i],
                new Color32(42, 56, 72, 255));
            partButton.onClick.AddListener(() => SelectPart(partIndex));
            partButton.interactable = IsPartAvailable(i);
            RegisterHitTarget(partButton, false, false, true, default);
        }

        Button nextColorsButton = CreateButton(
            "NextColors", partRect, new Vector2(0f, -268f),
            new Vector2(294f, 44f), "NEXT: COLORS", new Color32(61, 124, 124, 255));
        nextColorsButton.onClick.AddListener(ContinueToColorSelection);
        RegisterHitTarget(nextColorsButton, false, false, false, default);

        colorGroup = new GameObject("ColorSelection", typeof(RectTransform));
        colorGroup.transform.SetParent(panelRect, false);
        RectTransform colorRect = colorGroup.GetComponent<RectTransform>();
        colorRect.anchorMin = new Vector2(0f, 1f);
        colorRect.anchorMax = new Vector2(1f, 1f);
        colorRect.pivot = new Vector2(0.5f, 1f);
        colorRect.anchoredPosition = new Vector2(0f, -125f);
        colorRect.sizeDelta = new Vector2(-36f, 270f);

        for (int i = 0; i < paletteColors.Length; i++)
        {
            Color color = paletteColors[i];
            int column = i % 4;
            int row = i / 4;
            Button swatch = CreateButton(
                "Color" + i,
                colorRect,
                new Vector2(-99f + column * 66f, -row * 72f),
                new Vector2(54f, 54f),
                string.Empty,
                color);
            swatch.onClick.AddListener(() => CommitColor(color));
            RegisterHitTarget(swatch, false, true, true, color);
        }

        Button backButton = CreateButton(
            "Back", colorRect, new Vector2(-74f, -172f),
            new Vector2(136f, 48f), "Choose Part", new Color32(42, 56, 72, 255));
        backButton.onClick.AddListener(ShowPartSelection);
        RegisterHitTarget(backButton, false, false, false, default);

        Button anotherButton = CreateButton(
            "AnotherPart", colorRect, new Vector2(74f, -172f),
            new Vector2(136f, 48f), "CHANGE COLOR", new Color32(61, 124, 124, 255));
        anotherButton.onClick.AddListener(UnlockColorSelection);
        RegisterHitTarget(anotherButton, false, false, false, default);

        Button doneButton = CreateButton(
            "Done", panelRect, new Vector2(0f, -438f),
            new Vector2(294f, 48f), "DONE", new Color32(74, 155, 143, 255));
        doneButton.onClick.AddListener(ExitColorMode);
        RegisterHitTarget(doneButton, false, false, false, default);
    }

    private void BuildCursor(Transform parent)
    {
        GameObject cursor = new GameObject("HandColorCursor", typeof(RectTransform));
        cursor.transform.SetParent(parent, false);
        cursorRect = cursor.GetComponent<RectTransform>();
        cursorRect.anchorMin = cursorRect.anchorMax = new Vector2(0.5f, 0.5f);
        cursorRect.pivot = new Vector2(0.5f, 0.5f);
        cursorRect.sizeDelta = new Vector2(46f, 46f);

        cursorText = CreateText(
            "CursorDot", cursorRect, Vector2.zero, new Vector2(46f, 46f),
            "●", 34f, TextAlignmentOptions.Center, FontStyles.Normal);
        Stretch(cursorText.rectTransform);
        cursorText.color = new Color32(77, 220, 203, 255);
        Outline outline = cursorText.gameObject.AddComponent<Outline>();
        outline.effectColor = new Color32(255, 255, 255, 230);
        outline.effectDistance = new Vector2(2f, -2f);
    }

    private void EnterColorMode()
    {
        if (mode != ColorMode.Sculpt)
            return;

        CaptureInitialColors();
        // Start with a useful target so a user can enter the palette and color
        // the lantern immediately without first discovering another required step.
        selectedPart = 0;
        colorSelectionLocked = false;
        partSelectionLocked = false;
        ClearSelectedColorVisual();
        SetSculptControllersEnabled(false);
        mode = ColorMode.SelectColor;
        paletteEntry.SetActive(false);
        panel.SetActive(true);
        partGroup.SetActive(false);
        colorGroup.SetActive(true);
        headerText.text = partNames[selectedPart];
        SetHeaderBackLabel("← PARTS");
        instructionText.text = "Point and hold until the border completes.";
        ResetDwell();
    }

    private void ExitColorMode()
    {
        RestoreSelectedPreview();
        ShowSculptMode();
        SetSculptControllersEnabled(true);
    }

    private void ShowSculptMode()
    {
        mode = ColorMode.Sculpt;
        selectedPart = -1;
        colorSelectionLocked = false;
        partSelectionLocked = false;
        ClearSelectedPartVisual();
        ClearSelectedColorVisual();
        if (panel != null)
            panel.SetActive(false);
        if (paletteEntry != null)
            paletteEntry.SetActive(true);
        ResetDwell();
        SetHoveredTarget(null);
    }

    private void ShowPartSelection()
    {
        RestoreSelectedPreview();
        colorSelectionLocked = false;
        partSelectionLocked = false;
        ClearSelectedColorVisual();
        ClearSelectedPartVisual();
        mode = ColorMode.SelectPart;
        partGroup.SetActive(true);
        colorGroup.SetActive(false);
        headerText.text = "SELECT PART";
        SetHeaderBackLabel("← SCULPT");
        instructionText.text = "Choose the whole lantern or one part.";
        SetHoveredTarget(null);
    }

    private void SelectPart(int partIndex)
    {
        if (!IsPartAvailable(partIndex))
            return;

        selectedPart = partIndex;
        partSelectionLocked = true;
        headerText.text = "PART SELECTED";
        instructionText.text = partNames[partIndex] + " is locked. Choose NEXT or DONE.";
        SetHoveredTarget(null);
    }

    private void ContinueToColorSelection()
    {
        if (selectedPart < 0)
            selectedPart = 0;

        partSelectionLocked = false;
        colorSelectionLocked = false;
        ClearSelectedColorVisual();
        mode = ColorMode.SelectColor;
        partGroup.SetActive(false);
        colorGroup.SetActive(true);
        headerText.text = partNames[selectedPart];
        SetHeaderBackLabel("← PARTS");
        instructionText.text = "Point and hold until the border completes.";
        SetHoveredTarget(null);
    }

    private void CommitColor(Color color)
    {
        if (selectedPart < 0)
            return;

        ApplyColor(selectedPart, color);
        CommitTargetColor(selectedPart, color);
        colorSelectionLocked = true;
        instructionText.text = "Color saved and locked. Choose an action below.";
    }

    private void NavigateBack()
    {
        if (mode == ColorMode.SelectColor)
        {
            ShowPartSelection();
            return;
        }

        if (mode == ColorMode.SelectPart)
            ExitColorMode();
    }

    private void SetHeaderBackLabel(string label)
    {
        if (headerBackText != null)
            headerBackText.text = label;
    }

    private void UnlockColorSelection()
    {
        colorSelectionLocked = false;
        ClearSelectedColorVisual();
        SetHoveredTarget(null);
        instructionText.text = "Point and hold until the border completes.";
    }

    private void SetSculptControllersEnabled(bool enabled)
    {
        if (originalGestureController != null)
            originalGestureController.enabled = enabled;
        if (indexGestureController != null)
            indexGestureController.enabled = enabled;
    }

    private void CaptureInitialColors()
    {
        if (colorsCaptured || lanternRenderer == null)
            return;

        // Renderer.materials creates runtime-only material instances. Changing them
        // cannot modify the student's original material assets.
        runtimeMaterials = lanternRenderer.materials;
        committedMaterialColors = new Color[runtimeMaterials.Length];

        for (int i = 0; i < runtimeMaterials.Length; i++)
        {
            Material material = runtimeMaterials[i];
            if (material == null)
            {
                committedMaterialColors[i] = Color.white;
                continue;
            }

            if (material.HasProperty(BaseColorId))
                committedMaterialColors[i] = material.GetColor(BaseColorId);
            else if (material.HasProperty(ColorId))
                committedMaterialColors[i] = material.GetColor(ColorId);
            else
                committedMaterialColors[i] = Color.white;
        }

        colorsCaptured = true;
    }

    private void ApplyColor(int partIndex, Color color)
    {
        if (!colorsCaptured || runtimeMaterials == null || partIndex < 0)
            return;

        if (partIndex == 0)
        {
            for (int i = 0; i < runtimeMaterials.Length; i++)
                SetMaterialColor(i, color);
            return;
        }

        int materialIndex = GetMaterialIndex(partIndex);
        SetMaterialColor(materialIndex, color);
    }

    private void SetMaterialColor(int materialIndex, Color color)
    {
        if (runtimeMaterials == null || materialIndex < 0 ||
            materialIndex >= runtimeMaterials.Length)
            return;

        Material material = runtimeMaterials[materialIndex];
        if (material == null)
            return;

        if (material.HasProperty(BaseColorId))
            material.SetColor(BaseColorId, color);
        if (material.HasProperty(ColorId))
            material.SetColor(ColorId, color);
    }

    private void CommitTargetColor(int partIndex, Color color)
    {
        if (committedMaterialColors == null)
            return;

        if (partIndex == 0)
        {
            for (int i = 0; i < committedMaterialColors.Length; i++)
                committedMaterialColors[i] = color;
            return;
        }

        int materialIndex = GetMaterialIndex(partIndex);
        if (materialIndex >= 0 && materialIndex < committedMaterialColors.Length)
            committedMaterialColors[materialIndex] = color;
    }

    private void RestoreSelectedPreview()
    {
        if (selectedPart < 0 || committedMaterialColors == null)
            return;

        if (selectedPart == 0)
        {
            for (int i = 0; i < committedMaterialColors.Length; i++)
                SetMaterialColor(i, committedMaterialColors[i]);
            return;
        }

        int materialIndex = GetMaterialIndex(selectedPart);
        if (materialIndex >= 0 && materialIndex < committedMaterialColors.Length)
            SetMaterialColor(materialIndex, committedMaterialColors[materialIndex]);
    }

    private void MoveCursor(Vector2 screenPosition)
    {
        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
                canvasRect, screenPosition, canvasCamera, out Vector2 localPoint))
            return;

        float factor = 1f - Mathf.Exp(-cursorSmoothing * Time.unscaledDeltaTime);
        cursorRect.anchoredPosition = Vector2.Lerp(
            cursorRect.anchoredPosition, localPoint, factor);
        cursorRect.SetAsLastSibling();
    }

    private HitTarget FindHitTarget(Vector2 screenPosition)
    {
        for (int i = hitTargets.Count - 1; i >= 0; i--)
        {
            HitTarget target = hitTargets[i];
            if (target.Button == null || !target.Button.IsInteractable() ||
                !target.Button.gameObject.activeInHierarchy)
                continue;

            if (colorSelectionLocked && target.IsColor)
                continue;

            if (partSelectionLocked && target.IsPersistentChoice && !target.IsColor)
                continue;

            if (RectTransformUtility.RectangleContainsScreenPoint(
                    target.Rect, screenPosition, canvasCamera))
                return target;
        }

        return null;
    }

    private void SetHoveredTarget(HitTarget target)
    {
        if (hoveredTarget == target)
            return;

        if (hoveredTarget != null && hoveredTarget.Outline != null)
            hoveredTarget.Outline.enabled = false;

        if (!colorSelectionLocked && mode == ColorMode.SelectColor && selectedPart >= 0 &&
            (target == null || !target.IsColor))
            RestoreSelectedPreview();

        hoveredTarget = target;
        if (hoveredTarget != null && hoveredTarget.Outline != null)
            hoveredTarget.Outline.enabled = true;

        if (!colorSelectionLocked && mode == ColorMode.SelectColor && selectedPart >= 0 &&
            hoveredTarget != null && hoveredTarget.IsColor)
            ApplyColor(selectedPart, hoveredTarget.PreviewColor);
    }

    private void UpdateDwellSelection(HitTarget target)
    {
        if (target == null)
        {
            ResetDwell();
            return;
        }

        if (dwellTarget != target)
        {
            ResetDwell();
            dwellTarget = target;
        }

        if (completedDwellTarget == target)
            return;

        float requiredTime = target.IsEntryButton
            ? paletteDwellTime
            : selectionDwellTime;
        dwellTimer += Time.unscaledDeltaTime;
        SetDwellProgress(target, Mathf.Clamp01(dwellTimer / requiredTime));

        if (dwellTimer < requiredTime)
            return;

        completedDwellTarget = target;
        if (target.IsPersistentChoice)
            SetSelectedChoice(target);
        target.Button.onClick.Invoke();
    }

    private void ResetDwell()
    {
        if (dwellTarget != null)
            SetDwellProgress(dwellTarget, 0f);
        dwellTimer = 0f;
        dwellTarget = null;
        completedDwellTarget = null;
    }

    private static void SetDwellProgress(HitTarget target, float progress)
    {
        if (target == null || target.DwellEdges == null)
            return;

        for (int i = 0; i < target.DwellEdges.Length; i++)
        {
            Image edge = target.DwellEdges[i];
            if (edge == null)
                continue;
            edge.fillAmount = Mathf.Clamp01(progress * 4f - i);
            edge.enabled = edge.fillAmount > 0f;
        }
    }

    private bool IsPartAvailable(int partIndex)
    {
        if (lanternRenderer == null)
            return false;

        int materialCount = runtimeMaterials != null
            ? runtimeMaterials.Length
            : lanternRenderer.sharedMaterials.Length;

        if (partIndex == 0)
            return materialCount > 0;

        int materialIndex = GetMaterialIndex(partIndex);
        return materialIndex >= 0 && materialIndex < materialCount;
    }

    private int GetMaterialIndex(int partIndex)
    {
        switch (partIndex)
        {
            case 1: return bodyMaterialIndex;
            case 2: return topMaterialIndex;
            case 3: return bottomMaterialIndex;
            case 4: return tasselMaterialIndex;
            default: return -1;
        }
    }

    private void RegisterHitTarget(
        Button button,
        bool isEntry,
        bool isColor,
        bool isPersistentChoice,
        Color previewColor)
    {
        Outline outline = button.gameObject.GetComponent<Outline>();
        if (outline == null)
            outline = button.gameObject.AddComponent<Outline>();
        outline.effectColor = new Color32(77, 220, 203, 255);
        outline.effectDistance = new Vector2(4f, -4f);
        outline.enabled = false;

        Image[] dwellEdges = CreateDwellFrame(button.transform);

        hitTargets.Add(new HitTarget
        {
            Button = button,
            Rect = button.GetComponent<RectTransform>(),
            Outline = outline,
            IsEntryButton = isEntry,
            IsColor = isColor,
            IsPersistentChoice = isPersistentChoice,
            PreviewColor = previewColor,
            DwellEdges = dwellEdges
        });
    }

    private void SetSelectedChoice(HitTarget target)
    {
        if (target == null)
            return;

        if (target.IsColor)
        {
            ClearSelectedColorVisual();
            selectedColorTarget = target;
            selectedColorTarget.Rect.localScale = Vector3.one * 1.16f;
        }
        else
        {
            ClearSelectedPartVisual();
            selectedPartTarget = target;
            selectedPartTarget.Rect.localScale = Vector3.one * 1.08f;
        }
    }

    private void ClearSelectedPartVisual()
    {
        if (selectedPartTarget != null && selectedPartTarget.Rect != null)
            selectedPartTarget.Rect.localScale = Vector3.one;
        selectedPartTarget = null;
    }

    private void ClearSelectedColorVisual()
    {
        if (selectedColorTarget != null && selectedColorTarget.Rect != null)
            selectedColorTarget.Rect.localScale = Vector3.one;
        selectedColorTarget = null;
    }

    private Image[] CreateDwellFrame(Transform parent)
    {
        Color progressColor = new Color32(255, 224, 92, 255);
        Image[] edges = new Image[4];

        edges[0] = CreateDwellEdge(
            "DwellTop", parent,
            new Vector2(0f, 1f), new Vector2(1f, 1f),
            new Vector2(-8f, 4f), new Vector2(0f, 4f),
            Image.FillMethod.Horizontal, (int)Image.OriginHorizontal.Left,
            progressColor);
        edges[1] = CreateDwellEdge(
            "DwellRight", parent,
            new Vector2(1f, 0f), new Vector2(1f, 1f),
            new Vector2(4f, -8f), new Vector2(4f, 0f),
            Image.FillMethod.Vertical, (int)Image.OriginVertical.Top,
            progressColor);
        edges[2] = CreateDwellEdge(
            "DwellBottom", parent,
            new Vector2(0f, 0f), new Vector2(1f, 0f),
            new Vector2(-8f, 4f), new Vector2(0f, -4f),
            Image.FillMethod.Horizontal, (int)Image.OriginHorizontal.Right,
            progressColor);
        edges[3] = CreateDwellEdge(
            "DwellLeft", parent,
            new Vector2(0f, 0f), new Vector2(0f, 1f),
            new Vector2(4f, -8f), new Vector2(-4f, 0f),
            Image.FillMethod.Vertical, (int)Image.OriginVertical.Bottom,
            progressColor);

        return edges;
    }

    private Image CreateDwellEdge(
        string objectName,
        Transform parent,
        Vector2 anchorMin,
        Vector2 anchorMax,
        Vector2 sizeDelta,
        Vector2 anchoredPosition,
        Image.FillMethod fillMethod,
        int fillOrigin,
        Color color)
    {
        GameObject edgeObject = new GameObject(
            objectName, typeof(RectTransform), typeof(Image));
        edgeObject.transform.SetParent(parent, false);
        RectTransform rect = edgeObject.GetComponent<RectTransform>();
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = sizeDelta;
        rect.anchoredPosition = anchoredPosition;

        Image edge = edgeObject.GetComponent<Image>();
        edge.color = color;
        edge.raycastTarget = false;
        edge.type = Image.Type.Filled;
        edge.fillMethod = fillMethod;
        edge.fillOrigin = fillOrigin;
        edge.fillAmount = 0f;
        edge.enabled = false;
        return edge;
    }

    private Button CreateButton(
        string objectName,
        Transform parent,
        Vector2 anchoredPosition,
        Vector2 size,
        string label,
        Color color)
    {
        GameObject buttonObject = new GameObject(
            objectName,
            typeof(RectTransform), typeof(Image), typeof(Button));
        buttonObject.transform.SetParent(parent, false);
        RectTransform rect = buttonObject.GetComponent<RectTransform>();
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 1f);
        rect.pivot = new Vector2(0.5f, 1f);
        rect.anchoredPosition = anchoredPosition;
        rect.sizeDelta = size;
        Image image = buttonObject.GetComponent<Image>();
        image.color = color;

        Button button = buttonObject.GetComponent<Button>();
        ColorBlock colors = button.colors;
        colors.highlightedColor = Color.Lerp(color, Color.white, 0.18f);
        colors.pressedColor = Color.Lerp(color, Color.black, 0.15f);
        colors.disabledColor = new Color(color.r, color.g, color.b, 0.22f);
        button.colors = colors;

        if (!string.IsNullOrEmpty(label))
        {
            TMP_Text text = CreateText(
                "Label", rect, Vector2.zero, size - new Vector2(12f, 8f),
                label, 15f, TextAlignmentOptions.Center, FontStyles.Bold);
            Stretch(text.rectTransform);
            text.raycastTarget = false;
        }

        return button;
    }

    private TMP_Text CreateText(
        string objectName,
        Transform parent,
        Vector2 anchoredPosition,
        Vector2 size,
        string content,
        float fontSize,
        TextAlignmentOptions alignment,
        FontStyles style)
    {
        GameObject textObject = new GameObject(
            objectName,
            typeof(RectTransform), typeof(TextMeshProUGUI));
        textObject.transform.SetParent(parent, false);
        RectTransform rect = textObject.GetComponent<RectTransform>();
        rect.anchorMin = rect.anchorMax = new Vector2(0f, 1f);
        rect.pivot = new Vector2(0f, 1f);
        rect.anchoredPosition = anchoredPosition;
        rect.sizeDelta = size;

        TextMeshProUGUI text = textObject.GetComponent<TextMeshProUGUI>();
        if (gestureIndicator != null)
            text.font = gestureIndicator.font;
        text.text = content;
        text.fontSize = fontSize;
        text.fontStyle = style;
        text.alignment = alignment;
        text.color = Color.white;
        text.textWrappingMode = TextWrappingModes.Normal;
        text.raycastTarget = false;
        return text;
    }

    private Image CreateImage(
        string objectName,
        Transform parent,
        Vector2 anchoredPosition,
        Vector2 size,
        Color color)
    {
        GameObject imageObject = new GameObject(
            objectName,
            typeof(RectTransform), typeof(Image));
        imageObject.transform.SetParent(parent, false);
        RectTransform rect = imageObject.GetComponent<RectTransform>();
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = anchoredPosition;
        rect.sizeDelta = size;
        Image image = imageObject.GetComponent<Image>();
        image.color = color;
        image.raycastTarget = false;
        return image;
    }

    private void SetCursorVisible(bool visible)
    {
        if (cursorRect != null && cursorRect.gameObject.activeSelf != visible)
            cursorRect.gameObject.SetActive(visible);
    }

    private static void Stretch(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
    }
}
