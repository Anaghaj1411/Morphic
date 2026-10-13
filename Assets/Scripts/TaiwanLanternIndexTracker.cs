using UnityEngine;
using TMPro;
using Mediapipe.Unity.Sample.HandLandmarkDetection;

public class TaiwanLanternIndexTracker : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private SculptHandLandmarkerRunner handRunner;
    [SerializeField] private ParametricSkyLantern lantern;
    [SerializeField] private TMP_Text gestureIndicator;

    [Header("Tracking")]
    [SerializeField] private float positionSmoothing = 18f;

    [Header("Width")]
    [SerializeField] private float widthSensitivity = 2.2f;
    [SerializeField] private float minimumWidth = 0.78f;
    [SerializeField] private float maximumWidth = 1.35f;

    [Header("Height")]
    [SerializeField] private float heightSensitivity = 2.2f;
    [SerializeField] private float minimumHeight = 0.82f;
    [SerializeField] private float maximumHeight = 1.45f;

    [Header("Tracking Stability")]
    [SerializeField] private float lostHandGracePeriod = 0.35f;

    [Header("Index Detection")]
    [SerializeField] private float minimumIndexExtension = 0.14f;

    private Vector2 smoothedFirstIndex;
    private Vector2 smoothedSecondIndex;

    private Vector2 baselineFirstIndex;
    private Vector2 baselineSecondIndex;

    private float baselineWidth;
    private float baselineHeight;

    private bool trackingActive;
    private float lostTimer;

    private float currentWidth;
    private float currentHeight;

    private void Update()
    {
        if (handRunner == null || lantern == null)
        {
            SetGestureText("GESTURE: NONE");
            return;
        }

        if (!lantern.GuidedModeEnabled)
        {
            StopTracking();
            return;
        }

        if (!handRunner.TryGetLatestTwoHands(
                out SculptHandFrame firstHand,
                out SculptHandFrame secondHand))
        {
            HandleTrackingLoss();
            return;
        }

        // Only use the index finger extension to acquire the gesture.
        // We do NOT use OtherFingersCurlRatio.
        if (!IsIndexExtended(firstHand) ||
            !IsIndexExtended(secondHand))
        {
            HandleTrackingLoss();
            return;
        }

        lostTimer = 0f;

        Vector2 firstIndex = new Vector2(
            firstHand.IndexTip.x,
            firstHand.IndexTip.y
        );

        Vector2 secondIndex = new Vector2(
            secondHand.IndexTip.x,
            secondHand.IndexTip.y
        );

        if (!trackingActive)
        {
            StartTracking(firstIndex, secondIndex);
        }

        // Smooth the actual fingertip positions.
        float smoothing =
            1f - Mathf.Exp(
                -positionSmoothing * Time.deltaTime
            );

        smoothedFirstIndex = Vector2.Lerp(
            smoothedFirstIndex,
            firstIndex,
            smoothing
        );

        smoothedSecondIndex = Vector2.Lerp(
            smoothedSecondIndex,
            secondIndex,
            smoothing
        );

        UpdateLantern();

        SetGestureText("GESTURE: TWO INDEX FINGERS");
    }

    private bool IsIndexExtended(SculptHandFrame hand)
    {
        float indexExtension =
            hand.IndexToWristDistance /
            Mathf.Max(hand.PalmWidth, 0.001f);

        return indexExtension >= minimumIndexExtension;
    }

    private void StartTracking(
        Vector2 firstIndex,
        Vector2 secondIndex)
    {
        trackingActive = true;
        lostTimer = 0f;

        smoothedFirstIndex = firstIndex;
        smoothedSecondIndex = secondIndex;

        baselineFirstIndex = firstIndex;
        baselineSecondIndex = secondIndex;

        // Capture the lantern's current shape.
        baselineWidth = Mathf.Lerp(
            minimumWidth,
            maximumWidth,
            lantern.WidthAmount
        );

        baselineHeight = Mathf.Lerp(
            minimumHeight,
            maximumHeight,
            lantern.HeightAmount
        );

        currentWidth = baselineWidth;
        currentHeight = baselineHeight;
    }

    private void UpdateLantern()
    {
        float currentHorizontalDistance =
            Mathf.Abs(
                smoothedFirstIndex.x -
                smoothedSecondIndex.x
            );

        float baselineHorizontalDistance =
            Mathf.Abs(
                baselineFirstIndex.x -
                baselineSecondIndex.x
            );

        float currentVerticalDistance =
            Mathf.Abs(
                smoothedFirstIndex.y -
                smoothedSecondIndex.y
            );

        float baselineVerticalDistance =
            Mathf.Abs(
                baselineFirstIndex.y -
                baselineSecondIndex.y
            );

        float horizontalChange =
            currentHorizontalDistance -
            baselineHorizontalDistance;

        float verticalChange =
            currentVerticalDistance -
            baselineVerticalDistance;

        float targetWidth =
            Mathf.Clamp(
                baselineWidth +
                horizontalChange * widthSensitivity,
                minimumWidth,
                maximumWidth
            );

        float targetHeight =
            Mathf.Clamp(
                baselineHeight +
                verticalChange * heightSensitivity,
                minimumHeight,
                maximumHeight
            );

        float shapeSmoothing =
            1f - Mathf.Exp(
                -positionSmoothing * Time.deltaTime
            );

        currentWidth = Mathf.Lerp(
            currentWidth,
            targetWidth,
            shapeSmoothing
        );

        currentHeight = Mathf.Lerp(
            currentHeight,
            targetHeight,
            shapeSmoothing
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

    private void HandleTrackingLoss()
    {
        if (!trackingActive)
        {
            SetGestureText("GESTURE: NONE");
            return;
        }

        lostTimer += Time.deltaTime;

        // Keep the current tracking state alive through
        // short MediaPipe interruptions.
        if (lostTimer < lostHandGracePeriod)
        {
            SetGestureText("GESTURE: TWO INDEX FINGERS");
            return;
        }

        StopTracking();
    }

    private void StopTracking()
    {
        trackingActive = false;
        lostTimer = 0f;

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