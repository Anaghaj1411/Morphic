using System.Collections.Generic;
using Mediapipe.Unity.Sample.HandLandmarkDetection;
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
    }

    [Header("References")]
    [SerializeField] private SculptHandLandmarkerRunner handRunner;
    [SerializeField] private SkinnedMeshRenderer kerisRenderer;

    [Header("Carving - Two V Outwards")]
    [SerializeField, Range(0.015f, 0.12f)] private float carvingTravel = 0.022f;

    [Header("Sharpness / Curve - Pinch Lift")]
    [SerializeField, Range(0.1f, 1.8f)] private float pinchThreshold = 0.75f;
    [SerializeField, Range(0.2f, 2.2f)] private float pinchReleaseThreshold = 1.05f;
    [SerializeField, Range(0.0008f, 0.04f)] private float pinchTravel = 0.012f;

    [Header("Handle - Two Fists Down")]
    [SerializeField, Min(1f)] private float handleDownSensitivity = 420f;
    [SerializeField, Min(1f)] private float handleEase = 22f;

    [Header("Body Width / Height - Two Index")]
    [SerializeField, Range(0.015f, 0.12f)] private float fullTravel = 0.022f;
    [SerializeField, Range(0.008f, 0.08f)] private float heightTravel = 0.012f;
    [SerializeField, Range(0.03f, 0.25f)] private float followTime = 0.12f;
    [SerializeField, Range(0.4f, 3f)] private float oneEuroMinCutoff = 0.55f;
    [SerializeField, Range(0.001f, 0.05f)] private float oneEuroBeta = 0.006f;
    [SerializeField, Range(0f, 0.03f)] private float movementDeadZone = 0.0025f;
    [SerializeField, Range(0.02f, 0.5f)] private float activationTime = 0.05f;
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
    private const float HandleGraceDuration = 0.35f;
    private const float HandleDeadZone = 0.005f;
    private float heldSharpness;
    private float heldCurve;
    private float heldWidth;
    private float heldHeight;
    private float heldCarving;
    private bool holdBodyShapes;
    private const float TwoHandPinchCommit = 1.05f;
    private const float MinFistSeparation = 0.10f;
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
        if (pinchTravel > 0.002f)
            pinchTravel = 0.0012f;
        if (handleDownSensitivity > 1600f || handleDownSensitivity < 400f)
            handleDownSensitivity = 900f;
        if (handleEase > 48f || handleEase < 18f)
            handleEase = 34f;

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

    private void Update()
    {
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
        if (twoVNow)
            carvingHold = widthHeightHoldTime;
        else
            carvingHold -= Time.unscaledDeltaTime;

        bool twoIndex = !twoVNow && hasTwoHands &&
                        IsIndexPointing(first, widthHeightActive) &&
                        IsIndexPointing(second, widthHeightActive);
        if (twoIndex)
            widthHeightHold = widthHeightHoldTime;
        else
            widthHeightHold -= Time.unscaledDeltaTime;

        bool bothFists = !twoVNow && !twoIndex &&
                         hasTwoHands &&
                         PalmsAreSeparate(first, second) &&
                         LooksLikeHandleFist(first) &&
                         LooksLikeHandleFist(second);
        bool twoFistsNow = bothFists;
        if (twoFistsNow)
            handleDownGrace = HandleGraceDuration;
        else
            handleDownGrace -= Time.deltaTime;

        bool rightPinching = !twoFistsNow && hasRightHand &&
                             IsPinching(rightHand, true, ref sharpnessLift.held);
        bool leftPinching = !twoFistsNow && hasLeftHand &&
                            IsPinching(leftHand, false, ref curveLift.held);
        IsolateOneHandPinch(hasTwoHands, rightHand, leftHand, ref rightPinching, ref leftPinching);
        bool pinching = !twoFistsNow && (rightPinching || leftPinching);
        bool sculptGesture = twoIndex || twoVNow || twoFistsNow || pinching;

        // --- STRICT GESTURE ISOLATION ---
        // If we are already doing something, stay in that mode until released.
        if (currentGesture != GestureMode.None)
        {
            bool keepCurrent = false;
            switch (currentGesture)
            {
                case GestureMode.WidthHeight:
                    keepCurrent = twoIndex || (widthHeightActive && widthHeightHold > 0f);
                    if (keepCurrent && hasTwoHands)
                    {
                        if (TryDriveWidthHeight(first, second, twoIndex))
                        {
                            IsDrivingSculpt = !widthHeightPaused;
                            return;
                        }
                    }
                    EndWidthHeight();
                    break;

                case GestureMode.TwoVCarving:
                    keepCurrent = twoVNow || (carvingActive && carvingHold > 0f);
                    if (keepCurrent && hasTwoHands)
                    {
                        if (TryDriveCarving(first, second, twoVNow))
                        {
                            IsDrivingSculpt = !carvingPaused;
                            return;
                        }
                    }
                    EndCarving();
                    break;

                case GestureMode.HandleDown:
                    keepCurrent = twoFistsNow || (handleDownArmed && handleDownGrace > 0f);
                    if (keepCurrent && hasTwoHands && !twoVNow && !twoIndex)
                    {
                        FreezeOtherDeformations();
                        DriveHandleDown(first, second);
                        RestoreHeldBodyShapes();
                        madeDeformation = true;
                        IsDrivingSculpt = true;
                        return;
                    }
                    ResetHandleDownTracking();
                    break;

                case GestureMode.PinchSharpness:
                    keepCurrent = rightPinching || (sharpnessLift.active && sharpnessLift.hold > 0f);
                    if (keepCurrent && hasRightHand)
                    {
                        if (DrivePinchUp(rightHand, true))
                        {
                            IsDrivingSculpt = true;
                            return;
                        }
                    }
                    StopUnusedPinch(sharpnessLift);
                    break;

                case GestureMode.PinchCurve:
                    keepCurrent = leftPinching || (curveLift.active && curveLift.hold > 0f);
                    if (keepCurrent && hasLeftHand)
                    {
                        if (DrivePinchUp(leftHand, false))
                        {
                            IsDrivingSculpt = true;
                            return;
                        }
                    }
                    StopUnusedPinch(curveLift);
                    break;
            }

            currentGesture = GestureMode.None;
        }

        // --- NEW GESTURE DETECTION ---
        // Only look for new gestures if we aren't already doing one.
        bool openPalm = IsOpenPalm(first) || (hasTwoHands && IsOpenPalm(second));
        UpdateLockState(openPalm, twoIndex || twoVNow || pinching, sculptGesture);
        if (sculptGesture)
            deformationLocked = false;

        if (deformationLocked)
        {
            ResetAllTracking();
            IsDrivingSculpt = false;
            return;
        }

        // Priority: Carving > Width/Height > Handle > Pinch
        if (twoVNow && hasTwoHands)
        {
            if (TryDriveCarving(first, second, true))
            {
                currentGesture = GestureMode.TwoVCarving;
                IsDrivingSculpt = true;
                return;
            }
        }

        if (twoIndex && hasTwoHands)
        {
            if (TryDriveWidthHeight(first, second, true))
            {
                currentGesture = GestureMode.WidthHeight;
                IsDrivingSculpt = true;
                return;
            }
        }

        if (twoFistsNow && hasTwoHands)
        {
            currentGesture = GestureMode.HandleDown;
            FreezeOtherDeformations();
            DriveHandleDown(first, second);
            RestoreHeldBodyShapes();
            madeDeformation = true;
            IsDrivingSculpt = true;
            return;
        }

        if (rightPinching && hasRightHand)
        {
            if (DrivePinchUp(rightHand, true))
            {
                currentGesture = GestureMode.PinchSharpness;
                IsDrivingSculpt = true;
                return;
            }
        }

        if (leftPinching && hasLeftHand)
        {
            if (DrivePinchUp(leftHand, false))
            {
                currentGesture = GestureMode.PinchCurve;
                IsDrivingSculpt = true;
                return;
            }
        }

        currentGesture = GestureMode.None;
        IsDrivingSculpt = false;
    }

    private void ResetAllTracking()
    {
        ResetPinchTracking();
        ResetHandleDownTracking();
        PauseWidthHeight();
        PauseCarving();
        PausePinchLift(sharpnessLift);
        PausePinchLift(curveLift);
        targetWidth = width;
        targetHeight = height;
        targetCarving = carving;
        sharpnessLift.target = sharpnessLift.current;
        curveLift.target = curveLift.current;
        currentGesture = GestureMode.None;
    }

    private void LateUpdate()
    {
        float dt = Mathf.Max(Time.unscaledDeltaTime, 0.0001f);
        bool holdOthers = currentGesture == GestureMode.HandleDown;
        if (holdOthers)
            RestoreHeldBodyShapes();
        ApplySmoothedShape(
            ref width,
            ref targetWidth,
            ref widthVelocity,
            "Width",
            widthHeightActive && !holdOthers,
            dt);
        ApplySmoothedShape(
            ref height,
            ref targetHeight,
            ref heightVelocity,
            "Height",
            widthHeightActive && !holdOthers,
            dt);
        ApplySmoothedShape(
            ref sharpnessLift.current,
            ref sharpnessLift.target,
            ref sharpnessLift.velocity,
            "Sharpness",
            sharpnessLift.active && !holdOthers,
            dt,
            0.12f);
        ApplySmoothedShape(
            ref curveLift.current,
            ref curveLift.target,
            ref curveLift.velocity,
            "Curve",
            curveLift.active && !holdOthers,
            dt,
            0.12f);
        ApplySmoothedShape(
            ref carving,
            ref targetCarving,
            ref carvingVelocity,
            ActiveCarvingName,
            carvingActive && !holdOthers,
            dt);
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

        float damp = easeTime > 0f ? easeTime : followTime;
        current = Mathf.SmoothDamp(current, target, ref velocity, damp, 480f, dt);
        if (logicalName == "Carving 1")
        {
            ApplyFlowerAmount(current);
            return;
        }

        SetBlendShapeWeight(logicalName, current);
    }

    private bool TryDriveWidthHeight(SculptHandFrame first, SculptHandFrame second, bool pointingNow)
    {
        if (!widthHeightActive)
        {
            if (!pointingNow)
                return false;
            widthHeightActivation += Time.unscaledDeltaTime;
            if (widthHeightActivation < activationTime)
                return false;
            BeginWidthHeight(first, second);
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
        float widthSpan = Mathf.Max(fullTravel, 0.01f);
        float heightSpan = Mathf.Max(heightTravel, 0.008f);

        float widthDelta = spread - widthHeightBaselineX;
        if (Mathf.Abs(widthDelta) >= movementDeadZone)
            targetWidth = Mathf.Clamp(startingWidth + (widthDelta / widthSpan) * 100f, 0f, 100f);

        float heightDelta =
            (palmLift - widthHeightBaselinePalmY) +
            (stagger - widthHeightBaselineStagger);
        if (Mathf.Abs(heightDelta) >= movementDeadZone)
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
        if (!TryGetIndex(shapeName, out int index))
        {
            ResolveBlendShapes();
            if (!TryGetIndex(shapeName, out index))
                return false;
        }

        PinchLift lift = physicalRight ? sharpnessLift : curveLift;
        bool pinching = IsPinching(hand, physicalRight, ref lift.held);
        if (pinching)
            lift.hold = pinchHoldTime;
        else
            lift.hold -= Time.unscaledDeltaTime;

        bool keep = pinching || (lift.active && lift.hold > 0f && !IsOpenPalm(hand));
        if (!keep)
        {
            lift.active = false;
            lift.filterSeeded = false;
            return false;
        }

        float rawY = PinchHeight(hand);
        float dt = Mathf.Max(Time.unscaledDeltaTime, 0.0001f);
        if (!lift.active)
        {
            lift.active = true;
            lift.paused = false;
            lift.starting = kerisRenderer.GetBlendShapeWeight(index);
            lift.current = lift.starting;
            lift.target = lift.starting;
            lift.velocity = 0f;
            lift.filteredY = rawY;
            lift.filteredDy = 0f;
            lift.filterSeeded = true;
            lift.baselineY = rawY;
        }
        else if (!lift.filterSeeded)
        {
            lift.filteredY = rawY;
            lift.filteredDy = 0f;
            lift.filterSeeded = true;
        }
        else
        {
            FilterOneEuro(rawY, ref lift.filteredY, ref lift.filteredDy, dt);
        }

        lift.paused = false;
        float travel = Mathf.Max(pinchTravel, 0.0008f);
        float delta = lift.filteredY - lift.baselineY;
        if (Mathf.Abs(delta) >= travel * 0.08f)
            lift.target = Mathf.Clamp(lift.starting + (delta / travel) * 100f, 0f, 100f);

        if (Mathf.Abs(lift.target - lift.starting) > 1.5f)
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
        lift.current = 0f;
        lift.target = 0f;
        lift.starting = 0f;
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
        lift.velocity = 0f;
        lift.filterSeeded = false;
    }

    private static void IsolateOneHandPinch(
        bool hasTwoHands,
        SculptHandFrame rightHand,
        SculptHandFrame leftHand,
        ref bool rightPinching,
        ref bool leftPinching)
    {
        if (!hasTwoHands || (!rightPinching && !leftPinching))
            return;

        float rightDist = NormalizedPinch(rightHand);
        float leftDist = NormalizedPinch(leftHand);
        bool rightCommit = rightPinching && rightDist < TwoHandPinchCommit;
        bool leftCommit = leftPinching && leftDist < TwoHandPinchCommit;
        if (rightCommit != leftCommit)
        {
            rightPinching = rightCommit;
            leftPinching = leftCommit;
            return;
        }

        if (!rightCommit && !leftCommit)
        {
            rightPinching = false;
            leftPinching = false;
            return;
        }

        if (rightDist <= leftDist)
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
        if (!carvingActive)
        {
            if (!twoVNow)
                return false;
            carvingActivation += Time.unscaledDeltaTime;
            if (carvingActivation < activationTime)
                return false;
            BeginCarving(first, second);
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

        float delta = filteredCarvingSpread - carvingBaseline;
        if (Mathf.Abs(delta) < movementDeadZone)
            return;

        float travel = Mathf.Max(carvingTravel, 0.01f);
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
        return hand.IndexTipPalmRatio >= 1.08f &&
               hand.MiddleTipPalmRatio >= 1.08f &&
               hand.RingTipPalmRatio < 0.98f &&
               hand.PinkyTipPalmRatio < 0.98f &&
               hand.PinchDistance >= hand.PalmWidth * 0.55f;
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
            float follow = 1f - Mathf.Exp(-36f * dt);
            filteredFistY = Mathf.Lerp(filteredFistY, rawY, follow);
        }

        float delta = handleDownStartY - filteredFistY;
        if (Mathf.Abs(delta) < HandleDeadZone)
            delta = 0f;
        else
            delta -= Mathf.Sign(delta) * HandleDeadZone;

        float target = Mathf.Clamp(
            handleStartWeight + delta * handleDownSensitivity,
            0f,
            KerisHandleOverlay.HandleMaxAmount);
        float blend = 1f - Mathf.Exp(-handleEase * dt);
        float current = ReadHandleAmount();
        float smoothed = Mathf.Lerp(current, target, blend);
        SetHandleAmount(smoothed);
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
        width = targetWidth = ReadShape("Width");
        height = targetHeight = ReadShape("Height");
        carving = targetCarving = ReadShape(ActiveCarvingName);
        sharpnessLift.current = sharpnessLift.target = ReadShape("Sharpness");
        curveLift.current = curveLift.target = ReadShape("Curve");
    }

    private bool IsPinching(SculptHandFrame hand, bool physicalRight, ref bool held)
    {
        if (IsOpenPalm(hand) || IsVSign(hand))
        {
            held = false;
            return false;
        }

        // A pinch requires the index finger to be somewhat extended.
        // If it's curled (low angle), it's a fist and must not drive curve/sharpness.
        if (LooksLikeHandleFist(hand) || hand.IndexExtensionAngle < 130f)
        {
            held = false;
            return false;
        }

        float palm = Mathf.Max(hand.PalmWidth, 0.001f);
        float pinchDist = hand.PinchDistance / palm;
        float start = pinchThreshold;
        float release = Mathf.Max(pinchReleaseThreshold, pinchThreshold + 0.3f);
        if (!physicalRight)
        {
            start += 0.2f;
            release += 0.2f;
        }

        held = held ? pinchDist < release : pinchDist < start;
        return held;
    }

    private bool IsOpenPalm(SculptHandFrame hand)
    {
        float palm = Mathf.Max(hand.PalmWidth, 0.001f);
        return hand.PinchDistance >= palm * 0.48f &&
               hand.FingerCurlRatio >= 1.28f &&
               hand.OtherFingersCurlRatio >= 1.22f &&
               hand.IndexTipPalmRatio >= 1.0f &&
               !IsIndexPointing(hand, true);
    }

    private bool IsIndexPointing(SculptHandFrame hand)
    {
        return IsIndexPointing(hand, false);
    }

    private bool IsIndexPointing(SculptHandFrame hand, bool useActiveTolerance)
    {
        float requiredAngle = useActiveTolerance ? activeMinimumIndexAngle : minimumIndexAngle;
        float allowedOther = useActiveTolerance
            ? activeMaximumOtherFingerExtension
            : maximumOtherFingerExtension;
        return hand.IndexExtensionAngle >= requiredAngle &&
               hand.IndexTipPalmRatio >= minimumIndexTipExtension &&
               hand.OtherFingersCurlRatio <= allowedOther &&
               hand.MiddleTipPalmRatio <= (useActiveTolerance ? 1.15f : 1.02f) &&
               hand.PinchDistance >= hand.PalmWidth * minimumThumbIndexSeparation;
    }

    private void FreezeOtherDeformations()
    {
        StopUnusedPinch(sharpnessLift);
        StopUnusedPinch(curveLift);
        PauseWidthHeight();
        PauseCarving();
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

    private static bool LooksLikeHandleFist(SculptHandFrame hand)
    {
        // A fist has a curled index finger (low extension angle).
        return hand.IndexExtensionAngle < 120f &&
               hand.FingerCurlRatio <= 1.38f &&
               hand.IndexTipPalmRatio <= 1.28f &&
               hand.OtherFingersCurlRatio <= 1.38f &&
               hand.MiddleTipPalmRatio <= 1.22f;
    }

    private static bool IsScreenRightHand(
        SculptHandFrame hand,
        SculptHandFrame other,
        bool hasTwoHands)
    {
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
        heldSharpness = ReadShape("Sharpness");
        heldCurve = ReadShape("Curve");
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
