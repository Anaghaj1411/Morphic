using Mediapipe.Unity.Sample.HandLandmarkDetection;
using UnityEngine;

public class TwoHandScaleSculpture : MonoBehaviour
{
    public enum ScaleMode
    {
        IndependentXY,
        Proportional3D
    }

    [SerializeField] private SculptHandLandmarkerRunner handRunner;

    [Header("Scale Mode")]
    [SerializeField] private ScaleMode scaleMode = ScaleMode.IndependentXY;

    [Header("Dual Pinch Thresholds")]
    [SerializeField] private float dualPinchStartDistance = 0.07f;
    [SerializeField] private float dualPinchReleaseDistance = 0.09f;

    [Header("Stretch Limits")]
    [SerializeField] private float minimumScaleFactor = 0.4f;
    [SerializeField] private float maximumScaleFactor = 3.0f;

    [Header("Absolute Sensitivities")]
    [SerializeField] private float widthSensitivity = 1.8f;
    [SerializeField] private float heightSensitivity = 2.5f;
    [SerializeField] private float uniform3DSensitivity = 1.8f;

    [Header("Dead Zone & Filtering")]
    [SerializeField] private float distanceDeadZone = 0.015f;

    [Header("Smoothing")]
    [SerializeField] private float scaleSmoothingSpeed = 10f;

    public bool IsDualPinching { get; private set; }

    private bool isStretching;
    private float startHandDistance;
    private float startHorizDistance;
    private float startVertDistance;
    private Vector3 startObjectScale;
    private Vector3 targetScale;
    private Vector3 initialBaseScale;

    private void Awake()
    {
        FindTrackingComponents();
    }

    private void Start()
    {
        initialBaseScale = transform.localScale;
        targetScale = initialBaseScale;
    }

    private void Update()
    {
        FindTrackingComponents();

        if (handRunner == null ||
            !handRunner.TryGetLatestTwoHands(
                out SculptHandFrame firstHand,
                out SculptHandFrame secondHand))
        {
            StopStretching();
            SmoothToTarget();
            return;
        }

        float threshold = IsDualPinching ? dualPinchReleaseDistance : dualPinchStartDistance;
        bool firstIsPinching = firstHand.PinchDistance <= threshold;
        bool secondIsPinching = secondHand.PinchDistance <= threshold;

        bool bothPinching = firstIsPinching && secondIsPinching;
        IsDualPinching = bothPinching;

        if (!bothPinching)
        {
            StopStretching();
            SmoothToTarget();
            return;
        }

        Vector3 p1 = (firstHand.ThumbTip + firstHand.IndexTip) * 0.5f;
        Vector3 p2 = (secondHand.ThumbTip + secondHand.IndexTip) * 0.5f;

        Vector2 pinchPoint1 = new Vector2(p1.x, p1.y);
        Vector2 pinchPoint2 = new Vector2(p2.x, p2.y);

        float currentDistance = Vector2.Distance(pinchPoint1, pinchPoint2);
        float currentHoriz = Mathf.Abs(pinchPoint1.x - pinchPoint2.x);
        float currentVert = Mathf.Abs(pinchPoint1.y - pinchPoint2.y);

        if (!isStretching)
        {
            startHandDistance = currentDistance;
            startHorizDistance = currentHoriz;
            startVertDistance = currentVert;
            startObjectScale = transform.localScale;
            isStretching = true;
        }

        if (scaleMode == ScaleMode.IndependentXY)
        {
            float hDelta = currentHoriz - startHorizDistance;
            if (Mathf.Abs(hDelta) < distanceDeadZone) hDelta = 0f;

            float vDelta = currentVert - startVertDistance;
            if (Mathf.Abs(vDelta) < distanceDeadZone) vDelta = 0f;

            float xMultiplier = 1f;
            float yMultiplier = 1f;

            if (Mathf.Abs(hDelta) >= Mathf.Abs(vDelta))
            {
                xMultiplier = Mathf.Max(0.1f, 1f + hDelta * widthSensitivity);
            }
            else
            {
                yMultiplier = Mathf.Max(0.1f, 1f + vDelta * heightSensitivity);
            }

            Vector3 proposed = new Vector3(
                startObjectScale.x * xMultiplier,
                startObjectScale.y * yMultiplier,
                startObjectScale.z
            );
            targetScale = ClampScale(proposed);
        }
        else
        {
            float delta = currentDistance - startHandDistance;
            if (Mathf.Abs(delta) < distanceDeadZone) delta = 0f;

            float multiplier = Mathf.Max(0.1f, 1f + delta * uniform3DSensitivity);
            Vector3 proposed = startObjectScale * multiplier;
            targetScale = ClampScale(proposed);
        }

        SmoothToTarget();
    }

    private Vector3 ClampScale(Vector3 scale)
    {
        float minX = initialBaseScale.x * minimumScaleFactor;
        float maxX = initialBaseScale.x * maximumScaleFactor;
        float minY = initialBaseScale.y * minimumScaleFactor;
        float maxY = initialBaseScale.y * maximumScaleFactor;
        float minZ = initialBaseScale.z * minimumScaleFactor;
        float maxZ = initialBaseScale.z * maximumScaleFactor;

        return new Vector3(
            Mathf.Clamp(scale.x, minX, maxX),
            Mathf.Clamp(scale.y, minY, maxY),
            Mathf.Clamp(scale.z, minZ, maxZ)
        );
    }

    private void SmoothToTarget()
    {
        float smoothAmount = 1f - Mathf.Exp(-scaleSmoothingSpeed * Time.deltaTime);
        transform.localScale = Vector3.Lerp(transform.localScale, targetScale, smoothAmount);
    }

    private void StopStretching()
    {
        isStretching = false;
        IsDualPinching = false;
    }

    private void FindTrackingComponents()
    {
        if (handRunner == null)
        {
            handRunner = FindFirstObjectByType<SculptHandLandmarkerRunner>();
        }
    }
}