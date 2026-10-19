using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Mediapipe.Unity.Sample.HandLandmarkDetection;

// Old-style lantern sculpt: every key is a blend shape on the same
// SkinnedMeshRenderer. Width/Height therefore move the top and bottom
// rings with the body. Pop keys are the same sliders as the Inspector.
[DefaultExecutionOrder(250)]
public class TaiwanLanternGestureController : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private SculptHandLandmarkerRunner handRunner;
    [SerializeField] private SkinnedMeshRenderer lanternRenderer;
    [SerializeField] private TMP_Text gestureIndicator;

    [Header("BlendShape Names")]
    [SerializeField] private string widthShape = "Width";
    [SerializeField] private string heightShape = "Height";
    [SerializeField] private string topperShape = "Topper";
    [SerializeField] private string bottomShape = "Bottom";
    [SerializeField] private string middleShape = "Middle";
    [SerializeField] private string popUpTopShape = "TOP CIRCLE POP UP";
    [SerializeField] private string popUpBambooShape = "Pop Up Bamboo";
    [SerializeField] private string popUpBottomShape = "CIRCLE BOTTOM POP UP NEW";

    [Header("Width / Height")]
    [SerializeField] private float widthSensitivity = 150f;
    [SerializeField] private float heightSensitivity = 150f;
    [SerializeField, Range(0f, 0.05f)] private float movementDeadZone = 0.012f;
    [SerializeField, Range(90f, 175f)] private float minimumPointingIndexAngle = 125f;
    [SerializeField, Range(0.5f, 2f)] private float maximumOtherFingerExtension = 1.45f;
    [SerializeField, Range(0.8f, 1.8f)] private float minimumIndexTipExtension = 1.08f;
    [SerializeField, Range(0.2f, 0.8f)] private float minimumThumbIndexSeparation = 0.42f;

    [Header("Pinch / Thumb")]
    [SerializeField] private float pinchThreshold = 0.72f;
    [SerializeField] private float pinchReleaseThreshold = 0.95f;
    [SerializeField] private float leftPinchBoost = 1.35f;
    [SerializeField] private float popConfirmTime = 0f;
    [SerializeField] private float popEase = 12f;
    [SerializeField] private float popLiftSensitivity = 420f;
    [SerializeField] private float popPinchSensitivity = 920f;
    [SerializeField] private float popPinchEase = 28f;
    [SerializeField] private float smoothing = 18f;
    [SerializeField, Range(0.05f, 0.5f)] private float minimumThumbLength = 0.08f;
    [SerializeField, Min(0.05f)] private float shapeEaseTime = 0.12f;
    [SerializeField, Range(1.1f, 2f)] private float openPalmExtension = 1.32f;

    [Header("Unused (kept so scene serialization stays valid)")]
    [SerializeField] private float fistThreshold = 1.08f;
    [SerializeField] private float gestureGracePeriod = 0.5f;
    [SerializeField] private float topperEaseTime = 0.65f;
    [SerializeField] private float middleEaseTime = 0.28f;
    [SerializeField] private float middleMinPinchSeparation = 1f;
    [SerializeField] private float middleMaxPinchSeparation = 4f;
    [SerializeField] private float middleHoldStillTolerance = 0.05f;
    [SerializeField] private float singleThumbModeGracePeriod = 0.4f;
    [SerializeField] private float popUpMinimumHandHeight = 0.10f;
    [SerializeField] private float popUpMaximumHandHeight = 0.42f;
    [SerializeField] private float popUpGestureActivationTime = 0.08f;
    [SerializeField] private float bambooMinimumVSeparation = 1.2f;
    [SerializeField] private float bambooMaximumVSeparation = 4f;
    [SerializeField] private float gestureLockHoldDuration = 1f;
    [SerializeField] private float thumbGestureLockHoldDuration = 2f;
    [SerializeField] private float indexHoldStillTolerance = 0.01f;
    [SerializeField] private float thumbHoldStillTolerance = 15f;
    [SerializeField] private float thumbOutputDeadZone = 6f;

    private const int Width = 0;
    private const int Height = 1;
    private const int Topper = 2;
    private const int Bottom = 3;
    private const int PopTop = 4;
    private const int PopBottom = 5;
    private const int PopBamboo = 6;
    private const int PopTassel = 7;
    private const int ChannelCount = 8;

    private readonly int[] shapeIndex = new int[ChannelCount];
    private readonly float[] current = new float[ChannelCount];
    private readonly string[] lookupNames = new string[ChannelCount];


    private bool ownWidthHeight = true;
    private bool widthHeightArmed;
    private int widthHeightAxis;
    private float widthHeightBaselineX;
    private float widthHeightBaselineY;
    private float widthHeightStartW;
    private float widthHeightStartH;
    private bool externalFistRotationActive;
    private float committedTopper;
    private float committedBottom;
    private bool topperLiveThisFrame;
    private bool bottomLiveThisFrame;
    private bool rightVArmed;
    private bool leftVArmed;
    private float rightVStartY;
    private float leftVStartY;
    private float rightVStart;
    private float leftVStart;

    private CanvasGroup progressCardGroup;
    private TMP_Text progressPartText;
    private TMP_Text progressPercentText;
    private Image progressBarFill;
    private Image progressAccentStripe;
    private bool showProgressCard;
    private bool deformationLocked;

    public bool IsDeformationLocked => deformationLocked;
    private bool rightPinchHeld;
    private bool practiceRightPinchHeld;
    private bool practiceLeftPinchHeld;
    private bool leftPinchHeld;
    private float rightPinchTimer;
    private float leftPinchTimer;
    private bool rightPopArmed;
    private bool leftPopArmed;
    private float rightPopStartY;
    private float leftPopStartY;
    private float rightPopStart;
    private float leftPopStart;
    private bool bambooArmed;
    private float bambooStartY;
    private float bambooStart;
    private bool tasselArmed;
    private float tasselStartY;
    private float tasselStart;
    private bool madeDeformation;
    public bool suppressDriving;
    private float[] restBlendWeights;
    private Quaternion restLocalRotation;
    private float restCommittedTopper;
    private float restCommittedBottom;
    private bool topPopUsesRectangle;
    private bool bottomPopUsesRectangle;
    private bool tasselUsesTriple;
    private int unusedPopTopShape = -1;
    private int unusedPopBottomShape = -1;
    private int unusedTasselShape = -1;
    private string popTopLabel = "TOP CIRCLE";
    private string popBottomLabel = "BOTTOM CIRCLE";

    private void Awake()
    {
        lookupNames[Width] = widthShape;
        lookupNames[Height] = heightShape;
        lookupNames[Topper] = topperShape;
        lookupNames[Bottom] = bottomShape;
        lookupNames[PopTop] = popUpTopShape;
        lookupNames[PopBottom] = popUpBottomShape;
        lookupNames[PopBamboo] = popUpBambooShape;
        lookupNames[PopTassel] = "3 TASSEL POP OUT";

        SculptingEnhancementIndexGestureController indexDriver =
            GetComponent<SculptingEnhancementIndexGestureController>();
        ownWidthHeight = indexDriver == null || !indexDriver.enabled;

        BindLantern();
        ResolveShapes();
        CreateProgressCard();
        if (lanternRenderer != null)
            lanternRenderer.updateWhenOffscreen = true;
    }

    private void OnEnable()
    {
        BindLantern();
        ResolveShapes();
        SyncCurrentFromRenderer();
    }

    public void SyncCurrentFromRenderer()
    {
        if (lanternRenderer == null || lanternRenderer.sharedMesh == null)
            return;

        for (int c = 0; c < ChannelCount; c++)
        {
            if (shapeIndex[c] >= 0)
                current[c] = lanternRenderer.GetBlendShapeWeight(shapeIndex[c]);
        }

        committedTopper = current[Topper];
        committedBottom = current[Bottom];
    }

    public void ArmPopVariant(bool isTop, bool useRectangle)
    {
        if (isTop)
            topPopUsesRectangle = useRectangle;
        else
            bottomPopUsesRectangle = useRectangle;

        BindLantern();
        ApplyPopVariantBindings();
        SyncCurrentFromRenderer();
    }

    public void ArmTasselVariant(bool useTriple)
    {
        tasselUsesTriple = useTriple;
        BindLantern();
        ApplyTasselBinding();
        int tassel = ActiveTasselChannel();
        if (tassel >= 0 && shapeIndex[tassel] >= 0)
        {
            lanternRenderer.SetBlendShapeWeight(shapeIndex[tassel], 0f);
            current[tassel] = 0f;
        }

        if (unusedTasselShape >= 0)
            lanternRenderer.SetBlendShapeWeight(unusedTasselShape, 0f);
        SyncCurrentFromRenderer();
    }

    private void Update()
    {
        if (suppressDriving)
            return;

        if (Input.GetKeyDown(KeyCode.T))
            current[PopTop] = current[PopTop] > 50f ? 0f : 100f;
        if (Input.GetKeyDown(KeyCode.B))
            current[PopBottom] = current[PopBottom] > 50f ? 0f : 100f;
        if (Input.GetKeyDown(KeyCode.V))
            current[PopBamboo] = current[PopBamboo] > 50f ? 0f : 100f;
        if (Input.GetKeyDown(KeyCode.G))
        {
            int tassel = ActiveTasselChannel();
            if (tassel >= 0)
                current[tassel] = current[tassel] > 50f ? 0f : 100f;
        }

        topperLiveThisFrame = false;
        bottomLiveThisFrame = false;

        if (handRunner == null || lanternRenderer == null)
        {
            SetStatus("GESTURE: NONE");
            return;
        }


        if (externalFistRotationActive)
        {
            RestoreCommittedThumbShapes();
            SetStatus("GESTURE: FIST ROTATE");
            return;
        }

        bool hasTwo = handRunner.TryGetLatestTwoHands(
            out SculptHandFrame first, out SculptHandFrame second);
        if (!hasTwo && !handRunner.TryGetLatestHand(out first))
        {
            widthHeightArmed = false;
            bambooArmed = false;
            tasselArmed = false;
            rightVArmed = false;
            leftVArmed = false;
            SetStatus("GESTURE: NO HANDS");
            return;
        }

        if (!hasTwo)
            second = default;

        float blend = 1f - Mathf.Exp(-smoothing * Time.deltaTime);
        bool twoV = hasTwo && IsVSign(first) && IsVSign(second);
        bool twoFists = !twoV && hasTwo && LooksLikeFist(first) && LooksLikeFist(second);
        bool foundLeftV = TryGetHandV(first, second, hasTwo, false, out SculptHandFrame leftV);
        bool foundRightV = TryGetHandV(first, second, hasTwo, true, out SculptHandFrame rightV);
        bool leftVBottom = !twoV && foundLeftV;
        bool rightVTopper = !twoV && foundRightV;
        bool twoIndex = !twoV && !leftVBottom && !rightVTopper && hasTwo &&
                        IsIndexPointing(first) && IsIndexPointing(second);
        int openPalms = CountOpenPalms(first, second, hasTwo);
        bool firstRight = IsPhysicalRightHand(first, second, hasTwo);
        ref bool firstHeld = ref (firstRight ? ref rightPinchHeld : ref leftPinchHeld);
        bool pinchOrV = IsPinching(first, firstRight, ref firstHeld) ||
                        leftVBottom || rightVTopper;
        if (hasTwo)
        {
            bool secondRight = IsPhysicalRightHand(second, first, true);
            ref bool secondHeld = ref (secondRight ? ref rightPinchHeld : ref leftPinchHeld);
            pinchOrV = pinchOrV || IsPinching(second, secondRight, ref secondHeld);
        }

        if (openPalms >= 1 && madeDeformation && !twoIndex && !twoFists && !twoV && !pinchOrV)
            deformationLocked = true;

        if (twoIndex || twoFists || twoV || pinchOrV)
            deformationLocked = false;

        if (deformationLocked)
        {
            widthHeightArmed = false;
            bambooArmed = false;
            tasselArmed = false;
            SetStatus("LOCKED  |  open palm  |  TOP " +
                      current[PopTop].ToString("0") +
                      "  BOT " + current[PopBottom].ToString("0") +
                      "  BAM " + current[PopBamboo].ToString("0") +
                      "  TAS " + current[ActiveTasselChannel()].ToString("0"));
            return;
        }

        if (ownWidthHeight)
        {
            if (twoIndex)
            {
                DriveWidthHeight(first, second, blend);
                madeDeformation = true;
            }
            else
                widthHeightArmed = false;
        }

        if (twoV)
            DriveBambooSpread(first, second);
        else
            bambooArmed = false;

        if (twoFists)
            DriveTasselDown(first, second);
        else
            tasselArmed = false;

        if (rightVTopper)
            DriveVShape(rightV, Topper, blend, ref rightVArmed, ref rightVStartY,
                ref rightVStart, ref committedTopper, ref topperLiveThisFrame);
        else
            rightVArmed = false;

        if (leftVBottom)
            DriveVShape(leftV, Bottom, blend, ref leftVArmed, ref leftVStartY,
                ref leftVStart, ref committedBottom, ref bottomLiveThisFrame);
        else
            leftVArmed = false;

        if (!twoIndex && !twoV && !twoFists)
        {
            if (!IsVSign(first))
                DrivePop(first, firstRight, blend);
            if (hasTwo && !IsVSign(second))
                DrivePop(second, IsPhysicalRightHand(second, first, true), blend);
        }

        SetStatus(
            BuildStatus(twoIndex, rightVTopper, leftVBottom, twoFists, twoV, hasTwo, first, second) +
            "  |  TOP " + current[PopTop].ToString("0") +
            "  BOT " + current[PopBottom].ToString("0") +
            "  BAM " + current[PopBamboo].ToString("0") +
            "  TAS " + current[ActiveTasselChannel()].ToString("0"));
    }

    public void SetExternalFistRotationActive(bool active)
    {
        externalFistRotationActive = active;
        if (!active)
            return;

        RestoreCommittedThumbShapes();
        widthHeightArmed = false;
        bambooArmed = false;
        tasselArmed = false;
    }

    public void SynchronizeWeightsFromRenderer()
    {
        SyncCurrentFromRenderer();
    }

    public void CaptureRestState()
    {
        if (lanternRenderer == null || lanternRenderer.sharedMesh == null)
            return;

        restLocalRotation = lanternRenderer.transform.localRotation;
        restCommittedTopper = committedTopper;
        restCommittedBottom = committedBottom;
        Mesh mesh = lanternRenderer.sharedMesh;
        restBlendWeights = new float[mesh.blendShapeCount];
        for (int i = 0; i < restBlendWeights.Length; i++)
            restBlendWeights[i] = lanternRenderer.GetBlendShapeWeight(i);
    }

    public void RestoreRestState()
    {
        if (lanternRenderer == null || restBlendWeights == null)
            return;

        lanternRenderer.transform.localRotation = restLocalRotation;
        for (int i = 0; i < restBlendWeights.Length; i++)
            lanternRenderer.SetBlendShapeWeight(i, restBlendWeights[i]);

        committedTopper = restCommittedTopper;
        committedBottom = restCommittedBottom;
        SyncCurrentFromRenderer();
        widthHeightArmed = false;
        bambooArmed = false;
        tasselArmed = false;
        rightVArmed = false;
        leftVArmed = false;
        madeDeformation = false;
        deformationLocked = false;
        topperLiveThisFrame = false;
        bottomLiveThisFrame = false;
    }

    public enum PracticeGesture
    {
        None,
        TwoIndex,
        TwoFists,
        RightPinch,
        LeftPinch,
        RightV,
        LeftV,
        SingleFist,
        TwoV,
        OpenPalm
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
        bool twoFists = !twoV && hasTwo && LooksLikeFist(first) && LooksLikeFist(second);
        bool foundRightV = TryGetHandV(first, second, hasTwo, true, out _);
        bool foundLeftV = TryGetHandV(first, second, hasTwo, false, out _);
        bool twoIndex = !twoV && !foundLeftV && !foundRightV && hasTwo &&
                        IsIndexPointing(first) && IsIndexPointing(second);

        bool rightPinch = false;
        bool leftPinch = false;
        bool firstRight = IsPhysicalRightHand(first, second, hasTwo);
        if (firstRight)
            rightPinch = IsPinching(first, true, ref practiceRightPinchHeld);
        else
            leftPinch = IsPinching(first, false, ref practiceLeftPinchHeld);
        if (hasTwo)
        {
            bool secondRight = IsPhysicalRightHand(second, first, true);
            if (secondRight)
                rightPinch = rightPinch || IsPinching(second, true, ref practiceRightPinchHeld);
            else
                leftPinch = leftPinch || IsPinching(second, false, ref practiceLeftPinchHeld);
        }

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
        if (foundRightV && !twoV)
            return PracticeGesture.RightV;
        if (foundLeftV && !twoV)
            return PracticeGesture.LeftV;
        if (LooksLikeFist(first) && (!hasTwo || !LooksLikeFist(second)))
            return PracticeGesture.SingleFist;
        if (hasTwo && LooksLikeFist(second) && !LooksLikeFist(first))
            return PracticeGesture.SingleFist;
        if (CountOpenPalms(first, second, hasTwo) >= 1)
            return PracticeGesture.OpenPalm;
        return PracticeGesture.None;
    }

    private void LateUpdate()
    {
        if (suppressDriving || lanternRenderer == null)
            return;

        if (ownWidthHeight)
        {
            Write(Width, current[Width]);
            Write(Height, current[Height]);
        }

        if (!topperLiveThisFrame)
            current[Topper] = committedTopper;
        if (!bottomLiveThisFrame)
            current[Bottom] = committedBottom;
        Write(Topper, current[Topper]);
        Write(Bottom, current[Bottom]);
        Write(PopTop, current[PopTop]);
        Write(PopBottom, current[PopBottom]);
        WriteUnusedPop(unusedPopTopShape);
        WriteUnusedPop(unusedPopBottomShape);
        Write(PopBamboo, current[PopBamboo]);
        if (shapeIndex[PopTassel] >= 0 && shapeIndex[PopTassel] != shapeIndex[PopBamboo])
            Write(PopTassel, current[PopTassel]);
        WriteUnusedPop(unusedTasselShape);
        UpdateProgressCardFade();
    }

    private bool DrivePop(SculptHandFrame hand, bool physicalRight, float blend)
    {
        ref bool held = ref (physicalRight ? ref rightPinchHeld : ref leftPinchHeld);
        ref float timer = ref (physicalRight ? ref rightPinchTimer : ref leftPinchTimer);
        ref bool armed = ref (physicalRight ? ref rightPopArmed : ref leftPopArmed);
        ref float startY = ref (physicalRight ? ref rightPopStartY : ref leftPopStartY);
        ref float startPop = ref (physicalRight ? ref rightPopStart : ref leftPopStart);
        int pop = physicalRight ? PopTop : PopBottom;
        float pinchY = PinchHeight(hand);

        if (LooksLikeFist(hand) || IsOpenPalm(hand) ||
            !IsPinching(hand, physicalRight, ref held))
        {
            timer = 0f;
            armed = false;
            held = false;
            return false;
        }

        timer += Time.deltaTime;
        if (popConfirmTime > 0f && timer < popConfirmTime)
            return true;

        if (!armed)
        {
            armed = true;
            startY = pinchY;
            startPop = current[pop];
        }

        float lift = Mathf.Max(0f, pinchY - startY);
        float target = Mathf.Clamp(startPop + lift * popPinchSensitivity, 0f, 100f);
        target = Mathf.Max(target, current[pop]);
        float ease = 1f - Mathf.Exp(-popPinchEase * Time.deltaTime);
        current[pop] = Mathf.Lerp(current[pop], target, ease);
        if (target > startPop + 0.5f)
            madeDeformation = true;
        return true;
    }

    private static float PinchHeight(SculptHandFrame hand)
    {
        return (hand.ThumbTip.y + hand.IndexTip.y) * 0.5f;
    }

    private void DriveBambooSpread(SculptHandFrame first, SculptHandFrame second)
    {
        float spread = VSignSpread(first, second);
        if (!bambooArmed)
        {
            bambooArmed = true;
            bambooStartY = spread;
            bambooStart = current[PopBamboo];
            return;
        }

        float outward = Mathf.Max(0f, spread - bambooStartY);
        float target = Mathf.Clamp(bambooStart + outward * popLiftSensitivity, 0f, 100f);
        float ease = 1f - Mathf.Exp(-popEase * Time.deltaTime);
        current[PopBamboo] = Mathf.Lerp(current[PopBamboo], target, ease);
        if (target > bambooStart + 0.5f)
            madeDeformation = true;
    }

    private void DriveTasselDown(SculptHandFrame first, SculptHandFrame second)
    {
        int channel = ActiveTasselChannel();
        float fistY = (first.PalmCenter.y + second.PalmCenter.y) * 0.5f;
        if (!tasselArmed)
        {
            tasselArmed = true;
            tasselStartY = fistY;
            tasselStart = current[channel];
            return;
        }

        float drop = Mathf.Max(0f, tasselStartY - fistY);
        float target = Mathf.Clamp(tasselStart + drop * popLiftSensitivity * 1.35f, 0f, 100f);
        float ease = 1f - Mathf.Exp(-popEase * Time.deltaTime);
        current[channel] = Mathf.Lerp(current[channel], target, ease);
        if (target > tasselStart + 0.5f)
            madeDeformation = true;
    }

    private void DriveVShape(
        SculptHandFrame hand,
        int slot,
        float blend,
        ref bool armed,
        ref float startY,
        ref float startValue,
        ref float committed,
        ref bool live)
    {
        float vY = VSignHeight(hand);
        if (!armed)
        {
            armed = true;
            startY = vY;
            startValue = current[slot];
            return;
        }

        // Raise the V to increase, lower it to decrease.
        float target = Mathf.Clamp(
            startValue + (vY - startY) * popLiftSensitivity, 0f, 100f);
        current[slot] = Mathf.Lerp(current[slot], target, blend);
        committed = current[slot];
        live = true;
        if (Mathf.Abs(target - startValue) > 0.5f)
            madeDeformation = true;
    }

    private bool TryGetHandV(
        SculptHandFrame first,
        SculptHandFrame second,
        bool hasTwo,
        bool wantRight,
        out SculptHandFrame match)
    {
        match = default;
        if (IsPhysicalRightHand(first, second, hasTwo) == wantRight && IsVSign(first))
        {
            match = first;
            return true;
        }

        if (hasTwo && IsPhysicalRightHand(second, first, true) == wantRight && IsVSign(second))
        {
            match = second;
            return true;
        }

        return false;
    }

    private int ActiveTasselChannel()
    {
        if (shapeIndex[PopTassel] >= 0 && shapeIndex[PopTassel] != shapeIndex[PopBamboo])
            return PopTassel;
        return PopBamboo;
    }

    private static float VSignHeight(SculptHandFrame hand)
    {
        return (hand.IndexTip.y + hand.MiddleTip.y) * 0.5f;
    }

    private static float VSignSpread(SculptHandFrame first, SculptHandFrame second)
    {
        float leftX = (first.IndexTip.x + first.MiddleTip.x) * 0.5f;
        float rightX = (second.IndexTip.x + second.MiddleTip.x) * 0.5f;
        return Mathf.Abs(rightX - leftX);
    }

    private void DriveWidthHeight(
        SculptHandFrame first,
        SculptHandFrame second,
        float blend)
    {
        float horizontal = Mathf.Abs(first.IndexTip.x - second.IndexTip.x);
        float vertical = Mathf.Abs(first.IndexTip.y - second.IndexTip.y);

        if (!widthHeightArmed)
        {
            widthHeightBaselineX = horizontal;
            widthHeightBaselineY = vertical;
            widthHeightStartW = ReadRenderer(Width, current[Width]);
            widthHeightStartH = ReadRenderer(Height, current[Height]);
            current[Width] = widthHeightStartW;
            current[Height] = widthHeightStartH;
            widthHeightArmed = true;
            widthHeightAxis = 0;
            return;
        }

        float dx = DeadZone(horizontal - widthHeightBaselineX);
        float dy = DeadZone(vertical - widthHeightBaselineY);
        if (widthHeightAxis == 0)
        {
            float ax = Mathf.Abs(dx);
            float ay = Mathf.Abs(dy);
            if (ax <= 0f && ay <= 0f)
                return;
            if (ax >= ay * 1.2f)
                widthHeightAxis = 1;
            else if (ay >= ax * 1.2f)
                widthHeightAxis = 2;
            else
                return;
        }

        if (widthHeightAxis == 1)
        {
            current[Width] = Mathf.Lerp(
                current[Width],
                Mathf.Clamp(widthHeightStartW + dx * widthSensitivity, 0f, 100f),
                blend);
            current[Height] = widthHeightStartH;
        }
        else
        {
            current[Width] = widthHeightStartW;
            current[Height] = Mathf.Lerp(
                current[Height],
                Mathf.Clamp(widthHeightStartH + dy * heightSensitivity, 0f, 100f),
                blend);
        }
    }

    private void Write(int slot, float weight)
    {
        if (shapeIndex[slot] < 0)
            return;
        lanternRenderer.SetBlendShapeWeight(shapeIndex[slot], weight);
    }

    private void WriteUnusedPop(int shape)
    {
        if (shape < 0)
            return;
        lanternRenderer.SetBlendShapeWeight(shape, 0f);
    }

    private void ResolveShapes()
    {
        for (int i = 0; i < ChannelCount; i++)
            shapeIndex[i] = -1;

        if (lanternRenderer == null || lanternRenderer.sharedMesh == null)
            return;

        Mesh mesh = lanternRenderer.sharedMesh;
        for (int i = 0; i < mesh.blendShapeCount; i++)
        {
            string imported = Normalize(mesh.GetBlendShapeName(i));
            for (int c = 0; c < ChannelCount; c++)
            {
                if (shapeIndex[c] < 0 && imported == Normalize(lookupNames[c]))
                    shapeIndex[c] = i;
            }
        }

        for (int i = 0; i < mesh.blendShapeCount; i++)
        {
            string imported = mesh.GetBlendShapeName(i);
            for (int c = 0; c < ChannelCount; c++)
            {
                if (shapeIndex[c] < 0 && NamesMatch(imported, lookupNames[c]) &&
                    c != Bottom && c != Topper)
                    shapeIndex[c] = i;
            }

            if (shapeIndex[PopTop] < 0 &&
                (TokenMatch(imported, "TOP CIRCLE") || TokenMatch(imported, "TOPCIRCLE")))
                shapeIndex[PopTop] = i;
            if (shapeIndex[PopBottom] < 0 &&
                (TokenMatch(imported, "CIRCLE BOTTOM") || TokenMatch(imported, "BOTTOMCIRCLE")))
                shapeIndex[PopBottom] = i;
        }

        if (shapeIndex[PopBamboo] < 0)
            shapeIndex[PopBamboo] = FindBambooShape(mesh);

        ApplyPopVariantBindings();
        ApplyTasselBinding();

        for (int c = 0; c < ChannelCount; c++)
        {
            if (shapeIndex[c] >= 0)
                current[c] = lanternRenderer.GetBlendShapeWeight(shapeIndex[c]);
        }

        if (shapeIndex[PopBamboo] >= 0)
            Debug.Log("MORPHIC: Two-V bamboo uses " + mesh.GetBlendShapeName(shapeIndex[PopBamboo]));
        else
            Debug.LogWarning("MORPHIC: Pop Up Bamboo was not found on " + mesh.name);
        if (shapeIndex[PopTassel] >= 0)
            Debug.Log("MORPHIC: Left-V tassel uses " + mesh.GetBlendShapeName(shapeIndex[PopTassel]));
    }

    private void ApplyPopVariantBindings()
    {
        if (lanternRenderer == null || lanternRenderer.sharedMesh == null)
            return;

        Mesh mesh = lanternRenderer.sharedMesh;
        BindExclusivePop(mesh, PopTop, true, topPopUsesRectangle, ref unusedPopTopShape, ref popTopLabel);
        BindExclusivePop(mesh, PopBottom, false, bottomPopUsesRectangle, ref unusedPopBottomShape, ref popBottomLabel);
    }

    private void ApplyTasselBinding()
    {
        if (lanternRenderer == null || lanternRenderer.sharedMesh == null)
            return;

        Mesh mesh = lanternRenderer.sharedMesh;
        int active = FindTasselShape(mesh, tasselUsesTriple);
        int unused = FindTasselShape(mesh, !tasselUsesTriple);
        if (active >= 0)
            shapeIndex[PopTassel] = active;
        unusedTasselShape = unused >= 0 && unused != active && unused != shapeIndex[PopBamboo]
            ? unused
            : -1;
        if (unusedTasselShape >= 0)
            lanternRenderer.SetBlendShapeWeight(unusedTasselShape, 0f);
    }

    private static int FindBambooShape(Mesh mesh)
    {
        int fallback = -1;
        for (int i = 0; i < mesh.blendShapeCount; i++)
        {
            string compact = Normalize(mesh.GetBlendShapeName(i));
            if (!compact.Contains("BAMBOO"))
                continue;
            if (compact.Contains("TASSEL") || compact.Contains("TASSLE") ||
                compact.Contains("3") || compact.Contains("THREE"))
            {
                if (fallback < 0)
                    fallback = i;
                continue;
            }

            return i;
        }

        return fallback;
    }

    private static int FindTasselShape(Mesh mesh, bool useTriple)
    {
        int fallback = -1;
        for (int i = 0; i < mesh.blendShapeCount; i++)
        {
            string compact = Normalize(mesh.GetBlendShapeName(i));
            bool isTassel = compact.Contains("TASSEL") || compact.Contains("TASSLE");
            if (!isTassel)
                continue;

            bool isTriple = compact.Contains("3") || compact.Contains("THREE");
            bool isSingle = compact.Contains("SINGLE");
            if (useTriple)
            {
                if (isTriple)
                    return i;
                continue;
            }

            if (isSingle)
                return i;
            if (!isTriple && fallback < 0)
                fallback = i;
        }

        return fallback;
    }

    private void BindExclusivePop(
        Mesh mesh,
        int channel,
        bool isTop,
        bool useRectangle,
        ref int unusedShape,
        ref string label)
    {
        int active = FindPopShape(mesh, isTop, useRectangle);
        int unused = FindPopShape(mesh, isTop, !useRectangle);
        if (active >= 0)
            shapeIndex[channel] = active;
        unusedShape = unused;
        if (unused >= 0)
            lanternRenderer.SetBlendShapeWeight(unused, 0f);
        label = isTop
            ? (useRectangle ? "TOP RECTANGLE" : "TOP CIRCLE")
            : (useRectangle ? "BOTTOM RECTANGLE" : "BOTTOM CIRCLE");
    }

    private static int FindPopShape(Mesh mesh, bool isTop, bool useRectangle)
    {
        for (int i = 0; i < mesh.blendShapeCount; i++)
        {
            string compact = Normalize(mesh.GetBlendShapeName(i));
            bool onEnd = isTop ? compact.Contains("TOP") : compact.Contains("BOTTOM");
            if (!onEnd)
                continue;
            if (useRectangle)
            {
                if (compact.Contains("RECTANGLE"))
                    return i;
                continue;
            }

            if (compact.Contains("CIRCLE") && !compact.Contains("RECTANGLE"))
                return i;
        }

        return -1;
    }

    private static int FindShapeContaining(Mesh mesh, string token)
    {
        string needle = Normalize(token);
        for (int i = 0; i < mesh.blendShapeCount; i++)
        {
            if (Normalize(mesh.GetBlendShapeName(i)).Contains(needle))
                return i;
        }

        return -1;
    }

    private bool IsIndexPointing(SculptHandFrame hand)
    {
        return hand.IndexExtensionAngle >= minimumPointingIndexAngle &&
               hand.IndexTipPalmRatio >= minimumIndexTipExtension &&
               hand.OtherFingersCurlRatio <= maximumOtherFingerExtension &&
               hand.PinchDistance >= hand.PalmWidth * minimumThumbIndexSeparation;
    }

    private bool IsPinching(SculptHandFrame hand, bool physicalRight, ref bool held)
    {
        if (IsOpenPalm(hand) || IsIndexPointing(hand) || LooksLikeFist(hand) || LooksLikeVSign(hand))
        {
            held = false;
            return false;
        }

        float palm = Mathf.Max(hand.PalmWidth, 0.001f);
        float thumbIndex = TipDistance(hand.ThumbTip, hand.IndexTip) / palm;
        float thumbMiddle = TipDistance(hand.ThumbTip, hand.MiddleTip) / palm;
        float start = pinchThreshold;
        float release = Mathf.Max(pinchReleaseThreshold, pinchThreshold + 0.12f);
        if (!physicalRight)
        {
            start *= leftPinchBoost;
            release *= leftPinchBoost;
        }

        if (!held && thumbIndex > thumbMiddle + 0.04f)
        {
            held = false;
            return false;
        }

        if (held)
            held = thumbIndex < release;
        else
            held = thumbIndex < start;
        return held;
    }

    private static bool LooksLikeVSign(SculptHandFrame hand)
    {
        return IsVSign(hand);
    }

    private static bool IsVSign(SculptHandFrame hand)
    {
        return hand.IndexTipPalmRatio >= 1.02f &&
               hand.MiddleTipPalmRatio >= 1.02f &&
               hand.RingTipPalmRatio < 1.00f &&
               hand.PinkyTipPalmRatio < 1.00f &&
               hand.PinchDistance >= hand.PalmWidth * 0.38f;
    }

    private void RestoreCommittedThumbShapes()
    {
        current[Topper] = committedTopper;
        current[Bottom] = committedBottom;
    }

    private static bool HoldsThumbShape(SculptHandFrame hand)
    {
        return LooksLikeFist(hand) ||
               IsFormingFist(hand) ||
               ThumbAgainstFingers(hand) ||
               (FingersClosed(hand, 1.32f, 1.20f) && !HasExtendedThumb(hand));
    }

    private static bool LooksLikeFist(SculptHandFrame hand)
    {
        return FingersClosed(hand, 1.12f, 1.02f) && !HasExtendedThumb(hand);
    }

    private static bool IsFormingFist(SculptHandFrame hand)
    {
        return FingersClosed(hand, 1.28f, 1.16f) && !HasExtendedThumb(hand);
    }

    private static bool FingersClosed(
        SculptHandFrame hand,
        float maximumCurl,
        float maximumIndex)
    {
        return hand.FingerCurlRatio <= maximumCurl &&
               hand.IndexTipPalmRatio <= maximumIndex &&
               hand.OtherFingersCurlRatio <= maximumCurl;
    }

    private static bool ThumbAgainstFingers(SculptHandFrame hand)
    {
        float palm = Mathf.Max(hand.PalmWidth, 0.001f);
        return TipDistance(hand.ThumbTip, hand.IndexTip) / palm < 0.52f ||
               TipDistance(hand.ThumbTip, hand.MiddleTip) / palm < 0.50f;
    }

    public static bool HasExtendedThumb(SculptHandFrame hand)
    {
        float palm = Mathf.Max(hand.PalmWidth, 0.001f);
        Vector2 thumb = new Vector2(
            hand.ThumbTip.x - hand.ThumbMcp.x,
            hand.ThumbTip.y - hand.ThumbMcp.y);
        float thumbToPalm = Vector2.Distance(
            new Vector2(hand.ThumbTip.x, hand.ThumbTip.y),
            new Vector2(hand.PalmCenter.x, hand.PalmCenter.y)) / palm;
        // A wrap-around fist parks the thumb on the fingers. That must not
        // count as a thumb-sculpt pose or Topper gets driven back to 0.
        return thumbToPalm >= 1.32f &&
               thumb.magnitude >= palm * 0.32f &&
               hand.PinchDistance >= palm * 0.52f &&
               TipDistance(hand.ThumbTip, hand.IndexTip) / palm >= 0.55f &&
               TipDistance(hand.ThumbTip, hand.MiddleTip) / palm >= 0.50f &&
               !ThumbAgainstFingers(hand);
    }

    private bool LooksLikeThumbOnly(SculptHandFrame hand)
    {
        return HasExtendedThumb(hand);
    }

    private static float TipDistance(Vector3 a, Vector3 b)
    {
        return Vector2.Distance(new Vector2(a.x, a.y), new Vector2(b.x, b.y));
    }

    private bool IsOpenPalm(SculptHandFrame hand)
    {
        float palm = Mathf.Max(hand.PalmWidth, 0.001f);
        return hand.PinchDistance >= palm * 0.55f &&
               hand.FingerCurlRatio >= openPalmExtension &&
               hand.OtherFingersCurlRatio >= openPalmExtension * 0.92f &&
               hand.IndexTipPalmRatio >= 1.05f &&
               !IsIndexPointing(hand);
    }

    private int CountOpenPalms(SculptHandFrame first, SculptHandFrame second, bool hasTwo)
    {
        int count = IsOpenPalm(first) ? 1 : 0;
        if (hasTwo && IsOpenPalm(second))
            count++;
        return count;
    }

    private bool TryThumbWeight(SculptHandFrame hand, out float weight)
    {
        weight = 0f;
        bool dummy = false;
        if (IsPinching(hand, true, ref dummy) || IsIndexPointing(hand) ||
            IsOpenPalm(hand) || LooksLikeVSign(hand) || LooksLikeFist(hand) ||
            IsFormingFist(hand) || ThumbAgainstFingers(hand) ||
            !HasExtendedThumb(hand))
            return false;

        Vector2 thumb = new Vector2(
            hand.ThumbTip.x - hand.ThumbMcp.x,
            hand.ThumbTip.y - hand.ThumbMcp.y);

        float vertical = Mathf.Clamp(thumb.normalized.y, -1f, 1f);
        weight = Mathf.InverseLerp(0.55f, -0.75f, vertical) * 100f;
        return true;
    }

    private static bool IsThumbPointingUp(SculptHandFrame hand)
    {
        if (!HasExtendedThumb(hand))
            return false;

        Vector2 thumb = new Vector2(
            hand.ThumbTip.x - hand.ThumbMcp.x,
            hand.ThumbTip.y - hand.ThumbMcp.y);
        return thumb.sqrMagnitude > 0.0001f && thumb.normalized.y >= 0.35f;
    }

    private float PinchClosedAmount(SculptHandFrame hand, bool physicalRight)
    {
        float palm = Mathf.Max(hand.PalmWidth, 0.001f);
        float open = palm * pinchThreshold * (physicalRight ? 1f : leftPinchBoost);
        float closed = palm * 0.08f;
        float t = 1f - Mathf.InverseLerp(
            closed, Mathf.Max(open, closed + 0.001f), hand.PinchDistance);
        return Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t));
    }

    private bool IsPhysicalRightHand(
        SculptHandFrame hand,
        SculptHandFrame other,
        bool hasTwo)
    {
        if (hand.HasHandedness)
            return !hand.IsRightHand;
        if (hasTwo)
            return hand.PalmCenter.x >= other.PalmCenter.x;
        return hand.PalmCenter.x >= 0.5f;
    }

    private float ReadRenderer(int slot, float fallback)
    {
        if (shapeIndex[slot] < 0)
            return fallback;
        return lanternRenderer.GetBlendShapeWeight(shapeIndex[slot]);
    }

    private float DeadZone(float value)
    {
        float mag = Mathf.Abs(value);
        if (mag <= movementDeadZone)
            return 0f;
        return Mathf.Sign(value) * (mag - movementDeadZone);
    }

    private void BindLantern()
    {
        if (lanternRenderer != null && lanternRenderer.sharedMesh != null &&
            lanternRenderer.sharedMesh.blendShapeCount > 0)
            return;

        SkinnedMeshRenderer best = null;
        int bestScore = -1;
        SkinnedMeshRenderer[] renderers = FindObjectsByType<SkinnedMeshRenderer>(
            FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (int r = 0; r < renderers.Length; r++)
        {
            SkinnedMeshRenderer renderer = renderers[r];
            Mesh mesh = renderer.sharedMesh;
            if (mesh == null || mesh.blendShapeCount == 0)
                continue;

            int score = 0;
            string objectName = renderer.gameObject.name.ToUpperInvariant();
            if (objectName.Contains("FINALLANTERN") || objectName.Contains("LANTERN"))
                score += 10;

            for (int i = 0; i < mesh.blendShapeCount; i++)
            {
                string imported = Normalize(mesh.GetBlendShapeName(i));
                if (imported.Contains("HEIGHT"))
                    score++;
                if (imported.Contains("WIDTH"))
                    score++;
                if (imported.Contains("TOPPER"))
                    score++;
                if (imported.Contains("CIRCLE") && imported.Contains("POP"))
                    score += 2;
            }

            if (score > bestScore)
            {
                bestScore = score;
                best = renderer;
            }
        }

        if (best != null)
            lanternRenderer = best;
    }

    private static bool NamesMatch(string imported, string configured)
    {
        string a = Normalize(imported);
        string b = Normalize(configured);
        return a.Length > 0 && b.Length > 0 && (a == b || a.Contains(b) || b.Contains(a));
    }

    private static bool TokenMatch(string imported, string tokens)
    {
        string value = Normalize(imported);
        string[] parts = tokens.Split(' ');
        for (int i = 0; i < parts.Length; i++)
        {
            if (parts[i].Length == 0)
                continue;
            if (!value.Contains(Normalize(parts[i])))
                return false;
        }

        return true;
    }

    private static string Normalize(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return string.Empty;

        int dot = value.LastIndexOf('.');
        if (dot >= 0 && dot < value.Length - 1)
            value = value.Substring(dot + 1);

        char[] chars = value.ToCharArray();
        int count = 0;
        for (int i = 0; i < chars.Length; i++)
        {
            if (char.IsLetterOrDigit(chars[i]))
                chars[count++] = char.ToUpperInvariant(chars[i]);
        }

        return new string(chars, 0, count);
    }

    private string BuildStatus(
        bool twoIndex,
        bool rightVTopper,
        bool leftVBottom,
        bool twoFists,
        bool twoV,
        bool hasTwo,
        SculptHandFrame first,
        SculptHandFrame second)
    {
        if (twoV)
            return "TWO V SIDE: BAMBOO";
        if (twoFists)
            return "TWO FISTS DOWN: TASSEL";
        if (rightVTopper)
            return "RIGHT V: TOPPER";
        if (leftVBottom)
            return "LEFT V: BOTTOM";
        if (twoIndex)
            return "WIDTH + HEIGHT";

        string text = "";
        AppendHandStatus(ref text, first, second, hasTwo);
        if (hasTwo)
            AppendHandStatus(ref text, second, first, true);
        return text.Length == 0 ? "GESTURE: NONE" : text;
    }

    private void AppendHandStatus(
        ref string text,
        SculptHandFrame hand,
        SculptHandFrame other,
        bool hasTwo)
    {
        bool right = IsPhysicalRightHand(hand, other, hasTwo);
        ref bool held = ref (right ? ref rightPinchHeld : ref leftPinchHeld);
        if (IsPinching(hand, right, ref held))
            text = Append(text, right
                ? "RIGHT PINCH: " + popTopLabel
                : "LEFT PINCH: " + popBottomLabel);
        else if (IsOpenPalm(hand))
            text = Append(text, "OPEN PALM");
        else if (IsVSign(hand))
            text = Append(text, right ? "RIGHT V: TOPPER" : "LEFT V: BOTTOM");
    }

    private static string Append(string current, string part)
    {
        return current.Length == 0 ? part : current + "  |  " + part;
    }

    private void SetStatus(string message)
    {
        if (gestureIndicator != null)
            gestureIndicator.text = message;
        UpdateProgressCard(message);
    }

    private void CreateProgressCard()
    {
        if (progressCardGroup != null || gestureIndicator == null ||
            gestureIndicator.transform.parent == null)
            return;

        Transform canvasParent = gestureIndicator.transform.parent;
        GameObject card = new GameObject("LanternProgressCard",
            typeof(RectTransform), typeof(CanvasGroup), typeof(Image));
        card.transform.SetParent(canvasParent, false);
        card.transform.SetAsLastSibling();

        RectTransform cardRect = card.GetComponent<RectTransform>();
        cardRect.anchorMin = Vector2.one;
        cardRect.anchorMax = Vector2.one;
        cardRect.pivot = Vector2.one;
        cardRect.anchoredPosition = new Vector2(-24f, -24f);
        cardRect.sizeDelta = new Vector2(310f, 112f);
        card.GetComponent<Image>().color = new Color32(15, 24, 38, 242);

        progressAccentStripe = new GameObject("AccentStripe",
            typeof(RectTransform), typeof(Image)).GetComponent<Image>();
        RectTransform accentRect = progressAccentStripe.rectTransform;
        accentRect.SetParent(card.transform, false);
        accentRect.anchorMin = new Vector2(0f, 0f);
        accentRect.anchorMax = new Vector2(0f, 1f);
        accentRect.sizeDelta = new Vector2(4f, -18f);
        progressAccentStripe.color = new Color32(77, 220, 203, 255);

        progressCardGroup = card.GetComponent<CanvasGroup>();
        progressCardGroup.alpha = 0f;
        progressCardGroup.blocksRaycasts = false;

        progressPartText = CreateProgressLabel(card.transform, "Part",
            new Vector2(18f, -38f), new Vector2(190f, 28f), 19f);
        progressPercentText = CreateProgressLabel(card.transform, "Percent",
            new Vector2(-16f, -31f), new Vector2(92f, 34f), 25f);
        RectTransform percentRect = progressPercentText.rectTransform;
        percentRect.anchorMin = Vector2.one;
        percentRect.anchorMax = Vector2.one;
        percentRect.pivot = Vector2.one;

        GameObject fill = new GameObject("ProgressFill", typeof(RectTransform), typeof(Image));
        fill.transform.SetParent(card.transform, false);
        RectTransform fillRect = fill.GetComponent<RectTransform>();
        fillRect.anchorMin = new Vector2(0f, 0f);
        fillRect.anchorMax = new Vector2(1f, 0f);
        fillRect.pivot = new Vector2(0.5f, 0f);
        fillRect.anchoredPosition = new Vector2(0f, 16f);
        fillRect.sizeDelta = new Vector2(-36f, 8f);
        progressBarFill = fill.GetComponent<Image>();
        progressBarFill.color = new Color32(77, 220, 203, 255);
        progressBarFill.type = Image.Type.Filled;
        progressBarFill.fillMethod = Image.FillMethod.Horizontal;
    }

    private TMP_Text CreateProgressLabel(
        Transform parent,
        string objectName,
        Vector2 anchoredPosition,
        Vector2 size,
        float fontSize)
    {
        GameObject labelObject = new GameObject(objectName,
            typeof(RectTransform), typeof(TextMeshProUGUI));
        labelObject.transform.SetParent(parent, false);
        RectTransform rect = labelObject.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0f, 1f);
        rect.anchorMax = new Vector2(0f, 1f);
        rect.pivot = new Vector2(0f, 1f);
        rect.anchoredPosition = anchoredPosition;
        rect.sizeDelta = size;
        TextMeshProUGUI label = labelObject.GetComponent<TextMeshProUGUI>();
        label.font = gestureIndicator.font;
        label.fontSize = fontSize;
        label.fontStyle = FontStyles.Bold;
        label.color = Color.white;
        label.raycastTarget = false;
        return label;
    }

    private void UpdateProgressCard(string message)
    {
        if (progressCardGroup == null)
            return;
        if (message == "GESTURE: NONE" || message.Contains("NO HANDS"))
        {
            showProgressCard = false;
            return;
        }

        showProgressCard = true;
        float percent = 0f;
        if (message.Contains("WIDTH"))
            percent = (ReadRenderer(Width, current[Width]) + ReadRenderer(Height, current[Height])) * 0.5f;
        else if (message.Contains("TASSEL"))
            percent = current[ActiveTasselChannel()];
        else if (message.Contains("BAMBOO"))
            percent = current[PopBamboo];
        else if (message.Contains("TOP CIRCLE") || message.Contains("TOP RECTANGLE"))
            percent = current[PopTop];
        else if (message.Contains("BOTTOM CIRCLE") || message.Contains("BOTTOM RECTANGLE"))
            percent = current[PopBottom];
        else if (message.Contains("TOPPER"))
            percent = current[Topper];
        else if (message.Contains("BOTTOM"))
            percent = current[Bottom];

        progressPartText.text = message;
        progressPercentText.text = Mathf.RoundToInt(percent) + "%";
        progressBarFill.fillAmount = Mathf.Clamp01(percent / 100f);
    }

    private void UpdateProgressCardFade()
    {
        if (progressCardGroup == null)
            return;
        progressCardGroup.alpha = Mathf.MoveTowards(
            progressCardGroup.alpha, showProgressCard ? 1f : 0f,
            Time.unscaledDeltaTime * 5f);
    }
}
