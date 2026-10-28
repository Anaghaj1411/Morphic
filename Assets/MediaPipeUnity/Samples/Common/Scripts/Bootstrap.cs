// Copyright (c) 2021 homuler
//
// Use of this source code is governed by an MIT-style
// license that can be found in the LICENSE file or at
// https://opensource.org/licenses/MIT.

using System.Collections;
using UnityEngine;

namespace Mediapipe.Unity.Sample
{
  public class Bootstrap : MonoBehaviour
  {
    [SerializeField] private AppSettings _appSettings;

    public InferenceMode inferenceMode { get; private set; }
    public bool isFinished { get; private set; }
    private bool _isGlogInitialized;

    private void OnEnable()
    {
      var _ = StartCoroutine(Init());
    }

    private IEnumerator Init()
    {
      Debug.Log("The configuration for the sample app can be modified using AppSettings.asset.");
#if !DEBUG && !DEVELOPMENT_BUILD
      Debug.LogWarning("Logging for the MediaPipeUnityPlugin will be suppressed. To enable logging, please check the 'Development Build' option and build.");
#endif

      Logger.MinLogLevel = _appSettings.logLevel;

      Protobuf.SetLogHandler(Protobuf.DefaultLogHandler);

      Debug.Log("Setting global flags...");
      _appSettings.ResetGlogFlags();
      Glog.Initialize("MediaPipeUnityPlugin");
      _isGlogInitialized = true;

      Debug.Log("Initializing AssetLoader...");
      switch (_appSettings.assetLoaderType)
      {
        case AppSettings.AssetLoaderType.AssetBundle:
          {
            AssetLoader.Provide(new AssetBundleResourceManager("mediapipe"));
            break;
          }
        case AppSettings.AssetLoaderType.StreamingAssets:
          {
            AssetLoader.Provide(new StreamingAssetsResourceManager());
            break;
          }
        case AppSettings.AssetLoaderType.Local:
          {
#if UNITY_EDITOR
            AssetLoader.Provide(new LocalResourceManager());
            break;
#else
            Debug.LogError("LocalResourceManager is only supported on UnityEditor." +
              "To avoid this error, consider switching to the StreamingAssetsResourceManager and copying the required resources under StreamingAssets, for example.");
            yield break;
#endif
          }
        default:
          {
            Debug.LogError($"AssetLoaderType is unknown: {_appSettings.assetLoaderType}");
            yield break;
          }
      }

      DecideInferenceMode();
      if (inferenceMode == InferenceMode.GPU)
      {
        Debug.Log("Initializing GPU resources...");
        yield return GpuManager.Initialize();

        if (!GpuManager.IsInitialized)
        {
          Debug.LogWarning("GPU inference is unavailable, falling back to CPU.");
          inferenceMode = InferenceMode.CPU;
        }
      }

      Debug.Log("Preparing ImageSource...");
      ImageSourceProvider.Initialize(
        _appSettings.BuildWebCamSource(), _appSettings.BuildStaticImageSource(), _appSettings.BuildVideoSource());
      ImageSourceProvider.Switch(_appSettings.defaultImageSource);

      isFinished = true;
    }

    private void DecideInferenceMode()
    {
      // Desktop MediaPipe plugins ship CPU-only. GPU entry points exist on
      // Android/iOS OpenGL ES, not Windows/macOS/Linux players or editors.
#if UNITY_ANDROID || UNITY_IOS
      inferenceMode = _appSettings.preferableInferenceMode;
#else
      if (_appSettings.preferableInferenceMode == InferenceMode.GPU)
      {
        Debug.LogWarning("Current platform does not support GPU inference mode, so falling back to CPU mode");
      }
      inferenceMode = InferenceMode.CPU;
#endif
    }

    private void OnApplicationQuit()
    {
      GpuManager.Shutdown();

      if (_isGlogInitialized)
      {
        Glog.Shutdown();
      }

      Protobuf.ResetLogHandler();
    }
  }
}
