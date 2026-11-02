using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Unity drops sparse Keris blend shapes (flowers/carving) when it splits
/// vertices on import. Rebuild those shapes by mapping FBX deltas onto the
/// live mesh, and push flower faces out along their normals if needed.
/// </summary>
public static class KerisBlendShapeRebuilder
{
    private const string RebuiltSuffix = "_RebuiltBlendShapes";
    private const string ResourceName = "KerisBlendShapes";

    public static void RebuildIfNeeded(SkinnedMeshRenderer skin, bool force = false)
    {
        if (skin == null || skin.sharedMesh == null)
            return;

        Mesh source = skin.sharedMesh;
        if (!force && source.name.IndexOf(RebuiltSuffix, StringComparison.Ordinal) >= 0)
            return;

        Mesh copy = UnityEngine.Object.Instantiate(source);
        string baseName = source.name.Replace(RebuiltSuffix, "");
        copy.name = baseName + RebuiltSuffix;
        if (!RebuildOnMesh(copy))
        {
#if UNITY_EDITOR
            if (!Application.isPlaying)
                UnityEngine.Object.DestroyImmediate(copy);
            else
#endif
                UnityEngine.Object.Destroy(copy);
            return;
        }

        skin.sharedMesh = copy;
        skin.updateWhenOffscreen = true;
        Debug.Log(
            "MORPHIC: Rebuilt Keris blend shapes on '" + copy.name + "' verts=" +
            copy.vertexCount + " " + Describe(copy, "Carving 2.001") + " " +
            Describe(copy, "Carving 2") + " " + Describe(copy, "Height"),
            skin);
    }

    public static void RebuildNamed(SkinnedMeshRenderer skin, params string[] names)
    {
        if (skin == null || skin.sharedMesh == null || names == null || names.Length == 0)
            return;

        Mesh mesh = skin.sharedMesh;
        bool alreadyValid = true;
        for (int n = 0; n < names.Length; n++)
        {
            if (!ShapeLooksValid(mesh, names[n], 200, 0.04f))
            {
                alreadyValid = false;
                break;
            }
        }

        if (alreadyValid)
            return;

        Vector3[] unityVerts = mesh.vertices;
        Vector3[] unityNorms = mesh.normals;
        int vCount = unityVerts.Length;
        if (vCount == 0)
            return;

        var rebuilt = new Dictionary<string, Frame>(StringComparer.OrdinalIgnoreCase);
        BlendData data = LoadData();
        if (data != null && data.rest != null)
        {
            int[] map;
            Func<Vector3, Vector3> xform = FitTransform(unityVerts, data.rest, out map);
            if (xform != null)
            {
                var wanted = new HashSet<string>(names, StringComparer.OrdinalIgnoreCase);
                for (int s = 0; s < data.shapes.Count; s++)
                {
                    ShapeData shape = data.shapes[s];
                    if (!wanted.Contains(shape.name))
                        continue;
                    Vector3[] delta = new Vector3[vCount];
                    ApplyShape(shape, data.rest, map, xform, unityVerts, unityNorms, delta);
                    AmplifyDelta(delta, 0.08f);
                    if (MaxMagnitude(delta) < 0.01f)
                        continue;
                    rebuilt[shape.name] = MakeFrame(shape.name, delta);
                }
            }
        }

        for (int n = 0; n < names.Length; n++)
        {
            Frame existing;
            if (rebuilt.TryGetValue(names[n], out existing) &&
                MaxMagnitude(existing.deltaVerts) >= 0.04f &&
                CountMoved(existing.deltaVerts) >= 200)
                continue;

            Vector3[] synthesized = SynthesizeShape(names[n], unityVerts);
            if (synthesized == null || MaxMagnitude(synthesized) < 0.01f)
                continue;
            rebuilt[names[n]] = MakeFrame(names[n], synthesized);
        }

        if (rebuilt.Count == 0)
            return;

        var kept = ExtractExisting(mesh);
        foreach (var pair in rebuilt)
            kept[pair.Key] = pair.Value;

        var savedWeights = new Dictionary<string, float>(StringComparer.OrdinalIgnoreCase);
        for (int i = 0; i < mesh.blendShapeCount; i++)
            savedWeights[mesh.GetBlendShapeName(i)] = skin.GetBlendShapeWeight(i);

        string baseName = mesh.name.Replace("_PinchShapes", "").Replace("_NoOldFlowers", "");
        Mesh copy = UnityEngine.Object.Instantiate(mesh);
        copy.name = baseName + "_PinchShapes";
        copy.ClearBlendShapes();
        foreach (var pair in kept)
        {
            copy.AddBlendShapeFrame(
                pair.Value.name,
                pair.Value.weight,
                pair.Value.deltaVerts,
                pair.Value.deltaNorms,
                pair.Value.deltaTans);
        }

        skin.sharedMesh = copy;
        for (int i = 0; i < copy.blendShapeCount; i++)
        {
            float weight;
            if (!savedWeights.TryGetValue(copy.GetBlendShapeName(i), out weight))
                weight = 0f;
            skin.SetBlendShapeWeight(i, weight);
        }

        Debug.Log(
            "MORPHIC: Restored pinch shapes " + string.Join(",", rebuilt.Keys) +
            " " + Describe(copy, "Sharpness") + " " + Describe(copy, "Curve"),
            skin);
    }

