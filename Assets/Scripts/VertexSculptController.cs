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
    [SerializeField] private float sculptStrength = 0.5f;
    [SerializeField, Min(0f)] private float sculptRate = 0.5f;
    [SerializeField, Min(1f)] private float maxHoldSpeedMultiplier = 12f;
    [SerializeField, Min(0.1f)] private float holdAccelerationTime = 0.12f;
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
    private string lastDebugStatus;

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
                HideBrush();
            }
            SetDebugStatus("Vertex Sculpt: Waiting for a tracked hand frame.");
            return;
        }

        lastValidHandTime = Time.unscaledTime;

        bool wasPinching = isSculptPinching;
        float pinchThreshold = wasPinching ? pinchReleaseDistance : pinchStartDistance;
        isSculptPinching = hand.PinchDistance <= pinchThreshold;
        if (isSculptPinching && !wasPinching && !undoBlockedUntilPinchRelease)
            BeginSculptStroke();
        if (!isSculptPinching)
        {
            if (wasPinching)
                FinishSculptStroke();
            pinchHoldDuration = 0f;
            undoBlockedUntilPinchRelease = false;
        }

        // Hand landmarks use normalized image coordinates, matching ViewportPointToRay.
        Ray ray = sculptingCamera.ViewportPointToRay(
            new Vector3(hand.IndexTip.x, hand.IndexTip.y, 0f));
        Debug.DrawRay(ray.origin, ray.direction * 10f, Color.magenta);

        // Cast against the target directly. A general Physics.Raycast can hit
        // another scene collider first and hide the teapot from the brush.
        if (targetCollider.Raycast(ray, out RaycastHit hit, 100f))
        {
            lastSuccessfulRayTime = Time.unscaledTime;
            float indicatorSize = GetWorldBrushIndicatorDiameter();
            UpdateBrushPosition(hit.point + hit.normal * (indicatorSize * 0.75f));
            if (isSculptPinching && !undoBlockedUntilPinchRelease)
            {
                pinchHoldDuration += Time.deltaTime;
                float holdProgress = Mathf.Clamp01(pinchHoldDuration / holdAccelerationTime);
                float speedMultiplier = Mathf.Lerp(1f, maxHoldSpeedMultiplier, holdProgress);
                int movedVertexCount = Sculpt(hit.point, Time.deltaTime * sculptRate * speedMultiplier);
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
        Bounds bounds = mesh.bounds;
        Debug.Log($"VertexSculptController: Initialized with {originalVertices.Length} vertices. Mesh bounds: {bounds}", this);
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
        Debug.Log($"VertexSculptController: Sculpting {(enabled ? "enabled" : "disabled")}");
    }

    public void CopySculptSettingsFrom(VertexSculptController source)
    {
        if (source == null || source == this)
            return;

        brushRadius = source.brushRadius;
        sculptStrength = source.sculptStrength;
        sculptRate = source.sculptRate;
        maxHoldSpeedMultiplier = source.maxHoldSpeedMultiplier;
        holdAccelerationTime = source.holdAccelerationTime;
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
        if (targetMesh == null || originalMeshBounds.extents == Vector3.zero)
            return brushIndicatorDiameter;

        float smallestExtent = Mathf.Min(
            originalMeshBounds.extents.x,
            Mathf.Min(originalMeshBounds.extents.y, originalMeshBounds.extents.z));
        float localBrushRadius = Mathf.Min(brushRadius, smallestExtent * 0.04f);
        Vector3 scale = targetMesh.transform.lossyScale;
        float largestScale = Mathf.Max(
            Mathf.Abs(scale.x), Mathf.Abs(scale.y), Mathf.Abs(scale.z));
        float scaledMarkerDiameter = localBrushRadius * largestScale * 0.025f;
        return Mathf.Clamp(
            Mathf.Max(brushIndicatorDiameter, scaledMarkerDiameter), 0.035f, 0.08f);
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
    }

    /// <summary>
    /// Step 2: Sculpt vertices at the given position.
    /// </summary>
    public int Sculpt(Vector3 worldPosition, float strength)
    {
        if (!isInitialized || !sculptingEnabled || targetMesh == null)
            return 0;

        // Keep legacy Inspector values from applying the brush to the whole mesh.
        float boundsRadius = originalMeshBounds.extents.magnitude;
        boundsRadius = Mathf.Max(boundsRadius, 0.001f);
        float smallestExtent = Mathf.Min(
            originalMeshBounds.extents.x,
            Mathf.Min(originalMeshBounds.extents.y, originalMeshBounds.extents.z));
        float effectiveBrushRadius = mesh != null
            ? Mathf.Min(brushRadius, smallestExtent * 0.04f)
            : brushRadius;
        effectiveBrushRadius = Mathf.Max(effectiveBrushRadius, 0.001f);
        float maximumDisplacement = Mathf.Min(effectiveBrushRadius * 6f, boundsRadius * 0.3f);

        // Find vertices within brush radius
        Vector3 localPosition = targetMesh.transform.InverseTransformPoint(worldPosition);
        bool anyVertexModified = false;
        int movedVertexCount = 0;
        Vector3[] posedVertices = bakedColliderMesh != null ? bakedColliderMesh.vertices : currentVertices;
        Vector3[] posedNormals = bakedColliderMesh != null ? bakedColliderMesh.normals : mesh.normals;

        // Lantern geometry is lower resolution than the teapot. Expand a
        // small brush enough to include nearby mesh vertices, up to a safe cap.
        float nearestVertexDistance = float.MaxValue;
        foreach (int[] group in weldedVertexGroups)
        {
            Vector3 groupPosition = Vector3.zero;
            int includedCount = 0;
            foreach (int index in group)
            {
                if (sculptableVertices != null && !sculptableVertices[index])
                    continue;
                groupPosition += posedVertices[index];
                includedCount++;
            }
            if (includedCount == 0)
                continue;
            groupPosition /= includedCount;
            nearestVertexDistance = Mathf.Min(
                nearestVertexDistance, Vector3.Distance(groupPosition, localPosition));
        }

        if (nearestVertexDistance > effectiveBrushRadius)
        {
            float safeMaximumRadius = smallestExtent * 0.1f;
            effectiveBrushRadius = Mathf.Min(
                safeMaximumRadius,
                Mathf.Max(effectiveBrushRadius, nearestVertexDistance * 1.1f));
        }

        foreach (int[] group in weldedVertexGroups)
        {
            Vector3 vertexLocalPos = Vector3.zero;
            Vector3 normal = Vector3.zero;
            int includedCount = 0;
            foreach (int index in group)
            {
                if (sculptableVertices != null && !sculptableVertices[index])
                    continue;
                vertexLocalPos += posedVertices[index];
                if (index < posedNormals.Length)
                    normal += posedNormals[index];
                includedCount++;
            }
            if (includedCount == 0)
                continue;
            vertexLocalPos /= includedCount;

            float distance = Vector3.Distance(vertexLocalPos, localPosition);

            if (distance <= effectiveBrushRadius)
            {
                // Smoothstep-shaped falloff spreads movement evenly across a
                // rounded brush, like pressing soft clay instead of making a spike.
                float normalizedDistance = distance / effectiveBrushRadius;
                float falloff = 1f - normalizedDistance * normalizedDistance *
                    (3f - 2f * normalizedDistance);

                // Use the visible posed surface so blend-shape changes do not
                // make the brush operate on the hidden base mesh. Coincident
                // seam vertices share an averaged normal and displacement.
                Vector3 direction = normal.sqrMagnitude > 0.0001f
                    ? normal.normalized
                    : (vertexLocalPos - localPosition).normalized;
                // Scale movement to the mesh size, then boost narrow brushes
                // so a smaller detail area still produces a visible bulge.
                float referenceRadius = boundsRadius * 0.12f;
                float narrowBrushBoost = Mathf.Clamp(
                    referenceRadius / effectiveBrushRadius, 1f, 3f);
                float moveAmount = strength * falloff * sculptStrength *
                    boundsRadius * narrowBrushBoost;
                // Limit each frame's movement so a strong setting still feels
                // continuous and controllable while the pinch is held.
                float maximumFrameMove = effectiveBrushRadius * 3f * Time.deltaTime;
                moveAmount = Mathf.Min(moveAmount, maximumFrameMove);

                Vector3 currentOffset = currentVertices[group[0]] - originalVertices[group[0]];
                Vector3 proposedOffset = currentOffset + direction * moveAmount;
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

        if (generatedBrushMaterial != null)
            Destroy(generatedBrushMaterial);
    }

    public void HideBrush()
    {
        if (brushIndicator != null)
            brushIndicator.SetActive(false);
    }
}
