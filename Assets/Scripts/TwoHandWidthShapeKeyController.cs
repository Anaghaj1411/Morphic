using Mediapipe.Unity.Sample.HandLandmarkDetection;
using UnityEngine;

public class TwoHandWidthShapeKeyController : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private SculptHandLandmarkerRunner handRunner;
    [SerializeField] private SculptShapeKeyController shapeKeyController;
    [SerializeField] private GestureModeManager modeManager;

    [Header("Relative Width Control")]
    [SerializeField] private float widthSensitivity = 3.2f;
    [SerializeField] private float distanceDeadZone = 0.008f;

    [Header("Width Limits")]
    [SerializeField] private float minimumWidth = 0f;
    [SerializeField] private float maximumWidth = 1f;

    [Header("Filtering")]
    [SerializeField] private float distanceSmoothingSpeed = 10f;
    [SerializeField] private float widthSmoothingSpeed = 8f;

    public bool IsDualPinching { get; private set; }

    private float startSeparation;
    private float startWidth;
    private float filteredSeparation;
    private float targetWidth;
    private float currentWidth;
    private bool hasDistanceSample;
    private float timeOutOfMode;

    private void Awake()
    {
        FindReferences();
    }

    private void Start()
    {
        if (shapeKeyController != null)
        {
            currentWidth = shapeKeyController.Width;
            targetWidth = currentWidth;
        }
    }

    private void Update()
    {
        FindReferences();

        if (handRunner == null || shapeKeyController == null || modeManager == null)
        {
            return;
        }

        bool inWidthMode = modeManager.CurrentMode == GestureMode.Width;
        bool driving = inWidthMode && modeManager.IsActivelyDriving;

        if (SculptDriveFilter.ShouldClearLatch(inWidthMode, ref timeOutOfMode))
        {
            hasDistanceSample = false;
            IsDualPinching = false;
            SmoothWidth();
            return;
        }

        if (!driving)
        {
            IsDualPinching = false;
            SmoothWidth();
            return;
        }

        if (!handRunner.TryGetLatestTwoHands(
                out SculptHandFrame firstHand,
                out SculptHandFrame secondHand))
        {
            SmoothWidth();
            return;
        }

        IsDualPinching = true;

        float rawSeparation = GetHandSeparation(firstHand, secondHand);

        if (!hasDistanceSample)
        {
            startSeparation = rawSeparation;
            startWidth = currentWidth;
            filteredSeparation = rawSeparation;
            hasDistanceSample = true;
        }

        filteredSeparation = SculptDriveFilter.ExpLerp(
            filteredSeparation,
            rawSeparation,
            distanceSmoothingSpeed);

        float separationDelta = SculptDriveFilter.DeadZone(
            filteredSeparation - startSeparation,
            distanceDeadZone);

        targetWidth = Mathf.Clamp(
            startWidth + separationDelta * widthSensitivity,
            minimumWidth,
            maximumWidth);

        SmoothWidth();
    }

    private static float GetHandSeparation(
        SculptHandFrame firstHand,
        SculptHandFrame secondHand)
    {
        Vector2 first = new Vector2(firstHand.PalmCenter.x, firstHand.PalmCenter.y);
        Vector2 second = new Vector2(secondHand.PalmCenter.x, secondHand.PalmCenter.y);
        return Vector2.Distance(first, second);
    }

    private void SmoothWidth()
    {
        currentWidth = SculptDriveFilter.ExpLerp(
            currentWidth,
            targetWidth,
            widthSmoothingSpeed);

        shapeKeyController.SetWidth(currentWidth);
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
