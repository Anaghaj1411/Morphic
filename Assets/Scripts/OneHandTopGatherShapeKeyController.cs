using Mediapipe.Unity.Sample.HandLandmarkDetection;
using UnityEngine;

public class OneHandTopGatherShapeKeyController : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private SculptHandLandmarkerRunner handRunner;
    [SerializeField] private SculptShapeKeyController shapeKeyController;
    [SerializeField] private GestureModeManager modeManager;

    [Header("Gather Control")]
    [SerializeField] private float gatherSensitivity = 3.5f;
    [SerializeField] private float pinchTightenSensitivity = 6f;
    [SerializeField] private float verticalDeadZone = 0.008f;
    [SerializeField] private float pinchDeadZone = 0.004f;

    [Header("Gather Limits")]
    [SerializeField] private float minimumGather = 0f;
    [SerializeField] private float maximumGather = 1f;

    [Header("Filtering")]
    [SerializeField] private float positionSmoothingSpeed = 10f;
    [SerializeField] private float gatherSmoothingSpeed = 8f;

    private float startY;
    private float startPinchDistance;
    private float startGather;
    private float filteredY;
    private float filteredPinchDistance;
    private float targetGather;
    private float currentGather;
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
            currentGather = shapeKeyController.TopGather;
            targetGather = currentGather;
        }
    }

    private void Update()
    {
        FindReferences();

        if (handRunner == null || shapeKeyController == null || modeManager == null)
        {
            return;
        }

        bool inGatherMode = modeManager.CurrentMode == GestureMode.TopGather;
        bool driving = inGatherMode && modeManager.IsActivelyDriving;

        if (SculptDriveFilter.ShouldClearLatch(inGatherMode, ref timeOutOfMode))
        {
            hasSample = false;
            SmoothGather();
            return;
        }

        if (!driving)
        {
            SmoothGather();
            return;
        }

        if (!handRunner.TryGetLatestHand(out SculptHandFrame hand))
        {
            SmoothGather();
            return;
        }

        Vector3 pinchPoint = (hand.ThumbTip + hand.IndexTip) * 0.5f;
        float rawY = pinchPoint.y;
        float rawPinch = hand.PinchDistance;

        if (!hasSample)
        {
            startY = rawY;
            startPinchDistance = rawPinch;
            startGather = currentGather;
            filteredY = rawY;
            filteredPinchDistance = rawPinch;
            hasSample = true;
        }

        filteredY = SculptDriveFilter.ExpLerp(
            filteredY,
            rawY,
            positionSmoothingSpeed);

        filteredPinchDistance = SculptDriveFilter.ExpLerp(
            filteredPinchDistance,
            rawPinch,
            positionSmoothingSpeed);

        float upwardMovement = SculptDriveFilter.DeadZone(
            filteredY - startY,
            verticalDeadZone);

        float pinchTighten = SculptDriveFilter.DeadZone(
            startPinchDistance - filteredPinchDistance,
            pinchDeadZone);

        targetGather = Mathf.Clamp(
            startGather +
            upwardMovement * gatherSensitivity +
            pinchTighten * pinchTightenSensitivity,
            minimumGather,
            maximumGather);

        SmoothGather();
    }

    private void SmoothGather()
    {
        currentGather = SculptDriveFilter.ExpLerp(
            currentGather,
            targetGather,
            gatherSmoothingSpeed);

        shapeKeyController.SetTopGather(currentGather);
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
