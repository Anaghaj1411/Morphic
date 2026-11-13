using System.Collections.Generic;
using Mediapipe.Unity.Sample.HandLandmarkDetection;
using TMPro;
using UnityEngine;

/// <summary>
/// Keris-only hand sculpt. Drives carving, handle, width, height, sharpness, and curve.
/// Does not touch lantern or teapot.
/// </summary>
[DefaultExecutionOrder(250)]
public sealed class KerisGestureController : MonoBehaviour
{
    private sealed class PinchLift
    {
        public bool held;
        public bool active;
        public bool paused;
        public float hold;
        public float current;
        public float target;
        public float starting;
        public float velocity;
        public float baselineY;
        public float filteredY;
        public float filteredDy;
        public bool filterSeeded;
        public float arm;
    }

    [Header("References")]
    [SerializeField] private SculptHandLandmarkerRunner handRunner;
    [SerializeField] private TMP_Text gestureStatus;
    [SerializeField] private SkinnedMeshRenderer kerisRenderer;

    [Header("Carving - Two V Outwards")]
    [SerializeField, Range(0.015f, 0.12f)] private float carvingTravel = 0.022f;

    [Header("Sharpness / Curve - Pinch Lift")]
    [SerializeField, Range(0.1f, 1.8f)] private float pinchThreshold = 0.98f;
    [SerializeField, Range(0.2f, 2.2f)] private float pinchReleaseThreshold = 1.35f;
    [SerializeField, Min(1f)] private float pinchLiftSensitivity = 9800f;
    [SerializeField, Min(1f)] private float sharpnessLiftSensitivity = 14000f;
    [SerializeField, Min(1f)] private float pinchEase = 38f;

    [Header("Handle - Two Fists Down")]
    [SerializeField, Min(1f)] private float handleDownSensitivity = 420f;
    [SerializeField, Min(1f)] private float handleEase = 22f;

    [Header("Body Width / Height - Two Index")]
    [SerializeField, Range(0.015f, 0.12f)] private float fullTravel = 0.022f;
    [SerializeField, Range(0.008f, 0.08f)] private float heightTravel = 0.012f;
    [SerializeField, Range(0.03f, 0.25f)] private float followTime = 0.12f;
    [SerializeField, Range(0.4f, 3f)] private float oneEuroMinCutoff = 0.55f;
    [SerializeField, Range(0.001f, 0.05f)] private float oneEuroBeta = 0.006f;
    [SerializeField, Range(0f, 0.03f)] private float movementDeadZone = 0.0016f;
    [SerializeField, Range(0.02f, 0.5f)] private float activationTime = 0.02f;
    [SerializeField, Range(0.15f, 0.8f)] private float widthHeightHoldTime = 0.45f;
    [SerializeField, Range(0.2f, 0.8f)] private float pinchHoldTime = 0.55f;
    [SerializeField, Range(0.05f, 0.4f)] private float openPalmLockTime = 0.12f;
    [SerializeField, Range(90f, 175f)] private float minimumIndexAngle = 125f;
    [SerializeField, Range(90f, 175f)] private float activeMinimumIndexAngle = 105f;
    [SerializeField, Range(0.8f, 1.8f)] private float minimumIndexTipExtension = 1.08f;
    [SerializeField, Range(0.5f, 2f)] private float maximumOtherFingerExtension = 1.45f;
    [SerializeField, Range(0.5f, 2.2f)] private float activeMaximumOtherFingerExtension = 1.75f;
    [SerializeField, Range(0.2f, 0.8f)] private float minimumThumbIndexSeparation = 0.42f;

    private readonly Dictionary<string, int> blendShapeIndices = new Dictionary<string, int>();
    private readonly Dictionary<string, string> logicalToMeshName = new Dictionary<string, string>();

    public bool suppressDriving;
    public TMP_Text GestureStatus => gestureStatus;
    public bool IsDrivingSculpt { get; private set; }
    public bool IsDrivingHandle => currentGesture == GestureMode.HandleDown;
    public bool IsDeformationLocked => deformationLocked;

    public enum PracticeGesture
    {
        None,
        TwoIndex,
        TwoFists,
        RightPinch,
        LeftPinch,
        TwoV,
        SingleFist,
        OpenPalm,
        PointIndex
    }

    private bool practiceRightPinchHeld;
    private bool practiceLeftPinchHeld;

    private bool useCarving2;
    private bool useHandle2;
    private bool carvingArmed = true;
    private readonly PinchLift sharpnessLift = new PinchLift();
    private readonly PinchLift curveLift = new PinchLift();
    private float carving;
    private float targetCarving;
    private float startingCarving;
    private float carvingBaseline;
    private float carvingVelocity;
    private float filteredCarvingSpread;
    private float filteredCarvingSpreadD;
    private bool carvingSpreadSeeded;
    private bool carvingActive;
    private bool carvingPaused;
    private float carvingHold;
    private float carvingActivation;
    private bool handleDownArmed;
    private float handleDownStartY;
    private float handleStartWeight;
    private float handleDownGrace;
    private float filteredFistY;
    private float filteredFistDy;
    private bool fistYSeeded;
    private const float HandleGraceDuration = 0.12f;
    private const float HandleDeadZone = 0.0035f;
    private const float PinchArmTime = 0.01f;
    private const float GestureHoldGrace = 0.12f;
    private float latchedSharpness;
    private float latchedCurve;
    private float latchedWidth;
    private float latchedHeight;
    private float latchedCarving;
    private float latchedHandle;
    private float sharpnessAnchorY;
    private float curveAnchorY;
    private bool hasSharpnessAnchor;
    private bool hasCurveAnchor;
    private float heldSharpness;
    private float heldCurve;
    private float heldWidth;
    private float heldHeight;
    private float heldCarving;
    private bool holdBodyShapes;
    private const float TwoHandPinchCommit = 1.40f;
    private const float MinFistSeparation = 0.035f;
    private float width;
    private float height;
    private float targetWidth;
    private float targetHeight;
    private float startingWidth;
    private float startingHeight;
    private float widthHeightBaselineX;
    private float widthHeightBaselinePalmY;
    private float widthHeightBaselineStagger;
    private float filteredLeftX;
    private float filteredLeftY;
    private float filteredRightX;
    private float filteredRightY;
    private float filteredLeftPalmY;
    private float filteredRightPalmY;
    private float filteredLeftDx;
    private float filteredLeftDy;
    private float filteredRightDx;
    private float filteredRightDy;
    private float filteredLeftPalmDy;
    private float filteredRightPalmDy;
    private bool tipFilterSeeded;
    private bool widthHeightActive;
    private bool widthHeightPaused;
    private float widthHeightActivation;
    private float widthHeightHold;
    private float widthVelocity;
    private float heightVelocity;
    private float openPalmTimer;
    private float unlockTimer;
    private bool deformationLocked;
    private bool madeDeformation;
    private GestureMode currentGesture;

    private enum GestureMode
    {
        None,
        WidthHeight,
        TwoVCarving,
        PinchSharpness,
        PinchCurve,
        HandleDown
    }

    public void BindFrom(SculptHandLandmarkerRunner runner, SkinnedMeshRenderer renderer)
    {
        pinchThreshold = 0.98f;
        pinchReleaseThreshold = 1.40f;
        pinchLiftSensitivity = 9800f;
        sharpnessLiftSensitivity = 15000f;
        pinchEase = 32f;
        handleDownSensitivity = 900f;
        handleEase = 22f;
        movementDeadZone = 0.0016f;
        activationTime = 0f;

        bool same = handRunner == runner && kerisRenderer == renderer && blendShapeIndices.Count > 0;
        if (same)
        {
            if (!TryGetIndex("Sharpness", out _) || !TryGetIndex("Curve", out _))
            {
                PrepareRenderer(kerisRenderer);
                ResolveBlendShapes();
            }

            return;
        }

        handRunner = runner;
        kerisRenderer = renderer;
        PrepareRenderer(kerisRenderer);
        ResolveBlendShapes();
        ReadBodyWeights();
        ArmCarvingVariant(false);
        carvingArmed = true;
        EnsureGestureStatus();
        if (gestureStatus != null)
            gestureStatus.gameObject.SetActive(true);
    }

