using System;
using System.Collections;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using UnityEngine;
using UnityEngine.Networking;

/// Speaks Lisa's lines with Windows text-to-speech in the current
/// Morphic language. No per-line audio files. Install the matching
/// Windows speech voices (English, Chinese Traditional Taiwan, Indonesian).
public sealed class MorphicLisaVoice : MonoBehaviour
{
    private const int SpeakAsyncPurge = 3;
    private const string EnglishLcids = "|409|809|C09|1009|1409|1809|1C09|2009|2409|2809|2C09|3009|3409|";
    private const string TaiwanLcids = "|404|";
    private const string IndonesianLcids = "|421|";

    private const int PcmSampleRate = 24000;
    private const string ElevenLabsVoiceId = "WtA85syCrJwasGeHGH2p";

    private static MorphicLisaVoice instance;
    private MorphicLisaVoiceConfig config;
    private MorphicPiperTts piper;
    private MorphicEdgeTts edge;
    private AudioSource audio;
    private Coroutine speakRoutine;
    private int speakTicket;
    private object voice;
    private string boundCode;
    private bool warnedMissing;
    private bool warnedNoKey;

    private static readonly string[] WarmLines =
    {
        "Hi! I'm Lisa. Nice to meet you!",
        "It's wonderful to finally be back in Yuanlin. I opened a pottery studio here so I can work with clay every day.",
        "We're in Yuanlin, my hometown. This pottery studio is my new home for making art with clay."
    };

    private readonly System.Collections.Generic.Dictionary<string, AudioClip> clipCache =
        new System.Collections.Generic.Dictionary<string, AudioClip>();
    private readonly System.Collections.Generic.HashSet<string> prefetching =
        new System.Collections.Generic.HashSet<string>();
    private int mp3Tick;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Boot()
    {
        if (instance != null)
            return;
        GameObject root = new GameObject("MorphicLisaVoice");
        DontDestroyOnLoad(root);
        root.AddComponent<MorphicLisaVoice>();
    }

    public static void SpeakLine(string english)
    {
        if (instance == null)
            return;
        instance.SpeakText(MorphicLoc.T(english), false);
    }

    public static void SpeakReply(string displayed)
    {
        if (instance == null)
            return;
        instance.SpeakText(displayed, true);
    }

