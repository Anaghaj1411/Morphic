using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// Pink/green zoom cluster with plus and minus. Shown after the user
/// picks lantern, teapot, or keris so they can step the camera in or out.
public class MorphicSculptZoomUI : MonoBehaviour
{
    private const float MoveDuration = 0.28f;
    private const int MaxZoomLevel = 10;
    private const float CloseOvershoot = 0.62f;
    private const float ZoomPullFraction = 0.22f;
    private const float MinPull = 0.28f;
    private const float MaxPull = 0.65f;
    private static readonly Vector3 LanternZoomPos = new Vector3(-26.469f, 2.5f, -6.191f);
    private static readonly Vector3 LanternZoomEuler = new Vector3(0f, -257f, 90f);
    private static readonly Vector3 TeapotZoomPos = new Vector3(-26.448f, 2.517f, -6.203913f);
    private static readonly Vector3 TeapotZoomEuler = new Vector3(0f, -257f, 90f);
    private static readonly Vector3 KerisZoomPos = new Vector3(-26.06807f, 2.537f, -6.085f);
    private static readonly Vector3 KerisZoomEuler = new Vector3(0f, -257f, 90f);

    private GameObject root;
    private Button minusButton;
    private Button plusButton;
    private Coroutine moveRoutine;
    private bool zoomed;
    private int zoomLevel;
    private int extraZoomLevel;
    private bool hasRestPose;
    private Vector3 restPosition;
    private Quaternion restRotation;
    private float restFieldOfView;
    private float restNearClip;
    private static bool vertexSculptOwnsCamera;

    public bool IsZoomed => zoomed;

    public static MorphicSculptZoomUI FindInstance()
    {
        return FindFirstObjectByType<MorphicSculptZoomUI>(FindObjectsInactive.Include);
    }

    /// <summary>
    /// Vertex sculpt owns the camera. Keep the user's current zoom pose so
    /// leaving vertex sculpt can return to it.
    /// </summary>
    public static void HandOffToVertexSculpt(
        ref bool hasStored,
        ref Vector3 storedPosition,
        ref Quaternion storedRotation,
        ref float storedFieldOfView,
        ref float storedNearClip)
    {
        vertexSculptOwnsCamera = true;
        MorphicSculptZoomUI zoomUi = FindInstance();
        Camera view = zoomUi != null ? ResolveViewCamera() : Camera.main;
        if (!hasStored && view != null)
        {
            storedPosition = view.transform.position;
            storedRotation = view.transform.rotation;
            storedFieldOfView = view.fieldOfView;
            storedNearClip = view.nearClipPlane;
            hasStored = true;
        }

        if (zoomUi != null)
        {
            zoomUi.StopMove();
            zoomUi.extraZoomLevel = 0;
            zoomUi.SetVisible(true);
            zoomUi.BringToFront();
        }
    }

    public static void ResumeAfterVertexSculpt()
    {
        vertexSculptOwnsCamera = false;
        MorphicSculptZoomUI zoomUi = FindInstance();
        if (zoomUi == null)
            return;

        zoomUi.extraZoomLevel = 0;
        zoomUi.zoomed = zoomUi.zoomLevel > 0;
    }

    public static void ReleaseIfPresent()
    {
        vertexSculptOwnsCamera = false;
        MorphicSculptZoomUI zoomUi = FindInstance();
        if (zoomUi != null)
        {
            zoomUi.extraZoomLevel = 0;
            zoomUi.ReleaseWithoutRestore();
        }
    }

    public void ReleaseWithoutRestore()
    {
        StopMove();
        zoomed = false;
        zoomLevel = 0;
        extraZoomLevel = 0;
        hasRestPose = false;
    }

    public void SetVisible(bool visible)
    {
        EnsureUi();
        if (root == null)
            return;

        if (!visible)
            RestoreRestPose(false);

        root.SetActive(visible);
        if (visible)
            BringToFront();
    }

    public void BringToFront()
    {
        if (root != null && root.activeSelf)
            root.transform.SetAsLastSibling();
    }

    private void LateUpdate()
    {
        if (root != null && root.activeSelf)
            BringToFront();
    }

    private void Awake()
    {
        EnsureUi();
        SetVisible(false);
    }

    private void OnDisable()
    {
        StopMove();
    }

