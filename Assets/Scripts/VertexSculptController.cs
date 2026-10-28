using System.Collections.Generic;
using Mediapipe.Unity.Sample.HandLandmarkDetection;
using UnityEngine;

/// <summary>
/// Step 1: Basic hand-to-mesh raycasting for sculpting.
/// Detects which mesh vertex is being touched by the hand.
/// </summary>
public class VertexSculptController : MonoBehaviour
{
    private sealed class SculptUndoRecord
    {
        public int[] VertexIndices;
        public Vector3[] PreviousPositions;
    }

    [Header("References")]
    [SerializeField] private SkinnedMeshRenderer targetMesh;
    [SerializeField] private Camera sculptingCamera;
    [SerializeField] private Collider targetCollider;
    [SerializeField] private SculptHandLandmarkerRunner handRunner;

    [Header("Sculpting Settings")]
    [Tooltip("Brush radius in the target mesh's local units.")]
    [SerializeField, Min(0.001f)] private float brushRadius = 0.08f;
    [SerializeField, Range(0.02f, 0.5f)] private float brushSizeNormalized = 0.055f;
    [SerializeField, Range(0.05f, 1f)] private float sculptStrength = 0.52f;
    [SerializeField, Min(0f)] private float sculptRate = 0.52f;
    [SerializeField, Min(1f)] private float maxHoldSpeedMultiplier = 1.28f;
    [SerializeField, Min(0.1f)] private float holdAccelerationTime = 0.8f;
    [Header("Stroke Stabilization")]
    [SerializeField, Range(0f, 1f)] private float stabilization = 0.88f;
    [SerializeField, Range(0.4f, 3f)] private float oneEuroMinCutoff = 0.7f;
    [SerializeField, Range(0.001f, 0.05f)] private float oneEuroBeta = 0.006f;
    [SerializeField, Range(8f, 72f)] private float pullPixels = 44f;
    [SerializeField, Min(0f)] private float pinchStartDistance = 0.055f;
    [SerializeField, Min(0f)] private float pinchReleaseDistance = 0.08f;
    [SerializeField, Min(0f)] private float handTrackingGracePeriod = 0.2f;
    [SerializeField, Min(0f)] private float cursorMissGracePeriod = 0.2f;

    [Header("Debug")]
    [SerializeField] private bool showDebugVisuals = true;
    [SerializeField] private GameObject debugBrushIndicator;

    [Header("Brush Visual")]
    [SerializeField] private Material brushMaterial;
    [SerializeField, Min(0.005f)] private float brushIndicatorDiameter = 0.035f;
    private GameObject brushIndicator;
    private Mesh brushMesh;
    private Material generatedBrushMaterial;

    private Mesh mesh;
    private Mesh sourceMesh;
    private MeshCollider targetMeshCollider;
    private Mesh sourceColliderMesh;
    private Mesh bakedColliderMesh;
    private float[] lastColliderBlendShapeWeights;
    private float lastColliderRefreshTime;
    private float lastValidHandTime = -1f;
    private float lastSuccessfulRayTime = -1f;
    private float pinchHoldDuration;
    private Vector3[] originalVertices;
    private Vector3[] currentVertices;
    private bool[] sculptableVertices;
    private Dictionary<int, Vector3> strokeStartPositions;
    private readonly Stack<SculptUndoRecord> undoHistory = new Stack<SculptUndoRecord>();
    private int[][] weldedVertexGroups;
    private Bounds originalMeshBounds;
    private bool isInitialized;
    private bool sculptingEnabled = false;
    private bool isSculptPinching;
    private bool colliderNeedsRefresh;
    private bool strokeChanged;
    private bool undoBlockedUntilPinchRelease;
    private bool uiBlocked;
    private string lastDebugStatus;

    private bool pointerFilterReady;
    private float filteredPointerX;
    private float filteredPointerY;
    private float filteredPointerDx;
    private float filteredPointerDy;
    private bool lazyPointerReady;
    private Vector2 lazyScreen;

    public bool HasUndo => undoHistory.Count > 0 ||
        (strokeChanged && strokeStartPositions != null && strokeStartPositions.Count > 0);

