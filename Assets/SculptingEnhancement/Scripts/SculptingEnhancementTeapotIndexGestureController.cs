using System;
using Mediapipe.Unity.Sample.HandLandmarkDetection;
using UnityEngine;

[DefaultExecutionOrder(260)]
public sealed class SculptingEnhancementTeapotIndexGestureController : MonoBehaviour
{
    [SerializeField] private SculptHandLandmarkerRunner handRunner;
    [SerializeField] private SkinnedMeshRenderer teapotRenderer;
    [SerializeField, Min(1f)] private float widthSensitivity = 360f;
    [SerializeField, Min(1f)] private float heightSensitivity = 550f;
    [SerializeField, Range(0f, 0.03f)] private float movementDeadZone = 0.002f;
    [SerializeField, Min(1f)] private float smoothing = 24f;
    [SerializeField, Range(1f, 2.5f)] private float exclusiveAxisRatio = 1.2f;
    [SerializeField, Range(90f, 175f)] private float minimumIndexAngle = 125f;
    [SerializeField, Range(0.5f, 2f)] private float maximumOtherFingerExtension = 1.45f;
    [SerializeField, Range(90f, 175f)] private float activeMinimumIndexAngle = 110f;
    [SerializeField, Range(0.5f, 2.2f)] private float activeMaximumOtherFingerExtension = 1.7f;
    [SerializeField, Range(0.02f, 0.5f)] private float activationTime = 0.08f;
    [SerializeField, Range(0.2f, 0.8f)] private float minimumThumbIndexSeparation = 0.42f;
    [SerializeField, Range(0.7f, 1.4f)] private float maximumMiddleFingerExtension = 1.02f;
    [SerializeField, Range(0.8f, 1.8f)] private float minimumIndexTipExtension = 1.08f;
    [SerializeField, Range(1.1f, 2f)] private float openPalmExtension = 1.32f;

    private int widthIndex = -1;
    private int heightIndex = -1;
    private float baselineHorizontal;
    private float baselineVertical;
    private float startingWidth;
    private float startingHeight;
    private float currentWidth;
    private float currentHeight;
    private float targetWidth;
    private float targetHeight;
    private float activationTimer;
    private bool gestureActive;
    private bool gesturePaused;
    private int lockedWidthHeightAxis;
    public bool suppressDriving;

    public void BindFrom(SculptHandLandmarkerRunner runner, SkinnedMeshRenderer renderer)
    {
        if (runner != null)
            handRunner = runner;
        if (renderer != null)
            teapotRenderer = renderer;
        FindBlendShapes();
        ReadCurrentWeights();
    }

    private void Awake()
    {
        if (GetComponentInParent<LanternShelfController>(true) != null)
        {
            enabled = false;
            return;
        }

        if (teapotRenderer == null)
        {
            TeapotGestureController gestures = GetComponent<TeapotGestureController>();
            if (gestures != null)
                teapotRenderer = gestures.TeapotRenderer;
        }

        FindBlendShapes();
        ReadCurrentWeights();
    }

    private void OnEnable()
    {
        FindBlendShapes();
        gestureActive = false;
        gesturePaused = false;
        activationTimer = 0f;
        ReadCurrentWeights();
    }

    private void Update()
    {
        if (suppressDriving || handRunner == null || teapotRenderer == null)
            return;
        if (widthIndex < 0 || heightIndex < 0)
            return;

        bool hasTwo = handRunner.TryGetLatestTwoHands(
            out SculptHandFrame first, out SculptHandFrame second);
        if (hasTwo && IsOpenPalm(first))
        {
            PauseGesture();
            EndGesture();
            return;
        }

        if (!hasTwo)
        {
            activationTimer = 0f;
            if (gestureActive)
                PauseGesture();
            return;
        }

        if (!gestureActive)
        {
            if (!IsIndexPointing(first, false) || !IsIndexPointing(second, false))
            {
                activationTimer = 0f;
                return;
            }

            activationTimer += Time.deltaTime;
            if (activationTimer < activationTime)
                return;
            BeginGesture(first, second);
        }

        if (!(IsIndexPointing(first, true) && IsIndexPointing(second, true)))
        {
            PauseGesture();
            return;
        }

        if (gesturePaused)
            RebaseGesture(first, second);

        UpdateTargets(first, second);
        float factor = 1f - Mathf.Exp(-smoothing * Time.deltaTime);
        currentWidth = Mathf.Lerp(currentWidth, targetWidth, factor);
        currentHeight = Mathf.Lerp(currentHeight, targetHeight, factor);
    }

    private void LateUpdate()
    {
        if (suppressDriving || teapotRenderer == null)
            return;
        if (widthIndex >= 0)
            teapotRenderer.SetBlendShapeWeight(widthIndex, currentWidth);
        if (heightIndex >= 0)
            teapotRenderer.SetBlendShapeWeight(heightIndex, currentHeight);
    }