    private void EnsureUi()
    {
        if (root != null)
        {
            if (minusButton != null && plusButton != null)
            {
                WireButtons();
                return;
            }

            Object.DestroyImmediate(root);
            root = null;
            minusButton = null;
            plusButton = null;
        }

        Canvas canvas = FindFirstObjectByType<Canvas>();
        if (canvas == null)
            return;

        Transform existing = canvas.transform.Find("MorphicSculptZoom");
        if (existing != null)
        {
            if (existing.Find("Zoom/Fill/ZoomWord") == null)
            {
                Object.DestroyImmediate(existing.gameObject);
            }
            else
            {
                root = existing.gameObject;
                minusButton = existing.Find("Zoom/Fill/Minus")?.GetComponent<Button>();
                plusButton = existing.Find("Zoom/Fill/Plus")?.GetComponent<Button>();
                Transform existingFrame = existing.Find("Zoom");
                if (existingFrame != null)
                    PlaceTopRight(existingFrame.GetComponent<RectTransform>());
                WireButtons();
                return;
            }
        }

        root = new GameObject("MorphicSculptZoom", typeof(RectTransform));
        RectTransform rootRect = root.GetComponent<RectTransform>();
        rootRect.SetParent(canvas.transform, false);
        StretchFull(rootRect);

        Image frame = LanguagePageBuilder.CreateRoundedImage(
            "Zoom", root.transform, 28, LanguagePageBuilder.Green);
        PlaceTopRight(frame.rectTransform);
        frame.raycastTarget = true;

        Image fill = LanguagePageBuilder.CreateRoundedImage(
            "Fill", frame.transform, 24, LanguagePageBuilder.Pink);
        LanguagePageBuilder.Stretch(fill.rectTransform, new Vector2(5f, 5f));
        fill.raycastTarget = false;

        minusButton = CreateZoomStepper(fill.transform, "Minus", "–", new Vector2(-78f, 0f), ZoomOut);
        LanguagePageBuilder.TextLabel zoomWord = LanguagePageBuilder.CreateLabel(
            "ZoomWord", fill.transform, "ZOOM", 16);
        RectTransform wordRect = zoomWord.Rect;
        wordRect.anchorMin = wordRect.anchorMax = new Vector2(0.5f, 0.5f);
        wordRect.pivot = new Vector2(0.5f, 0.5f);
        wordRect.anchoredPosition = Vector2.zero;
        wordRect.sizeDelta = new Vector2(88f, 40f);
        if (zoomWord.Label != null)
            MorphicText.FitButton(zoomWord.Label);
        plusButton = CreateZoomStepper(fill.transform, "Plus", "+", new Vector2(78f, 0f), ZoomIn);
        WireButtons();
    }

    private static Button CreateZoomStepper(
        Transform parent,
        string name,
        string symbol,
        Vector2 pos,
        UnityEngine.Events.UnityAction click)
    {
        Sprite circle = LanguagePageBuilder.Circle(128);
        Image disc = LanguagePageBuilder.CreateImage(name, parent, circle);
        RectTransform rect = disc.rectTransform;
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = pos;
        rect.sizeDelta = new Vector2(42f, 42f);
        disc.color = LanguagePageBuilder.Green;
        disc.raycastTarget = true;
        disc.preserveAspect = true;

        Image core = LanguagePageBuilder.CreateImage("Core", disc.transform, circle);
        LanguagePageBuilder.Stretch(core.rectTransform, new Vector2(5f, 5f));
        core.color = LanguagePageBuilder.Pink;
        core.raycastTarget = false;
        core.preserveAspect = true;

        LanguagePageBuilder.TextLabel glyph = LanguagePageBuilder.CreateLabel(
            "Glyph", disc.transform, symbol, 26);
        LanguagePageBuilder.Stretch(glyph.Rect);
        if (glyph.Label != null)
            MorphicText.FitButton(glyph.Label);

        Button button = disc.gameObject.AddComponent<Button>();
        button.targetGraphic = disc;
        button.colors = LanguagePageBuilder.ButtonColors();
        button.onClick.AddListener(click);
        DialogueButtonClickSound.Instance?.RegisterButton(button);
        return button;
    }

    private void WireButtons()
    {
        if (minusButton != null)
        {
            minusButton.onClick.RemoveAllListeners();
            minusButton.onClick.AddListener(ZoomOut);
        }
        if (plusButton != null)
        {
            plusButton.onClick.RemoveAllListeners();
            plusButton.onClick.AddListener(ZoomIn);
        }
    }