    private void Awake()
    {
        if (sculptingCamera == null)
            sculptingCamera = Camera.main;
        if (handRunner == null)
            handRunner = FindFirstObjectByType<SculptHandLandmarkerRunner>();
        InitializeMesh();
        CreateBrushIndicator();
    }

    private void Update()
    {
        SyncColliderToBlendShapes();

        if (!sculptingEnabled)
        {
            isSculptPinching = false;
            pinchHoldDuration = 0f;
            ResetStabilizer();
            HideBrush();
            return;
        }

        if (handRunner == null)
        {
            HideBrush();
            SetDebugStatus("Vertex Sculpt: Hand Runner reference is missing.");
            return;
        }

        if (sculptingCamera == null)
        {
            HideBrush();
            SetDebugStatus("Vertex Sculpt: Sculpting Camera reference is missing.");
            return;
        }

        if (targetCollider == null || !targetCollider.enabled)
        {
            HideBrush();
            SetDebugStatus("Vertex Sculpt: Target Collider is missing or disabled.");
            return;
        }

        if (!handRunner.TryGetLatestHand(out SculptHandFrame hand))
        {
            bool trackingTimedOut = lastValidHandTime < 0f ||
                Time.unscaledTime - lastValidHandTime > handTrackingGracePeriod;
            if (trackingTimedOut)
            {
                FinishSculptStroke();
                isSculptPinching = false;
                pinchHoldDuration = 0f;
                ResetStabilizer();
                HideBrush();
            }
            SetDebugStatus("Vertex Sculpt: Waiting for a tracked hand frame.");
            return;
        }

        lastValidHandTime = Time.unscaledTime;

        bool wasPinching = isSculptPinching;
        float pinchThreshold = wasPinching ? pinchReleaseDistance : pinchStartDistance;
        bool pinchNow = !uiBlocked && hand.PinchDistance <= pinchThreshold;
        isSculptPinching = pinchNow;
        if (isSculptPinching && !wasPinching && !undoBlockedUntilPinchRelease)
            BeginSculptStroke();
        if (!isSculptPinching)
        {
            if (wasPinching)
                FinishSculptStroke();
            pinchHoldDuration = 0f;
            undoBlockedUntilPinchRelease = false;
        }

        Vector2 rawScreen = sculptingCamera.ViewportToScreenPoint(
            new Vector3(hand.IndexTip.x, hand.IndexTip.y, 0f));
        Ray ray = StabilizedRay(sculptingCamera, rawScreen);
        Debug.DrawRay(ray.origin, ray.direction * 10f, Color.magenta);

        // Cast against the target directly. A general Physics.Raycast can hit
        // another scene collider first and hide the teapot from the brush.
        if (targetCollider.Raycast(ray, out RaycastHit hit, 100f))
        {
            lastSuccessfulRayTime = Time.unscaledTime;
            float indicatorSize = GetWorldBrushIndicatorDiameter();
            UpdateBrushPosition(hit.point + hit.normal * (indicatorSize * 0.45f));
            if (isSculptPinching && !undoBlockedUntilPinchRelease)
            {
                pinchHoldDuration += Time.deltaTime;
                float holdProgress = Mathf.Clamp01(pinchHoldDuration / holdAccelerationTime);
                float speedMultiplier = Mathf.Lerp(1f, maxHoldSpeedMultiplier, holdProgress);
                int movedVertexCount = Sculpt(
                    hit.point, hit.normal, Time.deltaTime * sculptRate * speedMultiplier);
                SetDebugStatus(movedVertexCount > 0
                    ? "Vertex Sculpt: Pinch is moving mesh vertices."
                    : "Vertex Sculpt: Pinch detected, but the brush found no vertices to move.");
            }
            else
            {
                SetDebugStatus("Vertex Sculpt: Fingertip ray hits target; brush cursor is active.");
            }
        }
        else
        {
            if (lastSuccessfulRayTime < 0f ||
                Time.unscaledTime - lastSuccessfulRayTime > cursorMissGracePeriod)
                HideBrush();
            SetDebugStatus("Vertex Sculpt: Hand frame received, but fingertip ray misses target.");
        }
    }

