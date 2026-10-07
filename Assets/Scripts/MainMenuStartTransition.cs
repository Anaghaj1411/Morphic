using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class MainMenuStartTransition : MonoBehaviour
{
    [Header("Camera")]
    [Tooltip("Camera that moves. Falls back to Camera.main.")]
    [SerializeField] private Camera viewCamera;

    [Header("Gameplay UI")]
    [Tooltip("Gameplay UI that appears after the START camera transition finishes.")]
    [SerializeField] private GameObject gameplayUI;

    [Header("Pottery Studio Camera")]

[Tooltip("Existing camera inside the pottery environment. Its world position and rotation will be used as the pottery view.")]
[SerializeField] private Transform potteryReferenceCamera;





[Tooltip("Optional manual target. The automatic system is used when this is empty.")]
[SerializeField] private Transform potteryStudioCameraTarget;

[Tooltip("How long the camera takes to move into the pottery studio.")]
[SerializeField] private float potteryStudioMoveDuration = 1.5f;

[Tooltip("How far the camera stays from the pottery studio look-at point.")]
[SerializeField] private float potteryCameraDistance = 6f;

[Tooltip("Moves the camera sideways relative to the approach direction.")]
[SerializeField] private float potteryCameraSideOffset = 0f;

[Tooltip("Moves the camera vertically.")]
[SerializeField] private float potteryCameraHeightOffset = 0f;

[Tooltip("Moves the point the camera looks at vertically.")]
[SerializeField] private float potteryLookAtHeight = 1.2f;

private bool isMovingToPotteryStudio;

    public event Action PotteryStudioTransitionCompleted;

    [Header("Shops")]
    [Tooltip("Optional. Drag every parent object that contains a shop (pottery, restaurant, signs, fences...). Each root is measured together with its children.")]
    [SerializeField] private Transform[] shopRoots;

    [Tooltip("Drag the Pottery Studio object here. Optional if the name search works.")]
    [SerializeField] private Transform potteryOverride;

    [Tooltip("Drag the Restaurant object here. Strongly recommended: the FBX shops are unnamed, so the name search cannot find them.")]
    [SerializeField] private Transform restaurantOverride;

    [Tooltip("Name fragment used to find the Pottery Studio.")]
    [SerializeField] private string potteryName = "Pottery";

    [Tooltip("Name fragment used to find the Restaurant.")]
    [SerializeField] private string restaurantName = "Restaurant";

    [Header("Timing")]
    [SerializeField] private float fadeDuration = 0.45f;
    [SerializeField] private float moveDuration = 1.6f;

    [Header("Framing")]
    [Tooltip("Automatically group the nearby scenery into the two shopfronts, so the camera frames both without any manual wiring.")]
    [SerializeField] private bool autoDetectShops = true;

    [Tooltip("How much empty margin to leave around the two shops, 0-0.4.")]
    [Range(0f, 0.4f)]
    [SerializeField] private float framingPadding = 0.12f;

    [Tooltip("Aim this far above the shop roots so buildings fill the frame.")]
    [SerializeField] private float framingHeight = 1.5f;

    [Tooltip("Never place the camera closer than this.")]
    [SerializeField] private float minimumDistance = 4f;

    [Tooltip("Extra pull-back multiplier on the solved framing distance. Raise this if the shops still overflow the frame.")]
    [Range(0.5f, 3f)]
    [SerializeField] private float distanceMultiplier = 1.35f;

    [Tooltip("Shifts the final framing sideways along the camera's own right axis. Positive moves the camera right, which pushes the view left. Use this to centre the pair in frame.")]
    [Range(-10f, 10f)]
    [SerializeField] private float lateralOffset = 0f;

    [Tooltip("Width of the street gap that separates the two shopfronts, in world units. Lower this if the two buildings merge into one group.")]
    [Range(0.5f, 12f)]
    [SerializeField] private float autoDetectGap = 3f;

    [Tooltip("Distance used when the shops cannot be found.")]
    [SerializeField] private float fallbackDistance = 8f;

    [Tooltip("Limits how far the camera may tilt up or down, in degrees. Keeps the horizon level and the framing stable.")]
    [Range(10f, 80f)]
    [SerializeField] private float maximumPitch = 55f;

    [Header("Transition Target")]
    [Tooltip("ON = fly to the exact Target Pose below. OFF = solve the framing automatically from the shop bounds. Turn this OFF only if the Shop Roots are wired up correctly.")]
    [SerializeField] private bool useExplicitTarget = true;

    [Tooltip("Where the camera flies to when START is pressed. Frame the shot in the Scene view, then use the component context menu 'Capture Current View As Target' to fill these in.")]
    [SerializeField] private Vector3 targetPosition;

    [Tooltip("ON = keep the exact rotation the menu camera already has, and only change position. Turn this OFF (the capture menu does it for you) to also author a new rotation.")]
    [SerializeField] private bool keepStartRotation = true;

    [Tooltip("Rotation the camera ends at when START is pressed, used only while Keep Start Rotation is OFF. Filled in by 'Capture Current View As Target'.")]
    [SerializeField] private Vector3 targetEulerAngles;

    [Tooltip("Field of view at the end of the move. 0 = keep the camera's current FOV for the whole move.")]
    [SerializeField] private float targetFieldOfView = 0f;

    [Tooltip("Near clip plane at the end of the move. The menu camera starts with a large near clip, so this is lowered as the camera pushes in. 0 = keep the current near clip.")]
    [SerializeField] private float targetNearClipPlane = 0.3f;

    [Header("Menu Start Pose")]
    [Tooltip("The menu framing. Stored separately from the camera so you can jump back and forth between the two shots without losing either one.")]
    [SerializeField] private Vector3 menuPosition;

    [SerializeField] private Quaternion menuRotation = Quaternion.identity;

    [SerializeField] private float menuFieldOfView = 60f;

    [SerializeField] private float menuNearClipPlane = 0.3f;

    [Tooltip("ON = put the camera onto the stored menu pose as soon as the scene starts. This keeps the main menu framing exact even when the camera was left on the destination shot when the scene was saved.")]
    [SerializeField] private bool applyMenuPoseOnAwake = true;

    [Header("Fit Both Shops")]
    [Tooltip("ON = after the camera reaches its target position, widen the field of view just enough that both shopfronts fit inside the frame. Use this when sliding sideways would otherwise push a building off the edge.")]
    [SerializeField] private bool autoFitBothShops = true;

    [Tooltip("Empty margin kept around the shopfronts when auto fitting, as a fraction of the frame. 0.1 = 10% padding on each side.")]
    [Range(0f, 0.4f)]
    [SerializeField] private float autoFitPadding = 0.1f;

    [Tooltip("Never widen past this field of view, so a bad measurement cannot distort the shot.")]
    [Range(10f, 100f)]
    [SerializeField] private float maximumFieldOfView = 75f;

    public event Action TransitionCompleted;

    private CanvasGroup[] fadeGroups;
    private Vector3 startPosition;
    private Quaternion startRotation;
    private bool hasRun;
    private bool hasCachedPose;

    private void Awake()
    {
        EnsureCamera();
        ApplyStoredMenuPose();
        fadeGroups = FindFadeGroups();

        // Gameplay UI starts hidden.
        if (gameplayUI != null)
        {
            gameplayUI.SetActive(false);
        }
    }

    private void ApplyStoredMenuPose()
    {
        if (!applyMenuPoseOnAwake ||
            viewCamera == null)
        {
            return;
        }

        if (!IsUsable(menuPosition) ||
            !IsFinite(menuRotation))
        {
            return;
        }

        ApplyPose(
            menuPosition,
            menuRotation,
            menuFieldOfView,
            menuNearClipPlane);

        startPosition = menuPosition;
        startRotation = menuRotation;
        hasCachedPose = true;
    }

    [ContextMenu("1 - Capture Current View As Menu Start")]
    private void CaptureCurrentViewAsMenuStart()
    {
        if (!EnsureCamera())
        {
            Debug.LogError(
                "MainMenuStartTransition could not find a Camera to capture.",
                this);

            return;
        }

        Transform camTransform = viewCamera.transform;

        menuPosition = camTransform.position;
        menuRotation = camTransform.rotation;
        menuFieldOfView = viewCamera.fieldOfView;
        menuNearClipPlane = viewCamera.nearClipPlane;

        Debug.Log(
            "MainMenuStartTransition stored the menu start pose:\n" +
            "  position=" + menuPosition + "\n" +
            "  rotation=" + menuRotation.eulerAngles + "\n" +
            "  fieldOfView=" + menuFieldOfView + "\n" +
            "  nearClipPlane=" + menuNearClipPlane,
            this);
    }

    [ContextMenu("2 - Go To Menu Start View")]
    private void GoToMenuStartView()
    {
        if (!EnsureCamera())
        {
            Debug.LogError(
                "MainMenuStartTransition could not find a Camera.",
                this);

            return;
        }

        if (!IsFinite(menuPosition) ||
            !IsFinite(menuRotation))
        {
            Debug.LogWarning(
                "MainMenuStartTransition has no valid menu start pose stored yet.",
                this);

            return;
        }

        ApplyPose(
            menuPosition,
            menuRotation,
            menuFieldOfView,
            menuNearClipPlane);

        Debug.Log(
            "MainMenuStartTransition moved the camera to the menu start view: " +
            menuPosition,
            this);
    }

    [ContextMenu("3 - Go To Transition Target View")]
    private void GoToTransitionTargetView()
    {
        if (!EnsureCamera())
        {
            Debug.LogError(
                "MainMenuStartTransition could not find a Camera.",
                this);

            return;
        }

        if (!useExplicitTarget ||
            !TryGetExplicitTarget(
                out Vector3 target,
                out Quaternion targetRotation))
        {
            Debug.LogWarning(
                "MainMenuStartTransition has no transition target stored yet.",
                this);

            return;
        }

        float fieldOfView =
            targetFieldOfView > 0.01f
                ? targetFieldOfView
                : viewCamera.fieldOfView;

        float nearClip =
            targetNearClipPlane > 0.001f
                ? targetNearClipPlane
                : viewCamera.nearClipPlane;

        fieldOfView = SolveFovForBothShops(
            target,
            targetRotation,
            fieldOfView);

        ApplyPose(
            target,
            targetRotation,
            fieldOfView,
            nearClip);

        Debug.Log(
            "MainMenuStartTransition moved the camera to the transition target view: " +
            target +
            " (fov=" + fieldOfView + ")",
            this);
    }

    private void ApplyPose(
        Vector3 position,
        Quaternion rotation,
        float fieldOfView,
        float nearClip)
    {
        Transform camTransform = viewCamera.transform;

        camTransform.position = position;
        camTransform.rotation = rotation;

        if (fieldOfView > 0.01f)
        {
            viewCamera.fieldOfView = fieldOfView;
        }

        if (nearClip > 0.001f)
        {
            viewCamera.nearClipPlane = nearClip;
        }
    }

    [ContextMenu("Capture Current View As Target")]
    private void CaptureCurrentViewAsTarget()
    {
        if (!EnsureCamera())
        {
            Debug.LogError(
                "MainMenuStartTransition could not find a Camera to capture.",
                this);

            return;
        }

        Transform camTransform = viewCamera.transform;

        targetPosition = camTransform.position;
        targetEulerAngles = camTransform.rotation.eulerAngles;
        targetFieldOfView = viewCamera.fieldOfView;
        targetNearClipPlane = viewCamera.nearClipPlane;

        keepStartRotation = false;
        useExplicitTarget = true;

        Debug.Log(
            "MainMenuStartTransition captured the current view as the transition target:\n" +
            "  position=" + targetPosition + "\n" +
            "  eulerAngles=" + targetEulerAngles + "\n" +
            "  fieldOfView=" + targetFieldOfView + "\n" +
            "  nearClipPlane=" + targetNearClipPlane,
            this);
    }

    private bool EnsureCamera()
    {
        if (viewCamera == null)
        {
            viewCamera = Camera.main;
        }

        if (viewCamera == null)
        {
            Camera[] all = FindObjectsOfType<Camera>();

            for (int i = 0; i < all.Length; i++)
            {
                if (all[i] != null &&
                    all[i].isActiveAndEnabled)
                {
                    viewCamera = all[i];
                    break;
                }
            }

            if (viewCamera == null &&
                all.Length > 0)
            {
                viewCamera = all[0];
            }
        }

        if (viewCamera == null)
        {
            return false;
        }

        if (!hasCachedPose)
        {
            startPosition = viewCamera.transform.position;
            startRotation = viewCamera.transform.rotation;
            hasCachedPose = true;
        }

        return true;
    }

    public void EnterPotteryStudio()
{
    if (isMovingToPotteryStudio)
    {
        return;
    }

    if (!EnsureCamera())
    {
        Debug.LogError(
            "MainMenuStartTransition could not find a Camera for the Pottery Studio transition.",
            this);

        return;
    }

    StartCoroutine(MoveToPotteryStudio());
}

  private IEnumerator MoveToPotteryStudio()
{
    isMovingToPotteryStudio = true;

    // ---------------------------------------------------------
    // CHECK MAIN CAMERA
    // ---------------------------------------------------------

    if (viewCamera == null)
    {
        Debug.LogError(
            "MainMenuStartTransition: View Camera is not assigned.",
            this);

        isMovingToPotteryStudio = false;
        yield break;
    }

    // Capture starting near clip plane to lerp it
    float startNearClip = viewCamera.nearClipPlane;
    float targetNearClip = 0.01f; // Fixes buildings disappearing when zooming in


    // ---------------------------------------------------------
    // CHECK POTTERY CAMERA TARGET
    // ---------------------------------------------------------

    if (potteryStudioCameraTarget == null)
    {
        Debug.LogError(
            "MainMenuStartTransition: PotteryStudioCameraTarget is not assigned.",
            this);

        isMovingToPotteryStudio = false;
        yield break;
    }

    // ---------------------------------------------------------
    // GET CAMERA TRANSFORM
    // ---------------------------------------------------------

    Transform cameraTransform =
        viewCamera.transform;

    // Current street view
    Vector3 startPosition =
        cameraTransform.position;

    Quaternion startRotation =
        cameraTransform.rotation;

    // Exact pottery target view
    Vector3 targetPosition = potteryStudioCameraTarget.position;
    Quaternion targetRotation = potteryStudioCameraTarget.rotation;

    // ---------------------------------------------------------
    // DEBUG INFORMATION
    // ---------------------------------------------------------

    Debug.Log(
        "MORPHIC Pottery Camera Transition\n" +
        "Start Position: " +
        startPosition +
        "\nStart Rotation: " +
        startRotation.eulerAngles +
        "\nTarget Position: " +
        targetPosition +
        "\nTarget Rotation: " +
        targetRotation.eulerAngles,
        this);

    // ---------------------------------------------------------
    // HIDE GAMEPLAY UI DURING TRANSITION
    // ---------------------------------------------------------

    if (gameplayUI != null)
    {
        gameplayUI.SetActive(false);
    }

    // ---------------------------------------------------------
    // CAMERA MOVEMENT
    // ---------------------------------------------------------

    float duration =
        Mathf.Max(
            0.01f,
            potteryStudioMoveDuration);

    float elapsed = 0f;

    while (elapsed < duration)
    {
        elapsed +=
            Time.unscaledDeltaTime;

        float t =
            Mathf.Clamp01(
                elapsed / duration);

        // Smooth acceleration/deceleration
        float eased =
            t * t *
            (3f - 2f * t);

        // Position
        cameraTransform.position =
            Vector3.Lerp(
                startPosition,
                targetPosition,
                eased);

        // Rotation
        cameraTransform.rotation =
            Quaternion.Slerp(
                startRotation,
                targetRotation,
                eased);

        // Near Clip Plane (prevents clipping through walls/buildings)
        viewCamera.nearClipPlane = Mathf.Lerp(startNearClip, targetNearClip, eased);

        yield return null;
    }

    // ---------------------------------------------------------
    // FORCE EXACT FINAL POSITION
    // ---------------------------------------------------------

    cameraTransform.position =
        targetPosition;

    cameraTransform.rotation =
        targetRotation;

    viewCamera.nearClipPlane = targetNearClip;

    // ---------------------------------------------------------
    // FINISHED
    // ---------------------------------------------------------

    isMovingToPotteryStudio = false;

    PotteryStudioTransitionCompleted?.Invoke();
}
    public void StartGameplay()
    {
        if (hasRun)
        {
            return;
        }

        hasRun = true;

        if (!EnsureCamera())
        {
            Debug.LogError(
                "MainMenuStartTransition could not find a Camera.",
                this);

            return;
        }

        StartCoroutine(Run());
    }

    private CanvasGroup[] FindFadeGroups()
    {
        List<CanvasGroup> groups = new List<CanvasGroup>();

        string[] names =
        {
            "Morphic Logo",
            "Start Button",
            "Language Button",
            "Settings Button"
        };

        Canvas[] canvases = FindObjectsOfType<Canvas>();

        for (int i = 0; i < names.Length; i++)
        {
            GameObject target = FindByName(
                canvases,
                names[i]);

            if (target == null)
            {
                continue;
            }

            CanvasGroup group =
                target.GetComponent<CanvasGroup>();

            if (group == null)
            {
                group = target.AddComponent<CanvasGroup>();
            }

            groups.Add(group);
        }

        return groups.ToArray();
    }

    private static GameObject FindByName(
        Canvas[] canvases,
        string name)
    {
        for (int i = 0; i < canvases.Length; i++)
        {
            Transform found = FindChild(
                canvases[i].transform,
                name);

            if (found != null)
            {
                return found.gameObject;
            }
        }

        return null;
    }

    private static Transform FindChild(
        Transform root,
        string name)
    {
        if (root == null)
        {
            return null;
        }

        for (int i = 0; i < root.childCount; i++)
        {
            Transform child = root.GetChild(i);

            if (child.name == name)
            {
                return child;
            }

            Transform nested = FindChild(
                child,
                name);

            if (nested != null)
            {
                return nested;
            }
        }

        return null;
    }

    private void GetTargetFraming(
        out Vector3 target,
        out Quaternion targetRotation)
    {
        Vector3 forward =
            startRotation * Vector3.forward;

        if (TryGetExplicitTarget(
            out target,
            out targetRotation))
        {
            return;
        }

        Bounds combined;

        bool measured =
            TryMeasureAllShops(out combined);

        if (!measured)
        {
            Debug.LogWarning(
                "MainMenuStartTransition could not measure the shops " +
                "(roots=" + DescribeRoots() +
                ", " + potteryName +
                ", " + restaurantName +
                "); falling back to a simple forward move.",
                this);

            target =
                startPosition +
                forward * fallbackDistance;

            targetRotation =
                startRotation;

            return;
        }

        Vector3 lookAt =
            combined.center +
            Vector3.up * framingHeight;

        Vector3 direction =
            lookAt - startPosition;

        if (!IsUsable(direction))
        {
            direction = forward;
        }

        direction.Normalize();

        direction =
            ClampPitch(direction);

        float distance =
            RequiredDistance(
                lookAt,
                direction,
                combined);

        distance *=
            Mathf.Max(
                0.1f,
                distanceMultiplier);

        target =
            lookAt -
            direction * distance;

        if (Mathf.Abs(lateralOffset) > 0.001f)
        {
            Vector3 right =
                Vector3.Cross(
                    direction,
                    startRotation * Vector3.up).normalized;

            target +=
                right * lateralOffset;
        }

        if (!IsFinite(target))
        {
            target =
                startPosition +
                forward * fallbackDistance;

            targetRotation =
                startRotation;

            return;
        }

        targetRotation =
            LookRotationFrom(direction);
    }

    private float SolveFovForBothShops(
        Vector3 position,
        Quaternion rotation,
        float currentFov)
    {
        if (!autoFitBothShops ||
            viewCamera == null ||
            viewCamera.orthographic)
        {
            return currentFov;
        }

        Bounds combined;

        if (!TryMeasureAllShops(out combined))
        {
            return currentFov;
        }

        float aspect =
            viewCamera.aspect;

        if (aspect <= 0.01f ||
            float.IsNaN(aspect))
        {
            aspect = 16f / 9f;
        }

        Vector3 forward =
            rotation * Vector3.forward;

        Vector3 camUp =
            rotation * Vector3.up;

        Vector3 camRight =
            Vector3.Cross(
                forward,
                camUp);

        if (camRight.sqrMagnitude < 0.0001f)
        {
            return currentFov;
        }

        camRight.Normalize();

        float tanHorizontal = 0f;
        float tanVertical = 0f;

        Vector3 ext =
            combined.extents;

        Vector3 centre =
            combined.center;

        for (int i = 0; i < 8; i++)
        {
            Vector3 corner =
                new Vector3(
                    (i & 1) == 0 ? -ext.x : ext.x,
                    (i & 2) == 0 ? -ext.y : ext.y,
                    (i & 4) == 0 ? -ext.z : ext.z) +
                centre;

            Vector3 offset =
                corner - position;

            float depth =
                Vector3.Dot(
                    offset,
                    forward);

            if (depth <= 0.01f)
            {
                continue;
            }

            float side =
                Mathf.Abs(
                    Vector3.Dot(
                        offset,
                        camRight));

            float up =
                Mathf.Abs(
                    Vector3.Dot(
                        offset,
                        camUp));

            tanHorizontal =
                Mathf.Max(
                    tanHorizontal,
                    side / depth);

            tanVertical =
                Mathf.Max(
                    tanVertical,
                    up / depth);
        }

        if (tanHorizontal < 0.0001f &&
            tanVertical < 0.0001f)
        {
            return currentFov;
        }

        float fill =
            Mathf.Clamp(
                1f - Mathf.Max(
                    0f,
                    autoFitPadding),
                0.2f,
                1f);

        tanHorizontal /= fill;
        tanVertical /= fill;

        float requiredVerticalTan =
            Mathf.Max(
                tanVertical,
                tanHorizontal / aspect);

        if (requiredVerticalTan < 0.0001f)
        {
            return currentFov;
        }

        float required =
            2f *
            Mathf.Atan(
                requiredVerticalTan) *
            Mathf.Rad2Deg;

        if (!float.IsNaN(required) &&
            required > currentFov)
        {
            return Mathf.Min(
                required,
                maximumFieldOfView);
        }

        return currentFov;
    }

    private Quaternion LookRotationFrom(
        Vector3 direction)
    {
        Vector3 up =
            startRotation * Vector3.up;

        if (Mathf.Abs(
                Vector3.Dot(
                    direction,
                    up.normalized)) > 0.999f)
        {
            up =
                startRotation *
                Vector3.right;
        }

        Quaternion result =
            Quaternion.LookRotation(
                direction,
                up.normalized);

        if (!IsFinite(result))
        {
            return startRotation;
        }

        return result;
    }

    private static bool IsFinite(
        Quaternion q)
    {
        return
            !float.IsNaN(q.x) &&
            !float.IsNaN(q.y) &&
            !float.IsNaN(q.z) &&
            !float.IsNaN(q.w);
    }

    private Vector3 ClampPitch(
        Vector3 direction)
    {
        Vector3 camUp =
            (startRotation * Vector3.up).normalized;

        Vector3 side =
            startRotation * Vector3.right;

        float tilt =
            Vector3.Dot(
                direction,
                camUp);

        float limit =
            maximumPitch *
            Mathf.Deg2Rad;

        if (Mathf.Abs(tilt) <=
            Mathf.Sin(limit))
        {
            return direction;
        }

        float sign =
            tilt >= 0f
                ? 1f
                : -1f;

        Vector3 level =
            direction -
            camUp *
            (sign * Mathf.Sin(limit));

        if (level.sqrMagnitude <
            0.0001f)
        {
            level =
                side * sign;
        }

        level.Normalize();

        return
            (level * Mathf.Cos(limit) +
             camUp *
             (sign * Mathf.Sin(limit)))
            .normalized;
    }

    private static bool IsUsable(
        Vector3 v)
    {
        return
            v.sqrMagnitude > 0.0001f &&
            IsFinite(v);
    }

    private static bool IsFinite(
        Vector3 v)
    {
        return
            !float.IsNaN(v.x) &&
            !float.IsNaN(v.y) &&
            !float.IsNaN(v.z) &&
            !float.IsInfinity(v.x) &&
            !float.IsInfinity(v.y) &&
            !float.IsInfinity(v.z);
    }

    private bool TryGetExplicitTarget(
        out Vector3 target,
        out Quaternion targetRotation)
    {
        target = Vector3.zero;

        targetRotation =
            Quaternion.identity;

        if (!useExplicitTarget)
        {
            return false;
        }

        bool configured =
            targetPosition.sqrMagnitude >
                0.0001f ||
            (!keepStartRotation &&
             targetEulerAngles.sqrMagnitude >
                0.0001f);

        if (!configured)
        {
            return false;
        }

        if (!IsFinite(targetPosition))
        {
            Debug.LogWarning(
                "MainMenuStartTransition has a non-finite Target Position; ignoring the authored target.",
                this);

            return false;
        }

        if (keepStartRotation)
        {
            targetRotation =
                startRotation;

            target =
                targetPosition;

            return true;
        }

        targetRotation =
            Quaternion.Euler(
                targetEulerAngles);

        if (!IsFinite(targetRotation))
        {
            Debug.LogWarning(
                "MainMenuStartTransition has a non-finite Target Rotation; ignoring the authored target.",
                this);

            targetRotation =
                Quaternion.identity;

            return false;
        }

        target =
            targetPosition;

        return true;
    }

    private bool TryMeasureAllShops(
        out Bounds combined)
    {
        combined =
            new Bounds();

        if (TryMeasureAssigned(
            out combined))
        {
            return true;
        }

        if (autoDetectShops)
        {
            return TryAutoDetectShops(
                out combined);
        }

        return false;
    }

    private bool TryMeasureAssigned(
        out Bounds combined)
    {
        combined =
            new Bounds();

        bool initialised = false;

        if (shopRoots != null)
        {
            for (int i = 0;
                 i < shopRoots.Length;
                 i++)
            {
                if (shopRoots[i] == null)
                {
                    continue;
                }

                Bounds part =
                    Measure(
                        shopRoots[i]);

                if (!HasBounds(part))
                {
                    continue;
                }

                if (!initialised)
                {
                    combined =
                        part;

                    initialised =
                        true;
                }
                else
                {
                    combined.Encapsulate(
                        part);
                }
            }
        }

        Transform pottery =
            ResolveShop(
                potteryOverride,
                potteryName);

        Transform restaurant =
            ResolveShop(
                restaurantOverride,
                restaurantName);

        Bounds[] named =
        {
            Measure(pottery),
            Measure(restaurant)
        };

        for (int i = 0;
             i < named.Length;
             i++)
        {
            if (!HasBounds(named[i]))
            {
                continue;
            }

            if (!initialised)
            {
                combined =
                    named[i];

                initialised =
                    true;
            }
            else
            {
                combined.Encapsulate(
                    named[i]);
            }
        }

        return
            initialised &&
            HasBounds(combined);
    }

    private bool TryAutoDetectShops(
        out Bounds combined)
    {
        combined =
            new Bounds();

        Renderer[] renderers =
            FindObjectsOfType<Renderer>();

        Vector3 camRight =
            startRotation *
            Vector3.right;

        Vector3 camForward =
            startRotation *
            Vector3.forward;

        List<Renderer> visible =
            new List<Renderer>();

        for (int i = 0;
             i < renderers.Length;
             i++)
        {
            Renderer r =
                renderers[i];

            if (r == null ||
                !r.enabled ||
                !r.gameObject.activeInHierarchy)
            {
                continue;
            }

            Vector3 offset =
                r.bounds.center -
                startPosition;

            if (Vector3.Dot(
                    offset,
                    camForward) < 0.5f)
            {
                continue;
            }

            if (Mathf.Abs(
                    Vector3.Dot(
                        offset,
                        camRight)) > 60f)
            {
                continue;
            }

            if (r.bounds.size.y < 0.5f ||
                r.bounds.size.x > 200f ||
                r.bounds.size.z > 200f)
            {
                continue;
            }

            visible.Add(r);
        }

        if (visible.Count == 0)
        {
            return false;
        }

        visible.Sort(
            delegate (
                Renderer a,
                Renderer b)
            {
                float pa =
                    Vector3.Dot(
                        a.bounds.center -
                        startPosition,
                        camRight);

                float pb =
                    Vector3.Dot(
                        b.bounds.center -
                        startPosition,
                        camRight);

                return pa.CompareTo(pb);
            });

        float splitGap =
            Mathf.Max(
                2.5f,
                autoDetectGap);

        List<Bounds> clusters =
            new List<Bounds>();

        Bounds current =
            visible[0].bounds;

        float currentMax =
            HorizontalEdge(
                visible[0],
                camRight,
                true);

        for (int i = 1;
             i < visible.Count;
             i++)
        {
            float min =
                HorizontalEdge(
                    visible[i],
                    camRight,
                    false);

            float max =
                HorizontalEdge(
                    visible[i],
                    camRight,
                    true);

            if (min <=
                currentMax +
                splitGap)
            {
                current.Encapsulate(
                    visible[i].bounds);

                currentMax =
                    Mathf.Max(
                        currentMax,
                        max);
            }
            else
            {
                clusters.Add(
                    current);

                current =
                    visible[i].bounds;

                currentMax =
                    max;
            }
        }

        clusters.Add(current);

        if (clusters.Count == 0)
        {
            return false;
        }

        clusters.Sort(
            delegate (
                Bounds a,
                Bounds b)
            {
                return
                    b.size.sqrMagnitude.CompareTo(
                        a.size.sqrMagnitude);
            });

        int take =
            Mathf.Min(
                2,
                clusters.Count);

        bool initialised =
            false;

        for (int i = 0;
             i < take;
             i++)
        {
            if (clusters[i].size.sqrMagnitude < 1f)
            {
                continue;
            }

            if (!initialised)
            {
                combined =
                    clusters[i];

                initialised =
                    true;
            }
            else
            {
                combined.Encapsulate(
                    clusters[i]);
            }
        }

        return initialised;
    }

    private static float HorizontalEdge(
        Renderer r,
        Vector3 axis,
        bool max)
    {
        Bounds b =
            r.bounds;

        float centre =
            Vector3.Dot(
                b.center,
                axis);

        float extent =
            Mathf.Abs(
                b.extents.x *
                axis.x) +
            Mathf.Abs(
                b.extents.y *
                axis.y) +
            Mathf.Abs(
                b.extents.z *
                axis.z);

        return max
            ? centre + extent
            : centre - extent;
    }

    private string DescribeRoots()
    {
        if (shopRoots == null ||
            shopRoots.Length == 0)
        {
            return "none assigned";
        }

        System.Text.StringBuilder sb =
            new System.Text.StringBuilder();

        for (int i = 0;
             i < shopRoots.Length;
             i++)
        {
            sb.Append(
                Describe(
                    shopRoots[i]));

            sb.Append(", ");
        }

        return sb.ToString();
    }

    private static bool HasBounds(
        Bounds b)
    {
        return
            b.size.sqrMagnitude >
            0.001f;
    }

    private static Bounds Measure(
        Transform root)
    {
        Bounds result =
            new Bounds();

        bool initialised =
            false;

        if (root == null)
        {
            return result;
        }

        Renderer[] renderers =
            root.GetComponentsInChildren<Renderer>();

        for (int i = 0;
             i < renderers.Length;
             i++)
        {
            if (!initialised)
            {
                result =
                    renderers[i].bounds;

                initialised =
                    true;
            }
            else
            {
                result.Encapsulate(
                    renderers[i].bounds);
            }
        }

        if (!initialised)
        {
            result =
                new Bounds(
                    root.position,
                    Vector3.one);
        }

        return result;
    }

    private float RequiredDistance(
        Vector3 lookAt,
        Vector3 axis,
        Bounds bounds)
    {
        if (viewCamera == null ||
            viewCamera.orthographic)
        {
            return fallbackDistance;
        }

        float aspect =
            viewCamera.aspect;

        if (aspect <= 0f ||
            aspect < 0.01f ||
            float.IsNaN(aspect))
        {
            aspect =
                16f / 9f;
        }

        float halfVertical =
            viewCamera.fieldOfView *
            0.5f *
            Mathf.Deg2Rad;

        float tanVertical =
            Mathf.Tan(
                halfVertical);

        if (tanVertical < 0.0001f)
        {
            return fallbackDistance;
        }

        float tanHorizontal =
            tanVertical *
            aspect;

        if (tanHorizontal < 0.0001f)
        {
            tanHorizontal =
                0.0001f;
        }

        Vector3 ext =
            bounds.extents;

        Vector3 centre =
            bounds.center;

        Vector3 authoredUp =
            startRotation *
            Vector3.up;

        Vector3 camUp =
            authoredUp -
            axis *
            Vector3.Dot(
                authoredUp,
                axis);

        if (camUp.sqrMagnitude <
            0.0001f)
        {
            camUp =
                (startRotation *
                 Vector3.right) -
                axis *
                Vector3.Dot(
                    startRotation *
                    Vector3.right,
                    axis);
        }

        if (camUp.sqrMagnitude <
            0.0001f)
        {
            camUp =
                Vector3.up -
                axis *
                Vector3.Dot(
                    Vector3.up,
                    axis);
        }

        camUp.Normalize();

        float required =
            0f;

        for (int i = 0;
             i < 8;
             i++)
        {
            Vector3 corner =
                new Vector3(
                    (i & 1) == 0
                        ? -ext.x
                        : ext.x,
                    (i & 2) == 0
                        ? -ext.y
                        : ext.y,
                    (i & 4) == 0
                        ? -ext.z
                        : ext.z) +
                centre;

            Vector3 offset =
                corner - lookAt;

            Vector3 lateral =
                offset -
                axis *
                Vector3.Dot(
                    offset,
                    axis);

            float vertical =
                Mathf.Abs(
                    Vector3.Dot(
                        lateral,
                        camUp));

            Vector3 side =
                lateral -
                camUp *
                vertical;

            float horizontal =
                side.magnitude;

            float depth =
                Vector3.Dot(
                    offset,
                    axis);

            if (depth <= 0.01f)
            {
                required =
                    Mathf.Max(
                        required,
                        depth + 0.01f);

                continue;
            }

            float needVertical =
                vertical /
                tanVertical;

            float needHorizontal =
                horizontal /
                tanHorizontal;

            required =
                Mathf.Max(
                    required,
                    Mathf.Max(
                        needVertical,
                        needHorizontal));
        }

        float fill =
            Mathf.Max(
                0.2f,
                1f - framingPadding);

        return
            Mathf.Max(
                required / fill,
                minimumDistance);
    }

    private Transform ResolveShop(
        Transform assigned,
        string fragment)
    {
        if (assigned != null)
        {
            return assigned;
        }

        return FindShop(fragment);
    }

    private Transform FindShop(
        string fragment)
    {
        if (string.IsNullOrEmpty(fragment))
        {
            return null;
        }

        Transform[] all =
            FindObjectsOfType<Transform>();

        Transform best =
            null;

        for (int i = 0;
             i < all.Length;
             i++)
        {
            string name =
                all[i].name;

            if (name.IndexOf(
                    fragment,
                    StringComparison.OrdinalIgnoreCase) <
                0)
            {
                continue;
            }

            if (best == null ||
                Depth(all[i]) <
                Depth(best))
            {
                best =
                    all[i];
            }
        }

        return best;
    }

    private static int Depth(
        Transform t)
    {
        int depth = 0;

        Transform current =
            t;

        while (current.parent != null)
        {
            depth++;

            current =
                current.parent;
        }

        return depth;
    }

    private static string Describe(
        Transform t)
    {
        return t == null
            ? "NOT FOUND"
            : t.name +
              " @ " +
              t.position;
    }

    [ContextMenu("Log Shop Candidates")]
    private void LogShopCandidates()
    {
        bool hasCamera =
            EnsureCamera();

        Transform pottery =
            ResolveShop(
                potteryOverride,
                potteryName);

        Debug.Log(
            "Shop candidates (drag the two shop groups into Shop Roots):\n" +
            (hasCamera
                ? BuildCandidateReport(
                    pottery)
                : "No Camera found in the scene."),
            this);
    }

    private string BuildCandidateReport(
        Transform pottery)
    {
        System.Text.StringBuilder sb =
            new System.Text.StringBuilder();

        sb.AppendLine(
            "=== Camera ===");

        sb.AppendLine(
            "position=" +
            startPosition +
            " rotation=" +
            startRotation.eulerAngles +
            " fov=" +
            viewCamera.fieldOfView +
            " aspect=" +
            viewCamera.aspect.ToString("0.00"));

        sb.AppendLine(
            "=== Current overrides ===");

        sb.AppendLine(
            "pottery=" +
            Describe(pottery) +
            " bounds=" +
            Measure(pottery).size);

        sb.AppendLine(
            "restaurant=" +
            Describe(
                ResolveShop(
                    restaurantOverride,
                    restaurantName)) +
            " bounds=" +
            Measure(
                ResolveShop(
                    restaurantOverride,
                    restaurantName)).size);

        sb.AppendLine(
            "roots=" +
            DescribeRoots());

        Renderer[] renderers =
            FindObjectsOfType<Renderer>();

        System.Collections.Generic.Dictionary<
            string,
            System.Collections.Generic.List<Renderer>> groups =
            new System.Collections.Generic.Dictionary<
                string,
                System.Collections.Generic.List<Renderer>>();

        System.Collections.Generic.List<string> order =
            new System.Collections.Generic.List<string>();

        for (int i = 0;
             i < renderers.Length;
             i++)
        {
            Renderer r =
                renderers[i];

            if (r == null ||
                !r.enabled ||
                !r.gameObject.activeInHierarchy)
            {
                continue;
            }

            string key =
                TopLevelName(
                    r.transform);

            System.Collections.Generic.List<Renderer> list;

            if (!groups.TryGetValue(
                    key,
                    out list))
            {
                list =
                    new System.Collections.Generic.List<Renderer>();

                groups.Add(
                    key,
                    list);

                order.Add(
                    key);
            }

            list.Add(r);
        }

        order.Sort(
            delegate (
                string a,
                string b)
            {
                return
                    groups[b].Count.CompareTo(
                        groups[a].Count);
            });

        sb.AppendLine(
            "=== Scene groups (" +
            renderers.Length +
            " renderers under " +
            order.Count +
            " roots) ===");

        for (int g = 0;
             g < order.Count;
             g++)
        {
            string key =
                order[g];

            System.Collections.Generic.List<Renderer> list =
                groups[key];

            Bounds combined =
                list[0].bounds;

            for (int i = 1;
                 i < list.Count;
                 i++)
            {
                combined.Encapsulate(
                    list[i].bounds);
            }

            sb.AppendLine(
                "  [" +
                key +
                "] parts=" +
                list.Count +
                " centre=" +
                combined.center +
                " size=" +
                combined.size +
                " depth=" +
                Vector3.Distance(
                    startPosition,
                    combined.center)
                .ToString("0.0"));
        }

        return sb.ToString();
    }

    private static string TopLevelName(
        Transform t)
    {
        Transform current =
            t;

        while (current.parent != null &&
               current.parent.parent != null)
        {
            current =
                current.parent;
        }

        return current.name;
    }

    private IEnumerator Run()
    {
        if (!EnsureCamera())
        {
            yield break;
        }

        Transform camTransform =
            viewCamera.transform;

        Vector3 target;
        Quaternion targetRotation;

        GetTargetFraming(
            out target,
            out targetRotation);

        float endFieldOfView =
            viewCamera.fieldOfView;

        float startFieldOfView =
            endFieldOfView;

        if (endFieldOfView < 0.01f)
        {
            endFieldOfView =
                60f;
        }

        if (targetFieldOfView > 0.01f)
        {
            endFieldOfView =
                targetFieldOfView;
        }

        float fittedFieldOfView =
            SolveFovForBothShops(
                target,
                targetRotation,
                endFieldOfView);

        bool animateFieldOfView =
            !Mathf.Approximately(
                fittedFieldOfView,
                startFieldOfView);

        endFieldOfView =
            fittedFieldOfView;

        Debug.Log(
            "MainMenuStartTransition: roots=" +
            DescribeRoots() +
            ", pottery=" +
            Describe(
                ResolveShop(
                    potteryOverride,
                    potteryName)) +
            ", restaurant=" +
            Describe(
                ResolveShop(
                    restaurantOverride,
                    restaurantName)) +
            ", from=" +
            startPosition +
            ", to=" +
            target +
            ", look=" +
            (target +
             targetRotation *
             Vector3.forward) +
            ", rotationFrom=" +
            startRotation.eulerAngles +
            ", rotationTo=" +
            targetRotation.eulerAngles +
            ", distance=" +
            Vector3.Distance(
                startPosition,
                target) +
            ", fovFrom=" +
            startFieldOfView +
            ", fovTo=" +
            endFieldOfView,
            this);

        yield return StartCoroutine(
            FadeOut());

        SetMenuInteractable(false);

        float elapsed =
            0f;

        float startNearClip =
            viewCamera.nearClipPlane;

        bool animateNearClip =
            targetNearClipPlane > 0.001f;

        camTransform.position =
            startPosition;

        camTransform.rotation =
            startRotation;

        while (elapsed <
               moveDuration)
        {
            elapsed +=
                Time.unscaledDeltaTime;

            float t =
                Mathf.Clamp01(
                    elapsed /
                    moveDuration);

            float eased =
                t * t *
                (3f - 2f * t);

            camTransform.position =
                Vector3.Lerp(
                    startPosition,
                    target,
                    eased);

            camTransform.rotation =
                Quaternion.Slerp(
                    startRotation,
                    targetRotation,
                    eased);

            if (animateFieldOfView)
            {
                viewCamera.fieldOfView =
                    Mathf.Lerp(
                        startFieldOfView,
                        endFieldOfView,
                        eased);
            }

            if (animateNearClip)
            {
                viewCamera.nearClipPlane =
                    Mathf.Lerp(
                        startNearClip,
                        targetNearClipPlane,
                        eased);
            }

            yield return null;
        }

        camTransform.position =
            target;

        camTransform.rotation =
            targetRotation;

        if (animateFieldOfView)
        {
            viewCamera.fieldOfView =
                endFieldOfView;
        }

        if (animateNearClip)
        {
            viewCamera.nearClipPlane =
                targetNearClipPlane;
        }

        if (TransitionCompleted != null)
        {
            TransitionCompleted();
        }

        // Show Gameplay UI only after the camera has finished moving.
        if (gameplayUI != null)
        {
            gameplayUI.SetActive(true);
        }
    }

    private IEnumerator FadeOut()
    {
        if (fadeGroups == null ||
            fadeGroups.Length == 0)
        {
            yield break;
        }

        float elapsed =
            0f;

        while (elapsed <
               fadeDuration)
        {
            elapsed +=
                Time.unscaledDeltaTime;

            float t =
                Mathf.Clamp01(
                    elapsed /
                    fadeDuration);

            for (int i = 0;
                 i < fadeGroups.Length;
                 i++)
            {
                if (fadeGroups[i] != null)
                {
                    fadeGroups[i].alpha =
                        1f - t;
                }
            }

            yield return null;
        }

        for (int i = 0;
             i < fadeGroups.Length;
             i++)
        {
            if (fadeGroups[i] != null)
            {
                fadeGroups[i].alpha =
                    0f;

                fadeGroups[i].blocksRaycasts =
                    false;

                fadeGroups[i].interactable =
                    false;
            }
        }
    }

    private void SetMenuInteractable(
        bool value)
    {
        if (fadeGroups == null)
        {
            return;
        }

        for (int i = 0;
             i < fadeGroups.Length;
             i++)
        {
            if (fadeGroups[i] != null)
            {
                fadeGroups[i].blocksRaycasts =
                    value;

                fadeGroups[i].interactable =
                    value;
            }
        }
    }
}

