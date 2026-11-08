using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// Procedural Keris handles. No shape keys, no FBX handle mesh.
/// Handles are designed in code and grown out of the blade tang.
/// </summary>
[ExecuteInEditMode]
[ExecuteAlways]
[DefaultExecutionOrder(400)]
[DisallowMultipleComponent]
public sealed class KerisHandleOverlay : MonoBehaviour
{
    private const string ChildName = "KerisHandle";
    private const string GrowName = "KerisHandleGrow";
    public const float HandleMaxAmount = 35f;
    private const bool UseCodedMesh = true;
    private static readonly Vector3 HandlePosStraight = new Vector3(0.1f, -1.002f, 0.03f);
    private static readonly Vector3 HandlePosCurved = new Vector3(-0.1f, -0.662f, 0.07f);
    private static readonly Vector3 HandleLocalEuler = new Vector3(0.163f, 284.306f, -178.884f);
    private static readonly Vector3 HandleEulerCurved = new Vector3(0.489f, 296.203f, -179.562f);
    private static readonly Vector3 HandleLocalScale = new Vector3(3f, 0.4f, 4f);

    [Header("Coded handles")]
    [Range(0f, HandleMaxAmount)]
    public float handle1Amount;

    [Range(0f, HandleMaxAmount)]
    public float handle2Amount;

    [Header("Handle 1 or Handle 2")]
    [Tooltip("Also on the KerisHandle object in the Scene view.")]
    public bool useHandle2;

    [Header("Scene placement")]
    [Tooltip("Off = drag KerisHandle in the Scene view. Turn on after the pose is locked.")]
    [SerializeField] private bool lockHandlePose = true;

    private Transform handleRoot;
    private Transform growRoot;
    private MeshFilter handleFilter;
    private MeshRenderer handleRenderer;
    private Mesh handleMesh;
    private Material woodMat;
    private Material goldMat;
    private bool hasTint;
    private Color tintColor = Color.white;
    private bool built;
    private float lastHandle1Amount = float.NaN;
    private float lastHandle2Amount = float.NaN;
    private bool lastDrewHandle2;
    private float lastPushedControl1 = float.NaN;
    private float lastPushedControl2 = float.NaN;
    private float lastFollowCurve = float.NaN;
    private float lastFollowSharp = float.NaN;
    private float lastFollowWidth = float.NaN;
    private float lastFollowHeight = float.NaN;
    private Vector3[] restVerts;
    private int[] endIdx;
    private int[] innerIdx;
    private Vector3[] shapeDelta;
    private Vector3 restLocalLong;
    private Vector3 restLocalThin;
    private Vector3 restTangDirection;
    private Vector3[] liveEndLocal;
    private Mesh bakeMesh;
    private float restMinL;
    private float restMaxL;
    private bool restTangAtMin;
    private int followN;
    private float followMag;
    private float restWx;
    private float restWy;
    private float restWz;
    private float restBladeWidthWorld;
    private float warnTimer;

    private Vector3 tangOrigin;
    private Vector3 tangDirection;
    private Vector3 tangUp;
    private Vector3 worldTang;
    private Vector3 worldOut;
    private Vector3 worldAcross;
    private Vector3 worldThin;
    private float bladeLengthWorld;
    private float bladeWidthWorld;
    private bool tangCalculated;

    public static KerisHandleOverlay Ensure(SkinnedMeshRenderer skin)
    {
        if (skin == null)
            return null;
        return EnsureOn(skin.gameObject);
    }

    public static KerisHandleOverlay EnsureOn(GameObject host)
    {
        if (host == null)
            return null;

        KerisHandleOverlay overlay = host.GetComponent<KerisHandleOverlay>();
        if (overlay == null)
            overlay = host.AddComponent<KerisHandleOverlay>();
        if (!UseCodedMesh)
        {
            overlay.HideCodedChild();
            overlay.PushControlSliders(overlay.handle1Amount, overlay.handle2Amount);
            return overlay;
        }
        if (overlay.built && overlay.handleRoot != null)
            return overlay;
        overlay.NeutralizeOldHandleShapes();
        overlay.Build();
        overlay.SetAmount(overlay.handle1Amount, overlay.handle2Amount);
        return overlay;
    }

    public void SetAmount(float handle1, float handle2)
    {
        handle1Amount = Mathf.Clamp(handle1, 0f, HandleMaxAmount);
        handle2Amount = Mathf.Clamp(handle2, 0f, HandleMaxAmount);
        PushControlSliders(handle1Amount, handle2Amount);
        ApplyAmount(true);
    }

    public void SetHandleVariant(bool useSecond)
    {
        useHandle2 = useSecond;
        ApplyAmount(true);
    }

    public void SetColor(Color color)
    {
        hasTint = true;
        tintColor = color;
        ApplyStoredTint();
    }

    private void ApplyStoredTint()
    {
        if (!hasTint)
            return;
        Color gold = Color.Lerp(tintColor, Color.white, 0.28f);
        TintMaterial(woodMat, tintColor);
        TintMaterial(goldMat, gold);
        if (handleRenderer == null)
            return;
        Material[] materials = handleRenderer.sharedMaterials;
        for (int i = 0; i < materials.Length; i++)
            TintMaterial(materials[i], i == 1 ? gold : tintColor);
        handleRenderer.sharedMaterials = materials;
    }

    private static void TintMaterial(Material material, Color color)
    {
        if (material == null)
            return;
        if (material.HasProperty("_BaseColor"))
            material.SetColor("_BaseColor", color);
        if (material.HasProperty("_Color"))
            material.SetColor("_Color", color);
    }

    [ContextMenu("Rebuild Handle Now")]
    public void RebuildNow()
    {
        if (!UseCodedMesh)
        {
            HideCodedChild();
            PushControlSliders(handle1Amount, handle2Amount);
            return;
        }
        built = false;
        NeutralizeOldHandleShapes();
        Build();
        ApplyAmount(true);
    }

    private void OnEnable()
    {
        if (!UseCodedMesh)
        {
            HideCodedChild();
            PushControlSliders(handle1Amount, handle2Amount);
            return;
        }
        built = false;
        NeutralizeOldHandleShapes();
        Build();
    }

