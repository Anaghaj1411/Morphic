using Mediapipe.Unity.Sample.HandLandmarkDetection;
using UnityEngine;

public class OneHandBottomFlattenShapeKeyController : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private SculptHandLandmarkerRunner handRunner;
    [SerializeField] private SculptShapeKeyController shapeKeyController;
    [SerializeField] private GestureModeManager modeManager;

    [Header("Flatten Control")]
    [SerializeField] private float flattenSensitivity = 3.5f;
    [SerializeField] private float verticalDeadZone = 0.008f;

    [Header("Flatten Limits")]
    [SerializeField] private float minimumFlatten = 0f;
    [SerializeField] private float maximumFlatten = 1f;

    [Header("Filtering")]
    [SerializeField] private float positionSmoothingSpeed = 10f;
    [SerializeField] private float flattenSmoothingSpeed = 8f;

    public bool IsPinching { get; private set; }

    private float startY;
    private float startFlatten;
    private float filteredY;
    private float targetFlatten;
    private float currentFlatten;
    private bool hasPositionSample;
    private float timeOutOfMode;

    private void Awake()
    {
        FindReferences();
    }

    private void Start()
    {
        if (shapeKeyController != null)
        {
            currentFlatten = shapeKeyController.BottomFlatten;
            targetFlatten = currentFlatten;
        }
    }

    private void Update()
    {
        FindReferences();

        if (handRunner == null || shapeKeyController == null || modeManager == null)
        {
            return;
        }

        bool inFlattenMode = modeManager.CurrentMode == GestureMode.BottomFlatten;
        bool driving = inFlattenMode && modeManager.IsActivelyDriving;

        if (SculptDriveFilter.ShouldClearLatch(inFlattenMode, ref timeOutOfMode))
        {
            hasPositionSample = false;
            IsPinching = false;
            SmoothFlatten();
            return;
        }

        if (!driving)
        {
            IsPinching = false;
            SmoothFlatten();
            return;
        }

        if (!handRunner.TryGetLatestHand(out SculptHandFrame hand))
        {
            SmoothFlatten();
            return;
        }

        IsPinching = true;

        float rawY = hand.PalmCenter.y;

        if (!hasPositionSample)
        {
            startY = rawY;
            startFlatten = currentFlatten;
            filteredY = rawY;
            hasPositionSample = true;
        }

        filteredY = SculptDriveFilter.ExpLerp(
            filteredY,
            rawY,
            positionSmoothingSpeed);

        float downwardMovement = SculptDriveFilter.DeadZone(
            startY - filteredY,
            verticalDeadZone);

        targetFlatten = Mathf.Clamp(
            startFlatten + downwardMovement * flattenSensitivity,
            minimumFlatten,
            maximumFlatten);

        SmoothFlatten();
    }

    private void SmoothFlatten()
    {
        currentFlatten = SculptDriveFilter.ExpLerp(
            currentFlatten,
            targetFlatten,
            flattenSmoothingSpeed);

        shapeKeyController.SetBottomFlatten(currentFlatten);
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
