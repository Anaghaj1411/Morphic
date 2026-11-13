using System;
using System.Collections;
using System.Collections.Generic;
using Mediapipe.Unity.Sample;
using Mediapipe.Unity.Sample.HandLandmarkDetection;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

[DefaultExecutionOrder(260)]
public sealed partial class SculptingEnhancementKerisColorController : MonoBehaviour
{
    private enum ColorMode
    {
        Hidden,
        MainMenu,
        SelectPart,
        SelectBodyActions,
        SelectVariant,
        SelectColor,
        SelectDesign,
        SelectDrawMotif,
        DrawBandTargeting,
        VertexSculpt,
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
        public bool IsPersistentChoice;
        public bool IsFreeColorPalette;
        public Color PreviewColor;
        public Color BaseColor;
        public RectTransform SelectionCheck;
        public Image[] DwellEdges;
    }

    [SerializeField] private SculptHandLandmarkerRunner handRunner;
    [SerializeField] private Renderer kerisRenderer;
    [SerializeField] private SkinnedMeshRenderer kerisSkin;
    [SerializeField] private TeapotSurfacePatternPainter patternPainter;
    [SerializeField, Range(0.25f, 2.5f)] private float selectionDwellTime = 1.5f;
    [SerializeField, Min(1f)] private float cursorSmoothing = 11f;
    [SerializeField, Range(1f, 2.5f)] private float cameraToScreenRangeMultiplier = 1.8f;
    [SerializeField] private int selfieWidth = 640;
    [SerializeField] private int selfieHeight = 480;
    [SerializeField] private int selfieFPS = 30;
    [Header("Vertex Sculpt Camera")]
    [Tooltip("On = use the pose below during vertex sculpt and band drawing.")]
    [SerializeField] private bool useCustomSculptCamera = true;
    [SerializeField] private Vector3 sculptCameraPosition = new Vector3(-26.06807f, 2.537f, -6.085f);
    [SerializeField] private Vector3 sculptCameraRotation = new Vector3(0f, -257f, 90f);
    [SerializeField] private float sculptCameraFov = 50f;
    private static readonly Vector3 KerisSculptCamPos = new Vector3(-26.06807f, 2.537f, -6.085f);
    private static readonly Vector3 KerisSculptCamEuler = new Vector3(0f, -257f, 90f);
    private const float KerisSculptCamFov = 50f;

    private static readonly Color CardCream = LanguagePageBuilder.Cream;
    private static readonly Color PillPink = LanguagePageBuilder.Pink;
    private static readonly Color ActionPink = new Color32(255, 122, 210, 255);
    private static readonly Color DonePink = new Color32(236, 64, 168, 255);
    private static readonly Color DarkTextGreen = MorphicText.Ink;
    private const float CustomizePanelWidth = 540f;
    private const float PillWidth = 490f;
    private const float PillHeight = 60f;
    private const float PillStride = 82f;
    private const float MainMenuPanelHeight = 428f;
    private const float ColorSelectionPanelHeight = 680f;
    private const float VertexSculptPanelHeight = 600f;
    private const float HistoryBarHeight = 118f;
    private const float PartSelectionPanelHeight = 600f;
    private const float VariantSelectionPanelHeight = 460f;
    private const float ReviewPanelHeight = 360f;
    private const float ShelfPanelHeight = 450f;
    private const float SelfiePanelHeight = 690f;
    private const float BodyActionsPanelHeight = 430f;
    private const float DesignSelectionPanelHeight = 620f;
    private const float BandSelectionPanelHeight = 840f;
    private const float ConfirmResetPanelHeight = 280f;
    private const int PartBody = 0;
    private const int PartCarving = 1;
    private const int PartHandle = 2;

    private readonly string[] partNames =
    {
        "KERIS BODY",
        "KERIS CARVING",
        "KERIS HANDLE"
    };

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

    private readonly float[] coilSizes = { 0.0004f, 0.0009f, 0.0018f, 0.0035f };
    private readonly string[] coilSizeLabels = { "S", "M", "L", "XL" };
    private readonly float[] coilHeights = { 0.28f, 0.75f, 1.45f, 2.6f };
    private readonly string[] coilHeightLabels = { "SOFT", "MED", "STRONG", "MAX" };
    private readonly Button[] coilSizeButtons = new Button[4];
    private readonly Button[] coilHeightButtons = new Button[4];
    private readonly List<HitTarget> hitTargets = new List<HitTarget>();
    private enum TimedUndo { Clay, Sculpt, Band, Selfie, History }
    private readonly List<TimedUndo> timedUndo = new List<TimedUndo>();
    private readonly List<TimedUndo> timedRedo = new List<TimedUndo>();
    private int trackedClayCount;
    private int trackedBandCount;
    private int trackedSculptCount;
    private const int MaxHistory = 20;
    private sealed class KerisSnapshot
    {
        public float[] BlendWeights;
        public Color[] PartColors;
        public Color CommittedColor;
        public Texture Stamp;
        public Color StampTint;
        public bool StampVisible;
    }
    private readonly List<KerisSnapshot> history = new List<KerisSnapshot>();
    private readonly List<KerisSnapshot> redoHistory = new List<KerisSnapshot>();
    private bool lastDeformationLocked;
    private bool vertexSculptCameraZoomed;
    private bool hasStoredSculptCam;
    private Vector3 storedSculptCamPos;
    private Quaternion storedSculptCamRot;
    private float storedSculptCamFov;
    private float storedSculptCamNear;
    private bool hasHeldSculptCam;
    private Vector3 heldSculptCamPos;
    private Quaternion heldSculptCamRot;
    private float heldSculptCamFov;
    private Coroutine sculptCameraMove;

    private ColorMode mode = ColorMode.Hidden;
    private bool paletteAvailable;
    private Canvas canvas;
    private RectTransform canvasRect;
    private Camera canvasCamera;
    private GameObject uiRoot;
    private GameObject panel;
    private GameObject mainActionGroup;
    private GameObject partGroup;
    private GameObject variantGroup;
    private GameObject colorGroup;
    private GameObject vertexSculptGroup;
    private GameObject historyBar;
    private Button selfieEntryButton;
    private readonly Button[] partButtons = new Button[3];
    private readonly Button[] variantButtons = new Button[2];
    private readonly TMP_Text[] variantButtonLabels = new TMP_Text[2];
    private RectTransform cursorRect;
    private TMP_Text headerText;
    private TMP_Text headerBackText;
    private Button headerBackButton;
    private TMP_Text instructionText;
    private GameObject titleBurstRoot;
    private Image clayColorChip;
    private Color committedClayColor = new Color(0.74f, 0.42f, 0.30f, 1f);
    private static readonly Color KerisBaseBrass = new Color(0.80f, 0.58f, 0.22f, 1f);
    private Color committedKerisColor = KerisBaseBrass;
    private Color previewKerisColor = KerisBaseBrass;
    private HitTarget hoveredTarget;
    private HitTarget dwellTarget;
    private HitTarget completedDwellTarget;
    private HitTarget selectedColorTarget;
    private float dwellTimer;
    private int selectedCoilSize;
    private int selectedCoilHeight;
    private bool clayTintActive;
    private int selectedPart = -1;
    private int selectedVariant = -1;
    private string selectedVariantLabel = "";
    private KerisFistRotationController fistRotation;
    private KerisGestureController kerisGestures;
    private VertexSculptController vertexSculptController;
    private bool sculptLockedByTutorial;

    public void BindFrom(SculptHandLandmarkerRunner runner, GameObject kerisRoot)
    {
        handRunner = runner;
        if (kerisRoot != null)
        {
            kerisSkin = kerisRoot.GetComponentInChildren<SkinnedMeshRenderer>(true);
            kerisRenderer = kerisSkin;
            if (kerisRenderer == null)
                kerisRenderer = kerisRoot.GetComponentInChildren<MeshRenderer>(true);
            fistRotation = kerisRoot.GetComponentInChildren<KerisFistRotationController>(true);
            vertexSculptController = kerisRoot.GetComponentInChildren<VertexSculptController>(true);
            EnsureSkinnedForPainter(kerisRoot);
            EnsureKerisGestures();
            LogKerisShapeKeys();
        }

        if (paletteAvailable)
            ShowMainMenu();
    }

    public void WriteHelpDecor(MorphicCraftHelpState state)
    {
        if (state == null)
            return;

        state.hasStamp = appliedDesignTexture != null ? 1 : 0;
        state.hasSelfie = appliedSelfieTexture != null ? 1 : 0;
        state.currentStage = paletteAvailable && mode != ColorMode.Hidden
            ? "customize"
            : "sculpt";
        state.currentTool = "keris";
        MorphicLisaCraftReview.FillDecor(
            state,
            committedKerisColor,
            savedPartColors,
            partNames,
            appliedDesignTexture,
            appliedSelfieTexture,
            patternPainter,
            vertexSculptController);
    }

    public void SetTutorialSculptLock(bool locked)
    {
        sculptLockedByTutorial = locked;
        if (locked)
        {
            SetSculptGesturesEnabled(false);
            if (fistRotation != null)
                fistRotation.suppressDriving = true;
        }
    }

    public void SetPaletteAvailable(bool available)
    {
        paletteAvailable = available;
        if (!available)
        {
            SnapRestoreVertexSculptCamera();
            DisableVertexSculpt();
            if (uiRoot != null)
                uiRoot.SetActive(false);
            mode = ColorMode.Hidden;
            SetSculptGesturesEnabled(false);
            if (fistRotation != null)
                fistRotation.suppressDriving = sculptLockedByTutorial;
            return;
        }

        SnapViewToPotteryStudio();
        CaptureStudioCamera();
        EnsureKerisShelf();
        if (uiRoot == null)
            BuildInterface();
        if (uiRoot != null)
            uiRoot.SetActive(true);
        ShowMainMenu();
        CaptureBaseline();
    }

    private void Awake()
    {
        if (kerisSkin == null)
            kerisSkin = GetComponentInChildren<SkinnedMeshRenderer>(true);
        if (kerisRenderer == null)
            kerisRenderer = kerisSkin != null ? kerisSkin : GetComponentInChildren<Renderer>(true);
        fistRotation = GetComponentInChildren<KerisFistRotationController>(true);
        kerisGestures = GetComponentInChildren<KerisGestureController>(true);
        if (vertexSculptController == null)
            vertexSculptController = GetComponentInChildren<VertexSculptController>(true);
        CaptureKerisColor();
    }

    private void OnDisable()
    {
        SnapRestoreVertexSculptCamera();
        DisableVertexSculpt();
        if (uiRoot != null)
            uiRoot.SetActive(false);
    }