    private void Reset()
    {
        if (!UseCodedMesh)
        {
            HideCodedChild();
            return;
        }
        built = false;
        NeutralizeOldHandleShapes();
        Build();
    }

    private void OnValidate()
    {
        handle1Amount = Mathf.Clamp(handle1Amount, 0f, HandleMaxAmount);
        handle2Amount = Mathf.Clamp(handle2Amount, 0f, HandleMaxAmount);
        if (handle2Amount > handle1Amount + 0.5f)
            useHandle2 = true;
        else if (handle1Amount > handle2Amount + 0.5f)
            useHandle2 = false;
        if (built)
        {
            PushControlSliders(handle1Amount, handle2Amount);
            ApplyAmount(true);
        }
    }

    private void LateUpdate()
    {
        if (!UseCodedMesh)
        {
            HideCodedChild();
            PullControlSliders();
            PushControlSliders(handle1Amount, handle2Amount);
            return;
        }
        if (!built)
            Build();
        PullControlSliders();
        ApplyHandleLocalPose();
        float active = ActiveHandleAmount();
        if (handleRenderer != null)
            handleRenderer.enabled = !Application.isPlaying || active > 0.5f;
        ApplyAmount(false);
    }

    private void ApplyAmount(bool force)
    {
        if (!UseCodedMesh)
        {
            HideCodedChild();
            PushControlSliders(handle1Amount, handle2Amount);
            return;
        }
        if (!built || handleMesh == null)
            return;

        float activeAmount = ActiveHandleAmount();
        bool drawHandle2 = ActiveHandleIs2();
        ApplyHandleLocalPose();

        if (!force &&
            drawHandle2 == lastDrewHandle2 &&
            handleMesh != null &&
            handleMesh.vertexCount > 0)
            return;

        lastHandle1Amount = handle1Amount;
        lastHandle2Amount = handle2Amount;
        lastDrewHandle2 = drawHandle2;

        if (Application.isPlaying && activeAmount <= 0.5f)
        {
            if (handleRenderer != null)
                handleRenderer.enabled = false;
            return;
        }

        var verts = new List<Vector3>();
        var wood = new List<int>();
        var gold = new List<int>();

        if (drawHandle2)
            AddHandle2(verts, wood, gold, 1f);
        else
            AddHandle1(verts, wood, gold, 1f);

        handleMesh.Clear();
        handleMesh.SetVertices(verts);
        handleMesh.subMeshCount = 2;
        handleMesh.SetTriangles(wood, 0, false);
        handleMesh.SetTriangles(gold, 1, false);
        handleMesh.RecalculateNormals();
        handleMesh.RecalculateBounds();
        if (handleFilter != null)
            handleFilter.sharedMesh = handleMesh;
        if (handleRenderer != null)
            handleRenderer.enabled = true;
    }

    private void Build()
    {
        Mesh body = GetBodyMesh();
        if (body == null)
        {
            Warn("Keris has no readable mesh yet.");
            return;
        }

        RefreshLiveTang();

        Transform child = transform.Find(ChildName);
        bool created = child == null;
        if (created)
        {
            GameObject go = new GameObject(ChildName);
            child = go.transform;
            child.SetParent(transform, false);
            child.localPosition = HandlePosStraight;
            child.localEulerAngles = HandleLocalEuler;
            child.localScale = HandleLocalScale;
        }

        handleRoot = child;
        EnsureGrowRoot();
        Collider leftoverCollider = child.GetComponent<Collider>();
        if (leftoverCollider != null)
        {
            if (Application.isPlaying)
                Destroy(leftoverCollider);
            else
                DestroyImmediate(leftoverCollider);
        }
        ApplyHandleLocalPose();
        child.gameObject.SetActive(true);

        EnsureGrowRoot();
        MeshRenderer parentRend = child.GetComponent<MeshRenderer>();
        if (parentRend != null)
            parentRend.enabled = false;
        KerisHandleSceneEdit sceneEdit = child.GetComponent<KerisHandleSceneEdit>();
        if (sceneEdit == null)
        {
            sceneEdit = child.gameObject.AddComponent<KerisHandleSceneEdit>();
            sceneEdit.handle = useHandle2
                ? KerisHandleSceneEdit.Variant.Handle2
                : KerisHandleSceneEdit.Variant.Handle1;
        }

        if (handleMesh != null)
        {
            if (Application.isPlaying)
                Destroy(handleMesh);
            else
                DestroyImmediate(handleMesh);
        }

        handleMesh = new Mesh { name = "KerisHandleCoded" };
        handleMesh.hideFlags = HideFlags.DontSave;
        handleMesh.MarkDynamic();
        handleFilter.sharedMesh = handleMesh;
        if (woodMat == null)
            woodMat = MakeLitMaterial("KerisHandleWood", new Color(0.42f, 0.24f, 0.12f, 1f), 0.05f, 0.28f);
        if (goldMat == null)
            goldMat = MakeLitMaterial("KerisHandleGold", new Color(0.83f, 0.68f, 0.28f, 1f), 0.65f, 0.72f);
        ApplyStoredTint();
        handleRenderer.sharedMaterials = new[] { woodMat, goldMat };
        handleRenderer.enabled = true;
        handleRenderer.shadowCastingMode = ShadowCastingMode.On;
        handleRenderer.receiveShadows = true;
        handleRenderer.lightProbeUsage = LightProbeUsage.Off;
        handleRenderer.reflectionProbeUsage = ReflectionProbeUsage.Off;

        handleRoot = child;
        ApplyHandleLocalPose();
        built = true;
        lastHandle1Amount = float.NaN;
        lastHandle2Amount = float.NaN;
        ApplyAmount(true);
        Debug.Log("MORPHIC: Coded Keris handle on the blade tang.", this);
    }

    private void EnsureHandleParent()
    {
        if (handleRoot == null)
            return;
        if (handleRoot.parent != transform)
            handleRoot.SetParent(transform, true);
        handleRoot.gameObject.SetActive(true);
    }

