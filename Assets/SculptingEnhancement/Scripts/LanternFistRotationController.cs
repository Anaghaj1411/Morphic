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
        float yawGain = Mathf.Max(yawPerScreen, 400f);
        float pitchGain = Mathf.Max(pitchPerScreen, 400f);
        yaw += (moveSmoothed.x * 0.25f + moveRight * 0.75f) * yawGain;
        pitch += (moveSmoothed.y * 0.25f + moveUp * 0.75f) * pitchGain;
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

        // Selfie webcam: image-left is the user's right.
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
        if (rotating && originalGestureController != null)
            originalGestureController.SetExternalFistRotationActive(false);
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
