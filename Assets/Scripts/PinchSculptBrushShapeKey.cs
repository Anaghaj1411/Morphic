using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(MeshFilter))]
public class PinchSculptBrushShapeKey : MonoBehaviour
{
    [Header("Sculpt Mode")]
    [SerializeField] private SculptToolMode sculptMode =
        SculptToolMode.Inflate;

    [Header("Tool Strength")]
    [Range(0.01f, 1f)]
    [SerializeField] private float inflateStrength = 0.18f;

    [Range(0.01f, 1f)]
    [SerializeField] private float indentStrength = 0.18f;

    [Range(0.01f, 1f)]
    [SerializeField] private float smoothStrength = 0.30f;

    [Range(0.01f, 1f)]
    [SerializeField] private float grabStrength = 1.0f;

    [Range(0.01f, 1f)]
    [SerializeField] private float flattenStrength = 0.30f;

    [SerializeField] private Color inflateCursorColor =
        Color.cyan;

    [SerializeField] private Color indentCursorColor =
        new Color(1f, 0.45f, 0f);

    [SerializeField] private Color smoothCursorColor =
        new Color(0.65f, 0.25f, 0.95f);

    [SerializeField] private Color grabCursorColor =
        new Color(0.2f, 0.85f, 0.3f);

    [SerializeField] private Color flattenCursorColor =
        new Color(1f, 0.2f, 0.65f);

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
    [SerializeField] private float cursorCameraOffset = 0.75f;
    [SerializeField] private float cursorHoldSeconds = 0.80f;

    [Header("Symmetry")]
    [SerializeField] private bool enableSymmetry = false;

    [Header("Other Controls")]
    [SerializeField] private TwoHandScaleSculpture twoHandScale;
    [SerializeField] private MeshHistory meshHistory;

    private Mesh sculptMesh;
    private Vector3[] vertices;
    private Vector3[] initialVertices;
    private List<int[]> weldedVertexGroups;

    private GameObject brushCursor;
    private GameObject mirroredBrushCursor;
    private Material cursorMaterial;

    private bool hasLastCursorPosition;
    private Vector3 lastCursorPosition;
    private Vector3 lastCursorNormal;
    private float lastTrackingTime;

    private Vector3 prevPrimaryLocalPos;
    private bool wasPinching;

    public SculptToolMode CurrentSculptMode => sculptMode;
    public bool EnableSymmetry => enableSymmetry;
    public float BrushRadius => brushRadius;

    public void SetBrushRadius(float radius)
    {
        brushRadius = Mathf.Clamp(radius, 0.05f, 1.0f);
    }

    public void AdjustBrushRadius(float delta)
    {
        SetBrushRadius(brushRadius + delta);
    }

    public float GetCurrentToolStrength()
    {
        switch (sculptMode)
        {
            case SculptToolMode.Inflate:
                return inflateStrength;
            case SculptToolMode.Indent:
                return indentStrength;
            case SculptToolMode.Smooth:
                return smoothStrength;
            case SculptToolMode.Grab:
                return grabStrength;
            case SculptToolMode.Flatten:
                return flattenStrength;
            default:
                return inflateStrength;
        }
    }

    public void AdjustCurrentToolStrength(float amount)
    {
        switch (sculptMode)
        {
            case SculptToolMode.Inflate:
                inflateStrength =
                    Mathf.Clamp(inflateStrength + amount, 0.01f, 1f);
                break;

            case SculptToolMode.Indent:
                indentStrength =
                    Mathf.Clamp(indentStrength + amount, 0.01f, 1f);
                break;

            case SculptToolMode.Smooth:
                smoothStrength =
                    Mathf.Clamp(smoothStrength + amount, 0.01f, 1f);
                break;

            case SculptToolMode.Grab:
                grabStrength =
                    Mathf.Clamp(grabStrength + amount, 0.01f, 1f);
                break;

            case SculptToolMode.Flatten:
                flattenStrength =
                    Mathf.Clamp(flattenStrength + amount, 0.01f, 1f);
                break;
        }
    }