    private void ApplyHandleLocalPose()
    {
        EnsureHandleParent();
        if (handleRoot == null)
            return;
        float curveT = 0f;
        float widthT = 0f;
        Vector3 heightDelta = Vector3.zero;
        SkinnedMeshRenderer skin = GetComponent<SkinnedMeshRenderer>();
        if (skin != null)
        {
            curveT = Mathf.Clamp01(ReadFollowWeight(skin, "Curve") / 100f);
            widthT = Mathf.Clamp01(ReadWidthWeight(skin) / 100f);
            if (restVerts == null)
            {
                Mesh body = GetBodyMesh();
                if (body != null)
                    CalculateTangPosition(body);
            }
            heightDelta = ShapeTangDelta("Height");
        }
        handleRoot.localPosition = Vector3.Lerp(HandlePosStraight, HandlePosCurved, curveT) + heightDelta;
        handleRoot.localRotation = Quaternion.Slerp(
            Quaternion.Euler(HandleLocalEuler),
            Quaternion.Euler(HandleEulerCurved),
            curveT);
        handleRoot.localScale = HandleLocalScale * Mathf.Lerp(1f, 1.45f, widthT);
        if (growRoot != null)
        {
            float grow = 1f;
            if (Application.isPlaying)
            {
                grow = Mathf.Clamp01(ActiveHandleAmount() / HandleMaxAmount);
                grow = grow * grow * (3f - 2f * grow);
            }
            growRoot.localPosition = Vector3.zero;
            growRoot.localRotation = Quaternion.identity;
            growRoot.localScale = Vector3.one * Mathf.Max(grow, 0.0001f);
        }
    }

    private void EnsureGrowRoot()
    {
        if (handleRoot == null)
            return;

        Transform grow = handleRoot.Find(GrowName);
        if (grow == null)
        {
            grow = new GameObject(GrowName).transform;
            grow.SetParent(handleRoot, false);
            grow.localPosition = Vector3.zero;
            grow.localRotation = Quaternion.identity;
            grow.localScale = Vector3.one;
        }

        growRoot = grow;
        handleFilter = grow.GetComponent<MeshFilter>();
        if (handleFilter == null)
            handleFilter = grow.gameObject.AddComponent<MeshFilter>();
        handleRenderer = grow.GetComponent<MeshRenderer>();
        if (handleRenderer == null)
            handleRenderer = grow.gameObject.AddComponent<MeshRenderer>();
    }

    [ContextMenu("Log Handle Pose")]
    private void LogHandlePose()
    {
        if (handleRoot == null)
            handleRoot = transform.Find(ChildName);
        if (handleRoot == null)
        {
            Debug.LogWarning("MORPHIC: No KerisHandle to log.", this);
            return;
        }

        Debug.Log(
            "MORPHIC: KerisHandle pose  pos=" + handleRoot.localPosition.ToString("F4") +
            "  euler=" + handleRoot.localEulerAngles.ToString("F3") +
            "  scale=" + handleRoot.localScale.ToString("F4"),
            this);
    }

    private Vector3 ShapeTangDelta(string shapeName)
    {
        SkinnedMeshRenderer skin = GetComponent<SkinnedMeshRenderer>();
        if (skin == null || skin.sharedMesh == null || restVerts == null || endIdx == null || endIdx.Length == 0)
            return Vector3.zero;

        Mesh src = skin.sharedMesh;
        if (shapeDelta == null || shapeDelta.Length != src.vertexCount)
            shapeDelta = new Vector3[src.vertexCount];

        for (int s = 0; s < src.blendShapeCount; s++)
        {
            if (!string.Equals(src.GetBlendShapeName(s), shapeName, System.StringComparison.OrdinalIgnoreCase))
                continue;
            float weight = skin.GetBlendShapeWeight(s);
            if (weight < 0.01f)
                return Vector3.zero;
            src.GetBlendShapeFrameVertices(s, 0, shapeDelta, null, null);
            float frame = Mathf.Max(src.GetBlendShapeFrameWeight(s, 0), 1f);
            float scale = weight / frame;
            Vector3 sum = Vector3.zero;
            for (int i = 0; i < endIdx.Length; i++)
                sum += shapeDelta[endIdx[i]] * scale;
            return sum / endIdx.Length;
        }
        return Vector3.zero;
    }

    private static float ReadWidthWeight(SkinnedMeshRenderer skin)
    {
        float width = ReadFollowWeight(skin, "Weight");
        if (width < 0.01f)
            width = ReadFollowWeight(skin, "Width");
        return width;
    }

    private Mesh GetBodyMesh()
    {
        SkinnedMeshRenderer skin = GetComponent<SkinnedMeshRenderer>();
        if (skin != null && skin.sharedMesh != null)
            return skin.sharedMesh;
        MeshFilter filter = GetComponent<MeshFilter>();
        return filter != null ? filter.sharedMesh : null;
    }

    private void RefreshLiveTang()
    {
        Mesh body = GetBodyMesh();
        if (body == null)
            return;
        if (restVerts == null || endIdx == null || endIdx.Length == 0)
            CalculateTangPosition(body);
        if (restVerts == null || endIdx == null || endIdx.Length == 0)
            return;

        Vector3[] live = restVerts;
        SkinnedMeshRenderer skin = GetComponent<SkinnedMeshRenderer>();
        if (skin != null)
        {
            if (bakeMesh == null)
            {
                bakeMesh = new Mesh();
                bakeMesh.hideFlags = HideFlags.HideAndDontSave;
            }
            skin.BakeMesh(bakeMesh);
            Vector3[] baked = bakeMesh.vertices;
            if (baked != null && baked.Length == restVerts.Length)
                live = baked;
        }

        if (liveEndLocal == null || liveEndLocal.Length != endIdx.Length)
            liveEndLocal = new Vector3[endIdx.Length];
        Vector3 endSum = Vector3.zero;
        for (int i = 0; i < endIdx.Length; i++)
        {
            liveEndLocal[i] = live[endIdx[i]];
            endSum += liveEndLocal[i];
        }
        tangOrigin = endSum / liveEndLocal.Length;

        if (innerIdx != null && innerIdx.Length > 0)
        {
            Vector3 innerSum = Vector3.zero;
            for (int i = 0; i < innerIdx.Length; i++)
                innerSum += live[innerIdx[i]];
            Vector3 outward = tangOrigin - innerSum / innerIdx.Length;
            tangDirection = outward.sqrMagnitude > 1e-8f
                ? outward.normalized
                : restLocalLong;
        }
        else
            tangDirection = restLocalLong;

        tangUp = restLocalThin;
        StoreWorldTang(restWx, restWy, restWz, restLocalLong);
        tangCalculated = true;
    }

