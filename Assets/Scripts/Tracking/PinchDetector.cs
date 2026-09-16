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
    [SerializeField] private float fistStartDistance = 0.16f;
    [SerializeField] private float fistReleaseDistance = 0.20f;

    [Header("Smoothing")]
    [SerializeField] private float smoothingSpeed = 18f;

    public bool IsTracking { get; private set; }
    public bool IsPinching { get; private set; }
    public bool IsFist { get; private set; }

    public float PinchDistance { get; private set; }
    public float FistDistance { get; private set; }

    public Vector3 SmoothedIndexTip { get; private set; }

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
        FistDistance = hand.IndexToWristDistance;

        if (!hasSmoothedPoint)
        {
            SmoothedIndexTip = hand.IndexTip;
            hasSmoothedPoint = true;
        }
        else
        {
            float t =
                1f - Mathf.Exp(-smoothingSpeed * Time.deltaTime);

            SmoothedIndexTip = Vector3.Lerp(
                SmoothedIndexTip,
                hand.IndexTip,
                t
            );
        }

        if (!IsFist && FistDistance <= fistStartDistance)
        {
            IsFist = true;
            Debug.Log("FIST START");
        }
        else if (IsFist && FistDistance >= fistReleaseDistance)
        {
            IsFist = false;
            Debug.Log("FIST END");
        }

        if (IsFist)
        {
            IsPinching = false;
            return;
        }

        if (!IsPinching &&
            PinchDistance <= pinchStartDistance)
        {
            IsPinching = true;
            Debug.Log("PINCH START");
        }
        else if (IsPinching &&
                 PinchDistance >= pinchReleaseDistance)
        {
            IsPinching = false;
            Debug.Log("PINCH END");
        }
    }
}