    private void SetDebugStatus(string status)
    {
        if (status == lastDebugStatus)
            return;

        lastDebugStatus = status;
        Debug.Log(status, this);
    }

    private void InitializeMesh()
    {
        if (targetMesh == null)
        {
            targetMesh = GetComponent<SkinnedMeshRenderer>();
        }

        if (targetCollider == null && targetMesh != null)
            targetCollider = targetMesh.GetComponent<Collider>();

        if (targetMesh == null)
        {
            Debug.LogError("VertexSculptController: No SkinnedMeshRenderer found.", this);
            return;
        }

        sourceMesh = targetMesh.sharedMesh;
        if (sourceMesh == null)
        {
            Debug.LogError("VertexSculptController: Mesh is null.", this);
            return;
        }

        // Work on a private instance so sculpting never changes the imported/shared asset.
        mesh = Instantiate(sourceMesh);
        mesh.name = $"{sourceMesh.name} (Sculpt Instance)";
        targetMesh.sharedMesh = mesh;

        // MeshCollider was added in the scene with no mesh assigned. Give it
        // the same private mesh used by the renderer so fingertip rays can hit it.
        targetMeshCollider = targetCollider as MeshCollider;
        if (targetMeshCollider != null)
        {
            sourceColliderMesh = targetMeshCollider.sharedMesh;
            targetMeshCollider.sharedMesh = mesh;
            bakedColliderMesh = new Mesh { name = $"{mesh.name} (Collider Pose)" };
            lastColliderBlendShapeWeights = new float[mesh.blendShapeCount];
            targetMesh.BakeMesh(bakedColliderMesh);
            targetMeshCollider.sharedMesh = bakedColliderMesh;
            for (int i = 0; i < lastColliderBlendShapeWeights.Length; i++)
                lastColliderBlendShapeWeights[i] = targetMesh.GetBlendShapeWeight(i);
        }

        originalVertices = mesh.vertices;
        currentVertices = new Vector3[originalVertices.Length];
        System.Array.Copy(originalVertices, currentVertices, originalVertices.Length);
        originalMeshBounds = mesh.bounds;
        BuildWeldedVertexGroups();
        BuildSculptableVertexMask();

        isInitialized = true;
        RefreshBrushRadiusFromMesh();
        Bounds bounds = mesh.bounds;
        Debug.Log($"VertexSculptController: Initialized with {originalVertices.Length} vertices. Mesh bounds: {bounds}", this);
    }

    private float GetSmallestExtent()
    {
        if (originalMeshBounds.extents == Vector3.zero)
            return 0.25f;
        return Mathf.Max(
            0.001f,
            Mathf.Min(
                originalMeshBounds.extents.x,
                Mathf.Min(originalMeshBounds.extents.y, originalMeshBounds.extents.z)));
    }

    private void RefreshBrushRadiusFromMesh()
    {
        brushRadius = GetSmallestExtent() * brushSizeNormalized;
        if (brushIndicator != null)
            brushIndicator.transform.localScale = Vector3.one * GetWorldBrushIndicatorDiameter();
    }

    private void SyncColliderToBlendShapes()
    {
        if (targetMeshCollider == null || targetMesh == null || bakedColliderMesh == null)
            return;

        int blendShapeCount = targetMesh.sharedMesh.blendShapeCount;
        bool weightsChanged = lastColliderBlendShapeWeights == null ||
            lastColliderBlendShapeWeights.Length != blendShapeCount;

        for (int i = 0; !weightsChanged && i < blendShapeCount; i++)
        {
            if (Mathf.Abs(targetMesh.GetBlendShapeWeight(i) - lastColliderBlendShapeWeights[i]) > 0.01f)
                weightsChanged = true;
        }

        if (!weightsChanged && !colliderNeedsRefresh)
            return;

        // Avoid recooking the MeshCollider on every shape-key animation frame.
        if (Time.unscaledTime - lastColliderRefreshTime < 0.05f)
            return;

        targetMesh.BakeMesh(bakedColliderMesh);
        targetMeshCollider.sharedMesh = null;
        targetMeshCollider.sharedMesh = bakedColliderMesh;
        lastColliderRefreshTime = Time.unscaledTime;
        colliderNeedsRefresh = false;

        if (lastColliderBlendShapeWeights == null || lastColliderBlendShapeWeights.Length != blendShapeCount)
            lastColliderBlendShapeWeights = new float[blendShapeCount];

        for (int i = 0; i < blendShapeCount; i++)
            lastColliderBlendShapeWeights[i] = targetMesh.GetBlendShapeWeight(i);
    }