    private void FollowCurvedBody()
    {
        followN = 0;
        followMag = 0f;
        SkinnedMeshRenderer skin = GetComponent<SkinnedMeshRenderer>();
        if (skin == null || restVerts == null)
            return;
        if (bakeMesh == null)
        {
            bakeMesh = new Mesh();
            bakeMesh.hideFlags = HideFlags.HideAndDontSave;
        }
        skin.BakeMesh(bakeMesh);
        Vector3[] baked = bakeMesh.vertices;
        if (baked == null || baked.Length != restVerts.Length)
            return;

        float len = Mathf.Max(restMaxL - restMinL, 0.001f);
        float tangL = restTangAtMin ? restMinL : restMaxL;
        Vector3 dSum = Vector3.zero;
        int n = 0;
        float band = len * 0.28f;
        for (int pass = 0; pass < 2 && n == 0; pass++)
        {
            if (pass == 1)
                band = len * 0.55f;
            dSum = Vector3.zero;
            for (int i = 0; i < restVerts.Length; i++)
            {
                float along = Vector3.Dot(restVerts[i], restLocalLong);
                if (Mathf.Abs(along - tangL) > band)
                    continue;
                Vector3 d = baked[i] - restVerts[i];
                if (d.sqrMagnitude < 1e-10f)
                    continue;
                dSum += d;
                n++;
            }
        }
        followN = n;
        if (n > 0)
        {
            Vector3 dLocal = dSum / n;
            Vector3 localTang = transform.InverseTransformPoint(worldTang);
            Vector3 moved = transform.TransformPoint(localTang + dLocal);
            Vector3 dW = moved - worldTang;
            dW -= worldOut * Vector3.Dot(dW, worldOut);
            followMag = dW.magnitude;
            worldTang += dW;
        }
    }

    private bool BodyDeformChanged()
    {
        SkinnedMeshRenderer skin = GetComponent<SkinnedMeshRenderer>();
        if (skin == null || skin.sharedMesh == null)
            return false;

        float curve = ReadFollowWeight(skin, "Curve");
        float sharp = ReadFollowWeight(skin, "Sharpness");
        float width = ReadFollowWeight(skin, "Weight");
        if (width == 0f)
            width = ReadFollowWeight(skin, "Width");
        float height = ReadFollowWeight(skin, "Height");
        bool changed =
            !Mathf.Approximately(curve, lastFollowCurve) ||
            !Mathf.Approximately(sharp, lastFollowSharp) ||
            !Mathf.Approximately(width, lastFollowWidth) ||
            !Mathf.Approximately(height, lastFollowHeight);
        lastFollowCurve = curve;
        lastFollowSharp = sharp;
        lastFollowWidth = width;
        lastFollowHeight = height;
        return changed;
    }

    private static float ReadFollowWeight(SkinnedMeshRenderer skin, string shapeName)
    {
        Mesh mesh = skin.sharedMesh;
        if (mesh == null)
            return 0f;
        for (int i = 0; i < mesh.blendShapeCount; i++)
        {
            if (string.Equals(mesh.GetBlendShapeName(i), shapeName, System.StringComparison.OrdinalIgnoreCase))
                return skin.GetBlendShapeWeight(i);
        }
        return 0f;
    }

    private void CalculateTangPosition(Mesh body)
    {
        Vector3[] verts = body.vertices;
        if (verts == null || verts.Length < 8)
        {
            FallbackTangPosition(body.bounds);
            return;
        }

        Bounds bounds = body.bounds;
        Vector3 size = bounds.size;

        float wx = transform.TransformVector(new Vector3(size.x, 0f, 0f)).magnitude;
        float wy = transform.TransformVector(new Vector3(0f, size.y, 0f)).magnitude;
        float wz = transform.TransformVector(new Vector3(0f, 0f, size.z)).magnitude;

        Vector3 localLong, localThin;
        if (wx >= wy && wx >= wz)
            localLong = Vector3.right;
        else if (wy >= wz)
            localLong = Vector3.up;
        else
            localLong = Vector3.forward;

        if (wx <= wy && wx <= wz)
            localThin = Vector3.right;
        else if (wy <= wz)
            localThin = Vector3.up;
        else
            localThin = Vector3.forward;

        if (localThin == localLong)
            localThin = Vector3.Cross(localLong, Vector3.up).sqrMagnitude > 0.01f
                ? Vector3.Cross(localLong, Vector3.up).normalized
                : Vector3.Cross(localLong, Vector3.right).normalized;

        Vector3 localAcross = Vector3.Cross(localThin, localLong).normalized;

        float minL = float.PositiveInfinity;
        float maxL = float.NegativeInfinity;
        for (int i = 0; i < verts.Length; i++)
        {
            float along = Vector3.Dot(verts[i], localLong);
            if (along < minL) minL = along;
            if (along > maxL) maxL = along;
        }

        float band = Mathf.Max((maxL - minL) * 0.03f, 0.004f);
        float thickMin = 0f;
        float thickMax = 0f;
        int nMin = 0;
        int nMax = 0;
        Vector3 sumMin = Vector3.zero;
        Vector3 sumMax = Vector3.zero;
        for (int i = 0; i < verts.Length; i++)
        {
            Vector3 v = verts[i];
            float along = Vector3.Dot(v, localLong);
            float thick = Mathf.Abs(Vector3.Dot(v, localThin)) + Mathf.Abs(Vector3.Dot(v, localAcross));
            if (along <= minL + band)
            {
                thickMin += thick;
                sumMin += v;
                nMin++;
            }
            if (along >= maxL - band)
            {
                thickMax += thick;
                sumMax += v;
                nMax++;
            }
        }

        if (nMin > 0) thickMin /= nMin;
        if (nMax > 0) thickMax /= nMax;
        bool tangAtMin = nMin > 0 && (nMax <= 0 || thickMin >= thickMax);
        if (tangAtMin)
            tangOrigin = nMin > 0 ? sumMin / nMin : localLong * minL;
        else
            tangOrigin = nMax > 0 ? sumMax / nMax : localLong * maxL;

        float length = Mathf.Max(maxL - minL, 0.001f);
        float innerAlong = tangAtMin ? minL + length * 0.14f : maxL - length * 0.14f;
        float innerBand = length * 0.07f;
        Vector3 innerSum = Vector3.zero;
        int nInner = 0;
        var endList = new List<int>(64);
        var innerList = new List<int>(64);
        for (int i = 0; i < verts.Length; i++)
        {
            float along = Vector3.Dot(verts[i], localLong);
            if (tangAtMin ? along <= minL + band : along >= maxL - band)
                endList.Add(i);
            if (Mathf.Abs(along - innerAlong) <= innerBand)
            {
                innerSum += verts[i];
                nInner++;
                innerList.Add(i);
            }
        }

        if (nInner > 0)
        {
            Vector3 outward = tangOrigin - innerSum / nInner;
            tangDirection = outward.sqrMagnitude > 1e-8f
                ? outward.normalized
                : (tangAtMin ? -localLong : localLong);
        }
        else
            tangDirection = tangAtMin ? -localLong : localLong;

        restVerts = verts;
        endIdx = endList.Count > 0 ? endList.ToArray() : null;
        innerIdx = innerList.Count > 0 ? innerList.ToArray() : null;
        if (endIdx != null)
        {
            liveEndLocal = new Vector3[endIdx.Length];
            for (int i = 0; i < endIdx.Length; i++)
                liveEndLocal[i] = verts[endIdx[i]];
        }
        restMinL = minL;
        restMaxL = maxL;
        restTangAtMin = tangAtMin;
        restLocalLong = localLong;
        restLocalThin = localThin;
        restTangDirection = tangDirection;
        restWx = wx;
        restWy = wy;
        restWz = wz;

        tangUp = localThin;
        StoreWorldTang(wx, wy, wz, localLong);

        tangCalculated = true;
    }

