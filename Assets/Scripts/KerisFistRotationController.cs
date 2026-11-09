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
    [SerializeField, Range(40f, 400f)] private float turnYawRate = 140f;
    [SerializeField, Range(40f, 400f)] private float turnPitchRate = 140f;
    [SerializeField, Range(0.02f, 0.12f)] private float centerDeadZone = 0.05f;
    [SerializeField, Range(0.12f, 0.45f)] private float fullSteerRange = 0.28f;
    [SerializeField, Min(4f)] private float smoothing = 14f;

    public bool suppressDriving;

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
        Bind();
        DestroyLegacyAxisPivot();
    }

    private void OnDisable()
    {
        PauseRotation();
    }

    private void OnDestroy()
    {
        RestoreTargetParent();
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
            PauseRotation();
            return;
        }

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

        if (!rotating)
        {
            EnsurePivot();
            rotating = true;
            yawSpeed = 0f;
            pitchSpeed = 0f;
            lockedAxis = 0;
        }

        if (pivot == null)
            return;

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

    private void EnsurePivot()
    {
        Bind();
        DestroyLegacyAxisPivot();
        if (pivot != null || kerisRotationTarget == null)
            return;

        Transform parent = FindManualSpin();
        if (parent == null)
            parent = kerisRotationTarget.parent;
        originalParent = parent;
        restPosition = RendererWorldCenter(
            kerisRenderer != null ? kerisRenderer : kerisRotationTarget.GetComponentInChildren<Renderer>(true));
        pivot = new GameObject("Keris Fist Rotation Pivot").transform;
        PlacePivotAt(pivot, originalParent, restPosition);
        kerisRotationTarget.SetParent(pivot, true);
        restRotation = Quaternion.identity;
    }

    private void RestoreTargetParent()
    {
        if (pivot == null)
            return;

        Transform heldPivot = pivot;
        Transform target = kerisRotationTarget;
        Transform parent = originalParent;
        pivot = null;
        originalParent = null;

        if (target != null && parent != null && target.parent == heldPivot)
            target.SetParent(parent, true);
        else if (target != null && target.parent == heldPivot)
            target.SetParent(FindManualSpin(), true);

        if (heldPivot != null)
            Destroy(heldPivot.gameObject);
    }

    private static Transform FindManualSpin()
    {
        GameObject named = GameObject.Find(ManualSpinName);
        return named != null ? named.transform : null;
    }

    private void DestroyLegacyAxisPivot()
    {
        Transform root = kerisRotationTarget != null ? kerisRotationTarget : transform;
        Transform parent = root.parent;
        if (parent == null || parent.name != PivotName)
            return;

        Transform keep = FindManualSpin();
        root.SetParent(keep, true);
        Destroy(parent.gameObject);
    }

    private static Vector3 RendererWorldCenter(Renderer renderer)
    {
        if (renderer == null)
            return Vector3.zero;

        Transform tf = renderer.transform;
        SkinnedMeshRenderer skin = renderer as SkinnedMeshRenderer;
        if (skin != null && skin.sharedMesh != null)
        {
            Mesh baked = new Mesh { name = "KerisFistRotBounds" };
            skin.BakeMesh(baked);
            baked.RecalculateBounds();
            Vector3 local = baked.bounds.center;
            bool valid = baked.bounds.extents.sqrMagnitude > 0.0000001f;
            UnityEngine.Object.Destroy(baked);
            if (valid)
                return tf.TransformPoint(local);
        }

        MeshFilter filter = renderer.GetComponent<MeshFilter>();
        if (filter != null && filter.sharedMesh != null)
            return tf.TransformPoint(filter.sharedMesh.bounds.center);

        return tf.TransformPoint(renderer.localBounds.center);
    }

    private static void PlacePivotAt(Transform pivot, Transform parent, Vector3 worldCenter)
    {
        pivot.SetParent(null, false);
        pivot.position = worldCenter;
        pivot.rotation = Quaternion.identity;
        pivot.localScale = Vector3.one;
        if (parent == null)
            return;

        pivot.SetParent(parent, true);
        Vector3 lossy = parent.lossyScale;
        pivot.localScale = new Vector3(
            ReciprocalScale(lossy.x),
            ReciprocalScale(lossy.y),
            ReciprocalScale(lossy.z));
        pivot.position = worldCenter;
        pivot.rotation = Quaternion.identity;
    }

    private static float ReciprocalScale(float value)
    {
        return Mathf.Abs(value) < 0.0001f ? 1f : 1f / value;
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
        return hand.IndexExtensionAngle < 125f &&
               hand.FingerCurlRatio <= 1.28f &&
               hand.OtherFingersCurlRatio <= 1.20f;
    }

    private void ResolveSteer(SculptHandFrame fist, out float steerRight, out float steerUp)
    {
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
}
