using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Player builds crash on SculptingLab when hundreds of RoundedRect
/// Texture2D blobs are serialized into level0. Retarget UI images to
/// one project sprite and drop the scene-local copies before bake.
/// </summary>
public class MorphicPlayerSceneFix : IProcessSceneWithReport
{
    public const string SpritePath = "Assets/Resources/MorphicUI/RoundedRect.png";

    public int callbackOrder => 1000;

    public void OnProcessScene(Scene scene, BuildReport report)
    {
        if (!scene.IsValid() || !scene.isLoaded)
            return;

        int replaced = StripEmbeddedUiSprites(scene);
        if (replaced > 0)
            Debug.Log("MORPHIC: Stripped " + replaced + " embedded UI sprites from " + scene.path);
    }

    public static Sprite LoadSharedSprite()
    {
        Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(SpritePath);
        if (sprite == null)
            sprite = Resources.Load<Sprite>("MorphicUI/RoundedRect");
        if (sprite == null)
            sprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Background.psd");
        return sprite;
    }

    public static int StripEmbeddedUiSprites(Scene scene)
    {
        Sprite shared = LoadSharedSprite();
        if (shared == null)
            return 0;

        HashSet<Object> drop = new HashSet<Object>();
        int replaced = 0;

        foreach (GameObject root in scene.GetRootGameObjects())
        {
            Image[] images = root.GetComponentsInChildren<Image>(true);
            for (int i = 0; i < images.Length; i++)
            {
                Image image = images[i];
                if (CollectIfEmbedded(image.sprite, drop))
                {
                    image.sprite = shared;
                    replaced++;
                }

                if (CollectIfEmbedded(image.overrideSprite, drop))
                {
                    image.overrideSprite = shared;
                    replaced++;
                }
            }

            RawImage[] rawImages = root.GetComponentsInChildren<RawImage>(true);
            for (int i = 0; i < rawImages.Length; i++)
            {
                RawImage raw = rawImages[i];
                Texture2D texture = raw.texture as Texture2D;
                if (!IsEmbedded(texture))
                    continue;
                drop.Add(texture);
                raw.texture = shared.texture;
                replaced++;
            }

            SpriteRenderer[] renderers = root.GetComponentsInChildren<SpriteRenderer>(true);
            for (int i = 0; i < renderers.Length; i++)
            {
                SpriteRenderer renderer = renderers[i];
                if (!CollectIfEmbedded(renderer.sprite, drop))
                    continue;
                renderer.sprite = shared;
                replaced++;
            }
        }

        foreach (Object asset in drop)
        {
            if (asset == null)
                continue;
            asset.hideFlags = HideFlags.HideAndDontSave;
            Object.DestroyImmediate(asset, true);
        }

        return replaced;
    }

    private static bool CollectIfEmbedded(Sprite sprite, HashSet<Object> drop)
    {
        if (!IsEmbedded(sprite))
            return false;

        drop.Add(sprite);
        Texture2D texture = sprite.texture;
        if (IsEmbedded(texture))
            drop.Add(texture);
        return true;
    }

    private static bool IsEmbedded(Object asset)
    {
        if (asset == null)
            return false;
        string path = AssetDatabase.GetAssetPath(asset);
        if (string.IsNullOrEmpty(path))
            return true;
        return path.EndsWith(".unity", System.StringComparison.OrdinalIgnoreCase);
    }
}
