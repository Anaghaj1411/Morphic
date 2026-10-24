using System.Collections.Generic;
using Mediapipe.Unity.Sample.HandLandmarkDetection;
using TMPro;
using UnityEngine;

/// <summary>
/// Teapot-specific hand-control component. It binds to the Teapot renderer and
/// resolves its imported blend shapes by their exact FBX names.
/// </summary>
[DefaultExecutionOrder(250)]
public sealed class TeapotGestureController : MonoBehaviour
{
    private static readonly string[] RequiredBlendShapeNames =
    {
        "Width",
        "Height",
        "Middle",
        "Lid 1",
        "Lid 2",
        "Shaft 1",
        "Shaft 2",
        "Handle 1",
        "Handle 2"
    };

    [Header("References")]
    [SerializeField] private SculptHandLandmarkerRunner handRunner;
    [SerializeField] private TMP_Text gestureStatus;
    [SerializeField] private SkinnedMeshRenderer teapotRenderer;

    [Header("Body Gesture Controls")]
    [SerializeField, Min(1f)] private float widthSensitivity = 360f;
    [SerializeField, Min(1f)] private float heightSensitivity = 550f;
    [SerializeField, Min(1f)] private float middleSensitivity = 360f;
    [SerializeField, Range(0f, 100f)] private float middleMaximumWeight = 35.4f;
    [SerializeField, Min(1f)] private float smoothing = 24f;
    [SerializeField, Range(0f, 0.03f)] private float movementDeadZone = 0.002f;
    [SerializeField, Range(1f, 2.5f)] private float exclusiveAxisRatio = 1.2f;
    [SerializeField, Range(90f, 175f)] private float minimumIndexAngle = 125f;
    [SerializeField, Range(0.8f, 1.8f)] private float minimumIndexTipExtension = 1.08f;
    [SerializeField, Range(0.5f, 2f)] private float maximumOtherFingerExtension = 1.45f;
    [SerializeField, Range(0.2f, 0.8f)] private float minimumThumbIndexSeparation = 0.42f;

    [Header("Pinch Up Attachments")]
    [SerializeField, Range(0.1f, 1.2f)] private float pinchThreshold = 0.72f;
    [SerializeField, Range(0.2f, 1.5f)] private float pinchReleaseThreshold = 0.95f;
    [SerializeField, Min(1f)] private float leftPinchBoost = 1.35f;
    [SerializeField, Min(1f)] private float pinchLiftSensitivity = 920f;
    [SerializeField, Min(1f)] private float pinchEase = 28f;

    private readonly Dictionary<string, int> blendShapeIndices =
        new Dictionary<string, int>();

    private float width;
    private float height;
    private float middle;
    private float targetWidth;
    private float targetHeight;
    private float targetMiddle;
    private float widthHeightBaselineX;
    private float widthHeightBaselineY;
    private float widthHeightStart;
    private float middleBaseline;
    private float middleStart;
    private bool rightPinchHeld;
    private bool leftPinchHeld;
    private bool rightPinchArmed;
    private bool leftPinchArmed;
    private float rightPinchStartY;
    private float leftPinchStartY;
    private float rightPinchStartWeight;
    private float leftPinchStartWeight;
    private int lockedWidthHeightAxis;
    private GestureMode currentGesture;

    private enum GestureMode
    {
        None,
        WidthHeight,
        Middle,
        PinchUp
    }

    public SculptHandLandmarkerRunner HandRunner => handRunner;
    public TMP_Text GestureStatus => gestureStatus;
    public SkinnedMeshRenderer TeapotRenderer => teapotRenderer;

    /// <summary>
    /// Uses the scene's configured hand-tracking runner without adding a
    /// second camera or changing the Lantern's own mesh/controller setup.
    /// </summary>
    public void SetHandRunner(SculptHandLandmarkerRunner runner)
    {
        handRunner = runner;
    }

