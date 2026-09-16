using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(MeshFilter))]
public class PinchSculptBrush : MonoBehaviour
{
    [Header("Tracking")]
    [SerializeField] private PinchDetector pinchDetector;

    [Header("Brush")]
    [SerializeField] private float brushRadius = 0.30f;
    [SerializeField] private float sculptSpeed = 0.18f;

    [Header("Hand Mapping")]
    [Range(0.1f, 0.95f)]
    [SerializeField] private float handMappingRange = 0.95f;

    [Header("Brush Cursor")]
    [SerializeField] private bool showBrushCursor = true;
    [SerializeField] private float cursorSize = 0.12f;
    [SerializeField] private float cursorHoldSeconds = 0.80f;

    private Mesh sculptMesh;
    private Vector3[] vertices;
    private List<int[]> weldedVertexGroups;
    private GameObject brushCursor;

    private bool hasLastCursorPosition;
    private Vector3 lastCursorPosition;
    private Vector3 lastCursorNormal;
    private float lastTrackingTime;

    private void Awake()
    {
        sculptMesh = GetComponent<MeshFilter>().mesh;
        vertices = sculptMesh.vertices;

        BuildWeldedVertexGroups();

        if (pinchDetector == null)
        {
            pinchDetector = FindFirstObjectByType<PinchDetector>();
        }

        CreateBrushCursor();
    }

    [SerializeField] private TwoHandScaleSculpture twoHandScale;

    private void Update()
    {
        if (pinchDetector == null)
        {
            pinchDetector = FindFirstObjectByType<PinchDetector>();
        }

        if (twoHandScale == null)
        {
            twoHandScale = GetComponent<TwoHandScaleSculpture>();
        }

        if (twoHandScale != null && twoHandScale.IsDualPinching)
        {
            SetCursorVisible(false);
            return;
        }

        if (pinchDetector != null && pinchDetector.IsTracking)
        {
            GetBrushPoint(
                out Vector3 worldBrushPosition,
                out Vector3 worldBrushNormal
            );

            lastCursorPosition = worldBrushPosition;
            lastCursorNormal = worldBrushNormal;
            lastTrackingTime = Time.unscaledTime;
            hasLastCursorPosition = true;

            UpdateBrushCursor(
                worldBrushPosition,
                worldBrushNormal
            );

            if (pinchDetector.IsPinching)
            {
                InflateAt(worldBrushPosition);
            }

            return;
        }

        if (hasLastCursorPosition &&
            Time.unscaledTime - lastTrackingTime <= cursorHoldSeconds)
        {
            UpdateBrushCursor(
                lastCursorPosition,
                lastCursorNormal
            );
        }
        else
        {
            SetCursorVisible(false);
        }
    }

    private void GetBrushPoint(
        out Vector3 worldPosition,
        out Vector3 worldNormal
    )
    {
        Vector3 indexTip = pinchDetector.SmoothedIndexTip;

        Vector2 handPosition = new Vector2(
            (indexTip.x - 0.5f) * 2f,
            (indexTip.y - 0.5f) * 2f
        ) * handMappingRange;

        handPosition.x = Mathf.Clamp(handPosition.x, -1f, 1f);
        handPosition.y = Mathf.Clamp(handPosition.y, -1f, 1f);

        float mappedX = handPosition.x *
            Mathf.Sqrt(1f - handPosition.y * handPosition.y * 0.5f);

        float mappedY = handPosition.y *
            Mathf.Sqrt(1f - handPosition.x * handPosition.x * 0.5f);

        handPosition = new Vector2(mappedX, mappedY);

        Camera mainCamera = Camera.main;

        if (mainCamera == null)
        {
            worldPosition = transform.position;
            worldNormal = Vector3.forward;
            return;
        }

        float meshRadius =
            sculptMesh.bounds.extents.x * transform.lossyScale.x;

        float depth = Mathf.Sqrt(
            Mathf.Max(0f, 1f - handPosition.sqrMagnitude)
        ) * meshRadius;

        worldPosition =
            transform.position +
            mainCamera.transform.right *
                (handPosition.x * meshRadius) +
            mainCamera.transform.up *
                (handPosition.y * meshRadius) -
            mainCamera.transform.forward * depth;

        worldNormal =
            (worldPosition - transform.position).normalized;
    }

