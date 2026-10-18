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
    private float trackingLostTimer;
    private bool gestureActive;
    private bool gesturePaused;

    private void Awake()
    {
        BindLantern();
        FindBlendShapes();
        ReadCurrentWeights();
    }

    private void OnEnable()
    {
        BindLantern();
        FindBlendShapes();
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

        bool hasTwo = handRunner.TryGetLatestTwoHands(
            out SculptHandFrame first,
            out SculptHandFrame second);
        bool hasOne = hasTwo || handRunner.TryGetLatestHand(out first);
        int openPalms = 0;
        if (hasOne && IsOpenPalm(first))
            openPalms++;
        if (hasTwo && IsOpenPalm(second))
            openPalms++;

        if (openPalms >= 1 && gestureActive)
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

        bool stillPointing =
            IsIndexPointing(first, true) && IsIndexPointing(second, true);

        if (stillPointing)
        {
            if (gesturePaused)
                RebaseGesture(first, second);

            trackingLostTimer = 0f;
        }
        else
        {
            // Keep the last Width/Height. Only an open palm locks it.
            PauseGesture();
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
            string importedName = CompactName(mesh.GetBlendShapeName(i));
            if (importedName == CompactName(widthShape))
                widthIndex = i;
            else if (importedName == CompactName(heightShape))
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
        bool isNotVSign =
            hand.MiddleTipPalmRatio <= maximumMiddleFingerExtension;
        bool isNotOpenPalm = !IsOpenPalm(hand);

        return indexIsClearlyExtended &&
               otherFingersMatchPointing &&
               isNotPinching &&
               isNotVSign &&
               isNotOpenPalm;
    }

    private bool IsOpenPalm(SculptHandFrame hand)
    {
        return hand.PinchDistance >= hand.PalmWidth * 0.55f &&
               hand.FingerCurlRatio >= openPalmExtension &&
               hand.OtherFingersCurlRatio >= openPalmExtension * 0.92f &&
               hand.IndexTipPalmRatio >= 1.05f &&
               hand.MiddleTipPalmRatio > maximumMiddleFingerExtension;
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
            Mesh mesh = renderers[r].sharedMesh;
            if (mesh == null || mesh.blendShapeCount == 0)
                continue;

            int score = 0;
            string objectName = renderers[r].gameObject.name.ToUpperInvariant();
            if (objectName.Contains("FINALLANTERN") || objectName.Contains("LANTERN"))
                score += 10;

            for (int i = 0; i < mesh.blendShapeCount; i++)
            {
                string imported = CompactName(mesh.GetBlendShapeName(i));
                if (imported == "HEIGHT" || imported == "WIDTH")
                    score += 2;
            }

            if (score > bestScore)
            {
                bestScore = score;
                best = renderers[r];
            }
        }

        if (best != null)
            lanternRenderer = best;
    }

    private static string CompactName(string value)
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
}
