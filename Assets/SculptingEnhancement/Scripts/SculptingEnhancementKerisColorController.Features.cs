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
    private TMP_Text savedStatusText;
    private bool kerisSaved;
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
    private Renderer selfieSurfaceRenderer;
    private Material selfieSurfaceMaterial;
    private Material designSurfaceMaterial;
    private Material designHiddenMaterial;
    private Transform selfieSurfaceRoot;
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
    private RawImage freeColorPaletteImage;
    private Color freePalettePreviewColor = Color.white;
    private Vector2 lastColorSampleScreen;
    private Texture2D freeColorTexture;
    private Texture2D[] kerisStamps;
    private readonly Button[] shelfSlotButtons = new Button[KerisCollectionStore.MaxCount];
    private Color[] savedPartColors = { new Color(0.80f, 0.58f, 0.22f, 1f), Color.white, Color.white };
    private const int UnresolvedCarvingSlot = -2;
    private int cachedCarvingSlot = UnresolvedCarvingSlot;
    private Mesh cachedCarvingMesh;

    private const int SelfieTileRows = 4;
    private const float SelfieTileSpanFraction = 0.72f;
    private const float SelfieTileFillFraction = 0.82f;

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
        return button;
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
        if (clayTintActive) PreviewClayColor(freePalettePreviewColor);
        else if (designTintActive) committedDesignTint = freePalettePreviewColor;
        else if (bandTintActive) committedBandColor = freePalettePreviewColor;
        else ApplyKerisColor(freePalettePreviewColor);
    }

    private void CommitFreePaletteColor() => CommitColor(freePalettePreviewColor);

    private void OnColorDoneClicked()
    {
        if (clayTintActive) { clayTintActive = false; ShowVertexSculpt(); return; }
        if (designTintActive) { designTintActive = false; ShowDesignSelection(); return; }
        if (bandTintActive) { bandTintActive = false; ResumeBandDrawing(); return; }
        if (selectedPart >= 0 && PartNeedsVariant(selectedPart) && selectedVariant >= 0)
            ShowVariantSelection(selectedPart);
        else if (selectedPart >= 0) ShowPartSelection();
        else ShowMainMenu();
    }

    private void BuildReview(RectTransform parent)
    {
        reviewGroup = new GameObject("KerisReview", typeof(RectTransform));
        reviewGroup.transform.SetParent(parent, false);
        RectTransform rect = reviewGroup.GetComponent<RectTransform>();
        DockTop(rect, -140f, 230f);
        CreateText("ReviewPrompt", rect, Vector2.zero, new Vector2(PillWidth, 46f),
            "Your Keris is ready to keep on the shelf.", 20f,
            TextAlignmentOptions.Center, FontStyles.Bold);
        savedStatusText = CreateText("SavedStatus", rect, new Vector2(0f, -48f),
            new Vector2(PillWidth, 34f), "", 17f, TextAlignmentOptions.Center, FontStyles.Bold);
        Button save = CreateButton("SaveKeris", rect, new Vector2(0f, -95f),
            new Vector2(PillWidth, 56f), "SAVE TO SHELF", ActionPink);
        save.onClick.AddListener(SaveKerisToShelf);
        RegisterHitTarget(save, false, false, Color.white);
        Button done = CreateButton("ReviewDone", rect, new Vector2(0f, -162f),
            new Vector2(PillWidth, 56f), "DONE", DonePink);
        done.onClick.AddListener(ShowMainMenu);
        RegisterHitTarget(done, false, false, Color.white);
        Button decorate = CreateButton("ReviewDecorate", rect, new Vector2(0f, -228f),
            new Vector2(PillWidth, 54f), "STAMP & DRAW", PillPink);
        decorate.onClick.AddListener(ShowDesignSelection);
        RegisterHitTarget(decorate, false, false, Color.white);
        Button openShelf = CreateButton("ReviewShelf", rect, new Vector2(0f, -292f),
            new Vector2(PillWidth, 54f), "OPEN SHELF", ActionPink);
        openShelf.onClick.AddListener(ShowShelf);
        RegisterHitTarget(openShelf, false, false, Color.white);
        reviewGroup.SetActive(false);

        shelfGroup = new GameObject("KerisShelf", typeof(RectTransform));
        shelfGroup.transform.SetParent(parent, false);
        RectTransform shelfRect = shelfGroup.GetComponent<RectTransform>();
        DockTop(shelfRect, -140f, 240f);
        CreateText("ShelfTitle", shelfRect, Vector2.zero, new Vector2(PillWidth, 42f),
            "KERIS COLLECTION", 22f, TextAlignmentOptions.Center, FontStyles.Bold);
        for (int i = 0; i < shelfSlotButtons.Length; i++)
        {
            int slot = i;
            shelfSlotButtons[i] = CreateButton("KerisSlot" + i, shelfRect,
                new Vector2(-165f + (i % 3) * 165f, -55f - (i / 3) * 58f),
                new Vector2(154f, 48f), "EMPTY", PillPink);
            shelfSlotButtons[i].onClick.AddListener(() => LoadKerisFromShelf(slot));
            RegisterHitTarget(shelfSlotButtons[i], false, true, Color.white);
        }
        Button shelfDone = CreateButton("ShelfDone", shelfRect, new Vector2(0f, -242f),
            new Vector2(PillWidth, 56f), "DONE", DonePink);
        shelfDone.onClick.AddListener(LeaveShelfDone);
        RegisterHitTarget(shelfDone, false, false, Color.white);
        shelfGroup.SetActive(false);
        confirmDeleteGroup = null;
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
    }

    private void ShowBodyActions()
    {
        selectedPart = PartBody;
        ShowColorSelection();
    }

    private void ShowDesignSelection()
    {
        designTintActive = false;
        mode = ColorMode.SelectDesign;
        ShowMainMenu();
        mode = ColorMode.SelectDesign;
        mainActionGroup.SetActive(false);
        instructionText.gameObject.SetActive(true);
        instructionText.text = "Choose a stamp, then draw it on the Keris.";
        SetHeaderBackVisible(true);
        SetHeaderBackLabel("← BACK");
        SetPanelHeight(DesignSelectionPanelHeight);
        HideExtraGroups();
        if (designGroup == null)
        {
            designGroup = new GameObject("KerisStamps", typeof(RectTransform));
            designGroup.transform.SetParent(panel.transform, false);
            DockTop(designGroup.GetComponent<RectTransform>(), -124f, 370f);
            kerisStamps = Resources.LoadAll<Texture2D>("StampPatterns");
            if (kerisStamps == null || kerisStamps.Length == 0)
                kerisStamps = new Texture2D[0];
            int count = Mathf.Min(kerisStamps.Length, 6);
            for (int i = 0; i < count; i++)
            {
                Texture2D stamp = kerisStamps[i];
                Button button = CreateButton("Stamp" + i, designGroup.transform,
                    new Vector2(-125f + (i % 3) * 125f, -(i / 3) * 82f),
                    new Vector2(110f, 64f), "STAMP " + (i + 1), PillPink);
                button.onClick.AddListener(() => SelectStamp(stamp));
                RegisterHitTarget(button, false, false, Color.white);
            }
            Button draw = CreateButton("DrawDesign", designGroup.transform, new Vector2(0f, -210f),
                new Vector2(PillWidth, 58f), "FREE DRAW", ActionPink);
            draw.onClick.AddListener(ResumeBandDrawing);
            RegisterHitTarget(draw, false, false, Color.white);
            Button tint = CreateButton("StampColor", designGroup.transform, new Vector2(0f, -278f),
                new Vector2(PillWidth, 54f), "STAMP COLOR", PillPink);
            tint.onClick.AddListener(() => { designTintActive = true; ShowColorSelection(); });
            RegisterHitTarget(tint, false, false, Color.white);
        }
        designGroup.SetActive(true);
    }

    private void SelectStamp(Texture2D stamp)
    {
        EnsurePainter();
        appliedDesignTexture = stamp;
        if (patternPainter != null)
        {
            patternPainter.SetPatternTexture(stamp);
            patternPainter.SetStrokeColor(committedDesignTint, true);
            patternPainter.SetPaintingEnabled(true);
        }
        mode = ColorMode.DrawBandTargeting;
        if (designGroup != null) designGroup.SetActive(false);
        instructionText.text = "Draw on the Keris surface to place the stamp.";
    }

    private void ShowBandSelection()
    {
        ShowDesignSelection();
    }

    private void ResumeBandDrawing()
    {
        EnsurePainter();
        appliedDesignTexture = null;
        if (patternPainter != null)
        {
            patternPainter.SetPatternTexture(null);
            patternPainter.SetPaintingEnabled(true);
        }
        mode = ColorMode.DrawBandTargeting;
        if (designGroup != null) designGroup.SetActive(false);
        if (bandTintActive) bandTintActive = false;
        instructionText.text = "Point at the Keris and draw to add a surface pattern.";
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
        SetSelfieReviewControls(false);
        if (selfieInstructionText != null)
            selfieInstructionText.text = "Frame your photo, then point and hold TAKE SELFIE.";
        
        if (capturedSelfieTexture == null && appliedSelfieTexture != null)
            capturedSelfieTexture = Instantiate(appliedSelfieTexture);
        
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
            DestroyRuntime(appliedSelfieTexture);
            appliedSelfieTexture = Instantiate(capturedSelfieTexture);
            SyncSelfieSurfaceRenderer();
            ConfigureSelfieSurfaceMaterial(appliedSelfieTexture);
        }
        ShowMainMenu();
    }

    private void ShowReview()
    {
        DisableVertexSculpt();
        mode = ColorMode.Review;
        SetPanelHeight(ReviewPanelHeight);
        panel.SetActive(true);
        mainActionGroup.SetActive(false);
        if (historyBar != null) historyBar.SetActive(false);
        HideExtraGroups();
        HideReviewAndShelf();
        reviewGroup.SetActive(true);
        if (selfiePanel != null) selfiePanel.SetActive(false);
        headerText.text = "REVIEW KERIS";
        instructionText.gameObject.SetActive(false);
        SetHeaderBackVisible(true);
        RefreshSavedStatus();
        RefreshShelfSlots();
    }

    private void ShowShelf()
    {
        DisableVertexSculpt();
        mode = ColorMode.Shelf;
        SetPanelHeight(ShelfPanelHeight);
        panel.SetActive(true);
        mainActionGroup.SetActive(false);
        if (historyBar != null) historyBar.SetActive(false);
        HideExtraGroups();
        HideReviewAndShelf();
        shelfGroup.SetActive(true);
        headerText.text = "KERIS SHELF";
        instructionText.gameObject.SetActive(false);
        SetHeaderBackVisible(true);
        RefreshSavedStatus();
        RefreshShelfSlots();
    }

    private void LeaveShelfDone() => ShowMainMenu();

    private void HideReviewAndShelf()
    {
        if (reviewGroup != null) reviewGroup.SetActive(false);
        if (shelfGroup != null) shelfGroup.SetActive(false);
        if (confirmDeleteGroup != null) confirmDeleteGroup.SetActive(false);
    }

    private void SaveKerisToShelf()
    {
        if (kerisSaved || KerisCollectionStore.IsFull || kerisSkin == null) return;
        Mesh mesh = kerisSkin.sharedMesh;
        var weights = new float[mesh != null ? mesh.blendShapeCount : 0];
        for (int i = 0; i < weights.Length; i++) weights[i] = kerisSkin.GetBlendShapeWeight(i);
        KerisCollectionStore.Add(new KerisCollectionStore.Record
        {
            Id = Guid.NewGuid().ToString("N"),
            BlendWeights = weights,
            BladeColor = committedKerisColor,
            PartColors = (Color[])savedPartColors.Clone(),
            LocalRotation = new[] { transform.localRotation.x, transform.localRotation.y,
                transform.localRotation.z, transform.localRotation.w },
            StampVisible = appliedDesignTexture != null,
            StampName = appliedDesignTexture != null ? appliedDesignTexture.name : "",
            StampTint = committedDesignTint,
            SelfiePng = appliedSelfieTexture != null ? appliedSelfieTexture.EncodeToPNG() : null
        });
        kerisSaved = true;
        RefreshSavedStatus();
        ShowShelf();
    }

    private void RefreshSavedStatus()
    {
        if (savedStatusText == null) return;
        savedStatusText.text = kerisSaved
            ? "Saved • " + KerisCollectionStore.Count + " / " + KerisCollectionStore.MaxCount
            : "Shelf: " + KerisCollectionStore.Count + " / " + KerisCollectionStore.MaxCount;
    }

    private void RefreshShelfSlots()
    {
        IReadOnlyList<KerisCollectionStore.Record> records = KerisCollectionStore.All;
        for (int i = 0; i < shelfSlotButtons.Length; i++)
        {
            Button button = shelfSlotButtons[i];
            if (button == null) continue;
            bool occupied = i < records.Count;
            TMP_Text label = button.GetComponentInChildren<TMP_Text>();
            if (label != null) label.text = occupied ? "KERIS " + (i + 1) : "EMPTY";
            button.interactable = occupied;
            Image fill = GetButtonFill(button);
            if (fill != null) fill.color = occupied ? PillPink : new Color(0.72f, 0.72f, 0.72f, 1f);
        }
    }

    private void LoadKerisFromShelf(int slot)
    {
        IReadOnlyList<KerisCollectionStore.Record> records = KerisCollectionStore.All;
        if (slot < 0 || slot >= records.Count || kerisSkin == null) return;
        KerisCollectionStore.Record record = records[slot];
        Mesh mesh = kerisSkin.sharedMesh;
        int count = mesh != null ? Mathf.Min(mesh.blendShapeCount,
            record.BlendWeights != null ? record.BlendWeights.Length : 0) : 0;
        for (int i = 0; i < count; i++) kerisSkin.SetBlendShapeWeight(i, record.BlendWeights[i]);
        committedKerisColor = record.BladeColor;
        if (record.PartColors != null && record.PartColors.Length == savedPartColors.Length)
            savedPartColors = (Color[])record.PartColors.Clone();
        selectedPart = -1;
        ApplyKerisColor(committedKerisColor);
        if (record.LocalRotation != null && record.LocalRotation.Length == 4)
            transform.localRotation = new Quaternion(record.LocalRotation[0], record.LocalRotation[1],
                record.LocalRotation[2], record.LocalRotation[3]);
        if (record.SelfiePng != null && record.SelfiePng.Length > 0)
        {
            DestroyRuntime(appliedSelfieTexture);
            appliedSelfieTexture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            appliedSelfieTexture.LoadImage(record.SelfiePng);
        }
        if (record.StampVisible && !string.IsNullOrEmpty(record.StampName))
        {
            appliedDesignTexture = Resources.Load<Texture2D>("StampPatterns/" + record.StampName);
            if (patternPainter != null && appliedDesignTexture != null)
            {
                patternPainter.SetPatternTexture(appliedDesignTexture);
                patternPainter.SetStrokeColor(record.StampTint, true);
            }
        }
        kerisSaved = true;
        ShowMainMenu();
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
        capturedSelfieTexture = null;
        appliedSelfieTexture = null;
        appliedDesignTexture = null;
        if (selfieSurfaceRoot != null) selfieSurfaceRoot.gameObject.SetActive(false);
        if (patternPainter != null) patternPainter.ClearAllStrokes();
    }

    private void ApplyDesignTint(Color color)
    {
        if (patternPainter != null) patternPainter.SetStrokeColor(color, true);
    }

    private void SyncDesignSurfaceRenderer() { }

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

        if (selfieSurfaceRoot == null)
        {
            GameObject root = new GameObject("KerisSelfieOverlay");
            selfieSurfaceRoot = root.transform;
            selfieSurfaceRoot.SetParent(kerisRenderer.transform, false);
            selfieSurfaceRoot.localPosition = Vector3.zero;
            selfieSurfaceRoot.localRotation = Quaternion.identity;
            selfieSurfaceRoot.localScale = Vector3.one;

            MeshFilter filter = root.AddComponent<MeshFilter>();
            MeshRenderer meshRenderer = root.AddComponent<MeshRenderer>();
            meshRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            meshRenderer.receiveShadows = false;
            meshRenderer.allowOcclusionWhenDynamic = false;
            meshRenderer.sortingOrder = 30;
            selfieSurfaceRenderer = meshRenderer;

            selfieOverlayMesh = new Mesh { name = "KerisSelfieTiles" };
            selfieOverlayMesh.MarkDynamic();
            filter.sharedMesh = selfieOverlayMesh;
            lastSelfieTileLog = -1;
        }

        // Self-heal the overlay every frame: it always ends up parented to the
        // blade transform with a valid filter/renderer/mesh, so attachment can
        // never silently break.
        if (selfieSurfaceRoot.parent != kerisRenderer.transform)
        {
            selfieSurfaceRoot.SetParent(kerisRenderer.transform, false);
            selfieSurfaceRoot.localPosition = Vector3.zero;
            selfieSurfaceRoot.localRotation = Quaternion.identity;
            selfieSurfaceRoot.localScale = Vector3.one;
        }

        MeshFilter overlayFilter = selfieSurfaceRoot.GetComponent<MeshFilter>();
        if (overlayFilter == null)
            overlayFilter = selfieSurfaceRoot.gameObject.AddComponent<MeshFilter>();

        if (selfieOverlayMesh == null)
        {
            selfieOverlayMesh = new Mesh { name = "KerisSelfieTiles" };
            selfieOverlayMesh.MarkDynamic();
        }
        if (overlayFilter.sharedMesh != selfieOverlayMesh)
            overlayFilter.sharedMesh = selfieOverlayMesh;

        if (selfieSurfaceRenderer == null)
        {
            selfieSurfaceRenderer = selfieSurfaceRoot.GetComponent<MeshRenderer>();
            if (selfieSurfaceRenderer == null)
                selfieSurfaceRenderer = selfieSurfaceRoot.gameObject.AddComponent<MeshRenderer>();
            selfieSurfaceRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            selfieSurfaceRenderer.receiveShadows = false;
            selfieSurfaceRenderer.allowOcclusionWhenDynamic = false;
            selfieSurfaceRenderer.sortingOrder = 30;
        }

        selfieSurfaceRoot.gameObject.layer = kerisRenderer.gameObject.layer;
        selfieSurfaceRoot.gameObject.SetActive(true);
        selfieSurfaceRenderer.enabled = true;

        BuildSelfieTileMesh();
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

    // Builds a short column of small upright selfie tiles that hug the blade's
    // front surface and run along its length (tip to hilt). Everything is
    // sampled in world space and converted back to the skin's local space, so
    // the tiles land on the blade regardless of the imported transform's
    // rotation, scale or the blade's wavy silhouette.
    private void BuildSelfieTileMesh()
    {
        if (selfieOverlayMesh == null) return;

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

        // Keep only the blade's front-facing surface so the tiles follow the
        // wavy edge instead of the model's raw bounding box.
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
                if (worldNormal.sqrMagnitude < 0.0001f) continue;
                worldNormal.Normalize();
                if (Vector3.Dot(worldNormal, worldThin) < 0.45f) continue;
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
            selfieOverlayMesh.Clear();
            WarnSelfie("Keris blade surface could not be found for the selfie.");
            return;
        }

        float acrossRange = Mathf.Max(0.0001f, acrossMax - acrossMin);
        float acrossMid = (acrossMin + acrossMax) * 0.5f;
        float thinSpan = Mathf.Max(0.0001f, thinMax - thinMin);

        float photoAspect = appliedSelfieTexture != null && appliedSelfieTexture.height > 0
            ? appliedSelfieTexture.width / (float)appliedSelfieTexture.height
            : 0.75f;
        if (photoAspect <= 0.01f) photoAspect = 0.75f;

        float span = alongRange * SelfieTileSpanFraction;
        float spacing = SelfieTileRows > 1 ? span / (SelfieTileRows - 1) : span;
        float tileHeight = spacing * SelfieTileFillFraction;
        float tileWidth = tileHeight * photoAspect;
        // Dynamically adjust width based on Curve blend shape to prevent overflow
            float curveAmount = 0f;
            if (skin != null && skin.sharedMesh != null)
            {
                for (int shapeIdx = 0; shapeIdx < skin.sharedMesh.blendShapeCount; shapeIdx++)
                {
                    string shapeName = skin.sharedMesh.GetBlendShapeName(shapeIdx);
                    if (shapeName.Equals("Curve", System.StringComparison.OrdinalIgnoreCase))
                    {
                        curveAmount = skin.GetBlendShapeWeight(shapeIdx) / 100f;
                        break;
                    }
                }
            }
            float widthScale = Mathf.Lerp(0.85f, 0.60f, curveAmount);
            float maxWidth = acrossRange * widthScale;
        if (tileWidth > maxWidth)
        {
            tileHeight *= maxWidth / tileWidth;
            tileWidth = maxWidth;
        }
        tileWidth = Mathf.Max(tileWidth, 0.0005f);
        tileHeight = Mathf.Max(tileHeight, 0.0005f);

        // Nudge the pieces clear of the blade so the opaque blade can never
        // swallow them.
        float clearance = Mathf.Max(tileWidth * 0.15f, thinSpan * 0.6f);

        Camera camera = ResolveSelfieCamera();
        bool flipVertically = camera != null && Vector3.Dot(worldLong, camera.transform.up) < 0f;

        int quadCount = SelfieTileRows;
        Vector3[] outVertices = new Vector3[quadCount * 4];
        Vector3[] outNormals = new Vector3[quadCount * 4];
        Vector2[] outUvs = new Vector2[quadCount * 4];
        int[] outTriangles = new int[quadCount * 6];

        // Divide blade into sections and find widest point in each section
        var widestPoints = new System.Collections.Generic.List<int>();
        float sectionSize = alongRange / SelfieTileRows;
        
        for (int section = 0; section < SelfieTileRows; section++)
        {
            float sectionStart = alongMin + section * sectionSize;
            float sectionEnd = sectionStart + sectionSize;
            float sectionMid = (sectionStart + sectionEnd) * 0.5f;
            
            // Find widest point in this section
            int widestInSection = -1;
            float maxWidthInSection = 0f;
            
            for (float checkAlong = sectionStart; checkAlong <= sectionEnd; checkAlong += sectionSize * 0.1f)
            {
                float minAcross = float.MaxValue, maxAcross = float.MinValue;
                
                for (int i = 0; i < surfacePoints.Count; i++)
                {
                    Vector3 point = surfacePoints[i];
                    float along = Vector3.Dot(point, worldLong);
                    if (Mathf.Abs(along - checkAlong) > tileHeight * 0.6f) continue;
                    
                    float across = Vector3.Dot(point, worldAcross);
                    if (across < minAcross) minAcross = across;
                    if (across > maxAcross) maxAcross = across;
                }
                
                float widthHere = maxAcross - minAcross;
                if (widthHere > maxWidthInSection)
                {
                    maxWidthInSection = widthHere;
                    
                    // Find center point at this widest location
                    int bestCenter = -1;
                    float bestCenterDist = float.MaxValue;
                    for (int i = 0; i < surfacePoints.Count; i++)
                    {
                        Vector3 point = surfacePoints[i];
                        float dAlong = Vector3.Dot(point, worldLong) - checkAlong;
                        float dAcross = Vector3.Dot(point, worldAcross) - acrossMid;
                        float dist = dAlong * dAlong + dAcross * dAcross;
                        if (dist < bestCenterDist)
                        {
                            bestCenterDist = dist;
                            bestCenter = i;
                        }
                    }
                    widestInSection = bestCenter;
                }
            }
            
            if (widestInSection >= 0)
                widestPoints.Add(widestInSection);
        }
        
        int v = 0;
        int t = 0;
        for (int row = 0; row < widestPoints.Count; row++)
        {
            int best = widestPoints[row];
            if (best < 0) break;

            // Get center point from actual baked vertices (follows deformation)
            Vector3 centerLocal = vertices[best];
            Vector3 centerWorld = blade.TransformPoint(centerLocal);
            centerWorld += worldThin * clearance;

            Vector3 up = Vector3.ProjectOnPlane(worldLong, worldThin);
            if (up.sqrMagnitude < 0.0001f) up = worldLong;
            up.Normalize();
            Vector3 right = Vector3.Cross(worldThin, up);
            if (right.sqrMagnitude < 0.0001f) right = worldAcross;
            right.Normalize();

            // Simply reduce width when curve is active - no deformation
            float adjustedWidth = tileWidth * widthScale;
            
            Vector3 halfUp = up * (tileHeight * 0.5f);
            Vector3 halfRight = right * (adjustedWidth * 0.5f);
            
            // Find actual blade vertices for each corner (paste onto surface)
            Vector3[] corners = new Vector3[4];
            Vector3[] cornerTargets = new Vector3[4] 
            {
                centerWorld - halfUp - halfRight,
                centerWorld - halfUp + halfRight,
                centerWorld + halfUp - halfRight,
                centerWorld + halfUp + halfRight
            };
            
            // For each corner, find closest blade vertex
            for (int c = 0; c < 4; c++)
            {
                int bestCorner = best;
                float bestCornerDist = float.MaxValue;
                
                for (int i = 0; i < vertices.Length; i++)
                {
                    Vector3 vertWorld = blade.TransformPoint(vertices[i]);
                    float dist = Vector3.Distance(vertWorld, cornerTargets[c]);
                    if (dist < bestCornerDist)
                    {
                        bestCornerDist = dist;
                        bestCorner = i;
                    }
                }
                
                // Use actual blade vertex position (will deform with curve)
                corners[c] = vertices[bestCorner];
            }

            outVertices[v + 0] = corners[0];
            outVertices[v + 1] = corners[1];
            outVertices[v + 2] = corners[2];
            outVertices[v + 3] = corners[3];

            Vector3 localNormal = blade.InverseTransformDirection(worldThin).normalized;
            outNormals[v + 0] = localNormal;
            outNormals[v + 1] = localNormal;
            outNormals[v + 2] = localNormal;
            outNormals[v + 3] = localNormal;

            if (flipVertically)
            {
                outUvs[v + 0] = new Vector2(0f, 1f);
                outUvs[v + 1] = new Vector2(1f, 1f);
                outUvs[v + 2] = new Vector2(0f, 0f);
                outUvs[v + 3] = new Vector2(1f, 0f);
            }
            else
            {
                outUvs[v + 0] = new Vector2(0f, 0f);
                outUvs[v + 1] = new Vector2(1f, 0f);
                outUvs[v + 2] = new Vector2(0f, 1f);
                outUvs[v + 3] = new Vector2(1f, 1f);
            }

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
            selfieOverlayMesh.Clear();
            WarnSelfie("Keris selfie tiles could not be placed on the blade.");
            return;
        }

        if (v < outVertices.Length)
        {
            Array.Resize(ref outVertices, v);
            Array.Resize(ref outNormals, v);
            Array.Resize(ref outUvs, v);
            Array.Resize(ref outTriangles, t);
        }

        selfieOverlayMesh.Clear();
        selfieOverlayMesh.vertices = outVertices;
        selfieOverlayMesh.normals = outNormals;
        selfieOverlayMesh.uv = outUvs;
        selfieOverlayMesh.triangles = outTriangles;
        selfieOverlayMesh.RecalculateBounds();

        int tiles = t / 6;
        if (tiles != lastSelfieTileLog)
        {
            lastSelfieTileLog = tiles;
            Debug.Log("MORPHIC: Keris selfie attached as " + tiles + " vertical tile piece(s).", this);
        }
    }

    private void ConfigureDesignSurfaceMaterial(Texture texture) { }

    // Matches the coded keris overlays: an opaque, double-sided URP material so
    // the pieces always draw over the blade and can never be swallowed by it.
    private void ConfigureSelfieSurfaceMaterial(Texture texture)
    {
        if (selfieSurfaceRenderer == null || texture == null) return;
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
        material.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Geometry;

        if (material.HasProperty("_MainTex")) material.SetTexture("_MainTex", texture);
        if (material.HasProperty("_BaseMap")) material.SetTexture("_BaseMap", texture);
        if (material.HasProperty("_Color")) material.SetColor("_Color", Color.white);
        if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", Color.white);
        material.mainTexture = texture;

        selfieSurfaceRenderer.sharedMaterial = material;
        selfieSurfaceRenderer.enabled = true;
    }

    private void ApplyKerisColorToSelectedPart(Color color)
    {
        int part = selectedPart;
        if (part < 0)
        {
            savedPartColors[0] = savedPartColors[1] = savedPartColors[2] = color;
            SetRendererColor(kerisRenderer, color);
            SetRendererColor(FindOverlayRenderer("KerisFlowers"), color);
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
                SetRendererColor(FindOverlayRenderer("KerisFlowers"), color);
            return;
        }

        Renderer target = part == PartBody ? kerisRenderer : FindOverlayRenderer("KerisHandle");
        SetRendererColor(target, color);
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
