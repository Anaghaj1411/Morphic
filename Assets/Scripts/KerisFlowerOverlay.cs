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
    private static readonly string[] RemovedFlowerNames = { "KerisFlower_2", "KerisFlower_8" };

    private struct FlowerPose
    {
        public readonly string name;
        public readonly Vector3 localPosition;
        public readonly Quaternion localRotation;
        public readonly Vector3 localScale;

        public FlowerPose(string name, Vector3 localPosition, Quaternion localRotation, Vector3 localScale)
        {
            this.name = name;
            this.localPosition = localPosition;
            this.localRotation = localRotation;
            this.localScale = localScale;
        }
    }

    private static readonly Quaternion FlowerRotation =
        new Quaternion(0.333325177f, 0.6584064f, 0.637675941f, 0.2208277f);
    private static readonly Vector3 FlowerScale = new Vector3(0.1f, 0.5f, 0f);
    private static readonly FlowerPose[] LockedFlowers =
    {
        new FlowerPose("KerisFlower_0", new Vector3(1.2f, 0.119f, 0.36f), FlowerRotation, FlowerScale),
        new FlowerPose("KerisFlower_1", new Vector3(1.4f, 0.492f, 0.05f), FlowerRotation, FlowerScale),
        new FlowerPose("KerisFlower_3", new Vector3(1.4f, 0.321f, -0.48f), FlowerRotation, FlowerScale),
        new FlowerPose("KerisFlower_4", new Vector3(1.1f, -0.3f, 0.23f), FlowerRotation, FlowerScale),
        new FlowerPose("KerisFlower_5", new Vector3(1.3f, -0.437f, -0.58f), FlowerRotation, FlowerScale),
        new FlowerPose("KerisFlower_6", new Vector3(1.4f, -0.08f, -0.58f), FlowerRotation, FlowerScale),
        new FlowerPose("KerisFlower_7", new Vector3(1.8f, 0.639f, -0.54f), FlowerRotation, FlowerScale),
    };

    [Header("Coded flowers")]
    [Range(0f, 100f)]
    public float flowerAmount;

    [SerializeField] private int flowerCount = 7;
    [SerializeField] private float flowerRadiusMeters = 0.035f;
    [SerializeField] private float popMeters = 0.0045f;
    [SerializeField] private float surfaceClearance = 0.0025f;
    [SerializeField] private bool flipPopDirection;

    [Header("Scene placement")]
    [SerializeField] private Transform[] flowerAnchors;

    private Transform flowerRoot;
    private MeshFilter flowerFilter;
    private MeshRenderer flowerRenderer;
    private Mesh flowerMesh;
    private Mesh flowerSharedMesh;
    private Material flowerMaterial;
    private Color flowerColor = new Color(0.42f, 0.22f, 0.08f, 1f);
    private Mesh bakeMesh;
    private Stamp[] stamps;
    private bool built;
    private float lastAmount = float.NaN;
    private float lastPushedControl = float.NaN;
    private float lastSampleWidth = float.NaN;
    private float lastSampleHeight = float.NaN;
    private float lastSampleCurve = float.NaN;
    private float lastSampleSharp = float.NaN;
    private float warnTimer;

    private struct Stamp
    {
        public Vector3 worldOrigin;
        public Vector3 worldU;
        public Vector3 worldV;
        public Vector3 worldN;
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

    public void SetColor(Color color)
    {
        flowerColor = color;
        if (flowerMaterial == null)
            flowerMaterial = MakeFlowerMaterial(flowerColor);
        ApplyColorToMaterial(flowerMaterial, flowerColor);
        if (flowerAnchors == null)
            return;
        for (int i = 0; i < flowerAnchors.Length; i++)
        {
            if (flowerAnchors[i] == null)
                continue;
            MeshRenderer meshRenderer = flowerAnchors[i].GetComponent<MeshRenderer>();
            if (meshRenderer != null)
                meshRenderer.sharedMaterial = flowerMaterial;
        }
    }

    [ContextMenu("Rebuild Flower Meshes")]
    public void RebuildNow()
    {
        built = false;
        NeutralizeOldFlowerShape();
        Build();
        ApplyAmount(true);
    }

    [ContextMenu("Refresh Flower List")]
    public void ReseedFlowerPositions()
    {
        EnsureFlowerAnchors();
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
        flowerRadiusMeters = Mathf.Clamp(flowerRadiusMeters, 0.01f, 0.08f);
        popMeters = Mathf.Clamp(popMeters, 0.0008f, 0.012f);
        surfaceClearance = Mathf.Clamp(surfaceClearance, 0.0008f, 0.01f);
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
        else if (!AnchorsReady())
            EnsureFlowerAnchors();
        if (!Application.isPlaying)
            PullControlSlider();
        ApplyAmount(true);
    }

    private bool AnchorsReady()
    {
        if (flowerRoot == null || flowerAnchors == null || flowerAnchors.Length == 0)
            return false;
        for (int i = 0; i < flowerAnchors.Length; i++)
        {
            if (flowerAnchors[i] == null)
                return false;
        }
        return true;
    }

    private void ApplyAmount(bool force)
    {
        if (!AnchorsReady())
            EnsureFlowerAnchors();
        if (flowerAnchors == null)
            return;

        lastAmount = flowerAmount;
        float t = Mathf.Clamp01(flowerAmount / 100f);
        float grow = Application.isPlaying ? Mathf.Lerp(0.2f, 1f, t) : 1f;

        if (flowerRoot != null)
            flowerRoot.gameObject.SetActive(true);

        for (int i = 0; i < flowerAnchors.Length; i++)
        {
            Transform anchor = flowerAnchors[i];
            if (anchor == null)
                continue;
            MeshRenderer meshRenderer = anchor.GetComponent<MeshRenderer>();
            if (t <= 0.001f && Application.isPlaying)
            {
                if (meshRenderer != null)
                    meshRenderer.enabled = false;
                continue;
            }

            if (i < LockedFlowers.Length)
                ApplyLockedPose(anchor, LockedFlowers[i], grow);

            if (meshRenderer != null)
                meshRenderer.enabled = true;
        }
    }

    private void Build()
    {
        NeutralizeOldFlowerShape();
        EnsureFlowerAnchors();
        built = true;
        lastAmount = float.NaN;
        ApplyAmount(true);
    }

    private void EnsureFlowerAnchors()
    {
        Transform root = transform.Find(ChildName);
        if (root == null)
        {
            GameObject go = new GameObject(ChildName);
            root = go.transform;
            root.SetParent(transform, false);
            root.localPosition = Vector3.zero;
            root.localRotation = Quaternion.identity;
            root.localScale = Vector3.one;
        }
        flowerRoot = root;
        flowerRenderer = null;
        flowerFilter = null;
        RemoveDroppedFlower(root);

        if (flowerSharedMesh == null)
            flowerSharedMesh = CreateLocalFlowerMesh();
        if (flowerMaterial == null)
            flowerMaterial = MakeFlowerMaterial(flowerColor);
        ApplyColorToMaterial(flowerMaterial, flowerColor);
        Material sharedMat = flowerMaterial;

        flowerAnchors = new Transform[LockedFlowers.Length];
        flowerCount = LockedFlowers.Length;
        for (int i = 0; i < LockedFlowers.Length; i++)
        {
            FlowerPose pose = LockedFlowers[i];
            Transform anchor = root.Find(pose.name);
            bool created = false;
            if (anchor == null)
            {
                GameObject go = new GameObject(pose.name);
                anchor = go.transform;
                anchor.SetParent(root, false);
                created = true;
            }

            MeshFilter filter = anchor.GetComponent<MeshFilter>();
            if (filter == null)
                filter = anchor.gameObject.AddComponent<MeshFilter>();
            MeshRenderer meshRenderer = anchor.GetComponent<MeshRenderer>();
            if (meshRenderer == null)
                meshRenderer = anchor.gameObject.AddComponent<MeshRenderer>();
            filter.sharedMesh = flowerSharedMesh;
            meshRenderer.sharedMaterial = sharedMat;
            meshRenderer.shadowCastingMode = ShadowCastingMode.Off;
            meshRenderer.receiveShadows = false;
            meshRenderer.lightProbeUsage = LightProbeUsage.Off;
            meshRenderer.reflectionProbeUsage = ReflectionProbeUsage.Off;

            if (created)
                ApplyLockedPose(anchor, pose, 1f);

            flowerAnchors[i] = anchor;
        }
    }

    private static void ApplyLockedPose(Transform anchor, FlowerPose pose, float grow)
    {
        anchor.localPosition = pose.localPosition;
        anchor.localRotation = pose.localRotation;
        anchor.localScale = pose.localScale * Mathf.Max(0.001f, grow);
    }

    private void RemoveDroppedFlower(Transform root)
    {
        for (int i = 0; i < RemovedFlowerNames.Length; i++)
        {
            Transform dropped = root.Find(RemovedFlowerNames[i]);
            if (dropped == null)
                continue;
            if (Application.isPlaying)
                Destroy(dropped.gameObject);
            else
                DestroyImmediate(dropped.gameObject);
        }
    }

    private static Mesh CreateLocalFlowerMesh()
    {
        var verts = new List<Vector3>(96);
        var tris = new List<int>(240);
        const int slices = 40;
        const float radius = 0.5f;
        const float thickness = 0.12f;
        int top = 0;
        verts.Add(new Vector3(0f, 0f, thickness));
        for (int s = 0; s < slices; s++)
        {
            float ang = s * Mathf.PI * 2f / slices;
            float lobe = Mathf.Pow(Mathf.Abs(Mathf.Cos(2f * ang)), 0.62f);
            float rad = radius * Mathf.Lerp(0.48f, 1f, lobe);
            verts.Add(new Vector3(Mathf.Cos(ang) * rad, Mathf.Sin(ang) * rad, thickness));
        }
        int bottom = verts.Count;
        verts.Add(Vector3.zero);
        for (int s = 0; s < slices; s++)
        {
            float ang = s * Mathf.PI * 2f / slices;
            float lobe = Mathf.Pow(Mathf.Abs(Mathf.Cos(2f * ang)), 0.62f);
            float rad = radius * Mathf.Lerp(0.48f, 1f, lobe);
            verts.Add(new Vector3(Mathf.Cos(ang) * rad, Mathf.Sin(ang) * rad, 0f));
        }
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
        var mesh = new Mesh { name = "KerisFlowerLocal" };
        mesh.SetVertices(verts);
        mesh.SetTriangles(tris, 0);
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        return mesh;
    }

    private Mesh GetBodyMesh()
    {
        SkinnedMeshRenderer skin = GetComponent<SkinnedMeshRenderer>();
        if (skin != null && skin.sharedMesh != null)
            return skin.sharedMesh;
        MeshFilter filter = GetComponent<MeshFilter>();
        return filter != null ? filter.sharedMesh : null;
    }

    private Mesh GetLiveMesh()
    {
        SkinnedMeshRenderer skin = GetComponent<SkinnedMeshRenderer>();
        if (skin != null)
        {
            if (bakeMesh == null)
            {
                bakeMesh = new Mesh { name = "KerisFlowerBake" };
                bakeMesh.hideFlags = HideFlags.HideAndDontSave;
            }
            bakeMesh.Clear();
            skin.BakeMesh(bakeMesh);
            if (bakeMesh.vertexCount > 0)
                return bakeMesh;
        }
        return GetBodyMesh();
    }

    private bool NeedsResample()
    {
        SkinnedMeshRenderer skin = GetComponent<SkinnedMeshRenderer>();
        if (skin == null || skin.sharedMesh == null)
            return stamps == null || stamps.Length == 0;

        float width = ReadShape(skin, "Width");
        float height = ReadShape(skin, "Height");
        float curve = ReadShape(skin, "Curve");
        float sharp = ReadShape(skin, "Sharpness");
        bool dirty = stamps == null || stamps.Length == 0
            || Mathf.Abs(width - lastSampleWidth) > 0.4f
            || Mathf.Abs(height - lastSampleHeight) > 0.4f
            || Mathf.Abs(curve - lastSampleCurve) > 0.4f
            || Mathf.Abs(sharp - lastSampleSharp) > 0.4f;
        if (!dirty)
            return false;
        lastSampleWidth = width;
        lastSampleHeight = height;
        lastSampleCurve = curve;
        lastSampleSharp = sharp;
        return true;
    }

    private static float ReadShape(SkinnedMeshRenderer skin, string name)
    {
        Mesh mesh = skin.sharedMesh;
        for (int i = 0; i < mesh.blendShapeCount; i++)
        {
            if (string.Equals(mesh.GetBlendShapeName(i), name, System.StringComparison.OrdinalIgnoreCase))
                return skin.GetBlendShapeWeight(i);
        }
        return 0f;
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

        Vector3 view = ViewDirection(body.bounds.center);
        Vector3 worldN = view.normalized;
        if (flipPopDirection)
            worldN = -worldN;
        Camera camera = Camera.main;
        Vector3 worldV = camera != null ? camera.transform.up : Vector3.up;
        worldV -= worldN * Vector3.Dot(worldV, worldN);
        if (worldV.sqrMagnitude < 0.0001f)
            worldV = Vector3.up;
        worldV.Normalize();
        Vector3 worldU = Vector3.Cross(worldN, worldV).normalized;
        worldV = Vector3.Cross(worldU, worldN).normalized;

        var faces = new List<Face>(indices.Count / 9);
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
            if (Vector3.Dot(n, worldN) < 0.25f)
                continue;
            faces.Add(new Face { center = (a + b + c) / 3f, normal = n, area = area });
        }

        Vector3 localLong;
        Vector3 localThin;
        ChooseAxes(body.bounds, out localLong, out localThin);
        if (faces.Count < 3)
            return FallbackStamps(body.bounds, localLong, localThin, worldU, worldV, worldN);

        float alongMin = float.MaxValue;
        float alongMax = float.MinValue;
        var alongs = new float[faces.Count];
        var acrosses = new float[faces.Count];
        var acrossSorted = new List<float>(faces.Count);
        for (int i = 0; i < faces.Count; i++)
        {
            alongs[i] = Vector3.Dot(faces[i].center, worldV);
            acrosses[i] = Vector3.Dot(faces[i].center, worldU);
            acrossSorted.Add(acrosses[i]);
            if (alongs[i] < alongMin) alongMin = alongs[i];
            if (alongs[i] > alongMax) alongMax = alongs[i];
        }

        acrossSorted.Sort();
        float centerline = acrossSorted[acrossSorted.Count / 2];

        const int bins = 16;
        float length = Mathf.Max(0.001f, alongMax - alongMin);
        var binWidth = new float[bins];
        var binCount = new int[bins];
        var binMin = new float[bins];
        var binMax = new float[bins];
        for (int b = 0; b < bins; b++)
        {
            binMin[b] = float.MaxValue;
            binMax[b] = float.MinValue;
        }
        for (int i = 0; i < faces.Count; i++)
        {
            int b = Mathf.Clamp(Mathf.FloorToInt(((alongs[i] - alongMin) / length) * bins), 0, bins - 1);
            binCount[b]++;
            if (acrosses[i] < binMin[b]) binMin[b] = acrosses[i];
            if (acrosses[i] > binMax[b]) binMax[b] = acrosses[i];
        }

        var widths = new List<float>(bins);
        for (int b = 0; b < bins; b++)
        {
            if (binCount[b] < 4 || binMin[b] >= binMax[b])
                continue;
            float w = binMax[b] - binMin[b];
            binWidth[b] = w;
            widths.Add(w);
        }
        widths.Sort();
        float bladeWidth = widths.Count > 0
            ? widths[widths.Count / 2]
            : Mathf.Max(0.008f, acrossSorted[acrossSorted.Count - 1] - acrossSorted[0]);
        bladeWidth = Mathf.Max(bladeWidth, 0.008f);

        var innerAcross = new List<float>(faces.Count);
        for (int i = 0; i < faces.Count; i++)
        {
            int b = Mathf.Clamp(Mathf.FloorToInt(((alongs[i] - alongMin) / length) * bins), 0, bins - 1);
            if (binWidth[b] > bladeWidth * 1.35f)
                continue;
            innerAcross.Add(acrosses[i]);
        }
        if (innerAcross.Count > 0)
        {
            innerAcross.Sort();
            centerline = innerAcross[innerAcross.Count / 2];
        }

        int count = Mathf.Clamp(flowerCount, 3, 12);
        float alongStart = alongMin + length * 0.22f;
        float alongEnd = alongMax - length * 0.22f;
        float push = Mathf.Max(surfaceClearance, popMeters);
        float flowerSize = Mathf.Max(0.01f, Mathf.Min(bladeWidth * 0.42f, flowerRadiusMeters));
        float alongWindow = length * 0.05f;

        Vector3 PlaceOnBlade(float targetAlong, float across)
        {
            int best = -1;
            float bestFront = float.MinValue;
            for (int f = 0; f < faces.Count; f++)
            {
                if (Mathf.Abs(alongs[f] - targetAlong) > alongWindow)
                    continue;
                if (Mathf.Abs(acrosses[f] - across) > bladeWidth)
                    continue;
                float faceFront = Vector3.Dot(faces[f].center, worldN);
                if (faceFront > bestFront)
                {
                    bestFront = faceFront;
                    best = f;
                }
            }

            if (best < 0)
            {
                float bestAlong = float.MaxValue;
                for (int f = 0; f < faces.Count; f++)
                {
                    float alongErr = Mathf.Abs(alongs[f] - targetAlong);
                    if (alongErr < bestAlong)
                    {
                        bestAlong = alongErr;
                        best = f;
                        bestFront = Vector3.Dot(faces[f].center, worldN);
                    }
                }
            }

            Vector3 point = faces[Mathf.Max(0, best)].center;
            point += worldU * (across - Vector3.Dot(point, worldU));
            point += worldN * push;
            return point;
        }

        Vector3 bottomPos = PlaceOnBlade(alongStart, centerline);
        float lockedAcross = Vector3.Dot(bottomPos, worldU);

        var result = new Stamp[count];
        for (int i = 0; i < count; i++)
        {
            float t = count == 1 ? 0.5f : i / (float)(count - 1);
            float targetAlong = Mathf.Lerp(alongStart, alongEnd, t);
            result[i] = new Stamp
            {
                worldOrigin = PlaceOnBlade(targetAlong, lockedAcross),
                worldU = worldU,
                worldV = worldV,
                worldN = worldN,
                size = flowerSize
            };
        }

        return result;
    }

    private Stamp[] FallbackStamps(
        Bounds bounds,
        Vector3 localLong,
        Vector3 localThin,
        Vector3 worldU,
        Vector3 worldV,
        Vector3 worldN)
    {
        int count = Mathf.Clamp(flowerCount, 3, 12);
        var result = new Stamp[count];
        Vector3 center = transform.TransformPoint(bounds.center);
        float length = transform.TransformVector(localLong * bounds.size.magnitude * 0.5f).magnitude;
        float width = Mathf.Max(0.01f, transform.TransformVector(localThin * bounds.size.magnitude * 0.15f).magnitude);
        for (int i = 0; i < count; i++)
        {
            float u = count == 1 ? 0.5f : i / (float)(count - 1);
            float t = Mathf.Lerp(0.2f, 0.82f, u);
            result[i] = new Stamp
            {
                worldOrigin = center + worldV * ((t - 0.5f) * length) + worldN * popMeters,
                worldU = worldU,
                worldV = worldV,
                worldN = worldN,
                size = Mathf.Min(flowerRadiusMeters, width * 0.46f)
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

        float radius = stamp.size * Mathf.Lerp(0.2f, 1f, t);
        AddQuatrefoil(verts, tris, stamp, radius);
    }

    private void AddQuatrefoil(
        List<Vector3> verts,
        List<int> tris,
        Stamp stamp,
        float radius)
    {
        const int slices = 40;
        float thickness = Mathf.Max(0.0025f, radius * 0.22f);
        int top = verts.Count;
        verts.Add(ToLocal(stamp.worldOrigin + stamp.worldN * thickness));
        for (int s = 0; s < slices; s++)
        {
            float ang = s * Mathf.PI * 2f / slices;
            float lobe = Mathf.Pow(Mathf.Abs(Mathf.Cos(2f * ang)), 0.62f);
            float rad = radius * Mathf.Lerp(0.48f, 1f, lobe);
            Vector3 world = stamp.worldOrigin
                + stamp.worldU * (Mathf.Cos(ang) * rad)
                + stamp.worldV * (Mathf.Sin(ang) * rad)
                + stamp.worldN * thickness;
            verts.Add(ToLocal(world));
        }

        int bottom = verts.Count;
        verts.Add(ToLocal(stamp.worldOrigin));
        for (int s = 0; s < slices; s++)
        {
            float ang = s * Mathf.PI * 2f / slices;
            float lobe = Mathf.Pow(Mathf.Abs(Mathf.Cos(2f * ang)), 0.62f);
            float rad = radius * Mathf.Lerp(0.48f, 1f, lobe);
            Vector3 world = stamp.worldOrigin
                + stamp.worldU * (Mathf.Cos(ang) * rad)
                + stamp.worldV * (Mathf.Sin(ang) * rad);
            verts.Add(ToLocal(world));
        }

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

    private static void ApplyColorToMaterial(Material mat, Color color)
    {
        if (mat == null)
            return;
        if (mat.HasProperty("_BaseColor"))
            mat.SetColor("_BaseColor", color);
        if (mat.HasProperty("_Color"))
            mat.SetColor("_Color", color);
    }

    private static Material MakeFlowerMaterial(Color petal)
    {
        Shader shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null)
            shader = Shader.Find("Universal Render Pipeline/Unlit");
        if (shader == null)
            shader = Shader.Find("Sprites/Default");
        Material mat = new Material(shader);
        mat.name = "KerisFlowerCoded";
        ApplyColorToMaterial(mat, petal);
        if (mat.HasProperty("_Smoothness"))
            mat.SetFloat("_Smoothness", 0.35f);
        if (mat.HasProperty("_Metallic"))
            mat.SetFloat("_Metallic", 0.25f);
        if (mat.HasProperty("_Cull"))
            mat.SetFloat("_Cull", 0f);
        if (mat.HasProperty("_CullMode"))
            mat.SetFloat("_CullMode", 0f);
        mat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Geometry + 10;

        mat.doubleSidedGI = true;
        return mat;
    }

    private Vector3 ToLocal(Vector3 world)
    {
        return transform.InverseTransformPoint(world);
    }

    private void OnDrawGizmos()
    {
        if (flowerAnchors == null)
            return;
        Gizmos.color = new Color(0.85f, 0.45f, 0.1f, 0.9f);
        for (int i = 0; i < flowerAnchors.Length; i++)
        {
            if (flowerAnchors[i] == null)
                continue;
            Gizmos.DrawWireSphere(flowerAnchors[i].position, 0.012f);
        }
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
