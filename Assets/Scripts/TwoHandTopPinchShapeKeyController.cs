using Mediapipe.Unity.Sample.HandLandmarkDetection;
using UnityEngine;

public class TwoHandTopPinchShapeKeyController : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private SculptHandLandmarkerRunner handRunner;
    [SerializeField] private SculptShapeKeyController shapeKeyController;
    [SerializeField] private GestureModeManager modeManager;

    [Header("Pinch Tightness")]
    [SerializeField] private float pinchSensitivity = 8f;
    [SerializeField] private float pinchDeadZone = 0.004f;

    [Header("Pinch Limits")]
    [SerializeField] private float minimumPinch = 0f;
    [SerializeField] private float maximumPinch = 1f;

    [Header("Smoothing")]
    [SerializeField] private float pinchSmoothingSpeed = 8f;
    [SerializeField] private float shapeSmoothingSpeed = 8f;

    private float startPinchDistance;
    private float startPinch;
    private float filteredPinchDistance;
    private float targetPinch;
    private float currentPinch;
    private bool hasSample;
    private float timeOutOfMode;

    private void Awake()
    {
        FindReferences();
    }

    private void Start()
    {
        if (shapeKeyController != null)
        {
            currentPinch = shapeKeyController.TopPinch;
            targetPinch = currentPinch;
        }
    }

    private void Update()
    {
        FindReferences();

        if (handRunner == null || shapeKeyController == null || modeManager == null)
        {
            return;
        }

        bool inPinchMode = modeManager.CurrentMode == GestureMode.TopPinch;
        bool driving = inPinchMode && modeManager.IsActivelyDriving;

        if (SculptDriveFilter.ShouldClearLatch(inPinchMode, ref timeOutOfMode))
        {
            hasSample = false;
            SmoothPinch();
            return;
        }

        if (!driving)
        {
            SmoothPinch();
            return;
        }

        if (!handRunner.TryGetLatestTwoHands(
                out SculptHandFrame firstHand,
                out SculptHandFrame secondHand))
        {
            SmoothPinch();
            return;
        }

        float rawPinchDistance =
            (firstHand.PinchDistance + secondHand.PinchDistance) * 0.5f;

        if (!hasSample)
        {
            startPinchDistance = rawPinchDistance;
            startPinch = currentPinch;
            filteredPinchDistance = rawPinchDistance;
            hasSample = true;
        }

        filteredPinchDistance = SculptDriveFilter.ExpLerp(
            filteredPinchDistance,
            rawPinchDistance,
            pinchSmoothingSpeed);

        float pinchTighten = SculptDriveFilter.DeadZone(
            startPinchDistance - filteredPinchDistance,
            pinchDeadZone);

        targetPinch = Mathf.Clamp(
            startPinch + pinchTighten * pinchSensitivity,
            minimumPinch,
            maximumPinch);

        SmoothPinch();
    }

    private void SmoothPinch()
    {
        currentPinch = SculptDriveFilter.ExpLerp(
            currentPinch,
            targetPinch,
            shapeSmoothingSpeed);

        shapeKeyController.SetTopPinch(currentPinch);
    }

    private void FindReferences()
    {
        if (handRunner == null)
        {
            handRunner = FindFirstObjectByType<SculptHandLandmarkerRunner>();
        }

        if (shapeKeyController == null)
        {
            shapeKeyController = GetComponent<SculptShapeKeyController>();
        }

        if (modeManager == null)
        {
            modeManager = GetComponent<GestureModeManager>();

            if (modeManager == null)
            {
                modeManager = FindFirstObjectByType<GestureModeManager>();
            }
        }
    }
}