    private void Awake()
    {
        if (teapotRenderer == null)
            teapotRenderer = GetComponentInChildren<SkinnedMeshRenderer>(true);

        if (handRunner == null)
            Debug.LogWarning(
                "TeapotGestureController: assign the same configured SculptHandLandmarkerRunner used by the Lantern.",
                this);

        ResolveBlendShapes();
        ReadBodyWeights();
        middle = targetMiddle = Mathf.Clamp(middle, 0f, middleMaximumWeight);
        SetBlendShapeWeight("Middle", middle);
    }

    private void OnEnable()
    {
        if (gestureStatus != null)
            gestureStatus.gameObject.SetActive(true);
    }

    private void OnDisable()
    {
        if (gestureStatus != null)
            gestureStatus.gameObject.SetActive(false);

        ResetPinchTracking();
        currentGesture = GestureMode.None;
        lockedWidthHeightAxis = 0;
    }

    private void Update()
    {
        if (handRunner == null || teapotRenderer == null)
            return;

        bool hasTwoHands = handRunner.TryGetLatestTwoHands(
            out SculptHandFrame first,
            out SculptHandFrame second);

        bool hasAnyHand = hasTwoHands || handRunner.TryGetLatestHand(out first);
        if (!hasAnyHand)
        {
            ResetPinchTracking();
            EndGesture("TEAPOT: NO HANDS");
            return;
        }

        if (hasTwoHands && IsIndexPointing(first) && IsIndexPointing(second))
        {
            ResetPinchTracking();
            if (currentGesture != GestureMode.WidthHeight)
                BeginWidthHeight(first, second);

            DriveWidthHeight(first, second);
            SetStatus("TEAPOT: WIDTH / HEIGHT");
        }
        else if (hasTwoHands && IsVSign(first) && IsVSign(second))
        {
            ResetPinchTracking();
            if (currentGesture != GestureMode.Middle)
                BeginMiddle(first, second);

            DriveMiddle(first, second);
            SetStatus("TEAPOT: MIDDLE");
        }
        else
        {
            currentGesture = GestureMode.PinchUp;
            bool lidPinching = false;
            bool shaftPinching = false;

            bool firstIsRight = IsPhysicalRightHand(first, second, hasTwoHands);
            if (firstIsRight)
                lidPinching |= DrivePinchUp(first, true);
            else
                shaftPinching |= DrivePinchUp(first, false);

            if (hasTwoHands)
            {
                bool secondIsRight = IsPhysicalRightHand(second, first, true);
                if (secondIsRight)
                    lidPinching |= DrivePinchUp(second, true);
                else
                    shaftPinching |= DrivePinchUp(second, false);
            }

            if (lidPinching && shaftPinching)
                SetStatus("TEAPOT: LID 1 + SHAFT 1");
            else if (lidPinching)
                SetStatus("TEAPOT: LID 1");
            else if (shaftPinching)
                SetStatus("TEAPOT: SHAFT 1");
            else
                EndGesture("TEAPOT: READY");
        }

        float blend = 1f - Mathf.Exp(-smoothing * Time.deltaTime);
        width = Mathf.Lerp(width, targetWidth, blend);
        height = Mathf.Lerp(height, targetHeight, blend);
        middle = Mathf.Lerp(middle, targetMiddle, blend);
    }

    private void LateUpdate()
    {
        if (currentGesture == GestureMode.None)
            return;

        SetBlendShapeWeight("Width", width);
        SetBlendShapeWeight("Height", height);
        SetBlendShapeWeight("Middle", middle);
    }

    /// <summary>Returns the imported FBX blend-shape index for an exact name.</summary>
    public bool TryGetBlendShapeIndex(string exactName, out int index)
    {
        return blendShapeIndices.TryGetValue(exactName, out index);
    }

    /// <summary>Sets one resolved Teapot blend shape using Unity's 0–100 weight.</summary>
    public bool SetBlendShapeWeight(string exactName, float weight)
    {
        if (teapotRenderer == null ||
            !blendShapeIndices.TryGetValue(exactName, out int index))
        {
            return false;
        }

        float maximum = exactName == "Middle" ? middleMaximumWeight : 100f;
        teapotRenderer.SetBlendShapeWeight(index, Mathf.Clamp(weight, 0f, maximum));
        return true;
    }