    public static bool RebuildOnMesh(Mesh mesh)
    {
        Vector3[] unityVerts = mesh.vertices;
        Vector3[] unityNorms = mesh.normals;
        int vCount = unityVerts.Length;
        if (vCount == 0)
            return false;

        var kept = ExtractExisting(mesh);
        var rebuilt = new Dictionary<string, Frame>(StringComparer.OrdinalIgnoreCase);

        BlendData data = LoadData();
        int[] map = null;
        Func<Vector3, Vector3> xform = null;
        if (data != null && data.rest != null && data.rest.Length >= 9)
            xform = FitTransform(unityVerts, data.rest, out map);
        if (xform != null)
        {
            for (int s = 0; s < data.shapes.Count; s++)
            {
                ShapeData shape = data.shapes[s];
                Vector3[] delta = new Vector3[vCount];
                ApplyShape(shape, data.rest, map, xform, unityVerts, unityNorms, delta);
                if (IsCarving(shape.name))
                    AlignOutward(delta, unityVerts, unityNorms, mesh.bounds.center);

                rebuilt[shape.name] = new Frame
                {
                    name = shape.name,
                    weight = 100f,
                    deltaVerts = delta,
                    deltaNorms = new Vector3[vCount],
                    deltaTans = new Vector3[vCount]
                };
            }
        }

        if (!rebuilt.ContainsKey("Carving 2.001"))
        {
            rebuilt["Carving 2.001"] = new Frame
            {
                name = "Carving 2.001",
                weight = 100f,
                deltaVerts = new Vector3[vCount],
                deltaNorms = new Vector3[vCount],
                deltaTans = new Vector3[vCount]
            };
        }

        EnsureFlowerExtrusion(mesh, rebuilt, unityVerts, unityNorms);

        mesh.ClearBlendShapes();
        var emitted = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var pair in rebuilt)
        {
            Frame frame = pair.Value;
            Frame existing;
            if (!IsCarving(pair.Key) &&
                kept.TryGetValue(pair.Key, out existing) &&
                MaxMagnitude(existing.deltaVerts) > 0.01f)
                frame = existing;
            mesh.AddBlendShapeFrame(
                frame.name,
                frame.weight,
                frame.deltaVerts,
                frame.deltaNorms,
                frame.deltaTans);
            emitted.Add(pair.Key);
        }

        foreach (var pair in kept)
        {
            if (emitted.Contains(pair.Key))
                continue;
            mesh.AddBlendShapeFrame(
                pair.Value.name,
                pair.Value.weight,
                pair.Value.deltaVerts,
                pair.Value.deltaNorms,
                pair.Value.deltaTans);
        }