    private void FallbackTangPosition(Bounds bounds)
    {
        tangOrigin = bounds.center - Vector3.forward * bounds.extents.z * 0.5f;
        tangDirection = -Vector3.forward;
        tangUp = Vector3.up;
        StoreWorldTang(
            transform.TransformVector(new Vector3(bounds.size.x, 0f, 0f)).magnitude,
            transform.TransformVector(new Vector3(0f, bounds.size.y, 0f)).magnitude,
            transform.TransformVector(new Vector3(0f, 0f, bounds.size.z)).magnitude,
            Vector3.forward);
        tangCalculated = true;
    }

    private void StoreWorldTang(float wx, float wy, float wz, Vector3 localLong)
    {
        worldOut = transform.TransformVector(localLong);
        if (worldOut.sqrMagnitude < 1e-12f)
            worldOut = Vector3.forward;
        worldOut.Normalize();

        SkinnedMeshRenderer bodySkin = GetComponent<SkinnedMeshRenderer>();
        Vector3 bodyCenter = bodySkin != null ? bodySkin.bounds.center : transform.position;
        Vector3 probe = transform.TransformPoint(tangOrigin);
        if (Vector3.Dot(probe - bodyCenter, worldOut) < 0f)
            worldOut = -worldOut;

        worldTang = probe;
        float best = Vector3.Dot(probe, worldOut);
        if (liveEndLocal != null)
        {
            for (int i = 0; i < liveEndLocal.Length; i++)
            {
                Vector3 w = transform.TransformPoint(liveEndLocal[i]);
                float d = Vector3.Dot(w, worldOut);
                if (d > best)
                {
                    best = d;
                    worldTang = w;
                }
            }
        }
        else if (restVerts != null && endIdx != null)
        {
            for (int i = 0; i < endIdx.Length; i++)
            {
                Vector3 w = transform.TransformPoint(restVerts[endIdx[i]]);
                float d = Vector3.Dot(w, worldOut);
                if (d > best)
                {
                    best = d;
                    worldTang = w;
                }
            }
        }

        Vector3 thinSrc = tangUp.sqrMagnitude > 0.01f ? tangUp : restLocalThin;
        worldThin = transform.TransformVector(thinSrc);
        if (worldThin.sqrMagnitude < 1e-12f)
            worldThin = Vector3.up;
        worldThin.Normalize();
        worldAcross = Vector3.Cross(worldThin, worldOut).normalized;
        if (worldAcross.sqrMagnitude < 0.0001f)
            worldAcross = Vector3.Cross(worldOut, Vector3.up).normalized;
        worldThin = Vector3.Cross(worldOut, worldAcross).normalized;
        if (localLong == Vector3.right)
            bladeLengthWorld = wx;
        else if (localLong == Vector3.up)
            bladeLengthWorld = wy;
        else
            bladeLengthWorld = wz;
        bladeWidthWorld = Mathf.Max(
            localLong == Vector3.right ? wy : wx,
            localLong == Vector3.forward ? wy : wz);
        if (bladeLengthWorld < 0.01f)
            bladeLengthWorld = 1f;
        if (bladeWidthWorld < 0.01f)
            bladeWidthWorld = 0.2f;
        if (restBladeWidthWorld < 0.01f)
            restBladeWidthWorld = bladeWidthWorld;
        SkinnedMeshRenderer widthSkin = GetComponent<SkinnedMeshRenderer>();
        float widthW = widthSkin != null ? ReadFollowWeight(widthSkin, "Weight") : 0f;
        if (widthW < 0.01f && widthSkin != null)
            widthW = ReadFollowWeight(widthSkin, "Width");
        bladeWidthWorld = restBladeWidthWorld * Mathf.Lerp(1f, 1.45f, Mathf.Clamp01(widthW / 100f));
    }

    private float ActiveHandleAmount()
    {
        return ActiveHandleIs2() ? handle2Amount : handle1Amount;
    }

    private bool ActiveHandleIs2()
    {
        if (handle2Amount > handle1Amount + 0.5f)
            return true;
        if (handle1Amount > handle2Amount + 0.5f)
            return false;
        return useHandle2;
    }