    private void ResolveBlendShapes()
    {
        blendShapeIndices.Clear();

        if (teapotRenderer == null)
        {
            Debug.LogError(
                "TeapotGestureController: no SkinnedMeshRenderer was assigned or found under Teapot_Updated.",
                this);
            SetStatus("TEAPOT: RENDERER MISSING");
            return;
        }

        Mesh mesh = teapotRenderer.sharedMesh;
        if (mesh == null)
        {
            Debug.LogError(
                "TeapotGestureController: the assigned SkinnedMeshRenderer has no mesh.",
                this);
            SetStatus("TEAPOT: MESH MISSING");
            return;
        }

        int missingCount = 0;
        foreach (string shapeName in RequiredBlendShapeNames)
        {
            int index = mesh.GetBlendShapeIndex(shapeName);
            if (index < 0)
            {
                missingCount++;
                Debug.LogWarning(
                    $"TeapotGestureController: blend shape '{shapeName}' was not found on mesh '{mesh.name}'. " +
                    "Check the FBX shape-key spelling and make sure Import BlendShapes is enabled.",
                    this);
                continue;
            }

            blendShapeIndices.Add(shapeName, index);
        }

        SetStatus(missingCount == 0
            ? "TEAPOT: READY"
            : $"TEAPOT: {missingCount} BLEND SHAPE(S) MISSING");

        Debug.Log(
            $"TeapotGestureController: resolved {blendShapeIndices.Count}/{RequiredBlendShapeNames.Length} " +
            $"Teapot blend shapes on '{mesh.name}'.",
            this);
    }

    private void ReadBodyWeights()
    {
        if (teapotRenderer == null)
            return;

        if (blendShapeIndices.TryGetValue("Width", out int widthIndex))
            width = targetWidth = teapotRenderer.GetBlendShapeWeight(widthIndex);
        if (blendShapeIndices.TryGetValue("Height", out int heightIndex))
            height = targetHeight = teapotRenderer.GetBlendShapeWeight(heightIndex);
        if (blendShapeIndices.TryGetValue("Middle", out int middleIndex))
            middle = targetMiddle = teapotRenderer.GetBlendShapeWeight(middleIndex);
    }

    private void BeginWidthHeight(SculptHandFrame first, SculptHandFrame second)
    {
        currentGesture = GestureMode.WidthHeight;
        widthHeightBaselineX = Mathf.Abs(first.IndexTip.x - second.IndexTip.x);
        widthHeightBaselineY = Mathf.Abs(first.IndexTip.y - second.IndexTip.y);
        widthHeightStart = 0f;
        lockedWidthHeightAxis = 0;
        targetWidth = width;
        targetHeight = height;
    }

    private void DriveWidthHeight(SculptHandFrame first, SculptHandFrame second)
    {
        float horizontalChange = ApplyDeadZone(
            Mathf.Abs(first.IndexTip.x - second.IndexTip.x) - widthHeightBaselineX);
        float verticalChange = ApplyDeadZone(
            Mathf.Abs(first.IndexTip.y - second.IndexTip.y) - widthHeightBaselineY);

        if (lockedWidthHeightAxis == 0)
        {
            float horizontal = Mathf.Abs(horizontalChange);
            float vertical = Mathf.Abs(verticalChange);
            if (horizontal > 0f && horizontal >= vertical * exclusiveAxisRatio)
                lockedWidthHeightAxis = 1;
            else if (vertical > 0f && vertical >= horizontal * exclusiveAxisRatio)
                lockedWidthHeightAxis = 2;

            if (lockedWidthHeightAxis == 0)
                return;

            widthHeightStart = lockedWidthHeightAxis == 1 ? width : height;
        }

        if (lockedWidthHeightAxis == 1)
        {
            targetWidth = Mathf.Clamp(
                widthHeightStart + horizontalChange * widthSensitivity, 0f, 100f);
            targetHeight = height;
        }
        else
        {
            targetWidth = width;
            targetHeight = Mathf.Clamp(
                widthHeightStart + verticalChange * heightSensitivity, 0f, 100f);
        }
    }

