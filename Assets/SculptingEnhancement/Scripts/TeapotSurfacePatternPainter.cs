using System;
using System.Collections.Generic;
using Mediapipe.Unity.Sample.HandLandmarkDetection;
using UnityEngine;

/// <summary>
/// Handles real-time interactive 3D pattern drawing directly onto the surface of the teapot.
/// Features:
/// 1. Bold, prominent ribbon stroke sizing scaled properly to the teapot's world dimensions.
/// 2. Only draws when an isolated index finger pointing gesture is detected.
/// 3. Open palm gesture locks/unlocks drawing to prevent accidental marks.
/// 4. Mathematically anchors all stroke points to teapot mesh surface triangles via barycentric coordinates,
///    so when blend shapes (Width, Height, Middle, etc.) or scales change, the drawings deform and stay
///    glued to the outer surface without ever sinking inside!
/// </summary>
[DefaultExecutionOrder(270)]
public sealed class TeapotSurfacePatternPainter : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private SculptHandLandmarkerRunner handRunner;
    [SerializeField] private SkinnedMeshRenderer teapotRenderer;

    [Header("Brush Settings")]
    [SerializeField, Range(0.04f, 0.20f)] private float strokeWidth = 0.085f;
    [SerializeField, Range(0.003f, 0.03f)] private float minPointSpacing = 0.008f;
    [SerializeField] private Color strokeColor = Color.white;

    private readonly List<StrokeRecord> completedStrokes = new List<StrokeRecord>();
    private StrokeRecord currentStroke;

    private MeshCollider surfaceCollider;
    private Mesh bakedColliderMesh;
    private Mesh deformBakeMesh;
    private int[] cachedTriangles;
    private Vector3[] cachedBakedVertices;
    private Vector3[] cachedBakedNormals;
    private float[] cachedBlendWeights;

    private GameObject strokeContainer;
    private GameObject cursorObject;
    private LineRenderer cursorRing;
    private Material cursorMaterial;
    private Material strokeMaterial;
    private Texture2D activePatternTexture;

    private float accumulatedDistance;
    private bool isDrawingActive;
    private bool isPointerOnSurface;
    private bool isDrawingLocked;
    private float openPalmTimer;
    private Vector3 currentSurfacePoint;
    private Vector3 currentSurfaceNormal = Vector3.up;
    private int lastHitTriangleIndex = -1;
    private Vector3 lastHitBarycentric = Vector3.zero;

    private bool paintingEnabled;
    private int lastProcessedFrame = -1;

    public int StrokeCount => completedStrokes.Count;
    public bool IsDrawing => isDrawingActive;
    public bool IsOverSurface => isPointerOnSurface;
    public bool IsLocked => isDrawingLocked;
    public float StrokeWidth => strokeWidth;
    public Color StrokeColor => strokeColor;

    public event Action<bool> OnLockStateChanged;

    private sealed class StrokeRecord
    {
        public GameObject strokeObject;
        public Mesh strokeMesh;
        public Material material;
        public float strokeWidth;
        public readonly List<AnchorPoint> anchors = new List<AnchorPoint>();
    }

    private struct AnchorPoint
    {
        public int i0;
        public int i1;
        public int i2;
        public Vector3 bary;
        public float normalOffset;
        public Vector3 fallbackLocalPos;
        public Vector3 fallbackLocalNorm;
    }

    public void Initialize(SculptHandLandmarkerRunner runner, SkinnedMeshRenderer renderer)
    {
        handRunner = runner;
        teapotRenderer = renderer;
        Physics.queriesHitBackfaces = true;
        if (teapotRenderer != null && teapotRenderer.sharedMesh != null)
            cachedTriangles = teapotRenderer.sharedMesh.triangles;
        EnsureCollider();
        EnsureStrokeContainer();
        EnsureCursor();
        EnsureMaterial();
    }

    public void SetStrokeWidth(float width, bool updateExisting = true)
    {
        strokeWidth = Mathf.Clamp(width, 0.035f, 0.22f);
        UpdateCursorRingGeometry();

        if (updateExisting && completedStrokes.Count > 0 && cachedBakedVertices != null)
        {
            for (int i = 0; i < completedStrokes.Count; i++)
            {
                if (completedStrokes[i] != null)
                {
                    completedStrokes[i].strokeWidth = strokeWidth;
                    RebuildStrokeMeshFromAnchors(completedStrokes[i], cachedBakedVertices, cachedBakedNormals);
                }
            }
        }
    }

    public void SetPaintingEnabled(bool enabled)
    {
        paintingEnabled = enabled;
        if (enabled)
        {
            RefreshCollider();
            UpdateCursorRingGeometry();
        }
        else
        {
            if (isDrawingActive)
                FinishCurrentStroke();
            isPointerOnSurface = false;
        }

        if (cursorObject != null)
            cursorObject.SetActive(enabled && isPointerOnSurface);
    }

    public void SetDrawingLocked(bool locked)
    {
        if (isDrawingLocked == locked)
            return;

        isDrawingLocked = locked;
        openPalmTimer = 0f;
        if (isDrawingLocked && isDrawingActive)
            FinishCurrentStroke();

        OnLockStateChanged?.Invoke(isDrawingLocked);
    }

    public void SetPatternTexture(Texture2D pattern)
    {
        if (pattern != null)
        {
            pattern.wrapModeU = TextureWrapMode.Repeat;
            pattern.wrapModeV = TextureWrapMode.Clamp;
        }

        activePatternTexture = pattern;
        EnsureMaterial();
        if (strokeMaterial != null && pattern != null)
        {
            strokeMaterial.mainTexture = pattern;
            if (strokeMaterial.HasProperty("_MainTex"))
                strokeMaterial.SetTexture("_MainTex", pattern);
            if (strokeMaterial.HasProperty("_BaseMap"))
                strokeMaterial.SetTexture("_BaseMap", pattern);
        }
    }

    public void SetStrokeColor(Color color, bool updateExisting = true)
    {
        strokeColor = color;
        if (strokeMaterial != null)
        {
            if (strokeMaterial.HasProperty("_Color"))
                strokeMaterial.SetColor("_Color", color);
            if (strokeMaterial.HasProperty("_BaseColor"))
                strokeMaterial.SetColor("_BaseColor", color);
        }

        if (updateExisting)
        {
            for (int i = 0; i < completedStrokes.Count; i++)
            {
                if (completedStrokes[i] != null && completedStrokes[i].material != null)
                {
                    if (completedStrokes[i].material.HasProperty("_Color"))
                        completedStrokes[i].material.SetColor("_Color", color);
                    if (completedStrokes[i].material.HasProperty("_BaseColor"))
                        completedStrokes[i].material.SetColor("_BaseColor", color);
                }
            }

            if (currentStroke != null && currentStroke.material != null)
            {
                if (currentStroke.material.HasProperty("_Color"))
                    currentStroke.material.SetColor("_Color", color);
                if (currentStroke.material.HasProperty("_BaseColor"))
                    currentStroke.material.SetColor("_BaseColor", color);
            }
        }
    }

    public void UndoLastStroke()
    {
        if (completedStrokes.Count == 0)
            return;

        int lastIndex = completedStrokes.Count - 1;
        StrokeRecord last = completedStrokes[lastIndex];
        completedStrokes.RemoveAt(lastIndex);
        if (last != null && last.strokeObject != null)
            Destroy(last.strokeObject);
    }

    public void ClearAllStrokes()
    {
        if (isDrawingActive)
            FinishCurrentStroke();

        for (int i = 0; i < completedStrokes.Count; i++)
        {
            if (completedStrokes[i] != null && completedStrokes[i].strokeObject != null)
                Destroy(completedStrokes[i].strokeObject);
        }
        completedStrokes.Clear();
    }

    public void SetStrokesVisible(bool visible)
    {
        if (strokeContainer != null)
            strokeContainer.SetActive(visible);
    }

    private void Awake()
    {
        Physics.queriesHitBackfaces = true;
        if (teapotRenderer != null && teapotRenderer.sharedMesh != null)
            cachedTriangles = teapotRenderer.sharedMesh.triangles;
        EnsureCollider();
        EnsureStrokeContainer();
        EnsureCursor();
        EnsureMaterial();
    }

    private float GetLossyScaleFactor()
    {
        if (strokeContainer != null)
        {
            float s = strokeContainer.transform.lossyScale.x;
            if (s > 0.0001f)
                return s;
        }
        if (teapotRenderer != null)
        {
            float s = teapotRenderer.transform.lossyScale.x;
            if (s > 0.0001f)
                return s;
        }
        return 0.05f;
    }

    /// <summary>
    /// Explicit pointer update driven directly from the ColorController using active screen coordinates.
    /// </summary>
    public void ProcessPointer(Vector2 screenPosition, bool isHandTracked, bool isMouseClick)
    {
        if (!paintingEnabled || teapotRenderer == null)
            return;

        lastProcessedFrame = Time.frameCount;

        Camera viewCamera = ResolveCamera();
        if (viewCamera == null)
        {
            isPointerOnSurface = false;
            UpdateCursorVisual();
            return;
        }

        Ray ray = viewCamera.ScreenPointToRay(screenPosition);
        PerformRaycast(ray);

        // Check Open Palm gesture for Lock / Unlock
        SculptHandFrame hand = default;
        bool hasHandData = isHandTracked && handRunner != null && handRunner.TryGetLatestHand(out hand);

        if (hasHandData)
        {
            if (IsOpenPalm(hand))
            {
                openPalmTimer += Time.deltaTime;
                if (!isDrawingLocked && openPalmTimer >= 0.35f)
                {
                    SetDrawingLocked(true);
                }
                else if (isDrawingLocked && openPalmTimer >= 0.6f)
                {
                    SetDrawingLocked(false);
                }
            }
            else
            {
                openPalmTimer = Mathf.Max(0f, openPalmTimer - Time.deltaTime * 2f);
            }
        }

        UpdateCursorVisual();

        bool shouldDraw = false;
        if (isPointerOnSurface && !isDrawingLocked)
        {
            if (hasHandData)
            {
                // ONLY DRAW WHEN AN ISOLATED INDEX FINGER POINTING GESTURE IS DETECTED
                shouldDraw = IsIndexPointing(hand);
            }
            else
            {
                // In mouse mode (editor debugging): left mouse drag or click
                shouldDraw = isMouseClick || Input.GetMouseButton(0);
            }
        }

        if (shouldDraw)
        {
            if (!isDrawingActive)
                BeginNewStroke();

            AppendPointToStroke(currentSurfacePoint, currentSurfaceNormal, lastHitTriangleIndex, lastHitBarycentric);
        }
        else
        {
            if (isDrawingActive)
                FinishCurrentStroke();
        }
    }

    private void Update()
    {
        if (lastProcessedFrame == Time.frameCount)
            return;

        if (!paintingEnabled || teapotRenderer == null)
        {
            if (cursorObject != null && cursorObject.activeSelf)
                cursorObject.SetActive(false);
            if (isDrawingActive)
                FinishCurrentStroke();
            return;
        }

        Camera viewCamera = ResolveCamera();
        if (viewCamera == null)
            return;

        Ray ray;
        bool hasHand = false;
        SculptHandFrame hand = default;
        if (handRunner != null && handRunner.TryGetLatestHand(out hand))
        {
            hasHand = true;
            Vector3 viewportPoint = new Vector3(hand.IndexTip.x, hand.IndexTip.y, 0f);
            ray = viewCamera.ViewportPointToRay(viewportPoint);

            if (IsOpenPalm(hand))
            {
                openPalmTimer += Time.deltaTime;
                if (!isDrawingLocked && openPalmTimer >= 0.35f)
                    SetDrawingLocked(true);
                else if (isDrawingLocked && openPalmTimer >= 0.6f)
                    SetDrawingLocked(false);
            }
            else
            {
                openPalmTimer = Mathf.Max(0f, openPalmTimer - Time.deltaTime * 2f);
            }
        }
        else
        {
            ray = viewCamera.ScreenPointToRay(Input.mousePosition);
        }

        PerformRaycast(ray);
        UpdateCursorVisual();

        bool shouldDraw = false;
        if (isPointerOnSurface && !isDrawingLocked)
        {
            if (hasHand)
                shouldDraw = IsIndexPointing(hand);
            else
                shouldDraw = Input.GetMouseButton(0);
        }

        if (shouldDraw)
        {
            if (!isDrawingActive)
                BeginNewStroke();

            AppendPointToStroke(currentSurfacePoint, currentSurfaceNormal, lastHitTriangleIndex, lastHitBarycentric);
        }
        else
        {
            if (isDrawingActive)
                FinishCurrentStroke();
        }
    }

    private void LateUpdate()
    {
        // Whenever blend shapes change (e.g. Width, Height, Middle shape keys),
        // re-anchor and deform all stroke ribbon vertices so they ALWAYS stay on the surface!
        if (HasBlendShapesChanged())
        {
            UpdateAllStrokesToCurrentDeformation();
        }
    }

    private bool HasBlendShapesChanged()
    {
        if (teapotRenderer == null || teapotRenderer.sharedMesh == null)
            return false;

        int count = teapotRenderer.sharedMesh.blendShapeCount;
        if (cachedBlendWeights == null || cachedBlendWeights.Length != count)
        {
            cachedBlendWeights = new float[count];
            for (int i = 0; i < count; i++)
                cachedBlendWeights[i] = teapotRenderer.GetBlendShapeWeight(i);
            return true;
        }

        bool changed = false;
        for (int i = 0; i < count; i++)
        {
            float w = teapotRenderer.GetBlendShapeWeight(i);
            if (Mathf.Abs(w - cachedBlendWeights[i]) > 0.05f)
            {
                cachedBlendWeights[i] = w;
                changed = true;
            }
        }
        return changed;
    }

    public void UpdateAllStrokesToCurrentDeformation()
    {
        if (teapotRenderer == null || strokeContainer == null)
            return;

        if (deformBakeMesh == null)
        {
            deformBakeMesh = new Mesh { name = "TeapotDeformBake" };
            deformBakeMesh.MarkDynamic();
        }

        teapotRenderer.BakeMesh(deformBakeMesh);
        cachedBakedVertices = deformBakeMesh.vertices;
        cachedBakedNormals = deformBakeMesh.normals;

        if (surfaceCollider != null)
        {
            surfaceCollider.sharedMesh = null;
            surfaceCollider.sharedMesh = deformBakeMesh;
        }

        for (int i = 0; i < completedStrokes.Count; i++)
        {
            if (completedStrokes[i] != null)
                RebuildStrokeMeshFromAnchors(completedStrokes[i], cachedBakedVertices, cachedBakedNormals);
        }

        if (isDrawingActive && currentStroke != null)
        {
            RebuildStrokeMeshFromAnchors(currentStroke, cachedBakedVertices, cachedBakedNormals);
        }
    }

    public static bool IsIndexPointing(SculptHandFrame hand)
    {
        float palm = Mathf.Max(hand.PalmWidth, 0.001f);
        return hand.IndexExtensionAngle >= 115f &&
               hand.IndexTipPalmRatio >= 1.02f &&
               hand.OtherFingersCurlRatio <= 1.35f &&
               hand.MiddleTipPalmRatio <= 1.05f &&
               hand.PinchDistance >= palm * 0.35f &&
               !IsOpenPalm(hand);
    }

    public static bool IsOpenPalm(SculptHandFrame hand)
    {
        float palm = Mathf.Max(hand.PalmWidth, 0.001f);
        return hand.PinchDistance >= palm * 0.45f &&
               hand.FingerCurlRatio >= 1.30f &&
               hand.OtherFingersCurlRatio >= 1.25f &&
               hand.IndexTipPalmRatio >= 1.02f &&
               hand.MiddleTipPalmRatio >= 1.02f;
    }

    private void PerformRaycast(Ray ray)
    {
        RaycastHit[] hits = Physics.RaycastAll(ray, 100f);
        RaycastHit closestHit = default;
        float minDistance = float.MaxValue;
        bool hitFound = false;

        for (int i = 0; i < hits.Length; i++)
        {
            RaycastHit h = hits[i];
            bool isTeapotHit = false;

            if (surfaceCollider != null && h.collider == surfaceCollider)
            {
                isTeapotHit = true;
            }
            else if (teapotRenderer != null)
            {
                if (h.transform == teapotRenderer.transform || h.transform.IsChildOf(teapotRenderer.transform))
                    isTeapotHit = true;
                else if (teapotRenderer.transform.parent != null &&
                         (h.transform == teapotRenderer.transform.parent || h.transform.IsChildOf(teapotRenderer.transform.parent)))
                    isTeapotHit = true;
            }

            if (isTeapotHit && h.distance < minDistance)
            {
                minDistance = h.distance;
                closestHit = h;
                hitFound = true;
            }
        }

        if (hitFound)
        {
            isPointerOnSurface = true;
            currentSurfacePoint = closestHit.point;
            currentSurfaceNormal = closestHit.normal.sqrMagnitude > 0.001f
                ? closestHit.normal.normalized
                : Vector3.up;
            lastHitTriangleIndex = closestHit.triangleIndex;
            lastHitBarycentric = closestHit.barycentricCoordinate;
        }
        else
        {
            isPointerOnSurface = false;
            lastHitTriangleIndex = -1;
            lastHitBarycentric = Vector3.zero;
        }
    }

    private void UpdateCursorVisual()
    {
        if (cursorObject == null)
            return;

        if (!isPointerOnSurface || !paintingEnabled)
        {
            cursorObject.SetActive(false);
            return;
        }

        cursorObject.SetActive(true);
        cursorObject.transform.position = currentSurfacePoint + currentSurfaceNormal * 0.006f;
        if (currentSurfaceNormal.sqrMagnitude > 0.001f)
            cursorObject.transform.rotation = Quaternion.LookRotation(currentSurfaceNormal);

        Color cursorColor;
        if (isDrawingLocked)
        {
            // Amber/Gold indicates locked state
            cursorColor = new Color(1f, 0.78f, 0.15f, 0.95f);
        }
        else if (isDrawingActive)
        {
            // Electric green while actively drawing with index finger
            cursorColor = new Color(0.2f, 1f, 0.35f, 0.95f);
        }
        else
        {
            // Cyan hover
            cursorColor = new Color(0.1f, 0.85f, 1f, 0.9f);
        }

        if (cursorRing != null)
        {
            cursorRing.startColor = cursorColor;
            cursorRing.endColor = cursorColor;
        }

        if (cursorMaterial != null)
        {
            cursorMaterial.color = cursorColor;
            if (cursorMaterial.HasProperty("_BaseColor"))
                cursorMaterial.SetColor("_BaseColor", cursorColor);
        }
    }

    private void UpdateCursorRingGeometry()
    {
        if (cursorRing == null)
            return;

        float scaleFactor = GetLossyScaleFactor();
        float localRadius = (strokeWidth / scaleFactor) * 0.5f;
        int segments = cursorRing.positionCount;
        for (int i = 0; i < segments; i++)
        {
            float angle = (float)i / segments * Mathf.PI * 2f;
            cursorRing.SetPosition(i, new Vector3(Mathf.Cos(angle) * localRadius, Mathf.Sin(angle) * localRadius, 0f));
        }
    }

    private void BeginNewStroke()
    {
        EnsureStrokeContainer();
        EnsureMaterial();

        isDrawingActive = true;
        accumulatedDistance = 0f;

        GameObject strokeObj = new GameObject("PatternStroke_" + completedStrokes.Count);
        strokeObj.transform.SetParent(strokeContainer.transform, false);

        MeshFilter mf = strokeObj.AddComponent<MeshFilter>();
        MeshRenderer mr = strokeObj.AddComponent<MeshRenderer>();

        Mesh mesh = new Mesh { name = "PatternStrokeMesh" };
        mesh.MarkDynamic();
        mf.sharedMesh = mesh;

        Material mat = new Material(strokeMaterial);
        if (activePatternTexture != null)
        {
            mat.mainTexture = activePatternTexture;
            if (mat.HasProperty("_MainTex"))
                mat.SetTexture("_MainTex", activePatternTexture);
            if (mat.HasProperty("_BaseMap"))
                mat.SetTexture("_BaseMap", activePatternTexture);
        }
        if (mat.HasProperty("_Color"))
            mat.SetColor("_Color", strokeColor);
        if (mat.HasProperty("_BaseColor"))
            mat.SetColor("_BaseColor", strokeColor);
        if (mat.HasProperty("_Opacity"))
            mat.SetFloat("_Opacity", 1f);

        mr.sharedMaterial = mat;

        currentStroke = new StrokeRecord
        {
            strokeObject = strokeObj,
            strokeMesh = mesh,
            material = mat,
            strokeWidth = strokeWidth
        };

        AppendPointToStroke(currentSurfacePoint, currentSurfaceNormal, lastHitTriangleIndex, lastHitBarycentric);
    }

    private void AppendPointToStroke(Vector3 worldPoint, Vector3 worldNormal, int triIndex, Vector3 barycentric)
    {
        if (currentStroke == null || cachedBakedVertices == null)
            return;

        float scaleFactor = GetLossyScaleFactor();
        float worldOffset = 0.0035f;
        float localNormalOffset = worldOffset / scaleFactor;

        Vector3 pointOnSurface = worldPoint + worldNormal * worldOffset;

        if (currentStroke.anchors.Count > 0)
        {
            Vector3 lastPtLocal = EvaluatePosition(currentStroke.anchors[currentStroke.anchors.Count - 1], cachedBakedVertices, cachedBakedNormals);
            Vector3 lastPtWorld = strokeContainer.transform.TransformPoint(lastPtLocal);
            float dist = Vector3.Distance(pointOnSurface, lastPtWorld);
            if (dist < minPointSpacing)
                return;
            accumulatedDistance += dist;
        }

        int i0 = -1, i1 = -1, i2 = -1;
        Vector3 bary = Vector3.zero;

        if (triIndex >= 0 && cachedTriangles != null && (triIndex * 3 + 2) < cachedTriangles.Length)
        {
            i0 = cachedTriangles[triIndex * 3 + 0];
            i1 = cachedTriangles[triIndex * 3 + 1];
            i2 = cachedTriangles[triIndex * 3 + 2];
            bary = barycentric;
        }
        else
        {
            Vector3 localHit = strokeContainer.transform.InverseTransformPoint(worldPoint);
            int closest = FindClosestVertex(cachedBakedVertices, localHit);
            i0 = i1 = i2 = closest;
            bary = new Vector3(1f, 0f, 0f);
        }

        AnchorPoint anchor = new AnchorPoint
        {
            i0 = i0,
            i1 = i1,
            i2 = i2,
            bary = bary,
            normalOffset = localNormalOffset,
            fallbackLocalPos = strokeContainer.transform.InverseTransformPoint(worldPoint),
            fallbackLocalNorm = strokeContainer.transform.InverseTransformDirection(worldNormal).normalized
        };

        currentStroke.anchors.Add(anchor);
        RebuildStrokeMeshFromAnchors(currentStroke, cachedBakedVertices, cachedBakedNormals);
    }

    private static Vector3 EvaluatePosition(AnchorPoint anchor, Vector3[] V, Vector3[] N)
    {
        if (V != null && anchor.i0 >= 0 && anchor.i0 < V.Length &&
            anchor.i1 >= 0 && anchor.i1 < V.Length &&
            anchor.i2 >= 0 && anchor.i2 < V.Length)
        {
            Vector3 surfacePos = V[anchor.i0] * anchor.bary.x +
                                 V[anchor.i1] * anchor.bary.y +
                                 V[anchor.i2] * anchor.bary.z;
            Vector3 surfaceNorm = Vector3.up;
            if (N != null && anchor.i0 < N.Length && anchor.i1 < N.Length && anchor.i2 < N.Length)
            {
                surfaceNorm = N[anchor.i0] * anchor.bary.x +
                              N[anchor.i1] * anchor.bary.y +
                              N[anchor.i2] * anchor.bary.z;
                if (surfaceNorm.sqrMagnitude > 0.0001f)
                    surfaceNorm.Normalize();
                else
                    surfaceNorm = anchor.fallbackLocalNorm;
            }
            return surfacePos + surfaceNorm * anchor.normalOffset;
        }

        return anchor.fallbackLocalPos + anchor.fallbackLocalNorm * anchor.normalOffset;
    }

    private static Vector3 EvaluateNormal(AnchorPoint anchor, Vector3[] N)
    {
        if (N != null && anchor.i0 >= 0 && anchor.i0 < N.Length &&
            anchor.i1 >= 0 && anchor.i1 < N.Length &&
            anchor.i2 >= 0 && anchor.i2 < N.Length)
        {
            Vector3 surfaceNorm = N[anchor.i0] * anchor.bary.x +
                                  N[anchor.i1] * anchor.bary.y +
                                  N[anchor.i2] * anchor.bary.z;
            if (surfaceNorm.sqrMagnitude > 0.0001f)
                return surfaceNorm.normalized;
        }

        return anchor.fallbackLocalNorm;
    }

    private void RebuildStrokeMeshFromAnchors(StrokeRecord record, Vector3[] V, Vector3[] N)
    {
        if (record == null || record.strokeMesh == null || record.anchors == null)
            return;

        int count = record.anchors.Count;
        if (count < 2)
            return;

        int vertCount = count * 2;
        int triCount = (count - 1) * 6;

        Vector3[] vertices = new Vector3[vertCount];
        Vector3[] normals = new Vector3[vertCount];
        Vector2[] uvs = new Vector2[vertCount];
        Color[] colors = new Color[vertCount];
        int[] triangles = new int[triCount];

        Vector3[] pts = new Vector3[count];
        Vector3[] norms = new Vector3[count];

        for (int i = 0; i < count; i++)
        {
            pts[i] = EvaluatePosition(record.anchors[i], V, N);
            norms[i] = EvaluateNormal(record.anchors[i], N);
        }

        float currentDist = 0f;
        float scaleFactor = GetLossyScaleFactor();
        // Scale stroke width to world units
        float localStrokeWidth = record.strokeWidth / scaleFactor;
        float halfWidth = localStrokeWidth * 0.5f;
        // Textures have 4:1 aspect ratio, so repeat length is 4x the width
        float patternLength = localStrokeWidth * 4f;

        for (int i = 0; i < count; i++)
        {
            Vector3 pt = pts[i];
            Vector3 norm = norms[i];

            if (i > 0)
                currentDist += Vector3.Distance(pt, pts[i - 1]);

            Vector3 tangent;
            if (i == 0)
                tangent = (pts[1] - pts[0]).normalized;
            else if (i == count - 1)
                tangent = (pts[count - 1] - pts[count - 2]).normalized;
            else
                tangent = (pts[i + 1] - pts[i - 1]).normalized;

            Vector3 binormal = Vector3.Cross(tangent, norm).normalized;
            if (binormal.sqrMagnitude < 0.001f)
                binormal = Vector3.right;

            Vector3 leftPos = pt - binormal * halfWidth;
            Vector3 rightPos = pt + binormal * halfWidth;

            float u = currentDist / patternLength;

            int leftIdx = i * 2;
            int rightIdx = i * 2 + 1;

            vertices[leftIdx] = leftPos;
            vertices[rightIdx] = rightPos;

            normals[leftIdx] = norm;
            normals[rightIdx] = norm;

            uvs[leftIdx] = new Vector2(u, 0f);
            uvs[rightIdx] = new Vector2(u, 1f);

            colors[leftIdx] = Color.white;
            colors[rightIdx] = Color.white;

            if (i < count - 1)
            {
                int triIdx = i * 6;
                triangles[triIdx + 0] = leftIdx;
                triangles[triIdx + 1] = rightIdx;
                triangles[triIdx + 2] = leftIdx + 2;

                triangles[triIdx + 3] = rightIdx;
                triangles[triIdx + 4] = rightIdx + 2;
                triangles[triIdx + 5] = leftIdx + 2;
            }
        }

        record.strokeMesh.Clear();
        record.strokeMesh.vertices = vertices;
        record.strokeMesh.normals = normals;
        record.strokeMesh.uv = uvs;
        record.strokeMesh.colors = colors;
        record.strokeMesh.triangles = triangles;
        record.strokeMesh.RecalculateBounds();
    }

    private static int FindClosestVertex(Vector3[] verts, Vector3 localPos)
    {
        if (verts == null || verts.Length == 0)
            return 0;

        int closest = 0;
        float minDist = float.MaxValue;
        for (int i = 0; i < verts.Length; i++)
        {
            float d = (verts[i] - localPos).sqrMagnitude;
            if (d < minDist)
            {
                minDist = d;
                closest = i;
            }
        }
        return closest;
    }

    private void FinishCurrentStroke()
    {
        if (!isDrawingActive)
            return;

        isDrawingActive = false;
        if (currentStroke != null)
        {
            if (currentStroke.anchors.Count >= 2)
            {
                completedStrokes.Add(currentStroke);
            }
            else
            {
                if (currentStroke.strokeObject != null)
                    Destroy(currentStroke.strokeObject);
            }
        }

        currentStroke = null;
    }

    public void RefreshCollider()
    {
        if (teapotRenderer == null)
            return;

        if (surfaceCollider == null)
        {
            surfaceCollider = teapotRenderer.GetComponent<MeshCollider>();
            if (surfaceCollider == null)
                surfaceCollider = teapotRenderer.gameObject.AddComponent<MeshCollider>();
        }

        if (bakedColliderMesh == null)
        {
            bakedColliderMesh = new Mesh { name = "TeapotSurfaceColliderBake" };
            bakedColliderMesh.MarkDynamic();
        }

        teapotRenderer.BakeMesh(bakedColliderMesh);
        cachedBakedVertices = bakedColliderMesh.vertices;
        cachedBakedNormals = bakedColliderMesh.normals;
        if (cachedTriangles == null && teapotRenderer.sharedMesh != null)
            cachedTriangles = teapotRenderer.sharedMesh.triangles;

        surfaceCollider.sharedMesh = null;
        surfaceCollider.sharedMesh = bakedColliderMesh;
        surfaceCollider.enabled = true;
    }

    private void EnsureCollider()
    {
        if (surfaceCollider == null && teapotRenderer != null)
            RefreshCollider();
    }

    private void EnsureStrokeContainer()
    {
        if (strokeContainer != null)
            return;

        Transform parent = teapotRenderer != null ? teapotRenderer.transform : transform;
        strokeContainer = new GameObject("DrawnPatternStrokes");
        strokeContainer.transform.SetParent(parent, false);
    }

    private void EnsureCursor()
    {
        if (cursorObject != null)
            return;

        cursorObject = new GameObject("BrushCursor");
        cursorObject.transform.SetParent(transform, false);

        Shader unlitShader = Shader.Find("Sprites/Default");
        if (unlitShader == null)
            unlitShader = Shader.Find("Universal Render Pipeline/Unlit");

        cursorMaterial = new Material(unlitShader)
        {
            name = "BrushCursorMaterial"
        };
        cursorMaterial.color = new Color(0.1f, 0.85f, 1f, 0.9f);

        cursorRing = cursorObject.AddComponent<LineRenderer>();
        cursorRing.useWorldSpace = false;
        cursorRing.loop = true;
        cursorRing.startWidth = 0.02f;
        cursorRing.endWidth = 0.02f;
        cursorRing.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        cursorRing.receiveShadows = false;
        cursorRing.sharedMaterial = cursorMaterial;

        int segments = 24;
        cursorRing.positionCount = segments;
        UpdateCursorRingGeometry();

        GameObject dot = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        dot.name = "CursorCenterDot";
        dot.transform.SetParent(cursorObject.transform, false);
        dot.transform.localScale = Vector3.one * 0.024f;
        Collider dotCol = dot.GetComponent<Collider>();
        if (dotCol != null)
            Destroy(dotCol);
        MeshRenderer dotMr = dot.GetComponent<MeshRenderer>();
        if (dotMr != null)
            dotMr.sharedMaterial = cursorMaterial;

        cursorObject.SetActive(false);
    }

    private void EnsureMaterial()
    {
        if (strokeMaterial != null)
            return;

        Shader shader = MorphicOverlayShaders.PatternRibbon();
        if (shader == null)
            shader = Shader.Find("Universal Render Pipeline/Unlit");

        strokeMaterial = new Material(shader)
        {
            name = "Morphic Pattern Ribbon Material"
        };
        if (strokeMaterial.HasProperty("_Opacity"))
            strokeMaterial.SetFloat("_Opacity", 1f);
        if (strokeMaterial.HasProperty("_Color"))
            strokeMaterial.SetColor("_Color", strokeColor);
        if (strokeMaterial.HasProperty("_BaseColor"))
            strokeMaterial.SetColor("_BaseColor", strokeColor);
        if (activePatternTexture != null)
        {
            strokeMaterial.mainTexture = activePatternTexture;
            if (strokeMaterial.HasProperty("_MainTex"))
                strokeMaterial.SetTexture("_MainTex", activePatternTexture);
        }
    }

    private Camera ResolveCamera()
    {
        if (Camera.main != null)
            return Camera.main;

        GameObject mainCam = GameObject.Find("Main Camera");
        if (mainCam != null)
        {
            Camera cam = mainCam.GetComponent<Camera>();
            if (cam != null)
                return cam;
        }

        GameObject menuCam = GameObject.Find("MainMenuViewCamera");
        if (menuCam != null)
        {
            Camera cam = menuCam.GetComponent<Camera>();
            if (cam != null)
                return cam;
        }

        Camera[] cameras = FindObjectsByType<Camera>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        return cameras != null && cameras.Length > 0 ? cameras[0] : null;
    }
}