    private void BuildWeldedVertexGroups()
    {
        const float positionKeyScale = 10000f;
        var groupsByPosition = new Dictionary<Vector3Int, List<int>>();

        for (int i = 0; i < originalVertices.Length; i++)
        {
            Vector3 vertex = originalVertices[i];
            var key = new Vector3Int(
                Mathf.RoundToInt(vertex.x * positionKeyScale),
                Mathf.RoundToInt(vertex.y * positionKeyScale),
                Mathf.RoundToInt(vertex.z * positionKeyScale));

            if (!groupsByPosition.TryGetValue(key, out List<int> group))
            {
                group = new List<int>();
                groupsByPosition.Add(key, group);
            }

            group.Add(i);
        }

        weldedVertexGroups = new int[groupsByPosition.Count][];
        int groupIndex = 0;
        foreach (List<int> group in groupsByPosition.Values)
            weldedVertexGroups[groupIndex++] = group.ToArray();
    }

    private void BuildSculptableVertexMask()
    {
        sculptableVertices = new bool[originalVertices.Length];
        for (int i = 0; i < sculptableVertices.Length; i++)
            sculptableVertices[i] = true;

        int bambooShapeIndex = -1;
        for (int shape = 0; shape < mesh.blendShapeCount; shape++)
        {
            if (mesh.GetBlendShapeName(shape).IndexOf("bamboo", System.StringComparison.OrdinalIgnoreCase) >= 0)
            {
                bambooShapeIndex = shape;
                break;
            }
        }

        if (bambooShapeIndex < 0)
            return;

        Vector3[] deltaVertices = new Vector3[originalVertices.Length];
        Vector3[] deltaNormals = new Vector3[originalVertices.Length];
        Vector3[] deltaTangents = new Vector3[originalVertices.Length];
        float movementThreshold = Mathf.Max(originalMeshBounds.extents.magnitude * 0.00001f, 0.000001f);
        float movementThresholdSquared = movementThreshold * movementThreshold;
        int ignoredVertexCount = 0;

        for (int frame = 0; frame < mesh.GetBlendShapeFrameCount(bambooShapeIndex); frame++)
        {
            mesh.GetBlendShapeFrameVertices(
                bambooShapeIndex, frame, deltaVertices, deltaNormals, deltaTangents);

            for (int vertex = 0; vertex < deltaVertices.Length; vertex++)
            {
                if (deltaVertices[vertex].sqrMagnitude > movementThresholdSquared)
                {
                    if (sculptableVertices[vertex])
                        ignoredVertexCount++;
                    sculptableVertices[vertex] = false;
                }
            }
        }

        Debug.Log(
            $"VertexSculptController: Excluding {ignoredVertexCount} vertices affected by {mesh.GetBlendShapeName(bambooShapeIndex)} from sculpting.",
            this);
    }

    /// <summary>
    /// Enable or disable vertex sculpting mode.
    /// </summary>
    public void SetEnabled(bool enabled)
    {
        if (!enabled)
            FinishSculptStroke();
        sculptingEnabled = enabled;
        isSculptPinching = false;
        pinchHoldDuration = 0f;
        lastDebugStatus = null;
        if (!enabled)
        {
            uiBlocked = false;
            ResetStabilizer();
            HideBrush();
        }
        else
        {
            RefreshBrushRadiusFromMesh();
        }
        Debug.Log($"VertexSculptController: Sculpting {(enabled ? "enabled" : "disabled")}");
    }

