using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// Pink/green ZOOM pill. Shown after the user picks lantern, teapot, or
/// keris so they can dolly the camera in on the sculpt. Click again to
/// return to the studio framing.
public class MorphicSculptZoomUI : MonoBehaviour
{
    private const float MoveDuration = 0.4f;
    private const float ZoomPullFraction = 0.22f;
    private const float MinPull = 0.28f;
    private const float MaxPull = 0.65f;

    private GameObject root;
    private Button zoomButton;
    private TMP_Text zoomLabel;
    private Coroutine moveRoutine;
    private bool zoomed;
    private bool hasRestPose;
    private Vector3 restPosition;
    private Quaternion restRotation;
    private float restFieldOfView;
    private float restNearClip;

    public bool IsZoomed => zoomed;

    public static MorphicSculptZoomUI FindInstance()
    {
        return FindFirstObjectByType<MorphicSculptZoomUI>(FindObjectsInactive.Include);
    }

    /// <summary>
    /// Vertex sculpt owns the camera now. Keep the studio rest pose for it
    /// and drop the ZOOM button's extra dolly without moving the camera.
    /// </summary>
    public static void HandOffToVertexSculpt(
        ref bool hasStored,
        ref Vector3 storedPosition,
        ref Quaternion storedRotation,
        ref float storedFieldOfView,
        ref float storedNearClip)
    {
        MorphicSculptZoomUI zoomUi = FindInstance();
        if (zoomUi == null)
            return;

        if (!hasStored && zoomUi.hasRestPose)
        {
            storedPosition = zoomUi.restPosition;
            storedRotation = zoomUi.restRotation;
            storedFieldOfView = zoomUi.restFieldOfView;
            storedNearClip = zoomUi.restNearClip;
            hasStored = true;
        }

        zoomUi.ReleaseWithoutRestore();
    }

    public static void ReleaseIfPresent()
    {
        MorphicSculptZoomUI zoomUi = FindInstance();
        if (zoomUi != null)
            zoomUi.ReleaseWithoutRestore();
    }

    public void ReleaseWithoutRestore()
    {
        StopMove();
        zoomed = false;
        hasRestPose = false;
        UpdateLabel();
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
            root.transform.SetAsLastSibling();
        UpdateLabel();
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
            WireButton();
            return;
        }

        Canvas canvas = FindFirstObjectByType<Canvas>();
        if (canvas == null)
            return;

        Transform existing = canvas.transform.Find("MorphicSculptZoom");
        if (existing != null)
        {
            root = existing.gameObject;
            zoomButton = existing.GetComponentInChildren<Button>(true);
            zoomLabel = existing.GetComponentInChildren<TMP_Text>(true);
            Transform existingFrame = existing.Find("Zoom");
            if (existingFrame != null)
                PlaceTopLeft(existingFrame.GetComponent<RectTransform>());
            WireButton();
            return;
        }

        root = new GameObject("MorphicSculptZoom", typeof(RectTransform));
        RectTransform rootRect = root.GetComponent<RectTransform>();
        rootRect.SetParent(canvas.transform, false);
        StretchFull(rootRect);

        Image frame = LanguagePageBuilder.CreateRoundedImage(
            "Zoom", root.transform, 28, LanguagePageBuilder.Green);
        PlaceTopLeft(frame.rectTransform);
        frame.raycastTarget = true;

        Image fill = LanguagePageBuilder.CreateRoundedImage(
            "Fill", frame.transform, 24, LanguagePageBuilder.Pink);
        RectTransform fillRect = fill.rectTransform;
        fillRect.anchorMin = Vector2.zero;
        fillRect.anchorMax = Vector2.one;
        fillRect.offsetMin = new Vector2(5f, 5f);
        fillRect.offsetMax = new Vector2(-5f, -5f);
        fill.raycastTarget = true;

        LanguagePageBuilder.TextLabel label = LanguagePageBuilder.CreateLabel(
            "Label",
            fill.transform,
            "ZOOM",
            18);
        LanguagePageBuilder.Stretch(label.Rect);
        zoomLabel = label.Label;
        if (zoomLabel != null)
        {
            zoomLabel.fontStyle = FontStyles.Bold;
            zoomLabel.characterSpacing = 0.8f;
        }

        zoomButton = frame.gameObject.AddComponent<Button>();
        zoomButton.targetGraphic = fill;
        zoomButton.colors = LanguagePageBuilder.ButtonColors();
        WireButton();
    }

    private void WireButton()
    {
        if (zoomButton == null)
            return;
        zoomButton.onClick.RemoveAllListeners();
        zoomButton.onClick.AddListener(ToggleZoom);
    }

    private void ToggleZoom()
    {
        Camera view = ResolveViewCamera();
        if (view == null)
            return;

        if (zoomed)
        {
            RestoreRestPose(true);
            return;
        }

        if (!TryGetZoomTarget(view, out Vector3 zoomPosition))
            return;

        CaptureRestPose(view);
        zoomed = true;
        UpdateLabel();
        StartMove(view, zoomPosition, view.transform.rotation, view.fieldOfView);
    }

    private void RestoreRestPose(bool animate)
    {
        StopMove();
        if (!zoomed)
            return;

        zoomed = false;
        UpdateLabel();

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

    private static bool TryGetZoomTarget(Camera view, out Vector3 zoomPosition)
    {
        Vector3 position = view.transform.position;
        Vector3 forward = view.transform.forward;
        forward.y = 0f;
        if (forward.sqrMagnitude < 0.0001f)
            forward = view.transform.forward;
        else
            forward.Normalize();

        float pull = 0.45f;
        if (TryGetActiveCraftBounds(out Bounds bounds))
        {
            Vector3 toCraft = bounds.center - position;
            toCraft.y = 0f;
            float horizontal = toCraft.magnitude;
            if (horizontal > 0.2f)
                pull = Mathf.Clamp(horizontal * ZoomPullFraction, MinPull, MaxPull);
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

    private void UpdateLabel()
    {
        if (zoomLabel == null)
            return;
        zoomLabel.text = zoomed ? "ZOOM OUT" : "ZOOM";
    }

    private static void PlaceTopLeft(RectTransform button)
    {
        if (button == null)
            return;

        // Under GESTURES in the top-left cluster beside BACK.
        button.anchorMin = button.anchorMax = new Vector2(0f, 1f);
        button.pivot = new Vector2(0f, 1f);
        button.anchoredPosition = new Vector2(122f, -156f);
        button.sizeDelta = new Vector2(236f, 54f);
    }

    private static void StretchFull(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
    }
}