    public void RefreshMesh(Mesh newMesh)
{
    if (newMesh == null)
    {
        return;
    }

    sculptMesh = newMesh;
    vertices = sculptMesh.vertices;

    initialVertices = new Vector3[vertices.Length];

    System.Array.Copy(
        vertices,
        initialVertices,
        vertices.Length
    );

    BuildWeldedVertexGroups();

    prevPrimaryLocalPos = Vector3.zero;
    wasPinching = false;

    Debug.Log(
        "Sculpt brush refreshed for mesh: " +
        sculptMesh.name
    );
}

    private void Awake()
    {
        enableSymmetry = false;

        sculptMesh = GetComponent<MeshFilter>().mesh;
        vertices = sculptMesh.vertices;

        initialVertices = new Vector3[vertices.Length];

        System.Array.Copy(
            vertices,
            initialVertices,
            vertices.Length
        );

        BuildWeldedVertexGroups();

        if (pinchDetector == null)
        {
            pinchDetector =
                FindFirstObjectByType<PinchDetector>();
        }

        if (twoHandScale == null)
        {
            twoHandScale =
                GetComponent<TwoHandScaleSculpture>();
        }

        if (meshHistory == null)
        {
            meshHistory = GetComponent<MeshHistory>();
        }

        CreateBrushCursor();
    }

    private void Update()
    {
        CheckKeyboardModeSwitch();

        if (pinchDetector == null)
        {
            pinchDetector =
                FindFirstObjectByType<PinchDetector>();
        }

        if (twoHandScale == null)
        {
            twoHandScale =
                GetComponent<TwoHandScaleSculpture>();
        }

        if (meshHistory == null)
        {
            meshHistory = GetComponent<MeshHistory>();
        }

        if (twoHandScale != null &&
            twoHandScale.IsDualPinching)
        {
            wasPinching = false;
            SetCursorVisible(false);
            return;
        }

        if (pinchDetector != null &&
            pinchDetector.IsTracking)
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
                if (!wasPinching &&
                    meshHistory != null)
                {
                    meshHistory.SaveState();
                }

                SculptAt(worldBrushPosition);
                wasPinching = true;
            }
            else
            {
                wasPinching = false;
            }

