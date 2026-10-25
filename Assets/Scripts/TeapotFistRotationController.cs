using Mediapipe.Unity.Sample.HandLandmarkDetection;
using UnityEngine;

/// <summary>
/// Teapot-only copy of LanternFistRotationController. Same hold-to-steer,
/// axes, and pivot. It does not touch the lantern mesh or controllers.
/// </summary>
[DefaultExecutionOrder(240)]
public sealed class TeapotFistRotationController : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private SculptHandLandmarkerRunner handRunner;
    [SerializeField] private SkinnedMeshRenderer teapotRenderer;
    [SerializeField] private Transform teapotRotationTarget;
    [SerializeField] private TeapotGestureController teapotGestures;
    [SerializeField] private Camera viewCamera;

    [Header("Hold-to-steer")]
    [SerializeField, Range(40f, 400f)] private float turnYawRate = 95f;
    [SerializeField, Range(40f, 400f)] private float turnPitchRate = 76f;
    [SerializeField, Range(0.02f, 0.12f)] private float centerDeadZone = 0.06f;
    [SerializeField, Range(0.12f, 0.4f)] private float fullSteerRange = 0.22f;
    [SerializeField, Min(4f)] private float smoothing = 16f;

    private Transform pivot;
    private Transform originalParent;
    private Quaternion restRotation = Quaternion.identity;
    private float yaw;
    private float pitch;
    private float yawSpeed;
    private float pitchSpeed;
    private bool rotating;

    private void Awake()
    {
        Bind();
    }

    private void OnDisable()
    {
        PauseRotation();
    }

    private void OnDestroy()
    {
        if (pivot == null)
            return;
        if (teapotRotationTarget != null && teapotRotationTarget.parent == pivot)
            teapotRotationTarget.SetParent(originalParent, true);
        Destroy(pivot.gameObject);
    }

    public void BindFrom(SculptHandLandmarkerRunner runner, SkinnedMeshRenderer renderer,
        TeapotGestureController gestures)
    {
        if (handRunner == null)
            handRunner = runner;
        if (teapotRenderer == null)
            teapotRenderer = renderer;
        if (teapotGestures == null)
            teapotGestures = gestures;
        Bind();
    }

    private void Update()
    {
        if (handRunner == null || teapotRenderer == null)
            return;

        if (teapotGestures != null && teapotGestures.suppressDriving)
        {
            PauseRotation();
            return;
        }

        bool hasTwo = handRunner.TryGetLatestTwoHands(out SculptHandFrame first, out SculptHandFrame second);
        bool hasOne = hasTwo || handRunner.TryGetLatestHand(out first);
        if (hasTwo && LooksClosed(first) && LooksClosed(second))
        {
            PauseRotation();
            return;
        }

        if (!TryGetClosedHand(hasOne, hasTwo, first, second, out SculptHandFrame fist))
        {
            PauseRotation();
            return;
        }

        if (teapotGestures != null)
            teapotGestures.SetExternalFistRotationActive(true);

        if (!rotating)
        {
            EnsurePivot();
            rotating = true;
            yawSpeed = 0f;
            pitchSpeed = 0f;
        }

        // Where the fist sits on screen, not how it is moving.
        // Only one axis at a time: left/right turns, or up/down tilts.
        float steerLeft = ApplyDeadZone(0.5f - fist.PalmCenter.x);
        float steerUp = ApplyDeadZone(fist.PalmCenter.y - 0.5f);
        float targetYaw = 0f;
        float targetPitch = 0f;
        bool steerHorizontal = Mathf.Abs(steerLeft) > Mathf.Abs(steerUp);
        bool steerVertical = Mathf.Abs(steerUp) > Mathf.Abs(steerLeft);
        if (steerHorizontal)
            targetYaw = -steerLeft * turnYawRate;
        else if (steerVertical)
            targetPitch = -steerUp * turnPitchRate;

        float dt = Mathf.Max(Time.unscaledDeltaTime, 0.0001f);
        float blend = 1f - Mathf.Exp(-smoothing * dt);
        if (steerVertical)
        {
            // Cut leftover yaw immediately so up/down cannot keep turning around world-up.
            yawSpeed = 0f;
            pitchSpeed = Mathf.Lerp(pitchSpeed, targetPitch, blend);
        }
        else if (steerHorizontal)
        {
            pitchSpeed = 0f;
            yawSpeed = Mathf.Lerp(yawSpeed, targetYaw, blend);
        }
        else
        {
            yawSpeed = Mathf.Lerp(yawSpeed, 0f, blend);
            pitchSpeed = Mathf.Lerp(pitchSpeed, 0f, blend);
        }

        yaw += yawSpeed * dt;
        pitch += pitchSpeed * dt;
        ApplyRotation();
    }

    private bool TryGetClosedHand(
        bool hasOne,
        bool hasTwo,
        SculptHandFrame first,
        SculptHandFrame second,
        out SculptHandFrame fist)
    {
        fist = first;
        if (!hasOne)
            return false;
        if (LooksClosed(first))
        {
            fist = first;
            return true;
        }

        if (hasTwo && LooksClosed(second))
        {
            fist = second;
            return true;
        }

        return false;
    }

    private static bool LooksClosed(SculptHandFrame hand)
    {
        return hand.FingerCurlRatio <= 1.45f &&
               hand.OtherFingersCurlRatio <= 1.45f &&
               hand.IndexExtensionAngle < 145f;
    }

    private float ApplyDeadZone(float value)
    {
        float magnitude = Mathf.Abs(value);
        if (magnitude <= centerDeadZone)
            return 0f;

        float range = Mathf.Max(fullSteerRange, centerDeadZone + 0.02f);
        return Mathf.Sign(value) * Mathf.Clamp01((magnitude - centerDeadZone) / (range - centerDeadZone));
    }

    private void PauseRotation()
    {
        if (rotating && teapotGestures != null)
            teapotGestures.SetExternalFistRotationActive(false);
        rotating = false;
        yawSpeed = 0f;
        pitchSpeed = 0f;
    }

    private void ApplyRotation()
    {
        if (pivot == null)
            return;

        ResolveAxes(out Vector3 yawAxis, out Vector3 pitchAxis);
        pivot.rotation =
            Quaternion.AngleAxis(yaw, yawAxis) *
            Quaternion.AngleAxis(pitch, pitchAxis) *
            restRotation;
    }

    private void ResolveAxes(out Vector3 yawAxis, out Vector3 pitchAxis)
    {
        Camera cam = ResolveViewCamera();
        Vector3 viewForward = cam != null ? cam.transform.forward : Vector3.forward;
        viewForward = Vector3.ProjectOnPlane(viewForward, Vector3.up);
        if (viewForward.sqrMagnitude < 0.01f)
            viewForward = Vector3.forward;
        Vector3 viewRight = Vector3.Cross(viewForward.normalized, Vector3.up);
        if (viewRight.sqrMagnitude < 0.01f)
            viewRight = Vector3.right;

        // Same mapping as the lantern: left/right turns around view-right,
        // up/down spins around world-up.
        yawAxis = viewRight.normalized;
        pitchAxis = Vector3.up;
    }

    private void EnsurePivot()
    {
        Bind();
        if (pivot != null || teapotRotationTarget == null || teapotRenderer == null)
            return;

        originalParent = teapotRotationTarget.parent;
        pivot = new GameObject("Teapot Fist Rotation Pivot").transform;
        pivot.SetParent(originalParent, false);
        pivot.position = teapotRenderer.transform.TransformPoint(teapotRenderer.localBounds.center);
        pivot.rotation = Quaternion.identity;
        pivot.localScale = Vector3.one;
        teapotRotationTarget.SetParent(pivot, true);
        restRotation = pivot.rotation;
    }

    private void Bind()
    {
        if (teapotGestures == null)
            teapotGestures = GetComponent<TeapotGestureController>();
        if (handRunner == null && teapotGestures != null)
            handRunner = teapotGestures.HandRunner;
        if (handRunner == null)
            handRunner = FindFirstObjectByType<SculptHandLandmarkerRunner>(FindObjectsInactive.Include);
        if (teapotRenderer == null && teapotGestures != null)
            teapotRenderer = teapotGestures.TeapotRenderer;
        if (teapotRenderer == null)
            teapotRenderer = GetComponentInChildren<SkinnedMeshRenderer>(true);
        if (teapotRotationTarget == null && teapotRenderer != null)
            teapotRotationTarget = teapotRenderer.transform.root;
        ResolveViewCamera();
    }

    private Camera ResolveViewCamera()
    {
        if (viewCamera != null)
            return viewCamera;

        MainMenuStartTransition transition =
            FindFirstObjectByType<MainMenuStartTransition>(FindObjectsInactive.Include);
        if (transition != null && transition.ViewCamera != null)
            viewCamera = transition.ViewCamera;
        if (viewCamera == null)
        {
            GameObject named = GameObject.Find("MainMenuViewCamera");
            if (named != null)
                viewCamera = named.GetComponent<Camera>();
        }
        if (viewCamera == null)
            viewCamera = Camera.main;
        if (viewCamera == null)
        {
            Camera[] cameras = FindObjectsByType<Camera>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            if (cameras != null && cameras.Length > 0)
                viewCamera = cameras[0];
        }
        return viewCamera;
    }
}
