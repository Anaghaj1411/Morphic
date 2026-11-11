using System;
using System.Collections;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using UnityEngine;
using UnityEngine.Networking;

public sealed class MorphicPiperTts : MonoBehaviour
{
    private const string PiperZipUrl =
        "https://github.com/rhasspy/piper/releases/download/2023.11.14-2/piper_windows_amd64.zip";
    private const string VoiceBase =
        "https://huggingface.co/rhasspy/piper-voices/resolve/main/";

    private string root;
    private string piperExe;
    private bool ready;
    private bool downloading;
    private string lastError;

    public bool IsReady => ready && File.Exists(piperExe);

    public IEnumerator EnsureReady(string languageCode)
    {
        if (downloading)
        {
            while (downloading)
                yield return null;
            yield break;
        }

        root = Path.Combine(Application.persistentDataPath, "Piper");
        Directory.CreateDirectory(root);
        piperExe = FindPiperExe();

        downloading = true;
        if (string.IsNullOrEmpty(piperExe) || !File.Exists(piperExe))
        {
            UnityEngine.Debug.Log("MORPHIC: Downloading Piper (one-time, free, offline after this).");
            yield return DownloadAndUnzip(PiperZipUrl, root);
            piperExe = FindPiperExe();
        }

        VoiceSpec spec = VoiceFor(languageCode);
        string onnx = Path.Combine(root, spec.FileName);
        string json = onnx + ".json";
        if (!File.Exists(onnx) || !File.Exists(json))
        {
            UnityEngine.Debug.Log("MORPHIC: Downloading Lisa Piper voice " + spec.FileName + ".");
            yield return DownloadFile(VoiceBase + spec.OnnxPath, onnx);
            yield return DownloadFile(VoiceBase + spec.OnnxPath + ".json", json);
        }

        SanitizeVoiceJson(json);

        ready = File.Exists(piperExe) && File.Exists(onnx) && File.Exists(json);
        downloading = false;
        if (ready)
            UnityEngine.Debug.Log("MORPHIC: Piper ready (" + spec.FileName + ").");
        else
            UnityEngine.Debug.LogWarning("MORPHIC: Piper download failed. " + lastError);
    }

    public bool TrySpeak(string text, string languageCode, out byte[] pcm, out int sampleRate)
    {
        pcm = null;
        sampleRate = 22050;
        VoiceSpec spec = VoiceFor(languageCode);
        string onnx = Path.Combine(root, spec.FileName);
        if (!IsReady || !File.Exists(onnx))
            return false;

        sampleRate = ReadSampleRate(onnx + ".json", 22050);
        try
        {
            pcm = RunPiper(text, onnx);
            return pcm != null && pcm.Length > 2;
        }
        catch (Exception exception)
        {
            lastError = exception.Message;
            UnityEngine.Debug.LogWarning("MORPHIC: Piper speak failed. " + exception.Message);
            return false;
        }
    }

