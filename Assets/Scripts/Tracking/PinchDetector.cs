using Mediapipe.Unity.Sample.HandLandmarkDetection;
using UnityEngine;

public class PinchDetector : MonoBehaviour
{
    [Header("Tracking")]
    [SerializeField] private SculptHandLandmarkerRunner handRunner;

    [Header("Pinch Thresholds")]
    [SerializeField] private float pinchStartDistance = 0.06f;
    [SerializeField] private float pinchReleaseDistance = 0.09f;

    [Header("Fist Thresholds")]
    [SerializeField] private float fistStartDistance = 2.20f;
    [SerializeField] private float fistReleaseDistance = 2.50f;

    [Header("Smoothing")]
    [SerializeField] private float smoothingSpeed = 28f;

    public bool IsTracking { get; private set; }
    public bool IsPinching { get; private set; }
    public bool IsFist { get; private set; }

    public float PinchDistance { get; private set; }
    public float FistDistance { get; private set; }

    public Vector3 SmoothedIndexTip { get; private set; }
    public Vector3 SmoothedPalmCenter { get; private set; }

    private bool hasSmoothedPoint;

    private void Awake()
    {
        if (handRunner == null)
        {
            handRunner =
                FindFirstObjectByType<SculptHandLandmarkerRunner>();
        }
    }

    private void Update()
    {
        if (handRunner == null)
        {
            handRunner =
                FindFirstObjectByType<SculptHandLandmarkerRunner>();
        }

        if (handRunner == null ||
            !handRunner.TryGetLatestHand(out SculptHandFrame hand))
        {
            IsTracking = false;
            IsPinching = false;
            IsFist = false;
            hasSmoothedPoint = false;
            return;
        }

        IsTracking = true;
        PinchDistance = hand.PinchDistance;
        FistDistance = hand.OtherFingersCurlRatio;

        if (!hasSmoothedPoint)
        {
            SmoothedIndexTip = hand.IndexTip;
            SmoothedPalmCenter = hand.PalmCenter;
            hasSmoothedPoint = true;
        }
        else
        {
            float t = 1f - Mathf.Exp(-smoothingSpeed * Time.deltaTime);

            SmoothedIndexTip = Vector3.Lerp(
                SmoothedIndexTip,
                hand.IndexTip,
                t
            );

            SmoothedPalmCenter = Vector3.Lerp(
                SmoothedPalmCenter,
                hand.PalmCenter,
                t
            );
        }

        float otherFingersCurl = hand.OtherFingersCurlRatio;

        // A fist requires middle, ring, and pinky fingers to be curled inward (<= 1.45f)
        float fistEnter = fistStartDistance > 1f ? fistStartDistance : 1.45f;
        float fistExit = fistReleaseDistance > 1f ? fistReleaseDistance : 1.70f;

        if (!IsFist && otherFingersCurl <= fistEnter)
        {
            IsFist = true;
            IsPinching = false;
        }
        else if (IsFist && otherFingersCurl >= fistExit)
        {
            IsFist = false;
        }

        if (IsFist)
        {
            IsPinching = false;
            return;
        }

        // A pinch requires thumb and index to touch AND middle/ring/pinky to be extended (>= 1.55f)
        if (!IsPinching &&
            PinchDistance <= pinchStartDistance &&
            otherFingersCurl >= 1.55f)
        {
            IsPinching = true;
        }
        else if (IsPinching &&
                 (PinchDistance >= pinchReleaseDistance || otherFingersCurl < 1.45f))
        {
            IsPinching = false;
        }
    }
}