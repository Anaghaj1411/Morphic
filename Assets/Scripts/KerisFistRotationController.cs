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

    [Header("Hold-to-turn")]
    [SerializeField, Range(40f, 720f)] private float yawPerScreen = 400f;
    [SerializeField, Range(40f, 720f)] private float pitchPerScreen = 400f;
    [SerializeField, Range(0.001f, 0.02f)] private float moveDeadZone = 0.002f;
    [SerializeField, Min(4f)] private float smoothing = 18f;

    public bool suppressDriving;

    private Transform pivot;
    private Transform originalParent;
    private Vector3 restPosition;
    private Quaternion restRotation = Quaternion.identity;
    private float yaw;
    private float pitch;
    private Vector2 lastPalm;
    private Vector2 moveSmoothed;
    private bool hasPalm;
    private int lockedAxis;
    private bool rotating;

    private void Awake()
    {
        Bind();
        DestroyLegacyAxisPivot();
        ApplySmoothTurn();
    }

    private void ApplySmoothTurn()
    {
        yawPerScreen = 275f;
        pitchPerScreen = 220f;
        moveDeadZone = 0.0038f;
        smoothing = 8f;
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
            lastPalm = fist.PalmCenter;
            hasPalm = true;
            moveSmoothed = Vector2.zero;
            lockedAxis = 0;
            ApplyRotation();
            return;
        }

        if (pivot == null)
            return;

        ResolveMove(fist, out float moveRight, out float moveUp);
        float dt = Mathf.Max(Time.unscaledDeltaTime, 0.0001f);
        float blend = 1f - Mathf.Exp(-smoothing * dt);
        moveSmoothed.x = Mathf.Lerp(moveSmoothed.x, moveRight, blend);
        moveSmoothed.y = Mathf.Lerp(moveSmoothed.y, moveUp, blend);
        yaw += moveSmoothed.x * yawPerScreen;
        pitch += moveSmoothed.y * pitchPerScreen;
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
        return hand.IndexExtensionAngle < 110f &&
               hand.FingerCurlRatio <= 1.14f &&
               hand.OtherFingersCurlRatio <= 1.10f &&
               hand.IndexTipPalmRatio <= 1.08f;
    }

    private void ResolveMove(SculptHandFrame fist, out float moveRight, out float moveUp)
    {
        Vector2 palm = fist.PalmCenter;
        if (!hasPalm)
        {
            lastPalm = palm;
            hasPalm = true;
            moveRight = 0f;
            moveUp = 0f;
            return;
        }

        moveRight = lastPalm.x - palm.x;
        moveUp = palm.y - lastPalm.y;
        lastPalm = palm;

        if (Mathf.Abs(moveRight) < moveDeadZone)
            moveRight = 0f;
        if (Mathf.Abs(moveUp) < moveDeadZone)
            moveUp = 0f;

        float absRight = Mathf.Abs(moveRight);
        float absUp = Mathf.Abs(moveUp);
        if (absRight < 0.0001f && absUp < 0.0001f)
        {
            lockedAxis = 0;
            return;
        }

        const float stick = 0.72f;
        if (lockedAxis == 1 && absRight >= absUp * stick)
        {
            moveUp = 0f;
            return;
        }
        if (lockedAxis == 2 && absUp >= absRight * stick)
        {
            moveRight = 0f;
            return;
        }

        if (absRight >= absUp)
        {
            lockedAxis = 1;
            moveUp = 0f;
        }
        else
        {
            lockedAxis = 2;
            moveRight = 0f;
        }
    }

    public void ResetToRestPose()
    {
        PauseRotation();
        yaw = 0f;
        pitch = 0f;
        ApplyRotation();
    }

    private void PauseRotation()
    {
        rotating = false;
        hasPalm = false;
        moveSmoothed = Vector2.zero;
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
        if (TryScreenAxes(ResolveViewCamera(), out yawAxis, out pitchAxis))
            return;

        yawAxis = Vector3.up;
        pitchAxis = Vector3.left;
    }

    private static bool TryScreenAxes(Camera cam, out Vector3 yawAxis, out Vector3 pitchAxis)
    {
        yawAxis = Vector3.up;
        pitchAxis = Vector3.left;
        if (cam == null)
            return false;

        Vector3 origin = cam.ViewportToWorldPoint(new Vector3(0.5f, 0.5f, 1f));
        Vector3 screenRight = cam.ViewportToWorldPoint(new Vector3(1f, 0.5f, 1f)) - origin;
        Vector3 screenUp = cam.ViewportToWorldPoint(new Vector3(0.5f, 1f, 1f)) - origin;
        if (screenRight.sqrMagnitude < 0.0001f || screenUp.sqrMagnitude < 0.0001f)
            return false;

        yawAxis = screenUp.normalized;
        pitchAxis = -screenRight.normalized;
        return true;
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
