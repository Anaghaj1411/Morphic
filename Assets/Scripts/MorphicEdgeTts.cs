using System;
using System.Collections;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading;
using UnityEngine;

public sealed class MorphicEdgeTts : MonoBehaviour
{
    private readonly object gate = new object();
    private string pythonExe;
    private string pythonPrefix;
    private string cacheDir;
    private string workerPath;
    private Process worker;
    private Stream workerIn;
    private Stream workerOut;
    private bool ready;
    private bool checking;
    private string lastError;

    public bool IsReady => ready && worker != null && !worker.HasExited;

    public IEnumerator EnsureReady()
    {
        if (string.IsNullOrEmpty(cacheDir))
        {
            cacheDir = Path.Combine(Application.persistentDataPath, "EdgeTts");
            workerPath = Path.Combine(Application.streamingAssetsPath, "MorphicEdgeWorker.py");
        }

        if (IsReady)
            yield break;
        if (checking)
        {
            while (checking)
                yield return null;
            yield break;
        }

        checking = true;
        bool done = false;
        ThreadPool.QueueUserWorkItem(_ =>
        {
            try
            {
                ready = PreparePython() && StartWorker();
                if (!ready)
                {
                    UnityEngine.Debug.Log("MORPHIC: Installing Microsoft Edge TTS (one-time, free).");
                    string installErr;
                    Run(pythonExe, pythonPrefix + "-m pip install --user edge-tts", 120000, out installErr);
                    ready = PreparePython() && StartWorker();
                    if (!ready && !string.IsNullOrEmpty(installErr))
                        lastError = installErr;
                }
            }
            catch (Exception exception)
            {
                lastError = exception.Message;
                ready = false;
            }

            done = true;
        });
        while (!done)
            yield return null;
        checking = false;

        if (IsReady)
            UnityEngine.Debug.Log("MORPHIC: Microsoft Edge voice ready.");
        else
            UnityEngine.Debug.LogWarning("MORPHIC: Microsoft Edge voice not ready. " + lastError);
    }

    public bool TrySpeak(string text, string languageCode, out byte[] mp3)
    {
        mp3 = null;
        if (string.IsNullOrWhiteSpace(text))
            return false;

        lock (gate)
        {
            if (!IsReady && !(PreparePython() && StartWorker()))
                return false;

            mp3 = RequestMp3(text, VoiceFor(languageCode));
            if (mp3 == null || mp3.Length < 200)
            {
                StopWorker();
                if (!(PreparePython() && StartWorker()))
                    return false;
                mp3 = RequestMp3(text, VoiceFor(languageCode));
            }

            if (mp3 == null || mp3.Length < 200)
            {
                UnityEngine.Debug.LogWarning("MORPHIC: Edge TTS failed. " + lastError);
                mp3 = null;
                return false;
            }

            return true;
        }
    }

    private void OnDestroy()
    {
        lock (gate)
            StopWorker();
    }

    private bool PreparePython()
    {
        if (!FindPython(out pythonExe, out pythonPrefix))
        {
            lastError = "Python was not found. Edge voices need Python on this PC.";
            return false;
        }

        if (!File.Exists(workerPath))
        {
            lastError = "Missing MorphicEdgeWorker.py";
            return false;
        }

        return true;
    }

    private bool StartWorker()
    {
        if (worker != null && !worker.HasExited && workerIn != null && workerOut != null)
            return true;

        StopWorker();
        try
        {
            ProcessStartInfo start = new ProcessStartInfo
            {
                FileName = pythonExe,
                Arguments = pythonPrefix + "-u \"" + workerPath + "\"",
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardInput = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };
            worker = Process.Start(start);
            if (worker == null)
            {
                lastError = "could not start Edge voice worker";
                return false;
            }

            workerIn = worker.StandardInput.BaseStream;
            workerOut = worker.StandardOutput.BaseStream;
            try
            {
                workerOut.ReadTimeout = 60000;
            }
            catch
            {
            }
            Thread errThread = new Thread(() =>
            {
                try
                {
                    worker.StandardError.ReadToEnd();
                }
                catch
                {
                }
            });
            errThread.IsBackground = true;
            errThread.Start();

            string readyLine = ReadAsciiLine(workerOut, 60000);
            if (readyLine != "READY")
            {
                lastError = "Edge voice worker did not start";
                StopWorker();
                return false;
            }

            return true;
        }
        catch (Exception exception)
        {
            lastError = exception.Message;
            StopWorker();
            return false;
        }
    }

