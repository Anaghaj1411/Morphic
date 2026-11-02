using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// Procedural Keris flowers. No shape keys, no FBX flower mesh.
/// Flowers are designed in code and grown out of the blade.
/// </summary>
[ExecuteAlways]
[DefaultExecutionOrder(400)]
[DisallowMultipleComponent]
public sealed class KerisFlowerOverlay : MonoBehaviour
{
    private const string ChildName = "KerisFlowers";

    [Header("Coded flowers")]
    [Range(0f, 100f)]
    public float flowerAmount;

    [SerializeField] private int flowerCount = 7;
    [SerializeField] private float flowerRadiusMeters = 0.016f;
    [SerializeField] private float popMeters = 0.02f;
    [SerializeField] private float surfaceClearance = 0.016f;
    [SerializeField] private bool flipPopDirection;

    private Transform flowerRoot;
    private MeshFilter flowerFilter;
    private MeshRenderer flowerRenderer;
    private Mesh flowerMesh;
    private Stamp[] stamps;
    private bool built;
    private float lastAmount = float.NaN;
    private float lastPushedControl = float.NaN;
    private float warnTimer;

    private struct Stamp
    {
        public Vector3 origin;
        public Vector3 u;
        public Vector3 v;
        public Vector3 n;
        public float size;
    }

    public static KerisFlowerOverlay Ensure(SkinnedMeshRenderer skin)
    {
        if (skin == null)
            return null;
        return EnsureOn(skin.gameObject);
    }

    public static KerisFlowerOverlay EnsureOn(GameObject host)
    {
        if (host == null)
            return null;

        KerisFlowerOverlay overlay = host.GetComponent<KerisFlowerOverlay>();
        if (overlay == null)
            overlay = host.AddComponent<KerisFlowerOverlay>();
        if (overlay.built && overlay.flowerRoot != null)
            return overlay;
        overlay.NeutralizeOldFlowerShape();
        overlay.Build();
        overlay.SetAmount(overlay.flowerAmount);
        return overlay;
    }

    public void SetAmount(float amount)
    {
        flowerAmount = Mathf.Clamp(amount, 0f, 100f);
        PushControlSlider(flowerAmount);
        ApplyAmount(true);
    }

    [ContextMenu("Rebuild Flowers Now")]
    public void RebuildNow()
    {
        built = false;
        NeutralizeOldFlowerShape();
        Build();
        ApplyAmount(true);
    }

    private void OnEnable()
    {
        built = false;
        NeutralizeOldFlowerShape();
        Build();
    }

    private void OnValidate()
    {
        flowerCount = Mathf.Clamp(flowerCount, 3, 12);
        flowerRadiusMeters = Mathf.Clamp(flowerRadiusMeters, 0.006f, 0.04f);
        popMeters = Mathf.Clamp(popMeters, 0.002f, 0.05f);
        surfaceClearance = Mathf.Clamp(surfaceClearance, 0.004f, 0.04f);
        if (built)
        {
            PushControlSlider(flowerAmount);
            ApplyAmount(true);
        }
    }

    private void LateUpdate()
    {
        if (!built)
            Build();
        if (!Application.isPlaying)
            PullControlSlider();
        if (flowerRenderer != null)
            flowerRenderer.enabled = flowerAmount > 0.5f;
        ApplyAmount(false);
    }

