using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;

public static class MorphicCameraTools
{
    [MenuItem("Tools/MORPHIC/Capture Current Camera as Pottery View")]
    public static void CapturePotteryCamera()
    {
        // Find Main Camera
        Camera mainCamera = Camera.main;

        if (mainCamera == null)
        {
            Debug.LogError(
                "MORPHIC: Could not find the Main Camera. " +
                "Make sure your camera has the MainCamera tag.");
            return;
        }

        // Find pottery target
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

        // Copy EXACT position
        targetTransform.position =
            cameraTransform.position;

        // Copy EXACT rotation
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