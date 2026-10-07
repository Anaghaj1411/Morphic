using UnityEngine;
using UnityEngine.InputSystem;

public class MenuButtonHover : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private RectTransform hoverArea;
    [SerializeField] private RectTransform visualTarget;

    [Header("Hover")]
    [SerializeField] private float hoverScale = 1.06f;
    [SerializeField] private float hoverSpeed = 12f;

    [Header("Pressed")]
    [SerializeField] private float pressedScale = 0.90f;
    [SerializeField] private float pressSpeed = 35f;
    [SerializeField] private float pressedDuration = 0.20f;

    [Header("Detection Size")]
    [SerializeField, Range(0.4f, 1f)]
    private float detectionWidth = 0.70f;

    [SerializeField, Range(0.4f, 1f)]
    private float detectionHeight = 0.70f;

    private Canvas canvas;

    private Vector3 normalScale;
    private Vector3 targetScale;

    private bool isPressed;
    private float pressedTimer;

    private void Awake()
    {
        canvas = GetComponentInParent<Canvas>();

        if (hoverArea == null)
            hoverArea = GetComponent<RectTransform>();

        if (visualTarget == null)
        {
            // When built from code, the references are assigned just
            // after AddComponent, which runs Awake first. Falling back
            // to this object's own rect keeps hover working.
            visualTarget = GetComponent<RectTransform>();
        }

        if (visualTarget == null)
        {
            Debug.LogError(
                $"MenuButtonHover on {gameObject.name}: " +
                "no RectTransform is available for the Visual Target."
            );

            enabled = false;
            return;
        }

        normalScale = visualTarget.localScale;
        targetScale = normalScale;
    }

    private void Update()
    {
        if (hoverArea == null || visualTarget == null)
            return;

        if (Mouse.current == null)
            return;

        Camera uiCamera = null;

        if (canvas != null &&
            canvas.renderMode != RenderMode.ScreenSpaceOverlay)
        {
            uiCamera = canvas.worldCamera;
        }

        Vector2 mousePosition =
            Mouse.current.position.ReadValue();

        bool isHovering =
            IsMouseInsideReducedArea(mousePosition, uiCamera);

        // Detect mouse press directly.
        if (Mouse.current.leftButton.wasPressedThisFrame &&
            isHovering)
        {
            isPressed = true;
            pressedTimer = pressedDuration;
        }

        // Keep the press animation alive long enough to actually see.
        if (isPressed)
        {
            pressedTimer -= Time.unscaledDeltaTime;

            targetScale = normalScale * pressedScale;

            if (pressedTimer <= 0f)
            {
                isPressed = false;
            }
        }
        else
        {
            targetScale = isHovering
                ? normalScale * hoverScale
                : normalScale;
        }

        float speed = isPressed
            ? pressSpeed
            : hoverSpeed;

        float t = 1f - Mathf.Exp(
            -speed * Time.unscaledDeltaTime
        );

        visualTarget.localScale = Vector3.Lerp(
            visualTarget.localScale,
            targetScale,
            t
        );
    }

    private bool IsMouseInsideReducedArea(
        Vector2 screenPosition,
        Camera uiCamera)
    {
        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
                hoverArea,
                screenPosition,
                uiCamera,
                out Vector2 localMouse))
        {
            return false;
        }

        Rect rect = hoverArea.rect;

        float halfWidth =
            rect.width * detectionWidth * 0.5f;

        float halfHeight =
            rect.height * detectionHeight * 0.5f;

        return Mathf.Abs(localMouse.x) <= halfWidth &&
               Mathf.Abs(localMouse.y) <= halfHeight;
    }

    private void OnDisable()
    {
        if (visualTarget != null)
            visualTarget.localScale = normalScale;
    }
}