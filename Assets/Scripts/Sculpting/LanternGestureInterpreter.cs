using Mediapipe.Unity.Sample.HandLandmarkDetection;
using UnityEngine;

public class LanternGestureInterpreter : MonoBehaviour
{
    private enum LanternDrive
    {
        None,
        Width,
        Height,
        Taper,
        Burner,
        GlowHold
    }

    [Header("References")]
    [SerializeField] private ParametricSkyLantern lantern;
    [SerializeField] private SculptHandLandmarkerRunner handRunner;

    [Header("Pose")]
    [SerializeField] private float pinchMaxDistance = 0.09f;
    [SerializeField] private float openMinExtension = 1.18f;

    [Header("Axis Separation")]
    [SerializeField] private float axisDominanceRatio = 1.35f;
    [SerializeField] private float similarHeightLimit = 0.14f;

    [Header("Sensitivity")]
    [SerializeField] private float widthSensitivity = 2.8f;
    [SerializeField] private float heightSensitivity = 2.8f;
    [SerializeField] private float taperSensitivity = 2.4f;
    [SerializeField] private float burnerSensitivity = 3.2f;

    [Header("Stability")]
    [SerializeField] private float sizeDeadZone = 0.012f;
    [SerializeField] private float taperDeadZone = 0.012f;
    [SerializeField] private float burnerDeadZone = 0.018f;
    [SerializeField] private float glowStillness = 0.018f;
    [SerializeField] private float glowHoldSeconds = 0.85f;
    [SerializeField] private float smoothingSpeed = 10f;

    private LanternDrive currentDrive = LanternDrive.None;

    private float startMetric;
    private float startValue;
    private float filteredMetric;
    private bool hasSample;

    private float glowTimer;
    private float lastSeparationX;
    private float lastSeparationY;
    private float lastAverageY;
    private bool hasMotionSample;

    private void Awake()
    {
        FindReferences();
    }

    private void Update()
    {
        FindReferences();

        if (lantern == null ||
            !lantern.GuidedModeEnabled ||
            handRunner == null)
        {
            ResetDrive();
            return;
        }

        if (!handRunner.TryGetLatestTwoHands(
                out SculptHandFrame firstHand,
                out SculptHandFrame secondHand))
        {
            ResetDrive();
            glowTimer = 0f;
            hasMotionSample = false;
            return;
        }

        float separationX = Mathf.Abs(firstHand.PalmCenter.x - secondHand.PalmCenter.x);
        float separationY = Mathf.Abs(firstHand.PalmCenter.y - secondHand.PalmCenter.y);
        float averageY = (firstHand.PalmCenter.y + secondHand.PalmCenter.y) * 0.5f;

        if (!hasMotionSample)
        {
            lastSeparationX = separationX;
            lastSeparationY = separationY;
            lastAverageY = averageY;
            hasMotionSample = true;
        }

        float smooth = 1f - Mathf.Exp(-smoothingSpeed * Time.deltaTime);
        float filteredX = Mathf.Lerp(lastSeparationX, separationX, smooth);
        float filteredY = Mathf.Lerp(lastSeparationY, separationY, smooth);
        float filteredAvgY = Mathf.Lerp(lastAverageY, averageY, smooth);

        float deltaX = filteredX - lastSeparationX;
        float deltaY = filteredY - lastSeparationY;
        float deltaAvgY = filteredAvgY - lastAverageY;

        lastSeparationX = filteredX;
        lastSeparationY = filteredY;
        lastAverageY = filteredAvgY;

        bool bothPinching = IsPinch(firstHand) && IsPinch(secondHand);
        bool bothOpen = IsOpen(firstHand) && IsOpen(secondHand);
        bool handsAligned =
            Mathf.Abs(firstHand.PalmCenter.y - secondHand.PalmCenter.y) <=
            similarHeightLimit;

        LanternDrive detected = DetectDrive(
            bothPinching,
            bothOpen,
            handsAligned,
            filteredX,
            filteredY,
            deltaX,
            deltaY,
            deltaAvgY);

        if (detected != currentDrive)
        {
            currentDrive = detected;
            hasSample = false;
            if (detected != LanternDrive.GlowHold)
            {
                glowTimer = 0f;
            }
        }

        DriveDetected(detected, filteredX, filteredY, filteredAvgY);
    }

