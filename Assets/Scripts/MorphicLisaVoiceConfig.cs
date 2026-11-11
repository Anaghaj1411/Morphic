using UnityEngine;

[CreateAssetMenu(fileName = "MorphicLisaVoiceConfig", menuName = "Morphic/Lisa Voice Config")]
public sealed class MorphicLisaVoiceConfig : ScriptableObject
{
    [Header("ElevenLabs (Lisa)")]
    [Tooltip("Paste your ElevenLabs API key. Lisa stays silent without it.")]
    public string elevenLabsApiKey;

    [Tooltip("Voice from elevenlabs.io/voices/WtA85syCrJwasGeHGH2p (Ember).")]
    public string elevenLabsVoiceId = "WtA85syCrJwasGeHGH2p";

    public string elevenLabsModelId = "eleven_multilingual_v2";

    [Header("Windows fallback (optional)")]
    [Tooltip("Used only if the ElevenLabs key is empty.")]
    public string englishVoiceName;

    public string taiwanVoiceName;
    public string indonesianVoiceName;

    public bool HasElevenLabs
    {
        get
        {
            return !string.IsNullOrWhiteSpace(elevenLabsApiKey) &&
                   !string.IsNullOrWhiteSpace(elevenLabsVoiceId);
        }
    }

    public string PreferredNameFor(string code)
    {
        if (code == "zh-TW")
            return taiwanVoiceName;
        if (code == "id")
            return indonesianVoiceName;
        return englishVoiceName;
    }
}
