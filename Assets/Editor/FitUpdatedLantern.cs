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
}