    private void LateUpdate()
    {
        PollSculptHistory();
        if (vertexSculptCameraZoomed && sculptCameraMove == null)
            ApplyKerisSculptCameraPose(ResolveViewCamera());

        if (!workingKerisVisible)
            return;

        if (appliedDesignTexture != null)
        {
            SyncDesignSurfaceRenderer();
            ConfigureDesignSurfaceMaterial(appliedDesignTexture);
        }
        else if (designSurfaceRoot != null && designSurfaceRoot.gameObject.activeSelf)
        {
            designSurfaceRoot.gameObject.SetActive(false);
        }

        if (appliedSelfieTexture == null)
        {
            SetSelfieCirclesVisible(false);
            return;
        }

        SyncSelfieSurfaceRenderer();
        ConfigureSelfieSurfaceMaterial(appliedSelfieTexture);
    }

    private void OnDestroy()
    {
        DestroyRuntime(capturedSelfieTexture);
        DestroyRuntime(appliedSelfieTexture);
        DestroyRuntime(selfieSurfaceMaterial);
        DestroyRuntime(designSurfaceMaterial);
        DestroyRuntime(designHiddenMaterial);
        DestroyRuntime(tintedDesignTexture);
        DestroyRuntime(stampOverlayMesh);
        DestroyRuntime(stampAlphaTexture);
        DestroyRuntime(freeColorTexture);
        DestroyRuntime(selfieOverlayMesh);
        DestroyRuntime(selfieBoundsMesh);
        DestroySelfieCircles();
        if (designSurfaceRoot != null)
            Destroy(designSurfaceRoot.gameObject);
        SnapRestoreVertexSculptCamera();
    }

    private void Update()
    {
        if (uiRoot == null || !uiRoot.activeSelf || canvasRect == null)
            return;

        if (mode == ColorMode.Selfie && capturedSelfieTexture == null)
            ShowLiveSelfiePreview();

        Vector2 screenPosition;
        SculptHandFrame hand = default;
        bool hasHand = handRunner != null && handRunner.TryGetLatestHand(out hand);
        if (hasHand)
        {
            SetCursorVisible(true);
            Vector2 expanded = ExpandCameraPoint(
                new Vector2(hand.IndexTip.x, hand.IndexTip.y),
                cameraToScreenRangeMultiplier);
            screenPosition = new Vector2(expanded.x * Screen.width, expanded.y * Screen.height);
            screenPosition = MoveCursor(screenPosition);
        }
        else
        {
            SetCursorVisible(false);
            screenPosition = Input.mousePosition;
            if (mode != ColorMode.SelectColor && mode != ColorMode.VertexSculpt &&
                mode != ColorMode.DrawBandTargeting && mode != ColorMode.SelectDrawMotif)
            {
                SetHoveredTarget(null);
                ResetDwell();
                TrackNewPainterActions();
                return;
            }
        }

        HitTarget nextHovered = FindHitTarget(screenPosition);
        UpdateFreePalettePreview(nextHovered, screenPosition);
        if (mode == ColorMode.DrawBandTargeting && patternPainter != null)
            DriveBandDrawing(screenPosition, hasHand, nextHovered);
        if (mode == ColorMode.VertexSculpt && patternPainter != null)
        {
            bool cursorOverUi = IsCursorOverColorUi(screenPosition);
            if (cursorOverUi)
                patternPainter.SetPaintingEnabled(false);
            else
            {
                patternPainter.SetClayCoilMode(true);
                patternPainter.SetPaintingEnabled(true);
                patternPainter.ProcessPointer(screenPosition, hasHand, Input.GetMouseButton(0));
            }
        }

        bool overUi = nextHovered != null ||
                      ContainsScreenPoint(panel, screenPosition) ||
                      ContainsScreenPoint(selfiePanel, screenPosition);
        bool exclusiveDraw = mode == ColorMode.VertexSculpt || mode == ColorMode.DrawBandTargeting;
        bool allowGestures = !sculptLockedByTutorial && !overUi && mode != ColorMode.Hidden && !exclusiveDraw;
        SetSculptGesturesEnabled(allowGestures);
        if (fistRotation != null)
            fistRotation.suppressDriving = overUi || sculptLockedByTutorial ||
                (kerisGestures != null && kerisGestures.IsDrivingSculpt);
        if (instructionText != null && instructionText.gameObject.activeSelf &&
            kerisGestures != null && kerisGestures.IsDeformationLocked &&
            mode != ColorMode.VertexSculpt && mode != ColorMode.SelectColor)
            MorphicLoc.Apply(instructionText, "LOCKED. Point both index fingers to unlock.");

        SetHoveredTarget(nextHovered);
        UpdateDwellSelection(nextHovered);
        TrackNewPainterActions();
    }