    private LanternDrive DetectDrive(
        bool bothPinching,
        bool bothOpen,
        bool handsAligned,
        float separationX,
        float separationY,
        float deltaX,
        float deltaY,
        float deltaAvgY)
    {
        bool horizontalDominant =
            separationX > separationY * axisDominanceRatio;
        bool verticalDominant =
            separationY > separationX * axisDominanceRatio;

        if (bothPinching && verticalDominant)
        {
            return LanternDrive.Height;
        }

        if (bothPinching && horizontalDominant)
        {
            return LanternDrive.Width;
        }

        if (bothOpen &&
            Mathf.Abs(deltaX) < glowStillness &&
            Mathf.Abs(deltaY) < glowStillness &&
            Mathf.Abs(deltaAvgY) < glowStillness)
        {
            return LanternDrive.GlowHold;
        }

        bool pullingDown =
            handsAligned &&
            deltaAvgY < -burnerDeadZone &&
            Mathf.Abs(deltaAvgY) > Mathf.Abs(deltaX);

        if (pullingDown && !bothPinching)
        {
            return LanternDrive.Burner;
        }

        if (!bothPinching && horizontalDominant)
        {
            return LanternDrive.Taper;
        }

        if (currentDrive == LanternDrive.Width && bothPinching && !verticalDominant)
        {
            return LanternDrive.Width;
        }

        if (currentDrive == LanternDrive.Height && bothPinching && !horizontalDominant)
        {
            return LanternDrive.Height;
        }

        if (currentDrive == LanternDrive.Taper && !bothPinching)
        {
            return LanternDrive.Taper;
        }

        return LanternDrive.None;
    }

    private void DriveDetected(
        LanternDrive drive,
        float separationX,
        float separationY,
        float averageY)
    {
        switch (drive)
        {
            case LanternDrive.Width:
                DriveRelative(
                    separationX,
                    lantern.WidthAmount,
                    sizeDeadZone,
                    widthSensitivity,
                    lantern.SetWidthAmount);
                break;

            case LanternDrive.Height:
                DriveRelative(
                    separationY,
                    lantern.HeightAmount,
                    sizeDeadZone,
                    heightSensitivity,
                    lantern.SetHeightAmount);
                break;

            case LanternDrive.Taper:
                DriveRelative(
                    -separationX,
                    lantern.TaperAmount,
                    taperDeadZone,
                    taperSensitivity,
                    lantern.SetTaperAmount);
                break;

            case LanternDrive.Burner:
                DriveRelative(
                    -averageY,
                    lantern.BurnerAmount,
                    burnerDeadZone,
                    burnerSensitivity,
                    lantern.SetBurnerAmount);
                break;

            case LanternDrive.GlowHold:
                glowTimer += Time.deltaTime;
                if (glowTimer >= glowHoldSeconds)
                {
                    lantern.ActivateGlow();
                }
                break;

            default:
                hasSample = false;
                break;
        }
    }

    private void DriveRelative(
        float metric,
        float currentValue,
        float deadZone,
        float sensitivity,
        System.Action<float> setter)
    {
        if (!hasSample)
        {
            startMetric = metric;
            startValue = currentValue;
            filteredMetric = metric;
            hasSample = true;
        }

        filteredMetric = SculptDriveFilter.ExpLerp(
            filteredMetric,
            metric,
            smoothingSpeed);

        float delta = SculptDriveFilter.DeadZone(
            filteredMetric - startMetric,
            deadZone);

        setter(Mathf.Clamp01(startValue + delta * sensitivity));
    }

    private void ResetDrive()
    {
        currentDrive = LanternDrive.None;
        hasSample = false;
    }

    private bool IsPinch(SculptHandFrame hand)
    {
        return hand.PinchDistance <= pinchMaxDistance &&
               hand.OtherFingersCurlRatio >= 1.15f;
    }

    private bool IsOpen(SculptHandFrame hand)
    {
        return !IsPinch(hand) &&
               hand.FingerCurlRatio >= openMinExtension;
    }

    private void FindReferences()
    {
        if (lantern == null)
        {
            lantern = GetComponent<ParametricSkyLantern>();
        }

        if (handRunner == null)
        {
            handRunner = FindFirstObjectByType<SculptHandLandmarkerRunner>();
        }
    }
}