    private void BeginMiddle(SculptHandFrame first, SculptHandFrame second)
    {
        currentGesture = GestureMode.Middle;
        middleBaseline = VSignSpread(first, second);
        middleStart = middle;
        targetMiddle = middle;
    }

    private void DriveMiddle(SculptHandFrame first, SculptHandFrame second)
    {
        float spreadChange = VSignSpread(first, second) - middleBaseline;
        targetMiddle = Mathf.Clamp(
            middleStart + ApplyDeadZone(spreadChange) * middleSensitivity,
            0f,
            middleMaximumWeight);
    }

    private bool DrivePinchUp(SculptHandFrame hand, bool physicalRight)
    {
        ref bool held = ref (physicalRight ? ref rightPinchHeld : ref leftPinchHeld);
        ref bool armed = ref (physicalRight ? ref rightPinchArmed : ref leftPinchArmed);
        ref float startY = ref (physicalRight ? ref rightPinchStartY : ref leftPinchStartY);
        ref float startWeight = ref (physicalRight ? ref rightPinchStartWeight : ref leftPinchStartWeight);
        string shapeName = physicalRight ? "Lid 1" : "Shaft 1";

        if (!IsPinching(hand, physicalRight, ref held) ||
            !blendShapeIndices.TryGetValue(shapeName, out int index))
        {
            armed = false;
            return false;
        }

        if (!armed)
        {
            armed = true;
            startY = PinchHeight(hand);
            startWeight = teapotRenderer.GetBlendShapeWeight(index);
        }

        float upwardLift = Mathf.Max(0f, PinchHeight(hand) - startY);
        float target = Mathf.Clamp(startWeight + upwardLift * pinchLiftSensitivity, 0f, 100f);
        float currentWeight = teapotRenderer.GetBlendShapeWeight(index);
        target = Mathf.Max(target, currentWeight);
        float blend = 1f - Mathf.Exp(-pinchEase * Time.deltaTime);
        SetBlendShapeWeight(shapeName, Mathf.Lerp(currentWeight, target, blend));
        return true;
    }

    private bool IsPinching(SculptHandFrame hand, bool physicalRight, ref bool held)
    {
        if (IsOpenPalm(hand) || IsIndexPointing(hand) || LooksLikeFist(hand) || IsVSign(hand))
        {
            held = false;
            return false;
        }

        float palm = Mathf.Max(hand.PalmWidth, 0.001f);
        float thumbIndex = hand.PinchDistance / palm;
        float thumbMiddle = Vector2.Distance(
            new Vector2(hand.ThumbTip.x, hand.ThumbTip.y),
            new Vector2(hand.MiddleTip.x, hand.MiddleTip.y)) / palm;
        float start = pinchThreshold;
        float release = Mathf.Max(pinchReleaseThreshold, pinchThreshold + 0.12f);
        if (!physicalRight)
        {
            start *= leftPinchBoost;
            release *= leftPinchBoost;
        }

        if (!held && thumbIndex > thumbMiddle + 0.04f)
            return false;

        held = held ? thumbIndex < release : thumbIndex < start;
        return held;
    }

    private bool IsOpenPalm(SculptHandFrame hand)
    {
        float palm = Mathf.Max(hand.PalmWidth, 0.001f);
        return hand.PinchDistance >= palm * 0.55f &&
               hand.FingerCurlRatio >= 1.45f &&
               hand.OtherFingersCurlRatio >= 1.33f &&
               hand.IndexTipPalmRatio >= 1.05f &&
               !IsIndexPointing(hand);
    }

