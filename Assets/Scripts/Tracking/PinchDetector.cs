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

    // Curl-ratio fallbacks, used when the values above are not ratios.
    // Lowered from the old hard-coded 1.45/1.70 so that a natural pinch
    // (other fingers relaxed) is no longer misread as a fist.
    [SerializeField] private float fistCurlEnter = 1.05f;
    [SerializeField] private float fistCurlExit = 1.30f;

    [Header("Pinch Finger Extension")]
    // How extended the middle/ring/pinky fingers must be for a pinch to count.
    // The old hard-coded 1.55 sat just above the fist threshold, so in practice
    // only a fully splayed hand could ever register a pinch.
    [SerializeField] private float pinchMinOtherExtension = 1.10f;
    [SerializeField] private float pinchReleaseOtherExtension = 0.95f;

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

        // A fist requires middle, ring, and pinky fingers to be curled inward.
        float fistEnter = fistStartDistance > 1f ? fistStartDistance : fistCurlEnter;
        float fistExit = fistReleaseDistance > 1f ? fistReleaseDistance : fistCurlExit;

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

        // A pinch requires thumb and index to touch. The other three fingers
        // only need to stay out of a fist, not be fully splayed.
        if (!IsPinching &&
            PinchDistance <= pinchStartDistance &&
            otherFingersCurl >= pinchMinOtherExtension)
        {
            IsPinching = true;
        }
        else if (IsPinching &&
                 (PinchDistance >= pinchReleaseDistance ||
                  otherFingersCurl < pinchReleaseOtherExtension))
        {
            IsPinching = false;
        }
    }
}