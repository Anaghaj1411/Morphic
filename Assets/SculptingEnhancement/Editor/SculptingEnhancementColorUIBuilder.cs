using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class SculptingEnhancementColorUIBuilder
{
    private const string ScenePath =
        "Assets/SculptingEnhancement/Scenes/SculptingLab.unity";

    [InitializeOnLoadMethod]
    private static void ScheduleHierarchyCheck()
    {
        EditorApplication.delayCall += EnsureSceneHierarchy;
    }

    private static void EnsureSceneHierarchy()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            return;

        Scene scene = SceneManager.GetSceneByPath(ScenePath);
        bool openedForBuild = !scene.IsValid() || !scene.isLoaded;
        if (openedForBuild)
            scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive);

        try
        {
            SculptingEnhancementLanternColorController controller =
                FindController(scene);
            if (controller == null || HasCompleteHierarchy(controller))
                return;

            controller.RebuildSceneInterfaceForEditor();
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            Debug.Log(
                "MORPHIC: Added the lantern color UI to the SculptingLab Canvas Hierarchy.");
        }
        finally
        {
            if (openedForBuild && scene.IsValid() && scene.isLoaded)
                EditorSceneManager.CloseScene(scene, true);
        }
    }

    [MenuItem("Morphic/Rebuild Sculpting Lab Color UI")]
    public static void Rebuild()
    {
        Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        SculptingEnhancementLanternColorController controller = FindController(scene);

        if (controller == null)
            throw new MissingComponentException(
                "SculptingEnhancementLanternColorController is missing from SculptingLab.");

        controller.RebuildSceneInterfaceForEditor();
        EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();
        Debug.Log("MORPHIC: Saved the lantern color UI under the SculptingLab Canvas.");
    }

    private static SculptingEnhancementLanternColorController FindController(Scene scene)
    {
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            SculptingEnhancementLanternColorController controller =
                root.GetComponentInChildren<SculptingEnhancementLanternColorController>(true);
            if (controller != null)
                return controller;
        }

        return null;
    }

    private static bool HasCompleteHierarchy(
        SculptingEnhancementLanternColorController controller)
    {
        Canvas canvas = null;
        foreach (GameObject root in controller.gameObject.scene.GetRootGameObjects())
        {
            canvas = root.GetComponentInChildren<Canvas>(true);
            if (canvas != null)
                break;
        }

        Transform uiRoot = canvas != null
            ? canvas.transform.Find("SculptingEnhancementColorUI")
            : null;
        return uiRoot != null &&
               uiRoot.Find("LanternColorPanel/MorphicGoalsV1") != null &&
               uiRoot.Find("LanternColorPanel/MorphicShelfDoneV1") != null &&
               uiRoot.Find("LanternColorPanel/HistoryBar/Undo") != null &&
               uiRoot.Find("LanternColorPanel/HistoryBar/Reset") != null &&
               uiRoot.Find("LanternColorPanel/ConfirmReset/Cancel") != null &&
               uiRoot.Find("LanternColorPanel/ConfirmReset/Reset") != null &&
               uiRoot.Find("LanternColorPanel/MainActions/SelectPart") != null &&
               uiRoot.Find("LanternColorPanel/MainActions/ChangeColor") != null &&
               uiRoot.Find("LanternColorPanel/MainActions/Finish") != null &&
               uiRoot.Find("LanternColorPanel/Review/Save") != null &&
               uiRoot.Find("LanternColorPanel/Review/Retake") != null &&
               uiRoot.Find("LanternColorPanel/Review/Finish") != null &&
               uiRoot.Find("LanternColorPanel/Review/SavedStatus") != null &&
               uiRoot.Find("LanternColorPanel/ShelfActions/TakeBack") != null &&
               uiRoot.Find("LanternColorPanel/ShelfActions/Delete") != null &&
               uiRoot.Find("LanternColorPanel/ShelfActions/Done") != null &&
               uiRoot.Find("LanternColorPanel/ConfirmDelete/Cancel") != null &&
               uiRoot.Find("LanternColorPanel/ConfirmDelete/Delete") != null &&
               uiRoot.Find("SelfieEntry") != null &&
               uiRoot.Find("LanternColorPanel/PartSelection/Part4") != null &&
               uiRoot.Find("LanternColorPanel/PartSelection/Done") != null &&
               uiRoot.Find("LanternColorPanel/ColorSelection/Color7") != null &&
               uiRoot.Find("LanternColorPanel/ColorSelection/Color7/SelectionCheck") != null &&
               uiRoot.Find("LanternColorPanel/ColorSelection/FreeColorPalette") != null &&
               uiRoot.Find("LanternColorPanel/ColorSelection/Back") != null &&
               uiRoot.Find("LanternColorPanel/ColorSelection/Done") != null &&
               uiRoot.Find("LanternSelfiePanel/Content/Preview") != null &&
               uiRoot.Find("LanternSelfiePanel/Content/TakeSelfie") != null &&
               uiRoot.Find("LanternSelfiePanel/Content/RemoveBackground") != null &&
               uiRoot.Find("LanternSelfiePanel/Content/AttachToLantern") != null &&
               uiRoot.Find("LanternSelfiePanel/Content/Retake") != null &&
               uiRoot.Find("LanternSelfiePanel/Content/Done") != null &&
               uiRoot.Find("HandColorCursor/CursorDot") != null;
    }
}