    private void OnEnable()
    {
        EnsureGestureStatus();
        if (gestureStatus != null)
            gestureStatus.gameObject.SetActive(true);
    }

    private void OnDisable()
    {
        if (gestureStatus != null)
            gestureStatus.gameObject.SetActive(false);
    }

    public static void PrepareRenderer(SkinnedMeshRenderer skin)
    {
        if (skin == null)
            return;

        skin.updateWhenOffscreen = true;
        KerisFlowerOverlay.Ensure(skin);
        KerisHandleOverlay.Ensure(skin);
        KerisBlendShapeRebuilder.RebuildNamed(skin, "Sharpness", "Curve");

        Mesh mesh = skin.sharedMesh;
        Renderer[] renderers = skin.GetComponentsInChildren<Renderer>(true);
        for (int i = 0; i < renderers.Length; i++)
        {
            MeshRenderer meshRenderer = renderers[i] as MeshRenderer;
            if (meshRenderer == null)
                continue;
            if (meshRenderer.gameObject.name == "KerisFlowers" || meshRenderer.gameObject.name == "KerisHandle")
            {
                meshRenderer.enabled = true;
                continue;
            }
            MeshFilter filter = meshRenderer.GetComponent<MeshFilter>();
            if (mesh == null || filter == null || filter.sharedMesh == mesh)
                meshRenderer.enabled = false;
        }
    }

    public void ArmCarvingVariant(bool useSecond)
    {
        useCarving2 = useSecond;
        ZeroUnusedShape(useCarving2 ? "Carving 1" : "Carving 2");
        carving = targetCarving = ReadShape(ActiveCarvingName);
        carvingVelocity = 0f;
        EndCarving();
    }

    public void ArmHandleVariant(bool useSecond)
    {
        useHandle2 = useSecond;
        ZeroUnusedShape(useHandle2 ? "Handle 1" : "Handle 2");
        if (kerisRenderer != null)
        {
            KerisHandleOverlay overlay = kerisRenderer.GetComponent<KerisHandleOverlay>();
            if (overlay != null)
                overlay.SetHandleVariant(useSecond);
        }
    }

    public void WriteHelpWeights(MorphicCraftHelpState state)
    {
        if (state == null || kerisRenderer == null)
            return;

        state.width = Mathf.RoundToInt(ReadShape("Width"));
        state.height = Mathf.RoundToInt(ReadShape("Height"));
        state.bamboo = Mathf.RoundToInt(ReadShape(ActiveCarvingName));
        state.kerisHandle1 = Mathf.RoundToInt(ReadHandleAmount(1));
        state.kerisHandle2 = Mathf.RoundToInt(ReadHandleAmount(2));
        state.kerisUseHandle2 = useHandle2 ? 1 : 0;
        state.locked = deformationLocked ? 1 : 0;
    }

    private float ReadHandleAmount()
    {
        if (kerisRenderer == null)
            return 0f;
        KerisHandleOverlay overlay = kerisRenderer.GetComponent<KerisHandleOverlay>();
        if (overlay != null)
            return useHandle2 ? overlay.handle2Amount : overlay.handle1Amount;
        return 0f;
    }

    private float ReadHandleAmount(int handleVariant)
    {
        if (kerisRenderer == null)
            return 0f;
        KerisHandleOverlay overlay = kerisRenderer.GetComponent<KerisHandleOverlay>();
        if (overlay != null)
        {
            if (handleVariant == 1)
                return overlay.handle1Amount;
            else if (handleVariant == 2)
                return overlay.handle2Amount;
        }
        return 0f;
    }

    public void SetPinchDrivesCarving(bool enabled)
    {
        carvingArmed = enabled;
        if (!enabled)
            EndCarving();
    }

    public void SynchronizeWeightsFromRenderer()
    {
        if (widthHeightActive || carvingActive || sharpnessLift.active || curveLift.active)
            return;
        ReadBodyWeights();
    }

    public void CaptureRestState()
    {
        ReadBodyWeights();
        ResetAllTracking();
        deformationLocked = false;
        madeDeformation = false;
        IsDrivingSculpt = false;
    }

    public PracticeGesture ReadPracticeGesture()
    {
        if (handRunner == null)
            return PracticeGesture.None;

        bool hasTwo = handRunner.TryGetLatestTwoHands(
            out SculptHandFrame first, out SculptHandFrame second);
        if (!hasTwo && !handRunner.TryGetLatestHand(out first))
            return PracticeGesture.None;
        if (!hasTwo)
            second = default;

        bool twoV = hasTwo && IsVSign(first) && IsVSign(second);
        bool twoFists = !twoV && hasTwo &&
                        LooksLikeHandleFist(first) && LooksLikeHandleFist(second);
        bool twoIndex = !twoV && hasTwo &&
                        IsIndexPointing(first) && IsIndexPointing(second);

        SplitHands(first, second, hasTwo,
            out SculptHandFrame rightHand, out SculptHandFrame leftHand,
            out bool hasRightHand, out bool hasLeftHand);
        bool rightPinch = hasRightHand &&
            IsPinching(rightHand, true, ref practiceRightPinchHeld);
        bool leftPinch = hasLeftHand &&
            IsPinching(leftHand, false, ref practiceLeftPinchHeld);

        if (twoV)
            return PracticeGesture.TwoV;
        if (twoIndex)
            return PracticeGesture.TwoIndex;
        if (twoFists)
            return PracticeGesture.TwoFists;
        if (rightPinch)
            return PracticeGesture.RightPinch;
        if (leftPinch)
            return PracticeGesture.LeftPinch;
        if (LooksLikeHandleFist(first) && (!hasTwo || !LooksLikeHandleFist(second)))
            return PracticeGesture.SingleFist;
        if (hasTwo && LooksLikeHandleFist(second) && !LooksLikeHandleFist(first))
            return PracticeGesture.SingleFist;
        if ((IsIndexPointing(first) || (hasTwo && IsIndexPointing(second))) && !twoIndex)
            return PracticeGesture.PointIndex;
        if (IsOpenPalm(first) || (hasTwo && IsOpenPalm(second)))
            return PracticeGesture.OpenPalm;
        return PracticeGesture.None;
    }

    public void RestoreRestState()
    {
        if (kerisRenderer == null || kerisRenderer.sharedMesh == null)
            return;

        int count = kerisRenderer.sharedMesh.blendShapeCount;
        for (int i = 0; i < count; i++)
            kerisRenderer.SetBlendShapeWeight(i, 0f);

        KerisFlowerOverlay flowerOverlay = kerisRenderer.GetComponent<KerisFlowerOverlay>();
        if (flowerOverlay != null)
            flowerOverlay.SetAmount(0f);

        KerisHandleOverlay handleOverlay = kerisRenderer.GetComponent<KerisHandleOverlay>();
        if (handleOverlay != null)
            handleOverlay.SetAmount(0f, 0f);

        width = targetWidth = 0f;
        height = targetHeight = 0f;
        widthVelocity = 0f;
        heightVelocity = 0f;
        ResetPinchLift(sharpnessLift);
        ResetPinchLift(curveLift);
        latchedSharpness = 0f;
        latchedCurve = 0f;
        hasSharpnessAnchor = false;
        hasCurveAnchor = false;
        carving = targetCarving = 0f;
        carvingVelocity = 0f;
        openPalmTimer = 0f;
        unlockTimer = 0f;
        ResetPinchTracking();
        ResetHandleDownTracking();
        EndWidthHeight();
        EndCarving();
        currentGesture = GestureMode.None;
        deformationLocked = false;
        madeDeformation = false;
        IsDrivingSculpt = false;
    }

    private string ActiveCarvingName => useCarving2 ? "Carving 2" : "Carving 1";
    private string ActiveHandleName => useHandle2 ? "Handle 2" : "Handle 1";