    private void BuildWeldedVertexGroups()
    {
        Dictionary<Vector3Int, List<int>> groups =
            new Dictionary<Vector3Int, List<int>>();

        for (int i = 0; i < vertices.Length; i++)
        {
            Vector3 vertex = vertices[i];

            Vector3Int key = new Vector3Int(
                Mathf.RoundToInt(vertex.x * 100000f),
                Mathf.RoundToInt(vertex.y * 100000f),
                Mathf.RoundToInt(vertex.z * 100000f)
            );

            if (!groups.ContainsKey(key))
            {
                groups[key] = new List<int>();
            }

            groups[key].Add(i);
        }

        weldedVertexGroups = new List<int[]>();

        foreach (List<int> group in groups.Values)
        {
            weldedVertexGroups.Add(group.ToArray());
        }
    }

    private void InflateAt(Vector3 worldBrushPosition)
    {
        Vector3 localBrushPosition =
            transform.InverseTransformPoint(worldBrushPosition);

        float localRadius =
            brushRadius / transform.lossyScale.x;

        float localStrength =
            sculptSpeed * Time.deltaTime /
            transform.lossyScale.x;

        Vector3[] normals = sculptMesh.normals;

        foreach (int[] group in weldedVertexGroups)
        {
            int firstVertexIndex = group[0];

            float distance = Vector3.Distance(
                vertices[firstVertexIndex],
                localBrushPosition
            );

            if (distance > localRadius)
            {
                continue;
            }

            Vector3 averagedNormal = Vector3.zero;

            foreach (int vertexIndex in group)
            {
                averagedNormal += normals[vertexIndex];
            }

            averagedNormal.Normalize();

            float falloff = 1f - distance / localRadius;

            float displacement =
                localStrength * falloff * falloff;

            Vector3 movement =
                averagedNormal * displacement;

            foreach (int vertexIndex in group)
            {
                vertices[vertexIndex] += movement;
            }
        }

        sculptMesh.vertices = vertices;
        sculptMesh.RecalculateNormals();
        sculptMesh.RecalculateBounds();
    }

    private void CreateBrushCursor()
    {
        brushCursor = GameObject.CreatePrimitive(
            PrimitiveType.Sphere
        );

        brushCursor.name = "Brush Cursor";

        Collider cursorCollider =
            brushCursor.GetComponent<Collider>();

        if (cursorCollider != null)
        {
            Destroy(cursorCollider);
        }

        brushCursor.transform.localScale =
            Vector3.one * cursorSize;

        Renderer cursorRenderer =
            brushCursor.GetComponent<Renderer>();

        Shader cursorShader =
            Shader.Find("Universal Render Pipeline/Lit");

        if (cursorShader != null)
        {
            Material cursorMaterial =
                new Material(cursorShader);

            cursorMaterial.SetColor(
                "_BaseColor",
                Color.cyan
            );

            cursorRenderer.material = cursorMaterial;
        }

        SetCursorVisible(false);
    }

    private void UpdateBrushCursor(
        Vector3 worldPosition,
        Vector3 worldNormal
    )
    {
        if (!showBrushCursor)
        {
            SetCursorVisible(false);
            return;
        }

        Camera mainCamera = Camera.main;

        if (mainCamera == null)
        {
            SetCursorVisible(false);
            return;
        }

        Vector3 towardCamera =
            (mainCamera.transform.position - worldPosition)
                .normalized;

        brushCursor.transform.position =
            worldPosition + towardCamera * 0.12f;

        SetCursorVisible(true);
    }

    private void SetCursorVisible(bool isVisible)
    {
        if (brushCursor != null &&
            brushCursor.activeSelf != isVisible)
        {
            brushCursor.SetActive(isVisible);
        }
    }

    private void OnDestroy()
    {
        if (brushCursor != null)
        {
            Destroy(brushCursor);
        }
    }
}