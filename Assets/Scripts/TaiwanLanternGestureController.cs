using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Mediapipe.Unity.Sample.HandLandmarkDetection;

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
    [SerializeField] private string popUpTopShape = "Pop Up Top";
    [SerializeField] private string popUpBambooShape = "Pop Up Bamboo";
    [SerializeField] private string popUpBottomShape = "Pop Up Bottom";

    [Header("Gesture Sensitivity")]
    [SerializeField] private float widthSensitivity = 150f;
    [SerializeField] private float heightSensitivity = 150f;
    [SerializeField] private float pinchThreshold = 0.25f;
    [SerializeField] private float fistThreshold = 1.08f;
    [SerializeField] private float smoothing = 22f;
    [SerializeField] private float gestureGracePeriod = 0.5f;
    [SerializeField, Range(0f, 0.05f)] private float movementDeadZone = 0.012f;

    [Header("Index Pointing Gesture")]
    [SerializeField, Range(0.5f, 1.5f)] private float maximumOtherFingerExtension = 1.15f;
    [SerializeField, Range(120f, 175f)] private float minimumPointingIndexAngle = 140f;

    [Header("Topper Transition")]
    [SerializeField, Min(0.1f)] private float topperEaseTime = 0.65f;

    [Header("Middle Transition")]
    [SerializeField, Min(0.05f)] private float middleEaseTime = 0.28f;

    [Header("Thumb Rotation Control")]
    [SerializeField, Range(0.05f, 0.5f)] private float minimumThumbLength = 0.12f;

    [Header("Middle: Two-Hand Pinch Distance")]
    [SerializeField, Min(0.1f)] private float middleMinPinchSeparation = 1f;
    [SerializeField, Min(0.2f)] private float middleMaxPinchSeparation = 4f;
    [SerializeField, Range(0.01f, 0.3f)] private float middleHoldStillTolerance = 0.05f;
    [SerializeField, Range(0f, 1f)] private float singleThumbModeGracePeriod = 0.4f;

    [Header("Pop-Up: Single-Hand Pinch Height")]
    [SerializeField, Range(0f, 1f)] private float popUpMinimumHandHeight = 0.10f;
    [SerializeField, Range(0f, 1f)] private float popUpMaximumHandHeight = 0.42f;
    [SerializeField, Min(0.01f)] private float popUpGestureActivationTime = 0.08f;

    [Header("Pop-Up Bamboo: Two-Hand V Spacing")]
    [SerializeField, Min(0.1f)] private float bambooMinimumVSeparation = 1.2f;
    [SerializeField, Min(0.2f)] private float bambooMaximumVSeparation = 4f;

    [Header("Gesture Lock")]
    [SerializeField, Min(0.25f)] private float gestureLockHoldDuration = 1f;
    [SerializeField, Min(0.25f)] private float thumbGestureLockHoldDuration = 2f;
    [SerializeField, Range(0.003f, 0.04f)] private float indexHoldStillTolerance = 0.01f;
    [SerializeField, Range(2f, 20f)] private float thumbHoldStillTolerance = 15f;
    [SerializeField, Range(0f, 12f)] private float thumbOutputDeadZone = 6f;

    private readonly string[] shapeNames = new string[8];

    private int[] shapeIndices = new int[8];
    private float[] shapeMaximumWeights = new float[8];
    private float[] currentWeights = new float[8];
    private float[] gestureStartWeights = new float[8];

    private float baselineHorizontal;
    private float baselineVertical;
    private float gestureLostTimer;
    private float singleThumbModeTimer;
    private float topperTargetWeight;
    private float topperVelocity;
    private float middleTargetWeight;
    private float middleVelocity;
    private float topperMinimumWeight;
    private float topperMaximumWeight = 100f;
    private float bottomTargetWeight;
    private float bottomVelocity;
    private float bottomMinimumWeight;
    private float bottomMaximumWeight = 100f;
    private float gestureHoldTimer;
    private float stabilityAnchorA;
    private float stabilityAnchorB;

    private Vector2 firstBaselineIndex;
    private Vector2 secondBaselineIndex;

    private bool twoHandGestureActive;
    private bool gestureLocked;
    private bool hasStabilityAnchor;

    private string currentGestureName = "NONE";
    private string candidateGestureName = "NONE";
    private float gestureActivationTimer = 0f;
    private const float GESTURE_ACTIVATION_TIME = 0.2f;
    private bool externalFistRotationActive;

    private CanvasGroup progressCardGroup;
    private TMP_Text progressPartText;
    private TMP_Text progressPercentText;
    private Image progressBarFill;
    private Image progressAccentStripe;
    private Color progressAccent = new Color32(77, 220, 203, 255);
    private bool showProgressCard;

    private const int WIDTH = 0;
    private const int HEIGHT = 1;
    private const int TOPPER = 2;
    private const int BOTTOM = 3;
    private const int MIDDLE = 4;
    private const int POP_TOP = 5;
    private const int POP_BAMBOO = 6;
    private const int POP_BOTTOM = 7;

    private void Awake()
    {
        shapeNames[WIDTH] = widthShape;
        shapeNames[HEIGHT] = heightShape;
        shapeNames[TOPPER] = topperShape;
        shapeNames[BOTTOM] = bottomShape;
        shapeNames[MIDDLE] = middleShape;
        shapeNames[POP_TOP] = popUpTopShape;
        shapeNames[POP_BAMBOO] = popUpBambooShape;
        shapeNames[POP_BOTTOM] = popUpBottomShape;

        FindBlendShapes();
        CreateProgressCard();
    }

    private void FindBlendShapes()
    {
        for (int i = 0; i < shapeIndices.Length; i++)
        {
            shapeIndices[i] = -1;
            shapeMaximumWeights[i] = 100f;
        }

        if (lanternRenderer == null ||
            lanternRenderer.sharedMesh == null)
        {
            Debug.LogError("MORPHIC: Lantern renderer or mesh is null!");
            return;
        }

        Mesh mesh = lanternRenderer.sharedMesh;
        Debug.Log($"MORPHIC: Mesh has {mesh.blendShapeCount} blend shapes");

        for (int i = 0; i < mesh.blendShapeCount; i++)
        {
            string importedName = mesh.GetBlendShapeName(i);
            Debug.Log($"MORPHIC: Blend shape {i}: {importedName}");

            for (int j = 0; j < shapeNames.Length; j++)
            {
                // FBX exporters may append whitespace to blend shape names
                // (the bamboo channel in the current model has a trailing
                // space). Normalize names before matching scene settings.
                if (string.Equals(
                    importedName?.Trim(),
                    shapeNames[j]?.Trim(),
                    System.StringComparison.OrdinalIgnoreCase))
                {
                    shapeIndices[j] = i;
                    int frameCount = mesh.GetBlendShapeFrameCount(i);
                    if (frameCount > 0)
                    {
                        float frameMaximum = Mathf.Abs(
                            mesh.GetBlendShapeFrameWeight(i, frameCount - 1));
                        if (frameMaximum > 0.001f)
                            shapeMaximumWeights[j] = frameMaximum;
                    }
                }
            }
        }

        for (int i = 0; i < shapeIndices.Length; i++)
        {
            if (shapeIndices[i] < 0)
                Debug.LogWarning(
                    "Missing BlendShape: " + shapeNames[i], this
                );
            else
            {
                currentWeights[i] =
                    lanternRenderer.GetBlendShapeWeight(shapeIndices[i]);
                if (i == MIDDLE)
                    middleTargetWeight = currentWeights[i];
                if (i == TOPPER)
                {
                    int frameCount = mesh.GetBlendShapeFrameCount(shapeIndices[i]);
                    if (frameCount > 0)
                        topperMaximumWeight = mesh.GetBlendShapeFrameWeight(
                            shapeIndices[i], frameCount - 1);
                    topperTargetWeight = currentWeights[i];
                }
                else if (i == BOTTOM)
                {
                    int frameCount = mesh.GetBlendShapeFrameCount(shapeIndices[i]);
                    if (frameCount > 0)
                        bottomMaximumWeight = mesh.GetBlendShapeFrameWeight(
                            shapeIndices[i], frameCount - 1);
                    bottomTargetWeight = currentWeights[i];
                }
                Debug.Log($"MORPHIC: Found blend shape '{shapeNames[i]}' at index {shapeIndices[i]}");
                if (i == POP_BAMBOO)
                    Debug.Log($"MORPHIC: Bamboo blend shape maximum weight is {shapeMaximumWeights[i]:0.##}");

                // Clear shape keys that do not have an active gesture mapping.
                if (i != WIDTH && i != HEIGHT && i != TOPPER &&
                    i != BOTTOM && i != MIDDLE)
                {
                    currentWeights[i] = 0f;
                    lanternRenderer.SetBlendShapeWeight(shapeIndices[i], 0f);
                }
            }
        }
    }

    private void Update()
    {
        if (handRunner == null || lanternRenderer == null)
        {
            SetGestureText("GESTURE: NONE");
            return;
        }

        // Fist drag rotation is owned by the enhancement controller. While it
        // is active, do not reinterpret the curled fingers as one of the
        // existing thumb sculpting poses.
        if (externalFistRotationActive)
        {
            ApplyWeights();
            SetGestureText("GESTURE: FIST ROTATE");
            return;
        }

        bool hasTwoHands = handRunner.TryGetLatestTwoHands(
            out SculptHandFrame first,
            out SculptHandFrame second);

        if (!hasTwoHands)
        {
            if (handRunner.TryGetLatestHand(out SculptHandFrame singleHand))
            {
                first = singleHand;
                second = default;
            }
            else
            {
                HandleTrackingLoss();
                SetGestureText("GESTURE: NO HANDS");
                return;
            }
        }

        gestureLostTimer = 0f;

        float dt = Time.deltaTime;
        float factor = 1f - Mathf.Exp(-smoothing * dt);
        singleThumbModeTimer = hasTwoHands ? 0f : singleThumbModeTimer + dt;

        // Detect which gesture is active
        string detectedGesture = hasTwoHands
            ? DetectGesture(first, second)
            : singleThumbModeTimer >= singleThumbModeGracePeriod
                ? DetectSingleHandGesture(first)
                : "NONE";

        // Before lock, a gesture owns the controller. A pose change pauses
        // deformation until the user returns to the same pose and holds its
        // control value still for the full lock interval.
        if (!gestureLocked && currentGestureName != "NONE")
        {
            if (detectedGesture != currentGestureName)
            {
                ResetGestureHold();
                ApplyWeights();
                string switchTarget = detectedGesture == "NONE"
                    ? string.Empty
                    : " BEFORE " + detectedGesture;
                SetGestureText(
                    "HOLD STILL 2 SEC TO LOCK " + currentGestureName + switchTarget);
                return;
            }

            candidateGestureName = currentGestureName;
            gestureActivationTimer = 0f;
            ApplyGestureEffects(first, second, hasTwoHands, factor);
            UpdateGestureHold(first, second, hasTwoHands, detectedGesture, dt);
            ApplyWeights();
            SetGestureText(gestureLocked
                ? "LOCKED: " + currentGestureName + " - SWITCH GESTURE"
                : "HOLD STILL 2 SEC TO LOCK " + currentGestureName);
            return;
        }

        // After lock, ignore the old pose and accept a different recognized
        // gesture only after its classification remains stable briefly.
        if (gestureLocked)
        {
            if (detectedGesture != "NONE" && detectedGesture != currentGestureName)
            {
                if (candidateGestureName != detectedGesture)
                {
                    candidateGestureName = detectedGesture;
                    gestureActivationTimer = 0f;
                }
                else
                    gestureActivationTimer += dt;

                if (gestureActivationTimer >= GetGestureActivationTime(detectedGesture))
                {
                    currentGestureName = detectedGesture;
                    gestureLocked = false;
                    gestureActivationTimer = 0f;
                    candidateGestureName = currentGestureName;
                    ResetGestureHold();
                    twoHandGestureActive = false;
                }
            }
            else
            {
                candidateGestureName = currentGestureName;
                gestureActivationTimer = 0f;
            }

            ApplyWeights();
            SetGestureText(gestureLocked
                ? "LOCKED: " + currentGestureName + " - SWITCH GESTURE"
                : "GESTURE: " + currentGestureName);
            return;
        }

        // No gesture is active yet. Select the first valid gesture after
        // consistent detection, then let it own the controller until locked.
        if (detectedGesture == currentGestureName)
        {
            candidateGestureName = currentGestureName;
            gestureActivationTimer = 0f;
        }
        else
        {
            if (candidateGestureName != detectedGesture)
            {
                candidateGestureName = detectedGesture;
                gestureActivationTimer = 0f;
            }
            else
            {
                gestureActivationTimer += dt;
                if (gestureActivationTimer >= GetGestureActivationTime(detectedGesture))
                {
                    currentGestureName = candidateGestureName;
                    gestureActivationTimer = 0f;
                    ResetGestureHold();
                }
            }
        }

        if (currentGestureName != "INDEX FINGERS")
            twoHandGestureActive = false;

        if (currentGestureName != "NONE")
        {
            ApplyGestureEffects(first, second, hasTwoHands, factor);
            UpdateGestureHold(first, second, hasTwoHands, detectedGesture, dt);
        }

        ApplyWeights();

        SetGestureText(gestureLocked
            ? "LOCKED: " + currentGestureName + " - SWITCH GESTURE"
            : currentGestureName == "NONE"
                ? "GESTURE: NONE"
                : "HOLD STILL 2 SEC TO LOCK " + currentGestureName);
    }

    public void SetExternalFistRotationActive(bool active)
    {
        if (externalFistRotationActive == active)
            return;

        externalFistRotationActive = active;
        if (!active)
            return;

        currentGestureName = "NONE";
        candidateGestureName = "NONE";
        gestureActivationTimer = 0f;
        gestureLocked = false;
        twoHandGestureActive = false;
        ResetGestureHold();
    }

    public void SynchronizeWeightsFromRenderer()
    {
        if (lanternRenderer == null)
            return;

        for (int i = 0; i < shapeIndices.Length; i++)
        {
            if (shapeIndices[i] >= 0)
                currentWeights[i] = lanternRenderer.GetBlendShapeWeight(
                    shapeIndices[i]);
        }

        topperTargetWeight = currentWeights[TOPPER];
        bottomTargetWeight = currentWeights[BOTTOM];
        middleTargetWeight = currentWeights[MIDDLE];
        topperVelocity = 0f;
        bottomVelocity = 0f;
        middleVelocity = 0f;
        currentGestureName = "NONE";
        candidateGestureName = "NONE";
        gestureActivationTimer = 0f;
        twoHandGestureActive = false;
        gestureLocked = false;
        ResetGestureHold();
    }

    private void LateUpdate()
    {
        UpdateProgressCardFade();

        if (lanternRenderer == null)
            return;

        // Keep the Topper easing independent from detector frame stalls. A
        // missed or slow inference frame must not turn a toggle into a snap.
        float dt = Mathf.Min(Time.unscaledDeltaTime, 1f / 30f);
        EaseBlendShape(TOPPER, topperTargetWeight, ref topperVelocity, dt);
        EaseBlendShape(BOTTOM, bottomTargetWeight, ref bottomVelocity, dt);
        EaseBlendShape(MIDDLE, middleTargetWeight, ref middleVelocity, dt);
        // Apply gesture-driven shape keys after Animator evaluation so its
        // animation update cannot overwrite Width, Height, or the Pop-Up keys.
        ApplyWeights();
    }

    private string DetectGesture(SculptHandFrame first, SculptHandFrame second)
    {
        if (IsMiddlePinchPose(first) && IsMiddlePinchPose(second))
            return "DOUBLE PINCH";

        // Keep this interaction focused: other recognized poses must not
        // leave extra shape keys latched while Width/Height are being edited.
        if (IsIndexPointing(first) && IsIndexPointing(second))
            return "INDEX FINGERS";

        bool firstPinching = IsPinching(first);
        bool secondPinching = IsPinching(second);
        if (firstPinching != secondPinching)
        {
            SculptHandFrame pinchingHand = firstPinching ? first : second;
            if (pinchingHand.HasHandedness)
                return pinchingHand.IsRightHand
                    ? "POP UP BOTTOM PINCH"
                    : "POP UP TOP PINCH";
        }

        bool firstThumb = IsThumbPose(first) && first.HasHandedness;
        bool secondThumb = IsThumbPose(second) && second.HasHandedness;
        if (firstThumb != secondThumb)
        {
            SculptHandFrame thumbHand = firstThumb ? first : second;
            // Camera input is mirrored in this scene, so MediaPipe's handedness
            // label is opposite the user's physical left/right hand.
            return thumbHand.IsRightHand
                ? "LEFT THUMB ROTATION"
                : "RIGHT THUMB ROTATION";
        }

        if (firstThumb && secondThumb &&
            IsVerticalThumbRotationPose(first) &&
            IsVerticalThumbRotationPose(second))
            return "NONE";

        if (IsVSign(first) && IsVSign(second))
            return "BAMBOO V SPACING";

        return "NONE";
    }

    private string DetectSingleHandGesture(SculptHandFrame hand)
    {
        if (!hand.HasHandedness)
            return "NONE";

        if (IsPinching(hand))
            return hand.IsRightHand
                ? "POP UP BOTTOM PINCH"
                : "POP UP TOP PINCH";

        if (!IsThumbPose(hand))
            return "NONE";

        return hand.IsRightHand
            ? "LEFT THUMB ROTATION"
            : "RIGHT THUMB ROTATION";
    }

    private SculptHandFrame GetPopUpControlHand(
        SculptHandFrame first,
        SculptHandFrame second,
        bool hasTwoHands,
        bool controlPhysicalRightHand)
    {
        // Prefer the hand that is actually pinching. Looking it up again from
        // handedness can select the other hand if MediaPipe changes hand order
        // or its mirrored label during tracking, which delayed the top pop-up.
        bool firstPinching = IsPinching(first);
        bool secondPinching = hasTwoHands && IsPinching(second);
        if (firstPinching != secondPinching)
            return firstPinching ? first : second;

        if (!hasTwoHands && firstPinching)
            return first;

        bool expectedMediaPipeRight = !controlPhysicalRightHand;
        if (first.HasHandedness && first.IsRightHand == expectedMediaPipeRight)
            return first;
        if (hasTwoHands && second.HasHandedness &&
            second.IsRightHand == expectedMediaPipeRight)
            return second;
        return default;
    }

    private void ApplyGestureEffects(
        SculptHandFrame first,
        SculptHandFrame second,
        bool hasTwoHands,
        float factor)
    {
        switch (currentGestureName)
        {
            case "INDEX FINGERS":
                if (hasTwoHands)
                    ApplyWidthHeightGesture(first, second, factor);
                break;
            case "DOUBLE PINCH":
                if (hasTwoHands &&
                    IsMiddlePinchPose(first) && IsMiddlePinchPose(second))
                {
                    float palmWidth = (first.PalmWidth + second.PalmWidth) * 0.5f;
                    Vector2 firstPinch = GetPinchCenter(first);
                    Vector2 secondPinch = GetPinchCenter(second);
                    float pinchSeparation = Vector2.Distance(firstPinch, secondPinch);
                    float separationInPalmWidths = pinchSeparation / Mathf.Max(palmWidth, 0.001f);
                    float normalized = Mathf.InverseLerp(
                        middleMinPinchSeparation,
                        Mathf.Max(middleMaxPinchSeparation, middleMinPinchSeparation + 0.01f),
                        separationInPalmWidths);
                    SetTarget(MIDDLE, normalized * 100f, factor);
                }
                break;
            case "BAMBOO V SPACING":
                if (hasTwoHands && IsVSign(first) && IsVSign(second))
                {
                    float palmWidth = (first.PalmWidth + second.PalmWidth) * 0.5f;
                    float horizontalSeparation = Mathf.Abs(
                        first.PalmCenter.x - second.PalmCenter.x);
                    float separationInPalmWidths = horizontalSeparation /
                        Mathf.Max(palmWidth, 0.001f);
                    float normalizedOpen = Mathf.InverseLerp(
                        bambooMinimumVSeparation,
                        Mathf.Max(bambooMaximumVSeparation,
                            bambooMinimumVSeparation + 0.01f),
                        separationInPalmWidths);
                    SetTarget(
                        POP_BAMBOO,
                        normalizedOpen * shapeMaximumWeights[POP_BAMBOO],
                        factor);
                }
                break;
            case "POP UP TOP PINCH":
            case "POP UP BOTTOM PINCH":
                bool controlPhysicalRightHand = currentGestureName == "POP UP TOP PINCH";
                SculptHandFrame popUpHand = GetPopUpControlHand(
                    first, second, hasTwoHands, controlPhysicalRightHand);
                if (IsPinching(popUpHand))
                {
                    float minimumHeight = Mathf.Min(
                        popUpMinimumHandHeight, popUpMaximumHandHeight);
                    float maximumHeight = Mathf.Max(
                        popUpMinimumHandHeight, popUpMaximumHandHeight);
                    float normalizedHeight = Mathf.InverseLerp(
                        minimumHeight,
                        Mathf.Max(maximumHeight, minimumHeight + 0.01f),
                        GetPinchCenter(popUpHand).y);
                    SetTarget(
                        controlPhysicalRightHand ? POP_TOP : POP_BOTTOM,
                        normalizedHeight * 100f,
                        factor);
                }
                break;
            case "RIGHT THUMB ROTATION":
            case "LEFT THUMB ROTATION":
                bool controlRightThumb = currentGestureName == "RIGHT THUMB ROTATION";
                SculptHandFrame thumbControlHand = GetHandBySide(
                    first, second, hasTwoHands, controlRightThumb);
                int shapeSlot = controlRightThumb ? TOPPER : BOTTOM;
                if (IsThumbPose(thumbControlHand) &&
                    TryGetThumbRotationValue(thumbControlHand, shapeSlot, out float target))
                    SetTarget(shapeSlot, target, factor);
                break;
        }
    }

    private void ApplyWidthHeightGesture(SculptHandFrame first, SculptHandFrame second, float factor)
    {
        Vector2 firstIndex = new Vector2(first.IndexTip.x, first.IndexTip.y);
        Vector2 secondIndex = new Vector2(second.IndexTip.x, second.IndexTip.y);

        if (!twoHandGestureActive)
        {
            firstBaselineIndex = firstIndex;
            secondBaselineIndex = secondIndex;
            baselineHorizontal = Mathf.Abs(firstIndex.x - secondIndex.x);
            baselineVertical = Mathf.Abs(firstIndex.y - secondIndex.y);
            gestureStartWeights[WIDTH] = currentWeights[WIDTH];
            gestureStartWeights[HEIGHT] = currentWeights[HEIGHT];
            twoHandGestureActive = true;
            Debug.Log($"MORPHIC: Index gesture started. Baseline: H={baselineHorizontal:F3}, V={baselineVertical:F3}");
        }

        float currentHorizontal = Mathf.Abs(firstIndex.x - secondIndex.x);
        float currentVertical = Mathf.Abs(firstIndex.y - secondIndex.y);

        float horizontalChange = currentHorizontal - baselineHorizontal;
        float verticalChange = currentVertical - baselineVertical;

        horizontalChange = ApplyMovementDeadZone(horizontalChange);
        verticalChange = ApplyMovementDeadZone(verticalChange);

        // Make it more responsive - scale changes by sensitivity
        float widthTarget = gestureStartWeights[WIDTH] + horizontalChange * widthSensitivity;
        float heightTarget = gestureStartWeights[HEIGHT] + verticalChange * heightSensitivity;

        // Clamp to valid range
        widthTarget = Mathf.Clamp(widthTarget, 0f, 100f);
        heightTarget = Mathf.Clamp(heightTarget, 0f, 100f);

        SetTarget(WIDTH, widthTarget, factor);
        SetTarget(HEIGHT, heightTarget, factor);
    }

    private float ApplyMovementDeadZone(float change)
    {
        float magnitude = Mathf.Abs(change);
        if (magnitude <= movementDeadZone)
        {
            return 0f;
        }

        return Mathf.Sign(change) * (magnitude - movementDeadZone);
    }

    private float GetGestureActivationTime(string gesture)
    {
        if (gesture == "POP UP TOP PINCH" || gesture == "POP UP BOTTOM PINCH")
            return popUpGestureActivationTime;

        return GESTURE_ACTIVATION_TIME;
    }

    private void ApplyPinchGesture(SculptHandFrame hand, int shapeIndex, float factor)
    {
        float target = Mathf.Clamp01(
            1f - hand.PinchDistance / Mathf.Max(hand.PalmWidth * pinchThreshold, 0.001f));
        SetTarget(shapeIndex, target * 100f, factor);
    }

    private void ApplyFistGesture(SculptHandFrame hand, int shapeIndex, float factor)
    {
        float curl = Mathf.Clamp01(
            (fistThreshold - hand.FingerCurlRatio) / Mathf.Max(fistThreshold, 0.001f));
        SetTarget(shapeIndex, curl * 100f, factor);
    }

    private bool IsIndexPointing(SculptHandFrame hand)
    {
        // A straight index also occurs in an open palm. Require the other three
        // fingers to be curled so an open palm cannot enter the sculpt gesture.
        bool indexIsStraight =
            hand.IndexExtensionAngle >= minimumPointingIndexAngle;
        bool otherFingersAreCurled =
            hand.OtherFingersCurlRatio <= maximumOtherFingerExtension;

        return indexIsStraight && otherFingersAreCurled;
    }

    private bool IsPinching(SculptHandFrame hand)
    {
        return hand.PinchDistance <
            Mathf.Max(hand.PalmWidth * pinchThreshold, 0.001f);
    }

    private bool TryGetThumbRotationValue(
        SculptHandFrame hand,
        int shapeSlot,
        out float targetWeight)
    {
        Vector2 thumbAxis = new Vector2(
            hand.ThumbTip.x - hand.ThumbMcp.x,
            hand.ThumbTip.y - hand.ThumbMcp.y);

        if (thumbAxis.magnitude < hand.PalmWidth * minimumThumbLength)
        {
            targetWeight = currentWeights[shapeSlot];
            return false;
        }

        // Image points are converted to Unity screen coordinates (Y up).
        // Thumbs up is the minimum; rotating toward thumbs down increases the value.
        float verticalAmount = Mathf.Clamp(
            thumbAxis.normalized.y, -1f, 1f);
        float normalizedValue = (1f - verticalAmount) * 0.5f;
        float minimumWeight = shapeSlot == TOPPER
            ? topperMinimumWeight
            : bottomMinimumWeight;
        float maximumWeight = shapeSlot == TOPPER
            ? topperMaximumWeight
            : bottomMaximumWeight;
        targetWeight = Mathf.Lerp(minimumWeight, maximumWeight, normalizedValue);
        return true;
    }

    private void UpdateGestureHold(
        SculptHandFrame first,
        SculptHandFrame second,
        bool hasTwoHands,
        string detectedGesture,
        float dt)
    {
        if (detectedGesture != currentGestureName)
        {
            ResetGestureHold();
            return;
        }

        float measurementA;
        float measurementB = 0f;
        float tolerance;

        if (currentGestureName == "INDEX FINGERS" && hasTwoHands)
        {
            measurementA = Mathf.Abs(first.IndexTip.x - second.IndexTip.x);
            measurementB = Mathf.Abs(first.IndexTip.y - second.IndexTip.y);
            tolerance = indexHoldStillTolerance;
        }
        else if (currentGestureName == "DOUBLE PINCH" && hasTwoHands)
        {
            float palmWidth = (first.PalmWidth + second.PalmWidth) * 0.5f;
            measurementA = Vector2.Distance(
                GetPinchCenter(first),
                GetPinchCenter(second)) /
                Mathf.Max(palmWidth, 0.001f);
            tolerance = middleHoldStillTolerance;
        }
        else if (currentGestureName == "BAMBOO V SPACING" && hasTwoHands &&
                 IsVSign(first) && IsVSign(second))
        {
            float palmWidth = (first.PalmWidth + second.PalmWidth) * 0.5f;
            measurementA = Mathf.Abs(
                first.PalmCenter.x - second.PalmCenter.x) /
                Mathf.Max(palmWidth, 0.001f);
            tolerance = middleHoldStillTolerance;
        }
        else if (currentGestureName == "POP UP TOP PINCH" ||
                 currentGestureName == "POP UP BOTTOM PINCH")
        {
            bool controlPhysicalRightHand = currentGestureName == "POP UP TOP PINCH";
            SculptHandFrame popUpHand = GetPopUpControlHand(
                first, second, hasTwoHands, controlPhysicalRightHand);
            if (!IsPinching(popUpHand))
            {
                ResetGestureHold();
                return;
            }

            measurementA = GetPinchCenter(popUpHand).y;
            tolerance = middleHoldStillTolerance;
        }
        else if (currentGestureName == "RIGHT THUMB ROTATION" ||
                 currentGestureName == "LEFT THUMB ROTATION")
        {
            bool controlRightThumb = currentGestureName == "RIGHT THUMB ROTATION";
            SculptHandFrame thumbHand = GetHandBySide(
                first, second, hasTwoHands, controlRightThumb);
            int shapeSlot = controlRightThumb ? TOPPER : BOTTOM;
            if (!IsThumbPose(thumbHand) ||
                !TryGetThumbRotationValue(thumbHand, shapeSlot, out measurementA))
            {
                ResetGestureHold();
                return;
            }

            tolerance = thumbHoldStillTolerance;
        }
        else
        {
            ResetGestureHold();
            return;
        }

        if (!hasStabilityAnchor)
        {
            stabilityAnchorA = measurementA;
            stabilityAnchorB = measurementB;
            hasStabilityAnchor = true;
            gestureHoldTimer = 0f;
            return;
        }

        if (Mathf.Abs(measurementA - stabilityAnchorA) > tolerance ||
            Mathf.Abs(measurementB - stabilityAnchorB) > tolerance)
        {
            stabilityAnchorA = measurementA;
            stabilityAnchorB = measurementB;
            gestureHoldTimer = 0f;
            return;
        }

        gestureHoldTimer += dt;
        float requiredHoldDuration = currentGestureName == "INDEX FINGERS"
            ? gestureLockHoldDuration
            : thumbGestureLockHoldDuration;
        if (gestureHoldTimer >= requiredHoldDuration)
        {
            gestureLocked = true;
            gestureHoldTimer = 0f;
            hasStabilityAnchor = false;
            twoHandGestureActive = false;
            if (currentGestureName == "RIGHT THUMB ROTATION")
            {
                topperTargetWeight = currentWeights[TOPPER];
                topperVelocity = 0f;
            }
            else if (currentGestureName == "LEFT THUMB ROTATION")
            {
                bottomTargetWeight = currentWeights[BOTTOM];
                bottomVelocity = 0f;
            }
            Debug.Log("MORPHIC: Gesture locked: " + currentGestureName);
        }
    }

    private void ResetGestureHold()
    {
        gestureHoldTimer = 0f;
        hasStabilityAnchor = false;
    }

    private static SculptHandFrame GetHandBySide(
        SculptHandFrame first,
        SculptHandFrame second,
        bool hasTwoHands,
        bool rightHand)
    {
        bool expectedMediaPipeRight = !rightHand;
        if (first.HasHandedness && first.IsRightHand == expectedMediaPipeRight)
            return first;
        if (hasTwoHands && second.HasHandedness &&
            second.IsRightHand == expectedMediaPipeRight)
            return second;
        return default;
    }

    private void EaseBlendShape(int slot, float target, ref float velocity, float dt)
    {
        if (shapeIndices[slot] < 0)
            return;

        float easeTime = slot == MIDDLE ? middleEaseTime : topperEaseTime;
        currentWeights[slot] = Mathf.SmoothDamp(
            currentWeights[slot], target, ref velocity, easeTime,
            Mathf.Infinity, dt);

        if (Mathf.Abs(currentWeights[slot] - target) < 0.1f)
        {
            currentWeights[slot] = target;
            velocity = 0f;
        }

        lanternRenderer.SetBlendShapeWeight(shapeIndices[slot], currentWeights[slot]);
    }

    private bool IsThumbPose(SculptHandFrame hand)
    {
        // Require the four fingers to be curled around the palm. This rejects
        // open palms and keeps the thumb command distinct from index sculpting.
        return hand.OtherFingersCurlRatio <= maximumOtherFingerExtension &&
               hand.IndexExtensionAngle < minimumPointingIndexAngle;
    }

    private bool IsVerticalThumbRotationPose(SculptHandFrame hand)
    {
        Vector2 thumbAxis = new Vector2(
            hand.ThumbTip.x - hand.ThumbMcp.x,
            hand.ThumbTip.y - hand.ThumbMcp.y);
        return thumbAxis.magnitude >= hand.PalmWidth * minimumThumbLength &&
               Mathf.Abs(thumbAxis.normalized.y) >= 0.65f;
    }

    private bool IsMiddlePinchPose(SculptHandFrame hand)
    {
        // Require an actual thumb-index pinch and the other fingers extended,
        // separating this two-hand control from the curled-finger thumb poses.
        return IsPinching(hand) && hand.OtherFingersCurlRatio >= 1.1f;
    }

    private static Vector2 GetPinchCenter(SculptHandFrame hand)
    {
        return new Vector2(
            (hand.ThumbTip.x + hand.IndexTip.x) * 0.5f,
            (hand.ThumbTip.y + hand.IndexTip.y) * 0.5f);
    }

    private static bool IsVSign(SculptHandFrame hand)
    {
        // V pose: index and middle extended and separated, with ring and pinky
        // folded. This avoids confusing an open palm or single-pointing pose.
        float indexMiddleTipGap = Vector2.Distance(
            new Vector2(hand.IndexTip.x, hand.IndexTip.y),
            new Vector2(hand.MiddleTip.x, hand.MiddleTip.y)) /
            Mathf.Max(hand.PalmWidth, 0.001f);
        return hand.IndexExtensionAngle >= 140f &&
               hand.IndexTipPalmRatio >= 1.05f &&
               hand.MiddleTipPalmRatio >= 1.05f &&
               hand.RingTipPalmRatio <= 1.4f &&
               hand.PinkyTipPalmRatio <= 1.4f &&
               indexMiddleTipGap >= 0.16f;
    }

    private void SetTarget(int slot, float target, float factor)
    {
        if (shapeIndices[slot] < 0)
            return;

        target = Mathf.Clamp(target, 0f, 100f);

        if (slot == TOPPER)
        {
            if (Mathf.Abs(target - topperTargetWeight) < thumbOutputDeadZone)
                return;
            topperTargetWeight = target;
            return;
        }
        if (slot == BOTTOM)
        {
            if (Mathf.Abs(target - bottomTargetWeight) < thumbOutputDeadZone)
                return;
            bottomTargetWeight = target;
            return;
        }
        if (slot == MIDDLE)
        {
            middleTargetWeight = target;
            return;
        }

        currentWeights[slot] = Mathf.Lerp(
            currentWeights[slot],
            target,
            factor);
    }

    private void ApplyWeights()
    {
        // Width/Height and the directly mapped shape keys are written here.
        // Topper and Bottom are eased in LateUpdate, so leave those two to
        // their easing path. Middle was previously updated in currentWeights
        // but never sent to the renderer, which made DOUBLE PINCH appear to
        // detect correctly while the lantern did not move.
        for (int i = 0; i < shapeIndices.Length; i++)
        {
            if (i != TOPPER && i != BOTTOM && i != MIDDLE && shapeIndices[i] >= 0)
            {
                lanternRenderer.SetBlendShapeWeight(
                    shapeIndices[i],
                    currentWeights[i]);
            }
        }
    }

    private void HandleTrackingLoss()
    {
        gestureLostTimer += Time.deltaTime;

        if (gestureLostTimer > gestureGracePeriod)
        {
            twoHandGestureActive = false;
            currentGestureName = "NONE";
            candidateGestureName = "NONE";
            gestureActivationTimer = 0f;
            gestureLocked = false;
            ResetGestureHold();
            SetGestureText("GESTURE: NO HANDS");
        }
    }

    private void SetGestureText(string message)
    {
        if (gestureIndicator != null)
            gestureIndicator.text = message;

        UpdateProgressCard(message);
    }

    private bool TryGetProgressInfo(string message, out string label, out float percentage)
    {
        label = null;
        percentage = 0f;
        if (currentGestureName == "NONE" || message.Contains("NO HANDS"))
            return false;

        switch (currentGestureName)
        {
            case "INDEX FINGERS":
                label = "WIDTH + HEIGHT";
                percentage = (GetProgressPercent(WIDTH) + GetProgressPercent(HEIGHT)) * 0.5f;
                return true;
            case "DOUBLE PINCH":
                label = "MIDDLE";
                percentage = GetProgressPercent(MIDDLE);
                return true;
            case "BAMBOO V SPACING":
                label = "BAMBOO";
                percentage = GetProgressPercent(POP_BAMBOO);
                return true;
            case "POP UP TOP PINCH":
                label = "TOP POP-UP";
                percentage = GetProgressPercent(POP_TOP);
                return true;
            case "POP UP BOTTOM PINCH":
                label = "BOTTOM POP-UP";
                percentage = GetProgressPercent(POP_BOTTOM);
                return true;
            case "RIGHT THUMB ROTATION":
                label = "TOPPER";
                percentage = GetProgressPercent(TOPPER);
                return true;
            case "LEFT THUMB ROTATION":
                label = "BOTTOM";
                percentage = GetProgressPercent(BOTTOM);
                return true;
            default:
                return false;
        }
    }

    private float GetProgressPercent(int slot)
    {
        float maximum = Mathf.Max(shapeMaximumWeights[slot], 0.001f);
        return Mathf.Clamp01(currentWeights[slot] / maximum) * 100f;
    }

    private void CreateProgressCard()
    {
        if (progressCardGroup != null || gestureIndicator == null ||
            gestureIndicator.transform.parent == null)
            return;

        Transform canvasParent = gestureIndicator.transform.parent;
        GameObject card = new GameObject("LanternProgressCard",
            typeof(RectTransform), typeof(CanvasGroup), typeof(Image), typeof(Outline));
        card.transform.SetParent(canvasParent, false);
        card.transform.SetAsLastSibling();

        RectTransform cardRect = card.GetComponent<RectTransform>();
        cardRect.anchorMin = Vector2.one;
        cardRect.anchorMax = Vector2.one;
        cardRect.pivot = Vector2.one;
        cardRect.anchoredPosition = new Vector2(-24f, -24f);
        cardRect.sizeDelta = new Vector2(310f, 112f);

        Image background = card.GetComponent<Image>();
        background.color = new Color32(15, 24, 38, 242);
        Sprite panelSprite = Resources.GetBuiltinResource<Sprite>("UI/Skin/Background.psd");
        if (panelSprite != null)
        {
            background.sprite = panelSprite;
            background.type = Image.Type.Sliced;
        }

        Outline outline = card.GetComponent<Outline>();
        outline.effectColor = new Color32(82, 188, 189, 105);
        outline.effectDistance = new Vector2(1f, -1f);

        RectTransform accentRect = new GameObject("AccentStripe",
            typeof(RectTransform), typeof(Image)).GetComponent<RectTransform>();
        accentRect.SetParent(card.transform, false);
        accentRect.anchorMin = new Vector2(0f, 0f);
        accentRect.anchorMax = new Vector2(0f, 1f);
        accentRect.pivot = new Vector2(0f, 0.5f);
        accentRect.anchoredPosition = Vector2.zero;
        accentRect.sizeDelta = new Vector2(4f, -18f);
        progressAccentStripe = accentRect.GetComponent<Image>();
        progressAccentStripe.color = progressAccent;

        progressCardGroup = card.GetComponent<CanvasGroup>();
        progressCardGroup.alpha = 0f;
        progressCardGroup.interactable = false;
        progressCardGroup.blocksRaycasts = false;

        progressPartText = CreateProgressLabel(card.transform, "Part",
            new Vector2(18f, -38f), new Vector2(190f, 28f), 19f,
            Color.white, TextAlignmentOptions.Left, FontStyles.Bold);
        TMP_Text header = CreateProgressLabel(card.transform, "Header",
            new Vector2(18f, -14f), new Vector2(190f, 17f), 11f,
            new Color32(155, 187, 202, 255), TextAlignmentOptions.Left, FontStyles.Bold);
        header.text = "LIVE SCULPTING";
        progressPercentText = CreateProgressLabel(card.transform, "Percent",
            new Vector2(-16f, -31f), new Vector2(92f, 34f), 25f,
            progressAccent, TextAlignmentOptions.Right, FontStyles.Bold);
        RectTransform percentRect = progressPercentText.rectTransform;
        percentRect.anchorMin = Vector2.one;
        percentRect.anchorMax = Vector2.one;
        percentRect.pivot = Vector2.one;

        RectTransform barBackgroundRect = new GameObject("ProgressTrack",
            typeof(RectTransform), typeof(Image)).GetComponent<RectTransform>();
        barBackgroundRect.SetParent(card.transform, false);
        barBackgroundRect.anchorMin = new Vector2(0f, 0f);
        barBackgroundRect.anchorMax = new Vector2(1f, 0f);
        barBackgroundRect.pivot = new Vector2(0.5f, 0f);
        barBackgroundRect.anchoredPosition = new Vector2(0f, 16f);
        barBackgroundRect.sizeDelta = new Vector2(-36f, 8f);
        barBackgroundRect.GetComponent<Image>().color = new Color32(48, 63, 79, 255);

        RectTransform fillRect = new GameObject("ProgressFill",
            typeof(RectTransform), typeof(Image)).GetComponent<RectTransform>();
        fillRect.SetParent(barBackgroundRect, false);
        fillRect.anchorMin = Vector2.zero;
        fillRect.anchorMax = Vector2.one;
        fillRect.offsetMin = Vector2.zero;
        fillRect.offsetMax = Vector2.zero;
        progressBarFill = fillRect.GetComponent<Image>();
        progressBarFill.sprite = panelSprite;
        progressBarFill.type = Image.Type.Filled;
        progressBarFill.fillMethod = Image.FillMethod.Horizontal;
        progressBarFill.fillOrigin = (int)Image.OriginHorizontal.Left;
        progressBarFill.color = progressAccent;
        progressBarFill.fillAmount = 0f;
    }

    private TMP_Text CreateProgressLabel(
        Transform parent,
        string objectName,
        Vector2 anchoredPosition,
        Vector2 size,
        float fontSize,
        Color color,
        TextAlignmentOptions alignment,
        FontStyles style)
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
        label.fontStyle = style;
        label.color = color;
        label.alignment = alignment;
        label.text = string.Empty;
        label.raycastTarget = false;
        label.textWrappingMode = TextWrappingModes.NoWrap;
        return label;
    }

    private void UpdateProgressCard(string message)
    {
        if (progressCardGroup == null || !TryGetProgressInfo(message, out string label, out float percentage))
        {
            showProgressCard = false;
            return;
        }

        showProgressCard = true;
        progressAccent = GetProgressAccentColor();
        progressPartText.text = label;
        progressPercentText.text = Mathf.RoundToInt(percentage) + "%";
        progressPercentText.color = progressAccent;
        progressAccentStripe.color = progressAccent;
        progressBarFill.color = progressAccent;
        progressBarFill.fillAmount = percentage / 100f;
    }

    private Color GetProgressAccentColor()
    {
        if (currentGestureName == "BAMBOO V SPACING")
            return new Color32(244, 185, 91, 255);
        if (currentGestureName == "POP UP TOP PINCH" ||
            currentGestureName == "POP UP BOTTOM PINCH")
            return new Color32(77, 220, 203, 255);
        return new Color32(139, 164, 255, 255);
    }

    private void UpdateProgressCardFade()
    {
        if (progressCardGroup == null)
            return;

        float targetAlpha = showProgressCard ? 1f : 0f;
        progressCardGroup.alpha = Mathf.MoveTowards(
            progressCardGroup.alpha, targetAlpha, Time.unscaledDeltaTime * 5f);
    }
}