    private void ApplyAmount(bool force)
    {
        if (!built || flowerMesh == null || stamps == null || stamps.Length == 0)
            return;
        if (!force && Mathf.Abs(flowerAmount - lastAmount) < 0.05f)
            return;

        lastAmount = flowerAmount;
        float t = Mathf.Clamp01(flowerAmount / 100f);
        t = t * t * (3f - 2f * t);

        if (t <= 0.001f)
        {
            flowerMesh.Clear();
            if (flowerFilter != null)
                flowerFilter.sharedMesh = flowerMesh;
            if (flowerRenderer != null)
                flowerRenderer.enabled = false;
            if (flowerRoot != null)
                flowerRoot.gameObject.SetActive(false);
            return;
        }

        var verts = new List<Vector3>(stamps.Length * 180);
        var tris = new List<int>(stamps.Length * 360);
        for (int i = 0; i < stamps.Length; i++)
            AddFlower(verts, tris, stamps[i], t);

        flowerMesh.Clear();
        flowerMesh.SetVertices(verts);
        flowerMesh.SetTriangles(tris, 0);
        flowerMesh.RecalculateNormals();
        flowerMesh.RecalculateBounds();
        if (flowerFilter != null)
            flowerFilter.sharedMesh = flowerMesh;
        if (flowerRenderer != null)
            flowerRenderer.enabled = true;
        if (flowerRoot != null)
        {
            flowerRoot.localPosition = Vector3.zero;
            flowerRoot.localRotation = Quaternion.identity;
            flowerRoot.localScale = Vector3.one;
            flowerRoot.gameObject.SetActive(true);
        }
    }

    private void Build()
    {
        Mesh body = GetBodyMesh();
        if (body == null)
        {
            Warn("Keris has no readable mesh yet.");
            return;
        }

        stamps = SampleBlade(body);
        if (stamps == null || stamps.Length == 0)
        {
            Warn("Could not find Keris blade surface for flowers.");
            return;
        }

        Transform child = transform.Find(ChildName);
        if (child == null)
        {
            GameObject go = new GameObject(ChildName);
            child = go.transform;
            child.SetParent(transform, false);
        }

        child.localPosition = Vector3.zero;
        child.localRotation = Quaternion.identity;
        child.localScale = Vector3.one;

        flowerFilter = child.GetComponent<MeshFilter>();
        if (flowerFilter == null)
            flowerFilter = child.gameObject.AddComponent<MeshFilter>();
        flowerRenderer = child.GetComponent<MeshRenderer>();
        if (flowerRenderer == null)
            flowerRenderer = child.gameObject.AddComponent<MeshRenderer>();

        if (flowerMesh != null)
        {
            if (Application.isPlaying)
                Destroy(flowerMesh);
            else
                DestroyImmediate(flowerMesh);
        }

        flowerMesh = new Mesh { name = "KerisFlowersCoded" };
        flowerMesh.MarkDynamic();
        flowerFilter.sharedMesh = flowerMesh;
        flowerRenderer.sharedMaterial = MakeFlowerMaterial();
        flowerRenderer.enabled = true;
        flowerRenderer.shadowCastingMode = ShadowCastingMode.Off;
        flowerRenderer.receiveShadows = false;
        flowerRenderer.lightProbeUsage = LightProbeUsage.Off;
        flowerRenderer.reflectionProbeUsage = ReflectionProbeUsage.Off;

        flowerRoot = child;
        built = true;
        lastAmount = float.NaN;
        ApplyAmount(true);
        Debug.Log("MORPHIC: Coded " + stamps.Length + " Keris flowers on the blade.", this);
    }

    private Mesh GetBodyMesh()
    {
        SkinnedMeshRenderer skin = GetComponent<SkinnedMeshRenderer>();
        if (skin != null && skin.sharedMesh != null)
            return skin.sharedMesh;
        MeshFilter filter = GetComponent<MeshFilter>();
        return filter != null ? filter.sharedMesh : null;
    }

