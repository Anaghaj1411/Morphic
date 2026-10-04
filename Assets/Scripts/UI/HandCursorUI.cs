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

        Vector2 screenPosition = new Vector2(
            pinchPoint.x * Screen.width,
            pinchPoint.y * Screen.height
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
}