using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;

[InitializeOnLoad]
public static class MorphicCameraTools
{
    static MorphicCameraTools()
    {
        ClearForcedPlayScene();
        EditorApplication.delayCall += ClearForcedPlayScene;
        EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
    }

    private static void OnPlayModeStateChanged(PlayModeStateChange state)
    {
        if (state == PlayModeStateChange.ExitingEditMode)
            ClearForcedPlayScene();
    }

    private static void ClearForcedPlayScene()
    {
        if (EditorSceneManager.playModeStartScene != null)
            EditorSceneManager.playModeStartScene = null;
    }

    [MenuItem("Tools/MORPHIC/Capture Current Camera as Pottery View")]
    public static void CapturePotteryCamera()
    {
        Camera mainCamera = Camera.main;

        if (mainCamera == null)
        {
            Debug.LogError(
                "MORPHIC: Could not find the Main Camera. " +
                "Make sure your camera has the MainCamera tag.");
            return;
        }

        GameObject targetObject =
            GameObject.Find("PotteryStudioCameraTarget");

        if (targetObject == null)
        {
            Debug.LogError(
                "MORPHIC: Could not find PotteryStudioCameraTarget.");
            return;
        }

        Transform cameraTransform =
            mainCamera.transform;

        Transform targetTransform =
            targetObject.transform;

        Undo.RecordObject(
            targetTransform,
            "Capture Pottery Camera View");

        targetTransform.position =
            cameraTransform.position;

        targetTransform.rotation =
            cameraTransform.rotation;

        EditorUtility.SetDirty(targetTransform);

        EditorSceneManager.MarkSceneDirty(
            targetObject.scene);

        Debug.Log(
            "MORPHIC: Pottery camera view captured successfully!\n" +
            "Position: " + targetTransform.position +
            "\nRotation: " + targetTransform.eulerAngles);
    }
}
