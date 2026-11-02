using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class FitUpdatedLantern
{
    private const string NewLanternName = "Updatedlantern";
    private const string OldLanternName = "TaiwanLantern_Test";
    private const string NewLanternAsset =
        "Assets/Art/Environments/MainMenu/Updatedlantern.fbx";

    [MenuItem("Tools/MORPHIC/Rewire Final Lantern Gestures")]
    public static void RewireFinalLantern()
    {
        GameObject root = FindInOpenScenes("FinalLantern");
        if (root == null)
        {
            Debug.LogError("MORPHIC: FinalLantern is not in the open scene.");
            return;
        }

        SkinnedMeshRenderer renderer = root.GetComponentInChildren<SkinnedMeshRenderer>(true);
        if (renderer == null)
        {
            Debug.LogError("MORPHIC: FinalLantern has no Skinned Mesh Renderer.");
            return;
        }

        RewireLanternReferences(root, renderer);
        EditorSceneManager.MarkSceneDirty(root.scene);
        Selection.activeGameObject = root;
        Debug.Log("MORPHIC: Hand gestures are wired to FinalLantern.");
    }

    [MenuItem("Tools/MORPHIC/Fit Updated Lantern")]
    public static void Fit()
    {
        GameObject oldRoot = FindInOpenScenes(OldLanternName);
        GameObject newRoot = FindInOpenScenes(NewLanternName);

        if (newRoot == null)
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(NewLanternAsset);
            if (prefab == null)
            {
                Debug.LogError("MORPHIC: Could not find Updatedlantern in the scene or at " + NewLanternAsset);
                return;
            }

            newRoot = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            newRoot.name = NewLanternName;
            Undo.RegisterCreatedObjectUndo(newRoot, "Instantiate Updated Lantern");
        }

        SkinnedMeshRenderer newRenderer = newRoot.GetComponentInChildren<SkinnedMeshRenderer>(true);
        if (newRenderer == null)
        {
            Debug.LogError("MORPHIC: Updatedlantern has no Skinned Mesh Renderer.");
            return;
        }

        Undo.RecordObject(newRoot.transform, "Fit Updated Lantern");

        // Drop the old 8,8,1.5 squash. This mesh is already Y-up and much larger.
        newRoot.transform.localScale = Vector3.one;
        newRoot.transform.rotation = RotationToStandLongAxisUp(newRenderer);

        SkinnedMeshRenderer oldRenderer = oldRoot != null
            ? oldRoot.GetComponentInChildren<SkinnedMeshRenderer>(true)
            : null;

        if (oldRenderer != null)
        {
            newRoot.transform.position = oldRoot.transform.position;

            Bounds oldBounds = oldRenderer.bounds;
            Bounds newBounds = newRenderer.bounds;
            float oldHeight = Mathf.Max(oldBounds.size.y, 0.01f);
            float newHeight = Mathf.Max(newBounds.size.y, 0.01f);
            float uniformScale = oldHeight / newHeight;
            newRoot.transform.localScale = Vector3.one * uniformScale;

            // Sit on the same table: match the bottom of the old lantern.
            newBounds = newRenderer.bounds;
            float yDelta = oldBounds.min.y - newBounds.min.y;
            Vector3 centerDelta = oldBounds.center - newBounds.center;
            newRoot.transform.position += new Vector3(centerDelta.x, yDelta, centerDelta.z);
        }
        else
        {
            newRoot.transform.position = new Vector3(-25.483f, 2.517f, -6.345f);
            newRoot.transform.localScale = Vector3.one * 0.025f;
        }

        newRoot.SetActive(true);
        if (oldRoot != null)
        {
            Undo.RecordObject(oldRoot, "Hide Old Lantern");
            oldRoot.SetActive(false);
        }

        RewireLanternReferences(newRoot, newRenderer);
        EditorSceneManager.MarkSceneDirty(newRoot.scene);
        Selection.activeGameObject = newRoot;
        if (SceneView.lastActiveSceneView != null)
            SceneView.lastActiveSceneView.FrameSelected();

        Debug.Log(
            "MORPHIC: Fitted Updatedlantern with uniform scale " +
            newRoot.transform.localScale.x.ToString("0.####") +
            ". Old lantern is hidden. Save the scene.");
    }

    private static Quaternion RotationToStandLongAxisUp(SkinnedMeshRenderer renderer)
    {
        Mesh mesh = renderer.sharedMesh;
        if (mesh == null)
            return Quaternion.identity;

        Vector3 size = mesh.bounds.size;
        if (size.y >= size.x && size.y >= size.z)
            return Quaternion.identity;
        if (size.z >= size.x && size.z >= size.y)
            return Quaternion.Euler(-90f, 0f, 0f);
        return Quaternion.Euler(0f, 0f, 90f);
    }

    private static void RewireLanternReferences(GameObject newRoot, SkinnedMeshRenderer newRenderer)
    {
        TaiwanLanternGestureController gesture =
            Object.FindFirstObjectByType<TaiwanLanternGestureController>(FindObjectsInactive.Include);
        if (gesture != null)
        {
            Undo.RecordObject(gesture, "Rewire Lantern");
            SerializedObject so = new SerializedObject(gesture);
            so.FindProperty("lanternRenderer").objectReferenceValue = newRenderer;
            so.ApplyModifiedProperties();
        }

        SculptingEnhancementIndexGestureController index =
            Object.FindFirstObjectByType<SculptingEnhancementIndexGestureController>(FindObjectsInactive.Include);
        if (index != null)
        {
            Undo.RecordObject(index, "Rewire Lantern");
            SerializedObject so = new SerializedObject(index);
            so.FindProperty("lanternRenderer").objectReferenceValue = newRenderer;
            so.ApplyModifiedProperties();
        }

        SculptingEnhancementLanternColorController color =
            Object.FindFirstObjectByType<SculptingEnhancementLanternColorController>(FindObjectsInactive.Include);
        if (color != null)
        {
            Undo.RecordObject(color, "Rewire Lantern");
            SerializedObject so = new SerializedObject(color);
            so.FindProperty("lanternRenderer").objectReferenceValue = newRenderer;
            so.ApplyModifiedProperties();
        }

        DialogueManager dialogue =
            Object.FindFirstObjectByType<DialogueManager>(FindObjectsInactive.Include);
        if (dialogue != null)
        {
            Undo.RecordObject(dialogue, "Rewire Lantern");
            SerializedObject so = new SerializedObject(dialogue);
            so.FindProperty("taiwanLantern").objectReferenceValue = newRoot;
            so.ApplyModifiedProperties();
        }
    }

    private static GameObject FindInOpenScenes(string objectName)
    {
        for (int i = 0; i < SceneManager.sceneCount; i++)
        {
            Scene scene = SceneManager.GetSceneAt(i);
            if (!scene.isLoaded)
                continue;

            GameObject[] roots = scene.GetRootGameObjects();
            for (int r = 0; r < roots.Length; r++)
            {
                Transform[] children = roots[r].GetComponentsInChildren<Transform>(true);
                for (int c = 0; c < children.Length; c++)
                {
                    if (children[c].name == objectName)
                        return children[c].gameObject;
                }
            }
        }

        return null;
    }

    [MenuItem("Tools/MORPHIC/Rebuild Keris Flowers")]
    public static void RebuildKerisFlowers()
    {
        GameObject keris = FindInOpenScenes("Keris_Updated");
        if (keris == null)
            keris = FindInOpenScenes("KerisSpin");
        if (keris == null)
        {
            EditorUtility.DisplayDialog(
                "MORPHIC",
                "No Keris_Updated in the open scene.",
                "OK");
            return;
        }

        SkinnedMeshRenderer skin = keris.GetComponentInChildren<SkinnedMeshRenderer>(true);
        if (skin == null)
        {
            MeshFilter filter = keris.GetComponentInChildren<MeshFilter>(true);
            MeshRenderer meshRenderer = keris.GetComponentInChildren<MeshRenderer>(true);
            if (filter == null || filter.sharedMesh == null)
            {
                EditorUtility.DisplayDialog(
                    "MORPHIC",
                    "Keris has no mesh renderer.",
                    "OK");
                return;
            }

            skin = filter.gameObject.AddComponent<SkinnedMeshRenderer>();
            skin.sharedMesh = filter.sharedMesh;
            if (meshRenderer != null)
            {
                skin.sharedMaterials = meshRenderer.sharedMaterials;
                meshRenderer.enabled = false;
            }
        }

        Undo.RecordObject(skin, "Rebuild Keris Flowers");
        Mesh original = LoadOriginalKerisMesh();
        if (original != null)
            skin.sharedMesh = original;
        KerisFlowerOverlay overlay = KerisFlowerOverlay.Ensure(skin);
        if (overlay != null)
        {
            overlay.RebuildNow();
            EditorUtility.SetDirty(overlay);
        }

        EditorUtility.SetDirty(skin);
        EditorSceneManager.MarkSceneDirty(keris.scene);
        Transform flowers = skin.transform.Find("KerisFlowers");
        Selection.activeGameObject = flowers != null ? flowers.gameObject : skin.gameObject;
        if (SceneView.lastActiveSceneView != null)
            SceneView.lastActiveSceneView.FrameSelected();
        EditorUtility.DisplayDialog(
            "MORPHIC",
            "Old Carving 2.001 no longer moves the Keris mesh.\nDrag Carving 2.001 or Flower Amount to grow the new coded flowers.",
            "OK");
    }

    private static Mesh LoadOriginalKerisMesh()
    {
        const string path = "Assets/Art/Environments/MainMenu/Keris_Updated.fbx";
        UnityEngine.Object[] assets = AssetDatabase.LoadAllAssetsAtPath(path);
        Mesh best = null;
        int bestShapes = -1;
        for (int i = 0; i < assets.Length; i++)
        {
            Mesh mesh = assets[i] as Mesh;
            if (mesh == null)
                continue;
            if (mesh.blendShapeCount >= bestShapes)
            {
                best = mesh;
                bestShapes = mesh.blendShapeCount;
            }
        }

        return best;
    }

    private static int PushKerisFlowersOut(Mesh mesh)
    {
        Vector3[] verts = mesh.vertices;
        Vector3[] norms = mesh.normals;
        int vCount = verts.Length;
        Vector3[] flowerDelta = new Vector3[vCount];
        Vector3[] zeros = new Vector3[vCount];
        System.Collections.Generic.HashSet<int> flower = CollectOnlyFlowerVerts(mesh, verts);
        if (flower.Count < 12)
        {
            Debug.LogWarning("MORPHIC: Could not isolate Keris flower verts.");
            return 0;
        }

        Vector3 center = mesh.bounds.center;
        Vector3 flowerCenter = Vector3.zero;
        foreach (int i in flower)
            flowerCenter += verts[i];
        flowerCenter /= flower.Count;

        float cluster = 0f;
        foreach (int i in flower)
            cluster = Mathf.Max(cluster, (verts[i] - flowerCenter).magnitude);
        float distance = Mathf.Clamp(cluster * 0.35f, 0.05f, mesh.bounds.extents.magnitude * 0.12f);

        int moved = 0;
        foreach (int i in flower)
        {
            Vector3 n = i < norms.Length ? norms[i] : Vector3.zero;
            if (n.sqrMagnitude < 0.0001f)
                n = verts[i] - flowerCenter;
            n.Normalize();
            if (Vector3.Dot(n, verts[i] - center) < 0f)
                n = -n;
            flowerDelta[i] = n * distance;
            moved++;
        }

        System.Collections.Generic.List<string> names =
            new System.Collections.Generic.List<string>();
        System.Collections.Generic.List<Vector3[]> frames =
            new System.Collections.Generic.List<Vector3[]>();
        bool replaced = false;
        for (int s = 0; s < mesh.blendShapeCount; s++)
        {
            string name = mesh.GetBlendShapeName(s);
            Vector3[] dv = new Vector3[vCount];
            Vector3[] dn = new Vector3[vCount];
            Vector3[] dt = new Vector3[vCount];
            if (mesh.GetBlendShapeFrameCount(s) > 0)
                mesh.GetBlendShapeFrameVertices(s, 0, dv, dn, dt);
            if (name.IndexOf("Carving 2.001", System.StringComparison.OrdinalIgnoreCase) >= 0)
            {
                dv = flowerDelta;
                replaced = true;
            }
            names.Add(name);
            frames.Add(dv);
        }

        if (!replaced)
        {
            names.Add("Carving 2.001");
            frames.Add(flowerDelta);
        }

        mesh.ClearBlendShapes();
        for (int i = 0; i < names.Count; i++)
            mesh.AddBlendShapeFrame(names[i], 100f, frames[i], zeros, zeros);
        mesh.RecalculateBounds();
        return moved;
    }

    private static System.Collections.Generic.HashSet<int> CollectOnlyFlowerVerts(
        Mesh mesh,
        Vector3[] verts)
    {
        var set = new System.Collections.Generic.HashSet<int>();
        int subCount = mesh.subMeshCount;
        if (subCount > 1)
        {
            int best = -1;
            int bestDiff = int.MaxValue;
            int largestFaces = 0;
            for (int sm = 0; sm < subCount; sm++)
            {
                int faces = mesh.GetTriangles(sm).Length / 3;
                if (faces > largestFaces)
                    largestFaces = faces;
                int diff = Mathf.Abs(faces - 1647);
                if (diff < bestDiff)
                {
                    bestDiff = diff;
                    best = sm;
                }
            }

            if (best >= 0)
            {
                int faces = mesh.GetTriangles(best).Length / 3;
                if (faces < largestFaces && faces >= 80)
                {
                    int[] tris = mesh.GetTriangles(best);
                    for (int i = 0; i < tris.Length; i++)
                        set.Add(tris[i]);
                    if (set.Count > 0 && set.Count <= verts.Length / 3)
                        return set;
                }
            }
        }

        set.Clear();
        TryAddFlowerVertsFromBlendData(mesh, verts, set);
        return set;
    }

    private static void TryAddFlowerVertsFromBlendData(
        Mesh mesh,
        Vector3[] unityVerts,
        System.Collections.Generic.HashSet<int> set)
    {
        TextAsset asset = AssetDatabase.LoadAssetAtPath<TextAsset>(
            "Assets/Resources/KerisBlendShapes.bytes");
        if (asset == null || asset.bytes == null || asset.bytes.Length < 16)
            return;

        byte[] bytes = asset.bytes;
        if (bytes[0] != (byte)'K' || bytes[3] != (byte)'1')
            return;

        int pos = 4;
        int restCount = System.BitConverter.ToInt32(bytes, pos); pos += 4;
        Vector3[] rest = new Vector3[restCount];
        for (int i = 0; i < restCount; i++)
        {
            rest[i] = new Vector3(
                System.BitConverter.ToSingle(bytes, pos),
                System.BitConverter.ToSingle(bytes, pos + 4),
                System.BitConverter.ToSingle(bytes, pos + 8));
            pos += 12;
        }

        int shapeCount = System.BitConverter.ToInt32(bytes, pos); pos += 4;
        int[] flowerIdx = null;
        for (int s = 0; s < shapeCount; s++)
        {
            int nameLen = System.BitConverter.ToInt32(bytes, pos); pos += 4;
            string name = System.Text.Encoding.UTF8.GetString(bytes, pos, nameLen);
            pos += nameLen;
            int indexCount = System.BitConverter.ToInt32(bytes, pos); pos += 4;
            int[] indexes = new int[indexCount];
            for (int i = 0; i < indexCount; i++)
            {
                indexes[i] = System.BitConverter.ToInt32(bytes, pos);
                pos += 4;
            }
            int floatCount = System.BitConverter.ToInt32(bytes, pos); pos += 4;
            pos += floatCount * 4;
            if (name.IndexOf("Carving 2.001", System.StringComparison.OrdinalIgnoreCase) >= 0)
                flowerIdx = indexes;
        }

        if (flowerIdx == null || flowerIdx.Length == 0)
            return;

        Bounds restBounds = new Bounds(rest[0], Vector3.zero);
        for (int i = 1; i < rest.Length; i++)
            restBounds.Encapsulate(rest[i]);
        Bounds unityBounds = mesh.bounds;
        Vector3 scale = new Vector3(
            unityBounds.size.x / Mathf.Max(restBounds.size.x, 0.0001f),
            unityBounds.size.y / Mathf.Max(restBounds.size.y, 0.0001f),
            unityBounds.size.z / Mathf.Max(restBounds.size.z, 0.0001f));

        float maxDist = unityBounds.extents.magnitude * 0.05f;
        float maxSqr = maxDist * maxDist;
        for (int k = 0; k < flowerIdx.Length; k++)
        {
            int f = flowerIdx[k];
            if (f < 0 || f >= rest.Length)
                continue;
            Vector3 t = Vector3.Scale(rest[f] - restBounds.center, scale) + unityBounds.center;
            for (int u = 0; u < unityVerts.Length; u++)
            {
                if ((unityVerts[u] - t).sqrMagnitude <= maxSqr)
                    set.Add(u);
            }
        }
    }
}