        mesh.RecalculateBounds();
        return ShapeLooksValid(mesh, "Carving 2.001", 20, 0.001f) ||
               FlowerFallbackApplied(rebuilt);
    }

    private static float SafeDiv(float a, float b)
    {
        return Mathf.Abs(b) < 0.000001f ? 1f : a / b;
    }

    private static bool FlowerFallbackApplied(Dictionary<string, Frame> rebuilt)
    {
        Frame frame;
        return rebuilt.TryGetValue("Carving 2.001", out frame) &&
               MaxMagnitude(frame.deltaVerts) > 0.005f;
    }

    private static void EnsureFlowerExtrusion(
        Mesh mesh,
        Dictionary<string, Frame> rebuilt,
        Vector3[] verts,
        Vector3[] norms)
    {
        Frame frame;
        if (!rebuilt.TryGetValue("Carving 2.001", out frame))
            return;
        HashSet<int> flower = CollectSubmeshVerts(mesh, 0);
        if (flower.Count < 20 || flower.Count > verts.Length / 3)
            return;

        Vector3 center = mesh.bounds.center;
        float distance = Mathf.Max(mesh.bounds.extents.magnitude, 8f);
        Vector3[] delta = frame.deltaVerts;
        int moved = 0;
        foreach (int i in flower)
        {
            if (i < 0 || i >= verts.Length)
                continue;
            Vector3 n = i < norms.Length ? norms[i] : Vector3.zero;
            if (n.sqrMagnitude < 0.0001f)
                n = verts[i] - center;
            n.Normalize();
            if (Vector3.Dot(n, verts[i] - center) < 0f)
                n = -n;
            delta[i] = n * distance;
            moved++;
        }

        Debug.Log("MORPHIC: Keris flower fallback extruded " + moved + " verts by " + distance);
    }

    private static HashSet<int> CollectPlanarCluster(Vector3[] verts)
    {
        var set = new HashSet<int>();
        if (verts.Length == 0)
            return set;

        float[] xs = new float[verts.Length];
        float[] ys = new float[verts.Length];
        float[] zs = new float[verts.Length];
        for (int i = 0; i < verts.Length; i++)
        {
            xs[i] = verts[i].x;
            ys[i] = verts[i].y;
            zs[i] = verts[i].z;
        }

        int axis = TightestAxis(xs, ys, zs);
        float[] values = axis == 0 ? xs : axis == 1 ? ys : zs;
        float plane = Median(values);
        float span = MaxAbs(values) * 0.02f + 0.001f;
        for (int i = 0; i < values.Length; i++)
        {
            if (Mathf.Abs(values[i] - plane) <= span)
                set.Add(i);
        }

        return set;
    }

    private static int TightestAxis(float[] xs, float[] ys, float[] zs)
    {
        float rx = Range(xs);
        float ry = Range(ys);
        float rz = Range(zs);
        if (rx <= ry && rx <= rz)
            return 0;
        if (ry <= rz)
            return 1;
        return 2;
    }

    private static float Range(float[] v)
    {
        float min = v[0];
        float max = v[0];
        for (int i = 1; i < v.Length; i++)
        {
            if (v[i] < min) min = v[i];
            if (v[i] > max) max = v[i];
        }
        return max - min;
    }

    private static float MaxAbs(float[] v)
    {
        float m = 0f;
        for (int i = 0; i < v.Length; i++)
            m = Mathf.Max(m, Mathf.Abs(v[i]));
        return m;
    }

    private static float Median(float[] v)
    {
        float[] copy = (float[])v.Clone();
        System.Array.Sort(copy);
        return copy[copy.Length / 2];
    }

    private static HashSet<int> CollectSubmeshVerts(Mesh mesh, int submesh)
    {
        var set = new HashSet<int>();
        if (mesh.subMeshCount <= submesh)
            return set;
        int[] tris = mesh.GetTriangles(submesh);
        for (int i = 0; i < tris.Length; i++)
            set.Add(tris[i]);
        return set;
    }

    private static void ApplyShape(
        ShapeData shape,
        Vector3[] rest,
        int[] map,
        Func<Vector3, Vector3> xform,
        Vector3[] unityVerts,
        Vector3[] unityNorms,
        Vector3[] delta)
    {
        Vector3[] fbxDelta = new Vector3[rest.Length];
        if (shape.indexes == null || shape.indexes.Length == 0)
        {
            int n = Mathf.Min(rest.Length, shape.vertices.Length);
            for (int i = 0; i < n; i++)
                fbxDelta[i] = shape.vertices[i];
        }
        else
        {
            int n = Mathf.Min(shape.indexes.Length, shape.vertices.Length);
            for (int i = 0; i < n; i++)
            {
                int idx = shape.indexes[i];
                if (idx >= 0 && idx < fbxDelta.Length)
                    fbxDelta[idx] = shape.vertices[i];
            }
        }

        for (int u = 0; u < map.Length; u++)
        {
            int f = map[u];
            if (f < 0)
                continue;
            Vector3 before = xform(rest[f]);
            Vector3 after = xform(rest[f] + fbxDelta[f]);
            delta[u] = after - before;
        }
    }

    private static void AlignOutward(Vector3[] delta, Vector3[] verts, Vector3[] norms, Vector3 center)
    {
        for (int i = 0; i < delta.Length; i++)
        {
            if (delta[i].sqrMagnitude < 0.0000001f)
                continue;
            Vector3 n = i < norms.Length ? norms[i] : Vector3.zero;
            if (n.sqrMagnitude < 0.0001f)
                n = verts[i] - center;
            if (n.sqrMagnitude < 0.0001f)
                continue;
            if (Vector3.Dot(delta[i], n) < 0f)
                delta[i] = -delta[i];
        }
    }

    private static Func<Vector3, Vector3> FitTransform(
        Vector3[] unityVerts,
        Vector3[] rest,
        out int[] map)
    {
        Func<Vector3, Vector3>[] candidates =
        {
            v => v,
            v => v * 0.01f,
            v => new Vector3(v.x, v.z, -v.y),
            v => new Vector3(v.x, v.z, -v.y) * 0.01f,
            v => new Vector3(v.x, v.y, -v.z),
            v => new Vector3(v.x, v.y, -v.z) * 0.01f,
            v => new Vector3(-v.x, v.y, v.z),
            v => new Vector3(v.x * -0.09451288f, v.y * -3.47529173f, v.z * -0.34030145f),
            v => new Vector3(v.x * 0.09451288f, v.y * 3.47529173f, v.z * 0.34030145f),
            v => new Vector3(v.x * -0.09451288f, v.z * -3.47529173f, -v.y * -0.34030145f),
            v => new Vector3(v.x * 0.09451288f, v.z * 3.47529173f, -v.y * 0.34030145f),
            v => new Vector3(v.x * -0.00094513f, v.y * -0.03475292f, v.z * -0.00340301f),
            v => new Vector3(v.x * 0.00094513f, v.y * 0.03475292f, v.z * 0.00340301f)
        };

        float bestError = float.MaxValue;
        Func<Vector3, Vector3> best = null;
        int[] bestMap = null;
        int bestHits = 0;

        Bounds unityBounds = BoundsOf(unityVerts);
        Bounds restBounds = BoundsOf(rest);
        Vector3 restCenter = restBounds.center;
        Vector3 unityCenter = unityBounds.center;
        Vector3 bboxScale = new Vector3(
            SafeDiv(unityBounds.size.x, restBounds.size.x),
            SafeDiv(unityBounds.size.y, restBounds.size.y),
            SafeDiv(unityBounds.size.z, restBounds.size.z));

        var candidateList = new List<Func<Vector3, Vector3>>(candidates);
        candidateList.Add(v => Vector3.Scale(v - restCenter, bboxScale) + unityCenter);
        candidateList.Add(v => Vector3.Scale(new Vector3(v.x, v.z, -v.y) - restCenter, bboxScale) + unityCenter);
        candidateList.Add(v => Vector3.Scale(new Vector3(v.x, v.y, -v.z) - restCenter, bboxScale) + unityCenter);
        candidateList.Add(v => Vector3.Scale(new Vector3(-v.x, v.y, v.z) - restCenter, bboxScale) + unityCenter);
        candidates = candidateList.ToArray();

        float unitySize = Mathf.Max(unityBounds.extents.magnitude, 0.0001f);

        for (int c = 0; c < candidates.Length; c++)
        {
            Vector3[] transformed = new Vector3[rest.Length];
            for (int i = 0; i < rest.Length; i++)
                transformed[i] = candidates[c](rest[i]);

            int[] candidateMap;
            float error;
            int hits = MapNearest(unityVerts, transformed, unitySize * 0.05f, out candidateMap, out error);
            if (hits > bestHits || (hits == bestHits && error < bestError))
            {
                bestHits = hits;
                bestError = error;
                best = candidates[c];
                bestMap = candidateMap;
            }
        }

        map = bestMap;
        if (best == null || bestHits < Mathf.Max(20, unityVerts.Length / 20))
            return null;
        return best;
    }

    private static int MapNearest(
        Vector3[] unityVerts,
        Vector3[] transformed,
        float maxDist,
        out int[] map,
        out float meanError)
    {
        var grid = new Dictionary<long, List<int>>(transformed.Length);
        float cell = Mathf.Max(maxDist, 0.0001f);
        for (int i = 0; i < transformed.Length; i++)
        {
            long key = Hash(transformed[i], cell);
            List<int> list;
            if (!grid.TryGetValue(key, out list))
            {
                list = new List<int>(4);
                grid[key] = list;
            }
            list.Add(i);
        }

        map = new int[unityVerts.Length];
        float errorSum = 0f;
        int hits = 0;
        float maxSqr = maxDist * maxDist;
        var neighbor = new List<int>(32);

        for (int u = 0; u < unityVerts.Length; u++)
        {
            Vector3 p = unityVerts[u];
            neighbor.Clear();
            int ix = Mathf.FloorToInt(p.x / cell);
            int iy = Mathf.FloorToInt(p.y / cell);
            int iz = Mathf.FloorToInt(p.z / cell);
            for (int dx = -1; dx <= 1; dx++)
            for (int dy = -1; dy <= 1; dy++)
            for (int dz = -1; dz <= 1; dz++)
            {
                List<int> list;
                if (!grid.TryGetValue(HashCell(ix + dx, iy + dy, iz + dz), out list))
                    continue;
                neighbor.AddRange(list);
            }

            float bestSqr = maxSqr;
            int best = -1;
            for (int n = 0; n < neighbor.Count; n++)
            {
                float sqr = (transformed[neighbor[n]] - p).sqrMagnitude;
                if (sqr >= bestSqr)
                    continue;
                bestSqr = sqr;
                best = neighbor[n];
            }

            map[u] = best;
            if (best < 0)
                continue;
            hits++;
            errorSum += Mathf.Sqrt(bestSqr);
        }

        meanError = hits > 0 ? errorSum / hits : float.MaxValue;
        return hits;
    }

    private static long Hash(Vector3 p, float cell)
    {
        return HashCell(
            Mathf.FloorToInt(p.x / cell),
            Mathf.FloorToInt(p.y / cell),
            Mathf.FloorToInt(p.z / cell));
    }

    private static long HashCell(int x, int y, int z)
    {
        return ((long)(x + 100000) & 0x1FFFFF) |
               (((long)(y + 100000) & 0x1FFFFF) << 21) |
               (((long)(z + 100000) & 0x1FFFFF) << 42);
    }

    private static Bounds BoundsOf(Vector3[] verts)
    {
        if (verts.Length == 0)
            return new Bounds();
        Bounds b = new Bounds(verts[0], Vector3.zero);
        for (int i = 1; i < verts.Length; i++)
            b.Encapsulate(verts[i]);
        return b;
    }

    private static Dictionary<string, Frame> ExtractExisting(Mesh mesh)
    {
        var kept = new Dictionary<string, Frame>(StringComparer.OrdinalIgnoreCase);
        int vCount = mesh.vertexCount;
        for (int s = 0; s < mesh.blendShapeCount; s++)
        {
            string name = mesh.GetBlendShapeName(s);
            if (mesh.GetBlendShapeFrameCount(s) <= 0)
                continue;
            var dv = new Vector3[vCount];
            var dn = new Vector3[vCount];
            var dt = new Vector3[vCount];
            mesh.GetBlendShapeFrameVertices(s, 0, dv, dn, dt);
            kept[name] = new Frame
            {
                name = name,
                weight = mesh.GetBlendShapeFrameWeight(s, 0),
                deltaVerts = dv,
                deltaNorms = dn,
                deltaTans = dt
            };
        }

        return kept;
    }

    public static bool ShapeLooksValid(Mesh mesh, string name, int minMoved, float minMag)
    {
        if (mesh == null)
            return false;
        int index = -1;
        for (int i = 0; i < mesh.blendShapeCount; i++)
        {
            if (!string.Equals(mesh.GetBlendShapeName(i), name, StringComparison.OrdinalIgnoreCase))
                continue;
            index = i;
            break;
        }

        if (index < 0 || mesh.GetBlendShapeFrameCount(index) <= 0)
            return false;

        var dv = new Vector3[mesh.vertexCount];
        var dn = new Vector3[mesh.vertexCount];
        var dt = new Vector3[mesh.vertexCount];
        mesh.GetBlendShapeFrameVertices(index, 0, dv, dn, dt);
        int moved = 0;
        float max = 0f;
        for (int i = 0; i < dv.Length; i++)
        {
            float mag = dv[i].sqrMagnitude;
            if (mag <= 0.0000001f)
                continue;
            moved++;
            if (mag > max)
                max = mag;
        }

        return moved >= minMoved && Mathf.Sqrt(max) >= minMag;
    }

    public static string Describe(Mesh mesh, string name)
    {
        if (mesh == null)
            return name + "=none";
        int index = -1;
        for (int i = 0; i < mesh.blendShapeCount; i++)
        {
            if (!string.Equals(mesh.GetBlendShapeName(i), name, StringComparison.OrdinalIgnoreCase))
                continue;
            index = i;
            break;
        }

        if (index < 0)
            return name + "=missing";
        var dv = new Vector3[mesh.vertexCount];
        var dn = new Vector3[mesh.vertexCount];
        var dt = new Vector3[mesh.vertexCount];
        if (mesh.GetBlendShapeFrameCount(index) <= 0)
            return name + "=no-frames";
        mesh.GetBlendShapeFrameVertices(index, 0, dv, dn, dt);
        return name + "=" + CountMoved(dv) + "v/" + MaxMagnitude(dv).ToString("0.000");
    }

    private static int CountMoved(Vector3[] delta)
    {
        int n = 0;
        for (int i = 0; i < delta.Length; i++)
        {
            if (delta[i].sqrMagnitude > 0.0000001f)
                n++;
        }

        return n;
    }

    private static float MaxMagnitude(Vector3[] delta)
    {
        float max = 0f;
        for (int i = 0; i < delta.Length; i++)
        {
            float mag = delta[i].sqrMagnitude;
            if (mag > max)
                max = mag;
        }

        return Mathf.Sqrt(max);
    }

    private static Frame MakeFrame(string name, Vector3[] delta)
    {
        return new Frame
        {
            name = name,
            weight = 100f,
            deltaVerts = delta,
            deltaNorms = new Vector3[delta.Length],
            deltaTans = new Vector3[delta.Length]
        };
    }

    private static void AmplifyDelta(Vector3[] delta, float minMag)
    {
        float mag = MaxMagnitude(delta);
        if (mag < 0.0001f || mag >= minMag)
            return;
        float scale = minMag / mag;
        for (int i = 0; i < delta.Length; i++)
            delta[i] *= scale;
    }

    private static Vector3[] SynthesizeShape(string name, Vector3[] verts)
    {
        if (string.Equals(name, "Sharpness", StringComparison.OrdinalIgnoreCase))
            return SynthesizeSharpness(verts);
        if (string.Equals(name, "Curve", StringComparison.OrdinalIgnoreCase))
            return SynthesizeCurve(verts);
        return null;
    }

    private static void GetBladeAxes(
        Vector3[] verts,
        out Vector3 center,
        out Vector3 along,
        out Vector3 across,
        out Vector3 thin,
        out float alongMin,
        out float alongMax,
        out float acrossExtent,
        out float thinExtent)
    {
        Bounds bounds = BoundsOf(verts);
        center = bounds.center;
        Vector3 size = bounds.size;
        int longAxis = 0;
        int thinAxis = 0;
        if (size.y >= size.x && size.y >= size.z)
            longAxis = 1;
        else if (size.z >= size.x && size.z >= size.y)
            longAxis = 2;
        if (size[thinAxis] > size.y)
            thinAxis = 1;
        if (size[thinAxis] > size.z)
            thinAxis = 2;
        if (thinAxis == longAxis)
            thinAxis = (longAxis + 1) % 3;
        int acrossAxis = 3 - longAxis - thinAxis;

        along = Vector3.zero;
        across = Vector3.zero;
        thin = Vector3.zero;
        along[longAxis] = 1f;
        across[acrossAxis] = 1f;
        thin[thinAxis] = 1f;

        alongMin = float.MaxValue;
        alongMax = float.MinValue;
        float acrossMin = float.MaxValue;
        float acrossMax = float.MinValue;
        float thinMin = float.MaxValue;
        float thinMax = float.MinValue;
        for (int i = 0; i < verts.Length; i++)
        {
            float a = Vector3.Dot(verts[i], along);
            float c = Vector3.Dot(verts[i], across);
            float t = Vector3.Dot(verts[i], thin);
            if (a < alongMin) alongMin = a;
            if (a > alongMax) alongMax = a;
            if (c < acrossMin) acrossMin = c;
            if (c > acrossMax) acrossMax = c;
            if (t < thinMin) thinMin = t;
            if (t > thinMax) thinMax = t;
        }

        acrossExtent = Mathf.Max(acrossMax - acrossMin, 0.0001f);
        thinExtent = Mathf.Max(thinMax - thinMin, 0.0001f);
    }

    private static Vector3[] SynthesizeSharpness(Vector3[] verts)
    {
        Vector3 center;
        Vector3 along;
        Vector3 across;
        Vector3 thin;
        float alongMin;
        float alongMax;
        float acrossExtent;
        float thinExtent;
        GetBladeAxes(
            verts,
            out center,
            out along,
            out across,
            out thin,
            out alongMin,
            out alongMax,
            out acrossExtent,
            out thinExtent);
        _ = across;
        _ = acrossExtent;

        var delta = new Vector3[verts.Length];
        for (int i = 0; i < verts.Length; i++)
        {
            float t = Mathf.InverseLerp(alongMin, alongMax, Vector3.Dot(verts[i], along));
            float offset = Vector3.Dot(verts[i] - center, thin);
            delta[i] = -thin * offset * (0.4f + 0.6f * t);
        }

        AmplifyDelta(delta, Mathf.Max(thinExtent * 0.45f, 0.06f));
        return delta;
    }

    private static Vector3[] SynthesizeCurve(Vector3[] verts)
    {
        Vector3 center;
        Vector3 along;
        Vector3 across;
        Vector3 thin;
        float alongMin;
        float alongMax;
        float acrossExtent;
        float thinExtent;
        GetBladeAxes(
            verts,
            out center,
            out along,
            out across,
            out thin,
            out alongMin,
            out alongMax,
            out acrossExtent,
            out thinExtent);
        _ = thin;
        _ = thinExtent;

        var delta = new Vector3[verts.Length];
        float amp = Mathf.Max(acrossExtent * 0.22f, 0.05f);
        for (int i = 0; i < verts.Length; i++)
        {
            float t = Mathf.InverseLerp(alongMin, alongMax, Vector3.Dot(verts[i], along));
            float wave = Mathf.Sin(t * Mathf.PI * 2f);
            delta[i] = across * wave * amp;
        }

        return delta;
    }

    private static bool IsCarving(string name)
    {
        return name.IndexOf("Carving", StringComparison.OrdinalIgnoreCase) >= 0;
    }

    private static BlendData LoadData()
    {
        TextAsset asset = Resources.Load<TextAsset>(ResourceName);
        if (asset == null || asset.bytes == null || asset.bytes.Length < 16)
        {
            Debug.LogWarning("MORPHIC: KerisBlendShapes.bytes is missing from Resources.");
            return null;
        }

        byte[] bytes = asset.bytes;
        int pos = 0;
        if (bytes.Length < 4 || bytes[0] != (byte)'K' || bytes[1] != (byte)'B' ||
            bytes[2] != (byte)'S' || bytes[3] != (byte)'1')
        {
            Debug.LogWarning("MORPHIC: KerisBlendShapes.bytes has a bad header.");
            return null;
        }

        pos = 4;
        int restCount = ReadInt(bytes, ref pos);
        var data = new BlendData();
        data.rest = new Vector3[restCount];
        for (int i = 0; i < restCount; i++)
            data.rest[i] = ReadVec(bytes, ref pos);

        int shapeCount = ReadInt(bytes, ref pos);
        data.shapes = new List<ShapeData>(shapeCount);
        for (int s = 0; s < shapeCount; s++)
        {
            int nameLen = ReadInt(bytes, ref pos);
            string name = System.Text.Encoding.UTF8.GetString(bytes, pos, nameLen);
            pos += nameLen;
            int indexCount = ReadInt(bytes, ref pos);
            int[] indexes = null;
            if (indexCount > 0)
            {
                indexes = new int[indexCount];
                for (int i = 0; i < indexCount; i++)
                    indexes[i] = ReadInt(bytes, ref pos);
            }

            int floatCount = ReadInt(bytes, ref pos);
            int vertCount = floatCount / 3;
            var verts = new Vector3[vertCount];
            for (int i = 0; i < vertCount; i++)
                verts[i] = ReadVec(bytes, ref pos);

            data.shapes.Add(new ShapeData
            {
                name = name,
                indexes = indexes,
                vertices = verts
            });
        }

        return data;
    }

    private static int ReadInt(byte[] bytes, ref int pos)
    {
        int v = BitConverter.ToInt32(bytes, pos);
        pos += 4;
        return v;
    }

    private static Vector3 ReadVec(byte[] bytes, ref int pos)
    {
        float x = BitConverter.ToSingle(bytes, pos); pos += 4;
        float y = BitConverter.ToSingle(bytes, pos); pos += 4;
        float z = BitConverter.ToSingle(bytes, pos); pos += 4;
        return new Vector3(x, y, z);
    }

    private sealed class BlendData
    {
        public Vector3[] rest;
        public List<ShapeData> shapes;
    }

    private sealed class ShapeData
    {
        public string name;
        public int[] indexes;
        public Vector3[] vertices;
    }

    private sealed class Frame
    {
        public string name;
        public float weight;
        public Vector3[] deltaVerts;
        public Vector3[] deltaNorms;
        public Vector3[] deltaTans;
    }
}