    public static void Stop()
    {
        if (instance != null)
            instance.StopSpeaking();
    }

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }

        instance = this;
        config = Resources.Load<MorphicLisaVoiceConfig>("MorphicLisaVoiceConfig");
        audio = gameObject.GetComponent<AudioSource>();
        if (audio == null)
            audio = gameObject.AddComponent<AudioSource>();
        audio.playOnAwake = false;
        audio.spatialBlend = 0f;
        audio.volume = 1f;
        piper = gameObject.GetComponent<MorphicPiperTts>();
        if (piper == null)
            piper = gameObject.AddComponent<MorphicPiperTts>();
        edge = gameObject.GetComponent<MorphicEdgeTts>();
        if (edge == null)
            edge = gameObject.AddComponent<MorphicEdgeTts>();
        MorphicLoc.Changed -= OnLanguageChanged;
        MorphicLoc.Changed += OnLanguageChanged;
        Debug.Log("MORPHIC: Lisa voice started (Microsoft Edge).");
        StartCoroutine(WarmVoice());
    }

    private void OnDestroy()
    {
        MorphicLoc.Changed -= OnLanguageChanged;
        if (instance == this)
            instance = null;
        ClearClipCache();
        ReleaseVoice();
    }

    private void OnLanguageChanged()
    {
        boundCode = null;
        warnedMissing = false;
        EnsureVoice();
        ClearClipCache();
        if (isActiveAndEnabled)
            StartCoroutine(WarmVoice());
    }

    private IEnumerator WarmVoice()
    {
        if (edge == null)
            yield break;
        yield return edge.EnsureReady();
        for (int i = 0; i < WarmLines.Length; i++)
            yield return PrefetchLine(MorphicLoc.T(WarmLines[i]));
    }

    private IEnumerator PrefetchLine(string text)
    {
        if (string.IsNullOrWhiteSpace(text) || clipCache.ContainsKey(text) || prefetching.Contains(text))
            yield break;

        prefetching.Add(text);
        byte[] mp3 = null;
        bool done = false;
        ThreadPool.QueueUserWorkItem(_ =>
        {
            try
            {
                edge.TrySpeak(text, MorphicLoc.Code, out mp3);
            }
            catch
            {
            }

            done = true;
        });
        while (!done)
            yield return null;

        if (mp3 != null && mp3.Length > 200)
        {
            AudioClip clip = null;
            yield return DecodeMp3(mp3, decoded => clip = decoded);
            if (clip != null)
                clipCache[text] = clip;
        }

        prefetching.Remove(text);
    }

    private IEnumerator DecodeMp3(byte[] mp3, System.Action<AudioClip> done)
    {
        string folder = Path.Combine(Application.persistentDataPath, "EdgeTts");
        Directory.CreateDirectory(folder);
        string path = Path.Combine(folder, "lisa-" + (++mp3Tick) + ".mp3");
        File.WriteAllBytes(path, mp3);
        string url = new Uri(path).AbsoluteUri;
        using (UnityWebRequest request = UnityWebRequestMultimedia.GetAudioClip(url, AudioType.MPEG))
        {
            DownloadHandlerAudioClip handler = request.downloadHandler as DownloadHandlerAudioClip;
            if (handler != null)
                handler.compressed = false;
            yield return request.SendWebRequest();
            AudioClip clip = null;
            if (request.result == UnityWebRequest.Result.Success)
                clip = DownloadHandlerAudioClip.GetContent(request);
            done(clip);
        }

        try
        {
            File.Delete(path);
        }
        catch
        {
        }
    }

    private void ClearClipCache()
    {
        foreach (AudioClip clip in clipCache.Values)
        {
            if (clip != null)
                Destroy(clip);
        }

        clipCache.Clear();
        prefetching.Clear();
    }

    private void SpeakText(string text, bool isReply)
    {
        if (string.IsNullOrWhiteSpace(text))
            return;

        text = StripLisaPrefix(text);
        if (string.IsNullOrWhiteSpace(text))
            return;
        if (isReply && IsStatusLine(text))
            return;

        speakTicket++;
        if (speakRoutine != null)
            StopCoroutine(speakRoutine);
        speakRoutine = StartCoroutine(SpeakEdgeThenFallback(text, speakTicket));
    }

    private void StopSpeaking()
    {
        speakTicket++;
        if (speakRoutine != null)
        {
            StopCoroutine(speakRoutine);
            speakRoutine = null;
        }

        if (audio != null)
            audio.Stop();

        if (voice == null)
            return;
        try
        {
            voice.GetType().InvokeMember(
                "Speak",
                BindingFlags.InvokeMethod,
                null,
                voice,
                new object[] { string.Empty, SpeakAsyncPurge });
        }
        catch
        {
        }
    }

    private IEnumerator SpeakEdgeThenFallback(string text, int ticket)
    {
        AudioClip cached;
        if (clipCache.TryGetValue(text, out cached) && cached != null && audio != null)
        {
            audio.clip = cached;
            audio.Play();
            speakRoutine = null;
            yield break;
        }

        while (prefetching.Contains(text) && !clipCache.ContainsKey(text))
            yield return null;
        if (ticket != speakTicket)
            yield break;
        if (clipCache.TryGetValue(text, out cached) && cached != null && audio != null)
        {
            audio.clip = cached;
            audio.Play();
            speakRoutine = null;
            yield break;
        }

        if (edge != null)
        {
            yield return edge.EnsureReady();
            if (ticket != speakTicket)
                yield break;

            byte[] mp3 = null;
            bool edgeDone = false;
            ThreadPool.QueueUserWorkItem(_ =>
            {
                try
                {
                    edge.TrySpeak(text, MorphicLoc.Code, out mp3);
                }
                catch (Exception exception)
                {
                    Debug.LogWarning("MORPHIC: Edge voice failed. " + exception.Message, this);
                }

                edgeDone = true;
            });
            while (!edgeDone)
                yield return null;

            if (ticket != speakTicket)
                yield break;
            if (mp3 != null && mp3.Length > 200 && audio != null)
            {
                AudioClip clip = null;
                yield return DecodeMp3(mp3, decoded => clip = decoded);
                if (ticket != speakTicket)
                    yield break;
                if (clip != null)
                {
                    clipCache[text] = clip;
                    audio.clip = clip;
                    audio.Play();
                    speakRoutine = null;
                    yield break;
                }
            }
        }

        if (piper != null)
        {
            yield return piper.EnsureReady(MorphicLoc.Code);
            if (ticket != speakTicket)
                yield break;

            byte[] pcm = null;
            int sampleRate = 22050;
            bool done = false;
            ThreadPool.QueueUserWorkItem(_ =>
            {
                try
                {
                    piper.TrySpeak(text, MorphicLoc.Code, out pcm, out sampleRate);
                }
                catch (Exception exception)
                {
                    Debug.LogWarning("MORPHIC: Piper failed. " + exception.Message, this);
                }

                done = true;
            });
            while (!done)
                yield return null;

            if (ticket != speakTicket)
                yield break;
            if (pcm != null && pcm.Length > 2 && audio != null)
            {
                AudioClip clip = PcmToClip(pcm, sampleRate);
                if (clip != null)
                {
                    audio.clip = clip;
                    audio.Play();
                    speakRoutine = null;
                    yield break;
                }
            }

            Debug.LogWarning("MORPHIC: Piper did not return speech audio.");
        }

        SpeakWindows(text);
        speakRoutine = null;
    }

    private IEnumerator SpeakElevenLabs(string text, int ticket)
    {
        string voiceId = string.IsNullOrWhiteSpace(config.elevenLabsVoiceId)
            ? ElevenLabsVoiceId
            : config.elevenLabsVoiceId.Trim();
        string model = string.IsNullOrWhiteSpace(config.elevenLabsModelId)
            ? "eleven_multilingual_v2"
            : config.elevenLabsModelId.Trim();
        string url =
            "https://api.elevenlabs.io/v1/text-to-speech/" +
            UnityWebRequest.EscapeURL(voiceId) +
            "?output_format=pcm_24000";

        ElevenRequest body = new ElevenRequest
        {
            text = text,
            model_id = model,
            language_code = ElevenLanguageCode(),
            voice_settings = new ElevenVoiceSettings()
        };
        byte[] payload = Encoding.UTF8.GetBytes(JsonUtility.ToJson(body));

        using (UnityWebRequest request = new UnityWebRequest(url, UnityWebRequest.kHttpVerbPOST))
        {
            request.uploadHandler = new UploadHandlerRaw(payload);
            request.downloadHandler = new DownloadHandlerBuffer();
            request.SetRequestHeader("Content-Type", "application/json");
            request.SetRequestHeader("Accept", "application/octet-stream");
            request.SetRequestHeader("xi-api-key", config.elevenLabsApiKey.Trim());
            yield return request.SendWebRequest();

            if (ticket != speakTicket)
                yield break;

            if (request.result != UnityWebRequest.Result.Success)
            {
                if (request.responseCode == 402)
                    Debug.LogWarning(
                        "MORPHIC: Ember on ElevenLabs needs a paid plan. " +
                        "Free accounts cannot use this voice through the API. " +
                        "Lisa will use Windows speech until you upgrade at elevenlabs.io/app/subscription.",
                        this);
                else
                    Debug.LogWarning(
                        "MORPHIC: ElevenLabs voice failed (" + request.responseCode + "): " +
                        request.error + " " + request.downloadHandler.text,
                        this);

                SpeakWindows(text);
                speakRoutine = null;
                yield break;
            }

            AudioClip clip = PcmToClip(request.downloadHandler.data);
            if (clip == null || audio == null)
                yield break;

            audio.clip = clip;
            audio.Play();
        }

        speakRoutine = null;
    }

    private void SpeakWindows(string text)
    {
        if (!warnedNoKey && (config == null || !config.HasElevenLabs))
        {
            warnedNoKey = true;
            Debug.LogWarning(
                "MORPHIC: Paste your ElevenLabs API key into MorphicLisaVoiceConfig " +
                "(Morphic > Lisa Voice > Open Voice Config). Using Windows TTS until then.",
                this);
        }

        EnsureVoice();
        if (voice == null)
            return;

        try
        {
            voice.GetType().InvokeMember(
                "Speak",
                BindingFlags.InvokeMethod,
                null,
                voice,
                new object[] { text, SpeakAsyncPurge });
        }
        catch (Exception exception)
        {
            Debug.LogWarning("MORPHIC: Lisa voice could not speak. " + exception.Message, this);
        }
    }

    private static string ElevenLanguageCode()
    {
        if (MorphicLoc.Code == "zh-TW")
            return "zh";
        if (MorphicLoc.Code == "id")
            return "id";
        return "en";
    }

    private static AudioClip PcmToClip(byte[] pcm)
    {
        return PcmToClip(pcm, PcmSampleRate);
    }

    private static AudioClip PcmToClip(byte[] pcm, int sampleRate)
    {
        if (pcm == null || pcm.Length < 4)
            return null;
        if (sampleRate < 8000)
            sampleRate = 22050;

        int samples = pcm.Length / 2;
        float[] data = new float[samples];
        for (int i = 0; i < samples; i++)
            data[i] = BitConverter.ToInt16(pcm, i * 2) / 32768f;

        AudioClip clip = AudioClip.Create("LisaPiper", samples, 1, sampleRate, false);
        clip.SetData(data, 0);
        return clip;
    }

    [Serializable]
    private class ElevenRequest
    {
        public string text;
        public string model_id;
        public string language_code;
        public ElevenVoiceSettings voice_settings;
    }

    [Serializable]
    private class ElevenVoiceSettings
    {
        public float stability = 0.42f;
        public float similarity_boost = 0.78f;
        public float style = 0.28f;
        public bool use_speaker_boost = true;
    }

    private void EnsureVoice()
    {
        if (voice != null && boundCode == MorphicLoc.Code)
            return;

        ReleaseVoice();
        boundCode = MorphicLoc.Code;

#if UNITY_EDITOR_WIN || UNITY_STANDALONE_WIN
        try
        {
            Type voiceType = Type.GetTypeFromProgID("SAPI.SpVoice");
            if (voiceType == null)
            {
                WarnMissing("Windows SAPI");
                return;
            }

            voice = Activator.CreateInstance(voiceType);
            object token = FindVoiceToken(voice, MorphicLoc.Code, PreferredName());
            if (token == null)
            {
                ReleaseVoice();
                WarnMissing(VoiceNeed(MorphicLoc.Code));
                return;
            }

            voiceType.InvokeMember(
                "Voice",
                BindingFlags.SetProperty,
                null,
                voice,
                new object[] { token });
            voiceType.InvokeMember(
                "Rate",
                BindingFlags.SetProperty,
                null,
                voice,
                new object[] { -1 });
            voiceType.InvokeMember(
                "Volume",
                BindingFlags.SetProperty,
                null,
                voice,
                new object[] { 90 });
            Debug.Log("MORPHIC: Lisa voice ready for " + MorphicLoc.Code + ".");
        }
        catch (Exception exception)
        {
            ReleaseVoice();
            Debug.LogWarning("MORPHIC: Lisa voice failed to start. " + exception.Message, this);
        }
#else
        WarnMissing("Windows");
#endif
    }

    public static string[] ListInstalledVoiceNames()
    {
#if UNITY_EDITOR_WIN || UNITY_STANDALONE_WIN
        try
        {
            Type voiceType = Type.GetTypeFromProgID("SAPI.SpVoice");
            if (voiceType == null)
                return Array.Empty<string>();
            object sapi = Activator.CreateInstance(voiceType);
            object tokens = sapi.GetType().InvokeMember(
                "GetVoices",
                BindingFlags.InvokeMethod,
                null,
                sapi,
                null);
            int count = TokenCount(tokens);
            string[] names = new string[count];
            for (int i = 0; i < count; i++)
            {
                object token = TokenAt(tokens, i);
                names[i] = GetTokenDescription(token);
            }

            Marshal.ReleaseComObject(sapi);
            return names;
        }
        catch
        {
            return Array.Empty<string>();
        }
#else
        return Array.Empty<string>();
#endif
    }

    private string PreferredName()
    {
        return config != null ? config.PreferredNameFor(MorphicLoc.Code) : string.Empty;
    }

    private object FindVoiceToken(object sapi, string code, string preferredName)
    {
        object tokens = sapi.GetType().InvokeMember(
            "GetVoices",
            BindingFlags.InvokeMethod,
            null,
            sapi,
            null);
        if (tokens == null)
            return null;

        int count = TokenCount(tokens);
        object languageMatch = null;
        string want = (preferredName ?? string.Empty).Trim();
        for (int i = 0; i < count; i++)
        {
            object token = TokenAt(tokens, i);
            if (token == null)
                continue;

            string name = GetTokenDescription(token);
            if (!string.IsNullOrEmpty(want) &&
                name.IndexOf(want, StringComparison.OrdinalIgnoreCase) >= 0)
                return token;

            string language = GetTokenAttribute(token, "Language");
            if (languageMatch == null && VoiceMatches(code, language, name))
                languageMatch = token;
        }

        if (!string.IsNullOrEmpty(want))
            return null;
        return languageMatch;
    }

    private static int TokenCount(object tokens)
    {
        if (tokens == null)
            return 0;
        return Convert.ToInt32(
            tokens.GetType().InvokeMember(
                "Count",
                BindingFlags.GetProperty,
                null,
                tokens,
                null),
            CultureInfo.InvariantCulture);
    }

    private static object TokenAt(object tokens, int index)
    {
        return tokens.GetType().InvokeMember(
            "Item",
            BindingFlags.GetProperty,
            null,
            tokens,
            new object[] { index });
    }

    private static bool VoiceMatches(string code, string language, string name)
    {
        string raw = (language ?? string.Empty).Trim().ToUpperInvariant();
        int cut = raw.IndexOf(';');
        if (cut >= 0)
            raw = raw.Substring(0, cut);
        string lcid = "|" + raw + "|";
        string lower = (name ?? string.Empty).ToLowerInvariant();
        if (code == "zh-TW")
        {
            return TaiwanLcids.Contains(lcid) ||
                   lower.Contains("hanhan") ||
                   lower.Contains("taiwan") ||
                   (lower.Contains("chinese") &&
                    (lower.Contains("traditional") || lower.Contains("taiwan")));
        }

        if (code == "id")
        {
            return IndonesianLcids.Contains(lcid) ||
                   lower.Contains("gadis") ||
                   lower.Contains("indonesia");
        }

        return EnglishLcids.Contains(lcid) ||
               lower.Contains("zira") ||
               lower.Contains("aria") ||
               lower.Contains("jenny") ||
               lower.Contains("guy") ||
               lower.Contains("david") ||
               lower.Contains("hazel") ||
               lower.Contains("english");
    }

    private static string GetTokenAttribute(object token, string name)
    {
        try
        {
            object value = token.GetType().InvokeMember(
                "GetAttribute",
                BindingFlags.InvokeMethod,
                null,
                token,
                new object[] { name });
            return value != null ? value.ToString() : string.Empty;
        }
        catch
        {
            return string.Empty;
        }
    }

    private static string GetTokenDescription(object token)
    {
        try
        {
            object value = token.GetType().InvokeMember(
                "GetDescription",
                BindingFlags.InvokeMethod,
                null,
                token,
                null);
            return value != null ? value.ToString() : string.Empty;
        }
        catch
        {
            return string.Empty;
        }
    }

    private static string StripLisaPrefix(string text)
    {
        text = text.Trim();
        if (text.StartsWith("LISA:", StringComparison.OrdinalIgnoreCase))
            return text.Substring(5).Trim();
        if (text.StartsWith("LISA：", StringComparison.OrdinalIgnoreCase))
            return text.Substring(5).Trim();
        return text;
    }

    private static bool IsStatusLine(string text)
    {
        string key = MorphicLoc.EnglishKey(text);
        return key.IndexOf("Asking", StringComparison.OrdinalIgnoreCase) >= 0;
    }

    private static string VoiceNeed(string code)
    {
        if (code == "zh-TW")
            return "Microsoft Hanhan (Chinese Traditional, Taiwan)";
        if (code == "id")
            return "Microsoft Gadis (Indonesian)";
        return "Microsoft Aria / Zira (English)";
    }

    private void WarnMissing(string need)
    {
        if (warnedMissing)
            return;
        warnedMissing = true;
        Debug.LogWarning(
            "MORPHIC: Lisa has no voice for " + MorphicLoc.Code +
            ". Install or pick " + need +
            ". Use Morphic > Lisa Voice > List Installed Windows Voices, " +
            "then paste the name into MorphicLisaVoiceConfig.",
            this);
    }

    private void ReleaseVoice()
    {
        if (voice == null)
            return;
        try
        {
            Marshal.ReleaseComObject(voice);
        }
        catch
        {
        }

        voice = null;
    }
}
