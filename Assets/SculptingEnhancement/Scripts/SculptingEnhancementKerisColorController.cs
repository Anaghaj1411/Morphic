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
        public Image[] DwellEdges;
    }

    [SerializeField] private SculptHandLandmarkerRunner handRunner;
    [SerializeField] private Renderer kerisRenderer;
    [SerializeField] private SkinnedMeshRenderer kerisSkin;
    [SerializeField] private TeapotSurfacePatternPainter patternPainter;
    [SerializeField, Range(0.25f, 2.5f)] private float selectionDwellTime = 1.5f;
    [SerializeField, Min(1f)] private float cursorSmoothing = 24f;
    [SerializeField, Range(1f, 2.5f)] private float cameraToScreenRangeMultiplier = 1.8f;
    [SerializeField] private int selfieWidth = 640;
    [SerializeField] private int selfieHeight = 480;
    [SerializeField] private int selfieFPS = 30;
    [Header("Vertex Sculpt Camera")]
    [SerializeField] private Vector3 sculptCameraPosition = new Vector3(-26.3f, 2.478407f, -6.239563f);
    [SerializeField] private Vector3 sculptCameraRotation = new Vector3(1.35f, -265.187f, 89.806f);
    [SerializeField] private float sculptCameraFov = 35f;

    private static readonly Color CardCream = new Color32(255, 239, 214, 255);
    private static readonly Color PillPink = LanguagePageBuilder.Pink;
    private static readonly Color ActionPink = new Color32(242, 154, 214, 255);
    private static readonly Color DonePink = new Color32(236, 64, 168, 255);
    private static readonly Color DarkTextGreen = Color.black;
    private const float CustomizePanelWidth = 540f;
    private const float PillWidth = 490f;
    private const float PillHeight = 60f;
    private const float MainMenuPanelHeight = 500f;
    private const float ColorSelectionPanelHeight = 680f;
    private const float VertexSculptPanelHeight = 600f;
    private const float HistoryBarHeight = 78f;
    private const float PartSelectionPanelHeight = 560f;
    private const float VariantSelectionPanelHeight = 460f;
    private const float ReviewPanelHeight = 420f;
    private const float ShelfPanelHeight = 380f;
    private const float SelfiePanelHeight = 620f;
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
    private readonly float[] coilHeights = { 1.1f, 1.4f, 2.0f, 3.2f };
    private readonly string[] coilHeightLabels = { "SOFT", "MED", "STRONG", "MAX" };
    private readonly Button[] coilSizeButtons = new Button[4];
    private readonly Button[] coilHeightButtons = new Button[4];
    private readonly List<HitTarget> hitTargets = new List<HitTarget>();
    private bool vertexSculptCameraZoomed;
    private Vector3 storedSculptCamPos;
    private Quaternion storedSculptCamRot;
    private float storedSculptCamFov;
    private float storedSculptCamNear;
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
            DisableVertexSculpt();
            if (uiRoot != null)
                uiRoot.SetActive(false);
            mode = ColorMode.Hidden;
            SetSculptGesturesEnabled(false);
            if (fistRotation != null)
                fistRotation.suppressDriving = sculptLockedByTutorial;
            return;
        }

        if (uiRoot == null)
            BuildInterface();
        if (uiRoot != null)
            uiRoot.SetActive(true);
        ShowMainMenu();
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
        DisableVertexSculpt();
        if (uiRoot != null)
            uiRoot.SetActive(false);
    }

    private void LateUpdate()
    {
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
            MoveCursor(screenPosition);
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
                if (instructionText != null)
                {
                    if (patternPainter.IsLocked)
                        instructionText.text = "CLAY LOCKED. Open palm again to unlock.";
                    else if (patternPainter.IsDrawing)
                        instructionText.text = "LAYING CLAY... Trace your index finger.";
                    else if (patternPainter.IsOverSurface)
                        instructionText.text = "Point index finger and draw clay on the keris.";
                    else
                        instructionText.text = "Aim at the keris. Point index finger to draw clay.";
                }
            }
        }

        bool overUi = nextHovered != null;
        bool allowGestures = !sculptLockedByTutorial &&
                             (mode == ColorMode.MainMenu ||
                              mode == ColorMode.SelectVariant ||
                              (mode == ColorMode.SelectPart && selectedPart == PartBody));
        SetSculptGesturesEnabled(allowGestures && !overUi);
        if (fistRotation != null)
            fistRotation.suppressDriving = sculptLockedByTutorial || !allowGestures || overUi ||
                (kerisGestures != null && kerisGestures.IsDrivingSculpt);
        if (instructionText != null && instructionText.gameObject.activeSelf &&
            kerisGestures != null && kerisGestures.IsDeformationLocked &&
            mode != ColorMode.VertexSculpt && mode != ColorMode.SelectColor)
            instructionText.text = "LOCKED. Point both index fingers to unlock.";

        SetHoveredTarget(nextHovered);
        UpdateDwellSelection(nextHovered);
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
        instructionText = CreateText("Instruction", panelRect, new Vector2(10f, -78f),
            new Vector2(520f, 38f), "Choose a color or sculpt clay.", 19.5f,
            TextAlignmentOptions.Center, FontStyles.Bold);
        instructionText.color = DarkTextGreen;

        mainActionGroup = new GameObject("MainActions", typeof(RectTransform));
        mainActionGroup.transform.SetParent(panelRect, false);
        RectTransform mainRect = mainActionGroup.GetComponent<RectTransform>();
        DockTop(mainRect, -196f, 250f);

        selfieEntryButton = CreateButton("SelfieEntry", panelRect, new Vector2(0f, -118f),
            new Vector2(PillWidth, PillHeight), "SELFIE", new Color32(155, 92, 130, 255));
        selfieEntryButton.onClick.AddListener(ShowSelfieCapture);
        RegisterHitTarget(selfieEntryButton, false, false, Color.white);

        Button selectPart = CreateButton("SelectPart", mainRect, Vector2.zero,
            new Vector2(PillWidth, PillHeight), "SELECT PART", PillPink);
        selectPart.onClick.AddListener(ShowPartSelection);
        RegisterHitTarget(selectPart, false, false, Color.white);

        Button changeColor = CreateButton("ChangeColor", mainRect, new Vector2(0f, -72f),
            new Vector2(PillWidth, PillHeight), "CHANGE COLOR", ActionPink);
        changeColor.onClick.AddListener(ShowWholeKerisColorSelection);
        RegisterHitTarget(changeColor, false, false, Color.white);

        Button vertexSculpt = CreateButton("VertexSculpt", mainRect, new Vector2(0f, -144f),
            new Vector2(PillWidth, PillHeight), "VERTEX SCULPT", PillPink);
        TMP_Text vText = vertexSculpt.GetComponentInChildren<TMP_Text>();
        if (vText != null)
        {
            vText.fontSize = 22f;
            vText.fontStyle = FontStyles.Bold;
            vText.color = DarkTextGreen;
        }
        vertexSculpt.onClick.AddListener(ShowVertexSculpt);
        RegisterHitTarget(vertexSculpt, false, false, Color.white);

        Button finish = CreateButton("Finish", mainRect, new Vector2(0f, -216f),
            new Vector2(PillWidth, PillHeight), "FINISH", DonePink);
        finish.onClick.AddListener(ShowReview);
        RegisterHitTarget(finish, false, false, Color.white);

        partGroup = new GameObject("PartSelection", typeof(RectTransform));
        partGroup.transform.SetParent(panelRect, false);
        RectTransform partRect = partGroup.GetComponent<RectTransform>();
        DockTop(partRect, -124f, 430f);

        for (int i = 0; i < partNames.Length; i++)
        {
            int partIndex = i;
            partButtons[i] = CreateButton(
                "Part" + i, partRect, new Vector2(0f, -i * 70f),
                new Vector2(PillWidth, PillHeight), partNames[i], PillPink);
            partButtons[i].onClick.AddListener(() => SelectPart(partIndex));
            RegisterHitTarget(partButtons[i], false, true, Color.white);
        }

        Button partChangeColor = CreateButton("NextColors", partRect, new Vector2(0f, -222f),
            new Vector2(PillWidth, PillHeight), "CHANGE COLOR", ActionPink);
        partChangeColor.onClick.AddListener(ContinueToColorSelection);
        RegisterHitTarget(partChangeColor, false, false, Color.white);

        Button partDone = CreateButton("Done", partRect, new Vector2(0f, -294f),
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
                sizeText.fontStyle = FontStyles.Bold;
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
                strengthText.fontStyle = FontStyles.Bold;
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
            clayColText.fontStyle = FontStyles.Bold;
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
            doneText.fontStyle = FontStyles.Bold;
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

        Button undo = CreateButton("Undo", barRect, new Vector2(-130f, 0f),
            new Vector2(230f, 54f), "UNDO", PillPink);
        CenterInBar(undo, -130f);
        undo.onClick.AddListener(UndoClay);
        RegisterHitTarget(undo, false, false, Color.white);

        Button reset = CreateButton("Reset", barRect, new Vector2(130f, 0f),
            new Vector2(230f, 54f), "RESET", DonePink);
        CenterInBar(reset, 130f);
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
        headerText.text = "COLOR PALETTE";
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

    // "CHANGE COLOR" from the main menu tints the whole keris. Clear any part
    // selection left over from a previous part edit so it does not keep
    // targeting that one part.
    private void ShowWholeKerisColorSelection()
    {
        selectedPart = -1;
        selectedVariant = -1;
        selectedVariantLabel = "";
        RefreshPartButtons();
        ShowColorSelection();
    }

    private void ShowColorSelection()
    {
        DisableVertexSculpt();
        mode = ColorMode.SelectColor;
        SetPanelHeight(ColorSelectionPanelHeight);
        mainActionGroup.SetActive(false);
        HideExtraGroups();
        colorGroup.SetActive(true);
        if (selfieEntryButton != null)
            selfieEntryButton.gameObject.SetActive(false);
        headerText.text = clayTintActive
            ? "CLAY COLOR"
            : designTintActive
                ? "STAMP COLOR"
                : bandTintActive
                    ? "BAND COLOR"
                    : ColorHeaderLabel();
        instructionText.gameObject.SetActive(true);
        instructionText.text = clayTintActive
            ? "Pick a clay color."
            : designTintActive
                ? "Pick a tint for the stamp."
                : bandTintActive
                    ? "Pick a color for the drawn band."
                    : "Pick a color for the keris.";
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
        headerText.text = "VERTEX SCULPT";
        SetHeaderBackLabel("← BACK");
        SetHeaderBackVisible(true);
        SetTitleBurstsVisible(true);
        instructionText.gameObject.SetActive(true);
        instructionText.text = "Pick coil size and height, then point your index finger and draw clay.";
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
        RestoreVertexSculptCamera();
    }

    private void BeginVertexSculptCameraZoom()
    {
        if (vertexSculptCameraZoomed)
            return;

        Camera cam = ResolveViewCamera();
        Renderer focusRenderer = kerisRenderer;
        if (cam == null || focusRenderer == null)
            return;

        storedSculptCamPos = cam.transform.position;
        storedSculptCamRot = cam.transform.rotation;
        storedSculptCamFov = cam.fieldOfView;
        storedSculptCamNear = cam.nearClipPlane;
        vertexSculptCameraZoomed = true;

        Vector3 targetPos = sculptCameraPosition;
        Quaternion targetRot = Quaternion.Euler(sculptCameraRotation);
        float targetFov = sculptCameraFov;
        float targetNear = Mathf.Min(storedSculptCamNear, 0.05f);
        StartSculptCameraMove(cam, targetPos, targetRot, targetFov, targetNear, 0.45f);
    }

    private void RestoreVertexSculptCamera()
    {
        if (!vertexSculptCameraZoomed)
            return;

        if (!isActiveAndEnabled)
        {
            SnapRestoreVertexSculptCamera();
            return;
        }

        Camera cam = ResolveViewCamera();
        vertexSculptCameraZoomed = false;
        if (cam == null)
            return;

        StartSculptCameraMove(
            cam,
            storedSculptCamPos,
            storedSculptCamRot,
            storedSculptCamFov,
            storedSculptCamNear,
            0.5f);
    }

    private void SnapRestoreVertexSculptCamera()
    {
        if (!vertexSculptCameraZoomed)
            return;

        vertexSculptCameraZoomed = false;
        if (sculptCameraMove != null)
        {
            StopCoroutine(sculptCameraMove);
            sculptCameraMove = null;
        }

        Camera cam = ResolveViewCamera();
        if (cam == null)
            return;

        cam.transform.position = storedSculptCamPos;
        cam.transform.rotation = storedSculptCamRot;
        cam.fieldOfView = storedSculptCamFov;
        cam.nearClipPlane = storedSculptCamNear;
    }

    private void StartSculptCameraMove(
        Camera cam,
        Vector3 targetPos,
        Quaternion targetRot,
        float targetFov,
        float targetNear,
        float duration)
    {
        if (sculptCameraMove != null)
            StopCoroutine(sculptCameraMove);
        sculptCameraMove = StartCoroutine(
            MoveSculptCamera(cam, targetPos, targetRot, targetFov, targetNear, duration));
    }

    private IEnumerator MoveSculptCamera(
        Camera cam,
        Vector3 targetPos,
        Quaternion targetRot,
        float targetFov,
        float targetNear,
        float duration)
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
        sculptCameraMove = null;
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
        SetPanelHeight(PartSelectionPanelHeight);
        mainActionGroup.SetActive(false);
        HideExtraGroups();
        if (partGroup != null)
            partGroup.SetActive(true);
        if (selfieEntryButton != null)
            selfieEntryButton.gameObject.SetActive(false);
        headerText.text = "SELECT PART";
        instructionText.gameObject.SetActive(true);
        instructionText.text = "Choose the body, a carving, or the handle.";
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

        headerText.text = "SELECT PART";
        instructionText.text = selectedPart == PartBody
            ? "Point both index fingers for width and height. Right pinch up for sharpness, left pinch up for curve. Open palm to lock."
            : partNames[selectedPart] + " selected.";
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

        if (PartNeedsVariant(selectedPart) && selectedVariant < 0)
        {
            if (mode == ColorMode.SelectVariant)
            {
                instructionText.text = "Choose a shape first.";
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
                variantButtonLabels[i].text = labels[i];
        }

        headerText.text = partNames[partIndex];
        SetHeaderBackLabel("← PARTS");
        SetHeaderBackVisible(true);
        SetTitleBurstsVisible(true);
        instructionText.gameObject.SetActive(true);
        instructionText.text = partIndex == PartHandle
            ? "Choose Handle 1 or Handle 2."
            : "Choose Carving 1 or Carving 2.";
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
        headerText.text = partNames[selectedPart];
        ArmSelectedVariant();
        RefreshVariantButtons();
        SetSculptGesturesEnabled(true);
        if (kerisGestures != null)
            kerisGestures.SetPinchDrivesCarving(selectedPart == PartCarving);
        if (selectedPart == PartHandle)
            instructionText.text =
                "Make two fists and pull them down to bring " +
                selectedVariantLabel.ToLowerInvariant() + " out.";
        else
            instructionText.text =
                "Make two V signs and spread them outwards to bring " +
                selectedVariantLabel.ToLowerInvariant() + " out.";
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
            return;
        }

        if (bandTintActive)
        {
            committedBandColor = color;
            if (patternPainter != null)
                patternPainter.SetStrokeColor(color, true);
            if (bandColorChip != null)
                bandColorChip.color = color;
            return;
        }

        if (designTintActive)
        {
            committedDesignTint = color;
            if (appliedDesignTexture != null)
                ApplyDesignTint(color);
            return;
        }

        committedKerisColor = color;
        ApplyKerisColor(color);
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
        kerisSaved = false;
        RefreshSavedStatus();
        RefreshPartButtons();
        RefreshVariantButtons();
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

    private void UndoClay()
    {
        if (patternPainter != null && patternPainter.ClayStrokeCount > 0)
        {
            patternPainter.UndoLastClayStroke();
            if (instructionText != null)
            {
                instructionText.gameObject.SetActive(true);
                instructionText.text = "Last clay coil undone.";
            }
            return;
        }

        if (vertexSculptController != null && vertexSculptController.HasUndo &&
            vertexSculptController.UndoLastStroke())
        {
            if (instructionText != null)
            {
                instructionText.gameObject.SetActive(true);
                instructionText.text = "Last sculpt stroke undone.";
            }
            return;
        }

        if (patternPainter != null && patternPainter.StrokeCount > 0)
        {
            patternPainter.UndoLastStroke();
            if (instructionText != null)
            {
                instructionText.gameObject.SetActive(true);
                instructionText.text = "Last drawing stroke undone.";
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
            else if (hoveredTarget == null || !hoveredTarget.IsColor)
                ApplyDesignTint(committedDesignTint);
        }
        else if (bandTintActive)
        {
            if (hoveredTarget != null && hoveredTarget.IsColor && !hoveredTarget.IsFreeColorPalette)
            {
                if (patternPainter != null)
                    patternPainter.SetStrokeColor(hoveredTarget.PreviewColor, true);
            }
            else if (hoveredTarget == null || !hoveredTarget.IsColor)
            {
                if (patternPainter != null)
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
            cursorRect.gameObject.SetActive(visible && mode != ColorMode.Hidden);
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
            DwellEdges = CreateDwellFrame(button.transform)
        });
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
        Button button = CreateButton("HeaderBack", parent, new Vector2(16f, -14f),
            new Vector2(130f, 42f), "← BACK", PillPink);
        RectTransform rect = button.GetComponent<RectTransform>();
        rect.anchorMin = rect.anchorMax = new Vector2(0f, 1f);
        rect.pivot = new Vector2(0f, 1f);
        rect.anchoredPosition = new Vector2(16f, -14f);
        button.onClick.AddListener(NavigateBack);
        RegisterHitTarget(button, false, false, Color.white);
        return button;
    }

    private void SetHeaderBackVisible(bool visible)
    {
        if (headerBackButton != null)
            headerBackButton.gameObject.SetActive(visible);
        if (headerText == null)
            return;
        RectTransform hRect = headerText.rectTransform;
        if (visible)
        {
            hRect.anchoredPosition = new Vector2(10f, -44f);
            headerText.fontSize = 35f;
        }
        else
        {
            hRect.anchoredPosition = new Vector2(10f, -18f);
            headerText.fontSize = 40f;
        }
    }

    private void SetHeaderBackLabel(string label)
    {
        if (headerBackText != null)
            headerBackText.text = label;
    }

    private void SetTitleBurstsVisible(bool visible)
    {
        if (titleBurstRoot != null)
            titleBurstRoot.SetActive(visible);
    }

    private void SetPanelHeight(float height)
    {
        if (panel == null)
            return;
        panel.GetComponent<RectTransform>().sizeDelta =
            new Vector2(CustomizePanelWidth, height + HistoryBarHeight);
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
                label, isDone || isChangeColor ? 26f : 24f,
                TextAlignmentOptions.Center, FontStyles.Bold);
            Stretch(text.rectTransform);
            text.color = DarkTextGreen;
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
        text.color = DarkTextGreen;
        text.fontStyle = FontStyles.Bold;
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