            return;
        }

        wasPinching = false;

        if (hasLastCursorPosition &&
            Time.unscaledTime - lastTrackingTime <=
            cursorHoldSeconds)
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

    public void SetSculptMode(SculptToolMode mode)
    {
        sculptMode = mode;
        UpdateCursorColor();
    }

    public void ToggleSculptMode()
    {
        if (sculptMode == SculptToolMode.Inflate)
        {
            sculptMode = SculptToolMode.Indent;
        }
        else if (sculptMode == SculptToolMode.Indent)
        {
            sculptMode = SculptToolMode.Smooth;
        }
        else if (sculptMode == SculptToolMode.Smooth)
        {
            sculptMode = SculptToolMode.Grab;
        }
        else if (sculptMode == SculptToolMode.Grab)
        {
            sculptMode = SculptToolMode.Flatten;
        }
        else
        {
            sculptMode = SculptToolMode.Inflate;
        }

        UpdateCursorColor();
    }

    public void ToggleSymmetry()
    {
        enableSymmetry = !enableSymmetry;
    }

    public void ResetMesh()
    {
        if (initialVertices == null ||
            vertices == null)
        {
            return;
        }

        System.Array.Copy(
            initialVertices,
            vertices,
            initialVertices.Length
        );

        sculptMesh.vertices = vertices;
        sculptMesh.RecalculateNormals();
        sculptMesh.RecalculateBounds();
    }

    private void CheckKeyboardModeSwitch()
    {
        if (Input.GetKeyDown(KeyCode.Alpha1))
        {
            SetSculptMode(SculptToolMode.Inflate);
        }
        else if (Input.GetKeyDown(KeyCode.Alpha2))
        {
            SetSculptMode(SculptToolMode.Indent);
        }
        else if (Input.GetKeyDown(KeyCode.Alpha3))
        {
            SetSculptMode(SculptToolMode.Smooth);
        }
        else if (Input.GetKeyDown(KeyCode.Alpha4))
        {
            SetSculptMode(SculptToolMode.Grab);
        }
        else if (Input.GetKeyDown(KeyCode.Alpha5))
        {
            SetSculptMode(SculptToolMode.Flatten);
        }
        else if (Input.GetKeyDown(KeyCode.LeftBracket))
        {
            AdjustBrushRadius(-0.05f);
        }
        else if (Input.GetKeyDown(KeyCode.RightBracket))
        {
            AdjustBrushRadius(0.05f);
        }
        else if (Input.GetKeyDown(KeyCode.Tab))
        {
            ToggleSculptMode();
        }
        else if (Input.GetKeyDown(KeyCode.S))
        {
            ToggleSymmetry();
        }
        else if (Input.GetKeyDown(KeyCode.R))
        {
            ResetMesh();
        }
    }

    private void GetBrushPoint(
        out Vector3 worldPosition,
        out Vector3 worldNormal
    )
    {
        Vector3 indexTip =
            pinchDetector.SmoothedIndexTip;

        Vector2 handPosition = new Vector2(
            (indexTip.x - 0.5f) * 2f,
            (indexTip.y - 0.5f) * 2f
        ) * handMappingRange;

        handPosition.x =
            Mathf.Clamp(handPosition.x, -1f, 1f);

        handPosition.y =
            Mathf.Clamp(handPosition.y, -1f, 1f);

        float mappedX = handPosition.x *
            Mathf.Sqrt(
                1f -
                handPosition.y * handPosition.y * 0.5f
            );

        float mappedY = handPosition.y *
            Mathf.Sqrt(
                1f -
                handPosition.x * handPosition.x * 0.5f
            );

        handPosition = new Vector2(mappedX, mappedY);

        Camera mainCamera = Camera.main;

        if (mainCamera == null)
        {
            worldPosition = transform.position;
            worldNormal = Vector3.forward;
            return;
        }

        float meshRadius =
            sculptMesh.bounds.extents.x *
            transform.lossyScale.x;

        float depth = Mathf.Sqrt(
            Mathf.Max(
                0f,
                1f - handPosition.sqrMagnitude
            )
        ) * meshRadius;

        worldPosition =
            transform.position +
            mainCamera.transform.right *
                (handPosition.x * meshRadius) +
            mainCamera.transform.up *
                (handPosition.y * meshRadius) -
            mainCamera.transform.forward * depth;

        worldNormal =
            (worldPosition - transform.position)
                .normalized;
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

    private void SculptAt(Vector3 worldBrushPosition)
    {
        Vector3 primaryLocalPos =
            transform.InverseTransformPoint(
                worldBrushPosition
            );

        Vector3 localDelta = Vector3.zero;

        if (wasPinching)
        {
            localDelta =
                primaryLocalPos - prevPrimaryLocalPos;
        }

        ApplySculptPass(primaryLocalPos, localDelta);

        if (enableSymmetry)
        {
            Vector3 mirroredLocalPos = new Vector3(
                -primaryLocalPos.x,
                primaryLocalPos.y,
                primaryLocalPos.z
            );

            Vector3 mirroredLocalDelta = new Vector3(
                -localDelta.x,
                localDelta.y,
                localDelta.z
            );

            ApplySculptPass(
                mirroredLocalPos,
                mirroredLocalDelta
            );
        }

        prevPrimaryLocalPos = primaryLocalPos;

        sculptMesh.vertices = vertices;
        sculptMesh.RecalculateNormals();
        sculptMesh.RecalculateBounds();
    }

    private void ApplySculptPass(
        Vector3 localBrushPosition,
        Vector3 localDelta
    )
    {
        float localRadius =
            brushRadius / transform.lossyScale.x;

        float localStrength =
            sculptSpeed * Time.deltaTime /
            transform.lossyScale.x;

        Vector3[] normals = sculptMesh.normals;

        if (sculptMode == SculptToolMode.Grab)
        {
            if (localDelta == Vector3.zero)
            {
                return;
            }

            foreach (int[] group in weldedVertexGroups)
            {
                int firstVertexIndex = group[0];

                Vector3 currentPos =
                    vertices[firstVertexIndex];

                float distance = Vector3.Distance(
                    currentPos,
                    localBrushPosition
                );

                if (distance > localRadius)
                {
                    continue;
                }

                float falloff =
                    1f - distance / localRadius;

                Vector3 movement =
                    localDelta * falloff * falloff;

                foreach (int vertexIndex in group)
                {
                    vertices[vertexIndex] += movement;
                }
            }

            return;
        }

        if (sculptMode == SculptToolMode.Flatten)
        {
            Vector3 planeCenter = Vector3.zero;
            Vector3 planeNormal = Vector3.zero;
            int affectedGroupCount = 0;

            foreach (int[] group in weldedVertexGroups)
            {
                int firstVertexIndex = group[0];

                Vector3 currentPos =
                    vertices[firstVertexIndex];

                float distance = Vector3.Distance(
                    currentPos,
                    localBrushPosition
                );

                if (distance > localRadius)
                {
                    continue;
                }

                planeCenter += currentPos;

                Vector3 groupNormal = Vector3.zero;

                foreach (int vertexIndex in group)
                {
                    groupNormal += normals[vertexIndex];
                }

                planeNormal += groupNormal.normalized;
                affectedGroupCount++;
            }

            if (affectedGroupCount > 0)
            {
                planeCenter /= affectedGroupCount;
                planeNormal.Normalize();

                if (planeNormal == Vector3.zero)
                {
                    planeNormal = Vector3.up;
                }

                float flattenRate =
                    Mathf.Clamp01(12f * Time.deltaTime);

                foreach (int[] group in weldedVertexGroups)
                {
                    int firstVertexIndex = group[0];

                    Vector3 currentPos =
                        vertices[firstVertexIndex];

                    float distance = Vector3.Distance(
                        currentPos,
                        localBrushPosition
                    );

                    if (distance > localRadius)
                    {
                        continue;
                    }

                    float distanceToPlane = Vector3.Dot(
                        currentPos - planeCenter,
                        planeNormal
                    );

                    Vector3 targetPos =
                        currentPos -
                        planeNormal * distanceToPlane;

                    float falloff =
                        1f - distance / localRadius;

                    float lerpAmount = Mathf.Clamp01(
                        flattenRate * falloff * falloff
                    );

                    Vector3 delta =
                        (targetPos - currentPos) *
                        lerpAmount;

                    foreach (int vertexIndex in group)
                    {
                        vertices[vertexIndex] += delta;
                    }
                }
            }

            return;
        }

        if (sculptMode == SculptToolMode.Smooth)
        {
            float smoothStrength =
                6f * Time.deltaTime;

            foreach (int[] group in weldedVertexGroups)
            {
                int firstVertexIndex = group[0];

                Vector3 currentPos =
                    vertices[firstVertexIndex];

                float distance = Vector3.Distance(
                    currentPos,
                    localBrushPosition
                );

                if (distance > localRadius)
                {
                    continue;
                }

                Vector3 neighborAverage = Vector3.zero;
                int neighborCount = 0;

                foreach (int[] otherGroup in weldedVertexGroups)
                {
                    int otherFirstIndex = otherGroup[0];

                    if (otherFirstIndex == firstVertexIndex)
                    {
                        continue;
                    }

                    float neighborDistance =
                        Vector3.Distance(
                            currentPos,
                            vertices[otherFirstIndex]
                        );

                    if (neighborDistance <
                        localRadius * 0.6f)
                    {
                        neighborAverage +=
                            vertices[otherFirstIndex];

                        neighborCount++;
                    }
                }

                if (neighborCount > 0)
                {
                    neighborAverage /= neighborCount;

                    float falloff =
                        1f - distance / localRadius;

                    float lerpAmount = Mathf.Clamp01(
                        smoothStrength * falloff * falloff
                    );

                    Vector3 targetPos = Vector3.Lerp(
                        currentPos,
                        neighborAverage,
                        lerpAmount
                    );

                    Vector3 delta =
                        targetPos - currentPos;

                    foreach (int vertexIndex in group)
                    {
                        vertices[vertexIndex] += delta;
                    }
                }
            }

            return;
        }

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

            float falloff =
                1f - distance / localRadius;

            float displacement =
                localStrength * falloff * falloff;

            Vector3 direction =
                sculptMode == SculptToolMode.Indent
                    ? -averagedNormal
                    : averagedNormal;

            Vector3 movement =
                direction * displacement;

            foreach (int vertexIndex in group)
            {
                vertices[vertexIndex] += movement;
            }
        }
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

        MeshRenderer renderer =
            brushCursor.GetComponent<MeshRenderer>();

        Shader cursorShader = Shader.Find(
            "Universal Render Pipeline/Lit"
        );

        if (cursorShader != null)
        {
            cursorMaterial = new Material(cursorShader);

            cursorMaterial.renderQueue = 4000;

            cursorMaterial.SetInt(
                "_ZTest",
                (int)UnityEngine.Rendering
                    .CompareFunction.Always
            );

            cursorMaterial.SetFloat(
                "_Smoothness",
                0.8f
            );

            renderer.material = cursorMaterial;
        }

        brushCursor.transform.localScale =
            Vector3.one * cursorSize;

        if (mirroredBrushCursor != null)
        {
            Destroy(mirroredBrushCursor);
        }

        mirroredBrushCursor = Instantiate(brushCursor);

        mirroredBrushCursor.name =
            "Mirrored Brush Cursor";

        mirroredBrushCursor.SetActive(false);

        UpdateCursorColor();
    }

    private void UpdateCursorColor()
    {
        if (cursorMaterial == null)
        {
            return;
        }

        Color targetColor = inflateCursorColor;

        if (sculptMode == SculptToolMode.Indent)
        {
            targetColor = indentCursorColor;
        }
        else if (sculptMode == SculptToolMode.Smooth)
        {
            targetColor = smoothCursorColor;
        }
        else if (sculptMode == SculptToolMode.Grab)
        {
            targetColor = grabCursorColor;
        }
        else if (sculptMode == SculptToolMode.Flatten)
        {
            targetColor = flattenCursorColor;
        }

        cursorMaterial.SetColor(
            "_BaseColor",
            targetColor
        );
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
            (
                mainCamera.transform.position -
                worldPosition
            ).normalized;

        brushCursor.transform.position =
            worldPosition +
            towardCamera *
                (cursorCameraOffset + cursorSize);

        if (enableSymmetry &&
            mirroredBrushCursor != null)
        {
            Vector3 localPosition =
                transform.InverseTransformPoint(
                    worldPosition
                );

            Vector3 mirroredLocalPosition =
                new Vector3(
                    -localPosition.x,
                    localPosition.y,
                    localPosition.z
                );

            Vector3 mirroredWorldPosition =
                transform.TransformPoint(
                    mirroredLocalPosition
                );

            Vector3 mirroredTowardCamera =
                (
                    mainCamera.transform.position -
                    mirroredWorldPosition
                ).normalized;

            mirroredBrushCursor.transform.position =
                mirroredWorldPosition +
                mirroredTowardCamera *
                (cursorCameraOffset + cursorSize);
        }

        UpdateCursorColor();
        SetCursorVisible(true);
    }

    private void SetCursorVisible(bool isVisible)
    {
        if (brushCursor != null &&
            brushCursor.activeSelf != isVisible)
        {
            brushCursor.SetActive(isVisible);
        }

        if (mirroredBrushCursor != null)
        {
            mirroredBrushCursor.SetActive(
                isVisible && enableSymmetry
            );
        }
    }

    private void OnDestroy()
    {
        if (brushCursor != null)
        {
            Destroy(brushCursor);
        }

        if (mirroredBrushCursor != null)
        {
            Destroy(mirroredBrushCursor);
        }
    }
}