    private void ZoomIn()
    {
        NudgeZoom(1);
    }

    private void ZoomOut()
    {
        NudgeZoom(-1);
    }

    private void NudgeZoom(int delta)
    {
        if (vertexSculptOwnsCamera)
        {
            int nextExtra = Mathf.Clamp(extraZoomLevel + delta, 0, MaxZoomLevel);
            if (nextExtra == extraZoomLevel)
                return;
            extraZoomLevel = nextExtra;
            ApplyVertexExtraZoom(extraZoomLevel / (float)MaxZoomLevel);
            return;
        }

        Camera view = ResolveViewCamera();
        if (view == null)
            return;

        if (!hasRestPose)
            CaptureRestPose(view);

        int next = Mathf.Clamp(zoomLevel + delta, 0, MaxZoomLevel);
        if (next == zoomLevel)
            return;

        zoomLevel = next;
        zoomed = zoomLevel > 0;
        float t = zoomLevel / (float)MaxZoomLevel;
        Vector3 targetPos = TryGetActiveCraftBounds(out Bounds bounds)
            ? FrameTowardCraft(restPosition, restRotation, bounds, t)
            : DollyAlongView(view, restPosition, t);
        StartMove(view, targetPos, restRotation, view.fieldOfView);
    }

    public static Vector3 DollyAlongView(Camera cam, Vector3 from, float closer01)
    {
        if (cam == null)
            return from;
        return from + LevelViewDolly(cam) * (1.6f * Mathf.Clamp01(closer01));
    }

    public static Vector3 DollyAlongView(Camera cam, Vector3 from, float closer01, Bounds bounds)
    {
        Quaternion rotation = cam != null ? cam.transform.rotation : Quaternion.identity;
        return FrameTowardCraft(from, rotation, bounds, closer01);
    }

    public static Vector3 FrameTowardCraft(Camera cam, Vector3 from, Bounds bounds, float closer01)
    {
        Quaternion rotation = cam != null ? cam.transform.rotation : Quaternion.identity;
        return FrameTowardCraft(from, rotation, bounds, closer01);
    }

    public static Vector3 FrameTowardCraft(
        Vector3 from,
        Quaternion rotation,
        Bounds bounds,
        float closer01)
    {
        closer01 = Mathf.Clamp01(closer01);
        Vector3 focus = bounds.center;
        Vector3 dir = DirectionTowardCraft(rotation, from, focus);
        float depth = Vector3.Dot(focus - from, dir);
        if (depth < 0.3f)
            depth = Mathf.Max(0.3f, Vector3.Distance(from, focus));
        return from + dir * (depth * 0.70f * closer01);
    }

    public static Vector3 CenterOnCraft(Camera cam, Vector3 from, Bounds bounds, float distance)
    {
        Quaternion rotation = cam != null ? cam.transform.rotation : Quaternion.identity;
        return CenterOnCraft(from, rotation, bounds, distance);
    }

    public static Vector3 CenterOnCraft(
        Vector3 from,
        Quaternion rotation,
        Bounds bounds,
        float distance)
    {
        Vector3 focus = bounds.center;
        Vector3 dir = DirectionTowardCraft(rotation, from, focus);
        return focus - dir * Mathf.Max(0.2f, distance);
    }

    private static Vector3 DirectionTowardCraft(Quaternion rotation, Vector3 from, Vector3 focus)
    {
        Vector3 up = rotation * Vector3.up;
        Vector3 visualUp = Mathf.Abs(up.z) >= Mathf.Abs(up.y)
            ? new Vector3(0f, 0f, 1f)
            : Vector3.up;
        Vector3 toCraft = Vector3.ProjectOnPlane(focus - from, visualUp);
        if (toCraft.sqrMagnitude < 0.0001f)
            return LevelViewDolly(rotation);
        return toCraft.normalized;
    }

    public static Vector3 ApproachCraft(Vector3 from, Bounds bounds, float closer01)
    {
        return DollyAlongView(Camera.main, from, closer01, bounds);
    }

    /// <summary>
    /// Same look-dolly as before, with the table-dip removed. This camera is
    /// rolled 90°, so raw forward has a screen-down component; flatten that
    /// off so plus still moves into the craft instead of onto the table.
    /// </summary>
    private static Vector3 LevelViewDolly(Camera cam)
    {
        if (cam == null)
            return Vector3.forward;
        return LevelViewDolly(cam.transform.rotation);
    }

