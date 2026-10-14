using Mediapipe.Unity.Sample.HandLandmarkDetection;
using UnityEngine;

// This component intentionally lives outside the student's original Scripts
// folder. It owns only the two-index-finger Width/Height interaction and runs
// after the original lantern controller so the enhancement stays isolated.
[DefaultExecutionOrder(100)]
public sealed class SculptingEnhancementIndexGestureController : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private SculptHandLandmarkerRunner handRunner;
    [SerializeField] private SkinnedMeshRenderer lanternRenderer;

    [Header("BlendShape Names")]
    [SerializeField] private string widthShape = "Width";
    [SerializeField] private string heightShape = "Height";

    [Header("Responsive Width / Height Control")]
    [SerializeField, Min(1f)] private float widthSensitivity = 360f;
    [SerializeField, Min(1f)] private float heightSensitivity = 550f;
    [SerializeField, Range(0f, 0.03f)] private float movementDeadZone = 0.002f;
    [SerializeField, Min(1f)] private float smoothing = 24f;

    [Header("Easier Two-Index Recognition")]
    [SerializeField, Range(90f, 175f)] private float minimumIndexAngle = 125f;
    [SerializeField, Range(0.5f, 2f)] private float maximumOtherFingerExtension = 1.45f;
    [SerializeField, Range(90f, 175f)] private float activeMinimumIndexAngle = 110f;
    [SerializeField, Range(0.5f, 2.2f)] private float activeMaximumOtherFingerExtension = 1.7f;
    [SerializeField, Range(0.02f, 0.5f)] private float activationTime = 0.08f;
    [SerializeField, Range(0f, 0.5f)] private float trackingGracePeriod = 0.22f;

    [Header("Exclusive Gesture Filtering")]
    [Tooltip("Reject thumb-index pinches so Pop-Up gestures cannot change Width/Height.")]
    [SerializeField, Range(0.2f, 0.8f)] private float minimumThumbIndexSeparation = 0.42f;
    [Tooltip("A raised middle finger indicates a V sign or open palm, not index pointing.")]
    [SerializeField, Range(0.7f, 1.4f)] private float maximumMiddleFingerExtension = 1.02f;
    [Tooltip("The index fingertip must remain clearly outside the palm.")]
    [SerializeField, Range(0.8f, 1.8f)] private float minimumIndexTipExtension = 1.08f;

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
    private float trackingLostTimer;
    private bool gestureActive;
    private bool gesturePaused;

    private void Awake()
    {
        FindBlendShapes();
        ReadCurrentWeights();
    }

    private void OnEnable()
    {
        gestureActive = false;
        gesturePaused = false;
        activationTimer = 0f;
        trackingLostTimer = 0f;
        ReadCurrentWeights();
    }

    private void Update()
    {
        if (handRunner == null || lanternRenderer == null ||
            widthIndex < 0 || heightIndex < 0)
            return;

        if (!handRunner.TryGetLatestTwoHands(
                out SculptHandFrame first,
                out SculptHandFrame second))
        {
            activationTimer = 0f;
            if (gestureActive)
            {
                // Freeze immediately when either hand leaves the camera. Do not
                // keep smoothing toward an old target while tracking is absent.
                PauseGesture();
                trackingLostTimer += Time.deltaTime;
                if (trackingLostTimer > trackingGracePeriod)
                    EndGesture();
            }
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

        bool stillPointing =
            IsIndexPointing(first, true) && IsIndexPointing(second, true);

        if (stillPointing)
        {
            // If tracking or the pose flickered briefly, establish a fresh
            // baseline at the frozen shape. Returning fingers therefore
            // continue from the saved Width/Height instead of causing a jump.
            if (gesturePaused)
                RebaseGesture(first, second);

            trackingLostTimer = 0f;
        }
        else
        {
            // A gesture change must stop deformation on this exact frame.
            PauseGesture();
            trackingLostTimer += Time.deltaTime;
            if (trackingLostTimer > trackingGracePeriod)
                EndGesture();

            return;
        }

        UpdateTargets(first, second);
        SmoothWeights(Time.deltaTime);
    }

    private void LateUpdate()
    {
        // The student's original controller also writes BlendShapes in
        // LateUpdate. This component executes later and owns only Width/Height.
        ApplyWeights();
    }

    private void FindBlendShapes()
    {
        widthIndex = -1;
        heightIndex = -1;

        if (lanternRenderer == null || lanternRenderer.sharedMesh == null)
            return;

        Mesh mesh = lanternRenderer.sharedMesh;
        for (int i = 0; i < mesh.blendShapeCount; i++)
        {
            string importedName = mesh.GetBlendShapeName(i)?.Trim();
            if (string.Equals(importedName, widthShape.Trim(),
                    System.StringComparison.OrdinalIgnoreCase))
                widthIndex = i;
            else if (string.Equals(importedName, heightShape.Trim(),
                         System.StringComparison.OrdinalIgnoreCase))
                heightIndex = i;
        }

        if (widthIndex < 0 || heightIndex < 0)
            Debug.LogWarning(
                "MORPHIC Enhancement: Width or Height BlendShape was not found.",
                this);
    }

    private void ReadCurrentWeights()
    {
        if (lanternRenderer == null)
            return;

        if (widthIndex >= 0)
            currentWidth = targetWidth = lanternRenderer.GetBlendShapeWeight(widthIndex);
        if (heightIndex >= 0)
            currentHeight = targetHeight = lanternRenderer.GetBlendShapeWeight(heightIndex);
    }

    private void BeginGesture(SculptHandFrame first, SculptHandFrame second)
    {
        RebaseGesture(first, second);
        gestureActive = true;
        activationTimer = 0f;
        trackingLostTimer = 0f;
    }

    private void RebaseGesture(SculptHandFrame first, SculptHandFrame second)
    {
        Vector2 firstTip = ToPoint(first.IndexTip);
        Vector2 secondTip = ToPoint(second.IndexTip);

        // Use our committed values rather than reading the renderer here. The
        // original student controller also writes Width/Height, so reading it
        // during re-entry could restore that controller's older cached values.
        startingWidth = currentWidth;
        startingHeight = currentHeight;
        targetWidth = currentWidth;
        targetHeight = currentHeight;
        baselineHorizontal = Mathf.Abs(firstTip.x - secondTip.x);
        baselineVertical = Mathf.Abs(firstTip.y - secondTip.y);
        gesturePaused = false;
        trackingLostTimer = 0f;
    }

    private void UpdateTargets(SculptHandFrame first, SculptHandFrame second)
    {
        Vector2 firstTip = ToPoint(first.IndexTip);
        Vector2 secondTip = ToPoint(second.IndexTip);
        float horizontal = Mathf.Abs(firstTip.x - secondTip.x);
        float vertical = Mathf.Abs(firstTip.y - secondTip.y);

        float horizontalChange = ApplyDeadZone(horizontal - baselineHorizontal);
        float verticalChange = ApplyDeadZone(vertical - baselineVertical);

        targetWidth = Mathf.Clamp(
            startingWidth + horizontalChange * widthSensitivity, 0f, 100f);
        targetHeight = Mathf.Clamp(
            startingHeight + verticalChange * heightSensitivity, 0f, 100f);
    }

    private void SmoothWeights(float deltaTime)
    {
        float factor = 1f - Mathf.Exp(-smoothing * deltaTime);
        currentWidth = Mathf.Lerp(currentWidth, targetWidth, factor);
        currentHeight = Mathf.Lerp(currentHeight, targetHeight, factor);
    }

    private void ApplyWeights()
    {
        if (lanternRenderer == null)
            return;

        if (widthIndex >= 0)
            lanternRenderer.SetBlendShapeWeight(widthIndex, currentWidth);
        if (heightIndex >= 0)
            lanternRenderer.SetBlendShapeWeight(heightIndex, currentHeight);
    }

    private bool IsIndexPointing(SculptHandFrame hand, bool useActiveTolerance)
    {
        float requiredAngle = useActiveTolerance
            ? activeMinimumIndexAngle
            : minimumIndexAngle;
        float allowedOtherFingerExtension = useActiveTolerance
            ? activeMaximumOtherFingerExtension
            : maximumOtherFingerExtension;

        bool indexIsClearlyExtended =
            hand.IndexExtensionAngle >= requiredAngle &&
            hand.IndexTipPalmRatio >= minimumIndexTipExtension;
        bool otherFingersMatchPointing =
            hand.OtherFingersCurlRatio <= allowedOtherFingerExtension &&
            hand.MiddleTipPalmRatio <= maximumMiddleFingerExtension;
        bool isNotPinching =
            hand.PinchDistance >=
            hand.PalmWidth * minimumThumbIndexSeparation;

        // All conditions are deliberately required. This keeps double-index
        // control mutually exclusive from pinch, V-sign, open-palm and thumb
        // rotation gestures, so those gestures cannot alter Width or Height.
        return indexIsClearlyExtended &&
               otherFingersMatchPointing &&
               isNotPinching;
    }

    private float ApplyDeadZone(float value)
    {
        float magnitude = Mathf.Abs(value);
        if (magnitude <= movementDeadZone)
            return 0f;

        return Mathf.Sign(value) * (magnitude - movementDeadZone);
    }

    private void EndGesture()
    {
        gestureActive = false;
        gesturePaused = false;
        activationTimer = 0f;
        trackingLostTimer = 0f;
        targetWidth = currentWidth;
        targetHeight = currentHeight;
    }

    private void PauseGesture()
    {
        if (gesturePaused)
            return;

        // Commit exactly what is currently visible and discard any remaining
        // smoothing distance, preventing post-gesture drift.
        targetWidth = currentWidth;
        targetHeight = currentHeight;
        gesturePaused = true;
    }

    private static Vector2 ToPoint(Vector3 point)
    {
        return new Vector2(point.x, point.y);
    }
}