    private void BuildInterface()
    {
        canvas = FindFirstObjectByType<Canvas>();
        if (canvas == null)
            return;

        canvasRect = canvas.transform as RectTransform;
        canvasCamera = canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
        uiRoot = new GameObject("KerisColorUI", typeof(RectTransform));
        uiRoot.transform.SetParent(canvas.transform, false);
        Stretch(uiRoot.GetComponent<RectTransform>());

        panel = new GameObject("KerisColorPanel", typeof(RectTransform));
        panel.transform.SetParent(uiRoot.transform, false);
        RectTransform panelRect = panel.GetComponent<RectTransform>();
        StyleMorphicCard(panelRect);
        ApplyBottomRightDocking(panelRect);

        headerText = CreateText("Header", panelRect, new Vector2(10f, -16f),
            new Vector2(520f, 56f), "COLOR PALETTE", 40f,
            TextAlignmentOptions.Center, FontStyles.Bold);
        StyleMorphicTitle(headerText);
        BuildTitleBursts(panelRect);
        headerBackButton = CreateHeaderBackButton(panelRect);
        headerBackText = headerBackButton.GetComponentInChildren<TMP_Text>();
        instructionText = CreateText("Instruction", panelRect, new Vector2(10f, -90f),
            new Vector2(520f, 52f), "Choose a color or sculpt clay.", 22f,
            TextAlignmentOptions.Center, FontStyles.Bold);
        MorphicText.FitBody(instructionText);

        mainActionGroup = new GameObject("MainActions", typeof(RectTransform));
        mainActionGroup.transform.SetParent(panelRect, false);
        RectTransform mainRect = mainActionGroup.GetComponent<RectTransform>();
        DockTop(mainRect, -196f, 180f);

        selfieEntryButton = CreateButton("SelfieEntry", panelRect, new Vector2(0f, -118f),
            new Vector2(PillWidth, PillHeight), "SELFIE", new Color32(155, 92, 130, 255));
        selfieEntryButton.onClick.AddListener(ShowSelfieCapture);
        RegisterHitTarget(selfieEntryButton, false, false, Color.white);

        Button selectPart = CreateButton("SelectPart", mainRect, Vector2.zero,
            new Vector2(PillWidth, PillHeight), "SELECT PART", PillPink);
        selectPart.onClick.AddListener(ShowPartSelection);
        RegisterHitTarget(selectPart, false, false, Color.white);

        Button vertexSculpt = CreateButton("VertexSculpt", mainRect, new Vector2(0f, -72f),
            new Vector2(PillWidth, PillHeight), "VERTEX SCULPT", PillPink);
        TMP_Text vText = vertexSculpt.GetComponentInChildren<TMP_Text>();
        if (vText != null)
        {
            vText.fontSize = 22f;
            vText.color = DarkTextGreen;
        }
        vertexSculpt.onClick.AddListener(ShowVertexSculpt);
        RegisterHitTarget(vertexSculpt, false, false, Color.white);

        Button finish = CreateButton("Finish", mainRect, new Vector2(0f, -144f),
            new Vector2(PillWidth, PillHeight), "FINISH", DonePink);
        finish.onClick.AddListener(ShowReview);
        RegisterHitTarget(finish, false, false, Color.white);

        partGroup = new GameObject("PartSelection", typeof(RectTransform));
        partGroup.transform.SetParent(panelRect, false);
        RectTransform partRect = partGroup.GetComponent<RectTransform>();
        MorphicStudioLayout.DockContent(
            partRect, true, MorphicStudioLayout.PartListHeight(partNames.Length, PillStride));

        for (int i = 0; i < partNames.Length; i++)
        {
            int partIndex = i;
            partButtons[i] = CreateButton(
                "Part" + i, partRect, new Vector2(0f, -i * PillStride),
                new Vector2(PillWidth, PillHeight), partNames[i], PillPink);
            partButtons[i].onClick.AddListener(() => SelectPart(partIndex));
            RegisterHitTarget(partButtons[i], false, true, Color.white);
        }

        Button partDone = CreateButton("Done", partRect, new Vector2(0f, -partNames.Length * PillStride),
            new Vector2(PillWidth, PillHeight), "DONE", DonePink);
        partDone.onClick.AddListener(ShowMainMenu);
        RegisterHitTarget(partDone, false, false, Color.white);

        BuildVariantGroup(panelRect);

        colorGroup = new GameObject("ColorSelection", typeof(RectTransform));
        colorGroup.transform.SetParent(panelRect, false);
        RectTransform colorRect = colorGroup.GetComponent<RectTransform>();
        DockTop(colorRect, -124f, 490f);
        for (int i = 0; i < paletteColors.Length; i++)
        {
            Color color = paletteColors[i];
            int column = i % 4;
            int row = i / 4;
            Button swatch = CreateButton("Color" + i, colorRect,
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
        colorDone.onClick.AddListener(OnColorDoneClicked);
        RegisterHitTarget(colorDone, false, false, Color.white);

        BuildVertexSculptGroup(panelRect);
        BuildHistoryBar(panelRect);
        BuildConfirmReset(panelRect);
        BuildReview(panelRect);
        BuildSelfie(uiRoot.transform);
        BuildCursor();
        MorphicLoc.BindTree(uiRoot.transform);
        partGroup.SetActive(false);
        if (variantGroup != null)
            variantGroup.SetActive(false);
        colorGroup.SetActive(false);
        vertexSculptGroup.SetActive(false);
    }

    private void BuildVertexSculptGroup(RectTransform panelRect)
    {
        vertexSculptGroup = new GameObject("VertexSculptOptions", typeof(RectTransform));
        vertexSculptGroup.transform.SetParent(panelRect, false);
        RectTransform sculptRect = vertexSculptGroup.GetComponent<RectTransform>();
        DockTop(sculptRect, -124f, 440f);

        TMP_Text sizeTitle = CreateText("BrushSizeTitle", sculptRect, new Vector2(0f, 0f),
            new Vector2(PillWidth, 24f), "BRUSH SIZE", 19f,
            TextAlignmentOptions.Center, FontStyles.Bold);
        sizeTitle.color = DarkTextGreen;
        for (int s = 0; s < coilSizeButtons.Length; s++)
        {
            int sizeIdx = s;
            coilSizeButtons[s] = CreateButton("SculptSize" + s, sculptRect,
                new Vector2(-174f + s * 116f, -40f), new Vector2(114f, 48f),
                coilSizeLabels[s], s == selectedCoilSize ? ActionPink : PillPink);
            TMP_Text sizeText = coilSizeButtons[s].GetComponentInChildren<TMP_Text>();
            if (sizeText != null)
            {
                sizeText.fontSize = 18f;
                sizeText.color = DarkTextGreen;
            }
            coilSizeButtons[s].onClick.AddListener(() => SelectCoilSize(sizeIdx));
            RegisterHitTarget(coilSizeButtons[s], false, false, Color.white);
        }

        TMP_Text strengthTitle = CreateText("BrushStrengthTitle", sculptRect, new Vector2(0f, -108f),
            new Vector2(PillWidth, 24f), "STRENGTH", 19f,
            TextAlignmentOptions.Center, FontStyles.Bold);
        strengthTitle.color = DarkTextGreen;
        for (int s = 0; s < coilHeightButtons.Length; s++)
        {
            int strengthIdx = s;
            coilHeightButtons[s] = CreateButton("SculptStrength" + s, sculptRect,
                new Vector2(-174f + s * 116f, -148f), new Vector2(114f, 48f),
                coilHeightLabels[s], s == selectedCoilHeight ? ActionPink : PillPink);
            TMP_Text strengthText = coilHeightButtons[s].GetComponentInChildren<TMP_Text>();
            if (strengthText != null)
            {
                strengthText.fontSize = 16f;
                strengthText.color = DarkTextGreen;
            }
            coilHeightButtons[s].onClick.AddListener(() => SelectCoilHeight(strengthIdx));
            RegisterHitTarget(coilHeightButtons[s], false, false, Color.white);
        }

        Button clayColorBtn = CreateButton("ClayColor", sculptRect, new Vector2(0f, -216f),
            new Vector2(PillWidth, 56f), "CLAY COLOR", ActionPink);
        clayColorBtn.onClick.AddListener(OpenClayColor);
        TMP_Text clayColText = clayColorBtn.GetComponentInChildren<TMP_Text>();
        if (clayColText != null)
        {
            clayColText.fontSize = 24f;
            clayColText.color = DarkTextGreen;
        }
        RegisterHitTarget(clayColorBtn, false, false, Color.white);

        Image claySwatchFrame = LanguagePageBuilder.CreateRoundedImage(
            "SwatchFrame", clayColorBtn.transform, 10, LanguagePageBuilder.Green);
        RectTransform claySfRect = claySwatchFrame.rectTransform;
        claySfRect.anchorMin = claySfRect.anchorMax = new Vector2(0.88f, 0.5f);
        claySfRect.pivot = new Vector2(0.5f, 0.5f);
        claySfRect.anchoredPosition = Vector2.zero;
        claySfRect.sizeDelta = new Vector2(52f, 34f);
        claySwatchFrame.raycastTarget = false;
        Image claySwatchFill = LanguagePageBuilder.CreateRoundedImage(
            "SwatchFill", claySwatchFrame.transform, 8, committedClayColor);
        LanguagePageBuilder.Stretch(claySwatchFill.rectTransform, new Vector2(3f, 3f));
        claySwatchFill.raycastTarget = false;
        clayColorChip = claySwatchFill;

        Button done = CreateButton("Done", sculptRect, new Vector2(0f, -292f),
            new Vector2(PillWidth, 60f), "DONE", DonePink);
        TMP_Text doneText = done.GetComponentInChildren<TMP_Text>();
        if (doneText != null)
        {
            doneText.fontSize = 26f;
            doneText.color = DarkTextGreen;
        }
        done.onClick.AddListener(ShowMainMenu);
        RegisterHitTarget(done, false, false, Color.white);
        vertexSculptGroup.SetActive(false);
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
        barRect.sizeDelta = new Vector2(-40f, 58f);

        Button undo = CreateButton("Undo", barRect, new Vector2(-175f, 0f),
            new Vector2(150f, 54f), "UNDO", PillPink);
        CenterInBar(undo, -175f);
        undo.onClick.AddListener(UndoClay);
        RegisterHitTarget(undo, false, false, Color.white);

        Button redo = CreateButton("Redo", barRect, new Vector2(0f, 0f),
            new Vector2(150f, 54f), "REDO", PillPink);
        CenterInBar(redo, 0f);
        redo.onClick.AddListener(RedoClay);
        RegisterHitTarget(redo, false, false, Color.white);

        Button reset = CreateButton("Reset", barRect, new Vector2(175f, 0f),
            new Vector2(150f, 54f), "RESET", DonePink);
        CenterInBar(reset, 175f);
        reset.onClick.AddListener(ShowConfirmReset);
        RegisterHitTarget(reset, false, false, Color.white);
    }

    private void BuildCursor()
    {
        GameObject cursor = new GameObject("HandColorCursor", typeof(RectTransform));
        cursor.transform.SetParent(uiRoot.transform, false);
        cursorRect = cursor.GetComponent<RectTransform>();
        cursorRect.anchorMin = cursorRect.anchorMax = new Vector2(0.5f, 0.5f);
        cursorRect.pivot = new Vector2(0.5f, 0.5f);
        cursorRect.sizeDelta = new Vector2(46f, 46f);
        TMP_Text cursorText = CreateText("CursorDot", cursorRect, Vector2.zero, new Vector2(46f, 46f),
            "●", 34f, TextAlignmentOptions.Center, FontStyles.Normal);
        Stretch(cursorText.rectTransform);
        cursorText.raycastTarget = false;
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
        DisableVertexSculpt();
        ApplyKerisColor(committedKerisColor);
        mode = ColorMode.MainMenu;
        SetPanelHeight(MainMenuPanelHeight);
        panel.SetActive(true);
        mainActionGroup.SetActive(true);
        HideExtraGroups();
        HideReviewAndShelf();
        if (selfiePanel != null)
            selfiePanel.SetActive(false);
        if (selfieEntryButton != null)
            selfieEntryButton.gameObject.SetActive(true);
        if (historyBar != null)
            historyBar.SetActive(true);
        MorphicLoc.Apply(headerText, "COLOR PALETTE");
        instructionText.gameObject.SetActive(false);
        SetTitleBurstsVisible(false);
        SetHeaderBackVisible(false);
        ResetDwell();
        SetHoveredTarget(null);
        SetSculptGesturesEnabled(true);
        if (kerisGestures != null)
        {
            kerisGestures.ArmCarvingVariant(selectedPart == PartCarving && selectedVariant == 1);
            kerisGestures.SetPinchDrivesCarving(true);
        }
        if (fistRotation != null)
            fistRotation.suppressDriving = false;
    }

    private void ShowColorSelection()
    {
        DisableVertexSculpt();
        mode = ColorMode.SelectColor;
        SetPanelHeight(ColorSelectionPanelHeight);
        mainActionGroup.SetActive(false);
        HideExtraGroups();
        colorGroup.SetActive(true);
        MorphicStudioLayout.DockContent(colorGroup.GetComponent<RectTransform>(), true, 490f);
        if (selfieEntryButton != null)
            selfieEntryButton.gameObject.SetActive(false);
        MorphicLoc.Apply(
            headerText,
            clayTintActive
                ? "CLAY COLOR"
                : designTintActive
                    ? "STAMP COLOR"
                    : bandTintActive
                        ? "BAND COLOR"
                        : ColorHeaderLabel());
        instructionText.gameObject.SetActive(true);
        MorphicLoc.Apply(
            instructionText,
            clayTintActive
                ? "Pick a clay color."
                : designTintActive
                    ? "Pick a tint for the stamp."
                    : bandTintActive
                        ? "Pick a color for the drawn band."
                        : "Pick a color for the keris.");
        SetHeaderBackLabel(clayTintActive
            ? "← BACK"
            : designTintActive
                ? "← DESIGNS"
                : bandTintActive
                    ? "← BAND"
                    : selectedPart == PartBody
                        ? "← BODY"
                        : selectedVariant >= 0
                            ? "← SHAPE"
                            : selectedPart >= 0
                                ? "← PARTS"
                                : "← BACK");
        SetHeaderBackVisible(true);
        SetTitleBurstsVisible(true);
        ResetDwell();
        RefreshColorSelectionMarks();
    }

    private void ShowVertexSculpt()
    {
        EnsurePainter();
        clayTintActive = false;
        mode = ColorMode.VertexSculpt;
        SetPanelHeight(VertexSculptPanelHeight);
        panel.SetActive(true);
        if (selfiePanel != null)
            selfiePanel.SetActive(false);
        HideExtraGroups();
        HideReviewAndShelf();
        if (mainActionGroup != null)
            mainActionGroup.SetActive(false);
        if (vertexSculptGroup != null)
            vertexSculptGroup.SetActive(true);
        if (historyBar != null)
            historyBar.SetActive(true);
        if (selfieEntryButton != null)
            selfieEntryButton.gameObject.SetActive(false);
        SetSculptGesturesEnabled(false);
        MorphicLoc.Apply(headerText, "VERTEX SCULPT");
        SetHeaderBackLabel("← BACK");
        SetHeaderBackVisible(true);
        SetTitleBurstsVisible(true);
        if (instructionText != null)
            instructionText.gameObject.SetActive(false);
        if (uiRoot != null)
            uiRoot.transform.SetAsLastSibling();
        ApplyCoilSettings();
        RefreshCoilButtons();
        if (patternPainter != null)
        {
            patternPainter.SetClayCoilMode(true);
            patternPainter.SetDrawingLocked(false);
            patternPainter.SetPaintingEnabled(true);
        }
        ResetDwell();
        SetHoveredTarget(null);
        BeginVertexSculptCameraZoom();
    }

    private void DisableVertexSculpt()
    {
        if (patternPainter != null)
        {
            patternPainter.SetPaintingEnabled(false);
            patternPainter.SetClayCoilMode(false);
        }
        if (vertexSculptGroup != null)
            vertexSculptGroup.SetActive(false);
        if (!clayTintActive)
            RestoreVertexSculptCamera();
    }

    private bool IsZoomInspectMode()
    {
        return mode == ColorMode.VertexSculpt ||
               mode == ColorMode.DrawBandTargeting ||
               (mode == ColorMode.SelectColor && clayTintActive);
    }

    private void BeginVertexSculptCameraZoom()
    {
        if (vertexSculptCameraZoomed)
            return;

        Camera cam = FindNamedMainCamera();
        if (cam == null)
            cam = ResolveViewCamera();
        if (cam == null)
            return;

        if (!hasStoredSculptCam)
        {
            storedSculptCamPos = cam.transform.position;
            storedSculptCamRot = cam.transform.rotation;
            storedSculptCamFov = cam.fieldOfView;
            storedSculptCamNear = cam.nearClipPlane;
            hasStoredSculptCam = true;
        }

        MorphicSculptZoomUI.HandOffToVertexSculpt(
            ref hasStoredSculptCam,
            ref storedSculptCamPos,
            ref storedSculptCamRot,
            ref storedSculptCamFov,
            ref storedSculptCamNear);

        GetKerisZoomPose(out Vector3 targetPos, out Quaternion targetRot, out float targetFov);
        HoldSculptCameraPose(targetPos, targetRot, targetFov);
        float targetNear = Mathf.Min(storedSculptCamNear > 0f ? storedSculptCamNear : cam.nearClipPlane, 0.05f);

        vertexSculptCameraZoomed = true;

        bool alreadyThere =
            (cam.transform.position - targetPos).sqrMagnitude < 0.02f &&
            Quaternion.Angle(cam.transform.rotation, targetRot) < 2.5f;
        if (alreadyThere && hasStoredSculptCam)
            ApplyCameraPose(cam, storedSculptCamPos, storedSculptCamRot, storedSculptCamFov, storedSculptCamNear);

        if (isActiveAndEnabled)
            StartSculptCameraMove(cam, targetPos, targetRot, targetFov, targetNear, 0.45f);
        else
            ApplyKerisSculptCameraPose(cam);
    }

    private void CaptureStudioCamera()
    {
        Camera cam = ResolveViewCamera();
        if (cam == null)
            cam = FindNamedMainCamera();
        if (cam == null)
            return;

        storedSculptCamPos = cam.transform.position;
        storedSculptCamRot = cam.transform.rotation;
        storedSculptCamFov = cam.fieldOfView;
        storedSculptCamNear = cam.nearClipPlane;
        hasStoredSculptCam = true;
    }

    private void GetKerisZoomPose(
        out Vector3 pos,
        out Quaternion rot,
        out float fov)
    {
        pos = KerisSculptCamPos;
        rot = Quaternion.Euler(KerisSculptCamEuler);
        fov = storedSculptCamFov > 1f ? storedSculptCamFov : KerisSculptCamFov;
    }

    public void ApplyExtraSculptZoom(float closer01)
    {
        if (!vertexSculptCameraZoomed)
            return;

        Camera cam = FindNamedMainCamera();
        if (cam == null)
            cam = ResolveViewCamera();
        if (cam == null)
            return;

        GetKerisZoomPose(out Vector3 targetPos, out Quaternion targetRot, out float targetFov);
        if (kerisRenderer != null)
            targetPos = MorphicSculptZoomUI.DollyAlongView(
                cam, targetPos, closer01, kerisRenderer.bounds);
        else
            targetPos = MorphicSculptZoomUI.DollyAlongView(cam, targetPos, closer01);

        HoldSculptCameraPose(targetPos, targetRot, targetFov);
        float targetNear = Mathf.Min(storedSculptCamNear > 0f ? storedSculptCamNear : cam.nearClipPlane, 0.05f);
        if (isActiveAndEnabled)
            StartSculptCameraMove(cam, targetPos, targetRot, targetFov, targetNear, 0.28f);
        else
            ApplyCameraPose(cam, targetPos, targetRot, targetFov, targetNear);
    }

    private void HoldSculptCameraPose(Vector3 pos, Quaternion rot, float fov)
    {
        heldSculptCamPos = pos;
        heldSculptCamRot = rot;
        heldSculptCamFov = fov;
        hasHeldSculptCam = true;
    }

    private void ApplyKerisSculptCameraPose(Camera cam)
    {
        Vector3 pos;
        Quaternion rot;
        float fov;
        if (hasHeldSculptCam)
        {
            pos = heldSculptCamPos;
            rot = heldSculptCamRot;
            fov = heldSculptCamFov;
        }
        else
        {
            GetKerisZoomPose(out pos, out rot, out fov);
        }

        ApplyCameraPose(cam, pos, rot, fov, 0.05f);

        Camera named = FindNamedMainCamera();
        if (named != null && named != cam)
            ApplyCameraPose(named, pos, rot, fov, 0.05f);
    }

    private static void ApplyCameraPose(Camera cam, Vector3 pos, Quaternion rot, float fov, float near)
    {
        if (cam == null)
            return;
        cam.transform.SetPositionAndRotation(pos, rot);
        cam.fieldOfView = fov;
        cam.nearClipPlane = Mathf.Min(cam.nearClipPlane, near);
    }

    private static Camera FindNamedMainCamera()
    {
        GameObject mainCam = GameObject.Find("Main Camera");
        return mainCam != null ? mainCam.GetComponent<Camera>() : null;
    }

    [ContextMenu("Capture Current View As Sculpt Camera")]
    private void CaptureCurrentViewAsSculptCamera()
    {
        Camera cam = ResolveViewCamera();
        if (cam == null)
        {
            Debug.LogWarning("MORPHIC: No view camera to capture.", this);
            return;
        }

        useCustomSculptCamera = true;
        sculptCameraPosition = cam.transform.position;
        sculptCameraRotation = cam.transform.eulerAngles;
        sculptCameraFov = cam.fieldOfView;
        Debug.Log(
            "MORPHIC: Keris sculpt camera captured. pos=" + sculptCameraPosition.ToString("F3") +
            "  euler=" + sculptCameraRotation.ToString("F2") +
            "  fov=" + sculptCameraFov.ToString("F1"),
            this);
    }

    [ContextMenu("Apply Sculpt Camera Now")]
    private void ApplySculptCameraNow()
    {
        Camera cam = ResolveViewCamera();
        if (cam == null || !useCustomSculptCamera)
            return;
        cam.transform.SetPositionAndRotation(sculptCameraPosition, Quaternion.Euler(sculptCameraRotation));
        cam.fieldOfView = Mathf.Max(10f, sculptCameraFov);
    }

    private void RestoreVertexSculptCamera()
    {
        MorphicSculptZoomUI.ResumeAfterVertexSculpt();
        if (!hasStoredSculptCam)
            return;

        if (!isActiveAndEnabled)
        {
            SnapRestoreVertexSculptCamera();
            return;
        }

        Camera cam = ResolveViewCamera();
        vertexSculptCameraZoomed = false;
        hasHeldSculptCam = false;
        if (cam == null)
        {
            hasStoredSculptCam = false;
            return;
        }

        StartSculptCameraMove(
            cam,
            storedSculptCamPos,
            storedSculptCamRot,
            storedSculptCamFov,
            storedSculptCamNear,
            0.5f,
            true);
    }

    private void SnapRestoreVertexSculptCamera()
    {
        MorphicSculptZoomUI.ResumeAfterVertexSculpt();
        if (!hasStoredSculptCam)
            return;

        vertexSculptCameraZoomed = false;
        hasHeldSculptCam = false;
        hasStoredSculptCam = false;
        if (sculptCameraMove != null)
        {
            StopCoroutine(sculptCameraMove);
            sculptCameraMove = null;
        }

        Camera cam = ResolveViewCamera();
        if (cam == null)
            return;

        cam.transform.SetPositionAndRotation(storedSculptCamPos, storedSculptCamRot);
        cam.fieldOfView = storedSculptCamFov;
        cam.nearClipPlane = storedSculptCamNear;
    }

    private static void SnapViewToPotteryStudio()
    {
        MainMenuStartTransition transition =
            UnityEngine.Object.FindFirstObjectByType<MainMenuStartTransition>(FindObjectsInactive.Include);
        if (transition != null)
            transition.SnapViewToPotteryStudio();
    }

    private void StartSculptCameraMove(
        Camera cam,
        Vector3 targetPos,
        Quaternion targetRot,
        float targetFov,
        float targetNear,
        float duration,
        bool clearStoredWhenDone = false)
    {
        if (sculptCameraMove != null)
            StopCoroutine(sculptCameraMove);
        sculptCameraMove = StartCoroutine(
            MoveSculptCamera(cam, targetPos, targetRot, targetFov, targetNear, duration, clearStoredWhenDone));
    }

    private IEnumerator MoveSculptCamera(
        Camera cam,
        Vector3 targetPos,
        Quaternion targetRot,
        float targetFov,
        float targetNear,
        float duration,
        bool clearStoredWhenDone)
    {
        Transform camTf = cam.transform;
        Vector3 startPos = camTf.position;
        Quaternion startRot = camTf.rotation;
        float startFov = cam.fieldOfView;
        float startNear = cam.nearClipPlane;
        float elapsed = 0f;
        duration = Mathf.Max(0.05f, duration);

        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            float eased = t * t * (3f - 2f * t);
            camTf.position = Vector3.Lerp(startPos, targetPos, eased);
            camTf.rotation = Quaternion.Slerp(startRot, targetRot, eased);
            cam.fieldOfView = Mathf.Lerp(startFov, targetFov, eased);
            cam.nearClipPlane = Mathf.Lerp(startNear, targetNear, eased);
            yield return null;
        }

        camTf.position = targetPos;
        camTf.rotation = targetRot;
        cam.fieldOfView = targetFov;
        cam.nearClipPlane = targetNear;
        if (!clearStoredWhenDone)
            ApplyKerisSculptCameraPose(cam);
        sculptCameraMove = null;
        if (clearStoredWhenDone)
            hasStoredSculptCam = false;
    }

    private Camera ResolveViewCamera()
    {
        MainMenuStartTransition transition =
            FindFirstObjectByType<MainMenuStartTransition>(FindObjectsInactive.Include);
        if (transition != null && transition.ViewCamera != null)
            return transition.ViewCamera;

        if (Camera.main != null)
            return Camera.main;

        GameObject mainCam = GameObject.Find("Main Camera");
        if (mainCam != null)
        {
            Camera namedMain = mainCam.GetComponent<Camera>();
            if (namedMain != null)
                return namedMain;
        }

        Camera[] cameras = FindObjectsByType<Camera>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        if (cameras == null)
            return null;

        for (int i = 0; i < cameras.Length; i++)
        {
            if (cameras[i] != null && cameras[i].enabled && cameras[i].gameObject.name != "MainMenuViewCamera")
                return cameras[i];
        }

        return cameras.Length > 0 ? cameras[0] : null;
    }

    private void ShowPartSelection()
    {
        DisableVertexSculpt();
        mode = ColorMode.SelectPart;
        SetPanelHeight(MorphicStudioLayout.CardHeight(
            MorphicStudioLayout.PartListHeight(partNames.Length, PillStride), true));
        mainActionGroup.SetActive(false);
        HideExtraGroups();
        if (partGroup != null)
            partGroup.SetActive(true);
        MorphicLoc.BindTree(partGroup.transform);
        if (selfieEntryButton != null)
            selfieEntryButton.gameObject.SetActive(false);
        MorphicLoc.Apply(headerText, "SELECT PART");
        instructionText.gameObject.SetActive(true);
        MorphicLoc.Apply(instructionText, "Choose the body, a carving, or the handle.");
        MorphicText.FitBody(instructionText);
        MorphicStudioLayout.PlacePartList(partGroup.transform, partNames.Length, PillStride);
        Transform leftoverColors = partGroup != null ? partGroup.transform.Find("NextColors") : null;
        if (leftoverColors != null)
            leftoverColors.gameObject.SetActive(false);
        SetHeaderBackLabel("← BACK");
        SetHeaderBackVisible(true);
        SetTitleBurstsVisible(true);
        RefreshPartButtons();
        SetSculptGesturesEnabled(false);
        ResetDwell();
    }

    private void SelectPart(int partIndex)
    {
        selectedPart = Mathf.Clamp(partIndex, 0, partNames.Length - 1);
        selectedVariant = -1;
        selectedVariantLabel = "";
        RefreshPartButtons();

        if (PartNeedsVariant(selectedPart))
        {
            ShowVariantSelection(selectedPart);
            return;
        }

        if (selectedPart == PartBody)
        {
            ShowBodyActions();
            return;
        }

        MorphicLoc.Apply(headerText, "SELECT PART");
        if (selectedPart == PartBody)
            MorphicLoc.Apply(
                instructionText,
                "Point both index fingers for width and height. Right pinch up for sharpness, left pinch up for curve. Open palm to lock.");
        else
            instructionText.text = MorphicLoc.T(partNames[selectedPart]) + MorphicLoc.T(" selected.");
            MorphicLoc.ApplyFont(instructionText);
        SetSculptGesturesEnabled(selectedPart == PartBody);
        if (kerisGestures != null)
            kerisGestures.SetPinchDrivesCarving(false);
        ResetDwell();
        SetHoveredTarget(null);
    }

    private void ContinueToColorSelection()
    {
        if (selectedPart < 0)
            selectedPart = PartBody;

        if (selectedPart == PartHandle)
        {
            TryAdoptVisibleHandle();
            clayTintActive = false;
            ShowColorSelection();
            return;
        }

        if (PartNeedsVariant(selectedPart) && selectedVariant < 0)
        {
            if (mode == ColorMode.SelectVariant)
            {
                MorphicLoc.Apply(instructionText, "Choose a shape first.");
                return;
            }

            ShowVariantSelection(selectedPart);
            return;
        }

        clayTintActive = false;
        ShowColorSelection();
    }

    private void ShowVariantSelection(int partIndex)
    {
        selectedPart = partIndex;
        mode = ColorMode.SelectVariant;
        SetPanelHeight(VariantSelectionPanelHeight);
        mainActionGroup.SetActive(false);
        HideExtraGroups();
        if (variantGroup != null)
            variantGroup.SetActive(true);
        if (selfieEntryButton != null)
            selfieEntryButton.gameObject.SetActive(false);

        string[] labels = GetVariantLabels(partIndex);
        for (int i = 0; i < variantButtonLabels.Length; i++)
        {
            if (variantButtonLabels[i] != null)
                MorphicLoc.Apply(variantButtonLabels[i], labels[i]);
        }

        MorphicLoc.Apply(headerText, partNames[partIndex]);
        SetHeaderBackLabel("← PARTS");
        SetHeaderBackVisible(true);
        if (variantGroup != null)
            MorphicStudioLayout.DockContent(
                variantGroup.GetComponent<RectTransform>(), true, 310f);
        SetTitleBurstsVisible(true);
        instructionText.gameObject.SetActive(true);
        MorphicLoc.Apply(
            instructionText,
            partIndex == PartHandle
                ? "Choose HANDLE 1 or HANDLE 2."
                : "Choose Carving 1 or Carving 2.");
        RefreshVariantButtons();
        ResetDwell();
        SetHoveredTarget(null);
        if (partIndex == PartCarving && selectedVariant < 0)
            SelectVariant(0);
    }

    private void SelectVariant(int variantIndex)
    {
        selectedVariant = Mathf.Clamp(variantIndex, 0, 1);
        selectedVariantLabel = GetVariantLabels(selectedPart)[selectedVariant];
        MorphicLoc.Apply(headerText, partNames[selectedPart]);
        ArmSelectedVariant();
        RefreshVariantButtons();
        SetSculptGesturesEnabled(true);
        if (kerisGestures != null)
            kerisGestures.SetPinchDrivesCarving(selectedPart == PartCarving);
        if (selectedPart == PartHandle)
            MorphicLoc.Apply(instructionText, "Make two fists and pull them down to bring the handle out.");
        else
            MorphicLoc.Apply(instructionText, "Make two V signs and spread them outwards to bring the carving out.");
        ResetDwell();
        SetHoveredTarget(null);
    }

    private void BuildVariantGroup(RectTransform panelRect)
    {
        variantGroup = new GameObject("VariantSelection", typeof(RectTransform));
        variantGroup.transform.SetParent(panelRect, false);
        RectTransform variantRect = variantGroup.GetComponent<RectTransform>();
        DockTop(variantRect, -124f, 310f);

        for (int i = 0; i < variantButtons.Length; i++)
        {
            int variantIndex = i;
            variantButtons[i] = CreateButton(
                "Variant" + i, variantRect, new Vector2(0f, -i * 70f),
                new Vector2(PillWidth, PillHeight), GetVariantLabels(PartCarving)[i], PillPink);
            variantButtons[i].onClick.AddListener(() => SelectVariant(variantIndex));
            variantButtonLabels[i] = variantButtons[i].GetComponentInChildren<TMP_Text>();
            RegisterHitTarget(variantButtons[i], false, true, Color.white);
        }

        Button nextColors = CreateButton("NextColors", variantRect, new Vector2(0f, -152f),
            new Vector2(PillWidth, PillHeight), "CHANGE COLOR", ActionPink);
        nextColors.onClick.AddListener(ContinueToColorSelection);
        RegisterHitTarget(nextColors, false, false, Color.white);

        Button variantDone = CreateButton("Done", variantRect, new Vector2(0f, -224f),
            new Vector2(PillWidth, PillHeight), "DONE", DonePink);
        variantDone.onClick.AddListener(ShowMainMenu);
        RegisterHitTarget(variantDone, false, false, Color.white);
        variantGroup.SetActive(false);
    }

    private void HidePanel()
    {
        DisableVertexSculpt();
        mode = ColorMode.Hidden;
        if (panel != null)
            panel.SetActive(false);
        if (fistRotation != null)
            fistRotation.suppressDriving = false;
    }

    private void NavigateBack()
    {
        if (mode == ColorMode.SelectColor && clayTintActive)
        {
            clayTintActive = false;
            ShowVertexSculpt();
            return;
        }

        if (mode == ColorMode.SelectColor && bandTintActive)
        {
            bandTintActive = false;
            if (returnToBandDrawing)
            {
                returnToBandDrawing = false;
                ResumeBandDrawing();
            }
            else if (colorBackToBands)
                ShowBandSelection();
            else
                ResumeBandDrawing();
            return;
        }

        if (mode == ColorMode.SelectColor && designTintActive)
        {
            ShowDesignSelection();
            return;
        }

        if (mode == ColorMode.SelectColor)
        {
            if (selectedPart == PartBody)
            {
                ShowBodyActions();
                return;
            }

            if (PartNeedsVariant(selectedPart) && selectedVariant >= 0)
            {
                ShowVariantSelection(selectedPart);
                return;
            }

            ShowPartSelection();
            return;
        }

        if (mode == ColorMode.DrawBandTargeting)
        {
            ShowBandSelection();
            return;
        }

        if (mode == ColorMode.SelectDesign || mode == ColorMode.SelectDrawMotif)
        {
            ShowBodyActions();
            return;
        }

        if (mode == ColorMode.SelectBodyActions || mode == ColorMode.SelectVariant)
        {
            ShowPartSelection();
            return;
        }

        if (mode == ColorMode.ConfirmDelete)
        {
            ShowShelf();
            return;
        }

        if (mode == ColorMode.Shelf)
        {
            LeaveShelfDone();
            return;
        }

        if (mode == ColorMode.ConfirmReset)
        {
            HideConfirmReset();
            return;
        }

        if (mode == ColorMode.SelectPart || mode == ColorMode.Review ||
            mode == ColorMode.Selfie || mode == ColorMode.VertexSculpt)
        {
            ShowMainMenu();
            return;
        }

        ShowMainMenu();
    }

    private void OpenClayColor()
    {
        clayTintActive = true;
        ShowColorSelection();
    }

    private void SelectCoilSize(int index)
    {
        selectedCoilSize = Mathf.Clamp(index, 0, coilSizes.Length - 1);
        ApplyCoilSettings();
        RefreshCoilButtons();
    }

    private void SelectCoilHeight(int index)
    {
        selectedCoilHeight = Mathf.Clamp(index, 0, coilHeights.Length - 1);
        ApplyCoilSettings();
        RefreshCoilButtons();
    }

    private void ApplyCoilSettings()
    {
        if (patternPainter == null)
            return;
        float size = coilSizes[selectedCoilSize];
        patternPainter.SetStrokeWidth(size, false);
        patternPainter.SetClayLift(coilHeights[selectedCoilHeight]);
        patternPainter.SetMinPointSpacing(size * 0.25f);
        patternPainter.SetClayColor(committedClayColor, true);
        if (clayColorChip != null)
            clayColorChip.color = committedClayColor;
    }

    // Live clay tint preview: recolours the drawn coils plus the next-stroke
    // material so hovering a swatch gives immediate feedback, mirroring how the
    // keris colour screen previews its swatches.
    private void PreviewClayColor(Color color)
    {
        if (patternPainter != null)
            patternPainter.SetClayColor(color, true);
        if (clayColorChip != null)
            clayColorChip.color = color;
    }

    private void RefreshCoilButtons()
    {
        for (int i = 0; i < coilSizeButtons.Length; i++)
        {
            Image fill = GetButtonFill(coilSizeButtons[i]);
            if (fill != null)
                fill.color = i == selectedCoilSize ? ActionPink : PillPink;
        }

        for (int i = 0; i < coilHeightButtons.Length; i++)
        {
            Image fill = GetButtonFill(coilHeightButtons[i]);
            if (fill != null)
                fill.color = i == selectedCoilHeight ? ActionPink : PillPink;
        }
    }

    private void CommitColor(Color color)
    {
        if (clayTintActive)
        {
            committedClayColor = color;
            ApplyCoilSettings();
            MarkColorSwatch(color);
            return;
        }

        if (bandTintActive)
        {
            committedBandColor = color;
            if (patternPainter != null)
                patternPainter.SetStrokeColor(color, true);
            if (bandColorChip != null)
                bandColorChip.color = color;
            PushHistory();
            MarkColorSwatch(color);
            return;
        }

        if (designTintActive)
        {
            committedDesignTint = color;
            if (appliedDesignTexture != null)
            {
                EnsureDesignOverlay();
                ConfigureDesignSurfaceMaterial(appliedDesignTexture);
                ApplyDesignTint(color);
            }
            PushHistory();
            MarkColorSwatch(color);
            return;
        }

        committedKerisColor = color;
        ApplyKerisColor(color);
        PushHistory();
        MarkColorSwatch(color);
    }

    private const int FlowerMaterialSlot = 0;
    private const int BladeMaterialSlot = 1;

    private void CaptureKerisColor()
    {
        if (kerisRenderer == null)
            return;
        Material[] mats = kerisRenderer.sharedMaterials;
        int slot = mats != null && mats.Length > BladeMaterialSlot ? BladeMaterialSlot : 0;
        Material mat = mats != null && slot < mats.Length ? mats[slot] : kerisRenderer.sharedMaterial;
        committedKerisColor = KerisBaseBrass;
        previewKerisColor = committedKerisColor;
    }

    private void ApplyKerisColor(Color color)
    {
        ApplyKerisColorToSelectedPart(color);
        previewKerisColor = color;
    }

    private void EnsureFlowerMaterial()
    {
        if (kerisRenderer == null)
            return;
        Material[] materials = kerisRenderer.materials;
        if (materials.Length <= FlowerMaterialSlot || materials[FlowerMaterialSlot] == null)
            return;

        Material flower = materials[FlowerMaterialSlot];
        Color white = new Color(0.96f, 0.94f, 0.90f, 1f);
        if (flower.HasProperty("_BaseColor"))
            flower.SetColor("_BaseColor", white);
        if (flower.HasProperty("_Color"))
            flower.SetColor("_Color", white);
        if (flower.HasProperty("_Cull"))
            flower.SetFloat("_Cull", 0f);
        kerisRenderer.materials = materials;
    }

    private void ResetKerisColor()
    {
        selectedPart = -1;
        committedKerisColor = KerisBaseBrass;
        ApplyKerisColor(committedKerisColor);
        ClearWorkingSelfieAndDesign();
        if (patternPainter != null)
            patternPainter.ClearClayCoils();
        if (vertexSculptController != null)
            vertexSculptController.ResetMesh();
        if (kerisGestures != null)
            kerisGestures.RestoreRestState();
        else
            ResetBlendShapes();
        selectedVariant = -1;
        selectedVariantLabel = "";
        RefreshPartButtons();
        RefreshVariantButtons();
        CaptureBaseline();
    }

    private static bool PartNeedsVariant(int partIndex)
    {
        return partIndex == PartCarving || partIndex == PartHandle;
    }

    private static string[] GetVariantLabels(int partIndex)
    {
        return partIndex == PartHandle
            ? new[] { "HANDLE 1", "HANDLE 2" }
            : new[] { "CARVING 1", "CARVING 2" };
    }

    private string ColorHeaderLabel()
    {
        if (selectedPart < 0)
            return "CHANGE COLOR";
        if (string.IsNullOrEmpty(selectedVariantLabel))
            return partNames[selectedPart];
        return partNames[selectedPart] + " · " + selectedVariantLabel;
    }

    private void HideExtraGroups()
    {
        if (partGroup != null)
            partGroup.SetActive(false);
        if (variantGroup != null)
            variantGroup.SetActive(false);
        if (colorGroup != null)
            colorGroup.SetActive(false);
        if (vertexSculptGroup != null)
            vertexSculptGroup.SetActive(false);
        if (bodyActionGroup != null)
            bodyActionGroup.SetActive(false);
        if (designGroup != null)
            designGroup.SetActive(false);
        if (bandGroup != null)
            bandGroup.SetActive(false);
        if (bandTargetingBar != null)
            bandTargetingBar.SetActive(false);
        if (confirmResetGroup != null)
            confirmResetGroup.SetActive(false);
        if (patternPainter != null && mode != ColorMode.VertexSculpt && mode != ColorMode.DrawBandTargeting)
        {
            patternPainter.SetPaintingEnabled(false);
            patternPainter.SetClayCoilMode(false);
        }
    }

    private void RefreshPartButtons()
    {
        for (int i = 0; i < partButtons.Length; i++)
        {
            Image fill = GetButtonFill(partButtons[i]);
            if (fill != null)
                fill.color = i == selectedPart ? ActionPink : PillPink;
        }
    }

    private void RefreshVariantButtons()
    {
        for (int i = 0; i < variantButtons.Length; i++)
        {
            Image fill = GetButtonFill(variantButtons[i]);
            if (fill != null)
                fill.color = i == selectedVariant ? ActionPink : PillPink;
        }
    }

    private bool TryAdoptVisibleHandle()
    {
        ResolveKerisSkin();
        KerisHandleOverlay overlay = kerisSkin != null
            ? kerisSkin.GetComponent<KerisHandleOverlay>()
            : null;
        if (overlay == null)
            return false;

        float handle1 = overlay.handle1Amount;
        float handle2 = overlay.handle2Amount;
        if (handle1 < 0.5f && handle2 < 0.5f)
            return false;

        selectedVariant = handle2 > handle1 + 0.5f
            ? 1
            : handle1 > handle2 + 0.5f
                ? 0
                : overlay.useHandle2 ? 1 : 0;
        selectedVariantLabel = GetVariantLabels(PartHandle)[selectedVariant];
        return true;
    }

    private void ArmSelectedVariant()
    {
        EnsureKerisGestures();
        if (kerisGestures == null)
            return;

        if (selectedPart == PartCarving)
            kerisGestures.ArmCarvingVariant(selectedVariant == 1);
        else if (selectedPart == PartHandle)
            kerisGestures.ArmHandleVariant(selectedVariant == 1);
    }

    private void SetSculptGesturesEnabled(bool enabled)
    {
        if (sculptLockedByTutorial && enabled)
            return;
        EnsureKerisGestures();
        if (kerisGestures == null)
            return;
        if (enabled && kerisGestures.suppressDriving)
            kerisGestures.SynchronizeWeightsFromRenderer();
        kerisGestures.suppressDriving = !enabled;
        kerisGestures.enabled = true;
    }

    private void EnsureKerisGestures()
    {
        ResolveKerisSkin();
        if (kerisSkin == null)
            return;

        if (kerisGestures == null)
            kerisGestures = kerisSkin.GetComponent<KerisGestureController>();
        if (kerisGestures == null)
            kerisGestures = kerisSkin.gameObject.AddComponent<KerisGestureController>();

        if (handRunner != null)
            kerisGestures.BindFrom(handRunner, kerisSkin);
    }

    private void ResetBlendShapes()
    {
        ResolveKerisSkin();
        if (kerisSkin == null || kerisSkin.sharedMesh == null)
            return;
        int count = kerisSkin.sharedMesh.blendShapeCount;
        for (int i = 0; i < count; i++)
            kerisSkin.SetBlendShapeWeight(i, 0f);
    }

    private int FindVariantShape(int partIndex, int variantIndex)
    {
        if (partIndex == PartHandle)
            return FindShapeIndex(variantIndex == 0 ? "Handle 1" : "Handle 2");

        if (variantIndex == 0)
            return -1;
        return FindShapeIndexExactCarving2();
    }

    private int FindShapeIndex(params string[] names)
    {
        ResolveKerisSkin();
        Mesh mesh = kerisSkin != null ? kerisSkin.sharedMesh : null;
        if (mesh == null)
            return -1;

        for (int n = 0; n < names.Length; n++)
        {
            for (int i = 0; i < mesh.blendShapeCount; i++)
            {
                if (string.Equals(mesh.GetBlendShapeName(i), names[n], StringComparison.OrdinalIgnoreCase))
                    return i;
            }
        }

        return -1;
    }

    private int FindShapeIndexExactCarving2()
    {
        int exact = FindShapeIndex("Carving 2");
        if (exact >= 0)
            return exact;

        ResolveKerisSkin();
        Mesh mesh = kerisSkin != null ? kerisSkin.sharedMesh : null;
        if (mesh == null)
            return -1;
        for (int i = 0; i < mesh.blendShapeCount; i++)
        {
            string name = mesh.GetBlendShapeName(i);
            if (name.IndexOf("Carving 2", StringComparison.OrdinalIgnoreCase) >= 0 &&
                name.IndexOf(".001", StringComparison.OrdinalIgnoreCase) < 0)
                return i;
        }

        return -1;
    }

    private void LogKerisShapeKeys()
    {
        ResolveKerisSkin();
        if (kerisSkin == null || kerisSkin.sharedMesh == null)
        {
            Debug.LogWarning("MORPHIC: Keris mesh has no SkinnedMeshRenderer for shape keys.");
            return;
        }

        Mesh mesh = kerisSkin.sharedMesh;
        var names = new List<string>(mesh.blendShapeCount);
        for (int i = 0; i < mesh.blendShapeCount; i++)
            names.Add(mesh.GetBlendShapeName(i));
        Debug.Log("MORPHIC: Keris shape keys (" + names.Count + "): " + string.Join(", ", names));
    }

    private void ResolveKerisSkin()
    {
        if (kerisSkin != null)
            return;
        if (kerisRenderer is SkinnedMeshRenderer skin)
            kerisSkin = skin;
        else if (kerisRenderer != null)
            kerisSkin = kerisRenderer.GetComponent<SkinnedMeshRenderer>();
    }

    private void CaptureBaseline()
    {
        if (kerisGestures != null)
            kerisGestures.CaptureRestState();
        lastDeformationLocked = kerisGestures != null && kerisGestures.IsDeformationLocked;
        history.Clear();
        redoHistory.Clear();
        history.Add(CaptureSnapshot());
        ResetTimedUndo();
    }

    private void PushHistory()
    {
        KerisSnapshot snapshot = CaptureSnapshot();
        if (history.Count > 0 && KerisSnapshotsMatch(history[history.Count - 1], snapshot))
            return;

        redoHistory.Clear();
        history.Add(snapshot);
        while (history.Count > MaxHistory)
            history.RemoveAt(0);
        NoteTimedUndo(TimedUndo.History);
    }

    private void PollSculptHistory()
    {
        if (kerisGestures == null)
            return;
        bool locked = kerisGestures.IsDeformationLocked;
        if (locked && !lastDeformationLocked)
            PushHistory();
        lastDeformationLocked = locked;
    }

    private KerisSnapshot CaptureSnapshot()
    {
        var snapshot = new KerisSnapshot();
        ResolveKerisSkin();
        if (kerisSkin != null && kerisSkin.sharedMesh != null)
        {
            Mesh mesh = kerisSkin.sharedMesh;
            snapshot.BlendWeights = new float[mesh.blendShapeCount];
            for (int i = 0; i < snapshot.BlendWeights.Length; i++)
                snapshot.BlendWeights[i] = kerisSkin.GetBlendShapeWeight(i);
        }
        snapshot.PartColors = (Color[])savedPartColors.Clone();
        snapshot.CommittedColor = committedKerisColor;
        snapshot.Stamp = appliedDesignTexture;
        snapshot.StampTint = committedDesignTint;
        snapshot.StampVisible = appliedDesignTexture != null &&
            designSurfaceRenderer != null &&
            designSurfaceRenderer.enabled;
        return snapshot;
    }

    private void ApplySnapshot(KerisSnapshot snapshot)
    {
        if (snapshot == null)
            return;
        ResolveKerisSkin();
        if (kerisSkin != null && snapshot.BlendWeights != null && kerisSkin.sharedMesh != null)
        {
            int count = Mathf.Min(snapshot.BlendWeights.Length, kerisSkin.sharedMesh.blendShapeCount);
            for (int i = 0; i < count; i++)
                kerisSkin.SetBlendShapeWeight(i, snapshot.BlendWeights[i]);
        }
        if (kerisGestures != null)
            kerisGestures.SynchronizeWeightsFromRenderer();
        if (snapshot.PartColors != null && snapshot.PartColors.Length == savedPartColors.Length)
            savedPartColors = (Color[])snapshot.PartColors.Clone();
        committedKerisColor = snapshot.CommittedColor;
        ApplySavedPartColors();
        RestoreStampFromSnapshot(snapshot);
    }

    private void ApplySavedPartColors()
    {
        int previousPart = selectedPart;
        int previousVariant = selectedVariant;
        selectedPart = PartBody;
        ApplyKerisColorToSelectedPart(savedPartColors[PartBody]);
        selectedPart = PartCarving;
        int restoreVariant = selectedVariant;
        selectedVariant = 0;
        ApplyKerisColorToSelectedPart(savedPartColors[PartCarving]);
        selectedVariant = 1;
        ApplyKerisColorToSelectedPart(savedPartColors[PartCarving]);
        selectedVariant = restoreVariant;
        selectedPart = PartHandle;
        ApplyKerisColorToSelectedPart(savedPartColors[PartHandle]);
        selectedPart = previousPart;
        selectedVariant = previousVariant;
        previewKerisColor = committedKerisColor;
    }

    private static bool KerisSnapshotsMatch(KerisSnapshot a, KerisSnapshot b)
    {
        if (a == null || b == null)
            return false;
        if (a.BlendWeights != null && b.BlendWeights != null &&
            a.BlendWeights.Length == b.BlendWeights.Length)
        {
            for (int i = 0; i < a.BlendWeights.Length; i++)
            {
                if (Mathf.Abs(a.BlendWeights[i] - b.BlendWeights[i]) > 0.15f)
                    return false;
            }
        }
        else if (a.BlendWeights != b.BlendWeights)
            return false;

        if (ColorDistanceSq(a.CommittedColor, b.CommittedColor) > 0.0001f)
            return false;
        if (a.Stamp != b.Stamp || a.StampVisible != b.StampVisible)
            return false;
        if (ColorDistanceSq(a.StampTint, b.StampTint) > 0.0001f)
            return false;
        if (a.PartColors == null || b.PartColors == null ||
            a.PartColors.Length != b.PartColors.Length)
            return a.PartColors == b.PartColors;
        for (int i = 0; i < a.PartColors.Length; i++)
        {
            if (ColorDistanceSq(a.PartColors[i], b.PartColors[i]) > 0.0001f)
                return false;
        }
        return true;
    }

    private static float ColorDistanceSq(Color a, Color b)
    {
        float dr = a.r - b.r;
        float dg = a.g - b.g;
        float db = a.b - b.b;
        return dr * dr + dg * dg + db * db;
    }

    private void NoteTimedUndo(TimedUndo kind)
    {
        timedUndo.Add(kind);
        timedRedo.Clear();
    }

    private void ResetTimedUndo()
    {
        timedUndo.Clear();
        timedRedo.Clear();
        trackedClayCount = patternPainter != null ? patternPainter.ClayStrokeCount : 0;
        trackedBandCount = patternPainter != null ? patternPainter.StrokeCount : 0;
        trackedSculptCount = vertexSculptController != null ? vertexSculptController.UndoDepth : 0;
    }

    private void TrackNewPainterActions()
    {
        int clay = patternPainter != null ? patternPainter.ClayStrokeCount : 0;
        int band = patternPainter != null ? patternPainter.StrokeCount : 0;
        int sculpt = vertexSculptController != null ? vertexSculptController.UndoDepth : 0;
        while (trackedClayCount < clay)
        {
            NoteTimedUndo(TimedUndo.Clay);
            trackedClayCount++;
        }
        while (trackedBandCount < band)
        {
            NoteTimedUndo(TimedUndo.Band);
            trackedBandCount++;
        }
        while (trackedSculptCount < sculpt)
        {
            NoteTimedUndo(TimedUndo.Sculpt);
            trackedSculptCount++;
        }
        if (trackedClayCount > clay)
            trackedClayCount = clay;
        if (trackedBandCount > band)
            trackedBandCount = band;
        if (trackedSculptCount > sculpt)
            trackedSculptCount = sculpt;
    }

    private bool UndoTimedAction()
    {
        if (timedUndo.Count == 0)
            return false;
        TimedUndo kind = timedUndo[timedUndo.Count - 1];
        timedUndo.RemoveAt(timedUndo.Count - 1);
        if (!PerformTimedUndo(kind))
            return false;
        timedRedo.Add(kind);
        return true;
    }

    private bool RedoTimedAction()
    {
        if (timedRedo.Count == 0)
            return false;
        TimedUndo kind = timedRedo[timedRedo.Count - 1];
        timedRedo.RemoveAt(timedRedo.Count - 1);
        if (!PerformTimedRedo(kind))
            return false;
        timedUndo.Add(kind);
        return true;
    }

    private bool PerformTimedUndo(TimedUndo kind)
    {
        switch (kind)
        {
            case TimedUndo.Clay:
                if (patternPainter == null || patternPainter.ClayStrokeCount <= 0)
                    return false;
                patternPainter.UndoLastClayStroke();
                trackedClayCount = patternPainter.ClayStrokeCount;
                if (instructionText != null)
                {
                    instructionText.gameObject.SetActive(true);
                    MorphicLoc.Apply(instructionText, "Last clay coil undone.");
                }
                return true;
            case TimedUndo.Sculpt:
                if (vertexSculptController == null || !vertexSculptController.HasUndo)
                    return false;
                vertexSculptController.UndoLastStroke();
                trackedSculptCount = vertexSculptController.UndoDepth;
                if (instructionText != null)
                {
                    instructionText.gameObject.SetActive(true);
                    MorphicLoc.Apply(instructionText, "Last sculpt stroke undone.");
                }
                return true;
            case TimedUndo.Band:
                if (patternPainter == null || patternPainter.StrokeCount <= 0)
                    return false;
                patternPainter.UndoLastStroke();
                trackedBandCount = patternPainter.StrokeCount;
                if (instructionText != null)
                {
                    instructionText.gameObject.SetActive(true);
                    MorphicLoc.Apply(instructionText, "Last drawing stroke undone.");
                }
                return true;
            case TimedUndo.Selfie:
                return TryUndoSelfie();
            case TimedUndo.History:
                if (history.Count < 2)
                    return false;
                redoHistory.Add(history[history.Count - 1]);
                history.RemoveAt(history.Count - 1);
                ApplySnapshot(history[history.Count - 1]);
                if (instructionText != null)
                {
                    instructionText.gameObject.SetActive(true);
                    MorphicLoc.Apply(instructionText, "Last change undone.");
                }
                return true;
            default:
                return false;
        }
    }

    private bool PerformTimedRedo(TimedUndo kind)
    {
        switch (kind)
        {
            case TimedUndo.Clay:
                if (patternPainter == null || patternPainter.RedoClayCount <= 0)
                    return false;
                patternPainter.RedoLastClayStroke();
                trackedClayCount = patternPainter.ClayStrokeCount;
                if (instructionText != null)
                {
                    instructionText.gameObject.SetActive(true);
                    MorphicLoc.Apply(instructionText, "Last clay coil redone.");
                }
                return true;
            case TimedUndo.Sculpt:
                if (vertexSculptController == null || !vertexSculptController.HasRedo)
                    return false;
                vertexSculptController.RedoLastStroke();
                trackedSculptCount = vertexSculptController.UndoDepth;
                if (instructionText != null)
                {
                    instructionText.gameObject.SetActive(true);
                    MorphicLoc.Apply(instructionText, "Last sculpt stroke redone.");
                }
                return true;
            case TimedUndo.Band:
                if (patternPainter == null || patternPainter.RedoStrokeCount <= 0)
                    return false;
                patternPainter.RedoLastStroke();
                trackedBandCount = patternPainter.StrokeCount;
                if (instructionText != null)
                {
                    instructionText.gameObject.SetActive(true);
                    MorphicLoc.Apply(instructionText, "Last drawing stroke redone.");
                }
                return true;
            case TimedUndo.Selfie:
                return TryRedoSelfie();
            case TimedUndo.History:
                if (redoHistory.Count == 0)
                    return false;
                KerisSnapshot snapshot = redoHistory[redoHistory.Count - 1];
                redoHistory.RemoveAt(redoHistory.Count - 1);
                history.Add(snapshot);
                ApplySnapshot(snapshot);
                if (instructionText != null)
                {
                    instructionText.gameObject.SetActive(true);
                    MorphicLoc.Apply(instructionText, "Last change redone.");
                }
                return true;
            default:
                return false;
        }
    }

    private void UndoClay()
    {
        if (UndoTimedAction())
            return;
        if (patternPainter != null && patternPainter.ClayStrokeCount > 0)
        {
            patternPainter.UndoLastClayStroke();
            if (instructionText != null)
            {
                instructionText.gameObject.SetActive(true);
                MorphicLoc.Apply(instructionText, "Last clay coil undone.");
            }
            return;
        }

        if (vertexSculptController != null && vertexSculptController.HasUndo &&
            vertexSculptController.UndoLastStroke())
        {
            if (instructionText != null)
            {
                instructionText.gameObject.SetActive(true);
                MorphicLoc.Apply(instructionText, "Last sculpt stroke undone.");
            }
            return;
        }

        if (patternPainter != null && patternPainter.StrokeCount > 0)
        {
            patternPainter.UndoLastStroke();
            if (instructionText != null)
            {
                instructionText.gameObject.SetActive(true);
                MorphicLoc.Apply(instructionText, "Last drawing stroke undone.");
            }
            return;
        }

        if (TryUndoSelfie())
            return;

        if (history.Count >= 2)
        {
            redoHistory.Add(history[history.Count - 1]);
            history.RemoveAt(history.Count - 1);
            ApplySnapshot(history[history.Count - 1]);
            if (instructionText != null)
            {
                instructionText.gameObject.SetActive(true);
                MorphicLoc.Apply(instructionText, "Last change undone.");
            }
            return;
        }

        if (instructionText != null)
        {
            instructionText.gameObject.SetActive(true);
            MorphicLoc.Apply(instructionText, "Nothing to undo.");
        }
    }

    private void RedoClay()
    {
        if (RedoTimedAction())
            return;
        if (patternPainter != null && patternPainter.RedoClayCount > 0)
        {
            patternPainter.RedoLastClayStroke();
            if (instructionText != null)
            {
                instructionText.gameObject.SetActive(true);
                MorphicLoc.Apply(instructionText, "Last clay coil redone.");
            }
            return;
        }

        if (vertexSculptController != null && vertexSculptController.HasRedo &&
            vertexSculptController.RedoLastStroke())
        {
            if (instructionText != null)
            {
                instructionText.gameObject.SetActive(true);
                MorphicLoc.Apply(instructionText, "Last sculpt stroke redone.");
            }
            return;
        }

        if (patternPainter != null && patternPainter.RedoStrokeCount > 0)
        {
            patternPainter.RedoLastStroke();
            if (instructionText != null)
            {
                instructionText.gameObject.SetActive(true);
                MorphicLoc.Apply(instructionText, "Last drawing stroke redone.");
            }
            return;
        }

        if (TryRedoSelfie())
            return;

        if (redoHistory.Count > 0)
        {
            KerisSnapshot snapshot = redoHistory[redoHistory.Count - 1];
            redoHistory.RemoveAt(redoHistory.Count - 1);
            history.Add(snapshot);
            ApplySnapshot(snapshot);
            if (instructionText != null)
            {
                instructionText.gameObject.SetActive(true);
                MorphicLoc.Apply(instructionText, "Last change redone.");
            }
        }
    }

    private void EnsurePainter()
    {
        ResolveKerisSkin();
        if (patternPainter != null || kerisSkin == null)
            return;
        SkinnedMeshRenderer skin = kerisSkin;
        patternPainter = skin.GetComponent<TeapotSurfacePatternPainter>();
        if (patternPainter == null)
            patternPainter = skin.gameObject.AddComponent<TeapotSurfacePatternPainter>();
        patternPainter.Initialize(handRunner, skin);
        patternPainter.SetStabilization(0.55f);
        patternPainter.SetPullPixels(12f);
    }

    private void EnsureSkinnedForPainter(GameObject kerisRoot)
    {
        SkinnedMeshRenderer skin = kerisRoot.GetComponentInChildren<SkinnedMeshRenderer>(true);
        if (skin == null)
        {
            MeshFilter filter = kerisRoot.GetComponentInChildren<MeshFilter>(true);
            MeshRenderer meshRenderer = kerisRoot.GetComponentInChildren<MeshRenderer>(true);
            if (filter == null || filter.sharedMesh == null)
                return;

            skin = filter.gameObject.AddComponent<SkinnedMeshRenderer>();
            skin.sharedMesh = filter.sharedMesh;
            if (meshRenderer != null)
            {
                skin.sharedMaterials = meshRenderer.sharedMaterials;
                meshRenderer.enabled = false;
            }
        }

        KerisGestureController.PrepareRenderer(skin);
        kerisSkin = skin;
        kerisRenderer = skin;
        EnsureFlowerMaterial();
        committedKerisColor = KerisBaseBrass;
        ApplyKerisColor(committedKerisColor);
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
        if (clayTintActive)
        {
            // Clay tinting previews against the drawn coils rather than the keris
            // materials, so hovering a swatch recolours the clay live.
            if (hoveredTarget != null && hoveredTarget.IsColor && !hoveredTarget.IsFreeColorPalette)
                PreviewClayColor(hoveredTarget.PreviewColor);
            else if (hoveredTarget == null || !hoveredTarget.IsColor)
                PreviewClayColor(committedClayColor);
        }
        else if (designTintActive)
        {
            if (hoveredTarget != null && hoveredTarget.IsColor && !hoveredTarget.IsFreeColorPalette)
                ApplyDesignTint(hoveredTarget.PreviewColor);
            else
                ApplyDesignTint(committedDesignTint);
        }
        else if (bandTintActive)
        {
            if (hoveredTarget != null && hoveredTarget.IsColor && !hoveredTarget.IsFreeColorPalette)
            {
                if (patternPainter != null)
                    patternPainter.SetStrokeColor(hoveredTarget.PreviewColor, true);
            }
            else if (patternPainter != null)
            {
                patternPainter.SetStrokeColor(committedBandColor, true);
            }
        }
        else if (hoveredTarget != null && hoveredTarget.IsColor)
            ApplyKerisColor(hoveredTarget.PreviewColor);
        else if (hoveredTarget == null || !hoveredTarget.IsColor)
            ApplyKerisColor(committedKerisColor);
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
        if (target.IsColor)
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

    private Vector2 MoveCursor(Vector2 screenPosition)
    {
        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
                canvasRect, screenPosition, canvasCamera, out Vector2 localPoint))
            return screenPosition;
        float factor = 1f - Mathf.Exp(-cursorSmoothing * Time.unscaledDeltaTime);
        cursorRect.anchoredPosition = Vector2.Lerp(cursorRect.anchoredPosition, localPoint, factor);
        cursorRect.SetAsLastSibling();
        return RectTransformUtility.WorldToScreenPoint(canvasCamera, cursorRect.position);
    }