    private void EnsureHandleAxes()
    {
        if (handleRoot != null)
        {
            worldOut = handleRoot.up;
            worldAcross = handleRoot.right;
            worldThin = handleRoot.forward;
        }
        else if (worldOut.sqrMagnitude < 0.01f)
        {
            worldOut = Vector3.up;
            worldAcross = Vector3.right;
            worldThin = Vector3.forward;
        }

        if (bladeWidthWorld < 0.01f)
            bladeWidthWorld = 0.2f;
        tangCalculated = true;
    }

    private void AddHandle1(List<Vector3> verts, List<int> wood, List<int> gold, float t)
    {
        float u = Mathf.Max(t, 0.01f);
        Vector3 along = Vector3.up;
        Vector3 side = Vector3.right;
        Vector3 nrm = Vector3.forward;
        Vector3 surface = Vector3.zero;
        float embed = u * 0.22f;

        float tubeR = u * 0.22f;
        float barW = u * 0.50f;
        float barH = u * 0.18f;
        float barD = u * 0.20f;
        float stem = u * 1.05f;
        float hookR = u * 0.52f;

        AddWorldBox(verts, wood, surface - along * (embed * 0.45f), side, nrm, along, barW * 0.42f, barD * 0.85f, embed * 0.5f);
        Vector3 tCenter = surface + along * barH;
        AddWorldBox(verts, wood, tCenter, side, nrm, along, barW * 0.88f, barD, barH);
        Vector3 beakC = tCenter + side * (barW * 0.78f) + along * (barH * 0.55f);
        AddWorldBox(verts, wood, beakC, side, nrm, along, barW * 0.32f, barD * 0.78f, barH * 0.70f);
        AddDiamond(verts, gold, tCenter, side, nrm, along, barW * 0.62f, barD * 0.52f, barH * 0.14f);

        const int sides = 12;
        const int straightN = 7;
        const int arcN = 10;
        const int tailN = 3;
        int pathN = straightN + arcN + tailN;
        var path = new Vector3[pathN];
        Vector3 stem0 = surface + along * (barH * 1.15f);
        for (int i = 0; i < straightN; i++)
            path[i] = stem0 + along * (stem * i / (straightN - 1));

        Vector3 stemEnd = path[straightN - 1];
        Vector3 arcC = stemEnd - side * hookR;
        for (int i = 1; i <= arcN; i++)
        {
            float a = (i / (float)arcN) * (Mathf.PI * 0.5f);
            path[straightN - 1 + i] = arcC + side * (hookR * Mathf.Cos(a)) + along * (hookR * Mathf.Sin(a));
        }

        Vector3 hookEnd = path[straightN + arcN - 1];
        Vector3 tailDir = -side;
        for (int i = 1; i <= tailN; i++)
            path[straightN + arcN - 1 + i] = hookEnd + tailDir * (tubeR * 0.55f * i);

        AddSmoothTube(verts, wood, path, tubeR, sides);
        AddDisc(verts, wood, path[pathN - 1], tailDir, nrm, tubeR, sides);
    }

    private void AddHandle2(List<Vector3> verts, List<int> wood, List<int> gold, float t)
    {
        float u = Mathf.Max(t, 0.01f);
        Vector3 along = Vector3.up;
        Vector3 side = Vector3.right;
        Vector3 nrm = Vector3.forward;
        Vector3 tang = Vector3.zero;
        float z = u * 0.26f;

        Vector3 P(float x, float y, float d)
        {
            return tang + side * (x * u) + along * (y * u) + nrm * d;
        }

        var roof = new Vector2[]
        {
            new Vector2(-1.10f, -0.22f),
            new Vector2(-0.38f, -0.02f),
            new Vector2( 0.38f, -0.02f),
            new Vector2( 1.10f, -0.22f),
            new Vector2( 0.82f,  0.10f),
            new Vector2( 0.34f,  0.22f),
            new Vector2(-0.34f,  0.22f),
            new Vector2(-0.82f,  0.10f)
        };

        int n = roof.Length;
        int front = verts.Count;
        for (int i = 0; i < n; i++)
            verts.Add(ToLocal(P(roof[i].x, roof[i].y, z)));
        int back = verts.Count;
        for (int i = 0; i < n; i++)
            verts.Add(ToLocal(P(roof[i].x, roof[i].y, -z)));
        for (int i = 0; i < n; i++)
        {
            int j = (i + 1) % n;
            AddQuad(wood, front + i, front + j, back + j, back + i);
        }
        int fC = AddWorldVert(verts, P(0f, 0.04f, z));
        int bC = AddWorldVert(verts, P(0f, 0.04f, -z));
        for (int i = 0; i < n; i++)
        {
            int j = (i + 1) % n;
            AddTri(wood, fC, front + j, front + i);
            AddTri(wood, bC, back + i, back + j);
        }

        float gy = -0.03f;
        float gt = 0.03f;
        AddWorldBox(
            verts,
            gold,
            P(0f, gy, 0f),
            side,
            nrm,
            along,
            u * 0.36f,
            u * 0.22f,
            u * gt);

        float sw = u * 0.30f;
        float sh = u * 1.15f;
        float y0 = u * 0.22f;
        AddWorldBox(verts, wood, tang + along * (y0 + sh * 0.05f), side, nrm, along, sw * 1.08f, sw * 1.08f, sh * 0.05f);
        AddWorldBox(verts, wood, tang + along * (y0 + sh * 0.36f), side, nrm, along, sw, sw, sh * 0.26f);
        AddWorldBox(verts, wood, tang + along * (y0 + sh * 0.62f), side, nrm, along, sw * 0.90f, sw * 0.90f, sh * 0.08f);
        AddWorldBox(verts, wood, tang + along * (y0 + sh * 0.86f), side, nrm, along, sw * 1.22f, sw * 1.22f, sh * 0.12f);
    }

