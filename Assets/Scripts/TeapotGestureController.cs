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
    [SerializeField, Min(1f)] private float smoothing = 24f;
    [SerializeField, Range(0f, 0.03f)] private float movementDeadZone = 0.002f;
    [SerializeField, Range(1f, 2.5f)] private float exclusiveAxisRatio = 1.2f;
    [SerializeField, Range(90f, 175f)] private float minimumIndexAngle = 125f;
    [SerializeField, Range(0.8f, 1.8f)] private float minimumIndexTipExtension = 1.08f;
    [SerializeField, Range(0.5f, 2f)] private float maximumOtherFingerExtension = 1.45f;
    [SerializeField, Range(0.2f, 0.8f)] private float minimumThumbIndexSeparation = 0.42f;

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
    private int lockedWidthHeightAxis;
    private GestureMode currentGesture;

    private enum GestureMode
    {
        None,
        WidthHeight,
        Middle
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

        if (!hasTwoHands)
        {
            EndGesture("TEAPOT: NO HANDS");
            return;
        }

        if (IsIndexPointing(first) && IsIndexPointing(second))
        {
            if (currentGesture != GestureMode.WidthHeight)
                BeginWidthHeight(first, second);

            DriveWidthHeight(first, second);
            SetStatus("TEAPOT: WIDTH / HEIGHT");
        }
        else if (IsVSign(first) && IsVSign(second))
        {
            if (currentGesture != GestureMode.Middle)
                BeginMiddle(first, second);

            DriveMiddle(first, second);
            SetStatus("TEAPOT: MIDDLE");
        }
        else
        {
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

        teapotRenderer.SetBlendShapeWeight(index, Mathf.Clamp(weight, 0f, 100f));
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
            100f);
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