    private Stamp[] SampleBlade(Mesh body)
    {
        Vector3[] verts = body.vertices;
        if (verts == null || verts.Length < 8)
            return null;

        var indices = new List<int>(body.vertexCount * 3);
        for (int sm = 0; sm < Mathf.Max(1, body.subMeshCount); sm++)
            indices.AddRange(body.GetTriangles(sm));
        if (indices.Count < 9)
            return null;

        Vector3 localLong;
        Vector3 localThin;
        ChooseAxes(body.bounds, out localLong, out localThin);

        Vector3 worldThin = transform.TransformDirection(localThin).normalized;
        Vector3 worldLong = transform.TransformDirection(localLong).normalized;
        Vector3 view = ViewDirection(body.bounds.center);
        if (Vector3.Dot(worldThin, view) < 0f)
            worldThin = -worldThin;
        if (flipPopDirection)
            worldThin = -worldThin;

        var faces = new List<Face>(indices.Count / 9);
        Vector3 faceCentroid = Vector3.zero;
        for (int i = 0; i < indices.Count; i += 3)
        {
            Vector3 a = transform.TransformPoint(verts[indices[i]]);
            Vector3 b = transform.TransformPoint(verts[indices[i + 1]]);
            Vector3 c = transform.TransformPoint(verts[indices[i + 2]]);
            Vector3 n = Vector3.Cross(b - a, c - a);
            float area = n.magnitude * 0.5f;
            if (area < 0.0000001f)
                continue;
            n /= area * 2f;
            if (Vector3.Dot(n, worldThin) < 0.35f)
                continue;
            Vector3 center = (a + b + c) / 3f;
            faces.Add(new Face { center = center, normal = n, area = area });
            faceCentroid += center;
        }

        if (faces.Count < 3)
            return FallbackStamps(body.bounds, localLong, localThin);

        faceCentroid /= faces.Count;
        Vector3 worldAcross = Vector3.Cross(worldThin, worldLong).normalized;
        if (worldAcross.sqrMagnitude < 0.0001f)
            worldAcross = transform.up;

        float alongMin = float.MaxValue;
        float alongMax = float.MinValue;
        float acrossMin = float.MaxValue;
        float acrossMax = float.MinValue;
        for (int i = 0; i < indices.Count; i += 3)
        {
            Vector3 a = transform.TransformPoint(verts[indices[i]]);
            Vector3 n = Vector3.Cross(
                transform.TransformPoint(verts[indices[i + 1]]) - a,
                transform.TransformPoint(verts[indices[i + 2]]) - a);
            if (n.sqrMagnitude < 0.0000001f)
                continue;
            n.Normalize();
            if (Vector3.Dot(n, worldThin) < 0.35f)
                continue;
            for (int k = 0; k < 3; k++)
            {
                Vector3 p = transform.TransformPoint(verts[indices[i + k]]);
                float along = Vector3.Dot(p, worldLong);
                float across = Vector3.Dot(p, worldAcross);
                if (along < alongMin) alongMin = along;
                if (along > alongMax) alongMax = along;
                if (across < acrossMin) acrossMin = across;
                if (across > acrossMax) acrossMax = across;
            }
        }

        int count = Mathf.Clamp(flowerCount, 3, 12);
        float width = Mathf.Max(0.001f, acrossMax - acrossMin);
        float length = Mathf.Max(0.001f, alongMax - alongMin);
        float radius = flowerRadiusMeters;
        radius = Mathf.Min(radius, width * 0.24f, length / (count * 2.35f));
        radius = Mathf.Max(radius, 0.004f);
        float insetAlong = radius * 1.12f;
        if (alongMax - alongMin < insetAlong * 2.2f)
            insetAlong = (alongMax - alongMin) * 0.22f;

        float alongStart = alongMin + insetAlong;
        float alongEnd = alongMax - insetAlong;
        float acrossMid = 0.5f * (acrossMin + acrossMax);

        Vector3 localU = transform.InverseTransformVector(worldAcross);
        Vector3 localV = transform.InverseTransformVector(worldLong);
        Vector3 localN = transform.InverseTransformVector(worldThin);

        float[] stagger = { -1f, 0.62f, -0.38f, 0.92f, -0.74f, 0.28f, -0.86f, 0.5f };
        var result = new Stamp[count];
        for (int i = 0; i < count; i++)
        {
            float u = count == 1 ? 0.5f : i / (float)(count - 1);
            float along = Mathf.Lerp(alongStart, alongEnd, u);
            float thisRadius = radius * (i % 2 == 0 ? 1f : 0.88f);
            float insetAcross = thisRadius * 1.12f;
            float acrossMinSafe = acrossMin + insetAcross;
            float acrossMaxSafe = acrossMax - insetAcross;
            float maxOffset = Mathf.Max(0f, Mathf.Min(acrossMid - acrossMinSafe, acrossMaxSafe - acrossMid));
            float across = Mathf.Clamp(acrossMid + stagger[i % stagger.Length] * maxOffset, acrossMinSafe, acrossMaxSafe);
            Vector3 worldPos = faceCentroid
                + worldLong * (along - Vector3.Dot(faceCentroid, worldLong))
                + worldAcross * (across - Vector3.Dot(faceCentroid, worldAcross));
            result[i] = new Stamp
            {
                origin = transform.InverseTransformPoint(worldPos),
                u = localU,
                v = localV,
                n = localN,
                size = thisRadius
            };
        }

        return result;
    }