    private void SetCursorVisible(bool visible)
    {
        if (cursorRect == null)
            return;
        bool show = visible && mode != ColorMode.Hidden;
        if (cursorRect.gameObject.activeSelf != show)
            cursorRect.gameObject.SetActive(show);
    }

    private void RegisterHitTarget(Button button, bool isColor, bool isPersistentChoice, Color previewColor, bool isFreeColorPalette = false)
    {
        Outline outline = button.gameObject.GetComponent<Outline>();
        if (outline == null)
            outline = button.gameObject.AddComponent<Outline>();
        outline.effectColor = LanguagePageBuilder.Green;
        outline.effectDistance = new Vector2(4f, -4f);
        outline.enabled = false;
        Image fill = GetButtonFill(button);
        Transform checkTransform = button.transform.Find("SelectionCheck");
        hitTargets.Add(new HitTarget
        {
            Button = button,
            Rect = button.GetComponent<RectTransform>(),
            Outline = outline,
            IsColor = isColor,
            IsPersistentChoice = isPersistentChoice,
            IsFreeColorPalette = isFreeColorPalette,
            PreviewColor = previewColor,
            BaseColor = fill != null ? fill.color : Color.white,
            SelectionCheck = checkTransform != null
                ? checkTransform.GetComponent<RectTransform>()
                : null,
            DwellEdges = CreateDwellFrame(button.transform)
        });
    }

