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

    [Header("Rotation Axis")]
    [Tooltip("Horizontal world-space axis to roll around. Use (1, 0, 0) or (0, 0, 1), depending on the teapot orientation.")]
    [SerializeField] private Vector3 rotationAxis = new Vector3(0f, 0f, 1f);

    [Header("Hold-to-steer")]
    [SerializeField, Range(40f, 400f)] private float turnYawRate = 110f;
    [SerializeField, Range(0.02f, 0.12f)] private float centerDeadZone = 0.06f;
    [SerializeField, Range(0.12f, 0.4f)] private float fullSteerRange = 0.22f;
    [SerializeField, Min(4f)] private float smoothing = 16f;

    private Transform pivot;
    private Transform originalParent;
    private Vector3 restPosition;
    private Vector3 fixedAxis = Vector3.right;
    private Quaternion restRotation = Quaternion.identity;
    private float yaw;
    private float yawSpeed;
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
        RestoreTargetParent();
    }

    public void AbandonCopiedPivot()
    {
        pivot = null;
        originalParent = null;
        teapotRotationTarget = null;
    }

    private void RestoreTargetParent()
    {
        if (pivot == null)
            return;

        Transform heldPivot = pivot;
        Transform target = teapotRotationTarget;
        Transform parent = originalParent;
        pivot = null;
        originalParent = null;

        if (target != null &&
            parent != null &&
            target.parent == heldPivot)
        {
            target.SetParent(parent, true);
        }

        if (heldPivot != null)
            Destroy(heldPivot.gameObject);
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
        }

        // Fist left/right. Axis is locked when the fist first closes.
        float steerLeft = ApplyDeadZone(0.5f - fist.PalmCenter.x);
        float targetYaw = steerLeft * turnYawRate;

        float dt = Mathf.Max(Time.unscaledDeltaTime, 0.0001f);
        float blend = 1f - Mathf.Exp(-smoothing * dt);
        yawSpeed = Mathf.Lerp(yawSpeed, targetYaw, blend);
        yaw += yawSpeed * dt;
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
    }

    private void ApplyRotation()
    {
        if (pivot == null)
            return;

        pivot.position = restPosition;
        pivot.rotation = Quaternion.AngleAxis(yaw, fixedAxis) * restRotation;
    }

    private Vector3 ResolveViewRight()
    {
        Camera cam = ResolveViewCamera();
        Vector3 viewForward = cam != null ? cam.transform.forward : Vector3.forward;
        viewForward = Vector3.ProjectOnPlane(viewForward, Vector3.up);
        if (viewForward.sqrMagnitude < 0.01f)
            viewForward = Vector3.forward;
        Vector3 viewRight = Vector3.Cross(viewForward.normalized, Vector3.up);
        if (viewRight.sqrMagnitude < 0.01f)
            viewRight = Vector3.right;
        return viewRight.normalized;
    }

    private void EnsurePivot()
    {
        Bind();
        if (pivot != null || teapotRotationTarget == null || teapotRenderer == null)
            return;

        originalParent = teapotRotationTarget.parent;
        restPosition = teapotRenderer.bounds.center;
        fixedAxis = rotationAxis.sqrMagnitude > 0.0001f ? rotationAxis.normalized : Vector3.right;
        pivot = new GameObject("Teapot Fist Rotation Pivot").transform;
        pivot.SetParent(originalParent, false);
        pivot.position = restPosition;
        pivot.rotation = Quaternion.identity;
        pivot.localScale = Vector3.one;
        teapotRotationTarget.SetParent(pivot, true);
        restRotation = Quaternion.identity;
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
            teapotRotationTarget = ResolveTeapotRoot(teapotRenderer.transform);
        ResolveViewCamera();
    }

    private static Transform ResolveTeapotRoot(Transform start)
    {
        Transform current = start;
        while (current != null)
        {
            string objectName = current.name;
            if (objectName == "Teapot_Updated")
                return current;
            current = current.parent;
        }

        return start;
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
