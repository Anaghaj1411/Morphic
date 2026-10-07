using UnityEngine;
using TMPro;
using Mediapipe.Unity.Sample.HandLandmarkDetection;

public class TaiwanLanternGestureController : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private SculptHandLandmarkerRunner handRunner;
    [SerializeField] private ParametricSkyLantern lantern;
    [SerializeField] private TMP_Text gestureIndicator;

    [Header("Two Index Finger Gesture")]
    [SerializeField] private float minIndexExtension = 0.15f;
    [SerializeField] private float widthSensitivity = 1.2f;
    [SerializeField] private float heightSensitivity = 1.2f;

    [Header("Smoothing")]
    [SerializeField] private float widthSmoothing = 14f;
    [SerializeField] private float heightSmoothing = 14f;

    [Header("Width Limits")]
    [SerializeField] private float minimumWidth = 0.78f;
    [SerializeField] private float maximumWidth = 1.35f;

    [Header("Height Limits")]
    [SerializeField] private float minimumHeight = 0.82f;
    [SerializeField] private float maximumHeight = 1.45f;

    [Header("Gesture Stability")]
    [Tooltip("How long a temporary tracking loss can occur before leaving the gesture.")]
    [SerializeField] private float gestureGracePeriod = 0.5f;

    [Tooltip("How much the finger distance must change before movement becomes noticeable.")]
    [SerializeField] private float movementDeadZone = 0.003f;

    private float currentWidth = 1f;
    private float currentHeight = 1f;

    private float baseHorizontalDistance;
    private float baseVerticalDistance;

    private float baseLanternWidth;
    private float baseLanternHeight;

    private bool hasBaseline;
    private bool gestureActive;

    private float gestureLostTimer;

    private void Update()
    {
        if (handRunner == null || lantern == null)
        {
            SetGestureText("GESTURE: NONE");
            return;
        }

        if (!lantern.GuidedModeEnabled)
        {
            ResetCompletely();
            return;
        }

        bool handsAvailable =
            handRunner.TryGetLatestTwoHands(
                out SculptHandFrame firstHand,
                out SculptHandFrame secondHand);

        // ---------------------------------------------------------
        // HAND DATA TEMPORARILY LOST
        // ---------------------------------------------------------

        if (!handsAvailable)
        {
            HandleTemporaryLoss();
            return;
        }

        bool validGesture =
            IsTwoIndexFingerGesture(firstHand) &&
            IsTwoIndexFingerGesture(secondHand);

        // ---------------------------------------------------------
        // GESTURE CLASSIFICATION TEMPORARILY LOST
        // ---------------------------------------------------------

        if (!validGesture)
        {
            HandleTemporaryLoss();
            return;
        }

        // ---------------------------------------------------------
        // GESTURE IS VALID
        // ---------------------------------------------------------

        gestureLostTimer = 0f;

        if (!gestureActive)
        {
            BeginGesture(
                firstHand,
                secondHand
            );
        }

        UpdateGesture(
            firstHand,
            secondHand
        );

        SetGestureText("GESTURE: TWO INDEX FINGERS");
    }

    private void BeginGesture(
        SculptHandFrame firstHand,
        SculptHandFrame secondHand)
    {
        float horizontalDistance =
            Mathf.Abs(
                firstHand.IndexTip.x -
                secondHand.IndexTip.x
            );

        float verticalDistance =
            Mathf.Abs(
                firstHand.IndexTip.y -
                secondHand.IndexTip.y
            );

        baseHorizontalDistance = horizontalDistance;
        baseVerticalDistance = verticalDistance;

        // Capture the lantern's CURRENT shape.
        // This makes the gesture reversible.
        baseLanternWidth =
            Mathf.Lerp(
                minimumWidth,
                maximumWidth,
                lantern.WidthAmount
            );

        baseLanternHeight =
            Mathf.Lerp(
                minimumHeight,
                maximumHeight,
                lantern.HeightAmount
            );

        currentWidth = baseLanternWidth;
        currentHeight = baseLanternHeight;

        gestureActive = true;
        hasBaseline = true;
    }

    private void UpdateGesture(
        SculptHandFrame firstHand,
        SculptHandFrame secondHand)
    {
        if (!hasBaseline)
        {
            BeginGesture(
                firstHand,
                secondHand
            );
        }

        float horizontalDistance =
            Mathf.Abs(
                firstHand.IndexTip.x -
                secondHand.IndexTip.x
            );

        float verticalDistance =
            Mathf.Abs(
                firstHand.IndexTip.y -
                secondHand.IndexTip.y
            );

        float horizontalChange =
            horizontalDistance -
            baseHorizontalDistance;

        float verticalChange =
            verticalDistance -
            baseVerticalDistance;

        // Ignore microscopic MediaPipe noise.
        if (Mathf.Abs(horizontalChange) < movementDeadZone)
        {
            horizontalChange = 0f;
        }

        if (Mathf.Abs(verticalChange) < movementDeadZone)
        {
            verticalChange = 0f;
        }

        // IMPORTANT:
        // Calculate from the original gesture position,
        // NOT from the current value.
        //
        // Therefore:
        // fingers apart  -> wider
        // fingers together -> narrower
        // return to start -> original size
        float targetWidth =
            Mathf.Clamp(
                baseLanternWidth +
                horizontalChange * widthSensitivity,
                minimumWidth,
                maximumWidth
            );

        float targetHeight =
            Mathf.Clamp(
                baseLanternHeight +
                verticalChange * heightSensitivity,
                minimumHeight,
                maximumHeight
            );

        currentWidth =
            Mathf.Lerp(
                currentWidth,
                targetWidth,
                1f - Mathf.Exp(
                    -widthSmoothing *
                    Time.deltaTime
                )
            );

        currentHeight =
            Mathf.Lerp(
                currentHeight,
                targetHeight,
                1f - Mathf.Exp(
                    -heightSmoothing *
                    Time.deltaTime
                )
            );

        float widthAmount =
            Mathf.InverseLerp(
                minimumWidth,
                maximumWidth,
                currentWidth
            );

        float heightAmount =
            Mathf.InverseLerp(
                minimumHeight,
                maximumHeight,
                currentHeight
            );

        lantern.SetWidthAmount(widthAmount);
        lantern.SetHeightAmount(heightAmount);
    }

    private bool IsTwoIndexFingerGesture(
        SculptHandFrame hand)
    {
        float indexExtension =
            hand.IndexToWristDistance /
            Mathf.Max(
                hand.PalmWidth,
                0.001f
            );

        float otherFingerCurl =
            hand.OtherFingersCurlRatio;

        return
            indexExtension >= minIndexExtension &&
            otherFingerCurl < 1.5f;
    }

    private void HandleTemporaryLoss()
    {
        // If we are already controlling the lantern,
        // DON'T immediately cancel the gesture.
        if (gestureActive)
        {
            gestureLostTimer += Time.deltaTime;

            if (gestureLostTimer < gestureGracePeriod)
            {
                // Keep the current gesture alive.
                SetGestureText(
                    "GESTURE: TWO INDEX FINGERS"
                );

                return;
            }
        }

        // Only after the hand has genuinely been lost
        // do we end the gesture.
        ResetGesture();
    }

    private void ResetGesture()
    {
        gestureActive = false;
        hasBaseline = false;
        gestureLostTimer = 0f;

        SetGestureText("GESTURE: NONE");
    }

    private void ResetCompletely()
    {
        gestureActive = false;
        hasBaseline = false;
        gestureLostTimer = 0f;

        SetGestureText("GESTURE: NONE");
    }

    private void SetGestureText(string message)
    {
        if (gestureIndicator != null)
        {
            gestureIndicator.text = message;
        }
    }
}