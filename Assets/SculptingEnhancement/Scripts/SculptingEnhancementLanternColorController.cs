using System;
using System.Collections.Generic;
using Mediapipe.Unity.Sample.HandLandmarkDetection;
using Mediapipe.Unity.Sample;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Enhancement-only colour workflow. The UI is stored under the scene Canvas so
// designers can inspect and adjust every panel and button in the Hierarchy.
[DefaultExecutionOrder(200)]
public sealed class SculptingEnhancementLanternColorController : MonoBehaviour
{
    private enum ColorMode
    {
        Sculpt,
        MainMenu,
        SelectPart,
        SelectVariant,
        SelectColor,
        Selfie
    }

    private sealed class HitTarget
    {
        public Button Button;
        public RectTransform Rect;
        public Outline Outline;
        public bool IsColor;
        public bool IsFreeColorPalette;
        public bool IsPersistentChoice;
        public Color PreviewColor;
        public Color BaseColor;
        public RectTransform SelectionCheck;
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
    [SerializeField, Range(0.25f, 2.5f)] private float selectionDwellTime = 1.5f;
    [SerializeField, Min(1f)] private float cursorSmoothing = 24f;
    [Tooltip("Expands hand motion around the camera center so the full screen can be reached without moving the whole hand out of frame.")]
    [SerializeField, Range(1f, 2.5f)] private float cameraToScreenRangeMultiplier = 1.8f;

    [Header("Selfie Decals")]
    [Tooltip("Normalized fraction of lantern height occupied by each selfie; it scales with Height deformation.")]
    [SerializeField, Range(0.2f, 0.7f)] private float selfieHeightFraction = 0.42f;
    [Tooltip("Normalized circumference width; it scales with Width deformation while leaving gaps between copies.")]
    [SerializeField, Range(0.45f, 1.2f)] private float selfieWidthToRadius = 0.9f;
    [Tooltip("Scales the complete attached selfie on the lantern in both width and height. 1.0 is the original size; 1.5 makes it 50% larger.")]
    [InspectorName("Selfie Attached Image Scale")]
    [SerializeField, Range(0.5f, 3f)] private float selfieAttachedImageScale = 1.5f;
    [SerializeField, Range(1.001f, 1.08f)] private float selfieSurfaceOffset = 1.018f;
    [Tooltip("Controls the strength and depth of the irregular brush-bristle edge. Increase this for a more jagged outline.")]
    [SerializeField, Range(0f, 0.4f)] private float selfieBrushRoughness = 0.085f;
    [Tooltip("Cuts the four corners inward to form a broad horizontal paint stroke. Increase this for stronger corner removal.")]
    [InspectorName("Selfie Brush Corner Cut")]
    [SerializeField, Range(0f, 0.8f)] private float selfieBrushCornerRoundness = 0.28f;

    private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
    private static readonly int ColorId = Shader.PropertyToID("_Color");
    private static readonly int BrushEdgeRoughnessId =
        Shader.PropertyToID("_BrushEdgeRoughness");
    private static readonly int BrushCornerRoundnessId =
        Shader.PropertyToID("_BrushCornerRoundness");

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
    private int[][] partMaterialIndices;
    private Mesh runtimePartMesh;
    private readonly List<Material> isolatedLidMaterials = new List<Material>();
    private readonly List<Material> isolatedTasselMaterials = new List<Material>();

    private Canvas canvas;
    private RectTransform canvasRect;
    private Camera canvasCamera;
    private GameObject uiRoot;
    private GameObject panel;
    private GameObject mainActionGroup;
    private GameObject partGroup;
    private GameObject variantGroup;
    private GameObject colorGroup;
    private GameObject selfiePanel;
    private GameObject selfieGroup;
    private Button selfieEntryButton;
    private RectTransform cursorRect;
    private TMP_Text cursorText;
    private TMP_Text headerText;
    private TMP_Text headerBackText;
    private TMP_Text instructionText;
    private TMP_Text selfieHeaderText;
    private TMP_Text selfieInstructionText;
    private RawImage freeColorPaletteImage;
    private RawImage selfiePreviewImage;
    private Button takeSelfieButton;
    private Button selfieBackButton;
    private Button attachSelfieButton;
    private Button retakeSelfieButton;
    private Button selfieDoneButton;
    private RectTransform freeColorSelectionCheck;
    private HitTarget hoveredTarget;
    private HitTarget dwellTarget;
    private HitTarget completedDwellTarget;
    private HitTarget selectedPartTarget;
    private HitTarget selectedColorTarget;
    private ColorMode mode = ColorMode.Sculpt;
    private int selectedPart = -1;
    private int selectedVariant = -1;
    private string selectedVariantLabel = "";
    private bool popGestureArmed;
    private readonly Button[] variantButtons = new Button[2];
    private readonly TMP_Text[] variantButtonLabels = new TMP_Text[2];
    private float dwellTimer;
    private bool colorsCaptured;
    private bool colorSelectionLocked;
    private bool partSelectionLocked;
    private bool paletteAvailable;
    private bool uiPointerBlockingSculpt;
    private Color freePalettePreviewColor = Color.white;
    private Vector2 freePaletteMarkerPosition;
    private Texture2D capturedSelfieTexture;
    private Texture2D appliedSelfieTexture;
    private Material selfieSurfaceMaterial;
    private Material selfieHiddenMaterial;
    private SkinnedMeshRenderer selfieSurfaceRenderer;
    private Transform selfieSurfaceRoot;
    private Mesh selfieBoundsMesh;
    private Material selfieDecalMaterial;
    private Mesh selfieQuadMesh;
    private Mesh selfieBoundsBakeMesh;
    private Transform selfieDecalRoot;
    private MeshFilter selfieDecalFilter;
    private MeshRenderer selfieDecalRenderer;

    private const float MainMenuPanelHeight = 230f;
    private const float PartSelectionPanelHeight = 500f;
    private const float VariantSelectionPanelHeight = 320f;
    private const float ColorSelectionPanelHeight = 585f;
    private const float SelfiePanelHeight = 515f;

    public void SetPaletteAvailable(bool available)
    {
        paletteAvailable = available;

        if (uiRoot == null)
            return;

        if (!available)
        {
            RestoreSelectedPreview();
            SetSculptControllersEnabled(true);
            ShowSculptMode();
            return;
        }

        ShowMainMenu();
    }

    private void Start()
    {
        CaptureInitialColors();
        BuildInterface();

        // DialogueManager can enable the palette before this component's
        // inactive GameObject receives its first Start call. Preserve that
        // request instead of hiding the panel again during initialization.
        if (paletteAvailable)
            ShowMainMenu();
        else
            ShowSculptMode();
    }

    private void OnEnable()
    {
        if (uiRoot != null)
        {
            uiRoot.SetActive(true);
            if (paletteAvailable)
                ShowMainMenu();
            else
                ShowSculptMode();
        }
    }

    private void OnDisable()
    {
        RestoreSelectedPreview();
        SetSculptControllersEnabled(true);
        ShowSculptMode();
        if (uiRoot != null)
            uiRoot.SetActive(false);
    }

    private void OnDestroy()
    {
        if (selfieSurfaceRoot != null)
            DestroyRuntimeObject(selfieSurfaceRoot.gameObject);
        DestroyRuntimeObject(capturedSelfieTexture);
        DestroyRuntimeObject(appliedSelfieTexture);

        DestroyRuntimeObject(selfieDecalMaterial);
        DestroyRuntimeObject(selfieQuadMesh);
        DestroyRuntimeObject(selfieBoundsBakeMesh);
        DestroyRuntimeObject(runtimePartMesh);
        for (int i = 0; i < isolatedLidMaterials.Count; i++)
            DestroyRuntimeObject(isolatedLidMaterials[i]);
        isolatedLidMaterials.Clear();
        for (int i = 0; i < isolatedTasselMaterials.Count; i++)
            DestroyRuntimeObject(isolatedTasselMaterials[i]);
        isolatedTasselMaterials.Clear();

        DestroyRuntimeObject(selfieSurfaceMaterial);
        DestroyRuntimeObject(selfieHiddenMaterial);
        DestroyRuntimeObject(selfieBoundsMesh);

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
        Vector2 expandedIndexPosition = ExpandCameraPoint(
            new Vector2(hand.IndexTip.x, hand.IndexTip.y),
            cameraToScreenRangeMultiplier);
        Vector2 screenPosition = new Vector2(
            expandedIndexPosition.x * Screen.width,
            expandedIndexPosition.y * Screen.height);

        MoveCursor(screenPosition);
        // Use the same smoothed cursor position for hit testing, preview,
        // selection and the saved marker. This keeps the chosen color aligned
        // with the cursor the user actually sees on screen.
        Vector2 interactionScreenPosition = RectTransformUtility.WorldToScreenPoint(
            canvasCamera, cursorRect.position);
        HitTarget nextHovered = FindHitTarget(interactionScreenPosition);

        // A hand/face pointer must dwell over a button before it clicks. Stop
        // all shape recognition for that entire dwell interval so the pointing
        // pose cannot be misclassified as a sculpt gesture. Keep the lock when
        // the click opens another panel; Main Menu restores sculpting only
        // after the pointer has actually left the old button.
        if (mode == ColorMode.MainMenu)
        {
            bool shouldBlockSculpt = nextHovered != null;
            if (uiPointerBlockingSculpt != shouldBlockSculpt)
            {
                uiPointerBlockingSculpt = shouldBlockSculpt;
                SetSculptControllersEnabled(!uiPointerBlockingSculpt);
            }
        }

        SetHoveredTarget(nextHovered);
        UpdateFreePalettePreview(nextHovered, interactionScreenPosition);
        UpdateDwellSelection(nextHovered);
        UpdateSculptLockFromUi(IsCursorOverColorUi(interactionScreenPosition));
    }

    private void LateUpdate()
    {
        if (selfieSurfaceRenderer == null || lanternRenderer == null)
            return;

        // Run after both sculpting controllers (orders 0 and 100), then copy
        // the exact BlendShape state used by the visible lantern this frame.
        // This makes the overlay deform as part of the same surface instead of
        // lagging one frame and disappearing inside the lantern while shaping.
        SyncSelfieSurfaceRenderer();

        // Width/Height/Top/Bottom can change the true body centre and height.
        // Recalculate the selfie projection from the current baked geometry so
        // it remains centred continuously, not only when ATTACH is pressed.
        if (appliedSelfieTexture != null)
            ConfigureSelfieSurfaceMaterial(appliedSelfieTexture);
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

        Transform existingRoot = canvas.transform.Find("SculptingEnhancementColorUI");
        if (existingRoot != null && BindExistingInterface(existingRoot))
            return;

        if (existingRoot != null)
        {
            if (Application.isPlaying)
                Destroy(existingRoot.gameObject);
            else
                DestroyImmediate(existingRoot.gameObject);
        }

        BuildInterfaceHierarchy();
    }

    private void BuildInterfaceHierarchy()
    {
        hitTargets.Clear();

        uiRoot = new GameObject(
            "SculptingEnhancementColorUI",
            typeof(RectTransform));
        RectTransform rootRect = uiRoot.GetComponent<RectTransform>();
        rootRect.SetParent(canvas.transform, false);
        Stretch(rootRect);

        BuildColorPanel(rootRect);
        BuildSelfieInterface(rootRect);
        BuildCursor(rootRect);
        ApplyCenteredTypography();
        cursorRect.SetAsLastSibling();
    }