    private void AddSmoothTube(List<Vector3> verts, List<int> tris, Vector3[] path, float radius, int sides)
    {
        if (path == null || path.Length < 2)
            return;
        int rings = path.Length;
        int start = verts.Count;
        for (int i = 0; i < rings; i++)
        {
            Vector3 f = i < rings - 1 ? path[i + 1] - path[i] : path[i] - path[i - 1];
            if (f.sqrMagnitude < 1e-8f)
                f = worldOut;
            f.Normalize();
            Vector3 r = Vector3.Cross(f, worldThin);
            if (r.sqrMagnitude < 1e-6f)
                r = Vector3.Cross(f, worldAcross);
            r.Normalize();
            Vector3 u = Vector3.Cross(r, f).normalized;
            for (int s = 0; s < sides; s++)
            {
                float a = s * Mathf.PI * 2f / sides;
                verts.Add(ToLocal(path[i] + (r * Mathf.Cos(a) + u * Mathf.Sin(a)) * radius));
            }
        }

        for (int i = 0; i < rings - 1; i++)
        {
            for (int s = 0; s < sides; s++)
            {
                int s1 = (s + 1) % sides;
                AddQuad(
                    tris,
                    start + i * sides + s,
                    start + i * sides + s1,
                    start + (i + 1) * sides + s1,
                    start + (i + 1) * sides + s);
            }
        }
    }

    private void AddDisc(List<Vector3> verts, List<int> tris, Vector3 center, Vector3 normal, Vector3 hint, float radius, int sides)
    {
        Vector3 n = normal.sqrMagnitude > 1e-8f ? normal.normalized : worldOut;
        Vector3 r = Vector3.Cross(n, hint);
        if (r.sqrMagnitude < 1e-6f)
            r = Vector3.Cross(n, worldAcross);
        r.Normalize();
        Vector3 u = Vector3.Cross(r, n).normalized;
        int c = AddWorldVert(verts, center);
        int first = verts.Count;
        for (int s = 0; s < sides; s++)
        {
            float a = s * Mathf.PI * 2f / sides;
            verts.Add(ToLocal(center + (r * Mathf.Cos(a) + u * Mathf.Sin(a)) * radius));
        }
        for (int s = 0; s < sides; s++)
            AddTri(tris, c, first + s, first + (s + 1) % sides);
    }

    private void AddDiamond(
        List<Vector3> verts,
        List<int> tris,
        Vector3 center,
        Vector3 axisX,
        Vector3 axisY,
        Vector3 axisZ,
        float hx,
        float hy,
        float hz)
    {
        Vector3 x = axisX.normalized * hx;
        Vector3 y = axisY.normalized * hy;
        Vector3 z = axisZ.normalized * hz;
        int t0 = AddWorldVert(verts, center - z + x);
        int t1 = AddWorldVert(verts, center - z + y);
        int t2 = AddWorldVert(verts, center - z - x);
        int t3 = AddWorldVert(verts, center - z - y);
        int b0 = AddWorldVert(verts, center + z + x);
        int b1 = AddWorldVert(verts, center + z + y);
        int b2 = AddWorldVert(verts, center + z - x);
        int b3 = AddWorldVert(verts, center + z - y);
        AddQuad(tris, t0, t1, t2, t3);
        AddQuad(tris, b0, b3, b2, b1);
        AddQuad(tris, t0, b0, b1, t1);
        AddQuad(tris, t1, b1, b2, t2);
        AddQuad(tris, t2, b2, b3, t3);
        AddQuad(tris, t3, b3, b0, t0);
    }

    private int AddWorldVert(List<Vector3> verts, Vector3 world)
    {
        verts.Add(ToLocal(world));
        return verts.Count - 1;
    }

    private static void AddTri(List<int> tris, int a, int b, int c)
    {
        tris.Add(a);
        tris.Add(b);
        tris.Add(c);
    }

    private void AddWorldBox(
        List<Vector3> verts,
        List<int> tris,
        Vector3 center,
        Vector3 axisX,
        Vector3 axisY,
        Vector3 axisZ,
        float hx,
        float hy,
        float hz)
    {
        Vector3 x = axisX.normalized * hx;
        Vector3 y = axisY.normalized * hy;
        Vector3 z = axisZ.normalized * hz;
        int b = verts.Count;
        verts.Add(ToLocal(center - x - y - z));
        verts.Add(ToLocal(center + x - y - z));
        verts.Add(ToLocal(center + x + y - z));
        verts.Add(ToLocal(center - x + y - z));
        verts.Add(ToLocal(center - x - y + z));
        verts.Add(ToLocal(center + x - y + z));
        verts.Add(ToLocal(center + x + y + z));
        verts.Add(ToLocal(center - x + y + z));
        AddQuad(tris, b + 0, b + 1, b + 2, b + 3);
        AddQuad(tris, b + 5, b + 4, b + 7, b + 6);
        AddQuad(tris, b + 4, b + 0, b + 3, b + 7);
        AddQuad(tris, b + 1, b + 5, b + 6, b + 2);
        AddQuad(tris, b + 4, b + 5, b + 1, b + 0);
        AddQuad(tris, b + 3, b + 2, b + 6, b + 7);
    }

    private static void AddQuad(List<int> tris, int a, int b, int c, int d)
    {
        tris.Add(a); tris.Add(b); tris.Add(c);
        tris.Add(a); tris.Add(c); tris.Add(d);
    }

    private Vector3 ToLocal(Vector3 local)
    {
        return local;
    }

    private const string NeutralMeshSuffix = "_NoOldHandles";
    private const string OldHandleShape1 = "Handle 1";
    private const string OldHandleShape2 = "Handle 2";

    private void HideCodedChild()
    {
        Transform child = handleRoot != null ? handleRoot : transform.Find(ChildName);
        if (child != null)
            child.gameObject.SetActive(false);
        if (handleRenderer != null)
            handleRenderer.enabled = false;
    }