    private void EnsureGestureStatus()
    {
        if (gestureStatus != null)
            return;

        Canvas canvas = FindFirstObjectByType<Canvas>();
        if (canvas == null)
            return;

        Transform existing = canvas.transform.Find("Keris Gesture Status");
        if (existing != null)
        {
            gestureStatus = existing.GetComponent<TMP_Text>();
            return;
        }

        LanguagePageBuilder.TextLabel label = LanguagePageBuilder.CreateLabel(
            "Keris Gesture Status", canvas.transform, "NO HANDS", 22);
        RectTransform rect = label.Rect;
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 1f);
        rect.pivot = new Vector2(0.5f, 1f);
        rect.anchoredPosition = new Vector2(0f, -20f);
        rect.sizeDelta = new Vector2(760f, 48f);
        gestureStatus = label.Label;
        if (gestureStatus != null)
            gestureStatus.raycastTarget = false;
    }

    private void SetStatus(string message)
    {
        EnsureGestureStatus();
        if (gestureStatus == null)
            return;

        gestureStatus.gameObject.SetActive(true);
        MorphicLoc.Apply(gestureStatus, message);
    }

    private void Update()
    {
        EnsureGestureStatus();
        if (handRunner == null || kerisRenderer == null || suppressDriving)
        {
            PauseWidthHeight();
            PauseCarving();
            PausePinchLift(sharpnessLift);
            PausePinchLift(curveLift);
            IsDrivingSculpt = false;
            return;
        }

        bool hasTwoHands = handRunner.TryGetLatestTwoHands(
            out SculptHandFrame first,
            out SculptHandFrame second);
        bool hasAnyHand = hasTwoHands || handRunner.TryGetLatestHand(out first);
        if (!hasAnyHand)
        {
            CommitPinchLift(sharpnessLift, true);
            CommitPinchLift(curveLift, false);
            ResetPinchTracking();
            ResetHandleDownTracking();
            PauseWidthHeight();
            PauseCarving();
            if (widthHeightHold <= 0f)
                EndWidthHeight();
            if (carvingHold <= 0f)
                EndCarving();
            currentGesture = widthHeightActive ? GestureMode.WidthHeight
                : carvingActive ? GestureMode.TwoVCarving
                : GestureMode.None;
            IsDrivingSculpt = false;
            SetStatus("NO HANDS");
            return;
        }

        SplitHands(
            first,
            second,
            hasTwoHands,
            out SculptHandFrame rightHand,
            out SculptHandFrame leftHand,
            out bool hasRightHand,
            out bool hasLeftHand);
        bool twoVNow = hasTwoHands && IsVSign(first) && IsVSign(second);
        bool twoIndex = !twoVNow && hasTwoHands &&
                        IsIndexPointing(first, true) &&
                        IsIndexPointing(second, true);
        bool bothFists = !twoVNow && !twoIndex &&
                         hasTwoHands &&
                         HandsLookLikeTwoFists(first, second);
        if (bothFists)
            handleDownGrace = HandleGraceDuration;
        else
            handleDownGrace -= Time.deltaTime;
        bool twoFistsNow = bothFists ||
                           (handleDownArmed && handleDownGrace > 0f && hasTwoHands &&
                            !twoVNow && !twoIndex);

        bool rightPinching = !twoVNow && !twoIndex && !bothFists && hasRightHand &&
                             IsPinching(rightHand, true, ref sharpnessLift.held, sharpnessLift.active);
        bool leftPinching = !twoVNow && !twoIndex && !bothFists && hasLeftHand &&
                            IsPinching(leftHand, false, ref curveLift.held, curveLift.active);
        if (rightPinching && leftPinching)
            leftPinching = false;

        bool pinching = rightPinching || leftPinching;
        bool sculptGesture = twoIndex || twoVNow || twoFistsNow || pinching;
        bool openPalm = IsOpenPalm(first) || (hasTwoHands && IsOpenPalm(second));
        UpdateLockState(openPalm, twoIndex || twoVNow || pinching, sculptGesture);
        if (sculptGesture)
            deformationLocked = false;

        if (deformationLocked)
        {
            ResetAllTracking();
            IsDrivingSculpt = false;
            SetStatus("OPEN PALM");
            return;
        }

        bool continueV = currentGesture == GestureMode.TwoVCarving && hasTwoHands &&
                         !bothFists && !rightPinching && !leftPinching;
        bool continueWidth = currentGesture == GestureMode.WidthHeight && hasTwoHands &&
                             !twoVNow && !bothFists;
        bool continueHandle = currentGesture == GestureMode.HandleDown && hasTwoHands &&
                              !twoVNow && !twoIndex && !rightPinching && !leftPinching;
        bool continueSharp = currentGesture == GestureMode.PinchSharpness && hasRightHand &&
                             rightPinching && !twoVNow && !twoIndex && !bothFists;
        bool continueCurve = currentGesture == GestureMode.PinchCurve && hasLeftHand &&
                             leftPinching && !twoVNow && !twoIndex && !bothFists;

        if ((twoVNow || continueV) && hasTwoHands)
        {
            EnterGesture(GestureMode.TwoVCarving);
            if (TryDriveCarving(first, second, twoVNow))
            {
                currentGesture = GestureMode.TwoVCarving;
                IsDrivingSculpt = true;
                SetStatus("TWO V");
                return;
            }
        }
        else if ((twoIndex || continueWidth) && hasTwoHands)
        {
            EnterGesture(GestureMode.WidthHeight);
            if (TryDriveWidthHeight(first, second, twoIndex))
            {
                currentGesture = GestureMode.WidthHeight;
                IsDrivingSculpt = true;
                SetStatus("WIDTH & HEIGHT");
                return;
            }
        }
        else if ((twoFistsNow || continueHandle) && hasTwoHands)
        {
            EnterGesture(GestureMode.HandleDown);
            DriveHandleDown(first, second);
            RestoreHeldBodyShapes();
            currentGesture = GestureMode.HandleDown;
            madeDeformation = true;
            IsDrivingSculpt = true;
            SetStatus("TWO FISTS");
            return;
        }
        else if ((rightPinching || continueSharp) && hasRightHand)
        {
            EnterGesture(GestureMode.PinchSharpness);
            if (DrivePinchUp(rightHand, true))
            {
                currentGesture = GestureMode.PinchSharpness;
                IsDrivingSculpt = true;
                SetStatus("RIGHT PINCH");
                return;
            }
        }
        else if ((leftPinching || continueCurve) && hasLeftHand)
        {
            EnterGesture(GestureMode.PinchCurve);
            if (DrivePinchUp(leftHand, false))
            {
                currentGesture = GestureMode.PinchCurve;
                IsDrivingSculpt = true;
                SetStatus("LEFT PINCH");
                return;
            }
        }

        EndActiveGesture();
        currentGesture = GestureMode.None;
        IsDrivingSculpt = false;
        SetStatus(openPalm ? "OPEN PALM" : "NO HANDS");
    }

    private void EnterGesture(GestureMode next)
    {
        if (currentGesture == next)
            return;
        FreezeOthersFor(next);
        currentGesture = next;
    }

    private void EndActiveGesture()
    {
        switch (currentGesture)
        {
            case GestureMode.WidthHeight:
                EndWidthHeight();
                break;
            case GestureMode.TwoVCarving:
                EndCarving();
                break;
            case GestureMode.HandleDown:
                ResetHandleDownTracking();
                break;
            case GestureMode.PinchSharpness:
                CommitPinchLift(sharpnessLift, true);
                break;
            case GestureMode.PinchCurve:
                CommitPinchLift(curveLift, false);
                break;
        }
    }

    private bool TryContinueExclusiveGesture(
        SculptHandFrame first,
        SculptHandFrame second,
        bool hasTwoHands,
        bool hasRightHand,
        bool hasLeftHand,
        SculptHandFrame rightHand,
        SculptHandFrame leftHand,
        bool twoVNow,
        bool twoIndex,
        bool bothFists,
        bool twoFistsNow,
        bool rightPinching,
        bool leftPinching)
    {
        if (currentGesture == GestureMode.None)
            return false;

        bool pinchNow = rightPinching || leftPinching;
        switch (currentGesture)
        {
            case GestureMode.WidthHeight:
                if (twoVNow || bothFists || pinchNow)
                {
                    EndWidthHeight();
                    break;
                }
                if (twoIndex && hasTwoHands && TryDriveWidthHeight(first, second, true))
                {
                    FreezeOthersFor(GestureMode.WidthHeight);
                    IsDrivingSculpt = !widthHeightPaused;
                    return true;
                }
                if (widthHeightActive && widthHeightHold > 0f && hasTwoHands)
                {
                    PauseWidthHeight();
                    FreezeOthersFor(GestureMode.WidthHeight);
                    IsDrivingSculpt = false;
                    return true;
                }
                EndWidthHeight();
                break;

            case GestureMode.TwoVCarving:
                if (twoIndex || bothFists || pinchNow)
                {
                    EndCarving();
                    break;
                }
                if (twoVNow && hasTwoHands && TryDriveCarving(first, second, true))
                {
                    FreezeOthersFor(GestureMode.TwoVCarving);
                    IsDrivingSculpt = !carvingPaused;
                    return true;
                }
                if (carvingActive && carvingHold > 0f && hasTwoHands)
                {
                    PauseCarving();
                    FreezeOthersFor(GestureMode.TwoVCarving);
                    IsDrivingSculpt = false;
                    return true;
                }
                EndCarving();
                break;

            case GestureMode.HandleDown:
                if (twoVNow || twoIndex || pinchNow)
                {
                    ResetHandleDownTracking();
                    break;
                }
                if (bothFists && hasTwoHands)
                {
                    FreezeOthersFor(GestureMode.HandleDown);
                    DriveHandleDown(first, second);
                    RestoreHeldBodyShapes();
                    madeDeformation = true;
                    IsDrivingSculpt = true;
                    return true;
                }
                if (twoFistsNow && hasTwoHands)
                {
                    FreezeOthersFor(GestureMode.HandleDown);
                    RestoreHeldBodyShapes();
                    IsDrivingSculpt = false;
                    return true;
                }
                ResetHandleDownTracking();
                break;

            case GestureMode.PinchSharpness:
                if (twoVNow || twoIndex || bothFists || !rightPinching)
                {
                    CommitPinchLift(sharpnessLift, true);
                    SetBlendShapeWeight("Sharpness", latchedSharpness);
                    break;
                }
                if (hasRightHand && DrivePinchUp(rightHand, true))
                {
                    FreezeOthersFor(GestureMode.PinchSharpness);
                    IsDrivingSculpt = true;
                    return true;
                }
                CommitPinchLift(sharpnessLift, true);
                SetBlendShapeWeight("Sharpness", latchedSharpness);
                break;

            case GestureMode.PinchCurve:
                if (twoVNow || twoIndex || bothFists || !leftPinching)
                {
                    CommitPinchLift(curveLift, false);
                    SetBlendShapeWeight("Curve", latchedCurve);
                    break;
                }
                if (hasLeftHand && DrivePinchUp(leftHand, false))
                {
                    FreezeOthersFor(GestureMode.PinchCurve);
                    IsDrivingSculpt = true;
                    return true;
                }
                CommitPinchLift(curveLift, false);
                SetBlendShapeWeight("Curve", latchedCurve);
                break;
        }

        currentGesture = GestureMode.None;
        return false;
    }

    private void FreezeOthersFor(GestureMode keep)
    {
        if (keep != GestureMode.PinchSharpness)
        {
            CommitPinchLift(sharpnessLift, true);
            SetBlendShapeWeight("Sharpness", latchedSharpness);
        }

        if (keep != GestureMode.PinchCurve)
        {
            CommitPinchLift(curveLift, false);
            SetBlendShapeWeight("Curve", latchedCurve);
        }

        if (keep != GestureMode.WidthHeight)
            SilenceWidthHeight();

        if (keep != GestureMode.TwoVCarving)
            SilenceCarving();

        if (keep != GestureMode.HandleDown)
        {
            ResetHandleDownTracking();
            SetHandleAmount(latchedHandle);
        }

        PinInactiveShapes(keep);
    }

    private void PinInactiveShapes(GestureMode keep)
    {
        if (keep != GestureMode.PinchSharpness)
        {
            sharpnessLift.current = sharpnessLift.target = latchedSharpness;
            sharpnessLift.velocity = 0f;
            SetBlendShapeWeight("Sharpness", latchedSharpness);
        }

        if (keep != GestureMode.PinchCurve)
        {
            curveLift.current = curveLift.target = latchedCurve;
            curveLift.velocity = 0f;
            SetBlendShapeWeight("Curve", latchedCurve);
        }

        if (keep != GestureMode.WidthHeight)
        {
            width = targetWidth = latchedWidth;
            height = targetHeight = latchedHeight;
            widthVelocity = 0f;
            heightVelocity = 0f;
            SetBlendShapeWeight("Width", latchedWidth);
            SetBlendShapeWeight("Height", latchedHeight);
        }

        if (keep != GestureMode.TwoVCarving)
        {
            carving = targetCarving = latchedCarving;
            carvingVelocity = 0f;
            SetBlendShapeWeight(ActiveCarvingName, latchedCarving);
            if (!useCarving2)
                ApplyFlowerAmount(latchedCarving);
        }

        if (keep != GestureMode.HandleDown)
            SetHandleAmount(latchedHandle);
    }

    private void SilenceWidthHeight()
    {
        widthHeightActive = false;
        widthHeightPaused = false;
        widthHeightActivation = 0f;
        widthHeightHold = 0f;
        widthVelocity = 0f;
        heightVelocity = 0f;
        width = targetWidth = latchedWidth;
        height = targetHeight = latchedHeight;
        SetBlendShapeWeight("Width", latchedWidth);
        SetBlendShapeWeight("Height", latchedHeight);
    }

    private void SilenceCarving()
    {
        carvingActive = false;
        carvingPaused = false;
        carvingActivation = 0f;
        carvingHold = 0f;
        carvingVelocity = 0f;
        carvingSpreadSeeded = false;
        carving = targetCarving = latchedCarving;
        SetBlendShapeWeight(ActiveCarvingName, latchedCarving);
        if (!useCarving2)
            ApplyFlowerAmount(latchedCarving);
    }

    private void ResetAllTracking()
    {
        CommitPinchLift(sharpnessLift, true);
        CommitPinchLift(curveLift, false);
        ResetPinchTracking();
        ResetHandleDownTracking();
        PauseWidthHeight();
        PauseCarving();
        targetWidth = width;
        targetHeight = height;
        targetCarving = carving;
        sharpnessLift.current = sharpnessLift.target = latchedSharpness;
        curveLift.current = curveLift.target = latchedCurve;
        currentGesture = GestureMode.None;
    }

    private void LateUpdate()
    {
        float dt = Mathf.Max(Time.unscaledDeltaTime, 0.0001f);
        bool driveWidth = currentGesture == GestureMode.WidthHeight && widthHeightActive;
        bool driveCarving = currentGesture == GestureMode.TwoVCarving && carvingActive;
        bool driveHandle = currentGesture == GestureMode.HandleDown;

        if (driveHandle)
            RestoreHeldBodyShapes();

        if (driveWidth)
        {
            ApplySmoothedShape(ref width, ref targetWidth, ref widthVelocity, "Width", true, dt);
            ApplySmoothedShape(ref height, ref targetHeight, ref heightVelocity, "Height", true, dt);
            latchedWidth = width;
            latchedHeight = height;
        }

        if (driveCarving)
        {
            ApplySmoothedShape(
                ref carving, ref targetCarving, ref carvingVelocity, ActiveCarvingName, true, dt);
            latchedCarving = carving;
        }

        if (driveHandle)
            latchedHandle = ReadHandleAmount();

        if (currentGesture == GestureMode.PinchSharpness && sharpnessLift.active)
            latchedSharpness = sharpnessLift.current;
        if (currentGesture == GestureMode.PinchCurve && curveLift.active)
            latchedCurve = curveLift.current;
    }

    private void ApplySmoothedShape(
        ref float current,
        ref float target,
        ref float velocity,
        string logicalName,
        bool driving,
        float dt,
        float easeTime = -1f)
    {
        if (!driving)
        {
            current = target = ReadShape(logicalName);
            velocity = 0f;
            return;
        }

        float blend = 1f - Mathf.Exp(-24f * dt);
        current = Mathf.Lerp(current, target, blend);
        if (logicalName == "Carving 1")
        {
            ApplyFlowerAmount(current);
            return;
        }

        SetBlendShapeWeight(logicalName, current);
    }

    private bool TryDriveWidthHeight(SculptHandFrame first, SculptHandFrame second, bool pointingNow)
    {
        if (pointingNow)
            widthHeightHold = 0.22f;
        else
            widthHeightHold -= Time.unscaledDeltaTime;

        if (!widthHeightActive)
        {
            if (!pointingNow)
                return false;
            BeginWidthHeight(first, second);
        }

        if (!pointingNow && widthHeightHold <= 0f)
        {
            EndWidthHeight();
            return false;
        }

        widthHeightPaused = false;
        DriveWidthHeight(first, second);
        if (Mathf.Abs(targetWidth - startingWidth) > 1.5f ||
            Mathf.Abs(targetHeight - startingHeight) > 1.5f)
            madeDeformation = true;
        return true;
    }

    private void BeginWidthHeight(SculptHandFrame first, SculptHandFrame second)
    {
        currentGesture = GestureMode.WidthHeight;
        widthHeightActive = true;
        widthHeightActivation = 0f;
        RebaseWidthHeight(first, second);
    }

    private void RebaseWidthHeight(SculptHandFrame first, SculptHandFrame second)
    {
        startingWidth = width;
        startingHeight = height;
        targetWidth = width;
        targetHeight = height;
        widthVelocity = 0f;
        heightVelocity = 0f;
        SeedTipFilter(first, second);
        widthHeightBaselineX = Mathf.Abs(filteredRightX - filteredLeftX);
        widthHeightBaselinePalmY = (filteredLeftPalmY + filteredRightPalmY) * 0.5f;
        widthHeightBaselineStagger = filteredRightY - filteredLeftY;
        widthHeightPaused = false;
    }

    private void DriveWidthHeight(SculptHandFrame first, SculptHandFrame second)
    {
        float dt = Mathf.Max(Time.unscaledDeltaTime, 0.0001f);
        FilterTips(first, second, dt);

        float spread = Mathf.Abs(filteredRightX - filteredLeftX);
        float palmLift = (filteredLeftPalmY + filteredRightPalmY) * 0.5f;
        float stagger = filteredRightY - filteredLeftY;
        float gain = SculptHandFrame.CombinedDistanceGain(first, second);
        float widthSpan = Mathf.Max(fullTravel, 0.01f) / gain;
        float heightSpan = Mathf.Max(heightTravel, 0.008f) / gain;
        float dead = movementDeadZone / Mathf.Max(gain, 1f);

        float widthDelta = spread - widthHeightBaselineX;
        if (Mathf.Abs(widthDelta) >= dead)
            targetWidth = Mathf.Clamp(startingWidth + (widthDelta / widthSpan) * 100f, 0f, 100f);

        float heightDelta =
            (palmLift - widthHeightBaselinePalmY) +
            (stagger - widthHeightBaselineStagger);
        if (Mathf.Abs(heightDelta) >= dead)
            targetHeight = Mathf.Clamp(startingHeight + (heightDelta / heightSpan) * 100f, 0f, 100f);
    }

    private void SeedTipFilter(SculptHandFrame first, SculptHandFrame second)
    {
        SortTips(
            first,
            second,
            out float leftX,
            out float leftY,
            out float rightX,
            out float rightY,
            out float leftPalmY,
            out float rightPalmY);
        filteredLeftX = leftX;
        filteredLeftY = leftY;
        filteredRightX = rightX;
        filteredRightY = rightY;
        filteredLeftPalmY = leftPalmY;
        filteredRightPalmY = rightPalmY;
        filteredLeftDx = filteredLeftDy = 0f;
        filteredRightDx = filteredRightDy = 0f;
        filteredLeftPalmDy = filteredRightPalmDy = 0f;
        tipFilterSeeded = true;
    }

    private void FilterTips(SculptHandFrame first, SculptHandFrame second, float dt)
    {
        SortTips(
            first,
            second,
            out float leftX,
            out float leftY,
            out float rightX,
            out float rightY,
            out float leftPalmY,
            out float rightPalmY);
        if (!tipFilterSeeded)
        {
            SeedTipFilter(first, second);
            return;
        }

        FilterOneEuro(leftX, ref filteredLeftX, ref filteredLeftDx, dt);
        FilterOneEuro(leftY, ref filteredLeftY, ref filteredLeftDy, dt);
        FilterOneEuro(rightX, ref filteredRightX, ref filteredRightDx, dt);
        FilterOneEuro(rightY, ref filteredRightY, ref filteredRightDy, dt);
        FilterOneEuro(leftPalmY, ref filteredLeftPalmY, ref filteredLeftPalmDy, dt);
        FilterOneEuro(rightPalmY, ref filteredRightPalmY, ref filteredRightPalmDy, dt);
    }

    private static void SortTips(
        SculptHandFrame first,
        SculptHandFrame second,
        out float leftX,
        out float leftY,
        out float rightX,
        out float rightY,
        out float leftPalmY,
        out float rightPalmY)
    {
        bool firstIsLeft = first.PalmCenter.x <= second.PalmCenter.x;
        SculptHandFrame left = firstIsLeft ? first : second;
        SculptHandFrame right = firstIsLeft ? second : first;
        leftX = left.IndexTip.x;
        leftY = left.IndexTip.y;
        rightX = right.IndexTip.x;
        rightY = right.IndexTip.y;
        leftPalmY = left.PalmCenter.y;
        rightPalmY = right.PalmCenter.y;
    }

    private void FilterOneEuro(float raw, ref float filtered, ref float filteredD, float dt)
    {
        float dx = (raw - filtered) / dt;
        filteredD = LowPass(dx, filteredD, Alpha(1f, dt));
        float cutoff = oneEuroMinCutoff + oneEuroBeta * Mathf.Abs(filteredD);
        filtered = LowPass(raw, filtered, Alpha(cutoff, dt));
    }

    private void UpdateLockState(bool openPalm, bool twoIndex, bool _)
    {
        if (openPalm && madeDeformation)
        {
            openPalmTimer += Time.unscaledDeltaTime;
            unlockTimer = 0f;
            if (openPalmTimer >= openPalmLockTime)
            {
                deformationLocked = true;
                targetWidth = width;
                targetHeight = height;
                targetCarving = carving;
                sharpnessLift.target = sharpnessLift.current;
                curveLift.target = curveLift.current;
                widthVelocity = 0f;
                heightVelocity = 0f;
                carvingVelocity = 0f;
                sharpnessLift.velocity = 0f;
                curveLift.velocity = 0f;
            }

            return;
        }

        openPalmTimer = 0f;
        if (!deformationLocked)
            return;

        if (twoIndex && !openPalm)
        {
            unlockTimer += Time.unscaledDeltaTime;
            if (unlockTimer >= activationTime)
                deformationLocked = false;
            return;
        }

        unlockTimer = 0f;
    }

    private static float Alpha(float cutoff, float dt)
    {
        float tau = 1f / (2f * Mathf.PI * Mathf.Max(cutoff, 0.01f));
        return 1f / (1f + tau / dt);
    }

    private static float LowPass(float value, float previous, float alpha)
    {
        return alpha * value + (1f - alpha) * previous;
    }

    private void PauseWidthHeight()
    {
        if (!widthHeightActive || widthHeightPaused)
            return;
        widthVelocity = 0f;
        heightVelocity = 0f;
        widthHeightPaused = true;
    }

    private void EndWidthHeight()
    {
        latchedWidth = width;
        latchedHeight = height;
        widthHeightActive = false;
        widthHeightPaused = false;
        widthHeightActivation = 0f;
        widthHeightHold = 0f;
        targetWidth = width;
        targetHeight = height;
        tipFilterSeeded = false;
        if (currentGesture == GestureMode.WidthHeight)
            currentGesture = GestureMode.None;
    }

    private void SplitHands(
        SculptHandFrame first,
        SculptHandFrame second,
        bool hasTwoHands,
        out SculptHandFrame rightHand,
        out SculptHandFrame leftHand,
        out bool hasRightHand,
        out bool hasLeftHand)
    {
        bool firstRight = IsScreenRightHand(first, second, hasTwoHands);
        if (hasTwoHands)
        {
            rightHand = firstRight ? first : second;
            leftHand = firstRight ? second : first;
            hasRightHand = true;
            hasLeftHand = true;
            return;
        }

        rightHand = first;
        leftHand = first;
        hasRightHand = firstRight;
        hasLeftHand = !firstRight;
    }

    private bool DrivePinchUp(SculptHandFrame hand, bool physicalRight)
    {
        string shapeName = physicalRight ? "Sharpness" : "Curve";
        if (!TryGetIndex(shapeName, out _))
        {
            ResolveBlendShapes();
            if (!TryGetIndex(shapeName, out _))
                return false;
        }

        PinchLift lift = physicalRight ? sharpnessLift : curveLift;
        bool drivingThis =
            lift.active ||
            (physicalRight
                ? currentGesture == GestureMode.PinchSharpness
                : currentGesture == GestureMode.PinchCurve);
        bool pinching = IsPinching(hand, physicalRight, ref lift.held, drivingThis);
        float dt = Mathf.Max(Time.unscaledDeltaTime, 0.0001f);
        if (!pinching)
        {
            CommitPinchLift(lift, physicalRight);
            SetBlendShapeWeight(
                shapeName,
                physicalRight ? latchedSharpness : latchedCurve);
            return false;
        }

        float rawY = PinchHeight(hand);
        if (!lift.active)
        {
            float latched = physicalRight ? latchedSharpness : latchedCurve;
            lift.active = true;
            lift.paused = false;
            lift.starting = latched;
            lift.current = latched;
            lift.target = latched;
            lift.velocity = 0f;
            lift.baselineY = rawY;
            lift.filteredY = rawY;
            lift.filterSeeded = true;
            lift.hold = GestureHoldGrace;
        }
        else
        {
            float follow = 1f - Mathf.Exp(-(physicalRight ? 22f : 18f) * dt);
            lift.filteredY = Mathf.Lerp(lift.filteredY, rawY, Mathf.Clamp01(follow));
        }

        lift.paused = false;
        lift.hold = GestureHoldGrace;
        float gain = physicalRight ? sharpnessLiftSensitivity : pinchLiftSensitivity;
        float delta = lift.filteredY - lift.baselineY;
        float dead = 0.0075f / Mathf.Max(hand.DistanceGain, 1f);
        if (Mathf.Abs(delta) < dead)
            delta = 0f;
        else
            delta -= Mathf.Sign(delta) * dead;
        // Lowering the pinch still retracts, but much slower so other poses
        // cannot slam sharpness/curve back to zero.
        if (delta < 0f)
            delta *= 0.18f;
        float target = Mathf.Clamp(
            lift.starting + delta * gain * hand.DistanceGain, 0f, 100f);
        float blendHz = delta < 0f
            ? (physicalRight ? 10f : 8f)
            : (physicalRight ? 28f : 22f);
        float blend = 1f - Mathf.Exp(-blendHz * dt);
        lift.current = Mathf.Lerp(lift.current, target, Mathf.Clamp01(blend));
        lift.target = target;
        SetBlendShapeWeight(shapeName, lift.current);
        if (physicalRight)
            latchedSharpness = lift.current;
        else
            latchedCurve = lift.current;

        if (Mathf.Abs(target - lift.starting) > 1.5f)
            madeDeformation = true;
        return true;
    }

    private static void PausePinchLift(PinchLift lift)
    {
        if (lift == null || !lift.active || lift.paused)
            return;
        lift.velocity = 0f;
        lift.paused = true;
    }

    private static void ResetPinchLift(PinchLift lift)
    {
        if (lift == null)
            return;
        lift.held = false;
        lift.active = false;
        lift.paused = false;
        lift.hold = 0f;
        lift.arm = 0f;
        lift.current = 0f;
        lift.target = 0f;
        lift.starting = 0f;
        lift.velocity = 0f;
        lift.filterSeeded = false;
    }

    private void CommitPinchLift(PinchLift lift, bool physicalRight)
    {
        if (lift == null)
            return;
        if (lift.active)
        {
            if (physicalRight)
            {
                latchedSharpness = lift.current;
                sharpnessAnchorY = lift.filteredY;
                hasSharpnessAnchor = lift.filterSeeded;
            }
            else
            {
                latchedCurve = lift.current;
                curveAnchorY = lift.filteredY;
                hasCurveAnchor = lift.filterSeeded;
            }
        }
        lift.held = false;
        lift.active = false;
        lift.paused = false;
        lift.hold = 0f;
        lift.arm = 0f;
        lift.velocity = 0f;
        lift.filterSeeded = false;
    }

    private void AbandonPinchLift(PinchLift lift, bool physicalRight)
    {
        if (lift == null)
            return;
        float restore = lift.active || lift.filterSeeded
            ? lift.starting
            : (physicalRight ? latchedSharpness : latchedCurve);
        if (physicalRight)
        {
            latchedSharpness = restore;
            SetBlendShapeWeight("Sharpness", restore);
        }
        else
        {
            latchedCurve = restore;
            SetBlendShapeWeight("Curve", restore);
        }

        lift.current = lift.target = restore;
        lift.held = false;
        lift.active = false;
        lift.paused = false;
        lift.hold = 0f;
        lift.arm = 0f;
        lift.velocity = 0f;
        lift.filterSeeded = false;
    }

    private static void StopUnusedPinch(PinchLift lift)
    {
        if (lift == null)
            return;
        lift.held = false;
        lift.active = false;
        lift.paused = false;
        lift.hold = 0f;
        lift.arm = 0f;
        lift.velocity = 0f;
        lift.filterSeeded = false;
    }

    private static float Farness(in SculptHandFrame hand)
    {
        return Mathf.Clamp01(Mathf.InverseLerp(0.10f, 0.030f, hand.PalmWidth));
    }

    private static bool HandsLookLikeTwoFists(SculptHandFrame first, SculptHandFrame second)
    {
        return LooksLikeHandleFist(first) && LooksLikeHandleFist(second);
    }

    private static bool IsClearPinchPose(SculptHandFrame hand)
    {
        return NormalizedPinch(hand) < Mathf.Lerp(1.05f, 1.28f, Farness(hand));
    }

    private void IsolateOneHandPinch(
        bool hasTwoHands,
        SculptHandFrame rightHand,
        SculptHandFrame leftHand,
        ref bool rightPinching,
        ref bool leftPinching)
    {
        if (!hasTwoHands || (!rightPinching && !leftPinching))
            return;

        if (currentGesture == GestureMode.PinchSharpness || sharpnessLift.active)
        {
            leftPinching = false;
            return;
        }

        if (currentGesture == GestureMode.PinchCurve || curveLift.active)
        {
            rightPinching = false;
            return;
        }

        if (rightPinching != leftPinching)
            return;

        float rightDist = NormalizedPinch(rightHand);
        float leftDist = NormalizedPinch(leftHand);
        if (Mathf.Abs(rightDist - leftDist) < 0.16f || rightDist <= leftDist)
            leftPinching = false;
        else
            rightPinching = false;
    }

    private static float NormalizedPinch(SculptHandFrame hand)
    {
        return hand.PinchDistance / Mathf.Max(hand.PalmWidth, 0.001f);
    }

    private static bool PalmsAreSeparate(SculptHandFrame first, SculptHandFrame second)
    {
        float dx = first.PalmCenter.x - second.PalmCenter.x;
        float dy = first.PalmCenter.y - second.PalmCenter.y;
        return (dx * dx + dy * dy) >= MinFistSeparation * MinFistSeparation;
    }

    private bool TryDriveCarving(SculptHandFrame first, SculptHandFrame second, bool twoVNow)
    {
        if (twoVNow)
            carvingHold = 0.22f;
        else
            carvingHold -= Time.unscaledDeltaTime;

        if (!carvingActive)
        {
            if (!twoVNow)
                return false;
            BeginCarving(first, second);
        }

        if (!twoVNow && carvingHold <= 0f)
        {
            EndCarving();
            return false;
        }

        carvingPaused = false;
        DriveCarving(first, second);
        if (Mathf.Abs(targetCarving - startingCarving) > 1.5f)
            madeDeformation = true;
        return true;
    }

    private void BeginCarving(SculptHandFrame first, SculptHandFrame second)
    {
        currentGesture = GestureMode.TwoVCarving;
        carvingActive = true;
        carvingActivation = 0f;
        startingCarving = carving;
        targetCarving = carving;
        carvingVelocity = 0f;
        filteredCarvingSpread = VSignSpread(first, second);
        filteredCarvingSpreadD = 0f;
        carvingSpreadSeeded = true;
        carvingBaseline = filteredCarvingSpread;
        carvingPaused = false;
    }

    private void DriveCarving(SculptHandFrame first, SculptHandFrame second)
    {
        float dt = Mathf.Max(Time.unscaledDeltaTime, 0.0001f);
        float rawSpread = VSignSpread(first, second);
        if (!carvingSpreadSeeded)
        {
            filteredCarvingSpread = rawSpread;
            filteredCarvingSpreadD = 0f;
            carvingSpreadSeeded = true;
        }
        else
        {
            FilterOneEuro(rawSpread, ref filteredCarvingSpread, ref filteredCarvingSpreadD, dt);
        }

        float gain = SculptHandFrame.CombinedDistanceGain(first, second);
        float delta = filteredCarvingSpread - carvingBaseline;
        if (Mathf.Abs(delta) < movementDeadZone / Mathf.Max(gain, 1f))
            return;

        float travel = Mathf.Max(carvingTravel, 0.01f) / gain;
        targetCarving = Mathf.Clamp(startingCarving + (delta / travel) * 100f, 0f, 100f);
        if (!useCarving2)
            ApplyFlowerAmount(targetCarving);
    }

    private void ApplyFlowerAmount(float amount)
    {
        if (kerisRenderer == null)
            return;
        KerisFlowerOverlay overlay = kerisRenderer.GetComponent<KerisFlowerOverlay>();
        if (overlay == null)
            overlay = KerisFlowerOverlay.Ensure(kerisRenderer);
        if (overlay != null)
            overlay.SetAmount(amount);
    }

    private void PauseCarving()
    {
        if (!carvingActive || carvingPaused)
            return;
        carvingVelocity = 0f;
        carvingPaused = true;
    }

    private void EndCarving()
    {
        latchedCarving = carving;
        carvingActive = false;
        carvingPaused = false;
        carvingActivation = 0f;
        carvingHold = 0f;
        targetCarving = carving;
        carvingSpreadSeeded = false;
        if (currentGesture == GestureMode.TwoVCarving)
            currentGesture = GestureMode.None;
    }

    private static bool IsVSign(SculptHandFrame hand)
    {
        return hand.IndexTipPalmRatio >= 1.06f &&
               hand.MiddleTipPalmRatio >= 1.08f &&
               hand.RingTipPalmRatio < 1.12f &&
               hand.PinkyTipPalmRatio < 1.12f &&
               hand.IndexTipPalmRatio >= hand.RingTipPalmRatio + 0.12f &&
               hand.MiddleTipPalmRatio >= hand.PinkyTipPalmRatio + 0.12f &&
               hand.PinchDistance >= hand.PalmWidth * 0.28f;
    }

    private static float VSignSpread(SculptHandFrame first, SculptHandFrame second)
    {
        float firstX = (first.IndexTip.x + first.MiddleTip.x) * 0.5f;
        float secondX = (second.IndexTip.x + second.MiddleTip.x) * 0.5f;
        return Mathf.Abs(secondX - firstX);
    }

    private void DriveHandleDown(SculptHandFrame first, SculptHandFrame second)
    {
        float rawY = (first.PalmCenter.y + second.PalmCenter.y) * 0.5f;
        float dt = Mathf.Max(Time.unscaledDeltaTime, 0.0001f);
        if (!handleDownArmed)
        {
            handleDownArmed = true;
            filteredFistY = rawY;
            filteredFistDy = 0f;
            fistYSeeded = true;
            handleDownStartY = rawY;
            handleStartWeight = ReadHandleAmount();
            CaptureHeldBodyShapes();
        }
        else if (!fistYSeeded)
        {
            filteredFistY = rawY;
            filteredFistDy = 0f;
            fistYSeeded = true;
        }
        else
        {
            float follow = 1f - Mathf.Exp(-26f * dt);
            filteredFistY = Mathf.Lerp(filteredFistY, rawY, follow);
        }

        float gain = SculptHandFrame.CombinedDistanceGain(first, second);
        float dead = HandleDeadZone / Mathf.Max(gain, 1f);
        float delta = handleDownStartY - filteredFistY;
        if (Mathf.Abs(delta) < dead)
            delta = 0f;
        else
            delta -= Mathf.Sign(delta) * dead;

        float target = Mathf.Clamp(
            handleStartWeight +
            delta * handleDownSensitivity * gain,
            0f,
            KerisHandleOverlay.HandleMaxAmount);
        float blend = 1f - Mathf.Exp(-handleEase * dt);
        float current = ReadHandleAmount();
        float smoothed = Mathf.Lerp(current, target, blend);
        SetHandleAmount(smoothed);
        latchedHandle = smoothed;
    }

    private void SetHandleAmount(float amount)
    {
        if (kerisRenderer == null)
            return;
        KerisHandleOverlay overlay = KerisHandleOverlay.Ensure(kerisRenderer);
        if (overlay == null)
            return;
        if (useHandle2)
            overlay.SetAmount(0f, amount);
        else
            overlay.SetAmount(amount, 0f);
    }

    private void ResolveBlendShapes()
    {
        blendShapeIndices.Clear();
        logicalToMeshName.Clear();
        if (kerisRenderer == null || kerisRenderer.sharedMesh == null)
            return;

        Mesh mesh = kerisRenderer.sharedMesh;
        MapShape("Carving 1", mesh, "Carving 1");
        MapShape("Carving 2", mesh, "Carving 2");
        MapShape("Handle 1", mesh, "Handle 1");
        MapShape("Handle 2", mesh, "Handle 2");
        MapShape("Width", mesh, "Weight", "Width");
        MapShape("Height", mesh, "Height");
        MapShape("Sharpness", mesh, "Sharpness");
        MapShape("Curve", mesh, "Curve");
        Debug.Log(
            "MORPHIC: Keris gestures resolved " + blendShapeIndices.Count +
            " shape keys on '" + mesh.name + "' (" + mesh.vertexCount + " verts). " +
            DescribeShape("Width") + " " + DescribeShape("Height") + " " +
            DescribeShape("Sharpness") + " " + DescribeShape("Curve"),
            this);
    }

    private void MapShape(string logicalName, Mesh mesh, params string[] candidates)
    {
        for (int c = 0; c < candidates.Length; c++)
        {
            for (int i = 0; i < mesh.blendShapeCount; i++)
            {
                string shape = mesh.GetBlendShapeName(i);
                if (!string.Equals(shape, candidates[c], System.StringComparison.OrdinalIgnoreCase))
                    continue;
                if (logicalName == "Carving 2" &&
                    shape.IndexOf(".001", System.StringComparison.OrdinalIgnoreCase) >= 0)
                    continue;

                blendShapeIndices[logicalName] = i;
                logicalToMeshName[logicalName] = shape;
                return;
            }
        }
    }

    private string DescribeShape(string logicalName)
    {
        if (kerisRenderer == null || kerisRenderer.sharedMesh == null)
            return logicalName + "=missing";
        if (!TryGetIndex(logicalName, out int index))
            return logicalName + "=unmapped";

        Mesh mesh = kerisRenderer.sharedMesh;
        if (mesh.GetBlendShapeFrameCount(index) <= 0)
            return logicalName + "=no-frames";

        Vector3[] delta = new Vector3[mesh.vertexCount];
        Vector3[] normals = new Vector3[mesh.vertexCount];
        Vector3[] tangents = new Vector3[mesh.vertexCount];
        mesh.GetBlendShapeFrameVertices(index, 0, delta, normals, tangents);
        float max = 0f;
        int moved = 0;
        for (int i = 0; i < delta.Length; i++)
        {
            float mag = delta[i].sqrMagnitude;
            if (mag <= 0.0000001f)
                continue;
            moved++;
            if (mag > max)
                max = mag;
        }

        return logicalName + "=" + moved + "v/" + Mathf.Sqrt(max).ToString("0.0");
    }

    private bool SetBlendShapeWeight(string logicalName, float weight)
    {
        if (!TryGetIndex(logicalName, out int index))
            return false;
        kerisRenderer.SetBlendShapeWeight(index, Mathf.Clamp(weight, 0f, 100f));
        return true;
    }

    private float ReadShape(string logicalName)
    {
        if (logicalName == "Carving 1" && kerisRenderer != null)
        {
            KerisFlowerOverlay overlay = kerisRenderer.GetComponent<KerisFlowerOverlay>();
            if (overlay != null)
                return overlay.flowerAmount;
        }

        return TryGetIndex(logicalName, out int index)
            ? kerisRenderer.GetBlendShapeWeight(index)
            : 0f;
    }

    private bool TryGetIndex(string logicalName, out int index)
    {
        return blendShapeIndices.TryGetValue(logicalName, out index);
    }

    private void ZeroUnusedShape(string logicalName)
    {
        if (logicalName == "Carving 1" && kerisRenderer != null)
        {
            KerisFlowerOverlay overlay = kerisRenderer.GetComponent<KerisFlowerOverlay>();
            if (overlay != null)
                overlay.SetAmount(0f);
            return;
        }

        if ((logicalName == "Handle 1" || logicalName == "Handle 2") && kerisRenderer != null)
        {
            KerisHandleOverlay overlay = kerisRenderer.GetComponent<KerisHandleOverlay>();
            if (overlay != null)
            {
                if (logicalName == "Handle 1")
                    overlay.SetAmount(0f, overlay.handle2Amount);
                else
                    overlay.SetAmount(overlay.handle1Amount, 0f);
            }
            return;
        }

        SetBlendShapeWeight(logicalName, 0f);
    }

    private void ReadBodyWeights()
    {
        width = targetWidth = latchedWidth = ReadShape("Width");
        height = targetHeight = latchedHeight = ReadShape("Height");
        carving = targetCarving = latchedCarving = ReadShape(ActiveCarvingName);
        sharpnessLift.current = sharpnessLift.target = ReadShape("Sharpness");
        curveLift.current = curveLift.target = ReadShape("Curve");
        latchedSharpness = sharpnessLift.current;
        latchedCurve = curveLift.current;
        latchedHandle = ReadHandleAmount();
    }

    private bool IsPinching(
        SculptHandFrame hand,
        bool physicalRight,
        ref bool held,
        bool alreadyActive = false)
    {
        if (IsOpenPalm(hand) || IsVSign(hand))
        {
            held = false;
            return false;
        }

        if (!alreadyActive && IsIndexPointing(hand, true))
        {
            held = false;
            return false;
        }

        float palm = Mathf.Max(hand.PalmWidth, 0.001f);
        float thumbIndex = hand.PinchDistance / palm;
        float thumbMiddle = Vector2.Distance(
            new Vector2(hand.ThumbTip.x, hand.ThumbTip.y),
            new Vector2(hand.MiddleTip.x, hand.MiddleTip.y)) / palm;
        float pinchDist = Mathf.Min(thumbIndex, thumbMiddle * 1.08f);
        float far = Farness(hand);
        float start = pinchThreshold + far * 0.20f;
        float release = Mathf.Max(pinchReleaseThreshold, start + 0.28f) + far * 0.16f;
        if (alreadyActive)
            release += 0.45f;
        if (!physicalRight)
        {
            start += 0.08f;
            release += 0.12f;
        }

        bool closePinch = pinchDist < start;
        held = held ? pinchDist < release : closePinch;
        return held;
    }

    private bool IsOpenPalm(SculptHandFrame hand)
    {
        float palm = Mathf.Max(hand.PalmWidth, 0.001f);
        return hand.PinchDistance >= palm * 0.55f &&
               hand.FingerCurlRatio >= 1.45f &&
               hand.OtherFingersCurlRatio >= 1.33f &&
               hand.IndexTipPalmRatio >= 1.05f &&
               !IsIndexPointing(hand, true);
    }

    private bool IsIndexPointing(SculptHandFrame hand)
    {
        return IsIndexPointing(hand, false);
    }

    private bool IsIndexPointing(SculptHandFrame hand, bool useActiveTolerance)
    {
        if (IsVSign(hand))
            return false;
        return hand.IndexExtensionAngle >= (useActiveTolerance ? 105f : 110f) &&
               hand.IndexTipPalmRatio >= 1.05f &&
               hand.MiddleTipPalmRatio < 1.18f &&
               hand.PinchDistance >= hand.PalmWidth * 0.28f;
    }

    private void FreezeOtherDeformations()
    {
        AbandonPinchLift(sharpnessLift, true);
        AbandonPinchLift(curveLift, false);
        PauseWidthHeight();
        PauseCarving();
        targetWidth = width;
        targetHeight = height;
        targetCarving = carving;
        sharpnessLift.current = sharpnessLift.target = latchedSharpness;
        curveLift.current = curveLift.target = latchedCurve;
        widthVelocity = 0f;
        heightVelocity = 0f;
        carvingVelocity = 0f;
        sharpnessLift.velocity = 0f;
        curveLift.velocity = 0f;
    }

    private static bool LooksLikeTightFist(SculptHandFrame hand)
    {
        return hand.IndexExtensionAngle < 125f &&
               hand.FingerCurlRatio <= 1.42f &&
               hand.IndexTipPalmRatio <= 1.32f &&
               hand.OtherFingersCurlRatio <= 1.42f &&
               hand.MiddleTipPalmRatio <= 1.28f;
    }

    private static bool LooksLikeClosedHand(SculptHandFrame hand)
    {
        return hand.FingerCurlRatio <= 1.58f &&
               hand.OtherFingersCurlRatio <= 1.58f &&
               hand.MiddleTipPalmRatio <= 1.42f;
    }

    private static bool LooksLikeHandleFist(SculptHandFrame hand)
    {
        if (NormalizedPinch(hand) < 0.65f)
            return false;
        return hand.FingerCurlRatio <= 1.45f &&
               hand.IndexTipPalmRatio <= 1.35f &&
               hand.OtherFingersCurlRatio <= 1.45f;
    }

    private static bool IsScreenRightHand(
        SculptHandFrame hand,
        SculptHandFrame other,
        bool hasTwoHands)
    {
        if (hand.HasHandedness)
            return !hand.IsRightHand;
        if (hasTwoHands)
            return hand.PalmCenter.x >= other.PalmCenter.x;
        return hand.PalmCenter.x >= 0.48f;
    }

    private static float PinchHeight(SculptHandFrame hand)
    {
        return (hand.ThumbTip.y + hand.IndexTip.y) * 0.5f;
    }

    private void ResetPinchTracking()
    {
        PausePinchLift(sharpnessLift);
        PausePinchLift(curveLift);
    }

    private void CaptureHeldBodyShapes()
    {
        heldSharpness = Mathf.Max(latchedSharpness, ReadShape("Sharpness"));
        heldCurve = Mathf.Max(latchedCurve, ReadShape("Curve"));
        latchedSharpness = heldSharpness;
        latchedCurve = heldCurve;
        heldWidth = ReadShape("Width");
        heldHeight = ReadShape("Height");
        heldCarving = ReadShape(ActiveCarvingName);
        holdBodyShapes = true;
        sharpnessLift.current = sharpnessLift.target = heldSharpness;
        curveLift.current = curveLift.target = heldCurve;
        width = targetWidth = heldWidth;
        height = targetHeight = heldHeight;
        carving = targetCarving = heldCarving;
    }

    private void RestoreHeldBodyShapes()
    {
        if (!holdBodyShapes)
            CaptureHeldBodyShapes();
        latchedSharpness = heldSharpness;
        latchedCurve = heldCurve;
        SetBlendShapeWeight("Sharpness", heldSharpness);
        SetBlendShapeWeight("Curve", heldCurve);
        SetBlendShapeWeight("Width", heldWidth);
        SetBlendShapeWeight("Height", heldHeight);
        SetBlendShapeWeight(ActiveCarvingName, heldCarving);
        sharpnessLift.current = sharpnessLift.target = heldSharpness;
        curveLift.current = curveLift.target = heldCurve;
        width = targetWidth = heldWidth;
        height = targetHeight = heldHeight;
        carving = targetCarving = heldCarving;
        sharpnessLift.velocity = 0f;
        curveLift.velocity = 0f;
        widthVelocity = 0f;
        heightVelocity = 0f;
        carvingVelocity = 0f;
    }

    private void ResetHandleDownTracking()
    {
        handleDownArmed = false;
        handleDownGrace = 0f;
        fistYSeeded = false;
        filteredFistDy = 0f;
        holdBodyShapes = false;
    }

    private void ResetWidthHeightTracking()
    {
        EndWidthHeight();
    }
}
