using Mediapipe.Unity.Sample.HandLandmarkDetection;
using UnityEngine;

/// <summary>
/// Keris-only hold-to-steer. Spins in place around the blade center.
/// Does not touch lantern or teapot.
/// </summary>
[DefaultExecutionOrder(240)]
public sealed class KerisFistRotationController : MonoBehaviour
{
    private const string PivotName = "KerisAxisPivot";
    private const string ManualSpinName = "KerisSpin";

    [Header("References")]
    [SerializeField] private SculptHandLandmarkerRunner handRunner;
    [SerializeField] private Renderer kerisRenderer;
    [SerializeField] private Transform kerisRotationTarget;
    [SerializeField] private Camera viewCamera;

    [Header("Hold-to-steer")]
    [SerializeField, Range(40f, 400f)] private float turnYawRate = 110f;
    [SerializeField, Range(40f, 400f)] private float turnSideRate = 110f;
    [SerializeField, Range(0.02f, 0.12f)] private float centerDeadZone = 0.06f;
    [SerializeField, Range(0.12f, 0.4f)] private float fullSteerRange = 0.22f;
    [SerializeField, Min(4f)] private float smoothing = 16f;

    public bool suppressDriving;

    private Transform spinRoot;
    private float yawSpeed;
    private float sideSpeed;
    private bool rotating;

    private void Awake()
    {
        Bind();
        DestroyRuntimePivots();
    }

    private void OnDisable()
    {
        PauseRotation();
    }

    public void BindFrom(SculptHandLandmarkerRunner runner, Renderer renderer)
    {
        if (handRunner == null)
            handRunner = runner;
        if (kerisRenderer == null)
            kerisRenderer = renderer;
        Bind();
    }

    private void Update()
    {
        if (handRunner == null || kerisRotationTarget == null)
            return;

        if (suppressDriving)
        {
            PauseRotation();
            return;
        }

        VertexSculptController sculpt = GetComponentInChildren<VertexSculptController>(true);
        if (sculpt != null && sculpt.IsPinching)
        {
            PauseRotation();
            return;
        }

        bool hasTwo = handRunner.TryGetLatestTwoHands(out SculptHandFrame first, out SculptHandFrame second);
        bool hasOne = hasTwo || handRunner.TryGetLatestHand(out first);

        KerisGestureController gestures = GetComponentInChildren<KerisGestureController>(true);
        if (gestures != null && gestures.IsDrivingSculpt)
        {
            // Allow rotation ONLY while driving handle deformations (two fists)
            if (!gestures.IsDrivingHandle)
            {
                PauseRotation();
                return;
            }
        }

        // Remove the two-fist lockout to allow rotation while growing the handle
        if (!TryGetClosedHand(hasOne, hasTwo, first, second, out SculptHandFrame fist))
        {
            PauseRotation();
            return;
        }

        if (!rotating)
        {
            EnsureSpinRoot();
            rotating = true;
            yawSpeed = 0f;
            sideSpeed = 0f;
        }

        if (spinRoot == null)
            return;

        float steerLeftRight = ApplyDeadZone(0.5f - fist.PalmCenter.x);
        float steerUpDown = ApplyDeadZone(0.5f - fist.PalmCenter.y);
        float targetYaw = steerUpDown * turnYawRate;
        float targetSide = steerLeftRight * turnSideRate;

        float dt = Mathf.Max(Time.unscaledDeltaTime, 0.0001f);
        float blend = 1f - Mathf.Exp(-smoothing * dt);
        yawSpeed = Mathf.Lerp(yawSpeed, targetYaw, blend);
        sideSpeed = Mathf.Lerp(sideSpeed, targetSide, blend);
        spinRoot.Rotate(Vector3.up, yawSpeed * dt, Space.World);
        spinRoot.Rotate(ResolveViewRight(), sideSpeed * dt, Space.World);
    }

    private void EnsureSpinRoot()
    {
        Bind();
        DestroyRuntimePivots();
        if (kerisRotationTarget == null)
            return;

        Transform manual = FindManualSpin();
        if (manual != null)
        {
            if (kerisRotationTarget.parent != manual)
                kerisRotationTarget.SetParent(manual, true);
            spinRoot = manual;
            return;
        }

        spinRoot = kerisRotationTarget;
    }

    private static Transform FindManualSpin()
    {
        GameObject named = GameObject.Find(ManualSpinName);
        return named != null ? named.transform : null;
    }

    private void DestroyRuntimePivots()
    {
        Transform root = kerisRotationTarget != null ? kerisRotationTarget : transform;
        Transform parent = root.parent;
        if (parent != null && parent.name == PivotName)
        {
            Transform keep = FindManualSpin();
            root.SetParent(keep, true);
            Destroy(parent.gameObject);
        }

        GameObject leftover = GameObject.Find("Keris Fist Rotation Pivot");
        if (leftover != null)
        {
            Transform held = leftover.transform;
            Transform keep = FindManualSpin();
            for (int i = held.childCount - 1; i >= 0; i--)
                held.GetChild(i).SetParent(keep, true);
            Destroy(leftover);
        }
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
        // A closed hand (fist) for rotation should have a curled index finger (low extension angle).
        return hand.IndexExtensionAngle < 125f &&
               hand.FingerCurlRatio <= 1.45f &&
               hand.OtherFingersCurlRatio <= 1.45f;
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
        rotating = false;
        yawSpeed = 0f;
        sideSpeed = 0f;
    }

    private void Bind()
    {
        if (handRunner == null)
            handRunner = FindFirstObjectByType<SculptHandLandmarkerRunner>(FindObjectsInactive.Include);
        if (kerisRenderer == null)
            kerisRenderer = GetComponentInChildren<SkinnedMeshRenderer>(true);
        if (kerisRenderer == null)
            kerisRenderer = GetComponentInChildren<MeshRenderer>(true);
        if (kerisRotationTarget == null)
            kerisRotationTarget = ResolveKerisRoot(transform);
        ResolveViewCamera();
    }

    private static Transform ResolveKerisRoot(Transform start)
    {
        Transform current = start;
        Transform named = null;
        while (current != null)
        {
            string objectName = current.name;
            if (objectName == "Keris_Updated" || objectName == "Keris")
                named = current;
            current = current.parent;
        }

        return named != null ? named : start;
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
            viewCamera = Camera.main;
        return viewCamera;
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
}
