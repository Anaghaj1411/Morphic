using System;
using System.Collections.Generic;
using Mediapipe.Unity.Sample.HandLandmarkDetection;
using UnityEngine;

/// <summary>
/// Handles real-time interactive 3D pattern drawing directly onto the surface of the teapot.
/// Features:
/// 1. Bold, prominent ribbon stroke sizing scaled properly to the teapot's world dimensions.
/// 2. Stabilizes noisy hand tracking with a One Euro filter plus a lazy-pull
///    brush so strokes stay controlled instead of following every jitter.
/// 3. Only draws when an isolated index finger pointing gesture is detected.
/// 4. Open palm gesture locks/unlocks drawing to prevent accidental marks.
/// 5. Mathematically anchors all stroke points to teapot mesh surface triangles via barycentric coordinates,
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
    [SerializeField, Range(0.003f, 0.03f)] private float minPointSpacing = 0.011f;
    [SerializeField] private Color strokeColor = Color.white;

    [Header("Stroke Stabilization")]
    [SerializeField, Range(0f, 1f)] private float stabilization = 0.78f;
    [SerializeField, Range(0.4f, 3f)] private float oneEuroMinCutoff = 0.85f;
    [SerializeField, Range(0.001f, 0.05f)] private float oneEuroBeta = 0.008f;
    [SerializeField, Range(8f, 72f)] private float pullPixels = 34f;

    private readonly List<StrokeRecord> completedStrokes = new List<StrokeRecord>();
    private readonly List<StrokeRecord> clayStrokes = new List<StrokeRecord>();
    private StrokeRecord currentStroke;
    private bool clayCoilMode;
    private float clayLift = 1.15f;
    private Color clayColor = ClayColor;
    private GameObject clayContainer;
    private Material clayMaterial;
    private static readonly Color ClayColor = new Color(0.74f, 0.42f, 0.30f, 1f);

    private MeshCollider surfaceCollider;
    private Mesh bakedColliderMesh;
    private Mesh deformBakeMesh;
    private int[] cachedTriangles;
    private Vector3[] cachedBakedVertices;
    private Vector3[] cachedBakedNormals;
    private float[] cachedBlendWeights;

    private GameObject strokeContainer;
    private GameObject cursorObject;
    private Transform cursorCenterDot;
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

    private bool pointerFilterReady;
    private float filteredPointerX;
    private float filteredPointerY;
    private float filteredPointerDx;
    private float filteredPointerDy;
    private bool lazyPointerReady;
    private Vector2 lazyScreen;

    public int StrokeCount => completedStrokes.Count;
    public int ClayStrokeCount => clayStrokes.Count;
    public bool IsDrawing => isDrawingActive;
    public bool ClayCoilMode => clayCoilMode;
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
        public float clayLift;
        public bool isClayCoil;
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
        strokeWidth = Mathf.Clamp(width, 0.005f, 0.22f);
        UpdateCursorRingGeometry();

        if (updateExisting && cachedBakedVertices != null)
        {
            List<StrokeRecord> list = clayCoilMode ? clayStrokes : completedStrokes;
            for (int i = 0; i < list.Count; i++)
            {
                if (list[i] != null)
                {
                    list[i].strokeWidth = strokeWidth;
                    RebuildStrokeMeshFromAnchors(list[i], cachedBakedVertices, cachedBakedNormals);
                }
            }
        }
    }

    public void SetClayCoilMode(bool enabled)
    {
        if (clayCoilMode == enabled)
            return;

        if (isDrawingActive)
            FinishCurrentStroke();

        clayCoilMode = enabled;
        if (enabled)
        {
            EnsureClayContainer();
            EnsureClayMaterial();
            SetPatternTexture(null);
            ApplyClayColorToMaterials(clayColor, false);
            RefreshCollider();
        }

        UpdateCursorRingGeometry();
    }

    public void SetClayLift(float lift)
    {
        clayLift = Mathf.Clamp(lift, 0.85f, 1.8f);
    }

    public void SetClayColor(Color color, bool updateExisting = true)
    {
        clayColor = color;
        ApplyClayColorToMaterials(color, updateExisting);
    }

    private void ApplyClayColorToMaterials(Color color, bool updateExisting)
    {
        EnsureClayMaterial();
        if (clayMaterial != null)
        {
            clayMaterial.color = color;
            if (clayMaterial.HasProperty("_Color"))
                clayMaterial.SetColor("_Color", color);
            if (clayMaterial.HasProperty("_BaseColor"))
                clayMaterial.SetColor("_BaseColor", color);
        }

        if (!updateExisting)
            return;

        for (int i = 0; i < clayStrokes.Count; i++)
        {
            if (clayStrokes[i] == null || clayStrokes[i].material == null)
                continue;
            if (clayStrokes[i].material.HasProperty("_Color"))
                clayStrokes[i].material.SetColor("_Color", color);
            if (clayStrokes[i].material.HasProperty("_BaseColor"))
                clayStrokes[i].material.SetColor("_BaseColor", color);
        }

        if (currentStroke != null && currentStroke.isClayCoil && currentStroke.material != null)
        {
            if (currentStroke.material.HasProperty("_Color"))
                currentStroke.material.SetColor("_Color", color);
            if (currentStroke.material.HasProperty("_BaseColor"))
                currentStroke.material.SetColor("_BaseColor", color);
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
            ResetStabilizer();
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

    public void UndoLastClayStroke()
    {
        if (clayStrokes.Count == 0)
            return;

        int lastIndex = clayStrokes.Count - 1;
        StrokeRecord last = clayStrokes[lastIndex];
        clayStrokes.RemoveAt(lastIndex);
        if (last != null && last.strokeObject != null)
            Destroy(last.strokeObject);
    }

    public void ClearClayCoils()
    {
        if (isDrawingActive && currentStroke != null && currentStroke.isClayCoil)
            FinishCurrentStroke();

        for (int i = 0; i < clayStrokes.Count; i++)
        {
            if (clayStrokes[i] != null && clayStrokes[i].strokeObject != null)
                Destroy(clayStrokes[i].strokeObject);
        }
        clayStrokes.Clear();
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

        SculptHandFrame hand = default;
        bool hasHandData = isHandTracked && handRunner != null && handRunner.TryGetLatestHand(out hand);
        UpdateOpenPalmLock(hasHandData, hand);

        bool pointingNow = hasHandData && IsIndexPointing(hand, isDrawingActive, clayCoilMode);
        Ray ray = StabilizedRay(viewCamera, screenPosition);
        PerformRaycast(ray);
        if (!isPointerOnSurface && pointingNow)
        {
            PerformRaycast(viewCamera.ScreenPointToRay(new Vector3(screenPosition.x, screenPosition.y, 0f)));
            if (isPointerOnSurface)
                ResetStabilizer();
        }
        if (!isPointerOnSurface && !isDrawingActive)
            lazyPointerReady = false;

        UpdateCursorVisual();
        ApplyDrawIntent(hasHandData, hand, isMouseClick || Input.GetMouseButton(0));
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

        bool hasHand = false;
        SculptHandFrame hand = default;
        Vector2 rawScreen = Input.mousePosition;
        if (handRunner != null && handRunner.TryGetLatestHand(out hand))
        {
            hasHand = true;
            rawScreen = viewCamera.ViewportToScreenPoint(
                new Vector3(hand.IndexTip.x, hand.IndexTip.y, 0f));
        }

        UpdateOpenPalmLock(hasHand, hand);

        bool pointingNow = hasHand && IsIndexPointing(hand, isDrawingActive, clayCoilMode);
        Ray ray = StabilizedRay(viewCamera, rawScreen);
        PerformRaycast(ray);
        if (!isPointerOnSurface && pointingNow)
        {
            PerformRaycast(viewCamera.ScreenPointToRay(new Vector3(rawScreen.x, rawScreen.y, 0f)));
            if (isPointerOnSurface)
                ResetStabilizer();
        }
        if (!isPointerOnSurface && !isDrawingActive)
            lazyPointerReady = false;

        UpdateCursorVisual();
        ApplyDrawIntent(hasHand, hand, Input.GetMouseButton(0));
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

        for (int i = 0; i < clayStrokes.Count; i++)
        {
            if (clayStrokes[i] != null)
                RebuildStrokeMeshFromAnchors(clayStrokes[i], cachedBakedVertices, cachedBakedNormals);
        }

        if (isDrawingActive && currentStroke != null)
        {
            RebuildStrokeMeshFromAnchors(currentStroke, cachedBakedVertices, cachedBakedNormals);
        }
    }

    public static bool IsIndexPointing(SculptHandFrame hand)
    {
        return IsIndexPointing(hand, false, false);
    }

    public static bool IsIndexPointing(SculptHandFrame hand, bool alreadyDrawing)
    {
        return IsIndexPointing(hand, alreadyDrawing, false);
    }

    public static bool IsIndexPointing(SculptHandFrame hand, bool alreadyDrawing, bool easyMode)
    {
        float palm = Mathf.Max(hand.PalmWidth, 0.001f);
        float minAngle;
        float minIndexReach;
        float maxOtherReach;
        float maxMiddleReach;
        float minPinch;
        if (easyMode)
        {
            minAngle = alreadyDrawing ? 58f : 70f;
            minIndexReach = alreadyDrawing ? 0.62f : 0.72f;
            maxOtherReach = alreadyDrawing ? 2.35f : 2.10f;
            maxMiddleReach = alreadyDrawing ? 1.85f : 1.55f;
            minPinch = alreadyDrawing ? palm * 0.05f : palm * 0.08f;
        }
        else
        {
            minAngle = alreadyDrawing ? 62f : 72f;
            minIndexReach = alreadyDrawing ? 0.64f : 0.74f;
            maxOtherReach = alreadyDrawing ? 2.20f : 2.00f;
            maxMiddleReach = alreadyDrawing ? 1.70f : 1.48f;
            minPinch = alreadyDrawing ? palm * 0.06f : palm * 0.10f;
        }

        bool indexOut = hand.IndexExtensionAngle >= minAngle &&
                        hand.IndexTipPalmRatio >= minIndexReach;
        bool othersOk = hand.OtherFingersCurlRatio <= maxOtherReach &&
                        hand.MiddleTipPalmRatio <= maxMiddleReach;
        bool notPinching = hand.PinchDistance >= minPinch;
        return indexOut && othersOk && notPinching;
    }

    public static bool IsOpenPalm(SculptHandFrame hand)
    {
        float palm = Mathf.Max(hand.PalmWidth, 0.001f);
        return hand.PinchDistance >= palm * 0.52f &&
               hand.FingerCurlRatio >= 1.42f &&
               hand.OtherFingersCurlRatio >= 1.40f &&
               hand.IndexTipPalmRatio >= 1.08f &&
               hand.MiddleTipPalmRatio >= 1.12f;
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
        else if (clayCoilMode)
        {
            cursorColor = new Color(clayColor.r, clayColor.g, clayColor.b, isDrawingActive ? 0.95f : 0.9f);
        }
        else if (isDrawingActive)
        {
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

        if (clayCoilMode)
        {
            float lineW = Mathf.Clamp(strokeWidth * 0.28f, 0.0012f, 0.007f);
            cursorRing.startWidth = lineW;
            cursorRing.endWidth = lineW;
            if (cursorCenterDot != null)
                cursorCenterDot.localScale = Vector3.one * Mathf.Max(0.003f, strokeWidth * 0.55f);
        }
        else
        {
            cursorRing.startWidth = 0.02f;
            cursorRing.endWidth = 0.02f;
            if (cursorCenterDot != null)
                cursorCenterDot.localScale = Vector3.one * 0.024f;
        }
    }

    private void BeginNewStroke()
    {
        EnsureStrokeContainer();
        EnsureClayContainer();
        EnsureMaterial();
        EnsureClayMaterial();

        isDrawingActive = true;
        accumulatedDistance = 0f;
        lazyPointerReady = true;

        bool clay = clayCoilMode;
        Transform parent = clay ? clayContainer.transform : strokeContainer.transform;
        string objectName = clay
            ? "ClayCoil_" + clayStrokes.Count
            : "PatternStroke_" + completedStrokes.Count;
        GameObject strokeObj = new GameObject(objectName);
        strokeObj.transform.SetParent(parent, false);

        MeshFilter mf = strokeObj.AddComponent<MeshFilter>();
        MeshRenderer mr = strokeObj.AddComponent<MeshRenderer>();

        Mesh mesh = new Mesh { name = clay ? "ClayCoilMesh" : "PatternStrokeMesh" };
        mesh.MarkDynamic();
        mf.sharedMesh = mesh;

        Material source = clay ? clayMaterial : strokeMaterial;
        Material mat = new Material(source);
        if (!clay && activePatternTexture != null)
        {
            mat.mainTexture = activePatternTexture;
            if (mat.HasProperty("_MainTex"))
                mat.SetTexture("_MainTex", activePatternTexture);
            if (mat.HasProperty("_BaseMap"))
                mat.SetTexture("_BaseMap", activePatternTexture);
        }

        Color color = clay ? clayColor : strokeColor;
        if (mat.HasProperty("_Color"))
            mat.SetColor("_Color", color);
        if (mat.HasProperty("_BaseColor"))
            mat.SetColor("_BaseColor", color);
        if (mat.HasProperty("_Opacity"))
            mat.SetFloat("_Opacity", 1f);

        mr.sharedMaterial = mat;

        currentStroke = new StrokeRecord
        {
            strokeObject = strokeObj,
            strokeMesh = mesh,
            material = mat,
            strokeWidth = strokeWidth,
            clayLift = clayLift,
            isClayCoil = clay
        };

        AppendPointToStroke(currentSurfacePoint, currentSurfaceNormal, lastHitTriangleIndex, lastHitBarycentric);
    }

    private void AppendPointToStroke(Vector3 worldPoint, Vector3 worldNormal, int triIndex, Vector3 barycentric)
    {
        if (currentStroke == null || cachedBakedVertices == null)
            return;

        float scaleFactor = GetLossyScaleFactor();
        float worldOffset = currentStroke.isClayCoil ? 0.0015f : 0.0035f;
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

        if (record.isClayCoil)
        {
            RebuildClayCoilMesh(record, V, N);
            return;
        }

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
                if (currentStroke.isClayCoil)
                    clayStrokes.Add(currentStroke);
                else
                    completedStrokes.Add(currentStroke);
            }
            else
            {
                if (currentStroke.strokeObject != null)
                    Destroy(currentStroke.strokeObject);
            }
        }

        currentStroke = null;
        lazyPointerReady = false;
    }

    private void ApplyDrawIntent(bool hasHandData, SculptHandFrame hand, bool mouseHeld)
    {
        bool shouldDraw = false;
        if (isPointerOnSurface && !isDrawingLocked)
        {
            if (hasHandData)
                shouldDraw = IsIndexPointing(hand, isDrawingActive, clayCoilMode);
            else
                shouldDraw = mouseHeld;
        }

        if (shouldDraw)
        {
            if (!isDrawingActive)
            {
                ResetStabilizer();
                BeginNewStroke();
            }

            AppendPointToStroke(
                currentSurfacePoint,
                currentSurfaceNormal,
                lastHitTriangleIndex,
                lastHitBarycentric);
        }
        else if (isDrawingActive)
        {
            FinishCurrentStroke();
        }
    }

    private void UpdateOpenPalmLock(bool hasHandData, SculptHandFrame hand)
    {
        if (!hasHandData)
            return;

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
        float pull = isDrawingActive ? drawPull : hoverPull;

        if (!lazyPointerReady)
        {
            lazyScreen = filtered;
            lazyPointerReady = true;
            return lazyScreen;
        }

        Vector2 delta = filtered - lazyScreen;
        float dist = delta.magnitude;
        if (dist > pull)
        {
            lazyScreen = filtered - delta.normalized * pull;
        }
        else if (!isDrawingActive)
        {
            float catchUp = 1f - Mathf.Exp(-dt * 16f);
            lazyScreen = Vector2.Lerp(lazyScreen, filtered, catchUp);
        }

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

    private void EnsureClayContainer()
    {
        if (clayContainer != null)
            return;

        Transform parent = teapotRenderer != null ? teapotRenderer.transform : transform;
        clayContainer = new GameObject("ClayCoilStrokes");
        clayContainer.transform.SetParent(parent, false);
    }

    private void EnsureClayMaterial()
    {
        if (clayMaterial != null)
            return;

        Shader shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null)
            shader = Shader.Find("Universal Render Pipeline/Unlit");
        if (shader == null)
            shader = Shader.Find("Sprites/Default");
        if (shader == null)
            return;

        clayMaterial = new Material(shader)
        {
            name = "Morphic Clay Coil Material"
        };
        clayMaterial.color = clayColor;
        if (clayMaterial.HasProperty("_BaseColor"))
            clayMaterial.SetColor("_BaseColor", clayColor);
        if (clayMaterial.HasProperty("_Color"))
            clayMaterial.SetColor("_Color", clayColor);
        if (clayMaterial.HasProperty("_Smoothness"))
            clayMaterial.SetFloat("_Smoothness", 0.28f);
        if (clayMaterial.HasProperty("_Metallic"))
            clayMaterial.SetFloat("_Metallic", 0f);
    }

    private void RebuildClayCoilMesh(StrokeRecord record, Vector3[] V, Vector3[] N)
    {
        int count = record.anchors.Count;
        const int sides = 8;
        float scaleFactor = GetLossyScaleFactor();
        float radius = (record.strokeWidth / scaleFactor) * 0.5f;
        float lift = radius * Mathf.Max(0.9f, record.clayLift);

        Vector3[] pts = new Vector3[count];
        Vector3[] norms = new Vector3[count];
        for (int i = 0; i < count; i++)
        {
            pts[i] = EvaluatePosition(record.anchors[i], V, N);
            norms[i] = EvaluateNormal(record.anchors[i], N);
        }

        int vertCount = count * sides;
        int triCount = (count - 1) * sides * 6;
        Vector3[] vertices = new Vector3[vertCount];
        Vector3[] normals = new Vector3[vertCount];
        Vector2[] uvs = new Vector2[vertCount];
        int[] triangles = new int[triCount];

        for (int i = 0; i < count; i++)
        {
            Vector3 tangent;
            if (i == 0)
                tangent = (pts[1] - pts[0]).normalized;
            else if (i == count - 1)
                tangent = (pts[count - 1] - pts[count - 2]).normalized;
            else
                tangent = (pts[i + 1] - pts[i - 1]).normalized;
            if (tangent.sqrMagnitude < 0.0001f)
                tangent = Vector3.forward;

            Vector3 n = norms[i].sqrMagnitude > 0.0001f ? norms[i].normalized : Vector3.up;
            Vector3 binormal = Vector3.Cross(tangent, n);
            if (binormal.sqrMagnitude < 0.0001f)
                binormal = Vector3.Cross(Vector3.right, n);
            binormal.Normalize();
            n = Vector3.Cross(binormal, tangent).normalized;
            Vector3 center = pts[i] + n * lift;

            for (int s = 0; s < sides; s++)
            {
                float angle = s / (float)sides * Mathf.PI * 2f;
                Vector3 radial = (binormal * Mathf.Cos(angle) + n * Mathf.Sin(angle)) * radius;
                int idx = i * sides + s;
                vertices[idx] = center + radial;
                normals[idx] = radial.sqrMagnitude > 0.0001f ? radial.normalized : n;
                uvs[idx] = new Vector2(s / (float)sides, i / Mathf.Max(1f, count - 1f));
            }
        }

        int tri = 0;
        for (int i = 0; i < count - 1; i++)
        {
            for (int s = 0; s < sides; s++)
            {
                int sNext = (s + 1) % sides;
                int i0 = i * sides + s;
                int i1 = i * sides + sNext;
                int i2 = (i + 1) * sides + s;
                int i3 = (i + 1) * sides + sNext;
                triangles[tri++] = i0;
                triangles[tri++] = i2;
                triangles[tri++] = i1;
                triangles[tri++] = i1;
                triangles[tri++] = i2;
                triangles[tri++] = i3;
            }
        }

        record.strokeMesh.Clear();
        record.strokeMesh.vertices = vertices;
        record.strokeMesh.normals = normals;
        record.strokeMesh.uv = uvs;
        record.strokeMesh.triangles = triangles;
        record.strokeMesh.RecalculateNormals();
        record.strokeMesh.RecalculateBounds();
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
        cursorCenterDot = dot.transform;
        cursorCenterDot.SetParent(cursorObject.transform, false);
        cursorCenterDot.localScale = Vector3.one * 0.024f;
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
