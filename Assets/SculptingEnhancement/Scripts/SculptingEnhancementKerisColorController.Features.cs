using System;
using System.Collections;
using System.Collections.Generic;
using Mediapipe.Unity.Sample;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public sealed partial class SculptingEnhancementKerisColorController
{
    private GameObject bodyActionGroup;
    private GameObject designGroup;
    private GameObject bandGroup;
    private GameObject bandTargetingBar;
    private GameObject confirmResetGroup;
    private GameObject reviewGroup;
    private GameObject shelfGroup;
    private GameObject confirmDeleteGroup;
    private GameObject selfiePanel;
    private bool bandTintActive;
    private bool colorBackToBands;
    private bool designTintActive;
    private Color committedBandColor = Color.white;
    private Color committedDesignTint = Color.white;
    private Image bandColorChip;
    private Texture2D appliedDesignTexture;
    private Renderer designSurfaceRenderer;
    private Texture2D capturedSelfieTexture;
    private Texture2D appliedSelfieTexture;
    private Texture2D undoneSelfieTexture;
    private Renderer selfieSurfaceRenderer;
    private Material selfieSurfaceMaterial;
    private Material designSurfaceMaterial;
    private Material designHiddenMaterial;
    private MaterialPropertyBlock designPropertyBlock;
    private Texture2D tintedDesignTexture;
    private Texture lastBakedDesignSource;
    private Color lastBakedDesignTint = new Color(-1f, -1f, -1f, -1f);
    private Transform selfieSurfaceRoot;
    private const int SelfieCircleCount = 1;
    private readonly Transform[] selfieCircleRoots = new Transform[SelfieCircleCount];
    private readonly Renderer[] selfieCircleRenderers = new Renderer[SelfieCircleCount];
    private Mesh selfieOverlayMesh;
    private Mesh selfieBoundsMesh;
    private float selfieWarnTimer;
    private int lastSelfieTileLog = -1;
    private Transform designSurfaceRoot;
    private RawImage selfiePreviewImage;
    private TMP_Text selfieInstructionText;
    private GameObject selfieContent;
    private Button takeSelfieButton;
    private Button attachSelfieButton;
    private Button retakeSelfieButton;
    private Button selfieBackButton;
    private Button selfieDoneButton;
    private Button removeSelfieButton;
    private RawImage freeColorPaletteImage;
    private RectTransform freeColorSelectionCheck;
    private Color freePalettePreviewColor = Color.white;
    private Vector2 freePaletteMarkerPosition;
    private Vector2 lastColorSampleScreen;
    private Texture2D freeColorTexture;
    private Texture2D[] kerisStamps;
    private Texture2D[] kerisBands;
    private Texture2D lastBandMotif;
    private Texture2D appliedBandTexture;
    private Texture2D stampAlphaTexture;
    private Texture2D stampAlphaSource;
    private Mesh stampOverlayMesh;
    private bool returnToBandDrawing;
    private int selectedBandSizeIndex = 1;
    private Button targetingSizeBtn;
    private Button targetingColorBtn;
    private readonly Button[] bandButtons = new Button[8];
    private readonly RawImage[] bandPreviews = new RawImage[8];
    private readonly Button[] bandSizeButtons = new Button[4];
    private readonly string[] bandSizeLabels = { "SLIM", "MEDIUM", "BOLD", "WIDE" };
    private readonly float[] bandSizes = { 0.0006f, 0.0012f, 0.0022f, 0.0035f };
    private const int StampTileRows = 3;
    private const float StampTileSpanFraction = 0.78f;
    private const float StampTileFillFraction = 0.92f;
    private LanternShelfController kerisShelf;
    private Transform[] kerisSlots;
    private readonly List<GameObject> kerisShelfDisplays = new List<GameObject>();
    private bool workingKerisVisible = true;
    private const string KerisSlotName = "KerisShelfSlot";
    private const int KerisSlotCount = 9;
    private Color[] savedPartColors = { new Color(0.80f, 0.58f, 0.22f, 1f), Color.white, Color.white };
    private const int UnresolvedCarvingSlot = -2;
    private int cachedCarvingSlot = UnresolvedCarvingSlot;
    private Mesh cachedCarvingMesh;

    private const float SelfieCircleFill = 0.92f;
    private const float SelfieAlongFill = 2.6f;
    private const float SelfieAcrossFill = 0.78f;
    private const int SelfieCircleSegments = 36;
    private const float SelfieFrontDot = 0.22f;
    private const int SelfieStripRows = 32;

    private void BuildConfirmReset(RectTransform parent)
    {
        confirmResetGroup = new GameObject("ConfirmReset", typeof(RectTransform));
        confirmResetGroup.transform.SetParent(parent, false);
        RectTransform rect = confirmResetGroup.GetComponent<RectTransform>();
        DockTop(rect, -145f, 210f);
        CreateText("ResetPrompt", rect, new Vector2(0f, 0f), new Vector2(PillWidth, 55f),
            "Reset the Keris and clear your current decorations?", 20f,
            TextAlignmentOptions.Center, FontStyles.Bold);
        Button cancel = CreateButton("CancelReset", rect, new Vector2(-125f, -90f),
            new Vector2(220f, 56f), "CANCEL", PillPink);
        cancel.onClick.AddListener(HideConfirmReset);
        RegisterHitTarget(cancel, false, false, Color.white);
        Button confirm = CreateButton("ConfirmReset", rect, new Vector2(125f, -90f),
            new Vector2(220f, 56f), "RESET", DonePink);
        confirm.onClick.AddListener(() => { ResetKerisColor(); HideConfirmReset(); ShowMainMenu(); });
        RegisterHitTarget(confirm, false, false, Color.white);
        confirmResetGroup.SetActive(false);
    }

    private Button CreateFreeColorPalette(Transform parent, Vector2 anchoredPosition, Vector2 size)
    {
        GameObject root = new GameObject("FreeColorPalette", typeof(RectTransform), typeof(RawImage), typeof(Button));
        root.transform.SetParent(parent, false);
        RectTransform rect = root.GetComponent<RectTransform>();
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 1f);
        rect.pivot = new Vector2(0.5f, 1f);
        rect.anchoredPosition = anchoredPosition;
        rect.sizeDelta = size;
        freeColorPaletteImage = root.GetComponent<RawImage>();
        freeColorPaletteImage.raycastTarget = true;
        freeColorTexture = new Texture2D(128, 64, TextureFormat.RGB24, false)
        {
            name = "MorphicKerisFreeColorPalette",
            filterMode = FilterMode.Bilinear,
            wrapMode = TextureWrapMode.Clamp
        };
        for (int y = 0; y < freeColorTexture.height; y++)
        for (int x = 0; x < freeColorTexture.width; x++)
        {
            float h = x / (float)(freeColorTexture.width - 1);
            float v = y / (float)(freeColorTexture.height - 1);
            Color hue = Color.HSVToRGB(h, 1f, 1f);
            freeColorTexture.SetPixel(x, y, v > 0.5f
                ? Color.Lerp(hue, Color.white, (v - 0.5f) * 2f)
                : Color.Lerp(Color.black, hue, v * 2f));
        }
        freeColorTexture.Apply();
        freeColorPaletteImage.texture = freeColorTexture;
        Button button = root.GetComponent<Button>();
        button.targetGraphic = freeColorPaletteImage;
        button.transition = Selectable.Transition.None;
        freeColorSelectionCheck = CreateSelectionCheck(rect, true);
        return button;
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

    private void UpdateFreePalettePreview(HitTarget target, Vector2 screenPosition)
    {
        if (mode != ColorMode.SelectColor || target == null || !target.IsFreeColorPalette ||
            freeColorPaletteImage == null) return;
        RectTransform rect = freeColorPaletteImage.rectTransform;
        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(rect, screenPosition, canvasCamera,
                out Vector2 local)) return;
        Rect r = rect.rect;
        float x = Mathf.Clamp01(Mathf.InverseLerp(r.xMin, r.xMax, local.x));
        float y = Mathf.Clamp01(Mathf.InverseLerp(r.yMin, r.yMax, local.y));
        Color hue = Color.HSVToRGB(x, 1f, 1f);
        freePalettePreviewColor = y > 0.5f
            ? Color.Lerp(hue, Color.white, (y - 0.5f) * 2f)
            : Color.Lerp(Color.black, hue, y * 2f);
        lastColorSampleScreen = screenPosition;
        freePaletteMarkerPosition = local;
        if (clayTintActive)
            PreviewClayColor(freePalettePreviewColor);
        else if (!designTintActive && !bandTintActive)
            ApplyKerisColor(freePalettePreviewColor);
    }

    private void CommitFreePaletteColor()
    {
        CommitColor(freePalettePreviewColor);
        ClearSelectedColorVisual();
        if (freeColorSelectionCheck != null)
        {
            freeColorSelectionCheck.anchoredPosition = freePaletteMarkerPosition;
            freeColorSelectionCheck.gameObject.SetActive(true);
        }
    }

    private void OnColorDoneClicked()
    {
        if (clayTintActive) { clayTintActive = false; ShowVertexSculpt(); return; }
        if (designTintActive) { designTintActive = false; ShowDesignSelection(); return; }
        if (bandTintActive)
        {
            bandTintActive = false;
            if (returnToBandDrawing)
            {
                returnToBandDrawing = false;
                colorBackToBands = false;
                ResumeBandDrawing();
            }
            else if (colorBackToBands)
            {
                colorBackToBands = false;
                ShowBandSelection();
            }
            else
                ResumeBandDrawing();
            return;
        }
        if (selectedPart == PartBody)
            ShowBodyActions();
        else if (selectedPart >= 0 && PartNeedsVariant(selectedPart) && selectedVariant >= 0)
            ShowVariantSelection(selectedPart);
        else if (selectedPart >= 0) ShowPartSelection();
        else ShowMainMenu();
    }

    private void BuildReview(RectTransform parent)
    {
        reviewGroup = new GameObject("KerisReview", typeof(RectTransform));
        reviewGroup.transform.SetParent(parent, false);
        RectTransform rect = reviewGroup.GetComponent<RectTransform>();
        DockTop(rect, -124f, 220f);

        Button retake = CreateButton("Retake", rect, new Vector2(0f, -28f),
            new Vector2(PillWidth, PillHeight), "RETAKE", PillPink);
        retake.onClick.AddListener(ShowMainMenu);
        RegisterHitTarget(retake, false, false, Color.white);

        Button finish = CreateButton("Finish", rect, new Vector2(0f, -100f),
            new Vector2(PillWidth, PillHeight), "FINISH", DonePink);
        finish.onClick.AddListener(FinishToShelf);
        RegisterHitTarget(finish, false, false, Color.white);
        reviewGroup.SetActive(false);

        shelfGroup = new GameObject("ShelfActions", typeof(RectTransform));
        shelfGroup.transform.SetParent(parent, false);
        RectTransform shelfRect = shelfGroup.GetComponent<RectTransform>();
        DockTop(shelfRect, -124f, 250f);

        Button takeBack = CreateButton("TakeBack", shelfRect, new Vector2(0f, 0f),
            new Vector2(PillWidth, PillHeight), "TAKE BACK", PillPink);
        takeBack.onClick.AddListener(TakeBackFromShelf);
        RegisterHitTarget(takeBack, false, false, Color.white);

        Button delete = CreateButton("Delete", shelfRect, new Vector2(0f, -72f),
            new Vector2(PillWidth, PillHeight), "DELETE", DonePink);
        delete.onClick.AddListener(ShowConfirmDelete);
        RegisterHitTarget(delete, false, false, Color.white);

        Button done = CreateButton("Done", shelfRect, new Vector2(0f, -144f),
            new Vector2(PillWidth, PillHeight), "DONE", DonePink);
        done.onClick.AddListener(LeaveShelfDone);
        RegisterHitTarget(done, false, false, Color.white);
        shelfGroup.SetActive(false);

        confirmDeleteGroup = new GameObject("ConfirmDelete", typeof(RectTransform));
        confirmDeleteGroup.transform.SetParent(parent, false);
        RectTransform confirmRect = confirmDeleteGroup.GetComponent<RectTransform>();
        DockTop(confirmRect, -124f, 170f);

        Button cancelDelete = CreateButton("Cancel", confirmRect, new Vector2(0f, 0f),
            new Vector2(PillWidth, PillHeight), "CANCEL", PillPink);
        cancelDelete.onClick.AddListener(ShowShelf);
        RegisterHitTarget(cancelDelete, false, false, Color.white);

        Button confirmDelete = CreateButton("Delete", confirmRect, new Vector2(0f, -72f),
            new Vector2(PillWidth, PillHeight), "DELETE", DonePink);
        confirmDelete.onClick.AddListener(ConfirmDeleteKeris);
        RegisterHitTarget(confirmDelete, false, false, Color.white);
        confirmDeleteGroup.SetActive(false);
    }

    private void BuildSelfie(Transform parent)
    {
        selfiePanel = new GameObject("KerisSelfiePanel", typeof(RectTransform), typeof(Image));
        selfiePanel.transform.SetParent(parent, false);
        RectTransform rect = selfiePanel.GetComponent<RectTransform>();
        rect.anchorMin = rect.anchorMax = new Vector2(1f, 0f);
        rect.pivot = new Vector2(1f, 0f);
        rect.anchoredPosition = new Vector2(-24f, 24f);
        rect.sizeDelta = new Vector2(CustomizePanelWidth, SelfiePanelHeight);
        selfiePanel.GetComponent<Image>().color = Color.clear;
        
        StyleMorphicCard(rect);

        headerText = CreateText("SelfieTitle", rect, new Vector2(20f, -16f), new Vector2(380f, 48f),
            "SELFIE", 32f, TextAlignmentOptions.Center, FontStyles.Bold);
        StyleMorphicTitle(headerText);

        selfieInstructionText = CreateText("Instruction", rect, new Vector2(20f, -70f), new Vector2(380f, 40f),
            "Frame your photo, then take a selfie.", 18f, TextAlignmentOptions.Center, FontStyles.Bold);
        selfieInstructionText.color = LanguagePageBuilder.Green;

        selfieContent = new GameObject("Content", typeof(RectTransform));
        selfieContent.transform.SetParent(rect, false);
        RectTransform selfieRect = selfieContent.GetComponent<RectTransform>();
        DockTop(selfieRect, -118f, 520f);

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
        RegisterHitTarget(takeSelfieButton, false, false, Color.white);

        selfieBackButton = CreateButton("Back", selfieRect, new Vector2(0f, -306f),
            new Vector2(PillWidth, PillHeight), "BACK", PillPink);
        selfieBackButton.onClick.AddListener(ShowMainMenu);
        RegisterHitTarget(selfieBackButton, false, false, Color.white);

        attachSelfieButton = CreateButton("AttachToKeris", selfieRect, new Vector2(0f, -234f),
            new Vector2(PillWidth, PillHeight), "ATTACH TO KERIS", ActionPink);
        attachSelfieButton.onClick.AddListener(KeepSelfie);
        RegisterHitTarget(attachSelfieButton, false, false, Color.white);

        retakeSelfieButton = CreateButton("Retake", selfieRect, new Vector2(-92f, -306f),
            new Vector2(172f, PillHeight), "RETAKE", PillPink);
        retakeSelfieButton.onClick.AddListener(BeginSelfieRetake);
        RegisterHitTarget(retakeSelfieButton, false, false, Color.white);

        selfieDoneButton = CreateButton("Done", selfieRect, new Vector2(92f, -306f),
            new Vector2(172f, PillHeight), "DONE", DonePink);
        selfieDoneButton.onClick.AddListener(ShowMainMenu);
        RegisterHitTarget(selfieDoneButton, false, false, Color.white);

        removeSelfieButton = CreateButton("RemoveSelfie", selfieRect, new Vector2(0f, -378f),
            new Vector2(PillWidth, PillHeight), "REMOVE SELFIE", PillPink);
        removeSelfieButton.onClick.AddListener(RemoveSelfie);
        RegisterHitTarget(removeSelfieButton, false, false, Color.white);

        selfiePanel.SetActive(false);
        SetSelfieReviewControls(false);
    }

    private void SetSelfiePreviewTexture(Texture texture)
    {
        if (selfiePreviewImage == null) return;
        selfiePreviewImage.texture = texture;
        selfiePreviewImage.uvRect = new Rect(0f, 0f, 1f, 1f);
        if (texture == null || texture.width <= 0 || texture.height <= 0) return;

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
                capturedPixel.a = 255;
                targetPixels[targetY * targetWidth + targetX] = capturedPixel;
            }
        }

        Texture2D result = new Texture2D(targetWidth, targetHeight, TextureFormat.RGBA32, false);
        result.SetPixels32(targetPixels);
        result.Apply(false, false);
        return result;
    }

    private void SetSelfieReviewControls(bool review)
    {
        if (takeSelfieButton != null) takeSelfieButton.gameObject.SetActive(!review);
        if (selfieBackButton != null) selfieBackButton.gameObject.SetActive(!review);
        if (attachSelfieButton != null) attachSelfieButton.gameObject.SetActive(review);
        if (retakeSelfieButton != null) retakeSelfieButton.gameObject.SetActive(review);
        if (selfieDoneButton != null) selfieDoneButton.gameObject.SetActive(review);
        if (removeSelfieButton != null)
            removeSelfieButton.gameObject.SetActive(
                appliedSelfieTexture != null || (review && capturedSelfieTexture != null));
    }

    private void ShowBodyActions()
    {
        DisableVertexSculpt();
        selectedPart = PartBody;
        designTintActive = false;
        bandTintActive = false;
        EnsureBodyActionGroup();
        mode = ColorMode.SelectBodyActions;
        SetPanelHeight(BodyActionsPanelHeight);
        panel.SetActive(true);
        mainActionGroup.SetActive(false);
        HideExtraGroups();
        HideReviewAndShelf();
        if (selfiePanel != null)
            selfiePanel.SetActive(false);
        if (selfieEntryButton != null)
            selfieEntryButton.gameObject.SetActive(false);
        if (historyBar != null)
            historyBar.SetActive(true);
        if (bodyActionGroup != null)
            bodyActionGroup.SetActive(true);
        headerText.text = "KERIS BODY";
        instructionText.gameObject.SetActive(true);
        instructionText.text = appliedDesignTexture != null
            ? "Change the blade color, add another stamp, or draw a band."
            : "Change the blade color, add a stamped design, or draw a band.";
        SetHeaderBackLabel("← PARTS");
        SetHeaderBackVisible(true);
        SetTitleBurstsVisible(false);
        SetSculptGesturesEnabled(true);
        ResetDwell();
        SetHoveredTarget(null);
    }

    private void EnsureBodyActionGroup()
    {
        if (bodyActionGroup != null || panel == null)
            return;

        bodyActionGroup = new GameObject("BodyActions", typeof(RectTransform));
        bodyActionGroup.transform.SetParent(panel.transform, false);
        RectTransform bodyRect = bodyActionGroup.GetComponent<RectTransform>();
        DockTop(bodyRect, -124f, 310f);

        Button changeColor = CreateButton(
            "ChangeColor", bodyRect, new Vector2(0f, 0f),
            new Vector2(PillWidth, PillHeight), "CHANGE COLOR", ActionPink);
        changeColor.onClick.AddListener(OpenBodyPaperColor);
        RegisterHitTarget(changeColor, false, false, Color.white);

        Button addDesign = CreateButton(
            "AddDesign", bodyRect, new Vector2(0f, -74f),
            new Vector2(PillWidth, PillHeight), "ADD DESIGN", PillPink);
        addDesign.onClick.AddListener(ShowDesignSelection);
        RegisterHitTarget(addDesign, false, false, Color.white);

        Button drawBand = CreateButton(
            "DrawBand", bodyRect, new Vector2(0f, -148f),
            new Vector2(PillWidth, PillHeight), "DRAW BAND", PillPink);
        drawBand.onClick.AddListener(ShowBandSelection);
        RegisterHitTarget(drawBand, false, false, Color.white);

        Button done = CreateButton(
            "Done", bodyRect, new Vector2(0f, -222f),
            new Vector2(PillWidth, PillHeight), "DONE", DonePink);
        done.onClick.AddListener(ShowMainMenu);
        RegisterHitTarget(done, false, false, Color.white);
        bodyActionGroup.SetActive(false);
    }

    private void OpenBodyPaperColor()
    {
        designTintActive = false;
        bandTintActive = false;
        clayTintActive = false;
        selectedPart = PartBody;
        ContinueToColorSelection();
    }

    private void ShowDesignSelection()
    {
        DisableVertexSculpt();
        designTintActive = false;
        selectedPart = PartBody;
        EnsureDesignGroup();
        mode = ColorMode.SelectDesign;
        SetPanelHeight(DesignSelectionPanelHeight);
        panel.SetActive(true);
        mainActionGroup.SetActive(false);
        HideExtraGroups();
        HideReviewAndShelf();
        if (selfiePanel != null)
            selfiePanel.SetActive(false);
        if (selfieEntryButton != null)
            selfieEntryButton.gameObject.SetActive(false);
        if (designGroup != null)
            designGroup.SetActive(true);
        headerText.text = "ADD DESIGN";
        instructionText.gameObject.SetActive(true);
        instructionText.text = kerisStamps == null || kerisStamps.Length == 0
            ? "No stamps found in StampPatterns."
            : "Choose a stamp. Then tint it with Change Color.";
        SetHeaderBackLabel("← BODY");
        SetHeaderBackVisible(true);
        SetTitleBurstsVisible(false);
        ResetDwell();
        SetHoveredTarget(null);
    }

    private void EnsureDesignGroup()
    {
        if (designGroup != null || panel == null)
            return;

        designGroup = new GameObject("KerisStamps", typeof(RectTransform));
        designGroup.transform.SetParent(panel.transform, false);
        DockTop(designGroup.GetComponent<RectTransform>(), -124f, 420f);
        kerisStamps = MorphicPatternCatalog.Stamps();
        if (kerisStamps == null)
            kerisStamps = Array.Empty<Texture2D>();
        int count = Mathf.Min(kerisStamps.Length, 6);
        for (int i = 0; i < count; i++)
        {
            Texture2D stamp = kerisStamps[i];
            Button button = CreateButton("Stamp" + i, designGroup.transform,
                new Vector2(-125f + (i % 3) * 125f, -(i / 3) * 100f),
                new Vector2(110f, 88f), PatternLabel(stamp), PillPink);
            DecoratePatternButton(button, stamp, PatternLabel(stamp));
            button.onClick.AddListener(() => SelectStamp(stamp));
            RegisterHitTarget(button, false, false, Color.white);
        }

        Button tint = CreateButton("StampColor", designGroup.transform, new Vector2(0f, -228f),
            new Vector2(PillWidth, PillHeight), "CHANGE COLOR", ActionPink);
        tint.onClick.AddListener(() => { designTintActive = true; ShowColorSelection(); });
        RegisterHitTarget(tint, false, false, Color.white);

        Button clear = CreateButton("ClearDesign", designGroup.transform, new Vector2(0f, -300f),
            new Vector2(PillWidth, PillHeight), "CLEAR DESIGN", PillPink);
        clear.onClick.AddListener(ClearKerisDesign);
        RegisterHitTarget(clear, false, false, Color.white);

        Button done = CreateButton("Done", designGroup.transform, new Vector2(0f, -372f),
            new Vector2(PillWidth, PillHeight), "DONE", DonePink);
        done.onClick.AddListener(ShowBodyActions);
        RegisterHitTarget(done, false, false, Color.white);
        designGroup.SetActive(false);
    }

    private void ClearKerisDesign()
    {
        HideKerisDesign();
        instructionText.text = "Design cleared.";
        PushHistory();
    }

    private void HideKerisDesign()
    {
        appliedDesignTexture = null;
        designTintActive = false;
        committedDesignTint = Color.white;
        if (designSurfaceRenderer != null)
            designSurfaceRenderer.enabled = false;
        if (designSurfaceRoot != null)
            designSurfaceRoot.gameObject.SetActive(false);
    }

    private void RestoreStampFromSnapshot(KerisSnapshot snapshot)
    {
        if (snapshot != null && snapshot.StampVisible && snapshot.Stamp != null)
        {
            Texture2D stamp = snapshot.Stamp as Texture2D;
            if (stamp == null && !string.IsNullOrEmpty(snapshot.Stamp.name))
                stamp = FindStampByName(snapshot.Stamp.name);
            if (stamp != null)
            {
                ApplyKerisStamp(stamp, snapshot.StampTint);
                return;
            }
        }
        HideKerisDesign();
    }

    private void SelectStamp(Texture2D stamp)
    {
        if (stamp == null)
        {
            instructionText.text = "That stamp is missing.";
            return;
        }

        ApplyKerisStamp(stamp, committedDesignTint);
        instructionText.text = PatternLabel(stamp) + " applied. Change Color tints the stamp.";
        PushHistory();
    }

    private void ApplyKerisStamp(Texture2D stamp, Color tint)
    {
        appliedDesignTexture = stamp;
        committedDesignTint = tint;
        EnsureDesignOverlay();
        ConfigureDesignSurfaceMaterial(stamp);
        ApplyDesignTint(tint);
        if (designSurfaceRoot != null)
            designSurfaceRoot.gameObject.SetActive(true);
        if (designSurfaceRenderer != null)
            designSurfaceRenderer.enabled = true;
    }

    private void ShowBandSelection()
    {
        DisableVertexSculpt();
        designTintActive = false;
        bandTintActive = false;
        returnToBandDrawing = false;
        selectedPart = PartBody;
        EnsureBandGroup();
        RefreshBandButtons();
        RefreshBandSizeButtons();
        if (patternPainter != null)
            patternPainter.SetPaintingEnabled(false);
        if (bandColorChip != null)
            bandColorChip.color = committedBandColor;
        mode = ColorMode.SelectDrawMotif;
        SetPanelHeight(BandSelectionPanelHeight);
        panel.SetActive(true);
        mainActionGroup.SetActive(false);
        HideExtraGroups();
        HideReviewAndShelf();
        if (selfiePanel != null)
            selfiePanel.SetActive(false);
        if (selfieEntryButton != null)
            selfieEntryButton.gameObject.SetActive(false);
        if (historyBar != null)
            historyBar.SetActive(true);
        if (bandGroup != null)
            bandGroup.SetActive(true);
        headerText.text = "DRAW BAND";
        instructionText.gameObject.SetActive(true);
        instructionText.text = "Select a motif, adjust size & color, then trace on the keris.";
        SetHeaderBackLabel("← BODY");
        SetHeaderBackVisible(true);
        SetTitleBurstsVisible(false);
        ResetDwell();
        SetHoveredTarget(null);
    }

    private void EnsureBandGroup()
    {
        if (bandGroup != null || panel == null)
            return;

        bandGroup = new GameObject("BandSelection", typeof(RectTransform));
        bandGroup.transform.SetParent(panel.transform, false);
        RectTransform bandRect = bandGroup.GetComponent<RectTransform>();
        DockTop(bandRect, -120f, 720f);
        kerisBands = MorphicPatternCatalog.Bands();
        if (kerisBands == null)
            kerisBands = Array.Empty<Texture2D>();

        for (int i = 0; i < bandButtons.Length; i++)
        {
            int bandIndex = i;
            int column = i % 2;
            int row = i / 2;
            float posX = column == 0 ? -129f : 129f;
            float posY = -row * 100f;

            Image frame = LanguagePageBuilder.CreateRoundedImage("Band" + i, bandRect, 20, LanguagePageBuilder.Green);
            RectTransform rect = frame.rectTransform;
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 1f);
            rect.pivot = new Vector2(0.5f, 1f);
            rect.anchoredPosition = new Vector2(posX, posY);
            rect.sizeDelta = new Vector2(232f, 90f);
            frame.raycastTarget = true;

            Image fill = LanguagePageBuilder.CreateRoundedImage("Fill", frame.transform, 16, PillPink);
            LanguagePageBuilder.Stretch(fill.rectTransform, new Vector2(4f, 4f));
            fill.raycastTarget = true;

            GameObject previewObj = new GameObject("Preview", typeof(RectTransform), typeof(RawImage));
            previewObj.transform.SetParent(fill.transform, false);
            RectTransform prevRect = previewObj.GetComponent<RectTransform>();
            prevRect.anchorMin = new Vector2(0.04f, 0.38f);
            prevRect.anchorMax = new Vector2(0.96f, 0.94f);
            prevRect.offsetMin = Vector2.zero;
            prevRect.offsetMax = Vector2.zero;
            RawImage prevImg = previewObj.GetComponent<RawImage>();
            prevImg.color = Color.white;
            prevImg.raycastTarget = false;
            bandPreviews[i] = prevImg;

            GameObject labelObj = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI));
            labelObj.transform.SetParent(fill.transform, false);
            RectTransform labelRect = labelObj.GetComponent<RectTransform>();
            labelRect.anchorMin = new Vector2(0f, 0.02f);
            labelRect.anchorMax = new Vector2(1f, 0.38f);
            labelRect.offsetMin = Vector2.zero;
            labelRect.offsetMax = Vector2.zero;
            TextMeshProUGUI labelText = labelObj.GetComponent<TextMeshProUGUI>();
            labelText.text = "BAND " + (i + 1);
            labelText.fontSize = 18f;
            labelText.fontStyle = FontStyles.Bold;
            labelText.alignment = TextAlignmentOptions.Center;
            labelText.color = DarkTextGreen;
            labelText.raycastTarget = false;

            Button btn = frame.gameObject.AddComponent<Button>();
            btn.targetGraphic = fill;
            btn.onClick.AddListener(() => SelectBandMotif(bandIndex));
            RegisterHitTarget(btn, false, true, Color.white);
            bandButtons[i] = btn;
        }

        TMP_Text widthTitle = CreateText(
            "WidthTitle", bandRect, new Vector2(0f, -406f),
            new Vector2(PillWidth, 24f), "BAND WIDTH", 19f,
            TextAlignmentOptions.Center, FontStyles.Bold);
        widthTitle.color = DarkTextGreen;
        RectTransform widthRect = widthTitle.rectTransform;
        widthRect.anchorMin = widthRect.anchorMax = new Vector2(0.5f, 1f);
        widthRect.pivot = new Vector2(0.5f, 1f);
        widthRect.anchoredPosition = new Vector2(0f, -406f);

        for (int s = 0; s < bandSizeButtons.Length; s++)
        {
            int sizeIdx = s;
            float posX = -174f + s * 116f;
            bandSizeButtons[s] = CreateButton(
                "BandSize" + s, bandRect, new Vector2(posX, -438f),
                new Vector2(114f, 48f), bandSizeLabels[s],
                s == selectedBandSizeIndex ? ActionPink : PillPink);
            TMP_Text sizeText = bandSizeButtons[s].GetComponentInChildren<TMP_Text>();
            if (sizeText != null)
            {
                sizeText.fontSize = 18f;
                sizeText.fontStyle = FontStyles.Bold;
                sizeText.color = DarkTextGreen;
            }
            bandSizeButtons[s].onClick.AddListener(() => SelectBandSize(sizeIdx));
            RegisterHitTarget(bandSizeButtons[s], false, false, Color.white);
        }

        Button bandColorBtn = CreateButton(
            "BandColor", bandRect, new Vector2(0f, -502f),
            new Vector2(PillWidth, 56f), "BAND COLOR", ActionPink);
        bandColorBtn.onClick.AddListener(OpenBandColorSelection);
        TMP_Text bColText = bandColorBtn.GetComponentInChildren<TMP_Text>();
        if (bColText != null)
        {
            bColText.fontSize = 24f;
            bColText.fontStyle = FontStyles.Bold;
            bColText.color = DarkTextGreen;
        }
        RegisterHitTarget(bandColorBtn, false, false, Color.white);

        Image swatchFrame = LanguagePageBuilder.CreateRoundedImage("SwatchFrame", bandColorBtn.transform, 10, LanguagePageBuilder.Green);
        RectTransform sfRect = swatchFrame.rectTransform;
        sfRect.anchorMin = sfRect.anchorMax = new Vector2(0.88f, 0.5f);
        sfRect.pivot = new Vector2(0.5f, 0.5f);
        sfRect.anchoredPosition = Vector2.zero;
        sfRect.sizeDelta = new Vector2(52f, 34f);
        swatchFrame.raycastTarget = false;

        Image swatchFill = LanguagePageBuilder.CreateRoundedImage("SwatchFill", swatchFrame.transform, 8, committedBandColor);
        LanguagePageBuilder.Stretch(swatchFill.rectTransform, new Vector2(3f, 3f));
        swatchFill.raycastTarget = false;
        bandColorChip = swatchFill;

        Button clearStrokes = CreateButton(
            "ClearStrokes", bandRect, new Vector2(0f, -570f),
            new Vector2(PillWidth, 52f), "CLEAR DRAWINGS", PillPink);
        TMP_Text clearText = clearStrokes.GetComponentInChildren<TMP_Text>();
        if (clearText != null)
        {
            clearText.fontSize = 22f;
            clearText.fontStyle = FontStyles.Bold;
            clearText.color = DarkTextGreen;
        }
        clearStrokes.onClick.AddListener(() =>
        {
            if (patternPainter != null)
                patternPainter.ClearAllStrokes();
            instructionText.text = "All drawings cleared.";
        });
        RegisterHitTarget(clearStrokes, false, false, Color.white);

        Button done = CreateButton(
            "Done", bandRect, new Vector2(0f, -634f),
            new Vector2(PillWidth, 60f), "DONE", DonePink);
        done.onClick.AddListener(ShowBodyActions);
        RegisterHitTarget(done, false, false, Color.white);
        bandGroup.SetActive(false);
    }

    private void SelectBandMotif(int index)
    {
        if (kerisBands == null || index < 0 || index >= kerisBands.Length)
            return;

        Texture2D motif = kerisBands[index];
        if (motif == null)
            return;

        lastBandMotif = motif;
        appliedBandTexture = motif;
        PrepareBandPainter(motif);
        BeginBandDrawing(PatternLabel(motif));
    }

    private void SelectBandMotif(Texture2D motif)
    {
        lastBandMotif = motif;
        appliedBandTexture = motif;
        PrepareBandPainter(motif);
        BeginBandDrawing(PatternLabel(motif));
    }

    private void ResumeBandDrawing()
    {
        PrepareBandPainter(lastBandMotif);
        bandTintActive = false;
        BeginBandDrawing(lastBandMotif != null ? PatternLabel(lastBandMotif) : "BAND");
    }

    private void PrepareBandPainter(Texture2D motif)
    {
        EnsurePainter();
        if (patternPainter == null)
            return;

        float width = bandSizes[Mathf.Clamp(selectedBandSizeIndex, 0, bandSizes.Length - 1)];
        patternPainter.SetClayCoilMode(false);
        patternPainter.SetPatternTexture(motif);
        patternPainter.SetStrokeColor(committedBandColor, true);
        patternPainter.SetStrokeWidth(width, false);
        patternPainter.SetMinPointSpacing(width * 0.25f);
        patternPainter.SetDrawingLocked(false);
        patternPainter.SetPaintingEnabled(true);
    }

    private void BeginBandDrawing(string motifName)
    {
        mode = ColorMode.DrawBandTargeting;
        HideExtraGroups();
        HideReviewAndShelf();
        EnsureBandTargetingBar();
        RefreshBandSizeButtons();
        if (bandTargetingBar != null)
            bandTargetingBar.SetActive(true);
        if (historyBar != null)
            historyBar.SetActive(false);
        SetPanelHeight(330f);
        headerText.text = "DRAW: " + motifName.ToUpperInvariant();
        SetHeaderBackLabel("← MOTIFS");
        SetHeaderBackVisible(true);
        instructionText.gameObject.SetActive(true);
        instructionText.fontSize = 20f;
        instructionText.fontStyle = FontStyles.Bold;
        instructionText.color = DarkTextGreen;
        instructionText.text = "Trace index finger across the keris to draw. Open palm to lock.";
        ResetDwell();
        SetHoveredTarget(null);
        BeginVertexSculptCameraZoom();
    }

    private void SelectBandSize(int sizeIdx)
    {
        selectedBandSizeIndex = Mathf.Clamp(sizeIdx, 0, bandSizes.Length - 1);
        float width = bandSizes[selectedBandSizeIndex];
        if (patternPainter != null)
        {
            patternPainter.SetStrokeWidth(width, true);
            patternPainter.SetMinPointSpacing(width * 0.25f);
        }

        RefreshBandSizeButtons();
        instructionText.text = "Band width: " + bandSizeLabels[selectedBandSizeIndex] + ".";
    }

    private void RefreshBandSizeButtons()
    {
        for (int s = 0; s < bandSizeButtons.Length; s++)
        {
            if (bandSizeButtons[s] == null)
                continue;
            Image fill = GetButtonFill(bandSizeButtons[s]);
            if (fill != null)
                fill.color = s == selectedBandSizeIndex ? ActionPink : PillPink;
        }

        if (targetingSizeBtn != null)
        {
            TMP_Text label = targetingSizeBtn.GetComponentInChildren<TMP_Text>();
            if (label != null)
                label.text = "SIZE: " + bandSizeLabels[selectedBandSizeIndex];
        }
    }

    private void RefreshBandButtons()
    {
        if (kerisBands == null)
            kerisBands = MorphicPatternCatalog.Bands();
        int bandCount = kerisBands != null ? kerisBands.Length : 0;
        for (int i = 0; i < bandButtons.Length; i++)
        {
            bool visible = i < bandCount && kerisBands[i] != null;
            if (bandButtons[i] != null)
                bandButtons[i].gameObject.SetActive(visible);
            if (!visible)
                continue;

            if (bandPreviews[i] != null)
                bandPreviews[i].texture = kerisBands[i];
            TMP_Text label = bandButtons[i].GetComponentInChildren<TMP_Text>();
            if (label != null)
                label.text = PatternLabel(kerisBands[i]);
        }
    }

    private void OpenBandColorSelection()
    {
        colorBackToBands = !returnToBandDrawing;
        bandTintActive = true;
        clayTintActive = false;
        designTintActive = false;
        selectedPart = PartBody;
        ShowColorSelection();
        headerText.text = "BAND COLOR";
        SetHeaderBackLabel(returnToBandDrawing ? "← DRAW" : "← MOTIFS");
        instructionText.text = "Point and hold on a color to tint the band designs.";
    }

    private void EnsureBandTargetingBar()
    {
        if (bandTargetingBar != null || panel == null)
            return;

        bandTargetingBar = new GameObject("BandTargetingBar", typeof(RectTransform));
        bandTargetingBar.transform.SetParent(panel.transform, false);
        RectTransform barRect = bandTargetingBar.GetComponent<RectTransform>();
        DockTop(barRect, -116f, 56f);

        targetingSizeBtn = CreateButton(
            "TargetingSize", barRect, new Vector2(-187f, 0f),
            new Vector2(126f, 48f),
            "SIZE: " + bandSizeLabels[selectedBandSizeIndex], PillPink);
        targetingSizeBtn.onClick.AddListener(() =>
        {
            SelectBandSize((selectedBandSizeIndex + 1) % bandSizes.Length);
        });
        RegisterHitTarget(targetingSizeBtn, false, false, Color.white);

        targetingColorBtn = CreateButton(
            "TargetingColor", barRect, new Vector2(-80f, 0f),
            new Vector2(84f, 48f), "COLOR", ActionPink);
        targetingColorBtn.onClick.AddListener(() =>
        {
            returnToBandDrawing = true;
            OpenBandColorSelection();
        });
        RegisterHitTarget(targetingColorBtn, false, false, Color.white);

        Button undoBtn = CreateButton(
            "TargetingUndo", barRect, new Vector2(8f, 0f),
            new Vector2(84f, 48f), "UNDO", PillPink);
        undoBtn.onClick.AddListener(() =>
        {
            if (patternPainter != null)
                patternPainter.UndoLastStroke();
            instructionText.text = "Last stroke undone.";
        });
        RegisterHitTarget(undoBtn, false, false, Color.white);

        Button redoBtn = CreateButton(
            "TargetingRedo", barRect, new Vector2(96f, 0f),
            new Vector2(84f, 48f), "REDO", PillPink);
        redoBtn.onClick.AddListener(() =>
        {
            if (patternPainter != null)
                patternPainter.RedoLastStroke();
            instructionText.text = "Last stroke redone.";
        });
        RegisterHitTarget(redoBtn, false, false, Color.white);

        Button clearBtn = CreateButton(
            "TargetingClear", barRect, new Vector2(186f, 0f),
            new Vector2(92f, 48f), "CLEAR", PillPink);
        clearBtn.onClick.AddListener(() =>
        {
            if (patternPainter != null)
                patternPainter.ClearAllStrokes();
            instructionText.text = "All drawings cleared.";
        });
        RegisterHitTarget(clearBtn, false, false, Color.white);
        bandTargetingBar.SetActive(false);
    }

    private void DriveBandDrawing(Vector2 screenPosition, bool hasHand, HitTarget target)
    {
        if (patternPainter == null) return;
        bool overUi = target != null || IsCursorOverColorUi(screenPosition);
        patternPainter.SetPaintingEnabled(!overUi);
        if (!overUi)
            patternPainter.ProcessPointer(screenPosition, hasHand, Input.GetMouseButton(0));
    }

    private void ShowSelfieCapture()
    {
        DisableVertexSculpt();
        mode = ColorMode.Selfie;
        panel.SetActive(false);
        selfiePanel.SetActive(true);
        if (capturedSelfieTexture == null && appliedSelfieTexture != null)
            capturedSelfieTexture = Instantiate(appliedSelfieTexture);

        bool hasPhoto = capturedSelfieTexture != null;
        SetSelfieReviewControls(hasPhoto);
        if (selfieInstructionText != null)
        {
            selfieInstructionText.text = hasPhoto
                ? appliedSelfieTexture != null
                    ? "Remove the selfie, retake it, or press DONE."
                    : "Attach the photo to the keris, retake it, or press DONE."
                : "Frame your photo, then point and hold TAKE SELFIE.";
        }

        ShowLiveSelfiePreview();
        SetSculptGesturesEnabled(false);
    }

    private void ShowLiveSelfiePreview()
    {
        if (selfiePreviewImage == null) return;
        if (capturedSelfieTexture != null)
        {
            SetSelfiePreviewTexture(capturedSelfieTexture);
        }
        else
        {
            var imageSource = ImageSourceProvider.ImageSource;
            if (imageSource != null && imageSource.isPrepared)
            {
                SetSelfiePreviewTexture(imageSource.GetCurrentTexture());
            }
        }
    }

    private void CaptureSelfie()
    {
        var imageSource = ImageSourceProvider.ImageSource;
        if (imageSource == null || !imageSource.isPrepared)
        {
            if (selfieInstructionText != null) selfieInstructionText.text = "Camera is not ready yet.";
            return;
        }
        
        Texture source = imageSource.GetCurrentTexture();
        Texture2D readable = MorphicOverlayShaders.CaptureWebcamFrame(source);
        if (readable == null)
        {
            if (selfieInstructionText != null) selfieInstructionText.text = "Could not capture camera image.";
            return;
        }

        var transformOptions = imageSource.GetTransformationOptions();
        Texture2D result = TransformCapturedTexture(
            readable,
            !transformOptions.flipHorizontally,
            false,
            (int)transformOptions.rotationAngle);
        
        DestroyRuntime(readable);
        DestroyRuntime(capturedSelfieTexture);
        capturedSelfieTexture = result;
        SetSelfiePreviewTexture(capturedSelfieTexture);
        SetSelfieReviewControls(true);
        if (selfieInstructionText != null)
            selfieInstructionText.text = "Attach the photo to the keris, retake it, or press DONE.";
    }

    private void BeginSelfieRetake()
    {
        DestroyRuntime(capturedSelfieTexture);
        capturedSelfieTexture = null;
        SetSelfieReviewControls(false);
        ShowLiveSelfiePreview();
        if (selfieInstructionText != null)
            selfieInstructionText.text = "Frame your photo, then point and hold TAKE SELFIE.";
    }

    private void KeepSelfie()
    {
        if (capturedSelfieTexture != null)
        {
            DestroyRuntime(undoneSelfieTexture);
            undoneSelfieTexture = null;
            NoteTimedUndo(TimedUndo.Selfie);
            DestroyRuntime(appliedSelfieTexture);
            appliedSelfieTexture = Instantiate(capturedSelfieTexture);
            SyncSelfieSurfaceRenderer();
            ConfigureSelfieSurfaceMaterial(appliedSelfieTexture);
        }
        ShowMainMenu();
    }

    private void RemoveSelfie()
    {
        if (appliedSelfieTexture != null)
        {
            TryUndoSelfie();
            ForgetTimedSelfie();
        }
        DestroyRuntime(capturedSelfieTexture);
        capturedSelfieTexture = null;
        SetSelfieReviewControls(false);
        ShowLiveSelfiePreview();
        if (selfieInstructionText != null)
            selfieInstructionText.text = "Selfie removed. Take a new one, or press BACK.";
    }

    private void ForgetTimedSelfie()
    {
        for (int i = timedUndo.Count - 1; i >= 0; i--)
        {
            if (timedUndo[i] != TimedUndo.Selfie)
                continue;
            timedUndo.RemoveAt(i);
            timedRedo.Add(TimedUndo.Selfie);
            break;
        }
    }

    private void ShowReview()
    {
        DisableVertexSculpt();
        SetSculptGesturesEnabled(false);
        mode = ColorMode.Review;
        SetPanelHeight(ReviewPanelHeight);
        panel.SetActive(true);
        mainActionGroup.SetActive(false);
        if (historyBar != null) historyBar.SetActive(false);
        HideExtraGroups();
        HideReviewAndShelf();
        if (reviewGroup != null)
            reviewGroup.SetActive(true);
        if (selfiePanel != null) selfiePanel.SetActive(false);
        if (selfieEntryButton != null)
            selfieEntryButton.gameObject.SetActive(false);
        headerText.text = "REVIEW";
        instructionText.gameObject.SetActive(true);
        instructionText.text = "Go back, or put this keris on the shelf.";
        SetHeaderBackLabel("← PALETTE");
        SetHeaderBackVisible(true);
        SetTitleBurstsVisible(false);
        ResetDwell();
        SetHoveredTarget(null);
    }

    private void ShowShelf()
    {
        DisableVertexSculpt();
        SetSculptGesturesEnabled(false);
        mode = ColorMode.Shelf;
        SetPanelHeight(ShelfPanelHeight);
        panel.SetActive(true);
        mainActionGroup.SetActive(false);
        if (historyBar != null) historyBar.SetActive(false);
        HideExtraGroups();
        HideReviewAndShelf();
        if (shelfGroup != null)
            shelfGroup.SetActive(true);
        if (selfiePanel != null) selfiePanel.SetActive(false);
        if (selfieEntryButton != null)
            selfieEntryButton.gameObject.SetActive(false);
        headerText.text = "SHELF";
        instructionText.gameObject.SetActive(true);
        instructionText.text = "Take it back, delete it, or go back when you are done.";
        SetHeaderBackLabel("← REVIEW");
        SetHeaderBackVisible(true);
        SetTitleBurstsVisible(false);
        ResetDwell();
        SetHoveredTarget(null);
    }

    private void HideReviewAndShelf()
    {
        if (reviewGroup != null) reviewGroup.SetActive(false);
        if (shelfGroup != null) shelfGroup.SetActive(false);
        if (confirmDeleteGroup != null) confirmDeleteGroup.SetActive(false);
    }


    private void FinishToShelf()
    {
        EnsureKerisShelf();
        if (kerisShelf == null || ResolveKerisRoot() == null)
        {
            if (instructionText != null)
                instructionText.text = "The shelf is not ready yet.";
            return;
        }

        if (KerisCollectionStore.IsFull)
        {
            if (instructionText != null)
                instructionText.text = "The shelf is full.";
            return;
        }

        if (!CommitShelfCopy())
        {
            if (instructionText != null)
                instructionText.text = "Could not place this keris on the shelf.";
            return;
        }

        SnapRestoreVertexSculptCamera();
        SnapViewToPotteryStudio();
        SetWorkingKerisVisible(false);
        Transform seat = GetKerisSlot(KerisCollectionStore.Count - 1);
        if (kerisShelf != null)
            kerisShelf.PanToShelfView(seat != null ? seat : kerisShelf.Slot);
        ShowShelf();
    }

    private void TakeBackFromShelf()
    {
        KerisCollectionStore.RemoveLast();
        RemoveLastKerisDisplay();
        if (kerisShelf != null)
            kerisShelf.RestoreSculptView();
        SetWorkingKerisVisible(true);
        ShowMainMenu();
    }

    private void LeaveShelfDone()
    {
        SetWorkingKerisVisible(true);
        ResetKerisColor();
        if (kerisShelf != null)
            kerisShelf.RestoreSculptView();
        if (paletteAvailable)
            ShowMainMenu();
        else
            SetSculptGesturesEnabled(true);
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
        instructionText.text = "This removes the keris from the shelf.";
        SetHeaderBackVisible(true);
        ResetDwell();
        SetHoveredTarget(null);
    }

    private void ConfirmDeleteKeris()
    {
        KerisCollectionStore.RemoveLast();
        RemoveLastKerisDisplay();
        if (kerisShelf != null)
            kerisShelf.RestoreSculptView();
        SetWorkingKerisVisible(true);
        if (paletteAvailable)
            ShowMainMenu();
        else
            SetSculptGesturesEnabled(true);
    }

    private void EnsureKerisShelf()
    {
        if (kerisShelf != null)
        {
            EnsureKerisShelfSlots();
            return;
        }

        kerisShelf = FindFirstObjectByType<LanternShelfController>(FindObjectsInactive.Include);
        if (kerisShelf != null)
        {
            kerisShelf.BindPieces();
            EnsureKerisShelfSlots();
            return;
        }

        GameObject root = new GameObject(LanternShelfController.ShelfRootName);
        kerisShelf = root.AddComponent<LanternShelfController>();
        kerisShelf.BindPieces();
        EnsureKerisShelfSlots();
    }

    private Transform ResolveKerisRoot()
    {
        if (kerisSkin != null)
        {
            Transform current = kerisSkin.transform;
            while (current != null)
            {
                string objectName = current.name;
                if (objectName == "Keris_Updated" || objectName == "Keris")
                    return current;
                current = current.parent;
            }
        }

        if (kerisRenderer != null)
            return kerisRenderer.transform;
        return transform;
    }

    private bool CommitShelfCopy()
    {
        Transform source = ResolveKerisRoot();
        if (source == null)
            return false;

        ApplySavedPartColors();
        if (appliedDesignTexture != null)
            ApplyDesignTint(committedDesignTint);

        KerisCollectionStore.Record record = CaptureCollectionRecord();
        if (!KerisCollectionStore.Add(record))
            return false;

        int slotIndex = KerisCollectionStore.Count - 1;
        GameObject clone = Instantiate(source.gameObject);
        clone.name = "KerisCollection_" + record.Id;
        clone.SetActive(true);
        clone.transform.SetPositionAndRotation(source.position, source.rotation);
        UniqueCloneMeshes(clone);
        UniqueCloneMaterials(clone);
        CopyLookOntoClone(clone);
        StripGameplay(clone);
        SeatKerisAtOwnSlot(clone.transform, slotIndex);
        ForceCloneVisible(clone);
        if (kerisShelf != null)
            kerisShelf.SetPlacedDisplay(clone.transform);
        kerisShelfDisplays.Add(clone);
        return true;
    }

    private KerisCollectionStore.Record CaptureCollectionRecord()
    {
        KerisSnapshot snapshot = CaptureSnapshot();
        Transform root = ResolveKerisRoot();
        Quaternion rotation = root != null ? root.rotation : Quaternion.identity;
        return new KerisCollectionStore.Record
        {
            Id = Guid.NewGuid().ToString("N"),
            BlendWeights = snapshot.BlendWeights,
            BladeColor = committedKerisColor,
            PartColors = snapshot.PartColors,
            LocalRotation = new[] { rotation.x, rotation.y, rotation.z, rotation.w },
            StampVisible = snapshot.StampVisible,
            StampName = appliedDesignTexture != null ? appliedDesignTexture.name : "",
            StampTint = committedDesignTint,
            SelfiePng = appliedSelfieTexture != null ? appliedSelfieTexture.EncodeToPNG() : null
        };
    }

    private void SeatKerisAtOwnSlot(Transform keris, int slotIndex)
    {
        if (keris == null)
            return;

        EnsureKerisShelfSlots();
        Transform seat = GetKerisSlot(slotIndex);
        Transform collection = kerisShelf != null ? kerisShelf.CollectionRoot : null;
        Quaternion upright = keris.rotation;
        Vector3 worldScale = keris.lossyScale;
        if (collection != null)
            keris.SetParent(collection, true);
        keris.rotation = upright;
        KeepWorldScale(keris, worldScale);

        Bounds body = GetCloneBodyWorldBounds(keris);
        Vector3 currentBottom = new Vector3(body.center.x, body.min.y, body.center.z);
        Vector3 slotPos = seat != null ? seat.position : keris.position;
        keris.position += slotPos - currentBottom;
    }

    private Transform GetKerisSlot(int slotIndex)
    {
        EnsureKerisShelfSlots();
        if (kerisSlots == null || kerisSlots.Length == 0)
            return null;
        return kerisSlots[Mathf.Clamp(slotIndex, 0, kerisSlots.Length - 1)];
    }

    private void EnsureKerisShelfSlots()
    {
        List<Transform> found = FindNamedSlots(KerisSlotName);
        found.Sort((a, b) => SlotSortKey(a.name, KerisSlotName).CompareTo(SlotSortKey(b.name, KerisSlotName)));

        Transform parent = kerisShelf != null ? kerisShelf.transform : null;
        List<Transform> teapotSlots = FindNamedSlots("TeapotShelfSlot");
        teapotSlots.Sort((a, b) =>
            SlotSortKey(a.name, "TeapotShelfSlot").CompareTo(SlotSortKey(b.name, "TeapotShelfSlot")));
        List<Transform> lanternSlots = FindNamedSlots(LanternShelfController.SlotName);
        lanternSlots.Sort((a, b) =>
            SlotSortKey(a.name, LanternShelfController.SlotName)
                .CompareTo(SlotSortKey(b.name, LanternShelfController.SlotName)));

        if (parent == null && teapotSlots.Count > 0)
            parent = teapotSlots[0].parent;
        if (parent == null && lanternSlots.Count > 0)
            parent = lanternSlots[0].parent;
        if (parent == null)
            parent = transform;

        for (int i = found.Count; i < KerisSlotCount; i++)
        {
            GameObject slotObject = new GameObject(i == 0 ? KerisSlotName : KerisSlotName + "_" + (i + 1));
            Transform slot = slotObject.transform;
            slot.SetParent(parent, true);
            Transform template = i < teapotSlots.Count && teapotSlots[i] != null
                ? teapotSlots[i]
                : i < lanternSlots.Count ? lanternSlots[i] : null;
            if (template != null)
            {
                slot.position = template.position + template.forward * 0.4f;
                slot.rotation = template.rotation;
            }
            else
            {
                slot.position = parent.position + Vector3.right * (0.45f * i);
                slot.rotation = Quaternion.identity;
            }

            found.Add(slot);
        }

        kerisSlots = found.ToArray();
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

    private void SetWorkingKerisVisible(bool visible)
    {
        workingKerisVisible = visible;
        Transform root = ResolveKerisRoot();
        if (root != null)
        {
            Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);
            for (int i = 0; i < renderers.Length; i++)
            {
                if (renderers[i] == null || IsShelfClone(renderers[i].transform))
                    continue;
                renderers[i].enabled = visible;
            }
        }

        if (designSurfaceRoot != null && !IsShelfClone(designSurfaceRoot))
        {
            bool showStamp = visible && appliedDesignTexture != null;
            designSurfaceRoot.gameObject.SetActive(showStamp);
            if (designSurfaceRenderer != null)
                designSurfaceRenderer.enabled = showStamp;
        }

        SetSelfieCirclesVisible(visible && appliedSelfieTexture != null);
    }

    private static bool IsShelfClone(Transform target)
    {
        Transform current = target;
        while (current != null)
        {
            if (current.name.StartsWith("KerisCollection_", StringComparison.Ordinal))
                return true;
            current = current.parent;
        }
        return false;
    }

    private static void KeepWorldScale(Transform target, Vector3 worldScale)
    {
        if (target == null || target.parent == null)
        {
            if (target != null)
                target.localScale = worldScale;
            return;
        }

        Vector3 parentScale = target.parent.lossyScale;
        target.localScale = new Vector3(
            SafeDivide(worldScale.x, parentScale.x),
            SafeDivide(worldScale.y, parentScale.y),
            SafeDivide(worldScale.z, parentScale.z));
    }

    private static float SafeDivide(float value, float divisor)
    {
        return Mathf.Abs(divisor) < 0.0001f ? value : value / divisor;
    }

    private void RemoveLastKerisDisplay()
    {
        if (kerisShelfDisplays.Count == 0)
            return;
        GameObject last = kerisShelfDisplays[kerisShelfDisplays.Count - 1];
        kerisShelfDisplays.RemoveAt(kerisShelfDisplays.Count - 1);
        if (last != null)
            Destroy(last);
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

    private static void UniqueCloneMeshes(GameObject clone)
    {
        if (clone == null)
            return;

        SkinnedMeshRenderer[] skins = clone.GetComponentsInChildren<SkinnedMeshRenderer>(true);
        for (int i = 0; i < skins.Length; i++)
        {
            if (skins[i] == null || skins[i].sharedMesh == null)
                continue;
            float[] weights = null;
            Mesh sourceMesh = skins[i].sharedMesh;
            if (sourceMesh.blendShapeCount > 0)
            {
                weights = new float[sourceMesh.blendShapeCount];
                for (int w = 0; w < weights.Length; w++)
                    weights[w] = skins[i].GetBlendShapeWeight(w);
            }

            skins[i].sharedMesh = Instantiate(sourceMesh);
            if (weights == null)
                continue;
            int count = Mathf.Min(weights.Length, skins[i].sharedMesh.blendShapeCount);
            for (int w = 0; w < count; w++)
                skins[i].SetBlendShapeWeight(w, weights[w]);
        }

        MeshFilter[] filters = clone.GetComponentsInChildren<MeshFilter>(true);
        for (int i = 0; i < filters.Length; i++)
        {
            if (filters[i] == null || filters[i].sharedMesh == null)
                continue;
            filters[i].sharedMesh = Instantiate(filters[i].sharedMesh);
        }
    }

    private static void StripGameplay(GameObject clone)
    {
        if (clone == null)
            return;

        DisableAndDestroy(clone.GetComponentsInChildren<KerisGestureController>(true));
        DisableAndDestroy(clone.GetComponentsInChildren<KerisFistRotationController>(true));
        DisableAndDestroy(clone.GetComponentsInChildren<SculptingEnhancementKerisColorController>(true));
        DisableAndDestroy(clone.GetComponentsInChildren<VertexSculptController>(true));
        DisableAndDestroy(clone.GetComponentsInChildren<TeapotSurfacePatternPainter>(true));
        DestroyNamedChildren(clone.transform, "BrushCursor");
        DestroyNamedChildren(clone.transform, "KerisColorUI");
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

    private void CopyLookOntoClone(GameObject clone)
    {
        if (clone == null)
            return;

        Transform stamp = FindNamedChild(clone.transform, "KerisStampOverlay");
        if (stamp != null)
        {
            bool showStamp = appliedDesignTexture != null;
            stamp.gameObject.SetActive(showStamp);
            MeshRenderer stampRenderer = stamp.GetComponent<MeshRenderer>();
            if (stampRenderer != null)
            {
                stampRenderer.enabled = showStamp;
                if (showStamp)
                    AssignUniqueTexture(stampRenderer, tintedDesignTexture != null
                        ? tintedDesignTexture
                        : appliedDesignTexture, "_DesignTex");
            }
        }

        if (appliedSelfieTexture == null || selfieSurfaceRoot == null)
            return;

        GameObject selfie = Instantiate(selfieSurfaceRoot.gameObject);
        selfie.name = "KerisSelfieCircle0";
        selfie.transform.SetParent(clone.transform, true);
        MeshFilter filter = selfie.GetComponent<MeshFilter>();
        if (filter != null && filter.sharedMesh != null)
            filter.sharedMesh = Instantiate(filter.sharedMesh);
        MeshRenderer meshRenderer = selfie.GetComponent<MeshRenderer>();
        if (meshRenderer != null)
        {
            meshRenderer.materials = meshRenderer.materials;
            AssignUniqueTexture(meshRenderer, appliedSelfieTexture, "_MainTex");
            AssignUniqueTexture(meshRenderer, appliedSelfieTexture, "_BaseMap");
            meshRenderer.enabled = true;
        }
        selfie.SetActive(true);
    }

    private static void AssignUniqueTexture(Renderer target, Texture source, string property)
    {
        if (target == null || source == null)
            return;
        Texture2D pixels = source as Texture2D;
        Texture2D copy = pixels != null ? CopyTexturePixels(pixels, "Morphic Shelf " + property) : null;
        Material[] materials = target.materials;
        for (int i = 0; i < materials.Length; i++)
        {
            if (materials[i] == null)
                continue;
            if (materials[i].HasProperty(property))
                materials[i].SetTexture(property, copy != null ? copy : source);
            if (property == "_MainTex" && materials[i].HasProperty("_BaseMap"))
                materials[i].SetTexture("_BaseMap", copy != null ? copy : source);
        }
        target.materials = materials;
    }

    private static Texture2D CopyTexturePixels(Texture2D source, string textureName)
    {
        if (source == null)
            return null;
        Texture2D copy = new Texture2D(source.width, source.height, TextureFormat.RGBA32, false)
        {
            name = textureName,
            filterMode = source.filterMode,
            wrapMode = source.wrapMode
        };
        try
        {
            copy.SetPixels32(source.GetPixels32());
            copy.Apply(false, false);
            return copy;
        }
        catch
        {
            Destroy(copy);
            return source;
        }
    }

    private static Transform FindNamedChild(Transform root, string objectName)
    {
        if (root == null)
            return null;
        Transform[] children = root.GetComponentsInChildren<Transform>(true);
        for (int i = 0; i < children.Length; i++)
        {
            if (children[i] != null && children[i].name == objectName)
                return children[i];
        }
        return null;
    }

    private static void ForceCloneVisible(GameObject clone)
    {
        if (clone == null)
            return;
        clone.SetActive(true);
        Renderer[] renderers = clone.GetComponentsInChildren<Renderer>(true);
        for (int i = 0; i < renderers.Length; i++)
        {
            if (renderers[i] == null)
                continue;
            renderers[i].enabled = true;
            renderers[i].forceRenderingOff = false;
        }
    }

    private void ShowConfirmReset()
    {
        DisableVertexSculpt();
        mode = ColorMode.ConfirmReset;
        HideExtraGroups();
        mainActionGroup.SetActive(false);
        confirmResetGroup.SetActive(true);
        SetHeaderBackVisible(true);
        instructionText.gameObject.SetActive(false);
    }

    private void HideConfirmReset() => ShowMainMenu();

    private void ClearWorkingSelfieAndDesign()
    {
        DestroyRuntime(capturedSelfieTexture);
        DestroyRuntime(appliedSelfieTexture);
        DestroyRuntime(undoneSelfieTexture);
        capturedSelfieTexture = null;
        appliedSelfieTexture = null;
        undoneSelfieTexture = null;
        appliedDesignTexture = null;
        SetSelfieCirclesVisible(false);
        DestroySelfieCircles();
        if (selfiePreviewImage != null)
            selfiePreviewImage.texture = null;
        if (selfieSurfaceRenderer != null)
            selfieSurfaceRenderer.enabled = false;
        if (designSurfaceRenderer != null) designSurfaceRenderer.enabled = false;
        if (designSurfaceRoot != null) designSurfaceRoot.gameObject.SetActive(false);
        if (patternPainter != null)
        {
            patternPainter.ClearAllStrokes();
            patternPainter.ClearClayCoils();
        }
    }

    private bool TryUndoSelfie()
    {
        if (appliedSelfieTexture == null)
            return false;

        DestroyRuntime(undoneSelfieTexture);
        undoneSelfieTexture = appliedSelfieTexture;
        appliedSelfieTexture = null;
        SetSelfieCirclesVisible(false);
        if (selfieSurfaceRenderer != null)
            selfieSurfaceRenderer.enabled = false;
        if (instructionText != null)
        {
            instructionText.gameObject.SetActive(true);
            instructionText.text = "Selfie removed.";
        }
        return true;
    }

    private bool TryRedoSelfie()
    {
        if (undoneSelfieTexture == null)
            return false;

        appliedSelfieTexture = undoneSelfieTexture;
        undoneSelfieTexture = null;
        SyncSelfieSurfaceRenderer();
        ConfigureSelfieSurfaceMaterial(appliedSelfieTexture);
        SetSelfieCirclesVisible(true);
        if (instructionText != null)
        {
            instructionText.gameObject.SetActive(true);
            instructionText.text = "Selfie restored.";
        }
        return true;
    }

    private Color LiveDesignTint()
    {
        if (designTintActive && hoveredTarget != null && hoveredTarget.IsColor)
        {
            return hoveredTarget.IsFreeColorPalette
                ? freePalettePreviewColor
                : hoveredTarget.PreviewColor;
        }
        return committedDesignTint;
    }

    private Texture ResolveTintedDesign(Texture source, Color tint)
    {
        if (source == null)
            return tintedDesignTexture;
        if (tintedDesignTexture != null &&
            lastBakedDesignSource == source &&
            Mathf.Abs(lastBakedDesignTint.r - tint.r) < 0.002f &&
            Mathf.Abs(lastBakedDesignTint.g - tint.g) < 0.002f &&
            Mathf.Abs(lastBakedDesignTint.b - tint.b) < 0.002f)
            return tintedDesignTexture;

        tintedDesignTexture = MorphicOverlayShaders.BakeInkTint(
            source, tint, tintedDesignTexture, TextureWrapMode.Clamp, TextureWrapMode.Clamp);
        lastBakedDesignSource = source;
        lastBakedDesignTint = tint;
        return tintedDesignTexture;
    }

    private void ApplyDesignTint(Color color)
    {
        color.a = 1f;
        Texture painted = null;
        if (appliedDesignTexture != null)
            painted = ResolveTintedDesign(appliedDesignTexture, color);

        if (designSurfaceMaterial != null)
        {
            designSurfaceMaterial.enableInstancing = false;
            if (painted != null)
            {
                if (designSurfaceMaterial.HasProperty("_DesignTex"))
                    designSurfaceMaterial.SetTexture("_DesignTex", painted);
                if (designSurfaceMaterial.HasProperty("_MainTex"))
                    designSurfaceMaterial.SetTexture("_MainTex", painted);
                if (designSurfaceMaterial.HasProperty("_BaseMap"))
                    designSurfaceMaterial.SetTexture("_BaseMap", painted);
                designSurfaceMaterial.mainTexture = painted;
            }
            if (designSurfaceMaterial.HasProperty("_DesignColor"))
                designSurfaceMaterial.SetColor("_DesignColor", Color.white);
            if (designSurfaceMaterial.HasProperty("_Color"))
                designSurfaceMaterial.SetColor("_Color", Color.white);
            if (designSurfaceMaterial.HasProperty("_BaseColor"))
                designSurfaceMaterial.SetColor("_BaseColor", Color.white);
            if (designSurfaceMaterial.HasProperty("_Opacity"))
                designSurfaceMaterial.SetFloat("_Opacity", 1f);
            designSurfaceMaterial.color = Color.white;
        }

        if (designSurfaceRenderer == null)
            return;

        designSurfaceRenderer.SetPropertyBlock(null);
        designSurfaceRenderer.sharedMaterial = designSurfaceMaterial;
    }

    private void SyncDesignSurfaceRenderer()
    {
        if (appliedDesignTexture == null)
            return;

        if (kerisRenderer == null)
        {
            if (kerisSkin == null)
                kerisSkin = GetComponentInChildren<SkinnedMeshRenderer>(true);
            kerisRenderer = kerisSkin != null ? kerisSkin : GetComponentInChildren<Renderer>(true);
        }
        if (kerisRenderer == null)
            return;

        EnsureDesignOverlay();
        if (designSurfaceRoot == null)
            return;

        if (designSurfaceRoot.parent != kerisRenderer.transform)
        {
            designSurfaceRoot.SetParent(kerisRenderer.transform, false);
            designSurfaceRoot.localPosition = Vector3.zero;
            designSurfaceRoot.localRotation = Quaternion.identity;
            designSurfaceRoot.localScale = Vector3.one;
        }

        designSurfaceRoot.gameObject.layer = kerisRenderer.gameObject.layer;
        designSurfaceRoot.gameObject.SetActive(true);
        if (designSurfaceRenderer != null)
            designSurfaceRenderer.enabled = true;

        BuildStampTileMesh();
    }

    private void SyncSelfieSurfaceRenderer()
    {
        if (appliedSelfieTexture == null) return;

        // Resolve the blade renderer lazily so the selfie still attaches even if
        // the keris was bound late or the reference was cleared after a swap.
        if (kerisRenderer == null)
        {
            if (kerisSkin == null)
                kerisSkin = GetComponentInChildren<SkinnedMeshRenderer>(true);
            kerisRenderer = kerisSkin != null ? kerisSkin : GetComponentInChildren<Renderer>(true);
        }
        if (kerisRenderer == null) return;

        EnsureSelfieCircles();
        BuildSelfieTileMesh();
    }

    private void EnsureSelfieCircles()
    {
        if (selfieOverlayMesh == null)
            selfieOverlayMesh = CreateSelfieCircleMesh();

        for (int i = 0; i < SelfieCircleCount; i++)
        {
            if (selfieCircleRoots[i] == null)
            {
                GameObject root = new GameObject("KerisSelfieCircle" + i);
                selfieCircleRoots[i] = root.transform;
                MeshFilter filter = root.AddComponent<MeshFilter>();
                MeshRenderer meshRenderer = root.AddComponent<MeshRenderer>();
                meshRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                meshRenderer.receiveShadows = false;
                meshRenderer.allowOcclusionWhenDynamic = false;
                meshRenderer.sortingOrder = 40;
                meshRenderer.forceRenderingOff = false;
                filter.sharedMesh = selfieOverlayMesh;
                selfieCircleRenderers[i] = meshRenderer;
            }

            if (selfieCircleRoots[i].parent != null)
                selfieCircleRoots[i].SetParent(null, true);
            if (kerisRenderer != null)
                selfieCircleRoots[i].gameObject.layer = kerisRenderer.gameObject.layer;
            selfieCircleRoots[i].gameObject.SetActive(true);
            if (selfieCircleRenderers[i] != null)
                selfieCircleRenderers[i].enabled = true;
        }

        selfieSurfaceRoot = selfieCircleRoots[0];
        selfieSurfaceRenderer = selfieCircleRenderers[0];
        DestroyExtraSelfieCircles();
    }

    private void SetSelfieCirclesVisible(bool visible)
    {
        for (int i = 0; i < SelfieCircleCount; i++)
        {
            if (selfieCircleRoots[i] != null)
                selfieCircleRoots[i].gameObject.SetActive(visible);
        }
        if (selfieSurfaceRoot != null)
            selfieSurfaceRoot.gameObject.SetActive(visible);
    }

    private void DestroySelfieCircles()
    {
        for (int i = 0; i < SelfieCircleCount; i++)
        {
            if (selfieCircleRoots[i] != null)
                Destroy(selfieCircleRoots[i].gameObject);
            selfieCircleRoots[i] = null;
            selfieCircleRenderers[i] = null;
        }
        DestroyExtraSelfieCircles();
        selfieSurfaceRoot = null;
        selfieSurfaceRenderer = null;
    }

    private static void DestroyExtraSelfieCircles()
    {
        Transform[] transforms = FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (int i = 0; i < transforms.Length; i++)
        {
            Transform t = transforms[i];
            if (t == null || !t.name.StartsWith("KerisSelfieCircle", StringComparison.Ordinal))
                continue;
            string suffix = t.name.Substring("KerisSelfieCircle".Length);
            int index;
            if (!int.TryParse(suffix, out index) || index < SelfieCircleCount)
                continue;
            if (Application.isPlaying)
                UnityEngine.Object.Destroy(t.gameObject);
            else
                UnityEngine.Object.DestroyImmediate(t.gameObject);
        }
    }

    // The blade length is the model's longest world-space axis and its thickness
    // the shortest; measuring in world space keeps the choice correct when the
    // imported transform is rotated or non-uniformly scaled.
    private static void ChooseBladeAxes(Transform blade, Bounds localBounds,
        out Vector3 worldLong, out Vector3 worldThin)
    {
        Vector3 size = localBounds.size;
        float wx = blade.TransformVector(new Vector3(size.x, 0f, 0f)).magnitude;
        float wy = blade.TransformVector(new Vector3(0f, size.y, 0f)).magnitude;
        float wz = blade.TransformVector(new Vector3(0f, 0f, size.z)).magnitude;

        Vector3 longLocal;
        if (wx >= wy && wx >= wz) longLocal = Vector3.right;
        else if (wy >= wz) longLocal = Vector3.up;
        else longLocal = Vector3.forward;

        Vector3 thinLocal;
        if (wx <= wy && wx <= wz) thinLocal = Vector3.right;
        else if (wy <= wz) thinLocal = Vector3.up;
        else thinLocal = Vector3.forward;

        if (thinLocal == longLocal)
            thinLocal = Vector3.Cross(longLocal, Vector3.up).sqrMagnitude > 0.01f
                ? Vector3.Cross(longLocal, Vector3.up).normalized
                : Vector3.Cross(longLocal, Vector3.right).normalized;

        worldLong = blade.TransformDirection(longLocal).normalized;
        worldThin = blade.TransformDirection(thinLocal).normalized;
    }

    private static Camera ResolveSelfieCamera()
    {
        if (Camera.main != null) return Camera.main;
        Camera[] cameras = FindObjectsByType<Camera>(FindObjectsSortMode.None);
        if (cameras == null || cameras.Length == 0) return null;
        for (int i = 0; i < cameras.Length; i++)
            if (cameras[i] != null && cameras[i].enabled) return cameras[i];
        return cameras[0];
    }

    private static Vector3 SelfieViewDirection(Transform blade, Vector3[] localVertices)
    {
        Camera camera = ResolveSelfieCamera();
        if (camera == null) return blade.right;

        Vector3 sum = Vector3.zero;
        int step = Mathf.Max(1, localVertices.Length / 64);
        int samples = 0;
        for (int i = 0; i < localVertices.Length; i += step)
        {
            sum += localVertices[i];
            samples++;
        }

        Vector3 center = blade.TransformPoint(sum / Mathf.Max(1, samples));
        Vector3 direction = camera.transform.position - center;
        return direction.sqrMagnitude > 0.0001f ? direction.normalized : blade.right;
    }

    private void WarnSelfie(string message)
    {
        if (selfieWarnTimer > 0f) return;
        selfieWarnTimer = 2f;
        Debug.LogWarning("MORPHIC: " + message, this);
    }

    private static Mesh CreateSelfieCircleMesh()
    {
        const int segments = SelfieCircleSegments;
        var mesh = new Mesh { name = "KerisSelfieCircle" };
        var vertices = new Vector3[segments + 1];
        var normals = new Vector3[segments + 1];
        var uvs = new Vector2[segments + 1];
        var triangles = new int[segments * 3];

        vertices[0] = Vector3.zero;
        normals[0] = Vector3.forward;
        uvs[0] = new Vector2(0.5f, 0.5f);
        for (int i = 0; i < segments; i++)
        {
            float angle = (i / (float)segments) * Mathf.PI * 2f;
            float x = Mathf.Cos(angle) * 0.5f;
            float y = Mathf.Sin(angle) * 0.5f;
            vertices[i + 1] = new Vector3(x, y, 0f);
            normals[i + 1] = Vector3.forward;
            uvs[i + 1] = new Vector2(x + 0.5f, y + 0.5f);
            triangles[i * 3 + 0] = 0;
            triangles[i * 3 + 1] = i + 1;
            triangles[i * 3 + 2] = i + 2 <= segments ? i + 2 : 1;
        }

        mesh.vertices = vertices;
        mesh.normals = normals;
        mesh.uv = uvs;
        mesh.triangles = triangles;
        mesh.RecalculateBounds();
        return mesh;
    }

    // Places a rigid circular selfie at the current mid-blade spot. The disc
    // stays circular when the keris curves; only its position follows the metal.
    private bool BuildSelfieStripMesh()
    {
        SkinnedMeshRenderer skin = kerisSkin != null ? kerisSkin : kerisRenderer as SkinnedMeshRenderer;
        Transform blade = skin != null ? skin.transform : (kerisRenderer != null ? kerisRenderer.transform : null);
        if (blade == null)
            return false;

        Vector3[] vertices = null;
        if (skin != null)
        {
            if (selfieBoundsMesh == null)
                selfieBoundsMesh = new Mesh { name = "KerisSelfieBake" };
            selfieBoundsMesh.Clear();
            skin.BakeMesh(selfieBoundsMesh);
            if (selfieBoundsMesh.vertexCount > 0)
                vertices = selfieBoundsMesh.vertices;
        }
        if (vertices == null || vertices.Length < 8)
        {
            Mesh source = skin != null ? skin.sharedMesh : null;
            if (source == null && kerisRenderer != null)
            {
                MeshFilter filter = kerisRenderer.GetComponent<MeshFilter>();
                source = filter != null ? filter.sharedMesh : null;
            }
            if (source != null)
                vertices = source.vertices;
        }
        if (vertices == null || vertices.Length < 8)
            return false;

        Mesh boundsSource = skin != null && skin.sharedMesh != null
            ? skin.sharedMesh
            : selfieBoundsMesh;
        ChooseBladeAxes(blade, boundsSource != null ? boundsSource.bounds : new Bounds(Vector3.zero, Vector3.one),
            out Vector3 worldLong, out Vector3 worldThin);
        Vector3 view = SelfieViewDirection(blade, vertices);
        if (Vector3.Dot(worldThin, view) < 0f)
            worldThin = -worldThin;
        Vector3 worldAcross = Vector3.Cross(worldThin, worldLong);
        if (worldAcross.sqrMagnitude < 0.0001f)
            return false;
        worldAcross.Normalize();

        Camera camera = ResolveSelfieCamera();
        Vector3 up = worldLong;
        if (camera != null && Vector3.Dot(up, camera.transform.up) < 0f)
            up = -up;

        float alongMin = float.MaxValue, alongMax = float.MinValue;
        float thinMin = float.MaxValue, thinMax = float.MinValue;
        var alongAt = new float[vertices.Length];
        var acrossAt = new float[vertices.Length];
        var thinAt = new float[vertices.Length];
        var worldPoints = new Vector3[vertices.Length];
        for (int i = 0; i < vertices.Length; i++)
        {
            Vector3 worldPoint = blade.TransformPoint(vertices[i]);
            worldPoints[i] = worldPoint;
            alongAt[i] = Vector3.Dot(worldPoint, worldLong);
            acrossAt[i] = Vector3.Dot(worldPoint, worldAcross);
            thinAt[i] = Vector3.Dot(worldPoint, worldThin);
            if (alongAt[i] < alongMin) alongMin = alongAt[i];
            if (alongAt[i] > alongMax) alongMax = alongAt[i];
            if (thinAt[i] < thinMin) thinMin = thinAt[i];
            if (thinAt[i] > thinMax) thinMax = thinAt[i];
        }

        float alongRange = alongMax - alongMin;
        if (alongRange < 0.0001f)
            return false;

        float targetAlong = alongMin + alongRange * 0.48f;
        float window = alongRange * 0.10f;
        float frontCut = thinMin + (thinMax - thinMin) * 0.78f;

        Vector3 sum = Vector3.zero;
        int hits = 0;
        float acrossMin = float.MaxValue;
        float acrossMax = float.MinValue;
        int best = -1;
        float bestFacing = float.MinValue;
        for (int i = 0; i < vertices.Length; i++)
        {
            if (Mathf.Abs(alongAt[i] - targetAlong) > window)
                continue;
            if (acrossAt[i] < acrossMin) acrossMin = acrossAt[i];
            if (acrossAt[i] > acrossMax) acrossMax = acrossAt[i];
            if (thinAt[i] > bestFacing)
            {
                bestFacing = thinAt[i];
                best = i;
            }
            if (thinAt[i] < frontCut)
                continue;
            sum += worldPoints[i];
            hits++;
        }

        if (hits < 1 && best >= 0)
        {
            sum = worldPoints[best];
            hits = 1;
        }

        if (hits < 1)
        {
            window = alongRange * 0.28f;
            sum = Vector3.zero;
            hits = 0;
            acrossMin = float.MaxValue;
            acrossMax = float.MinValue;
            best = -1;
            bestFacing = float.MinValue;
            for (int i = 0; i < vertices.Length; i++)
            {
                if (Mathf.Abs(alongAt[i] - targetAlong) > window)
                    continue;
                if (acrossAt[i] < acrossMin) acrossMin = acrossAt[i];
                if (acrossAt[i] > acrossMax) acrossMax = acrossAt[i];
                if (thinAt[i] > bestFacing)
                {
                    bestFacing = thinAt[i];
                    best = i;
                }
                sum += worldPoints[i];
                hits++;
            }
            if (best >= 0)
            {
                sum = worldPoints[best];
                hits = 1;
            }
        }

        if (hits < 1)
            return false;

        float localWidth = Mathf.Max(acrossMax - acrossMin, alongRange * 0.08f, 0.0004f);
        float width = localWidth * SelfieCircleFill;
        float height = Mathf.Min(alongRange * 0.28f, Mathf.Max(width * SelfieAlongFill, alongRange * 0.18f));
        // Sit on the camera-facing surface, but in the middle of the blade
        // width. Using the sharpest vertex alone pulled the photo to one edge.
        float acrossMid = (acrossMin + acrossMax) * 0.5f;
        float frontThin = best >= 0 ? bestFacing : Vector3.Dot(sum / hits, worldThin);
        float worldPush = Mathf.Max(0.004f, alongRange * 0.016f);
        Vector3 attach = worldLong * targetAlong +
                         worldAcross * acrossMid +
                         worldThin * (frontThin + worldPush);

        EnsureSelfieCircles();
        Transform circle = selfieCircleRoots[0];
        if (circle == null)
            return false;
        circle.SetParent(null, true);
        circle.position = attach;
        circle.rotation = Quaternion.LookRotation(worldThin, up);
        circle.localScale = new Vector3(width, height, width);
        circle.gameObject.SetActive(true);

        if (lastSelfieTileLog != SelfieCircleCount)
        {
            lastSelfieTileLog = SelfieCircleCount;
            Debug.Log("MORPHIC: Keris selfie attached as " + SelfieCircleCount + " circles on the blade curves.", this);
        }
        return true;
    }

    private static int NearestFilledRow(int row, int first, int last, int[] rowCount, float[] rowMin, float[] rowMax)
    {
        if (row >= first && row <= last && rowCount[row] > 0 && rowMin[row] < rowMax[row])
            return row;
        for (int s = 1; s <= last - first; s++)
        {
            int left = row - s;
            int right = row + s;
            if (left >= first && rowCount[left] > 0 && rowMin[left] < rowMax[left])
                return left;
            if (right <= last && rowCount[right] > 0 && rowMin[right] < rowMax[right])
                return right;
        }
        return Mathf.Clamp(row, first, last);
    }

    private static float ReadKerisCurveAmount(SkinnedMeshRenderer skin)
    {
        if (skin == null || skin.sharedMesh == null)
            return 0f;
        float amount = 0f;
        Mesh mesh = skin.sharedMesh;
        for (int i = 0; i < mesh.blendShapeCount; i++)
        {
            string name = mesh.GetBlendShapeName(i);
            if (string.IsNullOrEmpty(name) || name.IndexOf("curve", StringComparison.OrdinalIgnoreCase) < 0)
                continue;
            amount = Mathf.Max(amount, Mathf.Clamp01(skin.GetBlendShapeWeight(i) / 100f));
        }
        return amount;
    }

    private void BuildSelfieTileMesh()
    {
        if (selfieOverlayMesh == null) return;
        if (BuildSelfieStripMesh())
            return;
        WarnSelfie("Keris selfie could not be placed on the blade.");
        return;

        SkinnedMeshRenderer skin = kerisSkin != null ? kerisSkin : kerisRenderer as SkinnedMeshRenderer;
        Mesh source = null;
        Transform blade = null;
        if (skin != null)
        {
            source = skin.sharedMesh;
            blade = skin.transform;
        }
        else if (kerisRenderer != null)
        {
            MeshFilter filter = kerisRenderer.GetComponent<MeshFilter>();
            source = filter != null ? filter.sharedMesh : null;
            blade = kerisRenderer.transform;
        }

        if (source == null || blade == null)
        {
            selfieOverlayMesh.Clear();
            WarnSelfie("Keris has no blade mesh to attach the selfie to.");
            return;
        }

        Vector3[] vertices;
        Vector3[] vertexNormals;
        if (skin != null)
        {
            if (selfieBoundsMesh == null)
                selfieBoundsMesh = new Mesh { name = "KerisSelfieBake" };
            selfieBoundsMesh.Clear();
            skin.BakeMesh(selfieBoundsMesh);
            bool baked = selfieBoundsMesh.vertexCount == source.vertexCount;
            vertices = baked ? selfieBoundsMesh.vertices : source.vertices;
            vertexNormals = baked ? selfieBoundsMesh.normals : source.normals;
        }
        else
        {
            vertices = source.vertices;
            vertexNormals = source.normals;
        }

        if (vertices == null || vertices.Length < 8)
        {
            selfieOverlayMesh.Clear();
            WarnSelfie("Keris blade mesh is not readable for the selfie.");
            return;
        }
        if (vertexNormals == null || vertexNormals.Length != vertices.Length)
            vertexNormals = null;

        ChooseBladeAxes(blade, source.bounds, out Vector3 worldLong, out Vector3 worldThin);
        Vector3 view = SelfieViewDirection(blade, vertices);
        if (Vector3.Dot(worldThin, view) < 0f)
            worldThin = -worldThin;

        Vector3 worldAcross = Vector3.Cross(worldThin, worldLong);
        if (worldAcross.sqrMagnitude < 0.0001f)
        {
            selfieOverlayMesh.Clear();
            WarnSelfie("Keris blade axes could not be resolved for the selfie.");
            return;
        }
        worldAcross.Normalize();

        int[] sourceTriangles = source.subMeshCount > BladeMaterialSlot
            ? source.GetTriangles(BladeMaterialSlot)
            : source.triangles;
        if (sourceTriangles == null || sourceTriangles.Length < 3)
            sourceTriangles = source.triangles;

        var frontMarks = new bool[vertices.Length];
        var alongAt = new float[vertices.Length];
        var acrossAt = new float[vertices.Length];
        float alongMin = float.MaxValue, alongMax = float.MinValue;
        float acrossMin = float.MaxValue, acrossMax = float.MinValue;
        float thinMin = float.MaxValue, thinMax = float.MinValue;

        for (int i = 0; i < vertices.Length; i++)
        {
            Vector3 worldPoint = blade.TransformPoint(vertices[i]);
            float thin = Vector3.Dot(worldPoint, worldThin);
            if (thin < thinMin) thinMin = thin;
            if (thin > thinMax) thinMax = thin;
            alongAt[i] = Vector3.Dot(worldPoint, worldLong);
            acrossAt[i] = Vector3.Dot(worldPoint, worldAcross);
        }

        for (int i = 0; i < sourceTriangles.Length; i += 3)
        {
            int a = sourceTriangles[i];
            int b = sourceTriangles[i + 1];
            int c = sourceTriangles[i + 2];
            if (a < 0 || b < 0 || c < 0 || a >= vertices.Length || b >= vertices.Length || c >= vertices.Length)
                continue;

            Vector3 worldA = blade.TransformPoint(vertices[a]);
            Vector3 worldB = blade.TransformPoint(vertices[b]);
            Vector3 worldC = blade.TransformPoint(vertices[c]);
            Vector3 faceNormal = Vector3.Cross(worldB - worldA, worldC - worldA);
            if (faceNormal.sqrMagnitude < 0.0000001f)
                continue;
            faceNormal.Normalize();
            if (Vector3.Dot(faceNormal, worldThin) < SelfieFrontDot)
                continue;

            frontMarks[a] = frontMarks[b] = frontMarks[c] = true;
            if (alongAt[a] < alongMin) alongMin = alongAt[a];
            if (alongAt[b] < alongMin) alongMin = alongAt[b];
            if (alongAt[c] < alongMin) alongMin = alongAt[c];
            if (alongAt[a] > alongMax) alongMax = alongAt[a];
            if (alongAt[b] > alongMax) alongMax = alongAt[b];
            if (alongAt[c] > alongMax) alongMax = alongAt[c];
            if (acrossAt[a] < acrossMin) acrossMin = acrossAt[a];
            if (acrossAt[b] < acrossMin) acrossMin = acrossAt[b];
            if (acrossAt[c] < acrossMin) acrossMin = acrossAt[c];
            if (acrossAt[a] > acrossMax) acrossMax = acrossAt[a];
            if (acrossAt[b] > acrossMax) acrossMax = acrossAt[b];
            if (acrossAt[c] > acrossMax) acrossMax = acrossAt[c];
        }

        float alongRange = alongMax - alongMin;
        float acrossRange = acrossMax - acrossMin;
        if (alongRange < 0.0001f || acrossRange < 0.0001f)
        {
            if (sourceTriangles != source.triangles)
            {
                sourceTriangles = source.triangles;
                Array.Clear(frontMarks, 0, frontMarks.Length);
                alongMin = acrossMin = thinMin = float.MaxValue;
                alongMax = acrossMax = thinMax = float.MinValue;
                for (int i = 0; i < sourceTriangles.Length; i += 3)
                {
                    int a = sourceTriangles[i];
                    int b = sourceTriangles[i + 1];
                    int c = sourceTriangles[i + 2];
                    if (a < 0 || b < 0 || c < 0 || a >= vertices.Length || b >= vertices.Length || c >= vertices.Length)
                        continue;
                    Vector3 worldA = blade.TransformPoint(vertices[a]);
                    Vector3 worldB = blade.TransformPoint(vertices[b]);
                    Vector3 worldC = blade.TransformPoint(vertices[c]);
                    Vector3 faceNormal = Vector3.Cross(worldB - worldA, worldC - worldA);
                    if (faceNormal.sqrMagnitude < 0.0000001f)
                        continue;
                    faceNormal.Normalize();
                    if (Vector3.Dot(faceNormal, worldThin) < SelfieFrontDot)
                        continue;
                    frontMarks[a] = frontMarks[b] = frontMarks[c] = true;
                    if (alongAt[a] < alongMin) alongMin = alongAt[a];
                    if (alongAt[b] < alongMin) alongMin = alongAt[b];
                    if (alongAt[c] < alongMin) alongMin = alongAt[c];
                    if (alongAt[a] > alongMax) alongMax = alongAt[a];
                    if (alongAt[b] > alongMax) alongMax = alongAt[b];
                    if (alongAt[c] > alongMax) alongMax = alongAt[c];
                    if (acrossAt[a] < acrossMin) acrossMin = acrossAt[a];
                    if (acrossAt[b] < acrossMin) acrossMin = acrossAt[b];
                    if (acrossAt[c] < acrossMin) acrossMin = acrossAt[c];
                    if (acrossAt[a] > acrossMax) acrossMax = acrossAt[a];
                    if (acrossAt[b] > acrossMax) acrossMax = acrossAt[b];
                    if (acrossAt[c] > acrossMax) acrossMax = acrossAt[c];
                }
                alongRange = alongMax - alongMin;
                acrossRange = acrossMax - acrossMin;
            }
        }
        if (alongRange < 0.0001f || acrossRange < 0.0001f)
        {
            selfieOverlayMesh.Clear();
            WarnSelfie("Keris blade surface could not be found for the selfie.");
            return;
        }

        float alongMid = (alongMin + alongMax) * 0.5f;
        BuildBladeWidthProfile(alongAt, acrossAt, frontMarks, alongMin, alongRange,
            out float[] binMid, out float[] binWidth);
        float midWidth = SampleProfile(binWidth, alongMid, alongMin, alongRange);
        float usableAcross = Mathf.Max(0.0001f, midWidth * SelfieAcrossFill);
        float usableAlong = alongRange * SelfieAlongFill;
        float photoAspect = appliedSelfieTexture != null && appliedSelfieTexture.height > 0
            ? appliedSelfieTexture.width / (float)appliedSelfieTexture.height
            : 0.75f;
        if (photoAspect <= 0.01f)
            photoAspect = 0.75f;

        float windowAlong = usableAlong;
        float windowAcross = windowAlong * photoAspect;
        if (windowAcross > usableAcross)
        {
            windowAcross = usableAcross;
            windowAlong = windowAcross / photoAspect;
        }
        windowAlong = Mathf.Max(windowAlong, alongRange * 0.12f);
        windowAcross = Mathf.Max(windowAcross, acrossRange * 0.12f);

        Camera camera = ResolveSelfieCamera();
        bool flipVertically = camera != null && Vector3.Dot(worldLong, camera.transform.up) < 0f;
        float thinSpan = Mathf.Max(0.0001f, thinMax - thinMin);
        float worldPush = Mathf.Max(0.0012f, thinSpan * 0.12f);
        Vector3 localThin = blade.InverseTransformDirection(worldThin).normalized;

        int maxVerts = sourceTriangles.Length;
        var outVertices = new Vector3[maxVerts];
        var outNormals = new Vector3[maxVerts];
        var outUvs = new Vector2[maxVerts];
        var outTriangles = new int[maxVerts];
        int v = 0;
        int t = 0;

        for (int i = 0; i < sourceTriangles.Length; i += 3)
        {
            int ia = sourceTriangles[i];
            int ib = sourceTriangles[i + 1];
            int ic = sourceTriangles[i + 2];
            if (ia < 0 || ib < 0 || ic < 0 || ia >= vertices.Length || ib >= vertices.Length || ic >= vertices.Length)
                continue;
            if (!frontMarks[ia] || !frontMarks[ib] || !frontMarks[ic])
                continue;

            Vector2 uvA = SelfieUv(alongAt[ia], acrossAt[ia], alongMid,
                SampleProfile(binMid, alongAt[ia], alongMin, alongRange),
                windowAlong, windowAcross, flipVertically);
            Vector2 uvB = SelfieUv(alongAt[ib], acrossAt[ib], alongMid,
                SampleProfile(binMid, alongAt[ib], alongMin, alongRange),
                windowAlong, windowAcross, flipVertically);
            Vector2 uvC = SelfieUv(alongAt[ic], acrossAt[ic], alongMid,
                SampleProfile(binMid, alongAt[ic], alongMin, alongRange),
                windowAlong, windowAcross, flipVertically);
            if (!SelfieTriangleInsideBladePhoto(uvA, uvB, uvC))
                continue;

            Vector3 nA = SelfieLocalPushNormal(blade, localThin, vertexNormals, ia);
            Vector3 nB = SelfieLocalPushNormal(blade, localThin, vertexNormals, ib);
            Vector3 nC = SelfieLocalPushNormal(blade, localThin, vertexNormals, ic);
            Vector3 pushA = blade.InverseTransformVector(blade.TransformDirection(nA).normalized * worldPush);
            Vector3 pushB = blade.InverseTransformVector(blade.TransformDirection(nB).normalized * worldPush);
            Vector3 pushC = blade.InverseTransformVector(blade.TransformDirection(nC).normalized * worldPush);

            outVertices[v + 0] = vertices[ia] + pushA;
            outVertices[v + 1] = vertices[ib] + pushB;
            outVertices[v + 2] = vertices[ic] + pushC;
            outNormals[v + 0] = nA;
            outNormals[v + 1] = nB;
            outNormals[v + 2] = nC;
            outUvs[v + 0] = uvA;
            outUvs[v + 1] = uvB;
            outUvs[v + 2] = uvC;
            outTriangles[t + 0] = v + 0;
            outTriangles[t + 1] = v + 1;
            outTriangles[t + 2] = v + 2;
            v += 3;
            t += 3;
        }

        if (t == 0)
        {
            selfieOverlayMesh.Clear();
            WarnSelfie("Keris selfie could not be placed on the blade.");
            return;
        }

        Array.Resize(ref outVertices, v);
        Array.Resize(ref outNormals, v);
        Array.Resize(ref outUvs, v);
        Array.Resize(ref outTriangles, t);

        selfieOverlayMesh.Clear();
        selfieOverlayMesh.vertices = outVertices;
        selfieOverlayMesh.normals = outNormals;
        selfieOverlayMesh.uv = outUvs;
        selfieOverlayMesh.triangles = outTriangles;
        selfieOverlayMesh.RecalculateBounds();

        int pieces = t / 3;
        if (pieces != lastSelfieTileLog)
        {
            lastSelfieTileLog = pieces;
            Debug.Log("MORPHIC: Keris selfie pasted onto " + pieces + " blade triangle(s).", this);
        }
    }

    private static Vector2 SelfieUv(
        float along, float across, float alongMid, float acrossMid,
        float windowAlong, float windowAcross, bool flipVertically)
    {
        float u = (across - acrossMid) / Mathf.Max(0.0001f, windowAcross) + 0.5f;
        float v = (along - alongMid) / Mathf.Max(0.0001f, windowAlong) + 0.5f;
        if (flipVertically)
            v = 1f - v;
        return new Vector2(u, v);
    }

    private static bool SelfieTriangleInsideBladePhoto(Vector2 a, Vector2 b, Vector2 c)
    {
        return SelfieUvOnPhoto(a) && SelfieUvOnPhoto(b) && SelfieUvOnPhoto(c);
    }

    private static bool SelfieUvOnPhoto(Vector2 uv)
    {
        return uv.x >= 0f && uv.x <= 1f && uv.y >= 0f && uv.y <= 1f;
    }

    private static Vector3 SelfieLocalPushNormal(
        Transform blade, Vector3 fallback, Vector3[] vertexNormals, int index)
    {
        if (vertexNormals == null || index < 0 || index >= vertexNormals.Length)
            return fallback;
        Vector3 local = vertexNormals[index];
        if (local.sqrMagnitude < 0.0001f)
            return fallback;
        if (Vector3.Dot(blade.TransformDirection(local), blade.TransformDirection(fallback)) < 0f)
            local = -local;
        return local.normalized;
    }

    private const int SelfieWidthBins = 28;

    private static void BuildBladeWidthProfile(
        float[] alongAt, float[] acrossAt, bool[] frontMarks,
        float alongMin, float alongRange, out float[] binMid, out float[] binWidth)
    {
        binMid = new float[SelfieWidthBins];
        binWidth = new float[SelfieWidthBins];
        var binMin = new float[SelfieWidthBins];
        var binMax = new float[SelfieWidthBins];
        for (int i = 0; i < SelfieWidthBins; i++)
        {
            binMin[i] = float.MaxValue;
            binMax[i] = float.MinValue;
        }

        for (int i = 0; i < alongAt.Length; i++)
        {
            if (!frontMarks[i])
                continue;
            int bin = Mathf.Clamp(
                Mathf.FloorToInt(((alongAt[i] - alongMin) / Mathf.Max(0.0001f, alongRange)) * SelfieWidthBins),
                0, SelfieWidthBins - 1);
            if (acrossAt[i] < binMin[bin]) binMin[bin] = acrossAt[i];
            if (acrossAt[i] > binMax[bin]) binMax[bin] = acrossAt[i];
        }

        float lastMid = 0f;
        float lastWidth = 0.0001f;
        for (int i = 0; i < SelfieWidthBins; i++)
        {
            if (binMin[i] < binMax[i])
            {
                lastMid = (binMin[i] + binMax[i]) * 0.5f;
                lastWidth = binMax[i] - binMin[i];
            }
            binMid[i] = lastMid;
            binWidth[i] = Mathf.Max(0.0001f, lastWidth);
        }

        for (int i = SelfieWidthBins - 1; i >= 0; i--)
        {
            if (binMin[i] < binMax[i])
            {
                lastMid = (binMin[i] + binMax[i]) * 0.5f;
                lastWidth = Mathf.Max(0.0001f, binMax[i] - binMin[i]);
            }
            if (binMin[i] >= binMax[i])
            {
                binMid[i] = lastMid;
                binWidth[i] = lastWidth;
            }
        }
    }

    private static float SampleProfile(float[] bins, float along, float alongMin, float alongRange)
    {
        if (bins == null || bins.Length == 0)
            return 0f;
        float t = (along - alongMin) / Mathf.Max(0.0001f, alongRange);
        float scaled = Mathf.Clamp01(t) * (bins.Length - 1);
        int i = Mathf.Clamp(Mathf.FloorToInt(scaled), 0, bins.Length - 2);
        return Mathf.Lerp(bins[i], bins[i + 1], scaled - i);
    }

    private void ConfigureDesignSurfaceMaterial(Texture texture)
    {
        if (designSurfaceRenderer == null || texture == null)
            return;

        Shader shader = MorphicOverlayShaders.KerisBodyDesign();
        bool useMeshUv = shader != null && shader.name.IndexOf("Keris", StringComparison.OrdinalIgnoreCase) >= 0;
        if (shader == null)
            shader = Shader.Find("Universal Render Pipeline/Unlit");
        if (shader == null)
            shader = Shader.Find("Unlit/Transparent");
        if (shader == null)
            shader = Shader.Find("Sprites/Default");
        if (shader == null)
            return;

        if (designSurfaceMaterial == null || designSurfaceMaterial.shader != shader)
        {
            DestroyRuntime(designSurfaceMaterial);
            designSurfaceMaterial = new Material(shader)
            {
                name = "Morphic Keris Stamp Material",
                renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent + 20
            };
        }

        Texture painted = ResolveTintedDesign(texture, committedDesignTint);
        Texture useTexture = painted != null ? painted : texture;

        Material material = designSurfaceMaterial;
        material.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent + 20;
        if (material.HasProperty("_DesignTex"))
            material.SetTexture("_DesignTex", useTexture);
        if (material.HasProperty("_UseMeshUV"))
            material.SetFloat("_UseMeshUV", 1f);
        if (material.HasProperty("_Opacity"))
            material.SetFloat("_Opacity", 1f);
        if (material.HasProperty("_MainTex"))
            material.SetTexture("_MainTex", useTexture);
        if (material.HasProperty("_BaseMap"))
            material.SetTexture("_BaseMap", useTexture);
        material.mainTexture = useTexture;
        if (!useMeshUv)
        {
            if (material.HasProperty("_Surface")) material.SetFloat("_Surface", 1f);
            if (material.HasProperty("_AlphaClip")) material.SetFloat("_AlphaClip", 0f);
            material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            material.DisableKeyword("_ALPHATEST_ON");
            if (material.HasProperty("_ZWrite")) material.SetInt("_ZWrite", 0);
            if (material.HasProperty("_SrcBlend"))
                material.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
            if (material.HasProperty("_DstBlend"))
                material.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            if (material.HasProperty("_Cull")) material.SetFloat("_Cull", 0f);
            if (material.HasProperty("_CullMode")) material.SetFloat("_CullMode", 0f);
            material.SetOverrideTag("RenderType", "Transparent");
        }
        designSurfaceRenderer.sharedMaterial = material;
        designSurfaceRenderer.enabled = true;
        ApplyDesignTint(committedDesignTint);
    }

    private void EnsureDesignOverlay()
    {
        ResolveKerisSkin();
        if (kerisRenderer == null)
        {
            kerisRenderer = kerisSkin != null ? kerisSkin : GetComponentInChildren<Renderer>(true);
        }
        if (kerisRenderer == null)
            return;

        if (designSurfaceRoot != null && designSurfaceRoot.GetComponent<SkinnedMeshRenderer>() != null)
        {
            Destroy(designSurfaceRoot.gameObject);
            designSurfaceRoot = null;
            designSurfaceRenderer = null;
        }

        if (designSurfaceRoot == null)
        {
            GameObject overlayObject = new GameObject("KerisStampOverlay");
            designSurfaceRoot = overlayObject.transform;
            designSurfaceRoot.SetParent(kerisRenderer.transform, false);
            designSurfaceRoot.localPosition = Vector3.zero;
            designSurfaceRoot.localRotation = Quaternion.identity;
            designSurfaceRoot.localScale = Vector3.one;

            MeshFilter filter = overlayObject.AddComponent<MeshFilter>();
            MeshRenderer meshRenderer = overlayObject.AddComponent<MeshRenderer>();
            meshRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            meshRenderer.receiveShadows = false;
            meshRenderer.allowOcclusionWhenDynamic = false;
            meshRenderer.sortingOrder = 20;
            designSurfaceRenderer = meshRenderer;

            stampOverlayMesh = new Mesh { name = "KerisStampTiles" };
            stampOverlayMesh.MarkDynamic();
            filter.sharedMesh = stampOverlayMesh;
        }

        MeshFilter overlayFilter = designSurfaceRoot.GetComponent<MeshFilter>();
        if (overlayFilter == null)
            overlayFilter = designSurfaceRoot.gameObject.AddComponent<MeshFilter>();
        if (stampOverlayMesh == null)
        {
            stampOverlayMesh = new Mesh { name = "KerisStampTiles" };
            stampOverlayMesh.MarkDynamic();
        }
        if (overlayFilter.sharedMesh != stampOverlayMesh)
            overlayFilter.sharedMesh = stampOverlayMesh;

        if (designSurfaceRenderer == null)
        {
            designSurfaceRenderer = designSurfaceRoot.GetComponent<MeshRenderer>();
            if (designSurfaceRenderer == null)
                designSurfaceRenderer = designSurfaceRoot.gameObject.AddComponent<MeshRenderer>();
        }

        designSurfaceRoot.gameObject.layer = kerisRenderer.gameObject.layer;
        designSurfaceRoot.gameObject.SetActive(true);
    }

    private Texture2D GetStampAlphaTexture(Texture2D stamp)
    {
        if (stamp == null)
            return null;
        if (stampAlphaTexture != null && stampAlphaSource == stamp)
            return stampAlphaTexture;

        DestroyRuntime(stampAlphaTexture);
        stampAlphaSource = stamp;
        stampAlphaTexture = BuildInkCutout(stamp);
        return stampAlphaTexture;
    }

    private static Texture2D BuildInkCutout(Texture2D stamp)
    {
        if (stamp == null)
            return null;

        Texture2D source = stamp.isReadable ? stamp : MorphicOverlayShaders.CaptureWebcamFrame(stamp);
        if (source == null)
            return null;

        Texture2D ink = new Texture2D(source.width, source.height, TextureFormat.RGBA32, false)
        {
            name = stamp.name + "_Ink",
            filterMode = FilterMode.Bilinear,
            wrapMode = TextureWrapMode.Clamp
        };
        Color[] pixels = source.GetPixels();
        for (int i = 0; i < pixels.Length; i++)
        {
            Color c = pixels[i];
            float luma = c.r * 0.299f + c.g * 0.587f + c.b * 0.114f;
            float alpha = c.a < 0.98f ? c.a : Mathf.Clamp01((0.68f - luma) * 5f);
            pixels[i] = new Color(1f, 1f, 1f, alpha);
        }
        ink.SetPixels(pixels);
        ink.Apply(false, false);
        if (source != stamp)
            Destroy(source);
        return ink;
    }

    private void BuildStampTileMesh()
    {
        if (stampOverlayMesh == null)
            return;

        SkinnedMeshRenderer skin = kerisSkin != null ? kerisSkin : kerisRenderer as SkinnedMeshRenderer;
        Mesh source = null;
        Transform blade = null;
        if (skin != null)
        {
            source = skin.sharedMesh;
            blade = skin.transform;
        }
        else if (kerisRenderer != null)
        {
            MeshFilter filter = kerisRenderer.GetComponent<MeshFilter>();
            source = filter != null ? filter.sharedMesh : null;
            blade = kerisRenderer.transform;
        }

        if (source == null || blade == null)
        {
            stampOverlayMesh.Clear();
            return;
        }

        Vector3[] vertices;
        Vector3[] vertexNormals;
        if (skin != null)
        {
            if (selfieBoundsMesh == null)
                selfieBoundsMesh = new Mesh { name = "KerisStampBake" };
            selfieBoundsMesh.Clear();
            skin.BakeMesh(selfieBoundsMesh);
            bool baked = selfieBoundsMesh.vertexCount == source.vertexCount;
            vertices = baked ? selfieBoundsMesh.vertices : source.vertices;
            vertexNormals = baked ? selfieBoundsMesh.normals : source.normals;
        }
        else
        {
            vertices = source.vertices;
            vertexNormals = source.normals;
        }

        if (vertices == null || vertices.Length < 8)
        {
            stampOverlayMesh.Clear();
            return;
        }
        if (vertexNormals == null || vertexNormals.Length != vertices.Length)
            vertexNormals = null;

        ChooseBladeAxes(blade, source.bounds, out Vector3 worldLong, out Vector3 worldThin);
        Vector3 view = SelfieViewDirection(blade, vertices);
        if (Vector3.Dot(worldThin, view) < 0f)
            worldThin = -worldThin;
        Vector3 worldAcross = Vector3.Cross(worldThin, worldLong);
        if (worldAcross.sqrMagnitude < 0.0001f)
        {
            stampOverlayMesh.Clear();
            return;
        }
        worldAcross.Normalize();

        var surfacePoints = new List<Vector3>(vertices.Length);
        float alongMin = float.MaxValue, alongMax = float.MinValue;
        float acrossMin = float.MaxValue, acrossMax = float.MinValue;
        float thinMin = float.MaxValue, thinMax = float.MinValue;
        for (int i = 0; i < vertices.Length; i++)
        {
            Vector3 worldPoint = blade.TransformPoint(vertices[i]);
            float thin = Vector3.Dot(worldPoint, worldThin);
            if (thin < thinMin) thinMin = thin;
            if (thin > thinMax) thinMax = thin;
            if (vertexNormals != null)
            {
                Vector3 worldNormal = blade.TransformDirection(vertexNormals[i]);
                if (worldNormal.sqrMagnitude < 0.0001f)
                    continue;
                worldNormal.Normalize();
                if (Vector3.Dot(worldNormal, worldThin) < 0.35f)
                    continue;
            }
            surfacePoints.Add(worldPoint);
            float along = Vector3.Dot(worldPoint, worldLong);
            float across = Vector3.Dot(worldPoint, worldAcross);
            if (along < alongMin) alongMin = along;
            if (along > alongMax) alongMax = along;
            if (across < acrossMin) acrossMin = across;
            if (across > acrossMax) acrossMax = across;
        }

        float alongRange = alongMax - alongMin;
        if (surfacePoints.Count < 3 || alongRange < 0.0001f)
        {
            stampOverlayMesh.Clear();
            return;
        }

        float acrossRange = Mathf.Max(0.0001f, acrossMax - acrossMin);
        float acrossMid = (acrossMin + acrossMax) * 0.5f;
        float thinSpan = Mathf.Max(0.0001f, thinMax - thinMin);
        float aspect = appliedDesignTexture != null && appliedDesignTexture.height > 0
            ? appliedDesignTexture.width / (float)appliedDesignTexture.height
            : 0.75f;

        float span = alongRange * StampTileSpanFraction;
        float spacing = StampTileRows > 1 ? span / (StampTileRows - 1) : span;
        float tileHeight = Mathf.Max(0.0005f, spacing * StampTileFillFraction);
        float tileWidth = Mathf.Max(0.0005f, Mathf.Min(tileHeight * aspect, acrossRange * 0.86f));
        float clearance = Mathf.Max(tileWidth * 0.12f, thinSpan * 0.55f);
        float startAlong = alongMin + (alongRange - span) * 0.5f;

        Vector3[] outVertices = new Vector3[StampTileRows * 4];
        Vector3[] outNormals = new Vector3[StampTileRows * 4];
        Vector2[] outUvs = new Vector2[StampTileRows * 4];
        int[] outTriangles = new int[StampTileRows * 6];
        int v = 0;
        int t = 0;

        for (int row = 0; row < StampTileRows; row++)
        {
            float targetAlong = startAlong + spacing * row;
            int best = -1;
            float bestScore = float.MaxValue;
            for (int i = 0; i < surfacePoints.Count; i++)
            {
                float dAlong = Vector3.Dot(surfacePoints[i], worldLong) - targetAlong;
                float dAcross = Vector3.Dot(surfacePoints[i], worldAcross) - acrossMid;
                float score = dAlong * dAlong + dAcross * dAcross;
                if (score < bestScore)
                {
                    bestScore = score;
                    best = i;
                }
            }
            if (best < 0)
                continue;

            Vector3 centerWorld = surfacePoints[best] + worldThin * clearance;
            Vector3 up = Vector3.ProjectOnPlane(worldLong, worldThin);
            if (up.sqrMagnitude < 0.0001f) up = worldLong;
            up.Normalize();
            Vector3 right = Vector3.Cross(worldThin, up);
            if (right.sqrMagnitude < 0.0001f) right = worldAcross;
            right.Normalize();

            Vector3 halfUp = up * (tileHeight * 0.5f);
            Vector3 halfRight = right * (tileWidth * 0.5f);
            Vector3 localNormal = blade.InverseTransformDirection(worldThin).normalized;
            outVertices[v + 0] = blade.InverseTransformPoint(centerWorld - halfUp - halfRight);
            outVertices[v + 1] = blade.InverseTransformPoint(centerWorld - halfUp + halfRight);
            outVertices[v + 2] = blade.InverseTransformPoint(centerWorld + halfUp - halfRight);
            outVertices[v + 3] = blade.InverseTransformPoint(centerWorld + halfUp + halfRight);
            outNormals[v + 0] = outNormals[v + 1] = outNormals[v + 2] = outNormals[v + 3] = localNormal;
            outUvs[v + 0] = new Vector2(0f, 0f);
            outUvs[v + 1] = new Vector2(1f, 0f);
            outUvs[v + 2] = new Vector2(0f, 1f);
            outUvs[v + 3] = new Vector2(1f, 1f);
            outTriangles[t + 0] = v + 0;
            outTriangles[t + 1] = v + 2;
            outTriangles[t + 2] = v + 1;
            outTriangles[t + 3] = v + 1;
            outTriangles[t + 4] = v + 2;
            outTriangles[t + 5] = v + 3;
            v += 4;
            t += 6;
        }

        if (t == 0)
        {
            stampOverlayMesh.Clear();
            return;
        }

        if (v < outVertices.Length)
        {
            Array.Resize(ref outVertices, v);
            Array.Resize(ref outNormals, v);
            Array.Resize(ref outUvs, v);
            Array.Resize(ref outTriangles, t);
        }

        stampOverlayMesh.Clear();
        stampOverlayMesh.vertices = outVertices;
        stampOverlayMesh.normals = outNormals;
        stampOverlayMesh.uv = outUvs;
        stampOverlayMesh.triangles = outTriangles;
        stampOverlayMesh.RecalculateBounds();
    }

    private Texture2D FindStampByName(string stampName)
    {
        if (kerisStamps == null)
            kerisStamps = MorphicPatternCatalog.Stamps();
        if (kerisStamps == null)
            return null;
        for (int i = 0; i < kerisStamps.Length; i++)
        {
            if (kerisStamps[i] != null && kerisStamps[i].name == stampName)
                return kerisStamps[i];
        }
        return null;
    }

    private static string PatternLabel(Texture2D texture)
    {
        return PatternLabel(texture != null ? texture.name : null);
    }

    private static string PatternLabel(string name)
    {
        if (string.IsNullOrEmpty(name))
            return "PATTERN";
        return name.Replace("Band_", "").Replace('_', ' ').ToUpperInvariant();
    }

    private static void DecoratePatternButton(Button button, Texture2D texture, string label)
    {
        if (button == null)
            return;

        Image fill = null;
        Transform fillTransform = button.transform.Find("Fill");
        if (fillTransform != null)
            fill = fillTransform.GetComponent<Image>();
        Transform parent = fill != null ? fill.transform : button.transform;

        RawImage preview = parent.GetComponentInChildren<RawImage>();
        if (preview == null)
        {
            GameObject previewObject = new GameObject("Preview", typeof(RectTransform), typeof(RawImage));
            previewObject.transform.SetParent(parent, false);
            RectTransform rect = previewObject.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.12f, 0.32f);
            rect.anchorMax = new Vector2(0.88f, 0.92f);
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            preview = previewObject.GetComponent<RawImage>();
            preview.raycastTarget = false;
        }

        preview.texture = texture;
        preview.color = Color.white;

        TMP_Text text = button.GetComponentInChildren<TMP_Text>();
        if (text == null)
            return;

        text.text = label;
        text.fontSize = 16f;
        RectTransform labelRect = text.rectTransform;
        labelRect.anchorMin = new Vector2(0f, 0f);
        labelRect.anchorMax = new Vector2(1f, 0.30f);
        labelRect.offsetMin = Vector2.zero;
        labelRect.offsetMax = Vector2.zero;
    }

    // Matches the coded keris overlays: an opaque, double-sided URP material so
    // the pieces always draw over the blade and can never be swallowed by it.
    private void ConfigureSelfieSurfaceMaterial(Texture texture)
    {
        if (texture == null) return;
        EnsureSelfieCircles();
        if (selfieSurfaceRenderer == null) return;
        if (selfieSurfaceMaterial == null)
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
            if (shader == null) shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null) shader = Shader.Find("Unlit/Texture");
            if (shader == null) shader = Shader.Find("Sprites/Default");
            if (shader == null)
            {
                WarnSelfie("No shader available for the Keris selfie overlay.");
                return;
            }

            selfieSurfaceMaterial = new Material(shader) { name = "KerisSelfieOverlayMaterial" };
        }

        Texture2D selfieTex = texture as Texture2D;
        if (selfieTex != null)
            selfieTex.wrapMode = TextureWrapMode.Clamp;

        Material material = selfieSurfaceMaterial;
        if (material.HasProperty("_Surface")) material.SetFloat("_Surface", 0f);
        if (material.HasProperty("_AlphaClip")) material.SetFloat("_AlphaClip", 0f);
        material.DisableKeyword("_SURFACE_TYPE_TRANSPARENT");
        material.DisableKeyword("_ALPHATEST_ON");
        if (material.HasProperty("_ZWrite")) material.SetInt("_ZWrite", 1);
        if (material.HasProperty("_SrcBlend"))
            material.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.One);
        if (material.HasProperty("_DstBlend"))
            material.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.Zero);
        if (material.HasProperty("_Cull")) material.SetFloat("_Cull", 0f);
        if (material.HasProperty("_CullMode")) material.SetFloat("_CullMode", 0f);
        if (material.HasProperty("_Smoothness")) material.SetFloat("_Smoothness", 0.12f);
        if (material.HasProperty("_Metallic")) material.SetFloat("_Metallic", 0f);
        material.SetOverrideTag("RenderType", "Opaque");
        material.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent + 40;
        if (material.HasProperty("_ZTest"))
            material.SetInt("_ZTest", (int)UnityEngine.Rendering.CompareFunction.LessEqual);

        if (material.HasProperty("_MainTex")) material.SetTexture("_MainTex", texture);
        if (material.HasProperty("_BaseMap")) material.SetTexture("_BaseMap", texture);
        if (material.HasProperty("_Color")) material.SetColor("_Color", Color.white);
        if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", Color.white);
        material.mainTexture = texture;
        if (material.mainTexture != null)
            material.mainTexture.wrapMode = TextureWrapMode.Clamp;

        for (int i = 0; i < SelfieCircleCount; i++)
        {
            Renderer renderer = selfieCircleRenderers[i] != null
                ? selfieCircleRenderers[i]
                : (i == 0 ? selfieSurfaceRenderer : null);
            if (renderer == null)
                continue;
            renderer.sharedMaterial = material;
            renderer.enabled = true;
        }
    }

    private void ApplyKerisColorToSelectedPart(Color color)
    {
        int part = selectedPart;
        if (part < 0)
        {
            savedPartColors[0] = savedPartColors[1] = savedPartColors[2] = color;
            SetRendererColor(kerisRenderer, color);
            TintKerisFlowers(color);
            SetRendererColor(FindOverlayRenderer("KerisHandle"), color);
            return;
        }

        savedPartColors[part] = color;
        if (part == PartCarving)
        {
            // Carving 1 is the coded flower overlay; Carving 2 deforms the keris
            // mesh itself, so tint the material slot the carving shape moves.
            if (selectedVariant == 1)
                SetRendererSlotColor(kerisRenderer, ResolveCarvingSlot(), color);
            else
                TintKerisFlowers(color);
            return;
        }

        if (part == PartHandle)
        {
            TintKerisHandle(color);
            return;
        }

        SetRendererColor(kerisRenderer, color);
    }

    private void TintKerisHandle(Color color)
    {
        KerisHandleOverlay overlay = kerisSkin != null
            ? kerisSkin.GetComponent<KerisHandleOverlay>()
            : GetComponent<KerisHandleOverlay>();
        if (overlay == null && kerisRenderer != null)
            overlay = kerisRenderer.GetComponent<KerisHandleOverlay>();
        if (overlay != null)
            overlay.SetColor(color);

        Transform root = kerisSkin != null ? kerisSkin.transform : transform;
        Transform handle = root.Find("KerisHandle");
        if (handle == null)
            return;
        Renderer[] renderers = handle.GetComponentsInChildren<Renderer>(true);
        for (int i = 0; i < renderers.Length; i++)
        {
            if (renderers[i] != null && renderers[i].enabled)
                SetRendererColor(renderers[i], color);
        }
    }

    private void TintKerisFlowers(Color color)
    {
        KerisFlowerOverlay overlay = kerisSkin != null
            ? kerisSkin.GetComponent<KerisFlowerOverlay>()
            : GetComponent<KerisFlowerOverlay>();
        if (overlay == null && kerisRenderer != null)
            overlay = kerisRenderer.GetComponent<KerisFlowerOverlay>();
        if (overlay != null)
        {
            overlay.SetColor(color);
            return;
        }

        Transform root = kerisSkin != null ? kerisSkin.transform : transform;
        Transform flowers = root.Find("KerisFlowers");
        if (flowers == null)
            return;
        MeshRenderer[] renderers = flowers.GetComponentsInChildren<MeshRenderer>(true);
        for (int i = 0; i < renderers.Length; i++)
            SetRendererColor(renderers[i], color);
    }

    private Renderer FindOverlayRenderer(string childName)
    {
        Transform root = kerisSkin != null ? kerisSkin.transform : transform;
        Transform child = root.Find(childName);
        return child != null ? child.GetComponent<Renderer>() : null;
    }

    private static void SetRendererColor(Renderer target, Color color)
    {
        if (target == null) return;
        Material[] materials = target.materials;
        for (int i = 0; i < materials.Length; i++)
        {
            Material material = materials[i];
            if (material == null) continue;
            if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", color);
            if (material.HasProperty("_Color")) material.SetColor("_Color", color);
        }
    }

    private static void SetRendererSlotColor(Renderer target, int slot, Color color)
    {
        if (target == null) return;
        Material[] materials = target.materials;
        if (materials == null || slot < 0 || slot >= materials.Length) return;
        Material material = materials[slot];
        if (material == null) return;
        if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", color);
        if (material.HasProperty("_Color")) material.SetColor("_Color", color);
    }

    // The carving geometry is its own submesh on the keris mesh (the Carving 2
    // blend shape pushes it out). Find that submesh so its material slot can be
    // tinted, instead of guessing a fixed slot index. Cached because this runs
    // on every color preview/hover update.
    private int ResolveCarvingSlot()
    {
        Mesh mesh = GetSharedMesh(kerisRenderer);
        if (mesh == null)
            return FlowerMaterialSlot;
        if (cachedCarvingSlot != UnresolvedCarvingSlot && ReferenceEquals(mesh, cachedCarvingMesh))
            return cachedCarvingSlot;

        int slot = ResolveShapeSlot(mesh, "Carving 2");
        if (slot < 0)
            slot = ResolveShapeSlot(mesh, "Carving 2.001");
        cachedCarvingSlot = slot >= 0 ? slot : FlowerMaterialSlot;
        cachedCarvingMesh = mesh;
        int slots = kerisRenderer.sharedMaterials != null ? kerisRenderer.sharedMaterials.Length : 0;
        Debug.Log(
            "MORPHIC: Keris carving color slot resolved=" + cachedCarvingSlot +
            " (mesh submeshes=" + mesh.subMeshCount + ", material slots=" + slots + ")",
            kerisRenderer);
        return cachedCarvingSlot;
    }

    private static int ResolveShapeSlot(Mesh mesh, string shapeName)
    {
        if (mesh == null || mesh.subMeshCount <= 1 || !mesh.isReadable)
            return -1;

        int shape = -1;
        for (int i = 0; i < mesh.blendShapeCount; i++)
        {
            if (string.Equals(mesh.GetBlendShapeName(i), shapeName, StringComparison.OrdinalIgnoreCase))
            {
                shape = i;
                break;
            }
        }
        if (shape < 0 || mesh.GetBlendShapeFrameCount(shape) <= 0)
            return -1;

        int vertexCount = mesh.vertexCount;
        if (vertexCount == 0)
            return -1;

        Vector3[] delta = new Vector3[vertexCount];
        Vector3[] normals = new Vector3[vertexCount];
        Vector3[] tangents = new Vector3[vertexCount];
        mesh.GetBlendShapeFrameVertices(shape, 0, delta, normals, tangents);

        float maxSqr = 0f;
        for (int i = 0; i < vertexCount; i++)
            if (delta[i].sqrMagnitude > maxSqr) maxSqr = delta[i].sqrMagnitude;
        if (maxSqr <= 0f)
            return -1;

        // Only strongly displaced vertices count, so incidental drift on the
        // body/blade does not win over the actual carving relief.
        float threshold = maxSqr * 0.25f;
        var moved = new HashSet<int>();
        int bestSlot = -1;
        int bestScore = 0;
        for (int sm = 0; sm < mesh.subMeshCount; sm++)
        {
            int[] tris = mesh.GetTriangles(sm);
            moved.Clear();
            for (int k = 0; k < tris.Length; k++)
            {
                int vi = tris[k];
                if (vi < 0 || vi >= vertexCount || moved.Contains(vi))
                    continue;
                if (delta[vi].sqrMagnitude < threshold)
                    continue;
                moved.Add(vi);
            }
            if (moved.Count > bestScore)
            {
                bestScore = moved.Count;
                bestSlot = sm;
            }
        }
        return bestSlot;
    }

    private static Mesh GetSharedMesh(Renderer target)
    {
        SkinnedMeshRenderer skin = target as SkinnedMeshRenderer;
        if (skin != null) return skin.sharedMesh;
        MeshFilter filter = target.GetComponent<MeshFilter>();
        return filter != null ? filter.sharedMesh : null;
    }
}
