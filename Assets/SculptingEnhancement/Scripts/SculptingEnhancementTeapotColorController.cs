using System;
using System.Collections.Generic;
using Mediapipe.Unity.Sample;
using Mediapipe.Unity.Sample.HandLandmarkDetection;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

[DefaultExecutionOrder(260)]
public sealed class SculptingEnhancementTeapotColorController : MonoBehaviour
{
    private enum ColorMode
    {
        Sculpt,
        MainMenu,
        SelectPart,
        SelectBodyActions,
        SelectVariant,
        SelectColor,
        SelectDesign,
        Selfie,
        Review,
        Shelf,
        ConfirmDelete,
        ConfirmReset
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
        public Image[] DwellEdges;
    }

    [SerializeField] private SculptHandLandmarkerRunner handRunner;
    [SerializeField] private SkinnedMeshRenderer teapotRenderer;
    [SerializeField] private TeapotGestureController teapotGestures;
    [SerializeField] private TeapotFistRotationController teapotFistRotation;
    [SerializeField] private SculptingEnhancementTeapotIndexGestureController teapotIndexGestures;
    [SerializeField] private TMP_Text gestureIndicator;
    [SerializeField, Min(0)] private int bodyMaterialIndex = 1;
    [SerializeField, Range(0.25f, 2.5f)] private float selectionDwellTime = 1.5f;
    [SerializeField, Min(1f)] private float cursorSmoothing = 24f;
    [SerializeField, Range(1f, 2.5f)] private float cameraToScreenRangeMultiplier = 1.8f;
    [Header("Body Designs")]
    [SerializeField] private Texture2D[] stampPatterns;
    [SerializeField, Range(3f, 12f)] private float designRepeatCount = 6f;
    [SerializeField, Range(0.08f, 0.5f)] private float designPatchHalfAngle = 0.22f;
    [SerializeField, Range(0.15f, 0.7f)] private float designVerticalHalfFraction = 0.38f;

