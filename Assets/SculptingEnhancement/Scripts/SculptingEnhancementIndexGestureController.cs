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

    [Header("Single-Fist Lantern-Vertical Rotation")]
    [SerializeField] private Transform lanternRotationTarget;
    [SerializeField] private TaiwanLanternGestureController originalGestureController;
    [Tooltip("Degrees of lantern rotation produced by horizontal fist movement. Lower values make rotation less sensitive.")]
    [SerializeField, Range(100f, 5000f)] private float fistRotationSensitivity = 1800f;
    [SerializeField, Range(0f, 0.05f)] private float fistHorizontalDeadZone = 0.001f;
    [SerializeField, Min(1f)] private float fistRotationSmoothing = 36f;
    [SerializeField, Range(0.02f, 0.5f)] private float fistActivationTime = 0.12f;
    [SerializeField, Range(0.7f, 1.4f)] private float fistMaximumExtension = 1.08f;
    [SerializeField, Range(0.7f, 1.5f)] private float activeFistMaximumExtension = 1.16f;

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
    private bool fistRotationActive;
    private float fistActivationTimer;
    private float fistBaselineX;
    private Transform lanternRotationPivot;
    private Transform lanternOriginalParent;
    private Quaternion fistStartingPivotRotation;
    private Vector3 fistLanternVerticalAxis;
    private float fistAppliedAngle;
    private float fistTargetAngle;

    private void Awake()
    {
        if (lanternRotationTarget == null && lanternRenderer != null)
            lanternRotationTarget = lanternRenderer.transform.root;
        if (originalGestureController == null)
        {
            originalGestureController =
                FindFirstObjectByType<TaiwanLanternGestureController>(
                    FindObjectsInactive.Include);
        }

        FindBlendShapes();
        ReadCurrentWeights();
    }

    private void OnEnable()
    {
        gestureActive = false;
        gesturePaused = false;
        activationTimer = 0f;
        trackingLostTimer = 0f;
        fistRotationActive = false;
        fistActivationTimer = 0f;
        if (originalGestureController != null)
            originalGestureController.SetExternalFistRotationActive(false);
        ReadCurrentWeights();
    }

    private void OnDisable()
    {
        EndFistRotation();
    }

    private void OnDestroy()
    {
        if (lanternRotationPivot == null)
            return;

        if (lanternRotationTarget != null &&
            lanternRotationTarget.parent == lanternRotationPivot)
        {
            lanternRotationTarget.SetParent(lanternOriginalParent, true);
        }

        Destroy(lanternRotationPivot.gameObject);
    }

    private void Update()
    {
        if (handRunner == null || lanternRenderer == null)
            return;

        bool hasTwoHands = handRunner.TryGetLatestTwoHands(
            out SculptHandFrame first,
            out SculptHandFrame second);

        if (!hasTwoHands)
        {
            if (handRunner.TryGetLatestHand(out SculptHandFrame singleHand))
            {
                if (HandleFistRotation(singleHand))
                {
                    EndGesture();
                    return;
                }
            }
            else
            {
                EndFistRotation();
            }
        }
        else
        {
            // Rotation deliberately requires one fist. Two hands remain
            // reserved for the existing Width/Height sculpting gesture.
            EndFistRotation();
        }

        if (widthIndex < 0 || heightIndex < 0)
            return;

        if (!hasTwoHands)
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

    public void SynchronizeWeightsFromRenderer()
    {
        ReadCurrentWeights();
        EndGesture();
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

    private bool HandleFistRotation(SculptHandFrame hand)
    {
        bool fistDetected = IsFist(
            hand, fistRotationActive || fistActivationTimer > 0f);
        if (!fistDetected)
        {
            EndFistRotation();
            return false;
        }

        // Claim the curled-finger pose immediately so the original controller
        // cannot treat it as a thumb sculpting command during activation.
        if (originalGestureController != null)
            originalGestureController.SetExternalFistRotationActive(true);

        if (!fistRotationActive)
        {
            fistActivationTimer += Time.deltaTime;
            if (fistActivationTimer < fistActivationTime)
                return true;

            BeginFistRotation(hand);
        }

        float horizontalMovement = hand.PalmCenter.x - fistBaselineX;
        if (Mathf.Abs(horizontalMovement) <= fistHorizontalDeadZone)
            horizontalMovement = 0f;
        else
        {
            horizontalMovement = Mathf.Sign(horizontalMovement) *
                (Mathf.Abs(horizontalMovement) - fistHorizontalDeadZone);
        }

        fistTargetAngle = horizontalMovement * fistRotationSensitivity;
        float factor = 1f - Mathf.Exp(-fistRotationSmoothing * Time.deltaTime);
        fistAppliedAngle = Mathf.LerpAngle(
            fistAppliedAngle, fistTargetAngle, factor);

        // Rotate from the pose captured at gesture start around the camera's
        // on-screen vertical direction. Rebuilding from that fixed pose every
        // frame prevents tracking noise or Euler conversion from introducing
        // rotation around any other axis.
        lanternRotationPivot.rotation =
            Quaternion.AngleAxis(fistAppliedAngle, fistLanternVerticalAxis) *
            fistStartingPivotRotation;
        return true;
    }

    private void BeginFistRotation(SculptHandFrame hand)
    {
        if (lanternRotationTarget == null)
            lanternRotationTarget = lanternRenderer.transform.root;
        EnsureLanternRotationPivot();
        if (lanternRotationPivot == null)
            return;

        fistStartingPivotRotation = lanternRotationPivot.rotation;
        // This imported lantern is authored with local Z as its true height
        // axis (the selfie surface shader uses the same convention). Rotate
        // around that transformed axis through the rendered centre so the
        // top and bottom stay aligned while the front turns toward the side.
        fistLanternVerticalAxis = lanternRenderer.transform
            .TransformDirection(Vector3.forward).normalized;
        fistAppliedAngle = 0f;
        fistTargetAngle = 0f;
        fistBaselineX = hand.PalmCenter.x;
        fistRotationActive = true;
        fistActivationTimer = 0f;
    }

    private void EnsureLanternRotationPivot()
    {
        if (lanternRotationPivot != null || lanternRotationTarget == null ||
            lanternRenderer == null)
            return;

        lanternOriginalParent = lanternRotationTarget.parent;
        GameObject pivotObject = new GameObject(
            "Lantern Screen-Vertical Rotation Pivot");
        lanternRotationPivot = pivotObject.transform;
        lanternRotationPivot.SetParent(lanternOriginalParent, false);

        // Use the rendered lantern's actual centre instead of the imported FBX
        // origin. This removes both the model-axis ambiguity and any orbiting
        // caused by an off-centre authoring pivot.
        lanternRotationPivot.position = lanternRenderer.transform.TransformPoint(
            lanternRenderer.localBounds.center);
        lanternRotationPivot.rotation = Quaternion.identity;
        lanternRotationPivot.localScale = Vector3.one;
        lanternRotationTarget.SetParent(lanternRotationPivot, true);
    }

    private void EndFistRotation()
    {
        fistRotationActive = false;
        fistActivationTimer = 0f;
        if (originalGestureController != null)
            originalGestureController.SetExternalFistRotationActive(false);
    }

    private bool IsFist(SculptHandFrame hand, bool useActiveTolerance)
    {
        float maximumExtension = useActiveTolerance
            ? activeFistMaximumExtension
            : fistMaximumExtension;

        return hand.FingerCurlRatio <= maximumExtension &&
               hand.OtherFingersCurlRatio <= maximumExtension &&
               hand.IndexTipPalmRatio <= maximumExtension + 0.12f &&
               hand.IndexExtensionAngle < 130f;
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