    private static bool LooksLikeFist(SculptHandFrame hand)
    {
        return FingersClosed(hand, 1.12f, 1.02f) && !HasExtendedThumb(hand);
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

    private static bool HasExtendedThumb(SculptHandFrame hand)
    {
        float palm = Mathf.Max(hand.PalmWidth, 0.001f);
        Vector2 thumb = new Vector2(
            hand.ThumbTip.x - hand.ThumbMcp.x,
            hand.ThumbTip.y - hand.ThumbMcp.y);
        float thumbToPalm = Vector2.Distance(
            new Vector2(hand.ThumbTip.x, hand.ThumbTip.y),
            new Vector2(hand.PalmCenter.x, hand.PalmCenter.y)) / palm;
        return thumbToPalm >= 1.32f &&
               thumb.magnitude >= palm * 0.32f &&
               hand.PinchDistance >= palm * 0.52f &&
               TipDistance(hand.ThumbTip, hand.IndexTip) / palm >= 0.55f &&
               TipDistance(hand.ThumbTip, hand.MiddleTip) / palm >= 0.50f &&
               !ThumbAgainstFingers(hand);
    }

    private static bool ThumbAgainstFingers(SculptHandFrame hand)
    {
        float palm = Mathf.Max(hand.PalmWidth, 0.001f);
        return TipDistance(hand.ThumbTip, hand.IndexTip) / palm < 0.52f ||
               TipDistance(hand.ThumbTip, hand.MiddleTip) / palm < 0.50f;
    }

    private static float TipDistance(Vector3 first, Vector3 second)
    {
        return Vector2.Distance(
            new Vector2(first.x, first.y),
            new Vector2(second.x, second.y));
    }

    private bool IsPhysicalRightHand(
        SculptHandFrame hand,
        SculptHandFrame other,
        bool hasTwoHands)
    {
        if (hand.HasHandedness)
            return !hand.IsRightHand;

        if (hasTwoHands)
            return hand.PalmCenter.x >= other.PalmCenter.x;

        return hand.PalmCenter.x >= 0.5f;
    }

    private static float PinchHeight(SculptHandFrame hand)
    {
        return (hand.ThumbTip.y + hand.IndexTip.y) * 0.5f;
    }

    private void ResetPinchTracking()
    {
        rightPinchHeld = false;
        leftPinchHeld = false;
        rightPinchArmed = false;
        leftPinchArmed = false;
    }

    private void EndGesture(string status)
    {
        currentGesture = GestureMode.None;
        lockedWidthHeightAxis = 0;
        targetWidth = width;
        targetHeight = height;
        targetMiddle = middle;
        SetStatus(status);
    }

    private float ApplyDeadZone(float value)
    {
        float magnitude = Mathf.Abs(value);
        if (magnitude <= movementDeadZone)
            return 0f;

        return Mathf.Sign(value) * (magnitude - movementDeadZone);
    }

    private bool IsIndexPointing(SculptHandFrame hand)
    {
        return hand.IndexExtensionAngle >= minimumIndexAngle &&
               hand.IndexTipPalmRatio >= minimumIndexTipExtension &&
               hand.OtherFingersCurlRatio <= maximumOtherFingerExtension &&
               hand.MiddleTipPalmRatio <= 1.02f &&
               hand.PinchDistance >= hand.PalmWidth * minimumThumbIndexSeparation;
    }

    private static bool IsVSign(SculptHandFrame hand)
    {
        return hand.IndexTipPalmRatio >= 1.02f &&
               hand.MiddleTipPalmRatio >= 1.02f &&
               hand.RingTipPalmRatio < 1.00f &&
               hand.PinkyTipPalmRatio < 1.00f &&
               hand.PinchDistance >= hand.PalmWidth * 0.38f;
    }

    private static float VSignSpread(SculptHandFrame first, SculptHandFrame second)
    {
        float firstX = (first.IndexTip.x + first.MiddleTip.x) * 0.5f;
        float secondX = (second.IndexTip.x + second.MiddleTip.x) * 0.5f;
        return Mathf.Abs(secondX - firstX);
    }

    private void SetStatus(string message)
    {
        if (gestureStatus != null)
            gestureStatus.text = message;
    }
}
