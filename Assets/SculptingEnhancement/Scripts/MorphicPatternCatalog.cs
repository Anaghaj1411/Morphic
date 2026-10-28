using System;
using UnityEngine;

/// <summary>
/// Keeps stamp and band textures referenced so Unity includes them in player builds.
/// Editor AssetDatabase.FindAssets does not run in the exe.
/// </summary>
public sealed class MorphicPatternCatalog : ScriptableObject
{
    [SerializeField] private Texture2D[] stamps;
    [SerializeField] private Texture2D[] bands;

    private static MorphicPatternCatalog instance;

    public static Texture2D[] Stamps()
    {
        return CopySorted(Load().stamps, "StampPatterns");
    }

    public static Texture2D[] Bands()
    {
        return CopySorted(Load().bands, "BandPatterns");
    }

    private static MorphicPatternCatalog Load()
    {
        if (instance == null)
            instance = Resources.Load<MorphicPatternCatalog>("MorphicPatternCatalog");
        if (instance == null)
            instance = CreateInstance<MorphicPatternCatalog>();
        return instance;
    }

    private static Texture2D[] CopySorted(Texture2D[] serialized, string resourceFolder)
    {
        Texture2D[] loaded = Filter(serialized);
        if (loaded.Length == 0)
            loaded = Filter(Resources.LoadAll<Texture2D>(resourceFolder));

#if UNITY_EDITOR
        if (loaded.Length == 0)
            loaded = LoadFromEditorFolder(
                resourceFolder == "StampPatterns"
                    ? "Assets/SculptingEnhancement/Art/StampPatterns"
                    : "Assets/SculptingEnhancement/Art/BandPatterns");
#endif

        Array.Sort(loaded, (a, b) => string.CompareOrdinal(a.name, b.name));
        return loaded;
    }

    private static Texture2D[] Filter(Texture2D[] source)
    {
        if (source == null || source.Length == 0)
            return Array.Empty<Texture2D>();

        int count = 0;
        for (int i = 0; i < source.Length; i++)
        {
            if (source[i] != null)
                count++;
        }

        if (count == 0)
            return Array.Empty<Texture2D>();

        Texture2D[] result = new Texture2D[count];
        int write = 0;
        for (int i = 0; i < source.Length; i++)
        {
            if (source[i] != null)
                result[write++] = source[i];
        }

        return result;
    }

#if UNITY_EDITOR
    private static Texture2D[] LoadFromEditorFolder(string folder)
    {
        string[] guids = UnityEditor.AssetDatabase.FindAssets("t:Texture2D", new[] { folder });
        var loaded = new System.Collections.Generic.List<Texture2D>(guids.Length);
        for (int i = 0; i < guids.Length; i++)
        {
            string path = UnityEditor.AssetDatabase.GUIDToAssetPath(guids[i]);
            Texture2D texture = UnityEditor.AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            if (texture != null)
                loaded.Add(texture);
        }

        return loaded.ToArray();
    }
#endif
}