    private void NeutralizeOldHandleShapes()
    {
        SkinnedMeshRenderer skin = GetComponent<SkinnedMeshRenderer>();
        if (skin == null || skin.sharedMesh == null)
            return;

        Mesh source = skin.sharedMesh;
        if (source.name.IndexOf(NeutralMeshSuffix, System.StringComparison.Ordinal) >= 0)
            return;

        int handle1 = FindShape(source, OldHandleShape1);
        int handle2 = FindShape(source, OldHandleShape2);
        if (handle1 < 0 && handle2 < 0)
            return;

        int vCount = source.vertexCount;
        var dv = new Vector3[vCount];
        var dn = new Vector3[vCount];
        var dt = new Vector3[vCount];

        Mesh copy = Instantiate(source);
        copy.name = source.name.Replace(NeutralMeshSuffix, "") + NeutralMeshSuffix;
        copy.ClearBlendShapes();

        var weights = new float[source.blendShapeCount];
        for (int i = 0; i < source.blendShapeCount; i++)
            weights[i] = skin.GetBlendShapeWeight(i);

        for (int s = 0; s < source.blendShapeCount; s++)
        {
            string name = source.GetBlendShapeName(s);
            int frames = source.GetBlendShapeFrameCount(s);
            if (frames <= 0)
                continue;
            for (int f = 0; f < frames; f++)
            {
                source.GetBlendShapeFrameVertices(s, f, dv, dn, dt);
                float frameWeight = source.GetBlendShapeFrameWeight(s, f);
                if (s == handle1 || s == handle2)
                {
                    System.Array.Clear(dv, 0, dv.Length);
                    System.Array.Clear(dn, 0, dn.Length);
                    System.Array.Clear(dt, 0, dt.Length);
                }

                copy.AddBlendShapeFrame(name, frameWeight, dv, dn, dt);
            }
        }

        skin.sharedMesh = copy;
        for (int i = 0; i < copy.blendShapeCount; i++)
        {
            string name = copy.GetBlendShapeName(i);
            float weight = i < weights.Length ? weights[i] : 0f;
            if (string.Equals(name, OldHandleShape1, System.StringComparison.OrdinalIgnoreCase))
                weight = handle1Amount;
            if (string.Equals(name, OldHandleShape2, System.StringComparison.OrdinalIgnoreCase))
                weight = handle2Amount;
            skin.SetBlendShapeWeight(i, weight);
        }

        Debug.Log("MORPHIC: Cleared old Handle 1/2 mesh deltas. Coded handles now own those sliders.", this);
    }

    private void PullControlSliders()
    {
        float weight1, weight2;
        if (!TryReadHandleControls(out weight1, out weight2))
            return;
        if (Mathf.Abs(weight1 - lastPushedControl1) < 0.2f &&
            Mathf.Abs(weight2 - lastPushedControl2) < 0.2f)
            return;
        handle1Amount = Mathf.Clamp(weight1, 0f, HandleMaxAmount);
        handle2Amount = Mathf.Clamp(weight2, 0f, HandleMaxAmount);
        lastPushedControl1 = handle1Amount;
        lastPushedControl2 = handle2Amount;
        if (handle2Amount > handle1Amount + 0.5f)
            useHandle2 = true;
        else if (handle1Amount > handle2Amount + 0.5f)
            useHandle2 = false;
        if (weight1 > HandleMaxAmount || weight2 > HandleMaxAmount)
            PushControlSliders(handle1Amount, handle2Amount);
    }

    private void PushControlSliders(float amount1, float amount2)
    {
        lastPushedControl1 = amount1;
        lastPushedControl2 = amount2;
        int index1, index2;
        SkinnedMeshRenderer skin = GetComponent<SkinnedMeshRenderer>();
        if (skin == null || skin.sharedMesh == null)
            return;
        if (TryFindHandleControl(skin.sharedMesh, OldHandleShape1, out index1))
            skin.SetBlendShapeWeight(index1, Mathf.Clamp01(amount1 / HandleMaxAmount) * 100f);
        if (TryFindHandleControl(skin.sharedMesh, OldHandleShape2, out index2))
            skin.SetBlendShapeWeight(index2, Mathf.Clamp01(amount2 / HandleMaxAmount) * 100f);
    }

    private bool TryReadHandleControls(out float weight1, out float weight2)
    {
        weight1 = 0f;
        weight2 = 0f;
        SkinnedMeshRenderer skin = GetComponent<SkinnedMeshRenderer>();
        if (skin == null || skin.sharedMesh == null)
            return false;

        int index1, index2;
        bool found1 = TryFindHandleControl(skin.sharedMesh, OldHandleShape1, out index1);
        bool found2 = TryFindHandleControl(skin.sharedMesh, OldHandleShape2, out index2);
        if (found1)
            weight1 = skin.GetBlendShapeWeight(index1) * HandleMaxAmount / 100f;
        if (found2)
            weight2 = skin.GetBlendShapeWeight(index2) * HandleMaxAmount / 100f;
        return found1 || found2;
    }

    private static bool TryFindHandleControl(Mesh mesh, string shapeName, out int index)
    {
        index = FindShape(mesh, shapeName);
        return index >= 0;
    }

    private static int FindShape(Mesh mesh, string name)
    {
        for (int i = 0; i < mesh.blendShapeCount; i++)
        {
            if (string.Equals(mesh.GetBlendShapeName(i), name, System.StringComparison.OrdinalIgnoreCase))
                return i;
        }

        return -1;
    }

    private static Material MakeLitMaterial(string matName, Color color, float metallic, float smoothness)
    {
        Shader shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null)
            shader = Shader.Find("Universal Render Pipeline/Unlit");
        if (shader == null)
            shader = Shader.Find("Sprites/Default");
        Material mat = new Material(shader);
        mat.name = matName;
        if (mat.HasProperty("_BaseColor"))
            mat.SetColor("_BaseColor", color);
        if (mat.HasProperty("_Color"))
            mat.SetColor("_Color", color);
        if (mat.HasProperty("_Smoothness"))
            mat.SetFloat("_Smoothness", smoothness);
        if (mat.HasProperty("_Metallic"))
            mat.SetFloat("_Metallic", metallic);
        if (mat.HasProperty("_Cull"))
            mat.SetFloat("_Cull", 0f);
        if (mat.HasProperty("_CullMode"))
            mat.SetFloat("_CullMode", 0f);
        mat.doubleSidedGI = true;
        return mat;
    }

    private void Warn(string message)
    {
        warnTimer -= Time.unscaledDeltaTime;
        if (warnTimer > 0f)
            return;
        warnTimer = 2f;
        Debug.LogWarning("MORPHIC: " + message, this);
    }
}