    private void SetSelectedChoice(HitTarget target)
    {
        if (target == null || !target.IsColor)
            return;
        ClearSelectedColorVisual();
        selectedColorTarget = target;
        if (selectedColorTarget.SelectionCheck != null)
            selectedColorTarget.SelectionCheck.gameObject.SetActive(true);
        if (freeColorSelectionCheck != null && !target.IsFreeColorPalette)
            freeColorSelectionCheck.gameObject.SetActive(false);
    }

    private void ClearSelectedColorVisual()
    {
        if (selectedColorTarget != null && selectedColorTarget.SelectionCheck != null)
            selectedColorTarget.SelectionCheck.gameObject.SetActive(false);
        selectedColorTarget = null;
    }

    private void MarkColorSwatch(Color color)
    {
        HitTarget match = FindColorSwatch(color);
        if (match != null)
            SetSelectedChoice(match);
        else
            ClearSelectedColorVisual();
    }

    private void RefreshColorSelectionMarks()
    {
        if (freeColorSelectionCheck != null)
            freeColorSelectionCheck.gameObject.SetActive(false);
        Color current = clayTintActive
            ? committedClayColor
            : designTintActive
                ? committedDesignTint
                : bandTintActive
                    ? committedBandColor
                    : committedKerisColor;
        MarkColorSwatch(current);
    }

