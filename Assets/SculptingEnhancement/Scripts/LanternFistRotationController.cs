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

    public bool suppressDriving;

    [Header("Hold-to-steer")]
    [SerializeField, Range(40f, 400f)] private float turnYawRate = 140f;
    [SerializeField, Range(40f, 400f)] private float turnPitchRate = 140f;
    [SerializeField, Range(0.02f, 0.12f)] private float centerDeadZone = 0.05f;
    [SerializeField, Range(0.12f, 0.45f)] private float fullSteerRange = 0.28f;
    [SerializeField, Min(4f)] private float smoothing = 14f;

    private Transform pivot;
    private Transform originalParent;
    private Vector3 restPosition;
    private Quaternion restRotation = Quaternion.identity;
    private float yaw;
    private float pitch;
    private float yawSpeed;
    private float pitchSpeed;
    private int lockedAxis;
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

        if (suppressDriving)
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
            lockedAxis = 0;
        }

        ResolveSteer(fist, out float steerRight, out float steerUp);
        float targetYaw = steerRight * turnYawRate;
        float targetPitch = steerUp * turnPitchRate;

        float dt = Mathf.Max(Time.unscaledDeltaTime, 0.0001f);
        float blend = 1f - Mathf.Exp(-smoothing * dt);
        yawSpeed = Mathf.Lerp(yawSpeed, targetYaw, blend);
        pitchSpeed = Mathf.Lerp(pitchSpeed, targetPitch, blend);

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
        return hand.FingerCurlRatio <= 1.28f &&
               hand.OtherFingersCurlRatio <= 1.20f &&
               hand.IndexExtensionAngle < 125f;
    }

    private void ResolveSteer(SculptHandFrame fist, out float steerRight, out float steerUp)
    {
        // Selfie webcam: image-left is the user's right. Invert X so a
        // fist held on the right spins the object toward the right.
        float x = 0.5f - fist.PalmCenter.x;
        float y = fist.PalmCenter.y - 0.5f;

        steerRight = ApplyDeadZone(x);
        steerUp = ApplyDeadZone(y);
        float absRight = Mathf.Abs(steerRight);
        float absUp = Mathf.Abs(steerUp);
        if (absRight < 0.001f && absUp < 0.001f)
        {
            lockedAxis = 0;
            return;
        }

        const float stick = 0.72f;
        if (lockedAxis == 1 && absRight >= absUp * stick)
        {
            steerUp = 0f;
            return;
        }
        if (lockedAxis == 2 && absUp >= absRight * stick)
        {
            steerRight = 0f;
            return;
        }

        if (absRight >= absUp)
        {
            lockedAxis = 1;
            steerUp = 0f;
        }
        else
        {
            lockedAxis = 2;
            steerRight = 0f;
        }
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
        lockedAxis = 0;
    }

    private void ApplyRotation()
    {
        if (pivot == null)
            return;

        ResolveViewAxes(out Vector3 yawAxis, out Vector3 pitchAxis);
        pivot.position = restPosition;
        // Fist on the right spins the front of the object toward the right
        // of the screen (negative around camera.up). Fist up nods the
        // front toward the top of the screen (positive around camera.right).
        pivot.rotation =
            Quaternion.AngleAxis(yaw, yawAxis) *
            Quaternion.AngleAxis(pitch, pitchAxis) *
            restRotation;
    }

    private void ResolveViewAxes(out Vector3 yawAxis, out Vector3 pitchAxis)
    {
        Camera cam = ResolveViewCamera();
        if (cam != null)
        {
            yawAxis = cam.transform.up;
            pitchAxis = cam.transform.right;
            if (yawAxis.sqrMagnitude > 0.01f && pitchAxis.sqrMagnitude > 0.01f)
            {
                yawAxis.Normalize();
                pitchAxis.Normalize();
                return;
            }
        }

        yawAxis = Vector3.up;
        pitchAxis = Vector3.right;
    }

    private void EnsurePivot()
    {
        Bind();
        if (pivot != null || lanternRotationTarget == null || lanternRenderer == null)
            return;

        originalParent = lanternRotationTarget.parent;
        restPosition = lanternRenderer.bounds.center;
        pivot = new GameObject("Lantern Fist Rotation Pivot").transform;
        pivot.SetParent(originalParent, false);
        pivot.position = restPosition;
        pivot.rotation = Quaternion.identity;
        pivot.localScale = Vector3.one;
        lanternRotationTarget.SetParent(pivot, true);
        restRotation = Quaternion.identity;
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