    private byte[] RunPiper(string text, string onnx)
    {
        ProcessStartInfo start = new ProcessStartInfo
        {
            FileName = piperExe,
            Arguments = "--model \"" + onnx + "\" --output_raw",
            WorkingDirectory = Path.GetDirectoryName(piperExe),
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        using (Process process = new Process { StartInfo = start })
        {
            process.Start();
            string err = "";
            Thread errThread = new Thread(() =>
            {
                try
                {
                    err = process.StandardError.ReadToEnd();
                }
                catch
                {
                }
            });
            errThread.IsBackground = true;
            errThread.Start();

            byte[] input = new UTF8Encoding(false).GetBytes(text);
            process.StandardInput.BaseStream.Write(input, 0, input.Length);
            process.StandardInput.Close();

            byte[] pcm;
            using (MemoryStream buffer = new MemoryStream())
            {
                process.StandardOutput.BaseStream.CopyTo(buffer);
                pcm = buffer.ToArray();
            }

            process.WaitForExit(30000);
            errThread.Join(2000);
            if (pcm == null || pcm.Length < 4)
            {
                lastError = string.IsNullOrEmpty(err) ? "Piper produced no audio" : err.Trim();
                UnityEngine.Debug.LogWarning("MORPHIC: Piper produced no audio. " + lastError);
            }

            return pcm;
        }
    }

    private static void SanitizeVoiceJson(string jsonPath)
    {
        if (string.IsNullOrEmpty(jsonPath) || !File.Exists(jsonPath))
            return;

        string json = File.ReadAllText(jsonPath, Encoding.UTF8);
        int mapLabel = json.IndexOf("\"phoneme_id_map\"", StringComparison.Ordinal);
        if (mapLabel < 0)
            return;

        int brace = json.IndexOf('{', mapLabel);
        if (brace < 0)
            return;

        int end = FindMatchingBrace(json, brace);
        if (end < 0)
            return;

        string map = json.Substring(brace, end - brace + 1);
        string cleaned = Regex.Replace(
            map,
            "\"([^\"]+)\"\\s*:\\s*\\[\\s*\\d+(?:\\s*,\\s*\\d+)*\\s*\\]\\s*,?",
            match => CodePointCount(match.Groups[1].Value) > 1 ? "" : match.Value);
        cleaned = Regex.Replace(cleaned, ",\\s*}", "}");
        if (cleaned == map)
            return;

        File.WriteAllText(jsonPath, json.Substring(0, brace) + cleaned + json.Substring(end + 1), new UTF8Encoding(false));
        UnityEngine.Debug.Log("MORPHIC: Patched Piper voice file so Lisa can speak.");
    }

    private static int FindMatchingBrace(string text, int open)
    {
        int depth = 0;
        bool inString = false;
        for (int i = open; i < text.Length; i++)
        {
            char c = text[i];
            if (c == '"' && (i == 0 || text[i - 1] != '\\'))
                inString = !inString;
            else if (!inString)
            {
                if (c == '{')
                    depth++;
                else if (c == '}')
                {
                    depth--;
                    if (depth == 0)
                        return i;
                }
            }
        }

        return -1;
    }

    private static int CodePointCount(string value)
    {
        int count = 0;
        for (int i = 0; i < value.Length; i++)
        {
            count++;
            if (char.IsHighSurrogate(value[i]) && i + 1 < value.Length && char.IsLowSurrogate(value[i + 1]))
                i++;
        }

        return count;
    }

    private string FindPiperExe()
    {
        if (string.IsNullOrEmpty(root) || !Directory.Exists(root))
            return null;
        string[] matches = Directory.GetFiles(root, "piper.exe", SearchOption.AllDirectories);
        return matches != null && matches.Length > 0 ? matches[0] : null;
    }

    private IEnumerator DownloadAndUnzip(string url, string dest)
    {
        string zipPath = Path.Combine(dest, "piper.zip");
        yield return DownloadFile(url, zipPath);
        if (!File.Exists(zipPath))
            yield break;

        string unpack = Path.Combine(dest, "unpack");
        if (Directory.Exists(unpack))
            Directory.Delete(unpack, true);
        Directory.CreateDirectory(unpack);
        try
        {
            ZipFile.ExtractToDirectory(zipPath, unpack);
        }
        catch (Exception exception)
        {
            lastError = exception.Message;
            yield break;
        }

        string[] unpackedExe = Directory.GetFiles(unpack, "piper.exe", SearchOption.AllDirectories);
        if (unpackedExe.Length == 0)
        {
            lastError = "piper.exe missing from zip";
            yield break;
        }

        CopyDirectory(Path.GetDirectoryName(unpackedExe[0]), dest);

        try
        {
            File.Delete(zipPath);
            Directory.Delete(unpack, true);
        }
        catch
        {
        }
    }

    private IEnumerator DownloadFile(string url, string path)
    {
        using (UnityWebRequest request = UnityWebRequest.Get(url))
        {
            request.SetRequestHeader("User-Agent", "MorphicPiper/1.0");
            yield return request.SendWebRequest();
            if (request.result != UnityWebRequest.Result.Success)
            {
                lastError = request.error;
                yield break;
            }

            Directory.CreateDirectory(Path.GetDirectoryName(path));
            File.WriteAllBytes(path, request.downloadHandler.data);
        }
    }

    private static VoiceSpec VoiceFor(string code)
    {
        if (code == "zh-TW")
            return new VoiceSpec
            {
                FileName = "zh_CN-huayan-medium.onnx",
                OnnxPath = "zh/zh_CN/huayan/medium/zh_CN-huayan-medium.onnx"
            };
        if (code == "id")
            return new VoiceSpec
            {
                FileName = "id_ID-news_tts-medium.onnx",
                OnnxPath = "id/id_ID/news_tts/medium/id_ID-news_tts-medium.onnx"
            };
        return new VoiceSpec
        {
            FileName = "en_US-hfc_female-medium.onnx",
            OnnxPath = "en/en_US/hfc_female/medium/en_US-hfc_female-medium.onnx"
        };
    }

    private static int ReadSampleRate(string jsonPath, int fallback)
    {
        if (!File.Exists(jsonPath))
            return fallback;
        try
        {
            string json = File.ReadAllText(jsonPath);
            Match match = Regex.Match(json, "\"sample_rate\"\\s*:\\s*(\\d+)");
            if (match.Success)
                return int.Parse(match.Groups[1].Value);
        }
        catch
        {
        }

        return fallback;
    }

    private static string FindNamedDirectory(string rootDir, string name)
    {
        string[] dirs = Directory.GetDirectories(rootDir, name, SearchOption.AllDirectories);
        return dirs != null && dirs.Length > 0 ? dirs[0] : null;
    }

    private static void CopyDirectory(string source, string dest)
    {
        Directory.CreateDirectory(dest);
        foreach (string file in Directory.GetFiles(source, "*", SearchOption.AllDirectories))
        {
            string rel = file.Substring(source.Length).TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            string target = Path.Combine(dest, rel);
            Directory.CreateDirectory(Path.GetDirectoryName(target));
            File.Copy(file, target, true);
        }
    }

    private struct VoiceSpec
    {
        public string FileName;
        public string OnnxPath;
    }
}