    public void SetUiBlocked(bool blocked)
    {
        uiBlocked = blocked;
        if (blocked && isSculptPinching)
        {
            FinishSculptStroke();
            isSculptPinching = false;
            pinchHoldDuration = 0f;
        }
    }

    public void SetBrushSizeNormalized(float fraction)
    {
        brushSizeNormalized = Mathf.Clamp(fraction, 0.02f, 0.14f);
        RefreshBrushRadiusFromMesh();
    }

    public void SetSculptStrength(float strength)
    {
        sculptStrength = Mathf.Clamp01(strength);
    }

    public void CopySculptSettingsFrom(VertexSculptController source)
    {
        if (source == null || source == this)
            return;

        brushRadius = source.brushRadius;
        brushSizeNormalized = source.brushSizeNormalized;
        sculptStrength = source.sculptStrength;
        sculptRate = source.sculptRate;
        maxHoldSpeedMultiplier = source.maxHoldSpeedMultiplier;
        holdAccelerationTime = source.holdAccelerationTime;
        stabilization = source.stabilization;
        oneEuroMinCutoff = source.oneEuroMinCutoff;
        oneEuroBeta = source.oneEuroBeta;
        pullPixels = source.pullPixels;
        pinchStartDistance = source.pinchStartDistance;
        pinchReleaseDistance = source.pinchReleaseDistance;
        handTrackingGracePeriod = source.handTrackingGracePeriod;
        cursorMissGracePeriod = source.cursorMissGracePeriod;
        brushIndicatorDiameter = source.brushIndicatorDiameter;
        brushMaterial = source.brushMaterial;

        if (brushIndicator != null)
        {
            brushIndicator.transform.localScale = Vector3.one * GetWorldBrushIndicatorDiameter();
            if (brushMaterial != null)
                brushIndicator.GetComponent<Renderer>().sharedMaterial = brushMaterial;
        }
    }

    public void BindHandRunner(SculptHandLandmarkerRunner runner)
    {
        if (handRunner == null)
            handRunner = runner;
    }

    private void BeginSculptStroke()
    {
        if (currentVertices == null)
            return;

        strokeStartPositions = new Dictionary<int, Vector3>();
        strokeChanged = false;
    }

    private void FinishSculptStroke()
    {
        if (strokeChanged && strokeStartPositions != null && strokeStartPositions.Count > 0)
        {
            var record = new SculptUndoRecord
            {
                VertexIndices = new int[strokeStartPositions.Count],
                PreviousPositions = new Vector3[strokeStartPositions.Count]
            };

            int i = 0;
            foreach (KeyValuePair<int, Vector3> entry in strokeStartPositions)
            {
                record.VertexIndices[i] = entry.Key;
                record.PreviousPositions[i] = entry.Value;
                i++;
            }

            undoHistory.Push(record);
        }

        strokeStartPositions = null;
        strokeChanged = false;
    }

    public bool UndoLastStroke()
    {
        if (currentVertices == null || !HasUndo)
            return false;

        if (strokeChanged && strokeStartPositions != null && strokeStartPositions.Count > 0)
        {
            RestoreStrokePositions(strokeStartPositions);
            strokeStartPositions = null;
        }
        else
        {
            SculptUndoRecord record = undoHistory.Pop();
            for (int i = 0; i < record.VertexIndices.Length; i++)
                currentVertices[record.VertexIndices[i]] = record.PreviousPositions[i];
        }

        strokeChanged = false;
        isSculptPinching = false;
        pinchHoldDuration = 0f;
        undoBlockedUntilPinchRelease = true;
        ApplyMeshChanges();
        return true;
    }

    private void RestoreStrokePositions(Dictionary<int, Vector3> positions)
    {
        foreach (KeyValuePair<int, Vector3> entry in positions)
            currentVertices[entry.Key] = entry.Value;
    }