    private HitTarget FindColorSwatch(Color color)
    {
        for (int i = 0; i < hitTargets.Count; i++)
        {
            HitTarget target = hitTargets[i];
            if (target == null || !target.IsColor || target.IsFreeColorPalette)
                continue;
            if (ColorsMatch(target.PreviewColor, color))
                return target;
        }
        return null;
    }

    private static bool ColorsMatch(Color a, Color b)
    {
        return Mathf.Abs(a.r - b.r) < 0.02f &&
               Mathf.Abs(a.g - b.g) < 0.02f &&
               Mathf.Abs(a.b - b.b) < 0.02f;
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
        Button button = MorphicBackButton.CreateHeader(parent, NavigateBack);
        RegisterHitTarget(button, false, false, Color.white);
        return button;
    }

    private void SetHeaderBackVisible(bool visible)
    {
        if (headerBackButton != null)
        {
            MorphicBackButton.RestyleExisting(headerBackButton);
            headerBackButton.gameObject.SetActive(visible);
        }

        MorphicStudioLayout.PlaceHeader(headerText, instructionText, visible);
    }

    private void SetHeaderBackLabel(string label)
    {
        if (headerBackText != null)
            MorphicLoc.Bind(headerBackText, label);
    }

    private void SetTitleBurstsVisible(bool visible)
    {
        if (titleBurstRoot != null)
            titleBurstRoot.SetActive(visible);
    }

