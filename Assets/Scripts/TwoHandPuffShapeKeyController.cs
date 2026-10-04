using Mediapipe.Unity.Sample.HandLandmarkDetection;
using UnityEngine;

public class TwoHandPuffShapeKeyController : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private SculptHandLandmarkerRunner handRunner;
    [SerializeField] private SculptShapeKeyController shapeKeyController;
    [SerializeField] private GestureModeManager modeManager;

    [Header("Open Hand Puff")]
    [SerializeField] private float puffSensitivity = 1.15f;
    [SerializeField] private float openDeadZone = 0.03f;

    [Header("Puff Limits")]
    [SerializeField] private float minimumPuff = 0f;
    [SerializeField] private float maximumPuff = 1f;

    [Header("Smoothing")]
    [SerializeField] private float handOpenSmoothingSpeed = 8f;
    [SerializeField] private float puffSmoothingSpeed = 8f;

    public bool IsHandOpen { get; private set; }

    private float startOpenRatio;
    private float startPuff;
    private float filteredOpenRatio;
    private float targetPuff;
    private float currentPuff;
    private bool hasOpenSample;
    private float timeOutOfMode;

    private void Awake()
    {
        FindReferences();
    }

    private void Start()
    {
        if (shapeKeyController != null)
        {
            currentPuff = shapeKeyController.Puff;
            targetPuff = currentPuff;
        }
    }

    private void Update()
    {
        FindReferences();

        if (handRunner == null || shapeKeyController == null || modeManager == null)
        {
            return;
        }

        bool inPuffMode = modeManager.CurrentMode == GestureMode.Puff;
        bool driving = inPuffMode && modeManager.IsActivelyDriving;

        if (SculptDriveFilter.ShouldClearLatch(inPuffMode, ref timeOutOfMode))
        {
            hasOpenSample = false;
            SmoothPuff();
            return;
        }

        if (!driving)
        {
            SmoothPuff();
            return;
        }

        if (!handRunner.TryGetLatestTwoHands(
                out SculptHandFrame firstHand,
                out SculptHandFrame secondHand))
        {
            SmoothPuff();
            return;
        }

        float rawOpenRatio =
            (GetOpenHandRatio(firstHand) + GetOpenHandRatio(secondHand)) * 0.5f;

        if (!hasOpenSample)
        {
            startOpenRatio = rawOpenRatio;
            startPuff = currentPuff;
            filteredOpenRatio = rawOpenRatio;
            hasOpenSample = true;
        }

        filteredOpenRatio = SculptDriveFilter.ExpLerp(
            filteredOpenRatio,
            rawOpenRatio,
            handOpenSmoothingSpeed);

        float openDelta = SculptDriveFilter.DeadZone(
            filteredOpenRatio - startOpenRatio,
            openDeadZone);

        targetPuff = Mathf.Clamp(
            startPuff + openDelta * puffSensitivity,
            minimumPuff,
            maximumPuff);

        IsHandOpen = true;
        SmoothPuff();
    }

    private static float GetOpenHandRatio(SculptHandFrame hand)
    {
        float extensionScore = hand.FingerCurlRatio;

        float thumbIndexDistance = Vector2.Distance(
            new Vector2(hand.ThumbTip.x, hand.ThumbTip.y),
            new Vector2(hand.IndexTip.x, hand.IndexTip.y));

        float spreadScore = thumbIndexDistance / Mathf.Max(hand.PalmWidth, 0.001f);

        return extensionScore * 0.75f + spreadScore * 0.25f;
    }

    private void SmoothPuff()
    {
        currentPuff = SculptDriveFilter.ExpLerp(
            currentPuff,
            targetPuff,
            puffSmoothingSpeed);

        shapeKeyController.SetPuff(currentPuff);
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