    /// <summary>
    /// Step 1: Test method to find the vertex closest to a world position using world-space distance.
    /// Returns the vertex index and world position.
    /// </summary>
    public (int index, Vector3 worldPosition) FindClosestVertex(Vector3 worldPosition)
    {
        if (!isInitialized || targetMesh == null)
            return (-1, Vector3.zero);

        int closestIndex = -1;
        float closestDistance = float.MaxValue;
        Vector3 closestWorldPos = Vector3.zero;

        // Check distance in world space
        for (int i = 0; i < currentVertices.Length; i++)
        {
            Vector3 vertexWorldPos = targetMesh.transform.TransformPoint(currentVertices[i]);
            float distance = Vector3.Distance(vertexWorldPos, worldPosition);

            if (distance < closestDistance)
            {
                closestDistance = distance;
                closestIndex = i;
                closestWorldPos = vertexWorldPos;
            }
        }

        if (closestIndex >= 0 && closestDistance <= brushRadius)
        {
            return (closestIndex, closestWorldPos);
        }

        return (-1, Vector3.zero);
    }

    /// <summary>
    /// Test method: Visual debug to verify raycasting is working.
    /// Call this from Update with your hand position.
    /// </summary>
    public void TestHandHit(Vector3 handPosition)
    {
        if (!isInitialized || !sculptingEnabled)
            return;

        var (index, worldPos) = FindClosestVertex(handPosition);

        if (index >= 0)
        {
            // Step 1 working! Ready for Step 2
            // You can add visual feedback here in Step 2
        }
    }