    private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
    private static readonly int ColorId = Shader.PropertyToID("_Color");
    private static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");
    private static readonly Color BandGrayColor = new Color(0.62f, 0.62f, 0.62f, 1f);

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
        "Whole Teapot",
        "Teapot Body",
        "Teapot Lid",
        "Teapot Spout",
        "Teapot Handle"
    };

    private readonly List<HitTarget> hitTargets = new List<HitTarget>();
    private readonly List<Material> isolatedLidMaterials = new List<Material>();
    private readonly List<Material> isolatedSpoutMaterials = new List<Material>();
    private readonly List<Material> isolatedHandleMaterials = new List<Material>();
    private readonly List<TeapotSnapshot> history = new List<TeapotSnapshot>();

    private Material[] runtimeMaterials;
    private Color[] committedMaterialColors;
    private int[][] partMaterialIndices;
    private Mesh runtimePartMesh;

    private Canvas canvas;
    private RectTransform canvasRect;
    private Camera canvasCamera;
    private GameObject uiRoot;
    private GameObject panel;
    private GameObject mainActionGroup;
    private GameObject partGroup;
    private GameObject variantGroup;
    private GameObject bodyActionGroup;
    private GameObject designGroup;
    private GameObject colorGroup;
    private GameObject reviewGroup;
    private GameObject shelfGroup;
    private GameObject confirmDeleteGroup;
    private TMP_Text savedStatusText;
    private bool teapotSaved;
    private LanternShelfController teapotShelf;
    private GameObject historyBar;
    private GameObject confirmResetGroup;
    private GameObject selfiePanel;
    private Button selfieEntryButton;
    private RectTransform cursorRect;
    private TMP_Text headerText;
    private TMP_Text headerBackText;
    private Button headerBackButton;
    private TMP_Text instructionText;
    private GameObject titleBurstRoot;
    private TMP_Text selfieHeaderText;
    private TMP_Text selfieInstructionText;
    private RawImage selfiePreviewImage;
    private Button takeSelfieButton;
    private Button selfieBackButton;
    private Button attachSelfieButton;
    private Button retakeSelfieButton;
    private Button selfieDoneButton;
    private RawImage freeColorPaletteImage;
    private RectTransform freeColorSelectionCheck;
    private Color freePalettePreviewColor = Color.white;
    private Vector2 freePaletteMarkerPosition;
    private Vector2 lastColorSampleScreen;
    private TMP_Text cursorText;
    private HitTarget hoveredTarget;
    private HitTarget dwellTarget;
    private HitTarget completedDwellTarget;
    private ColorMode mode = ColorMode.Sculpt;
    private int selectedPart = -1;
    private float dwellTimer;
    private bool colorsCaptured;
    private bool paletteAvailable;
    private bool sculptLockedByTutorial;
    private bool uiPointerBlockingSculpt;
    private ColorMode modeBeforeConfirm;
    private int selectedVariant = -1;
    private string selectedVariantLabel = "";
    private bool designTintActive;
    private bool colorBackToDesigns;
    private bool lastDeformationLocked;
    private readonly Button[] variantButtons = new Button[2];
    private readonly TMP_Text[] variantButtonLabels = new TMP_Text[2];
    private readonly Button[] designButtons = new Button[6];
    private readonly RawImage[] designPreviews = new RawImage[6];
    private Transform designSurfaceRoot;
    private SkinnedMeshRenderer designSurfaceRenderer;
    private Material designSurfaceMaterial;
    private Material designHiddenMaterial;
    private Texture appliedDesignTexture;
    private Color committedDesignTint = Color.white;
    private Texture2D capturedSelfieTexture;
    private Texture2D appliedSelfieTexture;
    private Transform selfieDecalRoot;
    private Material selfieDecalMaterial;
    private Mesh selfieQuadMesh;
    private readonly Transform[] selfiePlates = new Transform[1];
    private readonly Vector3[] selfieLocalOutwards = new Vector3[1];
    private Material selfieSurfaceMaterial;
    private Material selfieHiddenMaterial;
    private SkinnedMeshRenderer selfieSurfaceRenderer;
    private Transform selfieSurfaceRoot;
    private Mesh selfieBoundsMesh;
    private bool[] selfieBodyVertexMask;
    private Vector3 selfieUpLocal = Vector3.up;
    private Vector3 selfieFrontLocal = Vector3.forward;
    private static readonly Color CardCream = new Color32(255, 239, 214, 255);
    private static readonly Color PillPink = LanguagePageBuilder.Pink;
    private static readonly Color ActionPink = new Color32(242, 154, 214, 255);
    private static readonly Color DonePink = new Color32(236, 64, 168, 255);
    private const float CustomizePanelWidth = 420f;
    private const float PillWidth = 360f;
    private const float PillHeight = 60f;
    private const float MainMenuPanelHeight = 472f;
    private const float ReviewPanelHeight = 420f;
    private const float ShelfPanelHeight = 430f;
    private const float PartSelectionPanelHeight = 700f;
    private const float ColorSelectionPanelHeight = 640f;
    private const float SelfiePanelHeight = 560f;
    private const float VariantSelectionPanelHeight = 420f;
    private const float BodyActionsPanelHeight = 360f;
    private const float DesignSelectionPanelHeight = 560f;
    private const float HistoryBarHeight = 72f;
    private const int MaxHistory = 20;

    private sealed class TeapotSnapshot
    {
        public float[] BlendWeights;
        public Color[] MaterialColors;
        public Texture Stamp;
        public Color StampTint;
        public bool StampVisible;
    }

    public void WriteHelpDecor(MorphicCraftHelpState state)
    {
        if (state == null)
            return;

        state.hasStamp = appliedDesignTexture != null ? 1 : 0;
        state.hasSelfie = appliedSelfieTexture != null ? 1 : 0;
        state.currentStage = mode == ColorMode.Sculpt ? "sculpt" : "customize";
        state.currentTool = "teapot";
    }

    public void BindFrom(
        SculptHandLandmarkerRunner runner,
        SkinnedMeshRenderer renderer,
        TeapotGestureController gestures)
    {
        if (handRunner == null)
            handRunner = runner;
        if (teapotRenderer == null)
            teapotRenderer = renderer;
        if (teapotGestures == null)
            teapotGestures = gestures;
        if (teapotFistRotation == null && gestures != null)
            teapotFistRotation = gestures.GetComponent<TeapotFistRotationController>();
        if (teapotIndexGestures == null && gestures != null)
            teapotIndexGestures = gestures.GetComponent<SculptingEnhancementTeapotIndexGestureController>();
        if (gestureIndicator == null && gestures != null)
            gestureIndicator = gestures.GestureStatus;
    }

    public void SetPaletteAvailable(bool available)
    {
        paletteAvailable = available;
        if (uiRoot == null)
            return;

        if (uiRoot != null)
            uiRoot.SetActive(true);

        if (!available)
        {
            RestoreSelectedPreview();
            HideLeftoverSelfieCards();
            if (!sculptLockedByTutorial)
                SetSculptControllersEnabled(true);
            ShowSculptMode();
            return;
        }

        ShowMainMenu();
        CaptureBaseline();
    }

    public void SetTutorialSculptLock(bool locked)
    {
        sculptLockedByTutorial = locked;
        if (locked)
            SetSculptControllersEnabled(false);
    }

    private void Start()
    {
        BindFrom(handRunner, teapotRenderer, teapotGestures);
        if (teapotRenderer == null && teapotGestures != null)
            teapotRenderer = teapotGestures.TeapotRenderer;
        if (handRunner == null)
            handRunner = FindFirstObjectByType<SculptHandLandmarkerRunner>(FindObjectsInactive.Include);

        EnsureTeapotIndexHelper();
        EnsureTeapotShelf();
        if (teapotShelf != null)
            teapotShelf.ClearCollectionDisplays();
        TeapotCollectionStore.Clear();
        BuildInterface();
        LoadStampPatterns();
        if (paletteAvailable)
        {
            ShowMainMenu();
            CaptureBaseline();
        }
        else
            ShowSculptMode();
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
        if (selfieDecalRoot != null)
            Destroy(selfieDecalRoot.gameObject);
        if (selfieSurfaceRoot != null)
            Destroy(selfieSurfaceRoot.gameObject);
        DestroyRuntime(capturedSelfieTexture);
        DestroyRuntime(appliedSelfieTexture);
        DestroyRuntime(selfieDecalMaterial);
        DestroyRuntime(selfieQuadMesh);
        DestroyRuntime(selfieSurfaceMaterial);
        DestroyRuntime(selfieHiddenMaterial);
        DestroyRuntime(selfieBoundsMesh);
        DestroyRuntime(runtimePartMesh);
        if (designSurfaceRoot != null)
            Destroy(designSurfaceRoot.gameObject);
        DestroyRuntime(designSurfaceMaterial);
        DestroyRuntime(designHiddenMaterial);
        for (int i = 0; i < isolatedLidMaterials.Count; i++)
            DestroyRuntime(isolatedLidMaterials[i]);
        for (int i = 0; i < isolatedSpoutMaterials.Count; i++)
            DestroyRuntime(isolatedSpoutMaterials[i]);
        for (int i = 0; i < isolatedHandleMaterials.Count; i++)
            DestroyRuntime(isolatedHandleMaterials[i]);
    }

    private void Update()
    {
        if (uiRoot == null || handRunner == null || canvasRect == null)
            return;

        if (mode == ColorMode.Selfie && capturedSelfieTexture == null)
            ShowLiveSelfiePreview();

        Vector2 screenPosition;
        if (handRunner.TryGetLatestHand(out SculptHandFrame hand))
        {
            SetCursorVisible(true);
            Vector2 expanded = ExpandCameraPoint(
                new Vector2(hand.IndexTip.x, hand.IndexTip.y),
                cameraToScreenRangeMultiplier);
            screenPosition = new Vector2(expanded.x * Screen.width, expanded.y * Screen.height);
            MoveCursor(screenPosition);
        }
        else
        {
            SetCursorVisible(false);
            screenPosition = Input.mousePosition;
            if (mode != ColorMode.SelectColor)
            {
                SetHoveredTarget(null);
                ResetDwell();
                return;
            }
        }

        lastColorSampleScreen = screenPosition;
        HitTarget nextHovered = FindHitTarget(screenPosition);

        if (mode == ColorMode.MainMenu)
        {
            bool shouldBlock = nextHovered != null;
            if (uiPointerBlockingSculpt != shouldBlock)
            {
                uiPointerBlockingSculpt = shouldBlock;
                SetSculptControllersEnabled(!uiPointerBlockingSculpt);
            }
        }

        SetHoveredTarget(nextHovered);
        UpdateFreePalettePreview(nextHovered, screenPosition);
        UpdateDwellSelection(nextHovered);
        UpdateSculptLockFromUi(IsCursorOverColorUi(screenPosition));
    }

    private void LateUpdate()
    {
        PollSculptHistory();
        if (designSurfaceRenderer != null)
        {
            SyncDesignSurfaceRenderer();
            if (appliedDesignTexture != null)
                ConfigureDesignSurfaceMaterial(appliedDesignTexture);
        }

        if (appliedSelfieTexture == null)
            return;

        if (selfieSurfaceRenderer != null)
        {
            SyncSelfieSurfaceRenderer();
            ConfigureSelfieSurfaceMaterial(appliedSelfieTexture);
        }
        else if (selfieDecalRoot != null)
        {
            RefreshSelfiePlates();
        }
    }

    private void BuildInterface()
    {
        canvas = gestureIndicator != null
            ? gestureIndicator.GetComponentInParent<Canvas>()
            : FindFirstObjectByType<Canvas>();
        if (canvas == null)
            return;

        canvasRect = canvas.transform as RectTransform;
        canvasCamera = canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
        uiRoot = new GameObject("TeapotColorUI", typeof(RectTransform));
        uiRoot.transform.SetParent(canvas.transform, false);
        Stretch(uiRoot.GetComponent<RectTransform>());

        panel = new GameObject("TeapotColorPanel", typeof(RectTransform));
        panel.transform.SetParent(uiRoot.transform, false);
        RectTransform panelRect = panel.GetComponent<RectTransform>();
        StyleMorphicCard(panelRect);
        ApplyBottomRightDocking(panelRect);

        headerText = CreateText(
            "Header", panelRect, new Vector2(20f, -16f),
            new Vector2(380f, 56f), "COLOR PALETTE", 36f,
            TextAlignmentOptions.Center, FontStyles.Bold);
        StyleMorphicTitle(headerText);
        BuildTitleBursts(panelRect);
        headerBackButton = CreateHeaderBackButton(panelRect);
        headerBackText = headerBackButton.GetComponentInChildren<TMP_Text>();
        instructionText = CreateText(
            "Instruction", panelRect, new Vector2(20f, -78f),
            new Vector2(380f, 36f), "Choose the whole teapot or one part.", 18f,
            TextAlignmentOptions.Center, FontStyles.Bold);
        instructionText.color = LanguagePageBuilder.Green;

        mainActionGroup = new GameObject("MainActions", typeof(RectTransform));
        mainActionGroup.transform.SetParent(panelRect, false);
        RectTransform mainRect = mainActionGroup.GetComponent<RectTransform>();
        DockTop(mainRect, -196f, 250f);

        selfieEntryButton = CreateButton("SelfieEntry", panelRect, new Vector2(0f, -118f),
            new Vector2(PillWidth, PillHeight), "SELFIE", new Color32(155, 92, 130, 255));
        selfieEntryButton.onClick.AddListener(ShowSelfieCapture);
        RegisterHitTarget(selfieEntryButton, false, false, default);

        Button selectPart = CreateButton("SelectPart", mainRect, Vector2.zero,
            new Vector2(PillWidth, PillHeight), "SELECT PART", PillPink);
        selectPart.onClick.AddListener(ShowPartSelection);
        RegisterHitTarget(selectPart, false, false, default);

        Button changeColor = CreateButton("ChangeColor", mainRect, new Vector2(0f, -72f),
            new Vector2(PillWidth, PillHeight), "CHANGE COLOR", ActionPink);
        changeColor.onClick.AddListener(ContinueToColorSelection);
        RegisterHitTarget(changeColor, false, false, default);

        Button finish = CreateButton("Finish", mainRect, new Vector2(0f, -144f),
            new Vector2(PillWidth, PillHeight), "FINISH", DonePink);
        finish.onClick.AddListener(ShowReview);
        RegisterHitTarget(finish, false, false, default);

        partGroup = new GameObject("PartSelection", typeof(RectTransform));
        partGroup.transform.SetParent(panelRect, false);
        RectTransform partRect = partGroup.GetComponent<RectTransform>();
        DockTop(partRect, -124f, 550f);

        for (int i = 0; i < partNames.Length; i++)
        {
            int partIndex = i;
            Button partButton = CreateButton(
                "Part" + i, partRect, new Vector2(0f, -i * 70f),
                new Vector2(PillWidth, PillHeight), partNames[i], PillPink);
            partButton.onClick.AddListener(() => SelectPart(partIndex));
            RegisterHitTarget(partButton, false, true, default);
        }

        Button nextColors = CreateButton("NextColors", partRect, new Vector2(0f, -360f),
            new Vector2(PillWidth, PillHeight), "CHANGE COLOR", ActionPink);
        nextColors.onClick.AddListener(ContinueToColorSelection);
        RegisterHitTarget(nextColors, false, false, default);

        Button partDone = CreateButton("Done", partRect, new Vector2(0f, -438f),
            new Vector2(PillWidth, PillHeight), "DONE", DonePink);
        partDone.onClick.AddListener(ShowMainMenu);
        RegisterHitTarget(partDone, false, false, default);

        colorGroup = new GameObject("ColorSelection", typeof(RectTransform));
        colorGroup.transform.SetParent(panelRect, false);
        RectTransform colorRect = colorGroup.GetComponent<RectTransform>();
        DockTop(colorRect, -124f, 490f);

        for (int i = 0; i < paletteColors.Length; i++)
        {
            Color color = paletteColors[i];
            int column = i % 4;
            int row = i / 4;
            Button swatch = CreateButton(
                "Color" + i, colorRect,
                new Vector2(-126f + column * 84f, -row * 78f),
                new Vector2(68f, 68f), string.Empty, color);
            swatch.onClick.AddListener(() => CommitColor(color));
            CreateSelectionCheck(swatch.GetComponent<RectTransform>());
            RegisterHitTarget(swatch, true, true, color);
        }

        Button freeColorPaletteButton = CreateFreeColorPalette(
            colorRect, new Vector2(0f, -168f), new Vector2(PillWidth, 180f));
        freeColorPaletteButton.onClick.AddListener(CommitFreePaletteColor);
        RegisterHitTarget(freeColorPaletteButton, true, true, Color.white, true);

        Button colorDone = CreateButton("Done", colorRect, new Vector2(0f, -368f),
            new Vector2(PillWidth, PillHeight), "DONE", DonePink);
        colorDone.onClick.AddListener(ShowMainMenu);
        RegisterHitTarget(colorDone, false, false, default);

        BuildHistoryBar(panelRect);
        BuildConfirmReset(panelRect);
        BuildReview(panelRect);
        BuildSelfie(uiRoot.transform);
        BuildCursor();
        ApplyCenteredTypography();
    }

    private void BuildHistoryBar(RectTransform panelRect)
    {
        historyBar = new GameObject("HistoryBar", typeof(RectTransform));
        historyBar.transform.SetParent(panelRect, false);
        RectTransform barRect = historyBar.GetComponent<RectTransform>();
        barRect.anchorMin = new Vector2(0f, 0f);
        barRect.anchorMax = new Vector2(1f, 0f);
        barRect.pivot = new Vector2(0.5f, 0f);
        barRect.anchoredPosition = new Vector2(0f, 18f);
        barRect.sizeDelta = new Vector2(-32f, 56f);

        Button undo = CreateButton("Undo", barRect, new Vector2(-92f, 0f),
            new Vector2(168f, 52f), "UNDO", PillPink);
        CenterInBar(undo, -92f);
        undo.onClick.AddListener(UndoHistory);
        RegisterHitTarget(undo, false, false, default);

        Button reset = CreateButton("Reset", barRect, new Vector2(92f, 0f),
            new Vector2(168f, 52f), "RESET", DonePink);
        CenterInBar(reset, 92f);
        reset.onClick.AddListener(ShowConfirmReset);
        RegisterHitTarget(reset, false, false, default);
    }

    private void BuildConfirmReset(RectTransform panelRect)
    {
        confirmResetGroup = new GameObject("ConfirmReset", typeof(RectTransform));
        confirmResetGroup.transform.SetParent(panelRect, false);
        RectTransform rect = confirmResetGroup.GetComponent<RectTransform>();
        DockTop(rect, -124f, 180f);

        Button cancel = CreateButton("Cancel", rect, Vector2.zero,
            new Vector2(PillWidth, PillHeight), "CANCEL", PillPink);
        cancel.onClick.AddListener(HideConfirmReset);
        RegisterHitTarget(cancel, false, false, default);

        Button confirm = CreateButton("Reset", rect, new Vector2(0f, -72f),
            new Vector2(PillWidth, PillHeight), "RESET", DonePink);
        confirm.onClick.AddListener(ConfirmReset);
        RegisterHitTarget(confirm, false, false, default);
        confirmResetGroup.SetActive(false);
    }

    private void BuildReview(RectTransform panelRect)
    {
        reviewGroup = new GameObject("Review", typeof(RectTransform));
        reviewGroup.transform.SetParent(panelRect, false);
        RectTransform reviewRect = reviewGroup.GetComponent<RectTransform>();
        DockTop(reviewRect, -124f, 320f);

        savedStatusText = CreateText(
            "SavedStatus", reviewRect, new Vector2(0f, 8f),
            new Vector2(PillWidth, 28f), "", 18f,
            TextAlignmentOptions.Center, FontStyles.Bold);
        savedStatusText.color = LanguagePageBuilder.Green;

        Button save = CreateButton(
            "Save", reviewRect, new Vector2(0f, -28f),
            new Vector2(PillWidth, PillHeight), "SAVE", PillPink);
        save.onClick.AddListener(SaveTeapotReview);
        RegisterHitTarget(save, false, false, default);

        Button retake = CreateButton(
            "Retake", reviewRect, new Vector2(0f, -100f),
            new Vector2(PillWidth, PillHeight), "RETAKE", PillPink);
        retake.onClick.AddListener(ShowMainMenu);
        RegisterHitTarget(retake, false, false, default);

        Button finish = CreateButton(
            "Finish", reviewRect, new Vector2(0f, -172f),
            new Vector2(PillWidth, PillHeight), "FINISH", DonePink);
        finish.onClick.AddListener(FinishToShelf);
        RegisterHitTarget(finish, false, false, default);

        shelfGroup = new GameObject("ShelfActions", typeof(RectTransform));
        shelfGroup.transform.SetParent(panelRect, false);
        RectTransform shelfRect = shelfGroup.GetComponent<RectTransform>();
        DockTop(shelfRect, -124f, 250f);

        Button takeBack = CreateButton(
            "TakeBack", shelfRect, new Vector2(0f, 0f),
            new Vector2(PillWidth, PillHeight), "TAKE BACK", PillPink);
        takeBack.onClick.AddListener(TakeBackFromShelf);
        RegisterHitTarget(takeBack, false, false, default);

        Button delete = CreateButton(
            "Delete", shelfRect, new Vector2(0f, -72f),
            new Vector2(PillWidth, PillHeight), "DELETE", DonePink);
        delete.onClick.AddListener(ShowConfirmDelete);
        RegisterHitTarget(delete, false, false, default);

        Button done = CreateButton(
            "Done", shelfRect, new Vector2(0f, -144f),
            new Vector2(PillWidth, PillHeight), "DONE", DonePink);
        done.onClick.AddListener(LeaveShelfDone);
        RegisterHitTarget(done, false, false, default);

        confirmDeleteGroup = new GameObject("ConfirmDelete", typeof(RectTransform));
        confirmDeleteGroup.transform.SetParent(panelRect, false);
        RectTransform confirmRect = confirmDeleteGroup.GetComponent<RectTransform>();
        DockTop(confirmRect, -124f, 170f);

        Button cancelDelete = CreateButton(
            "Cancel", confirmRect, new Vector2(0f, 0f),
            new Vector2(PillWidth, PillHeight), "CANCEL", PillPink);
        cancelDelete.onClick.AddListener(ShowShelf);
        RegisterHitTarget(cancelDelete, false, false, default);

        Button confirmDelete = CreateButton(
            "Delete", confirmRect, new Vector2(0f, -72f),
            new Vector2(PillWidth, PillHeight), "DELETE", DonePink);
        confirmDelete.onClick.AddListener(ConfirmDeleteTeapot);
        RegisterHitTarget(confirmDelete, false, false, default);

        HideReviewAndShelf();
        RefreshSavedStatus();
    }

    private void BuildSelfie(Transform parent)
    {
        selfiePanel = new GameObject("TeapotSelfiePanel", typeof(RectTransform), typeof(Image));
        selfiePanel.transform.SetParent(parent, false);
        RectTransform rect = selfiePanel.GetComponent<RectTransform>();
        rect.anchorMin = rect.anchorMax = new Vector2(1f, 0f);
        rect.pivot = new Vector2(1f, 0f);
        rect.anchoredPosition = new Vector2(-24f, 24f);
        rect.sizeDelta = new Vector2(CustomizePanelWidth, SelfiePanelHeight);
        selfiePanel.GetComponent<Image>().color = Color.clear;
        StyleMorphicCard(rect);

        selfieHeaderText = CreateText("Header", rect, new Vector2(20f, -16f),
            new Vector2(380f, 48f), "SELFIE", 32f, TextAlignmentOptions.Center, FontStyles.Bold);
        StyleMorphicTitle(selfieHeaderText);
        selfieInstructionText = CreateText("Instruction", rect, new Vector2(20f, -70f),
            new Vector2(380f, 40f), "Frame your photo, then take a selfie.", 18f,
            TextAlignmentOptions.Center, FontStyles.Bold);
        selfieInstructionText.color = LanguagePageBuilder.Green;

        GameObject content = new GameObject("Content", typeof(RectTransform));
        content.transform.SetParent(rect, false);
        RectTransform selfieRect = content.GetComponent<RectTransform>();
        DockTop(selfieRect, -118f, 450f);

        GameObject preview = new GameObject("Preview", typeof(RectTransform), typeof(RawImage), typeof(Outline));
        preview.transform.SetParent(selfieRect, false);
        selfiePreviewImage = preview.GetComponent<RawImage>();
        selfiePreviewImage.color = Color.white;
        selfiePreviewImage.raycastTarget = false;
        Outline previewOutline = preview.GetComponent<Outline>();
        previewOutline.effectColor = new Color32(82, 188, 189, 180);
        previewOutline.effectDistance = new Vector2(2f, -2f);
        RectTransform previewRect = selfiePreviewImage.rectTransform;
        previewRect.anchorMin = previewRect.anchorMax = new Vector2(0.5f, 1f);
        previewRect.pivot = new Vector2(0.5f, 1f);
        previewRect.anchoredPosition = Vector2.zero;
        previewRect.sizeDelta = new Vector2(294f, 220f);

        takeSelfieButton = CreateButton("TakeSelfie", selfieRect, new Vector2(0f, -234f),
            new Vector2(PillWidth, PillHeight), "TAKE SELFIE", PillPink);
        takeSelfieButton.onClick.AddListener(CaptureSelfie);
        RegisterHitTarget(takeSelfieButton, false, false, default);

        selfieBackButton = CreateButton("Back", selfieRect, new Vector2(0f, -306f),
            new Vector2(PillWidth, PillHeight), "BACK", PillPink);
        selfieBackButton.onClick.AddListener(ShowMainMenu);
        RegisterHitTarget(selfieBackButton, false, false, default);

        attachSelfieButton = CreateButton("AttachToTeapot", selfieRect, new Vector2(0f, -234f),
            new Vector2(PillWidth, PillHeight), "ATTACH TO TEAPOT", ActionPink);
        attachSelfieButton.onClick.AddListener(AttachSelfie);
        RegisterHitTarget(attachSelfieButton, false, false, default);

        retakeSelfieButton = CreateButton("Retake", selfieRect, new Vector2(-92f, -306f),
            new Vector2(172f, PillHeight), "RETAKE", PillPink);
        retakeSelfieButton.onClick.AddListener(BeginSelfieRetake);
        RegisterHitTarget(retakeSelfieButton, false, false, default);

        selfieDoneButton = CreateButton("Done", selfieRect, new Vector2(92f, -306f),
            new Vector2(172f, PillHeight), "DONE", DonePink);
        selfieDoneButton.onClick.AddListener(ShowMainMenu);
        RegisterHitTarget(selfieDoneButton, false, false, default);

        selfiePanel.SetActive(false);
        SetSelfieReviewControls(false);
    }

    private void BuildCursor()
    {
        GameObject cursor = new GameObject("HandColorCursor", typeof(RectTransform));
        cursor.transform.SetParent(uiRoot.transform, false);
        cursorRect = cursor.GetComponent<RectTransform>();
        cursorRect.anchorMin = cursorRect.anchorMax = new Vector2(0.5f, 0.5f);
        cursorRect.pivot = new Vector2(0.5f, 0.5f);
        cursorRect.sizeDelta = new Vector2(46f, 46f);

        cursorText = CreateText(
            "CursorDot", cursorRect, Vector2.zero, new Vector2(46f, 46f),
            "●", 34f, TextAlignmentOptions.Center, FontStyles.Normal);
        Stretch(cursorText.rectTransform);
        cursorText.color = LanguagePageBuilder.Pink;
        Outline outline = cursorText.gameObject.AddComponent<Outline>();
        outline.effectColor = LanguagePageBuilder.Green;
        outline.effectDistance = new Vector2(2f, -2f);
        cursor.SetActive(false);
    }

    private void ShowMainMenu()
    {
        if (!paletteAvailable || panel == null)
            return;

        RestoreSelectedPreview();
        CaptureInitialColors();
        SetSculptControllersEnabled(!uiPointerBlockingSculpt);
        mode = ColorMode.MainMenu;
        SetPanelHeight(MainMenuPanelHeight);
        panel.SetActive(true);
        if (selfiePanel != null)
            selfiePanel.SetActive(false);
        if (mainActionGroup != null)
            mainActionGroup.SetActive(true);
        EnsureSelfieOnPalette();
        partGroup.SetActive(false);
        HideExtraCustomizeGroups();
        colorGroup.SetActive(false);
        if (confirmResetGroup != null)
            confirmResetGroup.SetActive(false);
        HideReviewAndShelf();
        if (historyBar != null)
            historyBar.SetActive(true);
        headerText.text = "COLOR PALETTE";
        instructionText.gameObject.SetActive(false);
        SetTitleBurstsVisible(false);
        SetHeaderBackVisible(false);
        ResetDwell();
        SetHoveredTarget(null);
    }

    private void ShowSculptMode()
    {
        mode = ColorMode.Sculpt;
        selectedPart = -1;
        RestoreSelectedPreview();
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
        SetSculptControllersEnabled(false);
        mode = ColorMode.SelectPart;
        SetPanelHeight(PartSelectionPanelHeight);
        mainActionGroup.SetActive(false);
        partGroup.SetActive(true);
        HideExtraCustomizeGroups();
        HideReviewAndShelf();
        colorGroup.SetActive(false);
        if (confirmResetGroup != null)
            confirmResetGroup.SetActive(false);
        if (selfieEntryButton != null)
            selfieEntryButton.gameObject.SetActive(false);
        instructionText.gameObject.SetActive(true);
        headerText.text = "SELECT PART";
        SetHeaderBackLabel("← SCULPT");
        SetHeaderBackVisible(true);
        SetTitleBurstsVisible(true);
        instructionText.text = "Choose the whole teapot or one part.";
        SetHoveredTarget(null);
    }

    private void SelectPart(int partIndex)
    {
        selectedPart = partIndex;
        selectedVariant = -1;
        selectedVariantLabel = "";

        if (PartNeedsVariant(partIndex))
        {
            ShowVariantSelection(partIndex);
            return;
        }

        if (partIndex == 1)
        {
            ShowBodyActions();
            return;
        }

        headerText.text = "SELECT PART";
        instructionText.text = partNames[partIndex] + " selected.";
        SetHoveredTarget(null);
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

        if (!colorBackToDesigns)
            designTintActive = false;

        RestoreSelectedPreview();
        SetSculptControllersEnabled(false);
        mode = ColorMode.SelectColor;
        SetPanelHeight(ColorSelectionPanelHeight);
        mainActionGroup.SetActive(false);
        partGroup.SetActive(false);
        HideExtraCustomizeGroups();
        colorGroup.SetActive(true);
        if (selfieEntryButton != null)
            selfieEntryButton.gameObject.SetActive(false);
        instructionText.gameObject.SetActive(true);
        headerText.text = string.IsNullOrEmpty(selectedVariantLabel)
            ? partNames[selectedPart]
            : partNames[selectedPart] + " · " + selectedVariantLabel;
        if (colorBackToDesigns)
            SetHeaderBackLabel("← DESIGNS");
        else if (selectedPart == 1)
            SetHeaderBackLabel("← BODY");
        else
            SetHeaderBackLabel(PartNeedsVariant(selectedPart) ? "← SHAPE" : "← PARTS");
        SetHeaderBackVisible(true);
        SetTitleBurstsVisible(false);
        instructionText.text = designTintActive
            ? "Point at a color to tint the stamp."
            : "Point and hold until the border completes.";
        SetHoveredTarget(null);
    }

    private void HideReviewAndShelf()
    {
        if (reviewGroup != null)
            reviewGroup.SetActive(false);
        if (shelfGroup != null)
            shelfGroup.SetActive(false);
        if (confirmDeleteGroup != null)
            confirmDeleteGroup.SetActive(false);
    }

    private void ShowReview()
    {
        RestoreSelectedPreview();
        SetSculptControllersEnabled(false);
        mode = ColorMode.Review;
        SetPanelHeight(ReviewPanelHeight);
        panel.SetActive(true);
        mainActionGroup.SetActive(false);
        partGroup.SetActive(false);
        HideExtraCustomizeGroups();
        colorGroup.SetActive(false);
        if (historyBar != null)
            historyBar.SetActive(false);
        if (selfiePanel != null)
            selfiePanel.SetActive(false);
        HideReviewAndShelf();
        if (reviewGroup != null)
            reviewGroup.SetActive(true);
        if (selfieEntryButton != null)
            selfieEntryButton.gameObject.SetActive(false);
        headerText.text = "REVIEW";
        instructionText.gameObject.SetActive(true);
        instructionText.text = "Save this teapot, go back, or put it on the shelf.";
        SetHeaderBackLabel("← PALETTE");
        SetHeaderBackVisible(true);
        SetTitleBurstsVisible(false);
        RefreshSavedStatus();
        SetHoveredTarget(null);
    }

    private void SaveTeapotReview()
    {
        teapotSaved = true;
        RefreshSavedStatus();
        instructionText.text = "Teapot saved.";
    }

    private void RefreshSavedStatus()
    {
        if (savedStatusText == null)
            return;
        savedStatusText.text = teapotSaved ? "SAVED" : "";
    }

    private void FinishToShelf()
    {
        EnsureTeapotShelf();
        if (teapotShelf == null || ResolveTeapotRoot() == null)
        {
            instructionText.text = "The shelf is not ready yet.";
            return;
        }

        if (TeapotCollectionStore.IsFull)
        {
            instructionText.text = "The shelf is full.";
            return;
        }

        if (!CommitShelfCopy())
        {
            instructionText.text = "Could not place this teapot on the shelf.";
            return;
        }

        SetWorkingTeapotVisible(false);
        teapotShelf.PanToShelfView();
        ShowShelf();
    }

    private void ShowShelf()
    {
        SetSculptControllersEnabled(false);
        mode = ColorMode.Shelf;
        SetPanelHeight(ShelfPanelHeight);
        panel.SetActive(true);
        mainActionGroup.SetActive(false);
        partGroup.SetActive(false);
        HideExtraCustomizeGroups();
        colorGroup.SetActive(false);
        if (historyBar != null)
            historyBar.SetActive(false);
        if (selfiePanel != null)
            selfiePanel.SetActive(false);
        if (selfieEntryButton != null)
            selfieEntryButton.gameObject.SetActive(false);
        HideReviewAndShelf();
        if (shelfGroup != null)
            shelfGroup.SetActive(true);
        headerText.text = "SHELF";
        instructionText.gameObject.SetActive(true);
        instructionText.text = "Take it back, delete it, or go back when you are done.";
        SetHeaderBackLabel("← REVIEW");
        SetHeaderBackVisible(true);
        SetTitleBurstsVisible(false);
        ResetDwell();
        SetHoveredTarget(null);
    }

    private void TakeBackFromShelf()
    {
        TeapotCollectionStore.RemoveLast();
        if (teapotShelf != null)
        {
            teapotShelf.RemoveLastDisplay();
            teapotShelf.RestoreSculptView();
        }

        SetWorkingTeapotVisible(true);
        ShowMainMenu();
    }

    private void LeaveShelfDone()
    {
        SetWorkingTeapotVisible(true);
        ResetWorkingTeapot();
        if (teapotShelf != null)
            teapotShelf.RestoreSculptView();
        if (paletteAvailable)
            ShowMainMenu();
        else
        {
            SetSculptControllersEnabled(true);
            ShowSculptMode();
        }
    }

    private void ShowConfirmDelete()
    {
        mode = ColorMode.ConfirmDelete;
        SetPanelHeight(ShelfPanelHeight);
        if (shelfGroup != null)
            shelfGroup.SetActive(false);
        if (confirmDeleteGroup != null)
            confirmDeleteGroup.SetActive(true);
        headerText.text = "DELETE";
        instructionText.gameObject.SetActive(true);
        instructionText.text = "This removes the teapot from the shelf.";
        SetHeaderBackVisible(true);
        ResetDwell();
        SetHoveredTarget(null);
    }

    private void ConfirmDeleteTeapot()
    {
        TeapotCollectionStore.RemoveLast();
        if (teapotShelf != null)
        {
            teapotShelf.RemoveLastDisplay();
            teapotShelf.RestoreSculptView();
        }

        SetWorkingTeapotVisible(true);
        if (paletteAvailable)
            ShowMainMenu();
        else
        {
            SetSculptControllersEnabled(true);
            ShowSculptMode();
        }
    }

    private void ShowSelfieCapture()
    {
        RestoreSelectedPreview();
        SetSculptControllersEnabled(false);
        mode = ColorMode.Selfie;
        panel.SetActive(false);
        if (selfieEntryButton != null)
            selfieEntryButton.gameObject.SetActive(false);
        selfiePanel.SetActive(true);
        selfieHeaderText.text = "SELFIE";
        selfieInstructionText.text = "Frame your photo, then point and hold TAKE SELFIE.";
        SetSelfieReviewControls(false);
        ShowLiveSelfiePreview();
        ResetDwell();
        SetHoveredTarget(null);
    }

    private void SetSelfieReviewControls(bool reviewing)
    {
        if (takeSelfieButton != null)
            takeSelfieButton.gameObject.SetActive(!reviewing);
        if (selfieBackButton != null)
            selfieBackButton.gameObject.SetActive(!reviewing);
        if (attachSelfieButton != null)
            attachSelfieButton.gameObject.SetActive(reviewing);
        if (retakeSelfieButton != null)
            retakeSelfieButton.gameObject.SetActive(reviewing);
        if (selfieDoneButton != null)
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
            return;
        }

        selfiePreviewImage.texture = imageSource.GetCurrentTexture();
    }

    private void CaptureSelfie()
    {
        var imageSource = ImageSourceProvider.ImageSource;
        if (imageSource == null || !imageSource.isPrepared)
        {
            selfieInstructionText.text = "Camera is not ready yet.";
            return;
        }

        Texture source = imageSource.GetCurrentTexture();
        if (source == null)
            return;

        RenderTexture temporary = RenderTexture.GetTemporary(source.width, source.height, 0);
        Graphics.Blit(source, temporary);
        RenderTexture previous = RenderTexture.active;
        RenderTexture.active = temporary;
        Texture2D readable = new Texture2D(source.width, source.height, TextureFormat.RGBA32, false);
        readable.ReadPixels(new Rect(0f, 0f, source.width, source.height), 0, 0);
        readable.Apply();
        RenderTexture.active = previous;
        RenderTexture.ReleaseTemporary(temporary);

        DestroyRuntime(capturedSelfieTexture);
        capturedSelfieTexture = readable;
        selfiePreviewImage.texture = capturedSelfieTexture;
        SetSelfieReviewControls(true);
        selfieInstructionText.text = "Attach the photo to the teapot, retake it, or press DONE.";
        ResetDwell();
    }

    private void BeginSelfieRetake()
    {
        DestroyRuntime(capturedSelfieTexture);
        capturedSelfieTexture = null;
        SetSelfieReviewControls(false);
        ShowLiveSelfiePreview();
        selfieInstructionText.text = "Frame your photo, then point and hold TAKE SELFIE.";
        ResetDwell();
    }

    private void AttachSelfie()
    {
        if (capturedSelfieTexture == null || teapotRenderer == null)
        {
            selfieInstructionText.text = "Take a selfie before attaching it.";
            return;
        }

        DestroyRuntime(appliedSelfieTexture);
        appliedSelfieTexture = CopyTexturePixels(capturedSelfieTexture, "Morphic Applied Teapot Selfie");
        FreezeSelfieAxes();

        HideLeftoverSelfieCards();
        CaptureInitialColors();
        EnsureSelfieSurfaceOverlay();
        ConfigureSelfieSurfaceMaterial(appliedSelfieTexture);

        selfieInstructionText.text = "Selfie attached to the teapot body.";
        PushHistory();
    }

    private static Texture2D CopyTexturePixels(Texture2D source, string textureName)
    {
        if (source == null)
            return null;

        Texture2D copy = new Texture2D(source.width, source.height, TextureFormat.RGBA32, true)
        {
            name = textureName,
            filterMode = FilterMode.Trilinear,
            anisoLevel = 4,
            wrapMode = TextureWrapMode.Clamp
        };
        copy.SetPixels32(source.GetPixels32());
        copy.Apply(true, false);
        return copy;
    }

    private void FreezeSelfieAxes()
    {
        if (teapotRenderer == null)
            return;

        // Mesh space is Z-up: lid along +Z, belly in XY. Keep the photo on
        // the camera-facing side of that belly.
        selfieUpLocal = Vector3.forward;

        Transform rendererTransform = teapotRenderer.transform;
        Camera view = ResolveViewCamera();
        Vector3 worldTowardCamera = view != null
            ? view.transform.position - rendererTransform.position
            : Vector3.back;
        Vector3 localTowardCamera = rendererTransform.InverseTransformDirection(worldTowardCamera);
        selfieFrontLocal = new Vector3(localTowardCamera.x, localTowardCamera.y, 0f);
        if (selfieFrontLocal.sqrMagnitude < 0.0001f)
            selfieFrontLocal = Vector3.up;
        selfieFrontLocal.Normalize();
        selfieFrontLocal = SteerSelfieOffAppendages(selfieFrontLocal);
    }

    private Vector3 SteerSelfieOffAppendages(Vector3 cameraFront)
    {
        Vector2 front = new Vector2(cameraFront.x, cameraFront.y);
        if (front.sqrMagnitude < 0.0001f)
            front = Vector2.up;
        front.Normalize();

        Vector2 shaft = AveragePartDirectionXY("SHAFT");
        Vector2 handle = AveragePartDirectionXY("HANDLE");
        Vector2 occupied = Vector2.zero;
        if (shaft.sqrMagnitude > 0.0001f)
            occupied += shaft.normalized;
        if (handle.sqrMagnitude > 0.0001f)
            occupied += handle.normalized;
        if (occupied.sqrMagnitude < 0.0001f)
        {
            // No part markers. Nudge off the camera center so a spout/handle
            // sitting on the facing side is less likely to be covered.
            return Quaternion.AngleAxis(36f, Vector3.forward) * new Vector3(front.x, front.y, 0f);
        }

        occupied.Normalize();
        Vector2 clearA = new Vector2(-occupied.y, occupied.x);
        Vector2 clearB = -clearA;
        Vector2 chosen = Vector2.Dot(clearA, front) >= Vector2.Dot(clearB, front) ? clearA : clearB;
        return new Vector3(chosen.x, chosen.y, 0f);
    }

    private Vector2 AveragePartDirectionXY(string token)
    {
        Mesh mesh = teapotRenderer != null ? teapotRenderer.sharedMesh : null;
        if (mesh == null || !mesh.isReadable)
            return Vector2.zero;

        bool[] marks = MarkMovingVertices(mesh, token);
        Vector3[] vertices = mesh.vertices;
        if (marks == null || vertices == null)
            return Vector2.zero;

        Bounds body = GetTeapotBodyLocalBounds();
        Vector2 center = new Vector2(body.center.x, body.center.y);
        Vector2 sum = Vector2.zero;
        int count = 0;
        int limit = Mathf.Min(marks.Length, vertices.Length);
        for (int i = 0; i < limit; i++)
        {
            if (!marks[i])
                continue;
            sum += new Vector2(vertices[i].x, vertices[i].y) - center;
            count++;
        }

        return count > 0 ? sum / count : Vector2.zero;
    }

    private void HideLeftoverSelfieCards()
    {
        if (selfieDecalRoot != null)
        {
            Destroy(selfieDecalRoot.gameObject);
            selfieDecalRoot = null;
        }

        for (int i = 0; i < selfiePlates.Length; i++)
            selfiePlates[i] = null;

        DestroySelfieOverlay();
    }

    private void DestroySelfieOverlay()
    {
        if (selfieSurfaceRenderer != null)
            selfieSurfaceRenderer.enabled = false;
        if (selfieSurfaceRoot != null)
        {
            Destroy(selfieSurfaceRoot.gameObject);
            selfieSurfaceRoot = null;
            selfieSurfaceRenderer = null;
        }
    }

    private bool EnsureSelfieSurfaceOverlay()
    {
        if (teapotRenderer == null || teapotRenderer.sharedMesh == null)
            return false;

        Shader shader = Shader.Find("Morphic/Selfie Surface Overlay");
        if (shader == null)
            return false;

        if (selfieSurfaceRoot == null)
        {
            GameObject overlayObject = new GameObject("TeapotSelfieSurfaceOverlay");
            selfieSurfaceRoot = overlayObject.transform;
            selfieSurfaceRenderer = overlayObject.AddComponent<SkinnedMeshRenderer>();
        }

        if (selfieSurfaceMaterial == null)
        {
            selfieSurfaceMaterial = new Material(shader)
            {
                name = "Morphic Teapot Selfie Surface Material",
                renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent + 20
            };
        }

        if (selfieHiddenMaterial == null)
        {
            selfieHiddenMaterial = new Material(shader)
            {
                name = "Morphic Hidden Teapot Selfie Submesh Material",
                renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent + 20
            };
            selfieHiddenMaterial.SetFloat("_Opacity", 0f);
        }

        Transform rendererTransform = teapotRenderer.transform;
        selfieSurfaceRoot.SetParent(rendererTransform.parent, false);
        selfieSurfaceRoot.localPosition = rendererTransform.localPosition;
        selfieSurfaceRoot.localRotation = rendererTransform.localRotation;
        selfieSurfaceRoot.localScale = rendererTransform.localScale * 1.02f;
        selfieSurfaceRoot.gameObject.layer = teapotRenderer.gameObject.layer;
        selfieSurfaceRoot.gameObject.SetActive(true);

        selfieSurfaceRenderer.sharedMesh = teapotRenderer.sharedMesh;
        selfieSurfaceRenderer.rootBone = teapotRenderer.rootBone;
        selfieSurfaceRenderer.bones = teapotRenderer.bones;
        selfieSurfaceRenderer.localBounds = teapotRenderer.localBounds;
        selfieSurfaceRenderer.quality = teapotRenderer.quality;
        selfieSurfaceRenderer.updateWhenOffscreen = teapotRenderer.updateWhenOffscreen;
        selfieSurfaceRenderer.skinnedMotionVectors = false;
        selfieSurfaceRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        selfieSurfaceRenderer.receiveShadows = false;
        selfieSurfaceRenderer.forceRenderingOff = false;
        selfieSurfaceRenderer.allowOcclusionWhenDynamic = false;
        selfieSurfaceRenderer.renderingLayerMask = teapotRenderer.renderingLayerMask;
        selfieSurfaceRenderer.sortingOrder = 20;

        AssignSelfieOverlayMaterials(selfieSurfaceRenderer, selfieHiddenMaterial, selfieSurfaceMaterial);
        selfieSurfaceRenderer.enabled = true;
        return true;
    }

    private void AssignSelfieOverlayMaterials(
        SkinnedMeshRenderer overlay,
        Material hidden,
        Material visible)
    {
        if (overlay == null || overlay.sharedMesh == null || hidden == null || visible == null)
            return;

        int subMeshCount = Mathf.Max(1, overlay.sharedMesh.subMeshCount);
        Material[] overlayMaterials = new Material[subMeshCount];
        for (int i = 0; i < overlayMaterials.Length; i++)
            overlayMaterials[i] = hidden;

        int[] slots = BodyAndBandSlots(bodyMaterialIndex);
        if (slots == null || slots.Length == 0)
            overlayMaterials[Mathf.Clamp(bodyMaterialIndex, 0, subMeshCount - 1)] = visible;
        else
        {
            for (int i = 0; i < slots.Length; i++)
            {
                int slot = slots[i];
                if (slot >= 0 && slot < overlayMaterials.Length)
                    overlayMaterials[slot] = visible;
            }
        }

        overlay.sharedMaterials = overlayMaterials;
    }

    private void ConfigureSelfieSurfaceMaterial(Texture texture)
    {
        ApplyTeapotSelfieMaterial(selfieSurfaceMaterial, teapotRenderer, texture, selfieUpLocal, selfieFrontLocal);
    }

    private void ApplyTeapotSelfieMaterial(
        Material material,
        SkinnedMeshRenderer skin,
        Texture texture,
        Vector3 upLocal,
        Vector3 frontLocal)
    {
        if (material == null || skin == null)
            return;

        Bounds localBounds = GetTeapotBodyLocalBounds();
        Bounds worldBounds = GetTeapotBodyWorldBounds();
        material.SetTexture("_SelfieTex", texture);
        material.SetFloat("_Opacity", 1f);
        material.SetFloat("_PatchHalfAngle", 0.56f);
        material.SetFloat("_VerticalHalfFraction", 0.78f);
        material.SetFloat("_BrushEdgeRoughness", 0f);
        material.SetFloat("_BrushCornerRoundness", 0f);
        material.SetFloat("_SurfacePush", 0.02f);
        material.SetVector("_ObjectCenter", localBounds.center);
        material.SetVector("_ObjectExtents", localBounds.extents);
        material.SetVector("_WorldCenter", worldBounds.center);
        material.SetVector("_WorldExtents", worldBounds.extents);
        material.SetFloat("_StampMode", 3f);
        material.SetVector("_UpDirection", upLocal.sqrMagnitude > 0.0001f ? upLocal : Vector3.up);
        material.SetVector("_FrontDirection", frontLocal.sqrMagnitude > 0.0001f ? frontLocal : Vector3.forward);
        float photoAspect = texture != null && texture.height > 0
            ? texture.width / (float)texture.height
            : 1f;
        material.SetFloat("_PhotoAspect", photoAspect);
        material.SetFloat("_UseYUp", 1f);
    }

    private void SyncSelfieSurfaceRenderer()
    {
        if (selfieSurfaceRenderer == null || teapotRenderer == null)
            return;

        Transform rendererTransform = teapotRenderer.transform;
        if (selfieSurfaceRoot.parent != rendererTransform.parent)
            selfieSurfaceRoot.SetParent(rendererTransform.parent, false);
        selfieSurfaceRoot.localPosition = rendererTransform.localPosition;
        selfieSurfaceRoot.localRotation = rendererTransform.localRotation;
        selfieSurfaceRoot.localScale = rendererTransform.localScale * 1.02f;

        selfieSurfaceRenderer.rootBone = teapotRenderer.rootBone;
        selfieSurfaceRenderer.bones = teapotRenderer.bones;
        selfieSurfaceRenderer.localBounds = teapotRenderer.localBounds;

        Mesh mesh = teapotRenderer.sharedMesh;
        if (mesh == null)
            return;
        if (selfieSurfaceRenderer.sharedMesh != mesh)
            selfieSurfaceRenderer.sharedMesh = mesh;

        for (int i = 0; i < mesh.blendShapeCount; i++)
            selfieSurfaceRenderer.SetBlendShapeWeight(i, teapotRenderer.GetBlendShapeWeight(i));
    }

    private void EnsureSelfiePlates()
    {
        if (selfieQuadMesh == null)
            selfieQuadMesh = CreateUprightSelfieQuad();
        if (selfieDecalMaterial == null)
            selfieDecalMaterial = CreateSelfiePlateMaterial();

        if (selfieDecalRoot != null)
        {
            Destroy(selfieDecalRoot.gameObject);
            selfieDecalRoot = null;
        }

        for (int i = 0; i < selfiePlates.Length; i++)
            selfiePlates[i] = null;

        GameObject root = new GameObject("TeapotSelfiePlates");
        selfieDecalRoot = root.transform;
        selfieDecalRoot.SetParent(null, true);
        selfieDecalRoot.gameObject.SetActive(true);

        GameObject plate = new GameObject("TeapotSelfiePlate");
        plate.transform.SetParent(selfieDecalRoot, false);
        MeshFilter filter = plate.AddComponent<MeshFilter>();
        MeshRenderer renderer = plate.AddComponent<MeshRenderer>();
        filter.sharedMesh = selfieQuadMesh;
        renderer.sharedMaterial = selfieDecalMaterial;
        renderer.enabled = true;
        renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        renderer.receiveShadows = false;
        renderer.allowOcclusionWhenDynamic = false;
        renderer.sortingOrder = 40;
        selfiePlates[0] = plate.transform;
    }

    private static Mesh CreateUprightSelfieQuad()
    {
        Mesh mesh = new Mesh { name = "Morphic Teapot Selfie Plate" };
        mesh.vertices = new[]
        {
            new Vector3(-0.5f, -0.5f, 0f),
            new Vector3(0.5f, -0.5f, 0f),
            new Vector3(-0.5f, 0.5f, 0f),
            new Vector3(0.5f, 0.5f, 0f)
        };
        mesh.uv = new[]
        {
            new Vector2(0f, 0f),
            new Vector2(1f, 0f),
            new Vector2(0f, 1f),
            new Vector2(1f, 1f)
        };
        mesh.triangles = new[] { 0, 2, 1, 2, 3, 1 };
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        return mesh;
    }

    private static Material CreateSelfiePlateMaterial()
    {
        Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
        if (shader == null)
            shader = Shader.Find("Unlit/Texture");
        if (shader == null)
            shader = Shader.Find("Sprites/Default");

        Material material = new Material(shader)
        {
            name = "Morphic Teapot Selfie Plate"
        };
        if (material.HasProperty("_BaseColor"))
            material.SetColor("_BaseColor", Color.white);
        if (material.HasProperty("_Color"))
            material.SetColor("_Color", Color.white);
        if (material.HasProperty("_Cull"))
            material.SetFloat("_Cull", 0f);
        return material;
    }

    private void ConfigureSelfiePlateMaterial(Texture texture)
    {
        if (selfieDecalMaterial == null)
            return;

        selfieDecalMaterial.mainTexture = texture;
        if (selfieDecalMaterial.HasProperty("_BaseMap"))
            selfieDecalMaterial.SetTexture("_BaseMap", texture);
        if (selfieDecalMaterial.HasProperty("_BaseColor"))
            selfieDecalMaterial.SetColor("_BaseColor", Color.white);
        if (selfieDecalMaterial.HasProperty("_Color"))
            selfieDecalMaterial.SetColor("_Color", Color.white);
    }

    private void RefreshSelfiePlates()
    {
        if (teapotRenderer == null || selfieDecalRoot == null || appliedSelfieTexture == null)
            return;

        Transform plate = selfiePlates[0];
        if (plate == null)
            return;

        Bounds worldBounds = GetTeapotBodyWorldBounds();
        Vector3 center = worldBounds.center;
        float radius = Mathf.Max(worldBounds.extents.x, worldBounds.extents.z);
        float face = Mathf.Min(worldBounds.size.y, radius * 2f) * 0.7f;
        if (face < 0.001f)
            return;

        Camera viewCamera = ResolveViewCamera();
        Vector3 front = viewCamera != null
            ? viewCamera.transform.position - center
            : Vector3.back;
        front = Vector3.ProjectOnPlane(front, Vector3.up);
        if (front.sqrMagnitude < 0.0001f)
            front = viewCamera != null
                ? Vector3.ProjectOnPlane(-viewCamera.transform.forward, Vector3.up)
                : Vector3.back;
        if (front.sqrMagnitude < 0.0001f)
            front = Vector3.back;
        front.Normalize();

        float aspect = appliedSelfieTexture.width / (float)Mathf.Max(1, appliedSelfieTexture.height);
        float height = face;
        float width = height * aspect;
        if (width > radius * 1.5f)
        {
            width = radius * 1.5f;
            height = width / Mathf.Max(0.01f, aspect);
        }

        plate.SetPositionAndRotation(
            center + front * (radius * 1.01f),
            Quaternion.LookRotation(front, Vector3.up));
        plate.localScale = new Vector3(width, height, 1f);
        plate.gameObject.layer = teapotRenderer.gameObject.layer;
        plate.gameObject.SetActive(true);
        MeshRenderer renderer = plate.GetComponent<MeshRenderer>();
        if (renderer != null)
        {
            renderer.enabled = true;
            renderer.sharedMaterial = selfieDecalMaterial;
        }
    }

    private Camera ResolveViewCamera()
    {
        GameObject named = GameObject.Find("MainMenuViewCamera");
        if (named != null)
        {
            Camera namedCamera = named.GetComponent<Camera>();
            if (namedCamera != null)
                return namedCamera;
        }

        if (Camera.main != null)
            return Camera.main;

        Camera[] cameras = FindObjectsByType<Camera>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        return cameras != null && cameras.Length > 0 ? cameras[0] : null;
    }

    private Bounds GetTeapotBodyWorldBounds()
    {
        if (teapotRenderer == null || teapotRenderer.sharedMesh == null)
            return default;

        if (selfieBoundsMesh == null)
        {
            selfieBoundsMesh = new Mesh { name = "Morphic Current Teapot Body Bounds" };
            selfieBoundsMesh.MarkDynamic();
        }

        teapotRenderer.BakeMesh(selfieBoundsMesh);
        Vector3[] vertices = selfieBoundsMesh.vertices;
        if (vertices == null || vertices.Length == 0)
            return teapotRenderer.bounds;

        bool[] bodyMask = EnsureSelfieBodyVertexMask();
        Transform rendererTransform = teapotRenderer.transform;
        Bounds world = default;
        bool started = false;
        int count = vertices.Length;
        for (int i = 0; i < count; i++)
        {
            if (bodyMask != null && i < bodyMask.Length && !bodyMask[i])
                continue;
            Vector3 worldVertex = rendererTransform.TransformPoint(vertices[i]);
            if (!started)
            {
                world = new Bounds(worldVertex, Vector3.zero);
                started = true;
            }
            else
            {
                world.Encapsulate(worldVertex);
            }
        }

        if (!started)
        {
            for (int i = 0; i < count; i++)
            {
                Vector3 worldVertex = rendererTransform.TransformPoint(vertices[i]);
                if (!started)
                {
                    world = new Bounds(worldVertex, Vector3.zero);
                    started = true;
                }
                else
                {
                    world.Encapsulate(worldVertex);
                }
            }
        }

        return started ? world : teapotRenderer.bounds;
    }

    private Bounds GetTeapotBodyLocalBounds()
    {
        if (teapotRenderer == null || teapotRenderer.sharedMesh == null)
            return default;

        if (selfieBoundsMesh == null)
        {
            selfieBoundsMesh = new Mesh { name = "Morphic Current Teapot Body Bounds" };
            selfieBoundsMesh.MarkDynamic();
        }

        teapotRenderer.BakeMesh(selfieBoundsMesh);
        Vector3[] vertices = selfieBoundsMesh.vertices;
        if (vertices == null || vertices.Length == 0)
            return teapotRenderer.localBounds;

        bool[] bodyMask = EnsureSelfieBodyVertexMask();
        Bounds body = default;
        bool started = false;
        int count = Mathf.Min(vertices.Length, bodyMask != null ? bodyMask.Length : 0);
        for (int i = 0; i < count; i++)
        {
            if (bodyMask != null && !bodyMask[i])
                continue;
            if (!started)
            {
                body = new Bounds(vertices[i], Vector3.zero);
                started = true;
            }
            else
            {
                body.Encapsulate(vertices[i]);
            }
        }

        if (!started)
        {
            selfieBoundsMesh.RecalculateBounds();
            return selfieBoundsMesh.bounds.extents.sqrMagnitude > 0.0000001f
                ? selfieBoundsMesh.bounds
                : teapotRenderer.localBounds;
        }

        return body;
    }

    private bool[] EnsureSelfieBodyVertexMask()
    {
        Mesh source = teapotRenderer != null ? teapotRenderer.sharedMesh : null;
        if (source == null)
            return selfieBodyVertexMask;

        if (selfieBodyVertexMask != null && selfieBodyVertexMask.Length == source.vertexCount)
            return selfieBodyVertexMask;

        bool[] lid = MarkMovingVertices(source, "LID");
        bool[] spout = MarkMovingVertices(source, "SHAFT");
        bool[] handle = MarkMovingVertices(source, "HANDLE");
        int bodySlot = Mathf.Clamp(bodyMaterialIndex, 0, Mathf.Max(0, source.subMeshCount - 1));
        int[] triangles = source.GetTriangles(bodySlot);
        selfieBodyVertexMask = new bool[source.vertexCount];
        for (int i = 0; i < triangles.Length; i++)
        {
            int vertex = triangles[i];
            if (vertex < 0 || vertex >= selfieBodyVertexMask.Length)
                continue;
            if (IsMarked(lid, vertex) || IsMarked(spout, vertex) || IsMarked(handle, vertex))
                continue;
            selfieBodyVertexMask[vertex] = true;
        }

        return selfieBodyVertexMask;
    }

    private void ShowConfirmReset()
    {
        modeBeforeConfirm = mode;
        confirmResetGroup.SetActive(true);
        historyBar.SetActive(false);
        mainActionGroup.SetActive(false);
        partGroup.SetActive(false);
        HideExtraCustomizeGroups();
        colorGroup.SetActive(false);
        if (reviewGroup != null)
            reviewGroup.SetActive(false);
        headerText.text = "RESET";
        instructionText.gameObject.SetActive(true);
        instructionText.text = "This clears sculpt, color, design, and selfie.";
        SetHeaderBackVisible(true);
        SetHoveredTarget(null);
    }

    private void HideConfirmReset()
    {
        confirmResetGroup.SetActive(false);
        historyBar.SetActive(true);
        if (modeBeforeConfirm == ColorMode.SelectPart)
            ShowPartSelection();
        else if (modeBeforeConfirm == ColorMode.SelectVariant)
            ShowVariantSelection(selectedPart);
        else if (modeBeforeConfirm == ColorMode.SelectBodyActions)
            ShowBodyActions();
        else if (modeBeforeConfirm == ColorMode.SelectDesign)
            ShowDesignSelection();
        else if (modeBeforeConfirm == ColorMode.SelectColor)
            ContinueToColorSelection();
        else
            ShowMainMenu();
    }

    private void ConfirmReset()
    {
        if (teapotGestures != null)
            teapotGestures.RestoreRestState();
        if (history.Count > 0)
            ApplySnapshot(history[0]);
        HideLeftoverSelfieCards();
        if (selfieSurfaceRenderer != null)
            selfieSurfaceRenderer.enabled = false;

        DestroyRuntime(appliedSelfieTexture);
        appliedSelfieTexture = null;
        appliedDesignTexture = null;
        designTintActive = false;
        committedDesignTint = Color.white;
        if (designSurfaceRenderer != null)
            designSurfaceRenderer.enabled = false;
        CaptureBaseline();
        HideConfirmReset();
        instructionText.text = "Teapot reset.";
    }

    private void UndoHistory()
    {
        RestoreSelectedPreview();
        if (history.Count < 2)
        {
            instructionText.gameObject.SetActive(true);
            instructionText.text = "Nothing to undo.";
            return;
        }

        history.RemoveAt(history.Count - 1);
        ApplySnapshot(history[history.Count - 1]);
        instructionText.gameObject.SetActive(true);
        instructionText.text = "Last change undone.";
    }

    private void NavigateBack()
    {
        if (confirmResetGroup != null && confirmResetGroup.activeSelf)
        {
            HideConfirmReset();
            return;
        }

        if (mode == ColorMode.SelectColor)
        {
            if (colorBackToDesigns)
                ShowDesignSelection();
            else if (selectedPart == 1)
                ShowBodyActions();
            else if (PartNeedsVariant(selectedPart))
                ShowVariantSelection(selectedPart);
            else
                ShowPartSelection();
        }
        else if (mode == ColorMode.SelectDesign)
            ShowBodyActions();
        else if (mode == ColorMode.SelectBodyActions || mode == ColorMode.SelectVariant)
            ShowPartSelection();
        else if (mode == ColorMode.ConfirmDelete)
            ShowShelf();
        else if (mode == ColorMode.Shelf)
            LeaveShelfDone();
        else if (mode == ColorMode.SelectPart || mode == ColorMode.Review || mode == ColorMode.Selfie)
            ShowMainMenu();
    }

    private void EnsureTeapotIndexHelper()
    {
        if (teapotGestures == null)
            return;
        if (teapotIndexGestures == null)
            teapotIndexGestures = teapotGestures.GetComponent<SculptingEnhancementTeapotIndexGestureController>();
        if (teapotIndexGestures == null)
            teapotIndexGestures = teapotGestures.gameObject.AddComponent<SculptingEnhancementTeapotIndexGestureController>();
        teapotIndexGestures.BindFrom(handRunner, teapotRenderer);
        teapotGestures.ownWidthHeight = teapotIndexGestures == null || !teapotIndexGestures.enabled;
    }

    private void EnsureTeapotShelf()
    {
        if (teapotShelf != null)
            return;

        teapotShelf = FindFirstObjectByType<LanternShelfController>(FindObjectsInactive.Include);
        if (teapotShelf != null)
        {
            teapotShelf.BindPieces();
            return;
        }

        GameObject root = new GameObject(LanternShelfController.ShelfRootName);
        teapotShelf = root.AddComponent<LanternShelfController>();
        teapotShelf.BindPieces();
    }

    private Transform ResolveTeapotRoot()
    {
        if (teapotRenderer == null)
            return null;

        Transform current = teapotRenderer.transform;
        while (current != null)
        {
            if (current.name == "Teapot_Updated" || current.name == "Teapot_test")
                return current;
            current = current.parent;
        }

        return teapotRenderer.transform;
    }

    private bool CommitShelfCopy()
    {
        Transform source = ResolveTeapotRoot();
        if (source == null || teapotShelf == null)
            return false;

        TeapotCollectionStore.Record record = CaptureCollectionRecord();
        if (!TeapotCollectionStore.Add(record))
            return false;

        int slotIndex = teapotShelf.CollectionCount;
        GameObject clone = Instantiate(source.gameObject);
        clone.name = "TeapotCollection_" + record.Id;
        clone.transform.SetPositionAndRotation(source.position, source.rotation);
        UniqueCloneMaterials(clone);
        StripGameplay(clone);
        ApplyRecordToClone(clone, record);
        CopyDesignOntoClone(clone);
        SeatTeapotAtOwnSlot(clone.transform, slotIndex);
        CopySelfieSurfaceOntoClone(clone);
        return true;
    }

    private const string TeapotSlotName = "TeapotShelfSlot";
    private const int TeapotSlotCount = 9;
    private Transform[] teapotSlots;

    private void SeatTeapotAtOwnSlot(Transform teapot, int slotIndex)
    {
        if (teapot == null)
            return;

        EnsureTeapotShelfSlots();
        Transform seat = GetTeapotSlot(slotIndex);
        Transform collection = teapotShelf != null ? teapotShelf.CollectionRoot : null;
        Quaternion upright = teapot.rotation;
        if (collection != null)
            teapot.SetParent(collection, true);
        teapot.rotation = upright;

        Bounds body = GetCloneBodyWorldBounds(teapot);
        Vector3 currentBottom = new Vector3(body.center.x, body.min.y, body.center.z);
        Vector3 slotPos = seat != null ? seat.position : teapot.position;
        teapot.position += slotPos - currentBottom;
    }

    private Transform GetTeapotSlot(int slotIndex)
    {
        EnsureTeapotShelfSlots();
        if (teapotSlots == null || teapotSlots.Length == 0)
            return null;
        return teapotSlots[Mathf.Clamp(slotIndex, 0, teapotSlots.Length - 1)];
    }

    private void EnsureTeapotShelfSlots()
    {
        List<Transform> found = FindNamedSlots(TeapotSlotName);
        found.Sort((a, b) => SlotSortKey(a.name, TeapotSlotName).CompareTo(SlotSortKey(b.name, TeapotSlotName)));

        Transform parent = teapotShelf != null ? teapotShelf.transform : null;
        List<Transform> lanternSlots = FindNamedSlots(LanternShelfController.SlotName);
        lanternSlots.Sort((a, b) =>
            SlotSortKey(a.name, LanternShelfController.SlotName)
                .CompareTo(SlotSortKey(b.name, LanternShelfController.SlotName)));

        if (parent == null && lanternSlots.Count > 0)
            parent = lanternSlots[0].parent;
        if (parent == null)
            parent = transform;

        for (int i = found.Count; i < TeapotSlotCount; i++)
        {
            GameObject slotObject = new GameObject(i == 0 ? TeapotSlotName : TeapotSlotName + "_" + (i + 1));
            Transform slot = slotObject.transform;
            slot.SetParent(parent, true);
            if (i < lanternSlots.Count && lanternSlots[i] != null)
            {
                slot.position = lanternSlots[i].position;
                slot.rotation = lanternSlots[i].rotation;
            }
            else
            {
                slot.position = parent.position + Vector3.right * (0.45f * i);
                slot.rotation = Quaternion.identity;
            }

            found.Add(slot);
        }

        teapotSlots = found.ToArray();
    }

    private static int SlotSortKey(string objectName, string prefix)
    {
        if (objectName == prefix)
            return 1;
        if (objectName.StartsWith(prefix + "_", StringComparison.Ordinal) &&
            int.TryParse(objectName.Substring(prefix.Length + 1), out int number))
            return number;
        return 99;
    }

    private static List<Transform> FindNamedSlots(string prefix)
    {
        List<Transform> found = new List<Transform>();
        for (int sceneIndex = 0; sceneIndex < UnityEngine.SceneManagement.SceneManager.sceneCount; sceneIndex++)
        {
            var scene = UnityEngine.SceneManagement.SceneManager.GetSceneAt(sceneIndex);
            if (!scene.isLoaded)
                continue;
            GameObject[] roots = scene.GetRootGameObjects();
            for (int i = 0; i < roots.Length; i++)
            {
                Transform[] transforms = roots[i].GetComponentsInChildren<Transform>(true);
                for (int t = 0; t < transforms.Length; t++)
                {
                    if (transforms[t] != null &&
                        transforms[t].name.StartsWith(prefix, StringComparison.Ordinal) &&
                        !found.Contains(transforms[t]))
                        found.Add(transforms[t]);
                }
            }
        }

        return found;
    }

    private static Bounds GetCloneBodyWorldBounds(Transform root)
    {
        SkinnedMeshRenderer skin = root.GetComponentInChildren<SkinnedMeshRenderer>(true);
        if (skin == null || skin.sharedMesh == null)
        {
            Renderer renderer = root.GetComponentInChildren<Renderer>(true);
            return renderer != null ? renderer.bounds : new Bounds(root.position, Vector3.one * 0.2f);
        }

        Mesh baked = new Mesh();
        skin.BakeMesh(baked);
        Vector3[] vertices = baked.vertices;
        UnityEngine.Object.Destroy(baked);
        if (vertices == null || vertices.Length == 0)
            return skin.bounds;

        Bounds world = new Bounds(skin.transform.TransformPoint(vertices[0]), Vector3.zero);
        for (int i = 1; i < vertices.Length; i++)
            world.Encapsulate(skin.transform.TransformPoint(vertices[i]));
        return world;
    }

    private void SetWorkingTeapotVisible(bool visible)
    {
        Transform root = ResolveTeapotRoot();
        if (root != null)
        {
            Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);
            for (int i = 0; i < renderers.Length; i++)
            {
                if (renderers[i] == null)
                    continue;
                renderers[i].enabled = visible;
            }
        }

        if (designSurfaceRoot != null)
        {
            designSurfaceRoot.gameObject.SetActive(visible && appliedDesignTexture != null);
            if (designSurfaceRenderer != null)
                designSurfaceRenderer.enabled = visible && appliedDesignTexture != null;
        }

        if (selfieSurfaceRoot != null)
        {
            bool showSelfie = visible && appliedSelfieTexture != null;
            selfieSurfaceRoot.gameObject.SetActive(showSelfie);
            if (selfieSurfaceRenderer != null)
                selfieSurfaceRenderer.enabled = showSelfie;
        }
    }

    private void ResetWorkingTeapot()
    {
        RestoreSelectedPreview();
        ClearWorkingSelfieAndDesign();
        if (teapotGestures != null)
            teapotGestures.RestoreRestState();
        if (history.Count > 0)
            ApplySnapshot(history[0]);
        teapotSaved = false;
        RefreshSavedStatus();
        CaptureBaseline();
    }

    private void ClearWorkingSelfieAndDesign()
    {
        HideLeftoverSelfieCards();
        if (selfieSurfaceRenderer != null)
            selfieSurfaceRenderer.enabled = false;
        DestroyRuntime(appliedSelfieTexture);
        appliedSelfieTexture = null;
        capturedSelfieTexture = null;
        appliedDesignTexture = null;
        designTintActive = false;
        committedDesignTint = Color.white;
        if (designSurfaceRenderer != null)
            designSurfaceRenderer.enabled = false;
        if (designSurfaceRoot != null)
            designSurfaceRoot.gameObject.SetActive(false);
    }

    private TeapotCollectionStore.Record CaptureCollectionRecord()
    {
        TeapotSnapshot snapshot = CaptureSnapshot();
        Transform root = ResolveTeapotRoot();
        Quaternion rotation = root != null ? root.rotation : Quaternion.identity;
        return new TeapotCollectionStore.Record
        {
            Id = Guid.NewGuid().ToString("N"),
            BlendWeights = snapshot.BlendWeights,
            MaterialColors = snapshot.MaterialColors,
            LocalRotation = new[] { rotation.x, rotation.y, rotation.z, rotation.w },
            StampVisible = snapshot.StampVisible,
            StampTint = snapshot.StampTint
        };
    }

    private void ApplyRecordToClone(GameObject clone, TeapotCollectionStore.Record record)
    {
        if (clone == null || record == null)
            return;

        SkinnedMeshRenderer skin = FindCloneTeapotRenderer(clone);
        if (skin == null)
            return;

        if (record.LocalRotation != null && record.LocalRotation.Length >= 4)
        {
            clone.transform.rotation = new Quaternion(
                record.LocalRotation[0],
                record.LocalRotation[1],
                record.LocalRotation[2],
                record.LocalRotation[3]);
        }

        if (record.BlendWeights != null && skin.sharedMesh != null)
        {
            int count = Mathf.Min(record.BlendWeights.Length, skin.sharedMesh.blendShapeCount);
            for (int i = 0; i < count; i++)
                skin.SetBlendShapeWeight(i, record.BlendWeights[i]);
        }

        if (record.MaterialColors == null)
            return;

        Material[] materials = skin.materials;
        int colorCount = Mathf.Min(record.MaterialColors.Length, materials.Length);
        for (int i = 0; i < colorCount; i++)
        {
            if (materials[i] == null)
                continue;
            if (materials[i].HasProperty(BaseColorId))
                materials[i].SetColor(BaseColorId, record.MaterialColors[i]);
            if (materials[i].HasProperty(ColorId))
                materials[i].SetColor(ColorId, record.MaterialColors[i]);
        }

        skin.materials = materials;
    }

    private static void UniqueCloneMaterials(GameObject clone)
    {
        if (clone == null)
            return;
        Renderer[] renderers = clone.GetComponentsInChildren<Renderer>(true);
        for (int i = 0; i < renderers.Length; i++)
        {
            if (renderers[i] == null)
                continue;
            renderers[i].materials = renderers[i].materials;
        }
    }

    private static void StripGameplay(GameObject clone)
    {
        if (clone == null)
            return;
        TeapotFistRotationController[] fists =
            clone.GetComponentsInChildren<TeapotFistRotationController>(true);
        for (int i = 0; i < fists.Length; i++)
        {
            if (fists[i] != null)
                fists[i].AbandonCopiedPivot();
        }

        DisableAndDestroy(clone.GetComponentsInChildren<TeapotGestureController>(true));
        DisableAndDestroy(fists);
        DisableAndDestroy(clone.GetComponentsInChildren<SculptingEnhancementTeapotIndexGestureController>(true));
        DisableAndDestroy(clone.GetComponentsInChildren<SculptingEnhancementTeapotColorController>(true));
        DisableAndDestroy(clone.GetComponentsInChildren<Canvas>(true));
        DisableAndDestroy(clone.GetComponentsInChildren<Camera>(true));
        DisableAndDestroy(clone.GetComponentsInChildren<AudioListener>(true));
    }

    private static void DisableAndDestroy(Component[] components)
    {
        if (components == null)
            return;
        for (int i = 0; i < components.Length; i++)
        {
            if (components[i] == null)
                continue;
            Behaviour behaviour = components[i] as Behaviour;
            if (behaviour != null)
                behaviour.enabled = false;
            Destroy(components[i]);
        }
    }

    private SkinnedMeshRenderer FindCloneTeapotRenderer(GameObject clone)
    {
        SkinnedMeshRenderer[] skins = clone.GetComponentsInChildren<SkinnedMeshRenderer>(true);
        if (skins == null || skins.Length == 0)
            return null;
        if (teapotRenderer != null)
        {
            for (int i = 0; i < skins.Length; i++)
            {
                if (skins[i] != null && skins[i].name == teapotRenderer.name)
                    return skins[i];
            }
        }

        for (int i = 0; i < skins.Length; i++)
        {
            if (skins[i] == null)
                continue;
            if (skins[i].name == "TeapotSelfieSurfaceOverlay" ||
                skins[i].name == "TeapotBodyDesignOverlay")
                continue;
            return skins[i];
        }

        return skins[0];
    }

    private void CopyDesignOntoClone(GameObject clone)
    {
        if (clone == null || appliedDesignTexture == null)
            return;

        SkinnedMeshRenderer cloneSkin = FindCloneTeapotRenderer(clone);
        if (cloneSkin == null || cloneSkin.sharedMesh == null)
            return;

        Shader shader = Shader.Find("Morphic/Lantern Body Design Overlay");
        if (shader == null)
            return;

        GameObject overlayObject = new GameObject("TeapotBodyDesignOverlay");
        Transform overlayRoot = overlayObject.transform;
        Transform skinTransform = cloneSkin.transform;
        overlayRoot.SetParent(skinTransform.parent != null ? skinTransform.parent : clone.transform, false);
        overlayRoot.localPosition = skinTransform.localPosition;
        overlayRoot.localRotation = skinTransform.localRotation;
        overlayRoot.localScale = skinTransform.localScale;
        overlayObject.layer = cloneSkin.gameObject.layer;

        SkinnedMeshRenderer overlay = overlayObject.AddComponent<SkinnedMeshRenderer>();
        overlay.sharedMesh = cloneSkin.sharedMesh;
        overlay.rootBone = cloneSkin.rootBone;
        overlay.bones = cloneSkin.bones;
        overlay.localBounds = cloneSkin.localBounds;
        overlay.quality = cloneSkin.quality;
        overlay.updateWhenOffscreen = true;
        overlay.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        overlay.receiveShadows = false;
        overlay.sortingOrder = 15;

        Material hidden = new Material(shader) { name = "Morphic Shelf Hidden Design" };
        hidden.SetFloat("_Opacity", 0f);
        Material visible = new Material(shader) { name = "Morphic Shelf Teapot Design" };
        visible.SetTexture("_DesignTex", appliedDesignTexture);
        visible.SetFloat("_Opacity", 1f);
        visible.SetFloat("_RepeatCount", designRepeatCount);
        visible.SetFloat("_PatchHalfAngle", designPatchHalfAngle);
        visible.SetFloat("_VerticalHalfFraction", designVerticalHalfFraction);
        visible.SetColor("_DesignColor", committedDesignTint);
        Bounds localBounds = GetTeapotBodyLocalBounds();
        visible.SetVector("_ObjectCenter", localBounds.center);
        visible.SetVector("_ObjectExtents", localBounds.extents);

        int subMeshCount = Mathf.Max(1, cloneSkin.sharedMesh.subMeshCount);
        Material[] overlayMaterials = new Material[subMeshCount];
        for (int i = 0; i < overlayMaterials.Length; i++)
            overlayMaterials[i] = hidden;
        overlayMaterials[Mathf.Clamp(bodyMaterialIndex, 0, subMeshCount - 1)] = visible;
        overlay.sharedMaterials = overlayMaterials;
        if (cloneSkin.sharedMesh != null)
        {
            for (int i = 0; i < cloneSkin.sharedMesh.blendShapeCount; i++)
                overlay.SetBlendShapeWeight(i, cloneSkin.GetBlendShapeWeight(i));
        }

        overlay.enabled = true;
    }

    private void CopySelfieSurfaceOntoClone(GameObject clone)
    {
        if (clone == null || appliedSelfieTexture == null)
            return;

        DestroyNamedChildren(clone.transform, "TeapotSelfiePlates");

        SkinnedMeshRenderer cloneSkin = FindCloneTeapotRenderer(clone);
        if (cloneSkin == null || cloneSkin.sharedMesh == null)
            return;

        Shader shader = Shader.Find("Morphic/Selfie Surface Overlay");
        if (shader == null)
            return;

        Texture2D shelfSelfie = CopyTexturePixels(appliedSelfieTexture, "Morphic Shelf Teapot Selfie");
        SkinnedMeshRenderer overlay = FindNamedOverlay(clone.transform, "TeapotSelfieSurfaceOverlay");
        if (overlay == null)
        {
            GameObject overlayObject = new GameObject("TeapotSelfieSurfaceOverlay");
            overlay = overlayObject.AddComponent<SkinnedMeshRenderer>();
        }

        PlaceCloneOverlay(overlay.transform, clone, cloneSkin);
        overlay.sharedMesh = cloneSkin.sharedMesh;
        overlay.rootBone = cloneSkin.rootBone;
        overlay.bones = cloneSkin.bones;
        overlay.localBounds = cloneSkin.localBounds;
        overlay.quality = cloneSkin.quality;
        overlay.updateWhenOffscreen = true;
        overlay.skinnedMotionVectors = false;
        overlay.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        overlay.receiveShadows = false;
        overlay.forceRenderingOff = false;
        overlay.allowOcclusionWhenDynamic = false;
        overlay.renderingLayerMask = cloneSkin.renderingLayerMask;
        overlay.sortingOrder = 20;

        Material hidden = new Material(shader)
        {
            name = "Morphic Shelf Hidden Teapot Selfie Submesh",
            renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent + 20
        };
        hidden.SetFloat("_Opacity", 0f);

        Material visible = new Material(shader)
        {
            name = "Morphic Shelf Teapot Selfie Surface",
            renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent + 20
        };
        ApplyTeapotSelfieMaterial(visible, cloneSkin, shelfSelfie, selfieUpLocal, selfieFrontLocal);

        int subMeshCount = Mathf.Max(1, cloneSkin.sharedMesh.subMeshCount);
        Material[] overlayMaterials = new Material[subMeshCount];
        for (int i = 0; i < overlayMaterials.Length; i++)
            overlayMaterials[i] = hidden;
        int[] slots = BodyAndBandSlots(bodyMaterialIndex);
        bool painted = false;
        if (slots != null)
        {
            for (int i = 0; i < slots.Length; i++)
            {
                int slot = slots[i];
                if (slot < 0 || slot >= overlayMaterials.Length)
                    continue;
                overlayMaterials[slot] = visible;
                painted = true;
            }
        }

        if (!painted)
            overlayMaterials[Mathf.Clamp(bodyMaterialIndex, 0, subMeshCount - 1)] = visible;
        overlay.sharedMaterials = overlayMaterials;

        if (cloneSkin.sharedMesh != null)
        {
            int count = cloneSkin.sharedMesh.blendShapeCount;
            for (int i = 0; i < count; i++)
                overlay.SetBlendShapeWeight(i, cloneSkin.GetBlendShapeWeight(i));
        }

        overlay.enabled = true;
        overlay.gameObject.SetActive(true);
    }

    private static SkinnedMeshRenderer FindNamedOverlay(Transform root, string objectName)
    {
        if (root == null)
            return null;
        Transform[] children = root.GetComponentsInChildren<Transform>(true);
        for (int i = 0; i < children.Length; i++)
        {
            Transform child = children[i];
            if (child == null || child.name != objectName)
                continue;
            if (child.TryGetComponent(out SkinnedMeshRenderer skin))
                return skin;
        }

        return null;
    }

    private static void PlaceCloneOverlay(Transform overlayRoot, GameObject clone, SkinnedMeshRenderer cloneSkin)
    {
        if (overlayRoot == null || clone == null || cloneSkin == null)
            return;

        Transform cloneSkinTransform = cloneSkin.transform;
        if (cloneSkinTransform == clone.transform)
        {
            overlayRoot.SetParent(clone.transform, false);
            overlayRoot.localPosition = Vector3.zero;
            overlayRoot.localRotation = Quaternion.identity;
            overlayRoot.localScale = Vector3.one;
        }
        else
        {
            Transform overlayParent = cloneSkinTransform.parent;
            if (overlayParent == null ||
                (overlayParent != clone.transform && !overlayParent.IsChildOf(clone.transform)))
                overlayParent = clone.transform;
            overlayRoot.SetParent(overlayParent, false);
            overlayRoot.localPosition = cloneSkinTransform.localPosition;
            overlayRoot.localRotation = cloneSkinTransform.localRotation;
            overlayRoot.localScale = cloneSkinTransform.localScale;
        }

        overlayRoot.gameObject.layer = cloneSkin.gameObject.layer;
    }

    private static void DestroyNamedChildren(Transform root, string objectName)
    {
        if (root == null || string.IsNullOrEmpty(objectName))
            return;

        Transform[] children = root.GetComponentsInChildren<Transform>(true);
        for (int i = children.Length - 1; i >= 0; i--)
        {
            Transform child = children[i];
            if (child == null || child == root || child.name != objectName)
                continue;
            Destroy(child.gameObject);
        }
    }

    private void CommitColor(Color color)
    {
        if (selectedPart < 0)
            selectedPart = 0;
        ApplyColor(selectedPart, color);
        if (designTintActive)
            committedDesignTint = color;
        else
            CommitTargetColor(selectedPart, color);
        PushHistory();
        instructionText.text = designTintActive
            ? "Stamp tint selected. Choose another color or press DONE."
            : "Color selected. Choose another color or press DONE.";
    }

    private void CaptureInitialColors()
    {
        if (colorsCaptured || teapotRenderer == null)
            return;

        runtimeMaterials = teapotRenderer.materials;
        IsolateMovingPart("LID", isolatedLidMaterials, "_Lid");
        IsolateMovingPart("SHAFT", isolatedSpoutMaterials, "_Spout");
        IsolateMovingPart("HANDLE", isolatedHandleMaterials, "_Handle");
        NeutralizeBandGold();
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
            committedMaterialColors[i] = ReadMaterialColor(runtimeMaterials[i]);
    }

    private void ResolvePartMaterialIndices()
    {
        partMaterialIndices = new int[partNames.Length][];
        int count = runtimeMaterials != null ? runtimeMaterials.Length : 0;
        int[] all = new int[count];
        for (int i = 0; i < count; i++)
            all[i] = i;
        partMaterialIndices[0] = all;
        partMaterialIndices[1] = BodyAndBandSlots(bodyMaterialIndex);
        partMaterialIndices[2] = IsolatedOrFallback(isolatedLidMaterials, bodyMaterialIndex);
        partMaterialIndices[3] = IsolatedOrFallback(isolatedSpoutMaterials, bodyMaterialIndex);
        partMaterialIndices[4] = IsolatedOrFallback(isolatedHandleMaterials, bodyMaterialIndex);
    }

    private int[] IsolatedOrFallback(List<Material> isolated, int fallback)
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

        return fallback >= 0 ? new[] { fallback } : Array.Empty<int>();
    }

    private void IsolateMovingPart(string token, List<Material> isolated, string suffix)
    {
        Mesh source = teapotRenderer != null ? teapotRenderer.sharedMesh : null;
        if (source == null || !source.isReadable || runtimeMaterials == null)
            return;

        bool[] partVerts = MarkMovingVertices(source, token);
        if (!HasMarked(partVerts))
            return;

        if (runtimePartMesh == null)
        {
            runtimePartMesh = Instantiate(source);
            runtimePartMesh.name = source.name + "_PartColors";
            teapotRenderer.sharedMesh = runtimePartMesh;
        }

        List<Material> materials = new List<Material>(runtimeMaterials);
        int originalCount = runtimePartMesh.subMeshCount;
        for (int subMesh = 0; subMesh < originalCount; subMesh++)
        {
            int[] triangles = runtimePartMesh.GetTriangles(subMesh);
            if (triangles.Length < 3)
                continue;

            List<int> partTris = new List<int>();
            List<int> restTris = new List<int>();
            for (int i = 0; i < triangles.Length; i += 3)
            {
                int marked = 0;
                if (IsMarked(partVerts, triangles[i])) marked++;
                if (IsMarked(partVerts, triangles[i + 1])) marked++;
                if (IsMarked(partVerts, triangles[i + 2])) marked++;
                List<int> dest = marked >= 2 ? partTris : restTris;
                dest.Add(triangles[i]);
                dest.Add(triangles[i + 1]);
                dest.Add(triangles[i + 2]);
            }

            if (partTris.Count == 0)
                continue;
            if (restTris.Count == 0)
            {
                if (subMesh < materials.Count)
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
            partMat.name = (sourceMat != null ? sourceMat.name : "Teapot") + suffix;
            materials.Add(partMat);
            isolated.Add(partMat);
        }

        runtimePartMesh.RecalculateBounds();
        runtimeMaterials = materials.ToArray();
        teapotRenderer.materials = runtimeMaterials;
    }

    private static bool[] MarkMovingVertices(Mesh mesh, string token)
    {
        bool[] moving = new bool[mesh.vertexCount];
        Vector3[] delta = new Vector3[mesh.vertexCount];
        for (int shape = 0; shape < mesh.blendShapeCount; shape++)
        {
            string compact = mesh.GetBlendShapeName(shape).Replace(" ", "").ToUpperInvariant();
            if (!compact.Contains(token))
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

    private static bool HasMarked(bool[] marks)
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

    private int[] GetMaterialIndices(int partIndex)
    {
        if (partMaterialIndices != null &&
            partIndex >= 0 &&
            partIndex < partMaterialIndices.Length &&
            partMaterialIndices[partIndex] != null)
            return partMaterialIndices[partIndex];
        return Array.Empty<int>();
    }

    private void HideExtraCustomizeGroups()
    {
        if (variantGroup != null)
            variantGroup.SetActive(false);
        if (bodyActionGroup != null)
            bodyActionGroup.SetActive(false);
        if (designGroup != null)
            designGroup.SetActive(false);
    }

    private static bool PartNeedsVariant(int partIndex)
    {
        return partIndex == 2 || partIndex == 3 || partIndex == 4;
    }

    private static string[] GetVariantLabels(int partIndex)
    {
        if (partIndex == 3)
            return new[] { "Spout 1", "Spout 2" };
        if (partIndex == 4)
            return new[] { "Handle 1", "Handle 2" };
        return new[] { "Lid 1", "Lid 2" };
    }

    private void ShowVariantSelection(int partIndex)
    {
        EnsureVariantGroup();
        colorBackToDesigns = false;
        designTintActive = false;
        selectedPart = partIndex;
        mode = ColorMode.SelectVariant;
        SetPanelHeight(VariantSelectionPanelHeight);
        mainActionGroup.SetActive(false);
        partGroup.SetActive(false);
        if (bodyActionGroup != null)
            bodyActionGroup.SetActive(false);
        if (designGroup != null)
            designGroup.SetActive(false);
        colorGroup.SetActive(false);
        if (reviewGroup != null)
            reviewGroup.SetActive(false);
        if (selfieEntryButton != null)
            selfieEntryButton.gameObject.SetActive(false);
        if (variantGroup != null)
            variantGroup.SetActive(true);

        string[] labels = GetVariantLabels(partIndex);
        for (int i = 0; i < variantButtons.Length; i++)
        {
            if (variantButtonLabels[i] != null)
                variantButtonLabels[i].text = labels[i];
        }

        headerText.text = partNames[partIndex];
        SetHeaderBackLabel("← PARTS");
        SetHeaderBackVisible(true);
        SetTitleBurstsVisible(false);
        instructionText.gameObject.SetActive(true);
        if (TryAdoptVisibleVariant(partIndex))
        {
            ArmSelectedVariant();
            instructionText.text = selectedVariantLabel +
                " is already showing. Change color, or pick another shape.";
        }
        else
        {
            instructionText.text = partIndex == 4
                ? "Choose Handle 1 or Handle 2."
                : partIndex == 3
                    ? "Choose Spout 1 or Spout 2."
                    : "Choose Lid 1 or Lid 2.";
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
        ArmSelectedVariant();

        if (selectedPart == 2)
            instructionText.text =
                "Pinch with your right hand and lift to raise " +
                selectedVariantLabel.ToLowerInvariant() + ".";
        else if (selectedPart == 3)
            instructionText.text =
                "Pinch with your left hand and lift to pull " +
                selectedVariantLabel.ToLowerInvariant() + ".";
        else
            instructionText.text =
                "Make two fists and pull them down to bring " +
                selectedVariantLabel.ToLowerInvariant() + " out.";
        SetSculptControllersEnabled(true);
        SetHoveredTarget(null);
    }

    private void ArmSelectedVariant()
    {
        if (teapotGestures == null)
            return;
        if (selectedPart == 2)
            teapotGestures.ArmLidVariant(selectedVariant == 1);
        else if (selectedPart == 3)
            teapotGestures.ArmShaftVariant(selectedVariant == 1);
        else if (selectedPart == 4)
            teapotGestures.ArmHandleVariant(selectedVariant == 1);
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
        if (teapotRenderer == null || !PartNeedsVariant(partIndex))
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
        if (teapotRenderer == null || shapeIndex < 0)
            return 0f;
        return teapotRenderer.GetBlendShapeWeight(shapeIndex);
    }

    private int FindVariantShape(int partIndex, int variantIndex)
    {
        Mesh mesh = teapotRenderer != null ? teapotRenderer.sharedMesh : null;
        if (mesh == null)
            return -1;

        string name = partIndex == 3
            ? (variantIndex == 0 ? "Shaft 1" : "Shaft 2")
            : partIndex == 4
                ? (variantIndex == 0 ? "Handle 1" : "Handle 2")
                : (variantIndex == 0 ? "Lid 1" : "Lid 2");
        for (int i = 0; i < mesh.blendShapeCount; i++)
        {
            if (string.Equals(mesh.GetBlendShapeName(i), name, StringComparison.OrdinalIgnoreCase))
                return i;
        }

        return -1;
    }

    private void EnsureVariantGroup()
    {
        if (variantGroup != null || panel == null)
            return;

        variantGroup = new GameObject("VariantSelection", typeof(RectTransform));
        variantGroup.transform.SetParent(panel.transform, false);
        RectTransform variantRect = variantGroup.GetComponent<RectTransform>();
        DockTop(variantRect, -124f, 280f);

        for (int i = 0; i < variantButtons.Length; i++)
        {
            int variantIndex = i;
            variantButtons[i] = CreateButton(
                "Variant" + i,
                variantRect,
                new Vector2(0f, -i * 70f),
                new Vector2(PillWidth, PillHeight),
                GetVariantLabels(2)[i],
                PillPink);
            variantButtons[i].onClick.AddListener(() => SelectVariant(variantIndex));
            variantButtonLabels[i] = variantButtons[i].GetComponentInChildren<TMP_Text>();
            RegisterHitTarget(variantButtons[i], false, true, default);
        }

        Button nextColorsButton = CreateButton(
            "NextColors", variantRect, new Vector2(0f, -152f),
            new Vector2(PillWidth, PillHeight), "CHANGE COLOR", ActionPink);
        nextColorsButton.onClick.AddListener(ContinueToColorSelection);
        RegisterHitTarget(nextColorsButton, false, false, default);

        Button variantDoneButton = CreateButton(
            "Done", variantRect, new Vector2(0f, -224f),
            new Vector2(PillWidth, PillHeight), "DONE", DonePink);
        variantDoneButton.onClick.AddListener(ShowMainMenu);
        RegisterHitTarget(variantDoneButton, false, false, default);
        variantGroup.SetActive(false);
    }

    private void ShowBodyActions()
    {
        EnsureBodyActionGroup();
        colorBackToDesigns = false;
        designTintActive = false;
        selectedPart = 1;
        mode = ColorMode.SelectBodyActions;
        SetPanelHeight(BodyActionsPanelHeight);
        mainActionGroup.SetActive(false);
        partGroup.SetActive(false);
        if (variantGroup != null)
            variantGroup.SetActive(false);
        if (designGroup != null)
            designGroup.SetActive(false);
        colorGroup.SetActive(false);
        if (reviewGroup != null)
            reviewGroup.SetActive(false);
        if (bodyActionGroup != null)
            bodyActionGroup.SetActive(true);
        if (selfieEntryButton != null)
            selfieEntryButton.gameObject.SetActive(false);
        instructionText.gameObject.SetActive(true);
        SetTitleBurstsVisible(false);
        headerText.text = "TEAPOT BODY";
        SetHeaderBackLabel("← PARTS");
        SetHeaderBackVisible(true);
        instructionText.text = appliedDesignTexture != null
            ? "Change the pot color, or add another design."
            : "Change the pot color, or add a stamped design.";
        ResetDwell();
        SetHoveredTarget(null);
    }

    private void ShowDesignSelection()
    {
        LoadStampPatterns();
        EnsureDesignGroup();
        RefreshDesignButtons();
        colorBackToDesigns = true;
        selectedPart = 1;
        mode = ColorMode.SelectDesign;
        SetPanelHeight(DesignSelectionPanelHeight);
        mainActionGroup.SetActive(false);
        partGroup.SetActive(false);
        if (variantGroup != null)
            variantGroup.SetActive(false);
        if (bodyActionGroup != null)
            bodyActionGroup.SetActive(false);
        colorGroup.SetActive(false);
        if (reviewGroup != null)
            reviewGroup.SetActive(false);
        if (designGroup != null)
            designGroup.SetActive(true);
        if (selfieEntryButton != null)
            selfieEntryButton.gameObject.SetActive(false);
        instructionText.gameObject.SetActive(true);
        SetTitleBurstsVisible(false);
        headerText.text = "ADD DESIGN";
        SetHeaderBackLabel("← BODY");
        SetHeaderBackVisible(true);
        instructionText.text = stampPatterns == null || stampPatterns.Length == 0
            ? "No stamps found in StampPatterns."
            : "Choose a stamp. Then tint it with Change Color.";
        ResetDwell();
        SetHoveredTarget(null);
    }

    private void OpenBodyPaperColor()
    {
        colorBackToDesigns = false;
        designTintActive = false;
        selectedPart = 1;
        ContinueToColorSelection();
    }

    private void OpenDesignTintColor()
    {
        if (appliedDesignTexture == null)
        {
            instructionText.text = "Choose a stamp first.";
            return;
        }

        colorBackToDesigns = true;
        designTintActive = true;
        selectedPart = 1;
        ContinueToColorSelection();
    }

    private void ApplyStamp(int stampIndex)
    {
        if (stampPatterns == null || stampIndex < 0 || stampIndex >= stampPatterns.Length)
            return;

        Texture2D stamp = stampPatterns[stampIndex];
        if (stamp == null)
            return;

        EnsureDesignOverlay();
        appliedDesignTexture = stamp;
        committedDesignTint = Color.white;
        designTintActive = true;
        ConfigureDesignSurfaceMaterial(stamp);
        ApplyDesignTint(committedDesignTint);
        if (designSurfaceRenderer != null)
            designSurfaceRenderer.enabled = true;
        instructionText.text = stamp.name + " applied. Change Color tints the stamp.";
        PushHistory();
    }

    private void ClearDesign()
    {
        appliedDesignTexture = null;
        designTintActive = false;
        committedDesignTint = Color.white;
        if (designSurfaceRenderer != null)
            designSurfaceRenderer.enabled = false;
        instructionText.text = "Design cleared.";
        PushHistory();
    }

    private void EnsureBodyActionGroup()
    {
        if (bodyActionGroup != null || panel == null)
            return;

        bodyActionGroup = new GameObject("BodyActions", typeof(RectTransform));
        bodyActionGroup.transform.SetParent(panel.transform, false);
        RectTransform bodyRect = bodyActionGroup.GetComponent<RectTransform>();
        DockTop(bodyRect, -124f, 220f);

        Button changeColor = CreateButton(
            "ChangeColor", bodyRect, new Vector2(0f, 0f),
            new Vector2(PillWidth, PillHeight), "CHANGE COLOR", ActionPink);
        changeColor.onClick.AddListener(OpenBodyPaperColor);
        RegisterHitTarget(changeColor, false, false, default);

        Button addDesign = CreateButton(
            "AddDesign", bodyRect, new Vector2(0f, -72f),
            new Vector2(PillWidth, PillHeight), "ADD DESIGN", PillPink);
        addDesign.onClick.AddListener(ShowDesignSelection);
        RegisterHitTarget(addDesign, false, false, default);

        Button done = CreateButton(
            "Done", bodyRect, new Vector2(0f, -144f),
            new Vector2(PillWidth, PillHeight), "DONE", DonePink);
        done.onClick.AddListener(ShowMainMenu);
        RegisterHitTarget(done, false, false, default);
        bodyActionGroup.SetActive(false);
    }

    private void EnsureDesignGroup()
    {
        if (designGroup != null || panel == null)
            return;

        designGroup = new GameObject("DesignSelection", typeof(RectTransform));
        designGroup.transform.SetParent(panel.transform, false);
        RectTransform designRect = designGroup.GetComponent<RectTransform>();
        DockTop(designRect, -124f, 420f);

        for (int i = 0; i < designButtons.Length; i++)
        {
            int stampIndex = i;
            int column = i % 2;
            int row = i / 2;
            designButtons[i] = CreateButton(
                "Stamp" + i,
                designRect,
                new Vector2(-76f + column * 152f, -row * 78f),
                new Vector2(138f, 70f),
                "Stamp " + (i + 1),
                PillPink);
            designButtons[i].onClick.AddListener(() => ApplyStamp(stampIndex));
            designPreviews[i] = CreateDesignPreview(designButtons[i].GetComponent<RectTransform>());
            RegisterHitTarget(designButtons[i], false, true, default);
        }

        Button tintButton = CreateButton(
            "ChangeColor", designRect, new Vector2(0f, -246f),
            new Vector2(PillWidth, PillHeight), "CHANGE COLOR", ActionPink);
        tintButton.onClick.AddListener(OpenDesignTintColor);
        RegisterHitTarget(tintButton, false, false, default);

        Button clearButton = CreateButton(
            "ClearDesign", designRect, new Vector2(0f, -318f),
            new Vector2(PillWidth, PillHeight), "CLEAR DESIGN", PillPink);
        clearButton.onClick.AddListener(ClearDesign);
        RegisterHitTarget(clearButton, false, false, default);

        Button done = CreateButton(
            "Done", designRect, new Vector2(0f, -390f),
            new Vector2(PillWidth, PillHeight), "DONE", DonePink);
        done.onClick.AddListener(ShowMainMenu);
        RegisterHitTarget(done, false, false, default);
        designGroup.SetActive(false);
    }

    private static RawImage CreateDesignPreview(RectTransform parent)
    {
        GameObject previewObject = new GameObject("Preview", typeof(RectTransform), typeof(RawImage));
        previewObject.transform.SetParent(parent, false);
        RectTransform rect = previewObject.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.08f, 0.28f);
        rect.anchorMax = new Vector2(0.92f, 0.92f);
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        RawImage image = previewObject.GetComponent<RawImage>();
        image.color = Color.white;
        image.raycastTarget = false;
        return image;
    }

    private void RefreshDesignButtons()
    {
        int stampCount = stampPatterns != null ? stampPatterns.Length : 0;
        for (int i = 0; i < designButtons.Length; i++)
        {
            bool visible = i < stampCount && stampPatterns[i] != null;
            if (designButtons[i] != null)
                designButtons[i].gameObject.SetActive(visible);
            if (!visible)
                continue;

            if (designPreviews[i] != null)
                designPreviews[i].texture = stampPatterns[i];
            TMP_Text label = designButtons[i].GetComponentInChildren<TMP_Text>();
            if (label != null)
            {
                label.text = stampPatterns[i].name;
                RectTransform labelRect = label.rectTransform;
                labelRect.anchorMin = new Vector2(0f, 0f);
                labelRect.anchorMax = new Vector2(1f, 0.28f);
                labelRect.offsetMin = Vector2.zero;
                labelRect.offsetMax = Vector2.zero;
            }
        }
    }

    private void LoadStampPatterns()
    {
        if (stampPatterns != null && stampPatterns.Length > 0)
            return;

#if UNITY_EDITOR
        string[] guids = UnityEditor.AssetDatabase.FindAssets(
            "t:Texture2D",
            new[] { "Assets/SculptingEnhancement/Art/StampPatterns" });
        var loaded = new List<Texture2D>(guids.Length);
        for (int i = 0; i < guids.Length; i++)
        {
            string path = UnityEditor.AssetDatabase.GUIDToAssetPath(guids[i]);
            Texture2D texture = UnityEditor.AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            if (texture != null)
                loaded.Add(texture);
        }

        loaded.Sort((a, b) => string.CompareOrdinal(a.name, b.name));
        stampPatterns = loaded.ToArray();
#endif
    }

    private void EnsureDesignOverlay()
    {
        if (teapotRenderer == null || teapotRenderer.sharedMesh == null)
            return;

        Shader shader = Shader.Find("Morphic/Lantern Body Design Overlay");
        if (shader == null)
        {
            Debug.LogError("MORPHIC: Body design shader was not found.", this);
            return;
        }

        if (designSurfaceRoot == null)
        {
            GameObject overlayObject = new GameObject("TeapotBodyDesignOverlay");
            designSurfaceRoot = overlayObject.transform;
            designSurfaceRenderer = overlayObject.AddComponent<SkinnedMeshRenderer>();
        }

        if (designSurfaceMaterial == null)
        {
            designSurfaceMaterial = new Material(shader)
            {
                name = "Morphic Teapot Body Design Material",
                renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent + 15
            };
        }

        if (designHiddenMaterial == null)
        {
            designHiddenMaterial = new Material(shader)
            {
                name = "Morphic Hidden Teapot Design Submesh Material",
                renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent + 15
            };
            designHiddenMaterial.SetFloat("_Opacity", 0f);
        }

        Transform rendererTransform = teapotRenderer.transform;
        designSurfaceRoot.SetParent(rendererTransform.parent, false);
        designSurfaceRoot.localPosition = rendererTransform.localPosition;
        designSurfaceRoot.localRotation = rendererTransform.localRotation;
        designSurfaceRoot.localScale = rendererTransform.localScale;
        designSurfaceRoot.gameObject.layer = teapotRenderer.gameObject.layer;
        designSurfaceRoot.gameObject.SetActive(true);

        designSurfaceRenderer.sharedMesh = teapotRenderer.sharedMesh;
        designSurfaceRenderer.rootBone = teapotRenderer.rootBone;
        designSurfaceRenderer.bones = teapotRenderer.bones;
        designSurfaceRenderer.localBounds = teapotRenderer.localBounds;
        designSurfaceRenderer.quality = teapotRenderer.quality;
        designSurfaceRenderer.updateWhenOffscreen = teapotRenderer.updateWhenOffscreen;
        designSurfaceRenderer.skinnedMotionVectors = false;
        designSurfaceRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        designSurfaceRenderer.receiveShadows = false;
        designSurfaceRenderer.forceRenderingOff = false;
        designSurfaceRenderer.allowOcclusionWhenDynamic = false;
        designSurfaceRenderer.renderingLayerMask = teapotRenderer.renderingLayerMask;
        designSurfaceRenderer.sortingOrder = 15;

        int subMeshCount = Mathf.Max(1, teapotRenderer.sharedMesh.subMeshCount);
        Material[] overlayMaterials = new Material[subMeshCount];
        for (int i = 0; i < overlayMaterials.Length; i++)
            overlayMaterials[i] = designHiddenMaterial;
        overlayMaterials[Mathf.Clamp(bodyMaterialIndex, 0, subMeshCount - 1)] = designSurfaceMaterial;
        designSurfaceRenderer.sharedMaterials = overlayMaterials;
        designSurfaceRenderer.enabled = true;
    }

    private void SyncDesignSurfaceRenderer()
    {
        if (designSurfaceRenderer == null || teapotRenderer == null)
            return;

        Transform rendererTransform = teapotRenderer.transform;
        if (designSurfaceRoot.parent != rendererTransform.parent)
            designSurfaceRoot.SetParent(rendererTransform.parent, false);
        designSurfaceRoot.localPosition = rendererTransform.localPosition;
        designSurfaceRoot.localRotation = rendererTransform.localRotation;
        designSurfaceRoot.localScale = rendererTransform.localScale;

        designSurfaceRenderer.rootBone = teapotRenderer.rootBone;
        designSurfaceRenderer.bones = teapotRenderer.bones;
        designSurfaceRenderer.localBounds = teapotRenderer.localBounds;

        Mesh mesh = teapotRenderer.sharedMesh;
        if (mesh == null)
            return;
        if (designSurfaceRenderer.sharedMesh != mesh)
            designSurfaceRenderer.sharedMesh = mesh;

        for (int i = 0; i < mesh.blendShapeCount; i++)
            designSurfaceRenderer.SetBlendShapeWeight(i, teapotRenderer.GetBlendShapeWeight(i));
    }

    private void ConfigureDesignSurfaceMaterial(Texture texture)
    {
        if (designSurfaceMaterial == null || teapotRenderer == null || texture == null)
            return;

        Bounds localBounds = GetTeapotBodyLocalBounds();
        designSurfaceMaterial.SetTexture("_DesignTex", texture);
        designSurfaceMaterial.SetFloat("_Opacity", 1f);
        designSurfaceMaterial.SetFloat("_RepeatCount", designRepeatCount);
        designSurfaceMaterial.SetFloat("_PatchHalfAngle", designPatchHalfAngle);
        designSurfaceMaterial.SetFloat("_VerticalHalfFraction", designVerticalHalfFraction);
        designSurfaceMaterial.SetVector("_ObjectCenter", localBounds.center);
        designSurfaceMaterial.SetVector("_ObjectExtents", localBounds.extents);
    }

    private void ApplyDesignTint(Color color)
    {
        if (designSurfaceMaterial == null)
            return;
        designSurfaceMaterial.SetColor("_DesignColor", color);
    }

    private void ApplyColor(int partIndex, Color color)
    {
        if (designTintActive && appliedDesignTexture != null)
        {
            ApplyDesignTint(color);
            return;
        }

        if (partIndex == 0 && runtimeMaterials != null)
        {
            for (int i = 0; i < runtimeMaterials.Length; i++)
                SetMaterialColor(i, color);
            return;
        }

        int[] slots = GetMaterialIndices(partIndex);
        for (int i = 0; i < slots.Length; i++)
            SetMaterialColor(slots[i], color);
    }

    private void SetMaterialColor(int materialIndex, Color color)
    {
        if (runtimeMaterials == null || materialIndex < 0 || materialIndex >= runtimeMaterials.Length)
            return;
        Material material = runtimeMaterials[materialIndex];
        if (material == null)
            return;
        if (material.HasProperty(BaseColorId))
            material.SetColor(BaseColorId, color);
        if (material.HasProperty(ColorId))
            material.SetColor(ColorId, color);
        if (material.HasProperty(EmissionColorId))
            material.SetColor(EmissionColorId, IsBandMaterial(material) ? Color.black : color);
    }

    private void NeutralizeBandGold()
    {
        if (runtimeMaterials == null)
            return;
        for (int i = 0; i < runtimeMaterials.Length; i++)
        {
            Material material = runtimeMaterials[i];
            if (!IsBandMaterial(material))
                continue;
            if (material.HasProperty(BaseColorId))
                material.SetColor(BaseColorId, BandGrayColor);
            if (material.HasProperty(ColorId))
                material.SetColor(ColorId, BandGrayColor);
            if (material.HasProperty(EmissionColorId))
                material.SetColor(EmissionColorId, Color.black);
            material.DisableKeyword("_EMISSION");
        }
    }

    private int[] BodyAndBandSlots(int bodyIndex)
    {
        List<int> slots = new List<int>();
        if (bodyIndex >= 0)
            slots.Add(bodyIndex);
        if (runtimeMaterials == null)
            return slots.ToArray();

        for (int i = 0; i < runtimeMaterials.Length; i++)
        {
            if (slots.Contains(i))
                continue;
            if (i == 0 || IsBandMaterial(runtimeMaterials[i]))
                slots.Add(i);
        }

        return slots.ToArray();
    }

    private static bool IsBandMaterial(Material material)
    {
        if (material == null)
            return false;
        string materialName = material.name.Replace(" (Instance)", string.Empty);
        return materialName == "TeapotBandGold" ||
            materialName.StartsWith("TeapotBandGold_", StringComparison.Ordinal) ||
            materialName == "Material.002" ||
            materialName.StartsWith("Material.002_", StringComparison.Ordinal);
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

        int[] slots = GetMaterialIndices(partIndex);
        for (int i = 0; i < slots.Length; i++)
        {
            int index = slots[i];
            if (index >= 0 && index < committedMaterialColors.Length)
                committedMaterialColors[index] = color;
        }
    }

    private void RestoreSelectedPreview()
    {
        if (designTintActive && appliedDesignTexture != null)
        {
            ApplyDesignTint(committedDesignTint);
            return;
        }

        if (committedMaterialColors == null)
            return;

        if (selectedPart <= 0)
        {
            for (int i = 0; i < committedMaterialColors.Length; i++)
                SetMaterialColor(i, committedMaterialColors[i]);
            return;
        }

        int[] slots = GetMaterialIndices(selectedPart);
        for (int i = 0; i < slots.Length; i++)
        {
            int index = slots[i];
            if (index >= 0 && index < committedMaterialColors.Length)
                SetMaterialColor(index, committedMaterialColors[index]);
        }
    }

    private static Color ReadMaterialColor(Material material)
    {
        if (material == null)
            return Color.white;
        if (material.HasProperty(BaseColorId))
            return material.GetColor(BaseColorId);
        if (material.HasProperty(ColorId))
            return material.GetColor(ColorId);
        return Color.white;
    }

    private void CaptureBaseline()
    {
        CaptureInitialColors();
        if (teapotGestures != null)
            teapotGestures.CaptureRestState();
        lastDeformationLocked = teapotGestures != null && teapotGestures.IsDeformationLocked;
        history.Clear();
        history.Add(CaptureSnapshot());
    }

    private void PushHistory()
    {
        history.Add(CaptureSnapshot());
        while (history.Count > MaxHistory)
            history.RemoveAt(0);
    }

    private TeapotSnapshot CaptureSnapshot()
    {
        TeapotSnapshot snapshot = new TeapotSnapshot();
        if (teapotRenderer != null && teapotRenderer.sharedMesh != null)
        {
            Mesh mesh = teapotRenderer.sharedMesh;
            snapshot.BlendWeights = new float[mesh.blendShapeCount];
            for (int i = 0; i < snapshot.BlendWeights.Length; i++)
                snapshot.BlendWeights[i] = teapotRenderer.GetBlendShapeWeight(i);
        }

        if (committedMaterialColors != null)
        {
            snapshot.MaterialColors = new Color[committedMaterialColors.Length];
            Array.Copy(committedMaterialColors, snapshot.MaterialColors, committedMaterialColors.Length);
        }

        snapshot.Stamp = appliedDesignTexture;
        snapshot.StampTint = committedDesignTint;
        snapshot.StampVisible = appliedDesignTexture != null &&
            designSurfaceRenderer != null &&
            designSurfaceRenderer.enabled;
        return snapshot;
    }

    private void ApplySnapshot(TeapotSnapshot snapshot)
    {
        if (snapshot == null || teapotRenderer == null)
            return;
        if (snapshot.BlendWeights != null && teapotRenderer.sharedMesh != null)
        {
            int count = Mathf.Min(snapshot.BlendWeights.Length, teapotRenderer.sharedMesh.blendShapeCount);
            for (int i = 0; i < count; i++)
                teapotRenderer.SetBlendShapeWeight(i, snapshot.BlendWeights[i]);
            if (teapotGestures != null)
                teapotGestures.SynchronizeWeightsFromRenderer();
        }

        if (snapshot.MaterialColors != null)
        {
            committedMaterialColors = new Color[snapshot.MaterialColors.Length];
            Array.Copy(snapshot.MaterialColors, committedMaterialColors, snapshot.MaterialColors.Length);
            for (int i = 0; i < committedMaterialColors.Length; i++)
                SetMaterialColor(i, committedMaterialColors[i]);
        }

        appliedDesignTexture = snapshot.Stamp;
        committedDesignTint = snapshot.StampTint;
        if (snapshot.StampVisible && snapshot.Stamp != null)
        {
            EnsureDesignOverlay();
            ConfigureDesignSurfaceMaterial(snapshot.Stamp);
            ApplyDesignTint(snapshot.StampTint);
            if (designSurfaceRenderer != null)
                designSurfaceRenderer.enabled = true;
        }
        else if (designSurfaceRenderer != null)
        {
            designSurfaceRenderer.enabled = false;
        }
    }

    private void PollSculptHistory()
    {
        if (teapotGestures == null)
            return;

        bool locked = teapotGestures.IsDeformationLocked;
        if (locked && !lastDeformationLocked)
            PushHistory();
        lastDeformationLocked = locked;
    }

    private void SetSculptControllersEnabled(bool enabled)
    {
        if (sculptLockedByTutorial && enabled)
            return;
        if (enabled && teapotGestures != null)
            teapotGestures.SynchronizeWeightsFromRenderer();
        if (teapotGestures != null)
        {
            teapotGestures.suppressDriving = !enabled;
            teapotGestures.enabled = true;
            teapotGestures.ownWidthHeight = teapotIndexGestures == null || !teapotIndexGestures.enabled;
        }

        if (teapotIndexGestures != null)
        {
            teapotIndexGestures.suppressDriving = !enabled;
            teapotIndexGestures.enabled = enabled;
            if (enabled)
                teapotIndexGestures.SynchronizeWeightsFromRenderer();
        }

        if (teapotFistRotation != null)
            teapotFistRotation.enabled = enabled;
    }

    private void UpdateSculptLockFromUi(bool overUi)
    {
        if (mode == ColorMode.Review || mode == ColorMode.Shelf || mode == ColorMode.ConfirmDelete)
        {
            SetSculptControllersEnabled(false);
            return;
        }

        if (mode == ColorMode.Sculpt || mode == ColorMode.MainMenu)
            return;
        if (mode == ColorMode.SelectVariant && selectedVariant >= 0 && !overUi)
        {
            SetSculptControllersEnabled(true);
            return;
        }

        SetSculptControllersEnabled(false);
    }

    private bool IsCursorOverColorUi(Vector2 screenPosition)
    {
        return hoveredTarget != null ||
               ContainsScreenPoint(panel, screenPosition) ||
               ContainsScreenPoint(selfiePanel, screenPosition);
    }

    private bool ContainsScreenPoint(GameObject root, Vector2 screenPosition)
    {
        if (root == null || !root.activeInHierarchy)
            return false;
        RectTransform rect = root.GetComponent<RectTransform>();
        return rect != null &&
               RectTransformUtility.RectangleContainsScreenPoint(rect, screenPosition, canvasCamera);
    }

    private void MoveCursor(Vector2 screenPosition)
    {
        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
                canvasRect, screenPosition, canvasCamera, out Vector2 localPoint))
            return;
        float factor = 1f - Mathf.Exp(-cursorSmoothing * Time.unscaledDeltaTime);
        cursorRect.anchoredPosition = Vector2.Lerp(cursorRect.anchoredPosition, localPoint, factor);
        cursorRect.SetAsLastSibling();
    }

    private void SetCursorVisible(bool visible)
    {
        if (cursorRect != null)
            cursorRect.gameObject.SetActive(visible && mode != ColorMode.Sculpt);
    }

    private HitTarget FindHitTarget(Vector2 screenPosition)
    {
        for (int i = 0; i < hitTargets.Count; i++)
        {
            HitTarget target = hitTargets[i];
            if (target.Button == null || !target.Button.gameObject.activeInHierarchy)
                continue;
            if (RectTransformUtility.RectangleContainsScreenPoint(target.Rect, screenPosition, canvasCamera))
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
        hoveredTarget = target;
        if (hoveredTarget != null && hoveredTarget.Outline != null)
            hoveredTarget.Outline.enabled = true;
        if (hoveredTarget != null && hoveredTarget.IsColor &&
            !hoveredTarget.IsFreeColorPalette && selectedPart >= 0)
            ApplyColor(selectedPart, hoveredTarget.PreviewColor);
        else if (hoveredTarget == null || !hoveredTarget.IsFreeColorPalette)
            RestoreSelectedPreview();
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

        dwellTimer += Time.unscaledDeltaTime;
        SetDwellProgress(target, Mathf.Clamp01(dwellTimer / selectionDwellTime));
        if (dwellTimer < selectionDwellTime)
            return;

        completedDwellTarget = target;
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
        outline.effectColor = LanguagePageBuilder.Green;
        outline.effectDistance = new Vector2(4f, -4f);
        outline.enabled = false;
        Image fill = GetButtonFill(button);
        hitTargets.Add(new HitTarget
        {
            Button = button,
            Rect = button.GetComponent<RectTransform>(),
            Outline = outline,
            IsColor = isColor,
            IsFreeColorPalette = isFreeColorPalette,
            IsPersistentChoice = isPersistentChoice,
            PreviewColor = previewColor,
            BaseColor = fill != null ? fill.color : Color.white,
            DwellEdges = CreateDwellFrame(button.transform)
        });
    }

    private void CommitFreePaletteColor()
    {
        Vector2 screen = lastColorSampleScreen;
        if (screen.sqrMagnitude < 1f)
            screen = Input.mousePosition;
        SampleFreePaletteColor(screen);
        CommitColor(freePalettePreviewColor);
        if (freeColorSelectionCheck != null)
        {
            freeColorSelectionCheck.anchoredPosition = freePaletteMarkerPosition;
            freeColorSelectionCheck.gameObject.SetActive(true);
        }
    }

    private void UpdateFreePalettePreview(HitTarget target, Vector2 screenPosition)
    {
        if (target == null || !target.IsFreeColorPalette ||
            mode != ColorMode.SelectColor || selectedPart < 0)
            return;

        if (!SampleFreePaletteColor(screenPosition))
            return;

        target.PreviewColor = freePalettePreviewColor;
        ApplyColor(selectedPart, freePalettePreviewColor);
    }

    private bool SampleFreePaletteColor(Vector2 screenPosition)
    {
        if (freeColorPaletteImage == null)
            return false;

        RectTransform paletteRect = freeColorPaletteImage.rectTransform;
        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
                paletteRect, screenPosition, canvasCamera, out Vector2 localPoint))
            return false;

        Rect rect = paletteRect.rect;
        if (rect.width < 0.001f || rect.height < 0.001f)
            return false;

        float x = Mathf.Clamp01(Mathf.InverseLerp(rect.xMin, rect.xMax, localPoint.x));
        float y = Mathf.Clamp01(Mathf.InverseLerp(rect.yMin, rect.yMax, localPoint.y));
        freePalettePreviewColor = EvaluateFreePaletteColor(x, y);
        freePaletteMarkerPosition = localPoint;
        return true;
    }

    private static Color EvaluateFreePaletteColor(float x, float y)
    {
        Color hue = Color.HSVToRGB(Mathf.Repeat(x, 1f), 1f, 1f);
        return y >= 0.5f
            ? Color.Lerp(hue, Color.white, (y - 0.5f) * 2f)
            : Color.Lerp(Color.black, hue, y * 2f);
    }

    private Button CreateFreeColorPalette(Transform parent, Vector2 anchoredPosition, Vector2 size)
    {
        GameObject paletteObject = new GameObject(
            "FreeColorPalette", typeof(RectTransform), typeof(RawImage), typeof(Button));
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
        Texture2D texture = new Texture2D(width, height, TextureFormat.RGB24, false)
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
                texture.SetPixel(x, y, EvaluateFreePaletteColor(normalizedX, normalizedY));
            }
        }

        texture.Apply(false, true);
        image.texture = texture;
        image.color = Color.white;
        image.raycastTarget = true;
    }

    private RectTransform CreateSelectionCheck(RectTransform parent, bool movable = false)
    {
        GameObject checkObject = new GameObject("SelectionCheck", typeof(RectTransform));
        checkObject.transform.SetParent(parent, false);
        RectTransform check = checkObject.GetComponent<RectTransform>();
        check.anchorMin = check.anchorMax = movable
            ? new Vector2(0.5f, 1f)
            : new Vector2(0.5f, 0.5f);
        check.pivot = new Vector2(0.5f, 0.5f);
        check.anchoredPosition = Vector2.zero;
        check.sizeDelta = new Vector2(38f, 38f);

        CreateCheckStroke("LeftBorder", check, new Vector2(-5f, -2f), new Vector2(15f, 8f), -45f, Color.black);
        CreateCheckStroke("RightBorder", check, new Vector2(5f, 0f), new Vector2(23f, 8f), 45f, Color.black);
        CreateCheckStroke("LeftFill", check, new Vector2(-5f, -2f), new Vector2(15f, 4f), -45f, Color.white);
        CreateCheckStroke("RightFill", check, new Vector2(5f, 0f), new Vector2(23f, 4f), 45f, Color.white);
        checkObject.SetActive(false);
        return check;
    }

    private static void CreateCheckStroke(
        string objectName, Transform parent, Vector2 anchoredPosition, Vector2 size, float rotation, Color color)
    {
        GameObject strokeObject = new GameObject(objectName, typeof(RectTransform), typeof(Image));
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

    private Image[] CreateDwellFrame(Transform parent)
    {
        Color progressColor = new Color32(255, 224, 92, 255);
        Image[] edges = new Image[4];
        edges[0] = MakeEdge(parent, "DwellTop", new Vector2(0f, 1f), new Vector2(1f, 1f),
            new Vector2(0f, 6f), Vector2.zero, Image.FillMethod.Horizontal, 0, progressColor);
        edges[1] = MakeEdge(parent, "DwellRight", new Vector2(1f, 0f), new Vector2(1f, 1f),
            new Vector2(6f, 0f), Vector2.zero, Image.FillMethod.Vertical, 0, progressColor);
        edges[2] = MakeEdge(parent, "DwellBottom", new Vector2(0f, 0f), new Vector2(1f, 0f),
            new Vector2(0f, 6f), Vector2.zero, Image.FillMethod.Horizontal, 1, progressColor);
        edges[3] = MakeEdge(parent, "DwellLeft", new Vector2(0f, 0f), new Vector2(0f, 1f),
            new Vector2(6f, 0f), Vector2.zero, Image.FillMethod.Vertical, 1, progressColor);
        return edges;
    }

    private static Image MakeEdge(
        Transform parent, string name, Vector2 min, Vector2 max, Vector2 size,
        Vector2 pos, Image.FillMethod method, int origin, Color color)
    {
        GameObject go = new GameObject(name, typeof(RectTransform), typeof(Image));
        go.transform.SetParent(parent, false);
        RectTransform rect = go.GetComponent<RectTransform>();
        rect.anchorMin = min;
        rect.anchorMax = max;
        rect.sizeDelta = size;
        rect.anchoredPosition = pos;
        Image image = go.GetComponent<Image>();
        image.color = color;
        image.raycastTarget = false;
        image.type = Image.Type.Filled;
        image.fillMethod = method;
        image.fillOrigin = origin;
        image.fillAmount = 0f;
        image.enabled = false;
        return image;
    }

    private Button CreateHeaderBackButton(RectTransform parent)
    {
        Button button = CreateButton("HeaderBack", parent, new Vector2(20f, -16f),
            new Vector2(140f, 36f), "← BACK", PillPink);
        RectTransform rect = button.GetComponent<RectTransform>();
        rect.anchorMin = rect.anchorMax = new Vector2(0f, 1f);
        rect.pivot = new Vector2(0f, 1f);
        rect.anchoredPosition = new Vector2(16f, -12f);
        button.onClick.AddListener(NavigateBack);
        RegisterHitTarget(button, false, false, default);
        return button;
    }

    private void SetHeaderBackVisible(bool visible)
    {
        if (headerBackButton != null)
            headerBackButton.gameObject.SetActive(visible);
    }

    private void SetHeaderBackLabel(string label)
    {
        if (headerBackText != null)
            headerBackText.text = label;
    }

    private void EnsureSelfieOnPalette()
    {
        if (panel == null)
            return;

        if (selfieEntryButton == null)
        {
            selfieEntryButton = CreateButton(
                "SelfieEntry", panel.transform, new Vector2(0f, -118f),
                new Vector2(PillWidth, PillHeight), "SELFIE", new Color32(155, 92, 130, 255));
            selfieEntryButton.onClick.RemoveListener(ShowSelfieCapture);
            selfieEntryButton.onClick.AddListener(ShowSelfieCapture);
            RegisterHitTarget(selfieEntryButton, false, false, default);
        }

        RectTransform rect = selfieEntryButton.GetComponent<RectTransform>();
        rect.SetParent(panel.transform, false);
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 1f);
        rect.pivot = new Vector2(0.5f, 1f);
        rect.anchoredPosition = new Vector2(0f, -118f);
        rect.sizeDelta = new Vector2(PillWidth, PillHeight);
        selfieEntryButton.gameObject.SetActive(mode == ColorMode.MainMenu);
    }

    private void SetPanelHeight(float height)
    {
        if (panel == null)
            return;
        panel.GetComponent<RectTransform>().sizeDelta =
            new Vector2(CustomizePanelWidth, height + HistoryBarHeight);
    }

    private static void ApplyBottomRightDocking(RectTransform rect)
    {
        rect.anchorMin = rect.anchorMax = new Vector2(1f, 0f);
        rect.pivot = new Vector2(1f, 0f);
        rect.anchoredPosition = new Vector2(-24f, 24f);
        rect.sizeDelta = new Vector2(CustomizePanelWidth, 400f);
    }

    private static void DockTop(RectTransform rect, float y, float height)
    {
        rect.anchorMin = new Vector2(0f, 1f);
        rect.anchorMax = new Vector2(1f, 1f);
        rect.pivot = new Vector2(0.5f, 1f);
        rect.anchoredPosition = new Vector2(0f, y);
        rect.sizeDelta = new Vector2(-32f, height);
    }

    private static void CenterInBar(Button button, float x)
    {
        RectTransform rect = button.GetComponent<RectTransform>();
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = new Vector2(x, 0f);
    }

    private Button CreateButton(
        string objectName, Transform parent, Vector2 anchoredPosition,
        Vector2 size, string label, Color color)
    {
        bool isDone = objectName == "Done" || objectName == "Reset" || objectName == "Finish";
        bool isChangeColor = objectName == "ChangeColor" || objectName == "NextColors";
        bool isSwatch = string.IsNullOrEmpty(label) || objectName.StartsWith("Color");
        Color fillColor = isSwatch ? color : isDone ? DonePink : isChangeColor ? ActionPink : PillPink;

        Image frame = LanguagePageBuilder.CreateRoundedImage(objectName, parent, 28, LanguagePageBuilder.Green);
        RectTransform rect = frame.rectTransform;
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 1f);
        rect.pivot = new Vector2(0.5f, 1f);
        rect.anchoredPosition = anchoredPosition;
        rect.sizeDelta = size;
        frame.raycastTarget = true;

        Image fill = LanguagePageBuilder.CreateRoundedImage("Fill", frame.transform, 24, fillColor);
        LanguagePageBuilder.Stretch(fill.rectTransform, new Vector2(5f, 5f));
        fill.raycastTarget = true;

        Button button = frame.gameObject.AddComponent<Button>();
        button.targetGraphic = fill;
        if (!string.IsNullOrEmpty(label))
        {
            TMP_Text text = CreateText(
                "Label", fill.rectTransform, Vector2.zero, size - new Vector2(16f, 10f),
                label, isDone || isChangeColor ? 22f : 20f,
                TextAlignmentOptions.Center, FontStyles.Bold);
            Stretch(text.rectTransform);
            text.color = LanguagePageBuilder.Green;
            text.raycastTarget = false;
        }

        return button;
    }

    private static TMP_Text CreateText(
        string objectName, Transform parent, Vector2 anchoredPosition, Vector2 size,
        string value, float fontSize, TextAlignmentOptions alignment, FontStyles style)
    {
        GameObject go = new GameObject(objectName, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        RectTransform rect = go.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0f, 1f);
        rect.anchorMax = new Vector2(0f, 1f);
        rect.pivot = new Vector2(0f, 1f);
        rect.anchoredPosition = anchoredPosition;
        rect.sizeDelta = size;
        TextMeshProUGUI text = go.AddComponent<TextMeshProUGUI>();
        text.text = value;
        text.fontSize = fontSize;
        text.alignment = alignment;
        text.fontStyle = style;
        return text;
    }

    private static void StyleMorphicCard(RectTransform panelRect)
    {
        Image frame = LanguagePageBuilder.CreateRoundedImage("Frame", panelRect, 32, LanguagePageBuilder.Green);
        LanguagePageBuilder.Stretch(frame.rectTransform);
        frame.raycastTarget = false;
        Image fill = LanguagePageBuilder.CreateRoundedImage("Fill", panelRect, 28, CardCream);
        LanguagePageBuilder.Stretch(fill.rectTransform, new Vector2(8f, 8f));
        fill.raycastTarget = false;
    }

    private static void StyleMorphicTitle(TMP_Text text)
    {
        if (text == null)
            return;
        text.color = LanguagePageBuilder.Pink;
        text.fontStyle = FontStyles.Bold;
        Outline outline = text.GetComponent<Outline>();
        if (outline == null)
            outline = text.gameObject.AddComponent<Outline>();
        outline.effectColor = LanguagePageBuilder.Green;
        outline.effectDistance = new Vector2(3.5f, -3.5f);
    }

    private void BuildTitleBursts(RectTransform parent)
    {
        titleBurstRoot = new GameObject("TitleBursts", typeof(RectTransform));
        titleBurstRoot.transform.SetParent(parent, false);
        Stretch(titleBurstRoot.GetComponent<RectTransform>());
        Vector2[] positions =
        {
            new Vector2(-150f, -28f), new Vector2(150f, -28f),
            new Vector2(-168f, -50f), new Vector2(168f, -50f)
        };
        float[] rotations = { 28f, -28f, -18f, 18f };
        for (int i = 0; i < positions.Length; i++)
        {
            Image tick = LanguagePageBuilder.CreateRoundedImage(
                "Burst" + i, titleBurstRoot.transform, 8, LanguagePageBuilder.Green);
            RectTransform rect = tick.rectTransform;
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 1f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = positions[i];
            rect.sizeDelta = new Vector2(18f, 7f);
            rect.localEulerAngles = new Vector3(0f, 0f, rotations[i]);
            tick.raycastTarget = false;
        }
    }

    private void SetTitleBurstsVisible(bool visible)
    {
        if (titleBurstRoot != null)
            titleBurstRoot.SetActive(visible);
    }

    private static Image GetButtonFill(Button button)
    {
        if (button == null)
            return null;
        Transform fill = button.transform.Find("Fill");
        return fill != null ? fill.GetComponent<Image>() : button.GetComponent<Image>();
    }

    private static Vector2 ExpandCameraPoint(Vector2 point, float multiplier)
    {
        return new Vector2(
            0.5f + (point.x - 0.5f) * multiplier,
            0.5f + (point.y - 0.5f) * multiplier);
    }

    private static void Stretch(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
    }

    private static void DestroyRuntime(UnityEngine.Object target)
    {
        if (target == null)
            return;
        if (Application.isPlaying)
            Destroy(target);
        else
            DestroyImmediate(target);
    }
}