    private bool BindExistingInterface(Transform root)
    {
        hitTargets.Clear();
        uiRoot = root.gameObject;
        panel = FindObject(root, "LanternColorPanel");
        mainActionGroup = FindObject(root, "LanternColorPanel/MainActions");
        partGroup = FindObject(root, "LanternColorPanel/PartSelection");
        colorGroup = FindObject(root, "LanternColorPanel/ColorSelection");
        selfiePanel = FindObject(root, "LanternSelfiePanel");
        selfieGroup = FindObject(root, "LanternSelfiePanel/Content");
        selfieEntryButton = FindComponent<Button>(root, "SelfieEntry");
        headerText = FindComponent<TMP_Text>(root, "LanternColorPanel/Header");
        instructionText = FindComponent<TMP_Text>(root, "LanternColorPanel/Instruction");
        selfieHeaderText = FindComponent<TMP_Text>(root, "LanternSelfiePanel/Header");
        selfieInstructionText = FindComponent<TMP_Text>(root, "LanternSelfiePanel/Instruction");
        freeColorPaletteImage = FindComponent<RawImage>(
            root, "LanternColorPanel/ColorSelection/FreeColorPalette");
        freeColorSelectionCheck = FindComponent<RectTransform>(
            root, "LanternColorPanel/ColorSelection/FreeColorPalette/SelectionCheck");
        selfiePreviewImage = FindComponent<RawImage>(
            root, "LanternSelfiePanel/Content/Preview");
        cursorRect = FindComponent<RectTransform>(root, "HandColorCursor");
        cursorText = FindComponent<TMP_Text>(root, "HandColorCursor/CursorDot");

        Button selectPartButton = FindComponent<Button>(root, "LanternColorPanel/MainActions/SelectPart");
        Button changeColorButton = FindComponent<Button>(root, "LanternColorPanel/MainActions/ChangeColor");
        Button nextColorsButton = FindComponent<Button>(root, "LanternColorPanel/PartSelection/NextColors");
        Button partDoneButton = FindComponent<Button>(root, "LanternColorPanel/PartSelection/Done");
        Button freeColorPaletteButton = FindComponent<Button>(
            root, "LanternColorPanel/ColorSelection/FreeColorPalette");
        Button backButton = FindComponent<Button>(root, "LanternColorPanel/ColorSelection/Back");
        Button colorDoneButton = FindComponent<Button>(
            root, "LanternColorPanel/ColorSelection/Done");
        takeSelfieButton = FindComponent<Button>(root, "LanternSelfiePanel/Content/TakeSelfie");
        selfieBackButton = FindComponent<Button>(root, "LanternSelfiePanel/Content/Back");
        attachSelfieButton = FindComponent<Button>(root, "LanternSelfiePanel/Content/AttachToLantern");
        retakeSelfieButton = FindComponent<Button>(root, "LanternSelfiePanel/Content/Retake");
        selfieDoneButton = FindComponent<Button>(root, "LanternSelfiePanel/Content/Done");

        if (panel == null || mainActionGroup == null ||
            partGroup == null || colorGroup == null || headerText == null ||
            instructionText == null || freeColorPaletteImage == null || selfiePanel == null ||
            selfieGroup == null || selfieEntryButton == null ||
            selfieHeaderText == null || selfieInstructionText == null ||
            freeColorSelectionCheck == null || cursorRect == null || cursorText == null ||
            selectPartButton == null ||
            changeColorButton == null || nextColorsButton == null ||
            partDoneButton == null || freeColorPaletteButton == null ||
            backButton == null || colorDoneButton == null || selfiePreviewImage == null ||
            takeSelfieButton == null || selfieBackButton == null ||
            attachSelfieButton == null ||
            retakeSelfieButton == null || selfieDoneButton == null)
        {
            Debug.LogWarning(
                "MORPHIC Enhancement: Scene color UI is incomplete; rebuilding it.",
                this);
            return false;
        }

        ApplyBottomRightDocking();

        selectPartButton.onClick.AddListener(ShowPartSelection);
        RegisterHitTarget(selectPartButton, false, false, default);
        changeColorButton.onClick.AddListener(ContinueToColorSelection);
        RegisterHitTarget(changeColorButton, false, false, default);
        selfieEntryButton.onClick.AddListener(ShowSelfieCapture);
        RegisterHitTarget(selfieEntryButton, false, false, default);

        for (int i = 0; i < partNames.Length; i++)
        {
            int partIndex = i;
            Button partButton = FindComponent<Button>(
                root, "LanternColorPanel/PartSelection/Part" + i);
            if (partButton == null)
                return false;
            partButton.onClick.AddListener(() => SelectPart(partIndex));
            partButton.interactable = IsPartAvailable(i);
            RegisterHitTarget(partButton, false, true, default);
        }

        nextColorsButton.onClick.AddListener(ContinueToColorSelection);
        RegisterHitTarget(nextColorsButton, false, false, default);
        partDoneButton.onClick.AddListener(ShowMainMenu);
        RegisterHitTarget(partDoneButton, false, false, default);

        for (int i = 0; i < paletteColors.Length; i++)
        {
            Color color = paletteColors[i];
            Button swatch = FindComponent<Button>(
                root, "LanternColorPanel/ColorSelection/Color" + i);
            if (swatch == null)
                return false;
            swatch.onClick.AddListener(() => CommitColor(color));
            RegisterHitTarget(swatch, true, true, color);
        }

        ConfigureFreeColorPalette(freeColorPaletteImage);
        freeColorPaletteButton.onClick.AddListener(CommitFreePaletteColor);
        RegisterHitTarget(
            freeColorPaletteButton, true, true, Color.white, true);
        backButton.onClick.AddListener(NavigateBack);
        RegisterHitTarget(backButton, false, false, default);
        colorDoneButton.onClick.AddListener(ShowMainMenu);
        RegisterHitTarget(colorDoneButton, false, false, default);

        BindSelfieButtons();
        ApplyCenteredTypography();
        EnsureVariantGroup();
        return true;
    }

    private static GameObject FindObject(Transform root, string path)
    {
        Transform child = root.Find(path);
        return child != null ? child.gameObject : null;
    }

    private static T FindComponent<T>(Transform root, string path)
        where T : Component
    {
        Transform child = root.Find(path);
        return child != null ? child.GetComponent<T>() : null;
    }

    private void BuildColorPanel(Transform parent)
    {
        panel = new GameObject(
            "LanternColorPanel",
            typeof(RectTransform), typeof(Image), typeof(Outline));
        panel.transform.SetParent(parent, false);
        RectTransform panelRect = panel.GetComponent<RectTransform>();
        panelRect.anchorMin = panelRect.anchorMax = new Vector2(1f, 0f);
        panelRect.pivot = new Vector2(1f, 0f);
        panelRect.anchoredPosition = new Vector2(-24f, 24f);
        panelRect.sizeDelta = new Vector2(330f, MainMenuPanelHeight);
        panel.GetComponent<Image>().color = new Color32(18, 27, 39, 248);
        Outline outline = panel.GetComponent<Outline>();
        outline.effectColor = new Color32(82, 188, 189, 120);
        outline.effectDistance = new Vector2(1f, -1f);

        GameObject layoutMarker = new GameObject(
            "CenteredCompactLayoutV8", typeof(RectTransform));
        layoutMarker.transform.SetParent(panelRect, false);

            headerText = CreateText(
            "Header", panelRect, new Vector2(20f, -20f),
            new Vector2(194f, 34f), "COLOR MODE", 21f,
            TextAlignmentOptions.Left, FontStyles.Bold);

        Button headerBackButton = CreateButton(
            "HeaderBack", panelRect, new Vector2(112f, -13f),
            new Vector2(88f, 34f), "← BACK",
            new Color32(42, 56, 72, 255));

        headerBackText = headerBackButton.GetComponentInChildren<TMP_Text>();
        headerBackButton.onClick.AddListener(NavigateBack);
        RegisterHitTarget(headerBackButton, false, false, default);


        instructionText = CreateText(
            "Instruction", panelRect, new Vector2(20f, -58f),
            new Vector2(290f, 52f), "Choose what you want to color", 15f,
            TextAlignmentOptions.Center, FontStyles.Normal);
        instructionText.color = new Color32(173, 196, 207, 255);

        mainActionGroup = new GameObject("MainActions", typeof(RectTransform));
        mainActionGroup.transform.SetParent(panelRect, false);
        RectTransform mainRect = mainActionGroup.GetComponent<RectTransform>();
        mainRect.anchorMin = new Vector2(0f, 1f);
        mainRect.anchorMax = new Vector2(1f, 1f);
        mainRect.pivot = new Vector2(0.5f, 1f);
        mainRect.anchoredPosition = new Vector2(0f, -82f);
        mainRect.sizeDelta = new Vector2(-36f, 150f);

        Button selectPartButton = CreateButton(
            "SelectPart", mainRect, Vector2.zero,
            new Vector2(294f, 56f), "SELECT PART", new Color32(42, 56, 72, 255));
        selectPartButton.onClick.AddListener(ShowPartSelection);
        RegisterHitTarget(selectPartButton, false, false, default);

        Button changeColorButton = CreateButton(
            "ChangeColor", mainRect, new Vector2(0f, -72f),
            new Vector2(294f, 56f), "CHANGE COLOR", new Color32(61, 124, 124, 255));
        changeColorButton.onClick.AddListener(ContinueToColorSelection);
        RegisterHitTarget(changeColorButton, false, false, default);

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
            RegisterHitTarget(partButton, false, true, default);
        }

        Button nextColorsButton = CreateButton(
            "NextColors", partRect, new Vector2(0f, -268f),
            new Vector2(294f, 44f), "CHANGE COLOR", new Color32(61, 124, 124, 255));
        nextColorsButton.onClick.AddListener(ContinueToColorSelection);
        RegisterHitTarget(nextColorsButton, false, false, default);

        Button partDoneButton = CreateButton(
            "Done", partRect, new Vector2(0f, -320f),
            new Vector2(294f, 44f), "DONE", new Color32(74, 155, 143, 255));
        partDoneButton.onClick.AddListener(ShowMainMenu);
        RegisterHitTarget(partDoneButton, false, false, default);
        EnsureVariantGroup();

        colorGroup = new GameObject("ColorSelection", typeof(RectTransform));
        colorGroup.transform.SetParent(panelRect, false);
        RectTransform colorRect = colorGroup.GetComponent<RectTransform>();
        colorRect.anchorMin = new Vector2(0f, 1f);
        colorRect.anchorMax = new Vector2(1f, 1f);
        colorRect.pivot = new Vector2(0.5f, 1f);
        colorRect.anchoredPosition = new Vector2(0f, -125f);
        colorRect.sizeDelta = new Vector2(-36f, 430f);

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
            CreateSelectionCheck(swatch.GetComponent<RectTransform>());
            RegisterHitTarget(swatch, true, true, color);
        }

        Button freeColorPaletteButton = CreateFreeColorPalette(
            colorRect, new Vector2(0f, -145f), new Vector2(310f, 180f));
        freeColorPaletteButton.onClick.AddListener(CommitFreePaletteColor);
        RegisterHitTarget(
            freeColorPaletteButton, true, true, Color.white, true);

        Button backButton = CreateButton(
            "Back", colorRect, new Vector2(0f, -340f),
            new Vector2(294f, 44f), "SELECT PART", new Color32(42, 56, 72, 255));
        backButton.onClick.AddListener(NavigateBack);
        RegisterHitTarget(backButton, false, false, default);