    public void SynchronizeWeightsFromRenderer()
    {
        ReadCurrentWeights();
        EndGesture();
    }

    private void FindBlendShapes()
    {
        widthIndex = -1;
        heightIndex = -1;
        if (teapotRenderer == null || teapotRenderer.sharedMesh == null)
            return;

        Mesh mesh = teapotRenderer.sharedMesh;
        for (int i = 0; i < mesh.blendShapeCount; i++)
        {
            string name = mesh.GetBlendShapeName(i);
            if (string.Equals(name, "Width", StringComparison.OrdinalIgnoreCase))
                widthIndex = i;
            else if (string.Equals(name, "Height", StringComparison.OrdinalIgnoreCase))
                heightIndex = i;
        }
    }

    private void ReadCurrentWeights()
    {
        if (teapotRenderer == null)
            return;
        if (widthIndex >= 0)
            currentWidth = targetWidth = teapotRenderer.GetBlendShapeWeight(widthIndex);
        if (heightIndex >= 0)
            currentHeight = targetHeight = teapotRenderer.GetBlendShapeWeight(heightIndex);
    }

    private void BeginGesture(SculptHandFrame first, SculptHandFrame second)
    {
        RebaseGesture(first, second);
        gestureActive = true;
        activationTimer = 0f;
    }

    private void RebaseGesture(SculptHandFrame first, SculptHandFrame second)
    {
        startingWidth = currentWidth;
        startingHeight = currentHeight;
        targetWidth = currentWidth;
        targetHeight = currentHeight;
        baselineHorizontal = Mathf.Abs(first.IndexTip.x - second.IndexTip.x);
        baselineVertical = Mathf.Abs(first.IndexTip.y - second.IndexTip.y);
        lockedWidthHeightAxis = 0;
        gesturePaused = false;
    }

    private void UpdateTargets(SculptHandFrame first, SculptHandFrame second)
    {
        float horizontal = Mathf.Abs(first.IndexTip.x - second.IndexTip.x);
        float vertical = Mathf.Abs(first.IndexTip.y - second.IndexTip.y);
        float horizontalChange = ApplyDeadZone(horizontal - baselineHorizontal);
        float verticalChange = ApplyDeadZone(vertical - baselineVertical);
        if (!TryLockAxis(horizontalChange, verticalChange))
        {
            targetWidth = startingWidth;
            targetHeight = startingHeight;
            return;
        }

        if (lockedWidthHeightAxis == 1)
        {
            targetWidth = Mathf.Clamp(startingWidth + horizontalChange * widthSensitivity, 0f, 100f);
            targetHeight = startingHeight;
        }
        else
        {
            targetWidth = startingWidth;
            targetHeight = Mathf.Clamp(startingHeight + verticalChange * heightSensitivity, 0f, 100f);
        }
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
               hand.MiddleTipPalmRatio <= maximumMiddleFingerExtension &&
               hand.PinchDistance >= hand.PalmWidth * minimumThumbIndexSeparation &&
               !IsOpenPalm(hand);
    }

    private bool IsOpenPalm(SculptHandFrame hand)
    {
        return hand.PinchDistance >= hand.PalmWidth * 0.55f &&
               hand.FingerCurlRatio >= openPalmExtension &&
               hand.OtherFingersCurlRatio >= openPalmExtension * 0.92f &&
               hand.IndexTipPalmRatio >= 1.05f &&
               hand.MiddleTipPalmRatio > maximumMiddleFingerExtension;
    }

    private bool TryLockAxis(float horizontalChange, float verticalChange)
    {
        if (lockedWidthHeightAxis != 0)
            return true;
        float horizontal = Mathf.Abs(horizontalChange);
        float vertical = Mathf.Abs(verticalChange);
        if (horizontal <= 0f && vertical <= 0f)
            return false;
        if (horizontal >= vertical * exclusiveAxisRatio)
        {
            lockedWidthHeightAxis = 1;
            return true;
        }

        if (vertical >= horizontal * exclusiveAxisRatio)
        {
            lockedWidthHeightAxis = 2;
            return true;
        }

        return false;
    }

    private float ApplyDeadZone(float value)
    {
        float magnitude = Mathf.Abs(value);
        return magnitude <= movementDeadZone ? 0f : Mathf.Sign(value) * (magnitude - movementDeadZone);
    }

    private void EndGesture()
    {
        gestureActive = false;
        gesturePaused = false;
        activationTimer = 0f;
        targetWidth = currentWidth;
        targetHeight = currentHeight;
        lockedWidthHeightAxis = 0;
    }

    private void PauseGesture()
    {
        if (gesturePaused)
            return;
        targetWidth = currentWidth;
        targetHeight = currentHeight;
        gesturePaused = true;
    }
}
