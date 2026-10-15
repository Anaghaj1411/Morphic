using Mediapipe.Unity.Sample.HandLandmarkDetection;
using UnityEngine;

public class HandCursorUI : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private SculptHandLandmarkerRunner handRunner;
    [SerializeField] private RectTransform canvasRect;
    [SerializeField] private RectTransform cursorRect;

    [Header("Cursor Movement")]
    [SerializeField] private float smoothSpeed = 25f;
    [Tooltip("Expands hand motion around the camera center so the cursor can reach every screen edge while the full hand remains visible.")]
    [SerializeField, Range(1f, 2.5f)] private float cameraToScreenRangeMultiplier = 1.8f;

    private Camera canvasCamera;

    private void Awake()
    {
        FindReferences();

        // Keep the cursor as the last UI element so it renders above
        // the other UI elements.
        transform.SetAsLastSibling();
    }

    private void Update()
    {
        FindReferences();

        if (handRunner == null ||
            canvasRect == null ||
            cursorRect == null)
        {
            return;
        }

        if (!handRunner.TryGetLatestHand(out SculptHandFrame hand))
        {
            cursorRect.gameObject.SetActive(false);
            return;
        }

        cursorRect.gameObject.SetActive(true);

        Vector3 pinchPoint =
            (hand.ThumbTip + hand.IndexTip) * 0.5f;

        Vector2 expandedPoint = ExpandCameraPoint(
            new Vector2(pinchPoint.x, pinchPoint.y),
            cameraToScreenRangeMultiplier);

        Vector2 screenPosition = new Vector2(
            expandedPoint.x * Screen.width,
            expandedPoint.y * Screen.height
        );

        if (RectTransformUtility.ScreenPointToLocalPointInRectangle(
                canvasRect,
                screenPosition,
                canvasCamera,
                out Vector2 localPosition))
        {
            cursorRect.anchoredPosition = Vector2.Lerp(
                cursorRect.anchoredPosition,
                localPosition,
                1f - Mathf.Exp(-smoothSpeed * Time.deltaTime)
            );
        }
    }

    private void FindReferences()
    {
        if (handRunner == null)
        {
            handRunner =
                FindFirstObjectByType<SculptHandLandmarkerRunner>();
        }

        if (canvasRect == null)
        {
            Canvas canvas = GetComponentInParent<Canvas>();

            if (canvas != null)
            {
                canvasRect =
                    canvas.GetComponent<RectTransform>();

                canvasCamera =
                    canvas.renderMode == RenderMode.ScreenSpaceOverlay
                        ? null
                        : canvas.worldCamera;
            }
        }

        if (cursorRect == null)
        {
            cursorRect = GetComponent<RectTransform>();
        }
    }

    private static Vector2 ExpandCameraPoint(
        Vector2 normalizedPoint,
        float multiplier)
    {
        Vector2 centered = normalizedPoint - Vector2.one * 0.5f;
        return new Vector2(
            Mathf.Clamp01(centered.x * multiplier + 0.5f),
            Mathf.Clamp01(centered.y * multiplier + 0.5f));
    }
}
