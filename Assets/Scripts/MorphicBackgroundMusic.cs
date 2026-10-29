using UnityEngine;

/// Quiet looping bed that starts with the first loaded scene and stays on.
/// Volume follows Settings → Music, scaled down so it stays in the background.
public sealed class MorphicBackgroundMusic : MonoBehaviour
{
    private const string MusicKey = "Morphic.MusicVolume";
    private const float DefaultMusic = 0.7f;
    private const float BedGain = 0.28f;

    private AudioSource source;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Install()
    {
        if (FindFirstObjectByType<MorphicBackgroundMusic>() != null)
            return;

        AudioClip clip = Resources.Load<AudioClip>("backtrack");
        if (clip == null)
            return;

        GameObject root = new GameObject("Morphic Background Music");
        DontDestroyOnLoad(root);

        MorphicBackgroundMusic player = root.AddComponent<MorphicBackgroundMusic>();
        player.StartTrack(clip);
    }

    public static void RefreshVolume()
    {
        MorphicBackgroundMusic player = FindFirstObjectByType<MorphicBackgroundMusic>();
        if (player != null)
            player.ApplyVolume();
    }

    private void StartTrack(AudioClip clip)
    {
        EnsureAudioListener();

        source = gameObject.AddComponent<AudioSource>();
        source.clip = clip;
        source.loop = true;
        source.playOnAwake = false;
        source.spatialBlend = 0f;
        source.priority = 200;
        source.ignoreListenerPause = true;
        ApplyVolume();
        source.Play();
    }

    private void ApplyVolume()
    {
        if (source == null)
            return;

        float music = Mathf.Clamp01(PlayerPrefs.GetFloat(MusicKey, DefaultMusic));
        source.volume = music * BedGain;
    }

    private static void EnsureAudioListener()
    {
        if (FindFirstObjectByType<AudioListener>() != null)
            return;

        Camera cam = Camera.main != null
            ? Camera.main
            : FindFirstObjectByType<Camera>();
        if (cam != null && cam.GetComponent<AudioListener>() == null)
            cam.gameObject.AddComponent<AudioListener>();
    }
}