    private Stamp[] FallbackStamps(Bounds bounds, Vector3 localLong, Vector3 localThin)
    {
        int count = Mathf.Clamp(flowerCount, 3, 12);
        var result = new Stamp[count];
        Vector3 localAcross = Vector3.Cross(localThin, localLong);
        if (localAcross.sqrMagnitude < 0.0001f)
            localAcross = Vector3.up;
        localAcross.Normalize();
        Vector3 worldN = transform.TransformDirection(localThin).normalized;
        Vector3 view = ViewDirection(bounds.center);
        if (Vector3.Dot(worldN, view) < 0f)
            localThin = -localThin;
        if (flipPopDirection)
            localThin = -localThin;

        Vector3 worldRight = transform.TransformDirection(localAcross).normalized;
        Vector3 worldAlong = transform.TransformDirection(localLong).normalized;
        Vector3 worldOut = transform.TransformDirection(localThin).normalized;
        Vector3 right = transform.InverseTransformVector(worldRight);
        Vector3 along = transform.InverseTransformVector(worldAlong);
        Vector3 outward = transform.InverseTransformVector(worldOut);

        for (int i = 0; i < count; i++)
        {
            float u = count == 1 ? 0.5f : i / (float)(count - 1);
            float t = Mathf.Lerp(0.18f, 0.88f, u);
            Vector3 origin = bounds.center
                + localLong * (t - 0.5f) * bounds.size.magnitude * 0.55f
                + localThin * (bounds.extents.x + bounds.extents.y + bounds.extents.z) * 0.08f;
            result[i] = new Stamp
            {
                origin = origin,
                u = right,
                v = along,
                n = outward,
                size = flowerRadiusMeters * (i % 2 == 0 ? 1f : 0.78f)
            };
        }

        return result;
    }

    private void ChooseAxes(Bounds bounds, out Vector3 localLong, out Vector3 localThin)
    {
        Vector3 size = bounds.size;
        float wx = transform.TransformVector(new Vector3(size.x, 0f, 0f)).magnitude;
        float wy = transform.TransformVector(new Vector3(0f, size.y, 0f)).magnitude;
        float wz = transform.TransformVector(new Vector3(0f, 0f, size.z)).magnitude;
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
    }

    private Vector3 ViewDirection(Vector3 localCenter)
    {
        Vector3 world = transform.TransformPoint(localCenter);
        Camera cam = Camera.main;
        if (cam == null)
        {
            Camera[] cams = FindObjectsByType<Camera>(FindObjectsSortMode.None);
            if (cams != null && cams.Length > 0)
                cam = cams[0];
        }

        if (cam != null)
            return (cam.transform.position - world).normalized;
        return transform.right;
    }

    private const string NeutralMeshSuffix = "_NoOldFlowers";
    private const string OldFlowerShape = "Carving 2.001";

    private void NeutralizeOldFlowerShape()
    {
        SkinnedMeshRenderer skin = GetComponent<SkinnedMeshRenderer>();
        if (skin == null || skin.sharedMesh == null)
            return;

        Mesh source = skin.sharedMesh;
        if (source.name.IndexOf(NeutralMeshSuffix, System.StringComparison.Ordinal) >= 0)
            return;

        int flower = FindShape(source, OldFlowerShape);
        if (flower < 0)
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
                if (s == flower)
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
            if (string.Equals(name, OldFlowerShape, System.StringComparison.OrdinalIgnoreCase))
                weight = flowerAmount;
            skin.SetBlendShapeWeight(i, weight);
        }