        Button colorDoneButton = CreateButton(
            "Done", colorRect, new Vector2(0f, -392f),
            new Vector2(294f, 44f), "DONE", new Color32(74, 155, 143, 255));
        colorDoneButton.onClick.AddListener(ShowMainMenu);
        RegisterHitTarget(colorDoneButton, false, false, default);

    }

    private void BuildSelfieInterface(Transform parent)
    {
        selfieEntryButton = CreateButton(
            "SelfieEntry", parent, Vector2.zero,
            new Vector2(150f, 54f), "SELFIE", new Color32(155, 92, 130, 255));
        RectTransform entryRect = selfieEntryButton.GetComponent<RectTransform>();
        entryRect.anchorMin = entryRect.anchorMax = new Vector2(1f, 0f);
        entryRect.pivot = new Vector2(1f, 0f);
        entryRect.anchoredPosition = new Vector2(-370f, 24f);
        selfieEntryButton.onClick.AddListener(ShowSelfieCapture);
        RegisterHitTarget(selfieEntryButton, false, false, default);

        selfiePanel = new GameObject(
            "LanternSelfiePanel",
            typeof(RectTransform), typeof(Image), typeof(Outline));
        selfiePanel.transform.SetParent(parent, false);
        RectTransform panelRect = selfiePanel.GetComponent<RectTransform>();
        panelRect.anchorMin = panelRect.anchorMax = new Vector2(1f, 0f);
        panelRect.pivot = new Vector2(1f, 0f);
        panelRect.anchoredPosition = new Vector2(-24f, 24f);
        panelRect.sizeDelta = new Vector2(330f, SelfiePanelHeight);
        selfiePanel.GetComponent<Image>().color = new Color32(18, 27, 39, 248);
        Outline outline = selfiePanel.GetComponent<Outline>();
        outline.effectColor = new Color32(202, 123, 170, 150);
        outline.effectDistance = new Vector2(1f, -1f);

        selfieHeaderText = CreateText(
            "Header", panelRect, new Vector2(20f, -20f),
            new Vector2(290f, 34f), "SELFIE", 21f,
            TextAlignmentOptions.Center, FontStyles.Bold);
        selfieInstructionText = CreateText(
            "Instruction", panelRect, new Vector2(20f, -58f),
            new Vector2(290f, 52f), "Frame your photo, then take a selfie.", 15f,
            TextAlignmentOptions.Center, FontStyles.Normal);
        selfieInstructionText.color = new Color32(173, 196, 207, 255);

        BuildSelfieGroup(panelRect);
        selfiePanel.SetActive(false);
    }

    private void BuildSelfieGroup(Transform panelRect)
    {
        selfieGroup = new GameObject("Content", typeof(RectTransform));
        selfieGroup.transform.SetParent(panelRect, false);
        RectTransform selfieRect = selfieGroup.GetComponent<RectTransform>();
        selfieRect.anchorMin = new Vector2(0f, 1f);
        selfieRect.anchorMax = new Vector2(1f, 1f);
        selfieRect.pivot = new Vector2(0.5f, 1f);
        selfieRect.anchoredPosition = new Vector2(0f, -118f);
        selfieRect.sizeDelta = new Vector2(-36f, 450f);

        GameObject previewObject = new GameObject(
            "Preview", typeof(RectTransform), typeof(RawImage), typeof(Outline));
        previewObject.transform.SetParent(selfieRect, false);
        RectTransform previewRect = previewObject.GetComponent<RectTransform>();
        previewRect.anchorMin = previewRect.anchorMax = new Vector2(0.5f, 1f);
        previewRect.pivot = new Vector2(0.5f, 1f);
        previewRect.anchoredPosition = Vector2.zero;
        previewRect.sizeDelta = new Vector2(294f, 220f);
        selfiePreviewImage = previewObject.GetComponent<RawImage>();
        selfiePreviewImage.color = Color.white;
        selfiePreviewImage.raycastTarget = false;
        Outline previewOutline = previewObject.GetComponent<Outline>();
        previewOutline.effectColor = new Color32(82, 188, 189, 180);
        previewOutline.effectDistance = new Vector2(2f, -2f);

        takeSelfieButton = CreateButton(
            "TakeSelfie", selfieRect, new Vector2(0f, -234f),
            new Vector2(294f, 50f), "TAKE SELFIE", new Color32(155, 92, 130, 255));
        selfieBackButton = CreateButton(
            "Back", selfieRect, new Vector2(0f, -296f),
            new Vector2(294f, 50f), "BACK", new Color32(42, 56, 72, 255));

        attachSelfieButton = CreateButton(
            "AttachToLantern", selfieRect, new Vector2(0f, -234f),
            new Vector2(294f, 44f), "ATTACH TO LANTERN", new Color32(61, 124, 124, 255));
        retakeSelfieButton = CreateButton(
            "Retake", selfieRect, new Vector2(0f, -286f),
            new Vector2(142f, 44f), "RETAKE", new Color32(42, 56, 72, 255));
        retakeSelfieButton.GetComponent<RectTransform>().anchoredPosition = new Vector2(-76f, -286f);
        selfieDoneButton = CreateButton(
            "Done", selfieRect, new Vector2(76f, -286f),
            new Vector2(142f, 44f), "DONE", new Color32(74, 155, 143, 255));

        BindSelfieButtons();
        SetSelfieReviewControls(false);
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

        private void ShowMainMenu()
    {
        if (!paletteAvailable || panel == null)
            return;

        RestoreSelectedPreview();
        CaptureInitialColors();
        colorSelectionLocked = false;
        partSelectionLocked = false;

        popGestureArmed = false;

        SetSculptControllersEnabled(false);

        SetSculptControllersEnabled(!uiPointerBlockingSculpt);

        mode = ColorMode.MainMenu;
        SetPanelHeight(MainMenuPanelHeight);
        panel.SetActive(true);

        if (selfiePanel != null)
            selfiePanel.SetActive(false);

        if (selfieEntryButton != null)
            selfieEntryButton.gameObject.SetActive(true);

        mainActionGroup.SetActive(true);
        partGroup.SetActive(false);
        if (variantGroup != null)
            variantGroup.SetActive(false);
        colorGroup.SetActive(false);
        headerText.text = "COLOR PALETTE";
        instructionText.gameObject.SetActive(false);

        ResetDwell();
        SetHoveredTarget(null);
    }

      private void ExitColorMode()
    {
        RestoreSelectedPreview();
        ShowSculptMode();
    }

    private void ShowSculptMode()
    {
        mode = ColorMode.Sculpt;
        selectedPart = -1;
        selectedVariant = -1;
        selectedVariantLabel = "";
        popGestureArmed = false;
        colorSelectionLocked = false;
        partSelectionLocked = false;
        ClearSelectedPartVisual();
        ClearSelectedColorVisual();
        if (panel != null)
            panel.SetActive(false);
        if (selfiePanel != null)
            selfiePanel.SetActive(false);
        if (selfieEntryButton != null)
            selfieEntryButton.gameObject.SetActive(false);
        ResetDwell();
        SetHoveredTarget(null);
    }

    private void ShowPartSelection()
    {
        RestoreSelectedPreview();
        colorSelectionLocked = false;
        partSelectionLocked = false;
        ClearSelectedColorVisual();
        popGestureArmed = false;
        SetSculptControllersEnabled(false);
        mode = ColorMode.SelectPart;
        SetPanelHeight(PartSelectionPanelHeight);
        mainActionGroup.SetActive(false);
        partGroup.SetActive(true);
        if (variantGroup != null)
            variantGroup.SetActive(false);
        colorGroup.SetActive(false);
        if (selfieEntryButton != null)
            selfieEntryButton.gameObject.SetActive(false);
        instructionText.gameObject.SetActive(true);
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
        selectedVariant = -1;
        selectedVariantLabel = "";
        popGestureArmed = false;
        partSelectionLocked = false;

        if (PartNeedsVariant(partIndex))
        {
            ShowVariantSelection(partIndex);
            return;
        }

        headerText.text = "SELECT PART";
        instructionText.text = partNames[partIndex] + " selected.";
        SetHoveredTarget(null);
    }

    private void ShowVariantSelection(int partIndex)
    {
        EnsureVariantGroup();
        mode = ColorMode.SelectVariant;
        SetPanelHeight(VariantSelectionPanelHeight);
        mainActionGroup.SetActive(false);
        partGroup.SetActive(false);
        if (variantGroup == null)
            return;

        variantGroup.SetActive(true);
        colorGroup.SetActive(false);
        if (selfieEntryButton != null)
            selfieEntryButton.gameObject.SetActive(false);
        string[] labels = GetVariantLabels(partIndex);
        for (int i = 0; i < variantButtons.Length; i++)
        {
            if (variantButtonLabels[i] != null)
                variantButtonLabels[i].text = labels[i];
        }

        headerText.text = partNames[partIndex];
        SetHeaderBackLabel("← PARTS");
        instructionText.gameObject.SetActive(true);
        if (TryAdoptVisibleVariant(partIndex))
        {
            if (originalGestureController != null)
            {
                if (partIndex == 2 || partIndex == 3)
                    originalGestureController.ArmPopVariant(partIndex == 2, selectedVariant == 1);
                else if (partIndex == 4)
                    originalGestureController.ArmTasselVariant(selectedVariant == 1);
            }

            instructionText.text = selectedVariantLabel +
                " is already showing. Change color, or pick another shape.";
        }
        else
        {
            instructionText.text = partIndex == 4
                ? "Choose one tassel or three tassels."
                : "Choose Circle or Rectangle, then pinch and lift.";
        }
        ResetDwell();
        SetHoveredTarget(null);
    }

    private void SelectVariant(int variantIndex)
    {
        selectedVariant = variantIndex;
        string[] labels = GetVariantLabels(selectedPart);
        selectedVariantLabel = labels[variantIndex];
        headerText.text = partNames[selectedPart];

        if (selectedPart == 2 || selectedPart == 3)
        {
            popGestureArmed = true;
            if (lanternRenderer != null)
            {
                int unusedShape = FindVariantShape(selectedPart, 1 - variantIndex);
                if (unusedShape >= 0)
                    lanternRenderer.SetBlendShapeWeight(unusedShape, 0f);
            }
            if (originalGestureController != null)
                originalGestureController.ArmPopVariant(selectedPart == 2, variantIndex == 1);

            instructionText.text = selectedPart == 2
                ? "Pinch with your right hand and lift to pop the " +
                  selectedVariantLabel.ToLowerInvariant() + " top."
                : "Pinch with your left hand and lift to pop the " +
                  selectedVariantLabel.ToLowerInvariant() + " bottom.";
            SetHoveredTarget(null);
            return;
        }

        if (selectedPart == 4)
        {
            popGestureArmed = true;
            if (lanternRenderer != null)
            {
                int unusedShape = FindVariantShape(selectedPart, 1 - variantIndex);
                if (unusedShape >= 0)
                    lanternRenderer.SetBlendShapeWeight(unusedShape, 0f);
            }

            if (originalGestureController != null)
                originalGestureController.ArmTasselVariant(variantIndex == 1);

            instructionText.text =
                "Make a V with your left hand and lift it up to ease the " +
                selectedVariantLabel.ToLowerInvariant() + " out.";
            SetHoveredTarget(null);
            return;
        }

        ApplyPartVariantShapes(selectedPart, variantIndex);
        instructionText.text = selectedVariantLabel + " selected.";
        if (originalGestureController != null)
            originalGestureController.SyncCurrentFromRenderer();
        SetHoveredTarget(null);
    }

    private static string[] GetVariantLabels(int partIndex)
    {
        if (partIndex == 4)
            return new[] { "Single Tassel", "3 Tassel" };
        return new[] { "Circle", "Rectangle" };
    }

    private static bool PartNeedsVariant(int partIndex)
    {
        return partIndex == 2 || partIndex == 3 || partIndex == 4;
    }

    private bool TryAdoptVisibleVariant(int partIndex)
    {
        int visible = FindVisibleVariant(partIndex);
        if (visible < 0)
            return false;

        selectedVariant = visible;
        selectedVariantLabel = GetVariantLabels(partIndex)[visible];
        return true;
    }

    private int FindVisibleVariant(int partIndex)
    {
        if (lanternRenderer == null || !PartNeedsVariant(partIndex))
            return -1;

        float first = ReadShapeWeight(FindVariantShape(partIndex, 0));
        float second = ReadShapeWeight(FindVariantShape(partIndex, 1));
        const float visibleWeight = 8f;
        if (first < visibleWeight && second < visibleWeight)
            return -1;
        return first >= second ? 0 : 1;
    }

    private float ReadShapeWeight(int shapeIndex)
    {
        if (lanternRenderer == null || shapeIndex < 0)
            return 0f;
        return lanternRenderer.GetBlendShapeWeight(shapeIndex);
    }

    private void EnsureVariantGroup()
    {
        if (variantGroup != null || panel == null)
            return;

        Transform panelTransform = panel.transform;
        Transform existing = panelTransform.Find("VariantSelection");
        if (existing != null)
        {
            variantGroup = existing.gameObject;
            BindExistingVariantButtons(existing);
            variantGroup.SetActive(false);
            return;
        }

        variantGroup = new GameObject("VariantSelection", typeof(RectTransform));
        variantGroup.transform.SetParent(panelTransform, false);
        RectTransform variantRect = variantGroup.GetComponent<RectTransform>();
        variantRect.anchorMin = new Vector2(0f, 1f);
        variantRect.anchorMax = new Vector2(1f, 1f);
        variantRect.pivot = new Vector2(0.5f, 1f);
        variantRect.anchoredPosition = new Vector2(0f, -120f);
        variantRect.sizeDelta = new Vector2(-36f, 220f);

        for (int i = 0; i < variantButtons.Length; i++)
        {
            int variantIndex = i;
            variantButtons[i] = CreateButton(
                "Variant" + i,
                variantRect,
                new Vector2(0f, -i * 52f),
                new Vector2(294f, 44f),
                GetVariantLabels(2)[i],
                new Color32(42, 56, 72, 255));
            variantButtons[i].onClick.AddListener(() => SelectVariant(variantIndex));
            variantButtonLabels[i] = variantButtons[i].GetComponentInChildren<TMP_Text>();
            RegisterHitTarget(variantButtons[i], false, true, default);
        }

        Button nextColorsButton = CreateButton(
            "NextColors", variantRect, new Vector2(0f, -116f),
            new Vector2(294f, 44f), "CHANGE COLOR", new Color32(61, 124, 124, 255));
        nextColorsButton.onClick.AddListener(ContinueToColorSelection);
        RegisterHitTarget(nextColorsButton, false, false, default);

        Button variantDoneButton = CreateButton(
            "Done", variantRect, new Vector2(0f, -168f),
            new Vector2(294f, 44f), "DONE", new Color32(74, 155, 143, 255));
        variantDoneButton.onClick.AddListener(ShowMainMenu);
        RegisterHitTarget(variantDoneButton, false, false, default);
        variantGroup.SetActive(false);
    }

    private void BindExistingVariantButtons(Transform variantRoot)
    {
        for (int i = 0; i < variantButtons.Length; i++)
        {
            int variantIndex = i;
            Transform buttonTransform = variantRoot.Find("Variant" + i);
            if (buttonTransform == null ||
                !buttonTransform.TryGetComponent(out Button button))
                continue;

            variantButtons[i] = button;
            variantButtonLabels[i] = button.GetComponentInChildren<TMP_Text>();
            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(() => SelectVariant(variantIndex));
            RegisterHitTarget(button, false, true, default);
        }

        Transform nextColors = variantRoot.Find("NextColors");
        if (nextColors != null && nextColors.TryGetComponent(out Button nextButton))
        {
            nextButton.onClick.RemoveAllListeners();
            nextButton.onClick.AddListener(ContinueToColorSelection);
            RegisterHitTarget(nextButton, false, false, default);
        }

        Transform done = variantRoot.Find("Done");
        if (done != null && done.TryGetComponent(out Button doneButton))
        {
            doneButton.onClick.RemoveAllListeners();
            doneButton.onClick.AddListener(ShowMainMenu);
            RegisterHitTarget(doneButton, false, false, default);
        }
    }

    private void ApplyPartVariantShapes(int partIndex, int variantIndex)
    {
        if (lanternRenderer == null || lanternRenderer.sharedMesh == null)
            return;

        int activeShape = FindVariantShape(partIndex, variantIndex);
        int inactiveShape = FindVariantShape(partIndex, 1 - variantIndex);
        if (inactiveShape >= 0)
            lanternRenderer.SetBlendShapeWeight(inactiveShape, 0f);
        if (activeShape >= 0)
            lanternRenderer.SetBlendShapeWeight(activeShape, 100f);
    }

    private int FindVariantShape(int partIndex, int variantIndex)
    {
        Mesh mesh = lanternRenderer.sharedMesh;
        if (mesh == null)
            return -1;

        for (int i = 0; i < mesh.blendShapeCount; i++)
        {
            if (IsVariantShape(mesh.GetBlendShapeName(i), partIndex, variantIndex))
                return i;
        }

        return -1;
    }

    private static bool IsVariantShape(string shapeName, int partIndex, int variantIndex)
    {
        string compact = CompactShapeName(shapeName);
        if (partIndex == 2)
        {
            if (!compact.Contains("TOP"))
                return false;
            return variantIndex == 0
                ? compact.Contains("CIRCLE") && !compact.Contains("RECTANGLE")
                : compact.Contains("RECTANGLE");
        }

        if (partIndex == 3)
        {
            if (!compact.Contains("BOTTOM"))
                return false;
            return variantIndex == 0
                ? compact.Contains("CIRCLE") && !compact.Contains("RECTANGLE")
                : compact.Contains("RECTANGLE");
        }

        if (partIndex == 4)
        {
            bool isTassel = compact.Contains("TASSEL") ||
                            compact.Contains("TASSLE") ||
                            compact.Contains("BAMBOO");
            if (!isTassel)
                return false;
            bool isSingle = compact.Contains("SINGLE");
            bool isTriple = compact.Contains("3") || compact.Contains("THREE");
            return variantIndex == 0 ? !isTriple : isTriple && !isSingle;
        }

        return false;
    }

    private static string CompactShapeName(string shapeName)
    {
        if (string.IsNullOrEmpty(shapeName))
            return "";

        char[] chars = shapeName.ToUpperInvariant().ToCharArray();
        int count = 0;
        for (int i = 0; i < chars.Length; i++)
        {
            if (char.IsLetterOrDigit(chars[i]))
                chars[count++] = chars[i];
        }

        return new string(chars, 0, count);
    }

    private void ContinueToColorSelection()
    {
        if (selectedPart < 0)
            selectedPart = 0;

        if (PartNeedsVariant(selectedPart) && selectedVariant < 0 &&
            !TryAdoptVisibleVariant(selectedPart))
        {
            if (mode == ColorMode.SelectVariant)
            {
                instructionText.text = "Choose a shape first.";
                return;
            }

            ShowVariantSelection(selectedPart);
            return;
        }

        partSelectionLocked = false;
        colorSelectionLocked = false;
        popGestureArmed = false;
        SetSculptControllersEnabled(false);
        mode = ColorMode.SelectColor;
        SetPanelHeight(ColorSelectionPanelHeight);
        mainActionGroup.SetActive(false);
        partGroup.SetActive(false);
        if (variantGroup != null)
            variantGroup.SetActive(false);
        colorGroup.SetActive(true);
        if (selfieEntryButton != null)
            selfieEntryButton.gameObject.SetActive(false);
        instructionText.gameObject.SetActive(true);
        headerText.text = string.IsNullOrEmpty(selectedVariantLabel)
            ? partNames[selectedPart]
            : partNames[selectedPart] + " · " + selectedVariantLabel;
        SetHeaderBackLabel(PartNeedsVariant(selectedPart) ? "← SHAPE" : "← PARTS");
        instructionText.text = "Point and hold until the border completes.";
        Transform colorBack = colorGroup != null ? colorGroup.transform.Find("Back") : null;
        TMP_Text colorBackLabel = colorBack != null
            ? colorBack.GetComponentInChildren<TMP_Text>()
            : null;
        if (colorBackLabel != null)
            colorBackLabel.text = PartNeedsVariant(selectedPart) ? "SELECT SHAPE" : "SELECT PART";
        SetHoveredTarget(null);
    }

    private void ShowSelfieCapture()
    {
        if (!paletteAvailable || selfiePanel == null || selfieGroup == null)
            return;

        RestoreSelectedPreview();
        SetSculptControllersEnabled(false);
        mode = ColorMode.Selfie;
        panel.SetActive(false);
        selfieEntryButton.gameObject.SetActive(false);
        selfiePanel.SetActive(true);
        selfieGroup.SetActive(true);
        selfieInstructionText.gameObject.SetActive(true);
        selfieHeaderText.text = "SELFIE";
        selfieInstructionText.text = "Frame your photo, then point and hold TAKE SELFIE.";
        SetSelfieReviewControls(false);
        ShowLiveSelfiePreview();
        ResetDwell();
        SetHoveredTarget(null);
    }

    private void BindSelfieButtons()
    {
        takeSelfieButton.onClick.AddListener(CaptureSelfie);
        selfieBackButton.onClick.AddListener(CloseSelfie);
        attachSelfieButton.onClick.AddListener(AttachSelfieToLantern);
        retakeSelfieButton.onClick.AddListener(BeginSelfieRetake);
        selfieDoneButton.onClick.AddListener(CloseSelfie);

        RegisterHitTarget(takeSelfieButton, false, false, default);
        RegisterHitTarget(selfieBackButton, false, false, default);
        RegisterHitTarget(attachSelfieButton, false, false, default);
        RegisterHitTarget(retakeSelfieButton, false, false, default);
        RegisterHitTarget(selfieDoneButton, false, false, default);
    }

    private void CloseSelfie()
    {
        ShowMainMenu();
    }

    private void SetSelfieReviewControls(bool reviewing)
    {
        takeSelfieButton.gameObject.SetActive(!reviewing);
        selfieBackButton.gameObject.SetActive(!reviewing);
        attachSelfieButton.gameObject.SetActive(reviewing);
        retakeSelfieButton.gameObject.SetActive(reviewing);
        selfieDoneButton.gameObject.SetActive(reviewing);
    }

    private void ShowLiveSelfiePreview()
    {
        if (selfiePreviewImage == null)
            return;

        var imageSource = ImageSourceProvider.ImageSource;
        if (imageSource == null || !imageSource.isPrepared)
        {
            selfiePreviewImage.texture = null;
            selfieInstructionText.text = "Camera is not ready yet. Please try again in a moment.";
            return;
        }

        SetSelfiePreviewTexture(imageSource.GetCurrentTexture());
    }

    private void CaptureSelfie()
    {
        Texture2D captured = CaptureCurrentCameraFrame();
        if (captured == null)
        {
            selfieInstructionText.text = "Could not capture the camera image. Please try again.";
            return;
        }

        DestroyRuntimeObject(capturedSelfieTexture);
        capturedSelfieTexture = captured;
        SetSelfiePreviewTexture(capturedSelfieTexture);
        SetSelfieReviewControls(true);
        selfieInstructionText.text = "Attach the photo to the lantern, retake it, or press DONE.";
        ResetDwell();
        SetHoveredTarget(null);
    }

    private Texture2D CaptureCurrentCameraFrame()
    {
        var imageSource = ImageSourceProvider.ImageSource;
        if (imageSource == null || !imageSource.isPrepared)
            return null;

        Texture source = imageSource.GetCurrentTexture();
        if (source == null || source.width <= 16 || source.height <= 16)
            return null;

        const int maxDimension = 768;
        float scale = Mathf.Min(1f, maxDimension / (float)Mathf.Max(source.width, source.height));
        int width = Mathf.Max(2, Mathf.RoundToInt(source.width * scale));
        int height = Mathf.Max(2, Mathf.RoundToInt(source.height * scale));
        RenderTexture temporary = RenderTexture.GetTemporary(
            width, height, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
        RenderTexture previous = RenderTexture.active;
        Graphics.Blit(source, temporary);
        RenderTexture.active = temporary;

        Texture2D readable = new Texture2D(width, height, TextureFormat.RGBA32, false);
        readable.ReadPixels(new Rect(0f, 0f, width, height), 0, 0, false);
        readable.Apply(false, false);

        RenderTexture.active = previous;
        RenderTexture.ReleaseTemporary(temporary);

        var transformOptions = imageSource.GetTransformationOptions();
        Texture2D result = TransformCapturedTexture(
            readable,
            // Match the saved selfie to the user's real left/right direction.
            // The MediaPipe front-camera transform produced the opposite
            // mirror state from the requested selfie result.
            !transformOptions.flipHorizontally,
            // Graphics.Blit + ReadPixels already produces the correct Unity
            // vertical orientation for this webcam. Applying MediaPipe's CPU
            // readback flip here turned the saved selfie upside down.
            false,
            (int)transformOptions.rotationAngle);
        DestroyRuntimeObject(readable);
        result.name = "Morphic Selfie";
        result.filterMode = FilterMode.Bilinear;
        result.wrapMode = TextureWrapMode.Clamp;
        return result;
    }

    private static Texture2D TransformCapturedTexture(
        Texture2D source,
        bool flipHorizontally,
        bool flipVertically,
        int rotationDegrees)
    {
        int rotation = ((rotationDegrees % 360) + 360) % 360;
        int sourceWidth = source.width;
        int sourceHeight = source.height;
        bool swapsAxes = rotation == 90 || rotation == 270;
        int targetWidth = swapsAxes ? sourceHeight : sourceWidth;
        int targetHeight = swapsAxes ? sourceWidth : sourceHeight;
        Color32[] sourcePixels = source.GetPixels32();
        Color32[] targetPixels = new Color32[targetWidth * targetHeight];

        for (int sourceY = 0; sourceY < sourceHeight; sourceY++)
        {
            for (int sourceX = 0; sourceX < sourceWidth; sourceX++)
            {
                int x = flipHorizontally ? sourceWidth - 1 - sourceX : sourceX;
                int y = flipVertically ? sourceHeight - 1 - sourceY : sourceY;
                int targetX;
                int targetY;

                switch (rotation)
                {
                    case 90:
                        targetX = sourceHeight - 1 - y;
                        targetY = x;
                        break;
                    case 180:
                        targetX = sourceWidth - 1 - x;
                        targetY = sourceHeight - 1 - y;
                        break;
                    case 270:
                        targetX = y;
                        targetY = sourceWidth - 1 - x;
                        break;
                    default:
                        targetX = x;
                        targetY = y;
                        break;
                }

                Color32 capturedPixel = sourcePixels[sourceY * sourceWidth + sourceX];
                // Webcam textures do not guarantee a meaningful alpha
                // channel. A zero alpha made a valid captured RGB image
                // completely invisible on the lantern decal material.
                capturedPixel.a = 255;
                targetPixels[targetY * targetWidth + targetX] = capturedPixel;
            }
        }

        Texture2D result = new Texture2D(
            targetWidth, targetHeight, TextureFormat.RGBA32, false);
        result.SetPixels32(targetPixels);
        result.Apply(false, false);
        return result;
    }

    private void SetSelfiePreviewTexture(Texture texture)
    {
        if (selfiePreviewImage == null)
            return;

        selfiePreviewImage.texture = texture;
        selfiePreviewImage.uvRect = new Rect(0f, 0f, 1f, 1f);
        if (texture == null || texture.width <= 0 || texture.height <= 0)
            return;

        const float maximumWidth = 294f;
        const float maximumHeight = 220f;
        float aspect = texture.width / (float)texture.height;
        float width = maximumWidth;
        float height = width / aspect;
        if (height > maximumHeight)
        {
            height = maximumHeight;
            width = height * aspect;
        }
        selfiePreviewImage.rectTransform.sizeDelta = new Vector2(width, height);
    }

    private void BeginSelfieRetake()
    {
        DestroyRuntimeObject(capturedSelfieTexture);
        capturedSelfieTexture = null;
        SetSelfieReviewControls(false);
        ShowLiveSelfiePreview();
        selfieInstructionText.text = "Frame your photo, then point and hold TAKE SELFIE.";
        ResetDwell();
        SetHoveredTarget(null);
    }

    private void AttachSelfieToLantern()
    {
        Texture2D selectedTexture = capturedSelfieTexture;
        if (selectedTexture == null || lanternRenderer == null)
        {
            selfieInstructionText.text = "Take a selfie before attaching it to the lantern.";
            return;
        }

        DestroyRuntimeObject(appliedSelfieTexture);
        appliedSelfieTexture = CopyTextureForSelfieSurface(
            selectedTexture, "Morphic Applied Selfie");
        EnsureSelfieSurfaceOverlay();
        ConfigureSelfieSurfaceMaterial(appliedSelfieTexture);
        SyncSelfieSurfaceRenderer();
        selfieInstructionText.text = "Selfie attached to the front, back, left and right.";
    }

    private static Texture2D CopyTextureForSelfieSurface(
        Texture2D source,
        string textureName)
    {
        // Keep exactly the same pixels and orientation that the user approved
        // in the Canvas preview. Surface orientation is now handled once by
        // the overlay shader instead of by rotating and flipping copies.
        Texture2D copy = new Texture2D(
            source.width, source.height, TextureFormat.RGBA32, false)
        {
            name = textureName,
            filterMode = FilterMode.Bilinear,
            wrapMode = TextureWrapMode.Clamp
        };
        copy.SetPixels32(source.GetPixels32());
        copy.Apply(false, false);
        return copy;
    }

    private void EnsureSelfieSurfaceOverlay()
    {
        if (lanternRenderer == null || lanternRenderer.sharedMesh == null)
            return;

        Shader shader = Shader.Find("Morphic/Selfie Surface Overlay");
        if (shader == null)
        {
            Debug.LogError(
                "MORPHIC: Selfie surface shader was not found. " +
                "The selfie cannot be attached to the lantern surface.",
                this);
            return;
        }

        if (selfieSurfaceRoot == null)
        {
            GameObject overlayObject = new GameObject("LanternSelfieSurfaceOverlay");
            selfieSurfaceRoot = overlayObject.transform;
            selfieSurfaceRenderer = overlayObject.AddComponent<SkinnedMeshRenderer>();
        }

        if (selfieSurfaceMaterial == null)
        {
            selfieSurfaceMaterial = new Material(shader)
            {
                name = "Morphic Selfie Surface Material",
                renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent + 20
            };
        }

        if (selfieHiddenMaterial == null)
        {
            selfieHiddenMaterial = new Material(shader)
            {
                name = "Morphic Hidden Selfie Submesh Material",
                renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent + 20
            };
            selfieHiddenMaterial.SetFloat("_Opacity", 0f);
        }

        Transform rendererTransform = lanternRenderer.transform;
        selfieSurfaceRoot.SetParent(rendererTransform.parent, false);
        selfieSurfaceRoot.localPosition = rendererTransform.localPosition;
        selfieSurfaceRoot.localRotation = rendererTransform.localRotation;
        selfieSurfaceRoot.localScale = rendererTransform.localScale;
        selfieSurfaceRoot.gameObject.layer = lanternRenderer.gameObject.layer;
        selfieSurfaceRoot.gameObject.SetActive(true);

        selfieSurfaceRenderer.sharedMesh = lanternRenderer.sharedMesh;
        selfieSurfaceRenderer.rootBone = lanternRenderer.rootBone;
        selfieSurfaceRenderer.bones = lanternRenderer.bones;
        selfieSurfaceRenderer.localBounds = lanternRenderer.localBounds;
        selfieSurfaceRenderer.quality = lanternRenderer.quality;
        selfieSurfaceRenderer.updateWhenOffscreen = lanternRenderer.updateWhenOffscreen;
        selfieSurfaceRenderer.skinnedMotionVectors = false;
        selfieSurfaceRenderer.shadowCastingMode =
            UnityEngine.Rendering.ShadowCastingMode.Off;
        selfieSurfaceRenderer.receiveShadows = false;
        selfieSurfaceRenderer.forceRenderingOff = false;
        selfieSurfaceRenderer.allowOcclusionWhenDynamic = false;
        selfieSurfaceRenderer.renderingLayerMask = lanternRenderer.renderingLayerMask;
        selfieSurfaceRenderer.sortingOrder = 20;

        int subMeshCount = Mathf.Max(1, lanternRenderer.sharedMesh.subMeshCount);
        Material[] overlayMaterials = new Material[subMeshCount];
        for (int i = 0; i < overlayMaterials.Length; i++)
            overlayMaterials[i] = selfieHiddenMaterial;
        overlayMaterials[Mathf.Clamp(bodyMaterialIndex, 0, subMeshCount - 1)] =
            selfieSurfaceMaterial;
        selfieSurfaceRenderer.sharedMaterials = overlayMaterials;
        selfieSurfaceRenderer.enabled = true;
    }

    private void ConfigureSelfieSurfaceMaterial(Texture texture)
    {
        if (selfieSurfaceMaterial == null || lanternRenderer == null)
            return;

        Bounds localBounds = GetCurrentLanternObjectBounds();
        float verticalHalfFraction = Mathf.Clamp(
            selfieHeightFraction * selfieAttachedImageScale, 0.05f, 0.9f);

        // Keep the selfie attached to a fixed normalized region of the
        // lantern surface. The previous aspect-ratio calculation reduced the
        // occupied angle whenever the lantern became wider, cancelling the
        // visible size change. A fixed angular fraction makes the photograph
        // stretch and shrink with the sculpted surface in both dimensions.
        float patchHalfAngle = Mathf.Clamp(
            selfieWidthToRadius * 0.5f * selfieAttachedImageScale,
            0.05f,
            0.78f);

        selfieSurfaceMaterial.SetTexture("_SelfieTex", texture);
        selfieSurfaceMaterial.SetFloat("_Opacity", 1f);
        selfieSurfaceMaterial.SetFloat("_PatchHalfAngle", patchHalfAngle);
        selfieSurfaceMaterial.SetFloat(
            "_VerticalHalfFraction",
            verticalHalfFraction);
        selfieSurfaceMaterial.SetFloat(
            BrushEdgeRoughnessId, selfieBrushRoughness);
        selfieSurfaceMaterial.SetFloat(
            BrushCornerRoundnessId, selfieBrushCornerRoundness);
        // The centre of the photograph is exactly the centre of the currently
        // deformed lantern. No saved attachment position or manual offset is
        // allowed here; Width and Height can move this point every frame.
        selfieSurfaceMaterial.SetVector("_ObjectCenter", localBounds.center);
        selfieSurfaceMaterial.SetVector("_ObjectExtents", localBounds.extents);
    }

    private Bounds GetCurrentLanternObjectBounds()
    {
        if (lanternRenderer == null || lanternRenderer.sharedMesh == null)
            return default;

        if (selfieBoundsMesh == null)
        {
            selfieBoundsMesh = new Mesh
            {
                name = "Morphic Current Lantern Bounds"
            };
            selfieBoundsMesh.MarkDynamic();
        }

        // localBounds is the FBX import-time box and remains offset after the
        // Width/Height/Top/Bottom blend shapes deform the lantern. BakeMesh
        // returns the currently visible skinned vertices in renderer object
        // space, which is the same positionOS space used by the selfie shader.
        lanternRenderer.BakeMesh(selfieBoundsMesh);
        // BakeMesh can leave the mesh carrying the renderer's imported/stale
        // bounds. Rebuild them from the actual deformed vertex positions so an
        // upward-only Height change also moves the calculated centre upward.
        selfieBoundsMesh.RecalculateBounds();
        Bounds currentBounds = selfieBoundsMesh.bounds;

        if (currentBounds.extents.sqrMagnitude <= 0.0000001f)
            return lanternRenderer.localBounds;

        return currentBounds;
    }

    private void SyncSelfieSurfaceRenderer()
    {
        if (selfieSurfaceRenderer == null || lanternRenderer == null)
            return;

        Transform rendererTransform = lanternRenderer.transform;
        if (selfieSurfaceRoot.parent != rendererTransform.parent)
            selfieSurfaceRoot.SetParent(rendererTransform.parent, false);
        selfieSurfaceRoot.localPosition = rendererTransform.localPosition;
        selfieSurfaceRoot.localRotation = rendererTransform.localRotation;
        selfieSurfaceRoot.localScale = rendererTransform.localScale;

        selfieSurfaceRenderer.rootBone = lanternRenderer.rootBone;
        selfieSurfaceRenderer.bones = lanternRenderer.bones;
        selfieSurfaceRenderer.localBounds = lanternRenderer.localBounds;

        Mesh mesh = lanternRenderer.sharedMesh;
        if (mesh == null)
            return;
        if (selfieSurfaceRenderer.sharedMesh != mesh)
            selfieSurfaceRenderer.sharedMesh = mesh;

        for (int i = 0; i < mesh.blendShapeCount; i++)
        {
            selfieSurfaceRenderer.SetBlendShapeWeight(
                i, lanternRenderer.GetBlendShapeWeight(i));
        }
    }

    // Retained temporarily for source-history comparison only. The detached
    // mesh approach is deliberately excluded: the active implementation above
    // renders on a duplicate SkinnedMeshRenderer and follows every deformation.
#if false
    private void EnsureSelfieDecals()
    {
        if (selfieDecalRoot == null)
        {
            Transform existing = lanternRenderer.transform.Find("LanternSelfieDecals");
            if (existing != null)
            {
                selfieDecalRoot = existing;
                selfieDecalRoot.SetParent(null, true);
            }
            else
            {
                GameObject rootObject = new GameObject("LanternSelfieDecals");
                selfieDecalRoot = rootObject.transform;
            }
        }

        // Keep the decal root in world space. The imported lantern has a
        // rotated local axis and a strongly non-uniform root scale, both of
        // which can collapse or move child quads when local coordinates are
        // used.
        selfieDecalRoot.SetParent(null, true);
        selfieDecalRoot.position = Vector3.zero;
        selfieDecalRoot.rotation = Quaternion.identity;
        selfieDecalRoot.localScale = Vector3.one;

        if (selfieQuadMesh == null)
            selfieQuadMesh = CreateSelfieQuadMesh();

        if (selfieDecalMaterial == null)
            selfieDecalMaterial = CreateSelfieDecalMaterial();

        selfieDecalRoot.gameObject.SetActive(true);

        // Older versions created four transformed child quads. Disable them;
        // the replacement below writes all four faces directly into one
        // world-space mesh so no Transform rotation can turn a photo sideways.
        for (int i = 0; i < selfieDecalRoot.childCount; i++)
            selfieDecalRoot.GetChild(i).gameObject.SetActive(false);

        selfieDecalFilter = selfieDecalRoot.GetComponent<MeshFilter>();
        if (selfieDecalFilter == null)
            selfieDecalFilter = selfieDecalRoot.gameObject.AddComponent<MeshFilter>();
        selfieDecalRenderer = selfieDecalRoot.GetComponent<MeshRenderer>();
        if (selfieDecalRenderer == null)
            selfieDecalRenderer = selfieDecalRoot.gameObject.AddComponent<MeshRenderer>();

        selfieDecalFilter.sharedMesh = selfieQuadMesh;
        selfieDecalRenderer.sharedMaterial = selfieDecalMaterial;
        selfieDecalRenderer.enabled = true;
        selfieDecalRenderer.forceRenderingOff = false;
        selfieDecalRenderer.shadowCastingMode =
            UnityEngine.Rendering.ShadowCastingMode.Off;
        selfieDecalRenderer.receiveShadows = false;
        selfieDecalRenderer.sortingOrder = 10;
        selfieDecalRoot.gameObject.layer = lanternRenderer.gameObject.layer;
    }

    private static Mesh CreateSelfieQuadMesh()
    {
        Mesh mesh = new Mesh { name = "Morphic Selfie Curved Four-Sided Mesh" };
        mesh.MarkDynamic();
        return mesh;
    }

    private static Material CreateSelfieDecalMaterial()
    {
        Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
        if (shader == null)
            shader = Shader.Find("Unlit/Transparent");
        if (shader == null)
            shader = Shader.Find("Sprites/Default");

        Material material = new Material(shader)
        {
            name = "Morphic Selfie Decal Material",
            renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent
        };
        material.SetOverrideTag("RenderType", "Transparent");
        if (material.HasProperty("_Surface"))
            material.SetFloat("_Surface", 1f);
        if (material.HasProperty("_Blend"))
            material.SetFloat("_Blend", 0f);
        if (material.HasProperty("_AlphaClip"))
            material.SetFloat("_AlphaClip", 0f);
        if (material.HasProperty("_SrcBlend"))
            material.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
        if (material.HasProperty("_DstBlend"))
            material.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        if (material.HasProperty("_ZWrite"))
            material.SetFloat("_ZWrite", 0f);
        if (material.HasProperty("_Cull"))
            material.SetFloat("_Cull", 0f);
        material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        material.EnableKeyword("_ALPHABLEND_ON");
        material.DisableKeyword("_ALPHAPREMULTIPLY_ON");
        return material;
    }

    private void ConfigureSelfieDecalMaterial(Texture texture)
    {
        if (selfieDecalMaterial == null)
            return;

        selfieDecalMaterial.mainTexture = texture;
        selfieDecalMaterial.mainTextureScale = Vector2.one;
        selfieDecalMaterial.mainTextureOffset = Vector2.zero;
        if (selfieDecalMaterial.HasProperty("_BaseMap"))
        {
            selfieDecalMaterial.SetTexture("_BaseMap", texture);
            selfieDecalMaterial.SetTextureScale("_BaseMap", Vector2.one);
            selfieDecalMaterial.SetTextureOffset("_BaseMap", Vector2.zero);
        }
        if (selfieDecalMaterial.HasProperty("_BaseColor"))
            selfieDecalMaterial.SetColor("_BaseColor", Color.white);
        if (selfieDecalMaterial.HasProperty("_Color"))
            selfieDecalMaterial.SetColor("_Color", Color.white);
    }

    private void RefreshSelfieDecalLayout()
    {
        if (lanternRenderer == null || selfieQuadMesh == null ||
            appliedSelfieTexture == null)
            return;

        Bounds bounds = GetCurrentLanternWorldBounds();
        Vector3 extents = bounds.extents;
        float aspect = appliedSelfieTexture.width / (float)appliedSelfieTexture.height;

        // Build four subdivided patches on the current X/Z circumference.
        // Keep the whole patch outside the baked bounds: an earlier ellipsoid
        // profile narrowed the radius toward the top and bottom and placed the
        // decal inside this lantern's fuller body, where it was depth-occluded.
        const int patchCount = 4;
        const int horizontalSegments = 10;
        const int verticalSegments = 6;
        int columns = horizontalSegments + 1;
        int rows = verticalSegments + 1;
        int verticesPerPatch = columns * rows;
        int indicesPerPatch = horizontalSegments * verticalSegments * 6;
        Vector3[] vertices = new Vector3[patchCount * verticesPerPatch];
        Vector3[] normals = new Vector3[vertices.Length];
        Vector2[] uv = new Vector2[vertices.Length];
        int[] triangles = new int[patchCount * indicesPerPatch];

        float radiusX = Mathf.Max(0.001f, extents.x);
        float radiusY = Mathf.Max(0.001f, extents.y);
        float radiusZ = Mathf.Max(0.001f, extents.z);
        float bodyHeight = radiusY * 2f;
        float averageRadius = (radiusX + radiusZ) * 0.5f;
        float maximumArcWidth = averageRadius * selfieWidthToRadius;
        float maximumHeight = bodyHeight * selfieHeightFraction;
        float width = Mathf.Min(maximumArcWidth, maximumHeight * aspect);
        float height = width / Mathf.Max(0.01f, aspect);
        if (height > maximumHeight)
        {
            height = maximumHeight;
            width = height * aspect;
        }

        float halfAngle = Mathf.Min(0.68f, width / (2f * averageRadius));
        float centerYOffset = radiusY * 0.08f;
        float clearance = Mathf.Min(radiusX, radiusZ) * 0.035f;
        float xRadius = radiusX * Mathf.Max(1.01f, selfieSurfaceOffset) + clearance;
        float zRadius = radiusZ * Mathf.Max(1.01f, selfieSurfaceOffset) + clearance;

        for (int patch = 0; patch < patchCount; patch++)
        {
            float centerAngle = patch * Mathf.PI * 0.5f;
            int vertexStart = patch * verticesPerPatch;
            int triangleStart = patch * indicesPerPatch;

            for (int row = 0; row < rows; row++)
            {
                float v = row / (float)verticalSegments;
                float localY = centerYOffset + Mathf.Lerp(-height * 0.5f, height * 0.5f, v);

                for (int column = 0; column < columns; column++)
                {
                    float horizontal = column / (float)horizontalSegments;
                    float angle = centerAngle + Mathf.Lerp(-halfAngle, halfAngle, horizontal);
                    float sin = Mathf.Sin(angle);
                    float cos = Mathf.Cos(angle);
                    int vertex = vertexStart + row * columns + column;

                    vertices[vertex] = bounds.center + new Vector3(
                        sin * xRadius,
                        localY,
                        cos * zRadius);
                    normals[vertex] = new Vector3(
                        sin / radiusX,
                        0f,
                        cos / radiusZ).normalized;

                    // The captured selfie is correct in the Canvas. Reverse U
                    // only for the outside-facing lantern surface so it is not
                    // displayed as a left/right mirror image.
                    uv[vertex] = new Vector2(1f - horizontal, v);
                }
            }

            int triangle = triangleStart;
            for (int row = 0; row < verticalSegments; row++)
            {
                for (int column = 0; column < horizontalSegments; column++)
                {
                    int bottomLeft = vertexStart + row * columns + column;
                    int bottomRight = bottomLeft + 1;
                    int topLeft = bottomLeft + columns;
                    int topRight = topLeft + 1;
                    triangles[triangle++] = bottomLeft;
                    triangles[triangle++] = bottomRight;
                    triangles[triangle++] = topRight;
                    triangles[triangle++] = bottomLeft;
                    triangles[triangle++] = topRight;
                    triangles[triangle++] = topLeft;
                }
            }
        }

        selfieQuadMesh.Clear();
        selfieQuadMesh.vertices = vertices;
        selfieQuadMesh.normals = normals;
        selfieQuadMesh.uv = uv;
        selfieQuadMesh.triangles = triangles;
        selfieQuadMesh.RecalculateBounds();
    }

    private Bounds GetCurrentLanternWorldBounds()
    {
        // Renderer.bounds is already in world space and includes the complete
        // FBX hierarchy, its strongly non-uniform root scale, and the current
        // visible skinned bounds. BakeMesh vertices use a different local/root
        // space for this imported lantern; transforming them again displaced
        // the generated decal away from the model.
        return lanternRenderer.bounds;
    }

    private int ComputeLanternLayoutState()
    {
        unchecked
        {
            int state = 17;
            Transform lanternTransform = lanternRenderer.transform;
            state = state * 31 + lanternTransform.position.GetHashCode();
            state = state * 31 + lanternTransform.rotation.GetHashCode();
            state = state * 31 + lanternTransform.lossyScale.GetHashCode();

            Mesh mesh = lanternRenderer.sharedMesh;
            int blendShapeCount = mesh != null ? mesh.blendShapeCount : 0;
            for (int i = 0; i < blendShapeCount; i++)
            {
                int quantizedWeight = Mathf.RoundToInt(
                    lanternRenderer.GetBlendShapeWeight(i) * 1000f);
                state = state * 31 + quantizedWeight;
            }
            return state;
        }
    }

#endif

    private static void DestroyRuntimeObject(UnityEngine.Object target)
    {
        if (target == null)
            return;

        if (Application.isPlaying)
            Destroy(target);
        else
            DestroyImmediate(target);
    }

    private void CommitColor(Color color)
    {
        if (selectedPart < 0)
            return;

        ApplyColor(selectedPart, color);
        CommitTargetColor(selectedPart, color);
        colorSelectionLocked = false;
        instructionText.text = "Color selected. Choose another color or press DONE.";
    }


         private void NavigateBack()
    {
        if (mode == ColorMode.SelectColor)
        {
            if (PartNeedsVariant(selectedPart))
                ShowVariantSelection(selectedPart);
            else
                ShowPartSelection();
            return;
        }

        if (mode == ColorMode.SelectVariant)
        {
            ShowPartSelection();
            return;
        }

        if (mode == ColorMode.SelectPart)
            ShowSculptMode();
    }

    private void SetHeaderBackLabel(string label)
    {
        if (headerBackText != null)
            headerBackText.text = label;
    }

    private void CommitFreePaletteColor()
    {
        CommitColor(freePalettePreviewColor);

        if (freeColorSelectionCheck != null)
        {
            freeColorSelectionCheck.anchoredPosition =
                freePaletteMarkerPosition;

            freeColorSelectionCheck.gameObject.SetActive(true);
        }
    }

    private void UpdateFreePalettePreview(
        HitTarget target,
        Vector2 screenPosition)
    {
        if (target == null || !target.IsFreeColorPalette ||
            mode != ColorMode.SelectColor || selectedPart < 0)
            return;

        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
                target.Rect, screenPosition, canvasCamera, out Vector2 localPoint))
            return;

        Rect rect = target.Rect.rect;
        float x = Mathf.InverseLerp(rect.xMin, rect.xMax, localPoint.x);
        float y = Mathf.InverseLerp(rect.yMin, rect.yMax, localPoint.y);
        freePalettePreviewColor = EvaluateFreePaletteColor(x, y);
        freePaletteMarkerPosition = localPoint;
        target.PreviewColor = freePalettePreviewColor;
        ApplyColor(selectedPart, freePalettePreviewColor);
    }

    private static Color EvaluateFreePaletteColor(float x, float y)
    {
        Color hue = Color.HSVToRGB(Mathf.Repeat(x, 1f), 1f, 1f);
        return y >= 0.5f
            ? Color.Lerp(hue, Color.white, (y - 0.5f) * 2f)
            : Color.Lerp(Color.black, hue, y * 2f);
    }

    private void ApplyCenteredTypography()
    {
        if (uiRoot == null)
            return;

        TMP_Text[] texts = uiRoot.GetComponentsInChildren<TMP_Text>(true);
        for (int i = 0; i < texts.Length; i++)
        {
            if (texts[i] != null)
                texts[i].alignment = TextAlignmentOptions.Center;
        }
    }

    private void SetPanelHeight(float height)
    {
        if (panel == null)
            return;

        RectTransform rect = panel.GetComponent<RectTransform>();
        if (rect != null)
            rect.sizeDelta = new Vector2(rect.sizeDelta.x, height);
    }

    private void ApplyBottomRightDocking()
    {
        if (panel == null)
            return;

        RectTransform rect = panel.GetComponent<RectTransform>();
        if (rect == null)
            return;

        rect.anchorMin = rect.anchorMax = new Vector2(1f, 0f);
        rect.pivot = new Vector2(1f, 0f);
        rect.anchoredPosition = new Vector2(-24f, 24f);
    }

    private void SetSculptControllersEnabled(bool enabled)
    {
        bool originalAlreadySet = originalGestureController == null ||
            originalGestureController.enabled == enabled;
        bool indexAlreadySet = indexGestureController == null ||
            indexGestureController.enabled == enabled;
        if (originalAlreadySet && indexAlreadySet)
            return;

        if (enabled)
        {
            // Both controllers cache BlendShape values. Refresh those caches
            // before re-enabling them after Selfie/Color mode, otherwise their
            // first Update/LateUpdate writes stale Width/Height values and the
            // lantern visibly jumps in size as soon as any hand is detected.
            if (originalGestureController != null)
                originalGestureController.SynchronizeWeightsFromRenderer();
            if (indexGestureController != null)
                indexGestureController.SynchronizeWeightsFromRenderer();
        }

        if (originalGestureController != null)
            originalGestureController.enabled = enabled;
        if (indexGestureController != null)
            indexGestureController.enabled = enabled;
    }

    private void UpdateSculptLockFromUi(bool overUi)
    {
        if (mode == ColorMode.Sculpt)
            return;

        bool allowSculpt = !overUi &&
            (mode == ColorMode.MainMenu ||
             (mode == ColorMode.SelectVariant && popGestureArmed));

        if (allowSculpt)
        {
            if (mode == ColorMode.SelectVariant && popGestureArmed)
            {
                if (originalGestureController != null)
                    originalGestureController.enabled = true;
                if (indexGestureController != null)
                    indexGestureController.enabled = false;
            }
            else
            {
                SetSculptControllersEnabled(true);
            }
        }
        else
        {
            SetSculptControllersEnabled(false);
        }
    }

    private bool IsCursorOverColorUi(Vector2 screenPosition)
    {
        if (hoveredTarget != null)
            return true;

        if (ContainsScreenPoint(panel, screenPosition) ||
            ContainsScreenPoint(selfiePanel, screenPosition))
            return true;

        if (selfieEntryButton != null &&
            selfieEntryButton.gameObject.activeInHierarchy &&
            ContainsScreenPoint(selfieEntryButton.gameObject, screenPosition))
            return true;

        return false;
    }

    private bool ContainsScreenPoint(GameObject root, Vector2 screenPosition)
    {
        if (root == null || !root.activeInHierarchy)
            return false;

        RectTransform rect = root.GetComponent<RectTransform>();
        return rect != null &&
            RectTransformUtility.RectangleContainsScreenPoint(rect, screenPosition, canvasCamera);
    }

    private void CaptureInitialColors()
    {
        if (colorsCaptured || lanternRenderer == null)
            return;

        // Renderer.materials creates runtime-only material instances. Changing them
        // cannot modify the student's original material assets.
        runtimeMaterials = lanternRenderer.materials;
        IsolateTopLidOntoOwnMaterials();
        IsolateTasselOntoOwnMaterials();
        CaptureCommittedColors();
        colorsCaptured = true;
        ResolvePartMaterialIndices();
    }

    private void CaptureCommittedColors()
    {
        if (runtimeMaterials == null)
            return;

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
    }

    private void ApplyColor(int partIndex, Color color)
    {
        if (!colorsCaptured || runtimeMaterials == null || partIndex < 0)
            return;

        int[] slots = GetMaterialIndices(partIndex);
        for (int i = 0; i < slots.Length; i++)
            SetMaterialColor(slots[i], color);
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

        int[] slots = GetMaterialIndices(partIndex);
        for (int i = 0; i < slots.Length; i++)
        {
            int materialIndex = slots[i];
            if (materialIndex >= 0 && materialIndex < committedMaterialColors.Length)
                committedMaterialColors[materialIndex] = color;
        }
    }

    private void RestoreSelectedPreview()
    {
        if (selectedPart < 0 || committedMaterialColors == null)
            return;

        int[] slots = GetMaterialIndices(selectedPart);
        for (int i = 0; i < slots.Length; i++)
        {
            int materialIndex = slots[i];
            if (materialIndex >= 0 && materialIndex < committedMaterialColors.Length)
                SetMaterialColor(materialIndex, committedMaterialColors[materialIndex]);
        }
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

    private static Vector2 ExpandCameraPoint(
        Vector2 normalizedPoint,
        float multiplier)
    {
        Vector2 centered = normalizedPoint - Vector2.one * 0.5f;
        return new Vector2(
            Mathf.Clamp01(centered.x * multiplier + 0.5f),
            Mathf.Clamp01(centered.y * multiplier + 0.5f));
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

        if (hoveredTarget != null)
            DialogueButtonClickSound.Instance?.PlayHover();

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

        float requiredTime = selectionDwellTime;
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

        int[] slots = GetMaterialIndices(partIndex);
        for (int i = 0; i < slots.Length; i++)
        {
            if (slots[i] >= 0 && slots[i] < materialCount)
                return true;
        }

        return false;
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

    private int[] GetMaterialIndices(int partIndex)
    {
        if (partIndex == 0)
        {
            int count = runtimeMaterials != null
                ? runtimeMaterials.Length
                : lanternRenderer != null ? lanternRenderer.sharedMaterials.Length : 0;
            int[] all = new int[count];
            for (int i = 0; i < count; i++)
                all[i] = i;
            return all;
        }

        if (partMaterialIndices != null &&
            partIndex >= 0 &&
            partIndex < partMaterialIndices.Length &&
            partMaterialIndices[partIndex] != null &&
            partMaterialIndices[partIndex].Length > 0)
            return partMaterialIndices[partIndex];

        int fallback = GetMaterialIndex(partIndex);
        return fallback >= 0 ? new[] { fallback } : Array.Empty<int>();
    }

    private void ResolvePartMaterialIndices()
    {
        partMaterialIndices = new int[partNames.Length][];
        partMaterialIndices[1] = new[] { bodyMaterialIndex };
        partMaterialIndices[2] = IsolatedOrFallback(isolatedLidMaterials, topMaterialIndex);
        partMaterialIndices[3] = new[] { bottomMaterialIndex };
        partMaterialIndices[4] = IsolatedOrFallback(isolatedTasselMaterials, tasselMaterialIndex);
    }

    private int[] IsolatedOrFallback(List<Material> isolated, int fallbackIndex)
    {
        if (isolated != null && isolated.Count > 0 && runtimeMaterials != null)
        {
            List<int> slots = new List<int>();
            for (int i = 0; i < runtimeMaterials.Length; i++)
            {
                if (isolated.Contains(runtimeMaterials[i]))
                    slots.Add(i);
            }

            if (slots.Count > 0)
                return slots.ToArray();
        }

        return fallbackIndex >= 0 ? new[] { fallbackIndex } : Array.Empty<int>();
    }

    private void IsolateTopLidOntoOwnMaterials()
    {
        IsolateMovingPartOntoOwnMaterials(IsTopLidShape, isolatedLidMaterials, "_TopLid");
    }

    private void IsolateTasselOntoOwnMaterials()
    {
        IsolateMovingPartOntoOwnMaterials(IsTasselColorShape, isolatedTasselMaterials, "_Tassel");
    }

    private void IsolateMovingPartOntoOwnMaterials(
        Func<string, bool> match,
        List<Material> isolated,
        string suffix)
    {
        Mesh source = lanternRenderer != null ? lanternRenderer.sharedMesh : null;
        if (source == null || !source.isReadable || runtimeMaterials == null)
            return;

        bool[] partVerts = MarkMovingVertices(source, match);
        if (!HasMarkedVertex(partVerts))
            return;

        if (runtimePartMesh == null)
        {
            runtimePartMesh = Instantiate(source);
            runtimePartMesh.name = source.name + "_PartColors";
            lanternRenderer.sharedMesh = runtimePartMesh;
        }

        List<Material> materials = new List<Material>(runtimeMaterials);
        int originalCount = runtimePartMesh.subMeshCount;
        for (int subMesh = 0; subMesh < originalCount; subMesh++)
        {
            int[] triangles = runtimePartMesh.GetTriangles(subMesh);
            if (triangles.Length < 3)
                continue;

            List<int> partTris = new List<int>(triangles.Length);
            List<int> restTris = new List<int>(triangles.Length);
            for (int i = 0; i < triangles.Length; i += 3)
            {
                int partCount = 0;
                if (IsMarked(partVerts, triangles[i]))
                    partCount++;
                if (IsMarked(partVerts, triangles[i + 1]))
                    partCount++;
                if (IsMarked(partVerts, triangles[i + 2]))
                    partCount++;

                List<int> dest = partCount >= 2 ? partTris : restTris;
                dest.Add(triangles[i]);
                dest.Add(triangles[i + 1]);
                dest.Add(triangles[i + 2]);
            }

            if (partTris.Count == 0)
                continue;

            if (restTris.Count == 0)
            {
                if (subMesh != bodyMaterialIndex &&
                    subMesh != topMaterialIndex &&
                    subMesh != bottomMaterialIndex &&
                    subMesh < materials.Count)
                    isolated.Add(materials[subMesh]);
                continue;
            }

            runtimePartMesh.SetTriangles(restTris, subMesh, false);
            int partSlot = runtimePartMesh.subMeshCount;
            runtimePartMesh.subMeshCount = partSlot + 1;
            runtimePartMesh.SetTriangles(partTris, partSlot, false);

            Material sourceMat = subMesh < materials.Count ? materials[subMesh] : null;
            Material partMat = sourceMat != null
                ? new Material(sourceMat)
                : new Material(Shader.Find("Universal Render Pipeline/Lit"));
            partMat.name = (sourceMat != null ? sourceMat.name : "Lantern") + suffix;
            materials.Add(partMat);
            isolated.Add(partMat);
        }

        runtimePartMesh.RecalculateBounds();
        runtimeMaterials = materials.ToArray();
        lanternRenderer.materials = runtimeMaterials;
    }

    private static bool[] MarkMovingVertices(Mesh mesh, Func<string, bool> match)
    {
        bool[] moving = new bool[mesh.vertexCount];
        Vector3[] delta = new Vector3[mesh.vertexCount];
        for (int shape = 0; shape < mesh.blendShapeCount; shape++)
        {
            if (!match(mesh.GetBlendShapeName(shape)))
                continue;

            mesh.GetBlendShapeFrameVertices(shape, 0, delta, null, null);
            float peak = 0f;
            for (int i = 0; i < delta.Length; i++)
                peak = Mathf.Max(peak, delta[i].sqrMagnitude);
            if (peak < 1e-10f)
                continue;

            float cutoff = peak * 0.18f * 0.18f;
            for (int i = 0; i < delta.Length; i++)
            {
                if (delta[i].sqrMagnitude >= cutoff)
                    moving[i] = true;
            }
        }

        return moving;
    }

    private static bool HasMarkedVertex(bool[] marks)
    {
        for (int i = 0; i < marks.Length; i++)
        {
            if (marks[i])
                return true;
        }

        return false;
    }

    private static bool IsMarked(bool[] marks, int index)
    {
        return index >= 0 && index < marks.Length && marks[index];
    }

    private static bool IsTopLidShape(string shapeName)
    {
        string compact = CompactShapeName(shapeName);
        if (!compact.Contains("TOP") || compact.Contains("BOTTOM"))
            return false;
        return compact.Contains("CIRCLE") || compact.Contains("RECTANGLE");
    }

    private static bool IsTasselColorShape(string shapeName)
    {
        string compact = CompactShapeName(shapeName);
        return compact.Contains("TASSEL") ||
               compact.Contains("TASSLE") ||
               compact.Contains("BAMBOO");
    }

    private void RegisterHitTarget(
        Button button,
        bool isColor,
        bool isPersistentChoice,
        Color previewColor,
        bool isFreeColorPalette = false)
    {
        Outline outline = button.gameObject.GetComponent<Outline>();
        if (outline == null)
            outline = button.gameObject.AddComponent<Outline>();
        outline.effectColor = new Color32(77, 220, 203, 255);
        outline.effectDistance = new Vector2(4f, -4f);
        outline.enabled = false;

        Image[] dwellEdges = CreateDwellFrame(button.transform);

        Transform checkTransform = button.transform.Find("SelectionCheck");
        Image buttonImage = button.GetComponent<Image>();
        hitTargets.Add(new HitTarget
        {
            Button = button,
            Rect = button.GetComponent<RectTransform>(),
            Outline = outline,
            IsColor = isColor,
            IsFreeColorPalette = isFreeColorPalette,
            IsPersistentChoice = isPersistentChoice,
            PreviewColor = previewColor,
            BaseColor = buttonImage != null ? buttonImage.color : Color.white,
            SelectionCheck = checkTransform != null
                ? checkTransform.GetComponent<RectTransform>()
                : null,
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
            if (selectedColorTarget.SelectionCheck != null)
                selectedColorTarget.SelectionCheck.gameObject.SetActive(true);
        }
        else
        {
            ClearSelectedPartVisual();
            selectedPartTarget = target;
            if (selectedPartTarget.Button != null &&
                selectedPartTarget.Button.image != null)
            {
                selectedPartTarget.Button.image.color =
                    new Color32(202, 123, 57, 255);
            }
        }
    }

    private void ClearSelectedPartVisual()
    {
        if (selectedPartTarget != null && selectedPartTarget.Button != null &&
            selectedPartTarget.Button.image != null)
        {
            selectedPartTarget.Button.image.color = selectedPartTarget.BaseColor;
        }
        selectedPartTarget = null;
    }

    private void ClearSelectedColorVisual()
    {
        if (selectedColorTarget != null && selectedColorTarget.SelectionCheck != null)
            selectedColorTarget.SelectionCheck.gameObject.SetActive(false);
        selectedColorTarget = null;
    }

    private Image[] CreateDwellFrame(Transform parent)
    {
        Color progressColor = new Color32(255, 224, 92, 255);
        Image[] edges = new Image[4];

        edges[0] = CreateDwellEdge(
            "DwellTop", parent,
            new Vector2(0f, 1f), new Vector2(1f, 1f),
            new Vector2(6f, 6f), new Vector2(0f, 3f),
            Image.FillMethod.Horizontal, (int)Image.OriginHorizontal.Left,
            progressColor);
        edges[1] = CreateDwellEdge(
            "DwellRight", parent,
            new Vector2(1f, 0f), new Vector2(1f, 1f),
            new Vector2(6f, 6f), new Vector2(3f, 0f),
            Image.FillMethod.Vertical, (int)Image.OriginVertical.Top,
            progressColor);
        edges[2] = CreateDwellEdge(
            "DwellBottom", parent,
            new Vector2(0f, 0f), new Vector2(1f, 0f),
            new Vector2(6f, 6f), new Vector2(0f, -3f),
            Image.FillMethod.Horizontal, (int)Image.OriginHorizontal.Right,
            progressColor);
        edges[3] = CreateDwellEdge(
            "DwellLeft", parent,
            new Vector2(0f, 0f), new Vector2(0f, 1f),
            new Vector2(6f, 6f), new Vector2(-3f, 0f),
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
        Transform existing = parent.Find(objectName);
        GameObject edgeObject;
        if (existing != null && existing.TryGetComponent(out Image _))
        {
            edgeObject = existing.gameObject;
        }
        else
        {
            edgeObject = new GameObject(
                objectName, typeof(RectTransform), typeof(Image));
            edgeObject.transform.SetParent(parent, false);
        }

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

    private Button CreateFreeColorPalette(
        Transform parent,
        Vector2 anchoredPosition,
        Vector2 size)
    {
        GameObject paletteObject = new GameObject(
            "FreeColorPalette",
            typeof(RectTransform), typeof(RawImage), typeof(Button));
        paletteObject.transform.SetParent(parent, false);
        RectTransform rect = paletteObject.GetComponent<RectTransform>();
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 1f);
        rect.pivot = new Vector2(0.5f, 1f);
        rect.anchoredPosition = anchoredPosition;
        rect.sizeDelta = size;

        freeColorPaletteImage = paletteObject.GetComponent<RawImage>();
        ConfigureFreeColorPalette(freeColorPaletteImage);

        Button button = paletteObject.GetComponent<Button>();
        button.targetGraphic = freeColorPaletteImage;
        button.transition = Selectable.Transition.None;
        freeColorSelectionCheck = CreateSelectionCheck(rect, true);
        return button;
    }

    private void ConfigureFreeColorPalette(RawImage image)
    {
        if (image == null)
            return;

        const int width = 160;
        const int height = 72;
        Texture2D texture = new Texture2D(
            width, height, TextureFormat.RGB24, false)
        {
            name = "MorphicFreeColorPalette",
            filterMode = FilterMode.Bilinear,
            wrapMode = TextureWrapMode.Clamp,
            hideFlags = HideFlags.HideAndDontSave
        };

        for (int y = 0; y < height; y++)
        {
            float normalizedY = y / (float)(height - 1);
            for (int x = 0; x < width; x++)
            {
                float normalizedX = x / (float)(width - 1);
                texture.SetPixel(
                    x, y,
                    EvaluateFreePaletteColor(normalizedX, normalizedY));
            }
        }

        texture.Apply(false, true);
        image.texture = texture;
        image.color = Color.white;
        image.raycastTarget = true;
    }

    private RectTransform CreateSelectionCheck(
        RectTransform parent,
        bool movable = false)
    {
        GameObject checkObject = new GameObject(
            "SelectionCheck", typeof(RectTransform));
        checkObject.transform.SetParent(parent, false);
        RectTransform check = checkObject.GetComponent<RectTransform>();
        check.anchorMin = check.anchorMax = movable
            ? new Vector2(0.5f, 1f)
            : new Vector2(0.5f, 0.5f);
        check.pivot = new Vector2(0.5f, 0.5f);
        check.anchoredPosition = Vector2.zero;
        check.sizeDelta = new Vector2(38f, 38f);

        // Draw the tick from UI images instead of a font glyph. This avoids
        // unsupported-glyph squares and gives a consistent black border.
        CreateCheckStroke(
            "LeftBorder", check, new Vector2(-5f, -2f),
            new Vector2(15f, 8f), -45f, Color.black);
        CreateCheckStroke(
            "RightBorder", check, new Vector2(5f, 0f),
            new Vector2(23f, 8f), 45f, Color.black);
        CreateCheckStroke(
            "LeftFill", check, new Vector2(-5f, -2f),
            new Vector2(15f, 4f), -45f, Color.white);
        CreateCheckStroke(
            "RightFill", check, new Vector2(5f, 0f),
            new Vector2(23f, 4f), 45f, Color.white);

        checkObject.SetActive(false);
        return check;
    }

    private static void CreateCheckStroke(
        string objectName,
        Transform parent,
        Vector2 anchoredPosition,
        Vector2 size,
        float rotation,
        Color color)
    {
        GameObject strokeObject = new GameObject(
            objectName, typeof(RectTransform), typeof(Image));
        strokeObject.transform.SetParent(parent, false);
        RectTransform rect = strokeObject.GetComponent<RectTransform>();
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = anchoredPosition;
        rect.sizeDelta = size;
        rect.localRotation = Quaternion.Euler(0f, 0f, rotation);

        Image image = strokeObject.GetComponent<Image>();
        image.color = color;
        image.raycastTarget = false;
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

#if UNITY_EDITOR
    public void RebuildSceneInterfaceForEditor()
    {
        canvas = gestureIndicator != null
            ? gestureIndicator.GetComponentInParent<Canvas>()
            : FindFirstObjectByType<Canvas>();

        if (canvas == null)
            throw new InvalidOperationException(
                "No Canvas was found for the lantern color interface.");

        canvasRect = canvas.GetComponent<RectTransform>();
        canvasCamera = canvas.renderMode == RenderMode.ScreenSpaceOverlay
            ? null
            : canvas.worldCamera;

        Transform existingRoot = canvas.transform.Find("SculptingEnhancementColorUI");
        if (existingRoot != null)
            DestroyImmediate(existingRoot.gameObject);

        BuildInterfaceHierarchy();
        ShowSculptMode();
        SetCursorVisible(false);
        UnityEditor.EditorUtility.SetDirty(canvas.gameObject);
        UnityEditor.EditorUtility.SetDirty(this);
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(gameObject.scene);
    }
#endif
}