    private static Vector3 LevelViewDolly(Quaternion rotation)
    {
        Vector3 forward = rotation * Vector3.forward;
        Vector3 up = rotation * Vector3.up;
        Vector3 visualUp = Mathf.Abs(up.z) >= Mathf.Abs(up.y)
            ? new Vector3(0f, 0f, 1f)
            : Vector3.up;
        Vector3 dir = Vector3.ProjectOnPlane(forward, visualUp);
        if (dir.sqrMagnitude < 0.0001f)
            return forward;
        return dir.normalized;
    }

    private static void ApplyVertexExtraZoom(float closer01)
    {
        SculptingEnhancementLanternColorController lantern =
            FindFirstObjectByType<SculptingEnhancementLanternColorController>(
                FindObjectsInactive.Include);
        if (lantern != null && lantern.isActiveAndEnabled)
            lantern.ApplyExtraSculptZoom(closer01);

        SculptingEnhancementTeapotColorController teapot =
            FindFirstObjectByType<SculptingEnhancementTeapotColorController>(
                FindObjectsInactive.Include);
        if (teapot != null && teapot.isActiveAndEnabled)
            teapot.ApplyExtraSculptZoom(closer01);

        SculptingEnhancementKerisColorController keris =
            FindFirstObjectByType<SculptingEnhancementKerisColorController>(
                FindObjectsInactive.Include);
        if (keris != null && keris.isActiveAndEnabled)
            keris.ApplyExtraSculptZoom(closer01);
    }

    private void RestoreRestPose(bool animate)
    {
        StopMove();
        if (!zoomed)
            return;

        zoomed = false;
        zoomLevel = 0;

        Camera view = ResolveViewCamera();
        if (view == null || !hasRestPose)
            return;

        if (animate)
        {
            StartMove(view, restPosition, restRotation, restFieldOfView);
            return;
        }

        view.transform.SetPositionAndRotation(restPosition, restRotation);
        if (restFieldOfView > 0.01f)
            view.fieldOfView = restFieldOfView;
        if (restNearClip > 0.001f)
            view.nearClipPlane = restNearClip;
    }

    private void CaptureRestPose(Camera view)
    {
        restPosition = view.transform.position;
        restRotation = view.transform.rotation;
        restFieldOfView = view.fieldOfView;
        restNearClip = view.nearClipPlane;
        hasRestPose = true;
    }

    private void StartMove(
        Camera view,
        Vector3 targetPosition,
        Quaternion targetRotation,
        float targetFieldOfView)
    {
        StopMove();
        moveRoutine = StartCoroutine(
            MoveCamera(view, targetPosition, targetRotation, targetFieldOfView));
    }

    private void StopMove()
    {
        if (moveRoutine == null)
            return;
        StopCoroutine(moveRoutine);
        moveRoutine = null;
    }

    private IEnumerator MoveCamera(
        Camera view,
        Vector3 targetPosition,
        Quaternion targetRotation,
        float targetFieldOfView)
    {
        Transform camTransform = view.transform;
        Vector3 fromPosition = camTransform.position;
        Quaternion fromRotation = camTransform.rotation;
        float fromFieldOfView = view.fieldOfView;
        float elapsed = 0f;

        while (elapsed < MoveDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / MoveDuration);
            float eased = t * t * (3f - 2f * t);
            camTransform.position = Vector3.Lerp(fromPosition, targetPosition, eased);
            camTransform.rotation = Quaternion.Slerp(fromRotation, targetRotation, eased);
            if (targetFieldOfView > 0.01f)
                view.fieldOfView = Mathf.Lerp(fromFieldOfView, targetFieldOfView, eased);
            yield return null;
        }

