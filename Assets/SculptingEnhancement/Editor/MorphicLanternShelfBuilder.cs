using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class MorphicLanternShelfBuilder
{
    private static readonly string[] ScenePaths =
    {
        "Assets/SculptingEnhancement/Scenes/SculptingLab.unity",
        "Assets/Scenes/MainMenu.unity"
    };

    [InitializeOnLoadMethod]
    private static void ScheduleBind()
    {
        EditorApplication.delayCall += BindOpenScenes;
    }

    [MenuItem("Morphic/Bind Lantern Shelf")]
    public static void BindFromMenu()
    {
        BindOpenScenes();
        CreateSlotsInOpenScenes();
        AssetDatabase.SaveAssets();
        Debug.Log("MORPHIC: Bound Cubee.0049-0059 as the lantern shelf.");
    }

    [MenuItem("Morphic/Create Lantern Shelf Slots")]
    public static void CreateSlotsFromMenu()
    {
        CreateSlotsInOpenScenes();
        AssetDatabase.SaveAssets();
        Debug.Log("MORPHIC: Created LanternShelfSlot through LanternShelfSlot_9. Drag each empty in the Scene view.");
    }

    [MenuItem("Morphic/Create Teapot Shelf Slots")]
    public static void CreateTeapotSlotsFromMenu()
    {
        CreateTeapotSlotsInOpenScenes();
        AssetDatabase.SaveAssets();
        Debug.Log("MORPHIC: Created TeapotShelfSlot through TeapotShelfSlot_9. Drag those empties. Leave LanternShelfSlot alone.");
    }

    private static void CreateSlotsInOpenScenes()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            return;

        for (int i = 0; i < SceneManager.sceneCount; i++)
        {
            Scene scene = SceneManager.GetSceneAt(i);
            if (!scene.isLoaded || !IsTrackedScene(scene.path))
                continue;

            LanternShelfController controller = FindController(scene);
            if (controller == null)
                continue;

            controller.CreateManualSlots();
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
        }
    }

    private static void CreateTeapotSlotsInOpenScenes()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            return;

        for (int i = 0; i < SceneManager.sceneCount; i++)
        {
            Scene scene = SceneManager.GetSceneAt(i);
            if (!scene.isLoaded || !IsTrackedScene(scene.path))
                continue;

            LanternShelfController controller = FindController(scene);
            Transform parent = controller != null ? controller.transform : null;
            if (parent == null)
            {
                GameObject existing = GameObject.Find("LanternShelf");
                parent = existing != null ? existing.transform : null;
            }

            if (parent == null)
                continue;

            const string teapotSlot = "TeapotShelfSlot";
            bool created = false;
            for (int slotIndex = 0; slotIndex < 9; slotIndex++)
            {
                string slotName = slotIndex == 0 ? teapotSlot : teapotSlot + "_" + (slotIndex + 1);
                Transform existingSlot = FindChildByName(parent, slotName);
                if (existingSlot != null)
                    continue;

                string lanternName = slotIndex == 0
                    ? LanternShelfController.SlotName
                    : LanternShelfController.SlotName + "_" + (slotIndex + 1);
                Transform lanternSlot = FindChildByName(parent, lanternName);

                GameObject slotObject = new GameObject(slotName);
                created = true;
                Transform slot = slotObject.transform;
                slot.SetParent(parent, true);
                if (lanternSlot != null)
                {
                    slot.position = lanternSlot.position;
                    slot.rotation = lanternSlot.rotation;
                }
                else
                {
                    slot.position = parent.position + Vector3.right * (0.45f * slotIndex);
                    slot.rotation = Quaternion.identity;
                }
            }

            if (!created)
                continue;

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
        }
    }

    private static Transform FindChildByName(Transform root, string objectName)
    {
        if (root == null)
            return null;
        Transform[] children = root.GetComponentsInChildren<Transform>(true);
        for (int i = 0; i < children.Length; i++)
        {
            if (children[i] != null && children[i].name == objectName)
                return children[i];
        }

        return null;
    }

    private static void BindOpenScenes()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            return;

        for (int i = 0; i < SceneManager.sceneCount; i++)
        {
            Scene scene = SceneManager.GetSceneAt(i);
            if (scene.isLoaded)
                BindScene(scene);
        }
    }

    private static void BindScene(Scene scene)
    {
        if (!IsTrackedScene(scene.path))
            return;

        LanternShelfController controller = FindController(scene);
        if (controller != null && controller.BoundPieceCount > 0 && controller.Slot != null)
            return;

        if (LanternShelfController.FindShelfPieces().Count == 0)
            return;

        if (controller == null)
        {
            GameObject root = new GameObject(LanternShelfController.ShelfRootName);
            SceneManager.MoveGameObjectToScene(root, scene);
            controller = root.AddComponent<LanternShelfController>();
        }

        controller.BindPieces();
        if (controller.BoundPieceCount == 0)
            return;

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
    }

    private static bool IsTrackedScene(string path)
    {
        for (int i = 0; i < ScenePaths.Length; i++)
        {
            if (path == ScenePaths[i])
                return true;
        }

        return false;
    }

    private static LanternShelfController FindController(Scene scene)
    {
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            LanternShelfController controller =
                root.GetComponentInChildren<LanternShelfController>(true);
            if (controller != null)
                return controller;
        }

        return null;
    }
}