    private byte[] RequestMp3(string text, string voice)
    {
        try
        {
            string json = "{\"voice\":\"" + EscapeJson(voice) + "\",\"text\":\"" + EscapeJson(text) + "\"}\n";
            byte[] payload = Encoding.UTF8.GetBytes(json);
            workerIn.Write(payload, 0, payload.Length);
            workerIn.Flush();

            byte[] lenBuf = new byte[4];
            if (!ReadExact(workerOut, lenBuf, 4, 45000))
            {
                lastError = "Edge voice worker timed out";
                return null;
            }

            if (BitConverter.IsLittleEndian)
                Array.Reverse(lenBuf);
            int length = BitConverter.ToInt32(lenBuf, 0);
            if (length <= 0 || length > 20 * 1024 * 1024)
            {
                lastError = "Edge voice worker returned no audio";
                return null;
            }

            byte[] mp3 = new byte[length];
            if (!ReadExact(workerOut, mp3, length, 45000))
            {
                lastError = "Edge voice audio was cut off";
                return null;
            }

            return mp3;
        }
        catch (Exception exception)
        {
            lastError = exception.Message;
            return null;
        }
    }

    private void StopWorker()
    {
        try
        {
            if (workerIn != null && worker != null && !worker.HasExited)
            {
                byte[] quit = Encoding.UTF8.GetBytes("{\"cmd\":\"quit\"}\n");
                workerIn.Write(quit, 0, quit.Length);
                workerIn.Flush();
            }
        }
        catch
        {
        }

        try
        {
            if (worker != null && !worker.HasExited)
            {
                if (!worker.WaitForExit(500))
                    worker.Kill();
            }
        }
        catch
        {
        }

        worker = null;
        workerIn = null;
        workerOut = null;
        ready = false;
    }

    private static bool FindPython(out string exe, out string prefix)
    {
        exe = null;
        prefix = "";
        string windowsPy = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "py.exe");
        if (File.Exists(windowsPy))
        {
            exe = windowsPy;
            prefix = "-3 ";
            return true;
        }

        string path = Environment.GetEnvironmentVariable("PATH") ?? "";
        foreach (string folder in path.Split(Path.PathSeparator))
        {
            if (string.IsNullOrWhiteSpace(folder))
                continue;
            string candidate = Path.Combine(folder.Trim(), "python.exe");
            if (File.Exists(candidate))
            {
                exe = candidate;
                prefix = "";
                return true;
            }
        }

        return false;
    }

    private static string VoiceFor(string code)
    {
        if (code == "zh-TW")
            return "zh-TW-HsiaoChenNeural";
        if (code == "id")
            return "id-ID-GadisNeural";
        return "en-US-AvaNeural";
    }

    private static string EscapeJson(string value)
    {
        if (string.IsNullOrEmpty(value))
            return "";
        return value
            .Replace("\\", "\\\\")
            .Replace("\"", "\\\"")
            .Replace("\r", "\\r")
            .Replace("\n", "\\n")
            .Replace("\t", "\\t");
    }

    private static string ReadAsciiLine(Stream stream, int timeoutMs)
    {
        MemoryStream line = new MemoryStream();
        DateTime deadline = DateTime.UtcNow.AddMilliseconds(timeoutMs);
        while (DateTime.UtcNow < deadline)
        {
            int next = stream.ReadByte();
            if (next < 0)
                return Encoding.ASCII.GetString(line.ToArray()).Trim();
            if (next == '\n')
                return Encoding.ASCII.GetString(line.ToArray()).Trim();
            if (next != '\r')
                line.WriteByte((byte)next);
        }

        return "";
    }

    private static bool ReadExact(Stream stream, byte[] buffer, int count, int timeoutMs)
    {
        int got = 0;
        DateTime deadline = DateTime.UtcNow.AddMilliseconds(timeoutMs);
        while (got < count)
        {
            if (DateTime.UtcNow >= deadline)
                return false;
            int n = stream.Read(buffer, got, count - got);
            if (n <= 0)
                return false;
            got += n;
        }

        return true;
    }

    private static int Run(string fileName, string arguments, int timeoutMs, out string error)
    {
        error = "";
        try
        {
            ProcessStartInfo start = new ProcessStartInfo
            {
                FileName = fileName,
                Arguments = arguments,
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };
            using (Process process = Process.Start(start))
            {
                if (process == null)
                {
                    error = "could not start " + fileName;
                    return -1;
                }

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
                process.StandardOutput.ReadToEnd();
                if (!process.WaitForExit(timeoutMs))
                {
                    try
                    {
                        process.Kill();
                    }
                    catch
                    {
                    }

                    error = "timed out";
                    return -1;
                }

                errThread.Join(2000);
                error = err;
                return process.ExitCode;
            }
        }
        catch (Exception exception)
        {
            error = exception.Message;
            return -1;
        }
    }
}