        camTransform.SetPositionAndRotation(targetPosition, targetRotation);
        if (targetFieldOfView > 0.01f)
            view.fieldOfView = targetFieldOfView;
        moveRoutine = null;
    }

    private static bool TryGetZoomTarget(
        Camera view,
        out Vector3 zoomPosition,
        out Quaternion zoomRotation)
    {
        zoomRotation = view.transform.rotation;

        CraftKind craft = GetActiveCraftKind();
        if (craft == CraftKind.Lantern)
        {
            zoomPosition = LanternZoomPos;
            zoomRotation = Quaternion.Euler(LanternZoomEuler);
            return true;
        }

        if (craft == CraftKind.Teapot)
        {
            zoomPosition = TeapotZoomPos;
            zoomRotation = Quaternion.Euler(TeapotZoomEuler);
            return true;
        }

        if (craft == CraftKind.Keris)
        {
            zoomPosition = KerisZoomPos;
            zoomRotation = Quaternion.Euler(KerisZoomEuler);
            return true;
        }

        Vector3 position = view.transform.position;
        Vector3 forward = view.transform.forward;
        forward.y = 0f;
        if (forward.sqrMagnitude < 0.0001f)
            forward = view.transform.forward;
        else
            forward.Normalize();

        float pullFraction = ZoomPullFraction;
        float minPull = MinPull;
        float maxPull = MaxPull;

        float pull = Mathf.Clamp(0.55f, minPull, maxPull);
        if (TryGetActiveCraftBounds(out Bounds bounds))
        {
            Vector3 toCraft = bounds.center - position;
            toCraft.y = 0f;
            float horizontal = toCraft.magnitude;
            if (horizontal > 0.2f)
                pull = Mathf.Clamp(horizontal * pullFraction, minPull, maxPull);
        }

        zoomPosition = position + forward * pull;
        zoomPosition.y = position.y;
        return true;
    }

    private static bool TryGetActiveCraftBounds(out Bounds bounds)
    {
        Transform craft = FindActiveCraft();
        if (craft == null)
        {
            bounds = default;
            return false;
        }

        Renderer[] renderers = craft.GetComponentsInChildren<Renderer>(false);
        bool hasBounds = false;
        bounds = default;
        for (int i = 0; i < renderers.Length; i++)
        {
            Renderer renderer = renderers[i];
            if (renderer == null || !renderer.enabled)
                continue;
            if (!(renderer is MeshRenderer) && !(renderer is SkinnedMeshRenderer))
                continue;

            if (!hasBounds)
            {
                bounds = renderer.bounds;
                hasBounds = true;
            }
            else
            {
                bounds.Encapsulate(renderer.bounds);
            }
        }

        return hasBounds;
    }

    private enum CraftKind
    {
        None,
        Lantern,
        Teapot,
        Keris
    }

    private static CraftKind GetActiveCraftKind()
    {
        LanternFistRotationController lantern =
            FindFirstObjectByType<LanternFistRotationController>(FindObjectsInactive.Exclude);
        if (lantern != null && lantern.isActiveAndEnabled)
            return CraftKind.Lantern;

        TeapotFistRotationController teapot =
            FindFirstObjectByType<TeapotFistRotationController>(FindObjectsInactive.Exclude);
        if (teapot != null && teapot.isActiveAndEnabled)
            return CraftKind.Teapot;

        KerisFistRotationController keris =
            FindFirstObjectByType<KerisFistRotationController>(FindObjectsInactive.Exclude);
        if (keris != null && keris.isActiveAndEnabled)
            return CraftKind.Keris;

        return CraftKind.None;
    }

    private static Transform FindActiveCraft()
    {
        LanternFistRotationController lantern =
            FindFirstObjectByType<LanternFistRotationController>(FindObjectsInactive.Exclude);
        if (lantern != null && lantern.isActiveAndEnabled)
            return lantern.transform;

        TeapotFistRotationController teapot =
            FindFirstObjectByType<TeapotFistRotationController>(FindObjectsInactive.Exclude);
        if (teapot != null && teapot.isActiveAndEnabled)
            return teapot.transform;

        KerisFistRotationController keris =
            FindFirstObjectByType<KerisFistRotationController>(FindObjectsInactive.Exclude);
        if (keris != null && keris.isActiveAndEnabled)
            return keris.transform;

        return null;
    }

    private static Camera ResolveViewCamera()
    {
        MainMenuStartTransition transition =
            FindFirstObjectByType<MainMenuStartTransition>(FindObjectsInactive.Include);
        if (transition != null && transition.ViewCamera != null)
            return transition.ViewCamera;

        Camera main = Camera.main;
        if (main != null)
            return main;

        return FindFirstObjectByType<Camera>(FindObjectsInactive.Exclude);
    }

    private static void PlaceTopRight(RectTransform button)
    {
        if (button == null)
            return;

        button.anchorMin = button.anchorMax = new Vector2(1f, 1f);
        button.pivot = new Vector2(1f, 1f);
        button.anchoredPosition = new Vector2(-24f, -86f);
        button.sizeDelta = new Vector2(236f, 56f);
    }

    private static void StretchFull(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
    }
}