    private void SetPanelHeight(float height, bool includeHistoryBar = true)
    {
        if (panel == null)
            return;
        float extra = includeHistoryBar ? HistoryBarHeight : 16f;
        panel.GetComponent<RectTransform>().sizeDelta =
            new Vector2(CustomizePanelWidth, height + extra);
        FitCreamToHistory(includeHistoryBar);
    }

    private void FitCreamToHistory(bool historyVisible)
    {
        if (panel == null)
            return;
        Transform fill = panel.transform.Find("Fill");
        if (fill == null)
            return;
        LanguagePageBuilder.Stretch(
            fill as RectTransform,
            12f,
            historyVisible ? HistoryBarHeight : 16f,
            12f,
            12f);
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
        button.colors = LanguagePageBuilder.ButtonColors();
        if (!string.IsNullOrEmpty(label))
        {
            TMP_Text text = CreateText(
                "Label", fill.rectTransform, Vector2.zero, size - new Vector2(16f, 10f),
                label, isDone || isChangeColor ? 26f : 24f,
                TextAlignmentOptions.Center, FontStyles.Bold);
            Stretch(text.rectTransform);
            MorphicText.Style(text);
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
        MorphicLoc.Bind(text, value);
        text.fontSize = MorphicText.Size(fontSize);
        text.alignment = alignment;
        text.raycastTarget = false;
        if (objectName == "Label")
            MorphicText.FitButton(text);
        else if (objectName == "Instruction")
            MorphicText.FitBody(text);
        else
            MorphicText.FitTitle(text);
        text.transform.SetAsLastSibling();
        return text;
    }

    private static void StyleMorphicCard(RectTransform panelRect)
    {
        Image frame = LanguagePageBuilder.CreateRoundedImage("Frame", panelRect, 32, LanguagePageBuilder.Green);
        LanguagePageBuilder.Stretch(frame.rectTransform);
        frame.raycastTarget = false;
        Image fill = LanguagePageBuilder.CreateRoundedImage("Fill", panelRect, 24, CardCream, false);
        LanguagePageBuilder.Stretch(fill.rectTransform, 12f, HistoryBarHeight, 12f, 12f);
        LanguagePageBuilder.RaiseCream(fill, 24);
        fill.raycastTarget = false;
    }

    private static void StyleMorphicTitle(TMP_Text text)
    {
        if (text == null)
            return;
        MorphicText.Style(text);
    }

    private void BuildTitleBursts(RectTransform parent)
    {
        titleBurstRoot = new GameObject("TitleBursts", typeof(RectTransform));
        titleBurstRoot.transform.SetParent(parent, false);
        Stretch(titleBurstRoot.GetComponent<RectTransform>());
        Vector2[] positions =
        {
            new Vector2(-215f, -28f), new Vector2(215f, -28f),
            new Vector2(-235f, -50f), new Vector2(235f, -50f)
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
        rect.sizeDelta = new Vector2(-40f, height);
    }

    private static void CenterInBar(Button button, float x)
    {
        RectTransform rect = button.GetComponent<RectTransform>();
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = new Vector2(x, 0f);
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
