using UnityEngine;

[ExecuteAlways]
public class WorldReferenceUIAligner : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Camera targetCamera;
    [SerializeField] private Canvas canvas;
    [SerializeField] private MeshFilter worldReference;
    [SerializeField] private RectTransform uiTarget;

    [Header("Position Reference")]
    [Tooltip("ON = use the Blender object's Transform position. OFF = use the mesh center.")]
    [SerializeField] private bool useTransformPosition = false;

    [Header("Options")]
    [SerializeField] private bool updateInEditor = true;

    private void Update()
    {
        if (Application.isPlaying || updateInEditor)
        {
            Align();
        }
    }

    public void Align()
    {
        if (targetCamera == null ||
            canvas == null ||
            worldReference == null ||
            uiTarget == null)
        {
            return;
        }

        Vector3 worldPoint;

        if (useTransformPosition)
        {
            // Use the actual Blender reference object's Transform position.
            worldPoint = worldReference.transform.position;
        }
        else
        {
            // Use the visual center of the mesh.
            Mesh mesh = worldReference.sharedMesh;

            if (mesh == null)
            {
                return;
            }

            worldPoint = worldReference.transform.TransformPoint(mesh.bounds.center);
        }

        Vector3 screenPoint =
            targetCamera.WorldToScreenPoint(worldPoint);

        if (screenPoint.z <= 0f)
        {
            return;
        }

        RectTransform canvasRect =
            canvas.transform as RectTransform;

        if (canvasRect == null)
        {
            return;
        }

        Camera uiCamera =
            canvas.renderMode == RenderMode.ScreenSpaceOverlay
                ? null
                : canvas.worldCamera;

        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
                canvasRect,
                new Vector2(screenPoint.x, screenPoint.y),
                uiCamera,
                out Vector2 localPoint))
        {
            return;
        }

        uiTarget.anchorMin = new Vector2(0.5f, 0.5f);
        uiTarget.anchorMax = new Vector2(0.5f, 0.5f);
        uiTarget.pivot = new Vector2(0.5f, 0.5f);

        // Position only. The UI object's own size is preserved.
        uiTarget.anchoredPosition = localPoint;
        uiTarget.localRotation = Quaternion.identity;
    }
}