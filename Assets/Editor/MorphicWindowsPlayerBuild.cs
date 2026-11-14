using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class MorphicWindowsPlayerBuild
{
    private const string OutputExe = "MorphicPlay/Morphic.exe";
    private const string LabPath = "Assets/SculptingEnhancement/Scenes/SculptingLab.unity";

    private static string FlagFile
    {
        get
        {
            string project = Directory.GetParent(Application.dataPath).FullName;
            return Path.Combine(project, "Temp", "MorphicBuildNow.txt");
        }
    }

    [InitializeOnLoadMethod]
    private static void RunFlaggedBuild()
    {
        EditorApplication.update -= TickFlaggedBuild;
        EditorApplication.update += TickFlaggedBuild;
    }

    private static void TickFlaggedBuild()
    {
        if (EditorApplication.isCompiling ||
            EditorApplication.isUpdating ||
            BuildPipeline.isBuildingPlayer)
            return;

        if (!File.Exists(FlagFile))
        {
            EditorApplication.update -= TickFlaggedBuild;
            return;
        }

        EditorApplication.update -= TickFlaggedBuild;
        File.Delete(FlagFile);
        BuildClean();
    }

    [MenuItem("Morphic/Build Windows Player (Clean)")]
    public static void BuildClean()
    {
        AssetDatabase.ImportAsset(MorphicPlayerSceneFix.SpritePath, ImportAssetOptions.ForceUpdate);
        AssetDatabase.ImportAsset(LabPath, ImportAssetOptions.ForceUpdate);

        Scene lab = SceneManager.GetSceneByPath(LabPath);
        if (lab.IsValid() && lab.isLoaded)
        {
            MorphicPlayerSceneFix.StripEmbeddedUiSprites(lab);
            ClearSceneDirty(lab);
        }

        string project = Directory.GetParent(Application.dataPath).FullName;
        string outputPath = Path.Combine(project, OutputExe);
        string outputDir = Path.GetDirectoryName(outputPath);
        if (Directory.Exists(outputDir))
            Directory.Delete(outputDir, true);
        Directory.CreateDirectory(outputDir);

        var enabledScenes = EditorBuildSettings.scenes;
        var scenePaths = new System.Collections.Generic.List<string>();
        for (int i = 0; i < enabledScenes.Length; i++)
        {
            if (enabledScenes[i].enabled)
                scenePaths.Add(enabledScenes[i].path);
        }

        var options = new BuildPlayerOptions
        {
            scenes = scenePaths.ToArray(),
            locationPathName = outputPath,
            target = BuildTarget.StandaloneWindows64,
            options = BuildOptions.CleanBuildCache | BuildOptions.Development
        };

        Debug.Log("MORPHIC: Clean Windows player build -> " + outputPath); // rebuild after deleting old folders
        BuildReport report = BuildPipeline.BuildPlayer(options);
        if (report.summary.result != BuildResult.Succeeded)
        {
            throw new System.Exception(
                "MORPHIC: Windows player build failed: " + report.summary.result);
        }

        Debug.Log("MORPHIC: Windows player ready at " + outputPath);
    }

    private static void ClearSceneDirty(Scene scene)
    {
        var flags = System.Reflection.BindingFlags.Static |
                    System.Reflection.BindingFlags.NonPublic |
                    System.Reflection.BindingFlags.Public;
        var method = typeof(EditorSceneManager).GetMethod("ClearSceneDirtiness", flags);
        if (method != null)
            method.Invoke(null, new object[] { scene });
    }
}