public static class MainMenuStartBootstrap
{
    [RuntimeInitializeOnLoadMethod(
        RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Install()
    {
        Camera camera =
            Camera.main;

        if (camera == null)
        {
            return;
        }

        MainMenuStartTransition transition =
            UnityEngine.Object.FindObjectOfType<
                MainMenuStartTransition>();

        if (transition == null)
        {
            transition =
                camera.gameObject.AddComponent<
                    MainMenuStartTransition>();
        }

        Button startButton =
            FindStartButton();

        if (startButton == null)
        {
            Debug.LogWarning(
                "MainMenuStartBootstrap could not find the Start Button, so the camera transition will not run.");

            return;
        }

        if (IsAlreadyBound(
                startButton,
                transition))
        {
            return;
        }

        startButton.onClick.AddListener(
            transition.StartGameplay);
    }

    private static bool IsAlreadyBound(
        Button button,
        MainMenuStartTransition transition)
    {
        if (transition == null)
        {
            return false;
        }

        for (int i = 0;
             i < button.onClick.GetPersistentEventCount();
             i++)
        {
            if (button.onClick.GetPersistentTarget(i) ==
                    transition &&
                button.onClick.GetPersistentMethodName(i) ==
                    "StartGameplay")
            {
                return true;
            }
        }

        return false;
    }

    private static Button FindStartButton()
    {
        Canvas[] canvases =
            UnityEngine.Object.FindObjectsOfType<Canvas>();

        for (int i = 0;
             i < canvases.Length;
             i++)
        {
            Transform found =
                FindChild(
                    canvases[i].transform,
                    "Start Button");

            if (found == null)
            {
                continue;
            }

            Button button =
                found.GetComponent<Button>();

            if (button == null)
            {
                button =
                    found.GetComponentInChildren<Button>();
            }

            if (button != null)
            {
                return button;
            }
        }

        return null;
    }

    private static Transform FindChild(
        Transform root,
        string name)
    {
        if (root == null)
        {
            return null;
        }

        for (int i = 0;
             i < root.childCount;
             i++)
        {
            Transform child =
                root.GetChild(i);

            if (child.name == name)
            {
                return child;
            }

            Transform nested =
                FindChild(
                    child,
                    name);

            if (nested != null)
            {
                return nested;
            }
        }

        return null;
    }
}