using Mediapipe.Unity.Sample.HandLandmarkDetection;
using UnityEngine;

public class ClayColorPainter : MonoBehaviour
{
    [Header("Color Painting")]
    [SerializeField] private Color paintColor = Color.red;
    [SerializeField] private float brushSize = 0.08f;

    [Header("Painting Strength")]
    [SerializeField] private float paintStrength = 1f;

    [Header("Paint Mode")]
    [SerializeField] private bool paintModeActive = false;

    [Header("Hand Tracking")]
    [SerializeField] private SculptHandLandmarkerRunner handRunner;

    [Header("Pinch Detection")]
    [SerializeField] private float pinchStartDistance = 0.055f;
    [SerializeField] private float pinchReleaseDistance = 0.08f;

    [Header("Clay Surface")]
    [SerializeField] private Collider clayCollider;

    [Header("References")]
    [SerializeField] private Camera mainCamera;

    public bool IsPainting { get; private set; }
    public bool IsOverClay { get; private set; }
    public Vector3 PaintPoint { get; private set; }

    private MeshFilter meshFilter;
    private Mesh mesh;
    private Color[] vertexColors;

    private void Awake()
    {
        FindReferences();
        InitializeVertexColors();
    }

    private void Update()
    {
        FindReferences();

        if (handRunner == null ||
            mainCamera == null ||
            clayCollider == null ||
            mesh == null)
        {
            IsPainting = false;
            IsOverClay = false;
            return;
        }

        if (!handRunner.TryGetLatestHand(out SculptHandFrame hand))
        {
            IsPainting = false;
            IsOverClay = false;
            return;
        }

        // Pinch detection with hysteresis.
        float threshold = IsPainting
            ? pinchReleaseDistance
            : pinchStartDistance;

        bool pinching = hand.PinchDistance <= threshold;
        IsPainting = pinching;

        // Middle point between thumb and index finger.
        Vector3 pinchPoint =
            (hand.ThumbTip + hand.IndexTip) * 0.5f;

        // Convert MediaPipe normalized coordinates to a camera ray.
        Ray ray = mainCamera.ViewportPointToRay(
            new Vector3(
                pinchPoint.x,
                pinchPoint.y,
                0f
            )
        );

        if (Physics.Raycast(ray, out RaycastHit hit, 100f))
        {
            if (hit.collider == clayCollider)
            {
                IsOverClay = true;
                PaintPoint = hit.point;

                // Only paint while Paint Mode is active.
                if (paintModeActive && IsPainting)
                {
                    PaintAtPoint(PaintPoint);
                }

                return;
            }
        }

        IsOverClay = false;
    }

    private void InitializeVertexColors()
    {
        if (meshFilter == null)
            return;

        mesh = meshFilter.mesh;

        if (mesh == null)
            return;

        int vertexCount = mesh.vertexCount;

        vertexColors = new Color[vertexCount];

        // Starting clay color.
        Color startingColor = new Color(
            0.65f,
            0.45f,
            0.30f,
            1f
        );

        for (int i = 0; i < vertexColors.Length; i++)
        {
            vertexColors[i] = startingColor;
        }

        mesh.colors = vertexColors;
    }

    private void PaintAtPoint(Vector3 worldPoint)
    {
        if (mesh == null || vertexColors == null)
            return;

        Vector3[] vertices = mesh.vertices;

        float brushRadius = brushSize;
        float brushRadiusSqr = brushRadius * brushRadius;

        Transform meshTransform = meshFilter.transform;

        for (int i = 0; i < vertices.Length; i++)
        {
            Vector3 worldVertex =
                meshTransform.TransformPoint(vertices[i]);

            float distanceSqr =
                (worldVertex - worldPoint).sqrMagnitude;

            if (distanceSqr <= brushRadiusSqr)
            {
                float distance =
                    Mathf.Sqrt(distanceSqr);

                float normalizedDistance =
                    Mathf.Clamp01(distance / brushRadius);

                float influence =
                    1f - normalizedDistance;

                influence *= paintStrength;
                influence = Mathf.Clamp01(influence);

                vertexColors[i] = Color.Lerp(
                    vertexColors[i],
                    paintColor,
                    influence
                );
            }
        }

        mesh.colors = vertexColors;
    }

    public void SetPaintColor(Color newColor)
    {
        paintColor = newColor;
    }

    public Color GetPaintColor()
    {
        return paintColor;
    }

    public void SetPaintMode(bool active)
    {
        paintModeActive = active;
    }

    public bool IsPaintModeActive()
    {
        return paintModeActive;
    }

    private void FindReferences()
    {
        if (handRunner == null)
        {
            handRunner =
                FindFirstObjectByType<SculptHandLandmarkerRunner>();
        }

        if (mainCamera == null)
        {
            mainCamera = Camera.main;
        }

        if (clayCollider == null)
        {
            clayCollider = GetComponent<Collider>();
        }

        if (meshFilter == null)
        {
            meshFilter = GetComponent<MeshFilter>();
        }
    }
}