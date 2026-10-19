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
        AssetDatabase.SaveAssets();
        Debug.Log("MORPHIC: Bound Cubee.0049-0059 as the lantern shelf.");
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
