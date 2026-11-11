using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

public static class MorphicLisaVoiceMenu
{
    private const string ConfigPath = "Assets/Resources/MorphicLisaVoiceConfig.asset";

    [MenuItem("Morphic/Lisa Voice/Create Voice Config")]
    public static void CreateConfig()
    {
        MorphicLisaVoiceConfig config = LoadOrCreateConfig();
        Selection.activeObject = config;
        EditorGUIUtility.PingObject(config);
        Debug.Log("MORPHIC: Lisa voice config is at " + ConfigPath +
            ". Paste your ElevenLabs API key. Voice ID is already set to Ember (WtA85syCrJwasGeHGH2p).");
    }

    [MenuItem("Morphic/Lisa Voice/List Installed Windows Voices")]
    public static void ListInstalledVoices()
    {
        string[] names = MorphicLisaVoice.ListInstalledVoiceNames();
        if (names == null || names.Length == 0)
        {
            Debug.LogWarning(
                "MORPHIC: No Windows voices found. Install voices in " +
                "Settings > Time & language > Speech > Add voices.");
            return;
        }

        StringBuilder log = new StringBuilder();
        log.AppendLine("MORPHIC: Installed Windows voices (" + names.Length + "). Copy a name into MorphicLisaVoiceConfig:");
        for (int i = 0; i < names.Length; i++)
            log.AppendLine("  " + (i + 1) + ". " + names[i]);
        Debug.Log(log.ToString());
    }

    [MenuItem("Morphic/Lisa Voice/Open Voice Config")]
    public static void OpenConfig()
    {
        MorphicLisaVoiceConfig config = LoadOrCreateConfig();
        Selection.activeObject = config;
        EditorGUIUtility.PingObject(config);
    }

    private static MorphicLisaVoiceConfig LoadOrCreateConfig()
    {
        MorphicLisaVoiceConfig config =
            AssetDatabase.LoadAssetAtPath<MorphicLisaVoiceConfig>(ConfigPath);
        if (config != null)
        {
            if (string.IsNullOrWhiteSpace(config.elevenLabsVoiceId))
            {
                config.elevenLabsVoiceId = "WtA85syCrJwasGeHGH2p";
                config.elevenLabsModelId = "eleven_multilingual_v2";
                EditorUtility.SetDirty(config);
                AssetDatabase.SaveAssets();
            }

            return config;
        }

        string folder = Path.GetDirectoryName(ConfigPath);
        if (!AssetDatabase.IsValidFolder(folder))
            AssetDatabase.CreateFolder("Assets", "Resources");

        config = ScriptableObject.CreateInstance<MorphicLisaVoiceConfig>();
        config.elevenLabsVoiceId = "WtA85syCrJwasGeHGH2p";
        config.elevenLabsModelId = "eleven_multilingual_v2";
        AssetDatabase.CreateAsset(config, ConfigPath);
        AssetDatabase.SaveAssets();
        return config;
    }
}
