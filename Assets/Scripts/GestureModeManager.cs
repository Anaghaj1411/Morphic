using Mediapipe.Unity.Sample.HandLandmarkDetection;
using UnityEngine;

public enum GestureMode
{
    Idle,
    Width,
    BottomFlatten,
    Puff,
    TopGather,
    TopPinch
}

public class GestureModeManager : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private SculptHandLandmarkerRunner handRunner;

    [Header("Mode Detection")]
    [Tooltip("FingerCurlRatio is tip-to-palm / palm width (higher = more open).")]
    [SerializeField] private float fistMaxExtension = 1.08f;

    [SerializeField] private float openMinExtension = 1.18f;

    [SerializeField] private float pinchMaxDistance = 0.10f;

    [SerializeField] private float pinchMinOtherExtension = 1.20f;

    [SerializeField] private float modeActivationTime = 0.16f;

    [SerializeField] private float idleTimeout = 0.5f;

    [SerializeField] private float twoHandGraceTime = 0.4f;

    [Header("Debug")]
    [SerializeField] private bool showDebug;

    public GestureMode CurrentMode { get; private set; } = GestureMode.Idle;
    public GestureMode DetectedMode => detectedMode;
    public bool HasTwoHands { get; private set; }

    public bool IsActivelyDriving =>
        CurrentMode != GestureMode.Idle &&
        detectedMode == CurrentMode;

    private float modeActivationTimer;
    private float idleTimer;
    private float lastTwoHandsTime = -10f;
    private GestureMode detectedMode;
    private GestureMode pendingMode;

    private void Awake()
    {
        FindReferences();
    }

    private void Update()
    {
        FindReferences();

        if (handRunner == null)
        {
            return;
        }

        DetectGestureMode();
        UpdateModeState();
        UpdateIdleTimer();
    }

    private void DetectGestureMode()
    {
        bool hasTwoHands = handRunner.TryGetLatestTwoHands(
            out SculptHandFrame firstHand,
            out SculptHandFrame secondHand);

        if (hasTwoHands)
        {
            HasTwoHands = true;
            lastTwoHandsTime = Time.unscaledTime;

            bool bothPinching = IsPinch(firstHand) && IsPinch(secondHand);
            bool bothFists = IsFist(firstHand) && IsFist(secondHand);
            bool bothOpen = IsOpenHand(firstHand) && IsOpenHand(secondHand);

            if (bothPinching)
            {
                detectedMode = GestureMode.TopPinch;
                return;
            }

            if (bothFists)
            {
                detectedMode = GestureMode.Width;
                return;
            }

            if (bothOpen)
            {
                detectedMode = GestureMode.Puff;
                return;
            }

            if (CurrentMode == GestureMode.TopPinch &&
                (IsPinch(firstHand) || IsPinch(secondHand)))
            {
                detectedMode = GestureMode.TopPinch;
                return;
            }

            if (CurrentMode == GestureMode.Width &&
                (IsFist(firstHand) || IsFist(secondHand)))
            {
                detectedMode = GestureMode.Width;
                return;
            }

            if (CurrentMode == GestureMode.Puff &&
                (IsOpenHand(firstHand) || IsOpenHand(secondHand)))
            {
                detectedMode = GestureMode.Puff;
                return;
            }

            detectedMode = GestureMode.Idle;
            return;
        }

        HasTwoHands = false;

        bool stillInTwoHandGrace =
            Time.unscaledTime - lastTwoHandsTime <= twoHandGraceTime &&
            (CurrentMode == GestureMode.Width ||
             CurrentMode == GestureMode.Puff ||
             CurrentMode == GestureMode.TopPinch);

        if (stillInTwoHandGrace)
        {
            detectedMode = CurrentMode;
            return;
        }

        if (!handRunner.TryGetLatestHand(out SculptHandFrame singleHand))
        {
            detectedMode = GestureMode.Idle;
            return;
        }

        if (IsPinch(singleHand))
        {
            detectedMode = GestureMode.TopGather;
            return;
        }

        detectedMode = IsFist(singleHand)
            ? GestureMode.BottomFlatten
            : GestureMode.Idle;
    }

    private void UpdateModeState()
    {
        if (detectedMode == GestureMode.Idle)
        {
            modeActivationTimer = 0f;
            pendingMode = GestureMode.Idle;
            return;
        }

        if (detectedMode == CurrentMode)
        {
            modeActivationTimer = 0f;
            pendingMode = detectedMode;
            return;
        }

        if (pendingMode != detectedMode)
        {
            pendingMode = detectedMode;
            modeActivationTimer = 0f;
        }

        modeActivationTimer += Time.deltaTime;

        if (modeActivationTimer >= modeActivationTime)
        {
            CurrentMode = detectedMode;
            modeActivationTimer = 0f;
            idleTimer = 0f;
        }
    }

    private void UpdateIdleTimer()
    {
        if (detectedMode == GestureMode.Idle && CurrentMode != GestureMode.Idle)
        {
            idleTimer += Time.deltaTime;

            if (idleTimer >= idleTimeout)
            {
                CurrentMode = GestureMode.Idle;
                idleTimer = 0f;
            }
        }
        else
        {
            idleTimer = 0f;
        }
    }

    public bool IsPinch(SculptHandFrame hand)
    {
        return hand.PinchDistance <= pinchMaxDistance &&
               hand.OtherFingersCurlRatio >= pinchMinOtherExtension;
    }

    public bool IsFist(SculptHandFrame hand)
    {
        if (IsPinch(hand))
        {
            return false;
        }

        return hand.FingerCurlRatio <= fistMaxExtension ||
               hand.OtherFingersCurlRatio <= fistMaxExtension;
    }

    public bool IsOpenHand(SculptHandFrame hand)
    {
        if (IsPinch(hand))
        {
            return false;
        }

        return hand.FingerCurlRatio >= openMinExtension;
    }

    private void FindReferences()
    {
        if (handRunner == null)
        {
            handRunner = FindFirstObjectByType<SculptHandLandmarkerRunner>();
        }
    }
}
