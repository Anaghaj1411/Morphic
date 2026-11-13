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

    public bool suppressDriving;

    [Header("Hold-to-turn")]
    [SerializeField, Range(40f, 720f)] private float yawPerScreen = 400f;
    [SerializeField, Range(40f, 720f)] private float pitchPerScreen = 400f;
    [SerializeField, Range(0.001f, 0.02f)] private float moveDeadZone = 0.002f;
    [SerializeField, Min(4f)] private float smoothing = 18f;

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

        if (suppressDriving)
        {
            PauseRotation();
            return;
        }

        bool hasTwo = handRunner.TryGetLatestTwoHands(out SculptHandFrame first, out SculptHandFrame second);
        bool hasOne = hasTwo || handRunner.TryGetLatestHand(out first);
        // Two hands are width / height / middle / handle. Never start a
        // one-fist turn while the second hand is in view.
        if (hasTwo)
        {
            PauseRotation();
            return;
        }

        VertexSculptController sculpt = teapotRenderer != null
            ? teapotRenderer.GetComponent<VertexSculptController>()
            : null;
        if (sculpt != null && sculpt.isActiveAndEnabled && sculpt.IsPinching)
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
            lastPalm = fist.PalmCenter;
            hasPalm = true;
            moveSmoothed = Vector2.zero;
            lockedAxis = 0;
            ApplyRotation();
            return;
        }

        ResolveMove(fist, out float moveRight, out float moveUp);
        float dt = Mathf.Max(Time.unscaledDeltaTime, 0.0001f);
        float blend = 1f - Mathf.Exp(-smoothing * dt);
        moveSmoothed.x = Mathf.Lerp(moveSmoothed.x, moveRight, blend);
        moveSmoothed.y = Mathf.Lerp(moveSmoothed.y, moveUp, blend);
        yaw += moveSmoothed.x * yawPerScreen;
        pitch += moveSmoothed.y * pitchPerScreen;
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
        return hand.FingerCurlRatio <= 1.14f &&
               hand.OtherFingersCurlRatio <= 1.10f &&
               hand.IndexTipPalmRatio <= 1.08f &&
               hand.IndexExtensionAngle < 110f;
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
        if (rotating && teapotGestures != null)
            teapotGestures.SetExternalFistRotationActive(false);
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

    private void EnsurePivot()
    {
        Bind();
        if (pivot != null || teapotRotationTarget == null || teapotRenderer == null)
            return;

        originalParent = teapotRotationTarget.parent;
        restPosition = RendererWorldCenter(teapotRenderer);
        pivot = new GameObject("Teapot Fist Rotation Pivot").transform;
        PlacePivotAt(pivot, originalParent, restPosition);
        teapotRotationTarget.SetParent(pivot, true);
        restRotation = Quaternion.identity;
    }

    private static Vector3 RendererWorldCenter(Renderer renderer)
    {
        if (renderer == null)
            return Vector3.zero;

        Transform tf = renderer.transform;
        SkinnedMeshRenderer skin = renderer as SkinnedMeshRenderer;
        if (skin != null && skin.sharedMesh != null)
        {
            Mesh baked = new Mesh { name = "TeapotFistRotBounds" };
            skin.BakeMesh(baked);
            baked.RecalculateBounds();
            Vector3 local = baked.bounds.center;
            bool valid = baked.bounds.extents.sqrMagnitude > 0.0000001f;
            UnityEngine.Object.Destroy(baked);
            if (valid)
                return tf.TransformPoint(local);
        }

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
