using Mediapipe.Unity.Sample.HandLandmarkDetection;
using UnityEngine;

[DefaultExecutionOrder(110)]
public sealed class LanternFistRotationController : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private SculptHandLandmarkerRunner handRunner;
    [SerializeField] private SkinnedMeshRenderer lanternRenderer;
    [SerializeField] private Transform lanternRotationTarget;
    [SerializeField] private TaiwanLanternGestureController originalGestureController;
    [SerializeField] private Camera viewCamera;

    [Header("Hold-to-steer")]
    [SerializeField, Range(40f, 400f)] private float turnYawRate = 110f;
    [SerializeField, Range(40f, 400f)] private float turnPitchRate = 110f;
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
        if (GetComponentInParent<LanternShelfController>(true) != null)
        {
            enabled = false;
            return;
        }

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
        if (lanternRotationTarget != null && lanternRotationTarget.parent == pivot)
            lanternRotationTarget.SetParent(originalParent, true);
        Destroy(pivot.gameObject);
    }

    public void BindFrom(SculptHandLandmarkerRunner runner, SkinnedMeshRenderer renderer,
        TaiwanLanternGestureController original)
    {
        if (handRunner == null)
            handRunner = runner;
        if (lanternRenderer == null)
            lanternRenderer = renderer;
        if (originalGestureController == null)
            originalGestureController = original;
        Bind();
    }

    private void Update()
    {
        if (handRunner == null || lanternRenderer == null)
            return;

        SculptingEnhancementIndexGestureController index =
            GetComponent<SculptingEnhancementIndexGestureController>();
        if (index != null && index.suppressDriving)
        {
            PauseRotation();
            return;
        }

        bool hasTwo = handRunner.TryGetLatestTwoHands(out SculptHandFrame first, out SculptHandFrame second);
        bool hasOne = hasTwo || handRunner.TryGetLatestHand(out first);
        // Two hands are tassel / width / bamboo. Never start a one-fist
        // turn while the second hand is coming in.
        if (hasTwo)
        {
            PauseRotation();
            return;
        }

        if (!TryGetClosedHand(hasOne, hasTwo, first, second, out SculptHandFrame fist))
        {
            PauseRotation();
            return;
        }

        if (originalGestureController != null)
            originalGestureController.SetExternalFistRotationActive(true);

        if (!rotating)
        {
            EnsurePivot();
            rotating = true;
            yawSpeed = 0f;
            pitchSpeed = 0f;
        }

        // Left/right only. Fist height does not steer.
        float steerLeft = ApplyDeadZone(0.5f - fist.PalmCenter.x);
        float targetYaw = -steerLeft * turnYawRate;

        float dt = Mathf.Max(Time.unscaledDeltaTime, 0.0001f);
        float blend = 1f - Mathf.Exp(-smoothing * dt);
        yawSpeed = Mathf.Lerp(yawSpeed, targetYaw, blend);
        pitch = 0f;
        pitchSpeed = 0f;

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
        if (rotating && originalGestureController != null)
            originalGestureController.SetExternalFistRotationActive(false);
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

        // Stay in place: tip left/right and nod up/down around the center.
        yawAxis = viewForward.normalized;
        pitchAxis = viewRight.normalized;
    }

    private void EnsurePivot()
    {
        Bind();
        if (pivot != null || lanternRotationTarget == null || lanternRenderer == null)
            return;

        originalParent = lanternRotationTarget.parent;
        pivot = new GameObject("Lantern Fist Rotation Pivot").transform;
        pivot.SetParent(originalParent, false);
        pivot.position = lanternRenderer.bounds.center;
        pivot.rotation = Quaternion.identity;
        pivot.localScale = Vector3.one;
        lanternRotationTarget.SetParent(pivot, true);
        restRotation = pivot.rotation;
    }

    private void Bind()
    {
        if (handRunner == null)
            handRunner = FindFirstObjectByType<SculptHandLandmarkerRunner>(FindObjectsInactive.Include);
        if (originalGestureController == null)
            originalGestureController =
                FindFirstObjectByType<TaiwanLanternGestureController>(FindObjectsInactive.Include);
        if (lanternRenderer == null)
        {
            SkinnedMeshRenderer[] renderers = FindObjectsByType<SkinnedMeshRenderer>(
                FindObjectsInactive.Include, FindObjectsSortMode.None);
            for (int i = 0; i < renderers.Length; i++)
            {
                string n = renderers[i].gameObject.name.ToUpperInvariant();
                if (n.Contains("LANTERN") && renderers[i].sharedMesh != null &&
                    renderers[i].sharedMesh.blendShapeCount > 0)
                {
                    lanternRenderer = renderers[i];
                    break;
                }
            }
        }

        if (lanternRotationTarget == null && lanternRenderer != null)
            lanternRotationTarget = ResolveLanternRoot(lanternRenderer.transform);
        ResolveViewCamera();
    }

    private static Transform ResolveLanternRoot(Transform start)
    {
        Transform current = start;
        while (current != null)
        {
            string objectName = current.name;
            if (objectName == "FinalLantern" ||
                objectName == "TaiwanLantern_Test" ||
                objectName == "Updatedlantern")
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