    private void OnDrawGizmos()
    {
        if (!showDebugVisuals || !isInitialized)
            return;

        // Draw brush radius at origin for visualization
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, brushRadius);
    }

    private void CreateBrushIndicator()
    {
        brushIndicator = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        brushIndicator.name = "BrushIndicator";
        brushIndicator.transform.SetParent(null);
        brushIndicator.SetActive(false);

        // Keep the pointer small and visible; sculpt radius is configured separately.
        brushIndicator.transform.localScale = Vector3.one * GetWorldBrushIndicatorDiameter();

        // Remove collider
        if (brushIndicator.GetComponent<Collider>() != null)
            Destroy(brushIndicator.GetComponent<Collider>());

        // Set material
        if (brushMaterial != null)
        {
            brushIndicator.GetComponent<Renderer>().material = brushMaterial;
        }
        else
        {
            // The project uses URP, so the built-in Standard shader renders pink.
            Shader cursorShader = Shader.Find("Universal Render Pipeline/Unlit");
            if (cursorShader == null)
                cursorShader = Shader.Find("Universal Render Pipeline/Lit");
            if (cursorShader == null)
                cursorShader = Shader.Find("Sprites/Default");

            if (cursorShader != null)
            {
                generatedBrushMaterial = new Material(cursorShader);
                Color cursorColor = new Color(1f, 0.22f, 0f, 1f);
                generatedBrushMaterial.color = cursorColor;
                generatedBrushMaterial.SetColor("_BaseColor", cursorColor);
                generatedBrushMaterial.SetColor("_Color", cursorColor);
                generatedBrushMaterial.SetColor("_EmissionColor", cursorColor * 1.5f);
                generatedBrushMaterial.EnableKeyword("_EMISSION");
                brushIndicator.GetComponent<Renderer>().material = generatedBrushMaterial;
            }
            else
            {
                Debug.LogError("VertexSculptController: No supported shader found for the brush cursor.", this);
            }
        }

        Renderer indicatorRenderer = brushIndicator.GetComponent<Renderer>();
        indicatorRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        indicatorRenderer.receiveShadows = false;
    }

    private float GetWorldBrushIndicatorDiameter()
    {
        float t = Mathf.InverseLerp(0.028f, 0.12f, brushSizeNormalized);
        return Mathf.Lerp(0.007f, 0.014f, Mathf.Clamp01(t));
    }

    /// <summary>
    /// Update brush indicator position
    /// </summary>
    public void UpdateBrushPosition(Vector3 worldPosition)
    {
        if (brushIndicator == null)
            return;

        brushIndicator.SetActive(sculptingEnabled);
        brushIndicator.transform.position = worldPosition;
        brushIndicator.transform.localScale = Vector3.one * GetWorldBrushIndicatorDiameter();
    }

    /// <summary>
    /// Step 2: Sculpt vertices at the given position.
    /// </summary>
    public int Sculpt(Vector3 worldPosition, Vector3 worldNormal, float strength)
    {
        if (!isInitialized || !sculptingEnabled || targetMesh == null)
            return 0;

        float smallestExtent = GetSmallestExtent();
        float clayRadius = Mathf.Clamp(
            brushRadius, smallestExtent * 0.02f, smallestExtent * 0.16f);
        float maximumDisplacement = smallestExtent * Mathf.Lerp(0.16f, 0.7f, sculptStrength);

        Vector3 localPosition = targetMesh.transform.InverseTransformPoint(worldPosition);
        Vector3 pressDirection = targetMesh.transform.InverseTransformDirection(worldNormal);
        if (pressDirection.sqrMagnitude < 0.0001f)
            pressDirection = Vector3.up;
        else
            pressDirection.Normalize();

        Vector3[] posedVertices = bakedColliderMesh != null ? bakedColliderMesh.vertices : currentVertices;
        bool anyVertexModified = false;
        int movedVertexCount = 0;
        float speedPerSecond = smallestExtent * Mathf.Lerp(0.08f, 0.42f, sculptStrength);
        float maximumFrameMove = smallestExtent *
            Mathf.Lerp(0.08f, 0.3f, sculptStrength) * Time.deltaTime;

        foreach (int[] group in weldedVertexGroups)
        {
            Vector3 vertexLocalPos = Vector3.zero;
            int includedCount = 0;
            foreach (int index in group)
            {
                if (sculptableVertices != null && !sculptableVertices[index])
                    continue;
                if (index < 0 || index >= posedVertices.Length)
                    continue;
                vertexLocalPos += posedVertices[index];
                includedCount++;
            }

            if (includedCount == 0)
                continue;

            vertexLocalPos /= includedCount;
            float distance = Vector3.Distance(vertexLocalPos, localPosition);
            if (distance > clayRadius)
                continue;

            float t = Mathf.Clamp01(distance / clayRadius);
            float falloff = 0.5f + 0.5f * Mathf.Cos(t * Mathf.PI);
            falloff *= falloff;
            float moveAmount = (strength / Mathf.Max(sculptRate, 0.01f)) *
                falloff * speedPerSecond;
            moveAmount = Mathf.Min(moveAmount, maximumFrameMove * falloff);

            int sampleIndex = group[0];
            Vector3 currentOffset = currentVertices[sampleIndex] - originalVertices[sampleIndex];
            Vector3 proposedOffset = currentOffset + pressDirection * moveAmount;
            if (proposedOffset.magnitude > maximumDisplacement)
                proposedOffset = proposedOffset.normalized * maximumDisplacement;
            Vector3 appliedDelta = proposedOffset - currentOffset;

            foreach (int index in group)
            {
                if (sculptableVertices != null && !sculptableVertices[index])
                    continue;
                if (strokeStartPositions != null && !strokeStartPositions.ContainsKey(index))
                    strokeStartPositions.Add(index, currentVertices[index]);
                currentVertices[index] += appliedDelta;
            }

            anyVertexModified = true;
            movedVertexCount += includedCount;
        }

        if (anyVertexModified)
        {
            strokeChanged = true;
            ApplyMeshChanges();
        }

        return movedVertexCount;
    }

    private void ApplyMeshChanges()
    {
        if (mesh == null || targetMesh == null)
            return;

        mesh.vertices = currentVertices;
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        targetMesh.sharedMesh = mesh;
        if (targetMeshCollider != null)
        {
            colliderNeedsRefresh = true;
        }
    }

    private void OnDestroy()
    {
        if (mesh != null)
        {
            if (targetMesh != null && targetMesh.sharedMesh == mesh)
                targetMesh.sharedMesh = sourceMesh;

            if (targetMeshCollider != null &&
                (targetMeshCollider.sharedMesh == mesh || targetMeshCollider.sharedMesh == bakedColliderMesh))
                targetMeshCollider.sharedMesh = sourceColliderMesh;

            Destroy(mesh);
        }

        if (bakedColliderMesh != null)
            Destroy(bakedColliderMesh);

        if (targetMesh != null)
            targetMesh.forceRenderingOff = false;

        if (generatedBrushMaterial != null)
            Destroy(generatedBrushMaterial);
    }

    public void HideBrush()
    {
        if (brushIndicator != null)
            brushIndicator.SetActive(false);
    }

    private Ray StabilizedRay(Camera viewCamera, Vector2 rawScreen)
    {
        float dt = Mathf.Max(Time.deltaTime, 0.0001f);
        Vector2 filtered = FilterPointer(rawScreen, dt);
        Vector2 brushScreen = ApplyLazyPull(filtered, dt);
        return viewCamera.ScreenPointToRay(new Vector3(brushScreen.x, brushScreen.y, 0f));
    }

    private Vector2 FilterPointer(Vector2 raw, float dt)
    {
        float amount = Mathf.Clamp01(stabilization);
        if (amount <= 0.001f)
        {
            pointerFilterReady = false;
            return raw;
        }

        if (!pointerFilterReady)
        {
            filteredPointerX = raw.x;
            filteredPointerY = raw.y;
            filteredPointerDx = 0f;
            filteredPointerDy = 0f;
            pointerFilterReady = true;
            return raw;
        }

        float minCutoff = Mathf.Lerp(2.4f, oneEuroMinCutoff, amount);
        float beta = Mathf.Lerp(0.04f, oneEuroBeta, amount);
        const float dCutoff = 1f;

        float dx = (raw.x - filteredPointerX) / dt;
        filteredPointerDx = LowPass(dx, filteredPointerDx, Alpha(dCutoff, dt));
        float cutoffX = minCutoff + beta * Mathf.Abs(filteredPointerDx);
        filteredPointerX = LowPass(raw.x, filteredPointerX, Alpha(cutoffX, dt));

        float dy = (raw.y - filteredPointerY) / dt;
        filteredPointerDy = LowPass(dy, filteredPointerDy, Alpha(dCutoff, dt));
        float cutoffY = minCutoff + beta * Mathf.Abs(filteredPointerDy);
        filteredPointerY = LowPass(raw.y, filteredPointerY, Alpha(cutoffY, dt));

        return new Vector2(filteredPointerX, filteredPointerY);
    }

    private Vector2 ApplyLazyPull(Vector2 filtered, float dt)
    {
        float amount = Mathf.Clamp01(stabilization);
        if (amount <= 0.001f)
        {
            lazyPointerReady = false;
            lazyScreen = filtered;
            return filtered;
        }

        float heightScale = Mathf.Max(UnityEngine.Screen.height, 1) / 1080f;
        float drawPull = Mathf.Lerp(6f, pullPixels, amount) * heightScale;
        float hoverPull = drawPull * 0.28f;
        float pull = isSculptPinching ? drawPull : hoverPull;

        if (!lazyPointerReady)
        {
            lazyScreen = filtered;
            lazyPointerReady = true;
            return lazyScreen;
        }

        Vector2 delta = filtered - lazyScreen;
        float dist = delta.magnitude;
        if (dist > pull)
            lazyScreen = filtered - delta.normalized * pull;
        else if (!isSculptPinching)
            lazyScreen = Vector2.Lerp(lazyScreen, filtered, 1f - Mathf.Exp(-dt * 16f));

        return lazyScreen;
    }

    private void ResetStabilizer()
    {
        pointerFilterReady = false;
        lazyPointerReady = false;
        filteredPointerDx = 0f;
        filteredPointerDy = 0f;
    }

    private static float Alpha(float cutoff, float dt)
    {
        float tau = 1f / (2f * Mathf.PI * Mathf.Max(cutoff, 0.01f));
        return 1f / (1f + tau / dt);
    }

    private static float LowPass(float value, float previous, float alpha)
    {
        return alpha * value + (1f - alpha) * previous;
    }
}