        Debug.Log("MORPHIC: Cleared old Carving 2.001 mesh deltas. Coded flowers now own that slider.", this);
    }

    private void PullControlSlider()
    {
        float weight;
        if (!TryReadFlowerControl(out weight))
            return;
        if (!float.IsNaN(lastPushedControl) && Mathf.Abs(weight - lastPushedControl) < 0.2f)
            return;
        if (Mathf.Abs(weight - flowerAmount) < 0.2f)
            return;
        flowerAmount = weight;
        lastPushedControl = weight;
    }

    private void PushControlSlider(float amount)
    {
        lastPushedControl = amount;
        int index;
        SkinnedMeshRenderer skin = GetComponent<SkinnedMeshRenderer>();
        if (skin == null || skin.sharedMesh == null || !TryFindFlowerControl(skin.sharedMesh, out index))
            return;
        skin.SetBlendShapeWeight(index, Mathf.Clamp(amount, 0f, 100f));
    }

    private bool TryReadFlowerControl(out float weight)
    {
        weight = 0f;
        SkinnedMeshRenderer skin = GetComponent<SkinnedMeshRenderer>();
        int index;
        if (skin == null || skin.sharedMesh == null || !TryFindFlowerControl(skin.sharedMesh, out index))
            return false;
        weight = skin.GetBlendShapeWeight(index);
        return true;
    }

    private static bool TryFindFlowerControl(Mesh mesh, out int index)
    {
        index = FindShape(mesh, OldFlowerShape);
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

    private void AddFlower(List<Vector3> verts, List<int> tris, Stamp stamp, float t)
    {
        if (t <= 0.001f)
            return;

        float radius = stamp.size * t;
        Vector3 origin = stamp.origin + stamp.n * (surfaceClearance + popMeters * t);
        AddQuatrefoil(verts, tris, origin, stamp, radius);
    }

    private static void AddQuatrefoil(
        List<Vector3> verts,
        List<int> tris,
        Vector3 origin,
        Stamp stamp,
        float radius)
    {
        const int slices = 48;
        const float thickness = 0.004f;
        int top = verts.Count;
        verts.Add(origin + stamp.n * thickness);
        for (int s = 0; s < slices; s++)
        {
            float ang = s * Mathf.PI * 2f / slices;
            float lobe = Mathf.Pow(Mathf.Abs(Mathf.Cos(2f * ang)), 0.72f);
            float rad = radius * Mathf.Lerp(0.42f, 1f, lobe);
            verts.Add(origin
                + stamp.u * (Mathf.Cos(ang) * rad)
                + stamp.v * (Mathf.Sin(ang) * rad)
                + stamp.n * thickness);
        }

        int bottom = verts.Count;
        verts.Add(origin);
        for (int s = 0; s < slices; s++)
            verts.Add(verts[top + 1 + s] - stamp.n * thickness);

        for (int s = 0; s < slices; s++)
        {
            int s1 = (s + 1) % slices;
            tris.Add(top);
            tris.Add(top + 1 + s);
            tris.Add(top + 1 + s1);
            tris.Add(bottom);
            tris.Add(bottom + 1 + s1);
            tris.Add(bottom + 1 + s);
            tris.Add(top + 1 + s);
            tris.Add(bottom + 1 + s);
            tris.Add(bottom + 1 + s1);
            tris.Add(top + 1 + s);
            tris.Add(bottom + 1 + s1);
            tris.Add(top + 1 + s1);
        }
    }

    private static Material MakeFlowerMaterial()
    {
        Shader shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null)
            shader = Shader.Find("Universal Render Pipeline/Unlit");
        if (shader == null)
            shader = Shader.Find("Sprites/Default");
        Material mat = new Material(shader);
        mat.name = "KerisFlowerCoded";
        Color petal = new Color(0.95f, 0.89f, 0.64f, 1f);
        if (mat.HasProperty("_BaseColor"))
            mat.SetColor("_BaseColor", petal);
        if (mat.HasProperty("_Color"))
            mat.SetColor("_Color", petal);
        if (mat.HasProperty("_Smoothness"))
            mat.SetFloat("_Smoothness", 0.18f);
        if (mat.HasProperty("_Metallic"))
            mat.SetFloat("_Metallic", 0f);
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

    private struct Face
    {
        public Vector3 center;
        public Vector3 normal;
        public float area;
    }
}
