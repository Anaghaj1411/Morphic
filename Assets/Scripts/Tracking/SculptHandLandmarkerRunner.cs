using System.Collections;
using Mediapipe.Tasks.Vision.HandLandmarker;
using UnityEngine;
using UnityEngine.Rendering;

namespace Mediapipe.Unity.Sample.HandLandmarkDetection
{
    public readonly struct SculptHandFrame
    {
        public readonly Vector3 Wrist;
        public readonly Vector3 ThumbTip;
        public readonly Vector3 IndexTip;

        public SculptHandFrame(
            Vector3 wrist,
            Vector3 thumbTip,
            Vector3 indexTip
        )
        {
            Wrist = wrist;
            ThumbTip = thumbTip;
            IndexTip = indexTip;
        }

        public float PinchDistance =>
            Vector3.Distance(ThumbTip, IndexTip);

        public float IndexToWristDistance =>
            Vector3.Distance(IndexTip, Wrist);
    }

    public class SculptHandLandmarkerRunner :
        VisionTaskApiRunner<HandLandmarker>
    {
        private const int WristIndex = 0;
        private const int ThumbTipIndex = 4;
        private const int IndexTipIndex = 8;

        [SerializeField]
        private HandLandmarkerResultAnnotationController
            annotationController;

        private Experimental.TextureFramePool textureFramePool;

        private SculptHandFrame latestHand;
        private SculptHandFrame secondHand;

        private bool hasLatestHand;
        private bool hasSecondHand;

        private float lastResultTime;

        public readonly HandLandmarkDetectionConfig config =
            new HandLandmarkDetectionConfig();

        private void Awake()
        {
            config.RunningMode =
                Tasks.Vision.Core.RunningMode.VIDEO;

            config.NumHands = 2;

            config.MinHandDetectionConfidence = 0.35f;
            config.MinHandPresenceConfidence = 0.35f;
            config.MinTrackingConfidence = 0.35f;

            screen = FindFirstObjectByType<Screen>();
        }

        public bool TryGetLatestHand(
            out SculptHandFrame hand
        )
        {
            hand = latestHand;

            return hasLatestHand &&
                   Time.unscaledTime - lastResultTime < 0.25f;
        }

        public bool TryGetLatestTwoHands(
            out SculptHandFrame firstHand,
            out SculptHandFrame otherHand
        )
        {
            firstHand = latestHand;
            otherHand = secondHand;

            return hasLatestHand &&
                   hasSecondHand &&
                   Time.unscaledTime - lastResultTime < 0.25f;
        }

        public override void Stop()
        {
            base.Stop();

            textureFramePool?.Dispose();
            textureFramePool = null;

            hasLatestHand = false;
            hasSecondHand = false;
        }

        protected override IEnumerator Run()
        {
            yield return AssetLoader.PrepareAssetAsync(
                config.ModelPath
            );

            var options = config.GetHandLandmarkerOptions();

            taskApi = HandLandmarker.CreateFromOptions(
                options,
                GpuManager.GpuResources
            );

            var imageSource = ImageSourceProvider.ImageSource;

            yield return imageSource.Play();

            if (!imageSource.isPrepared)
            {
                Debug.LogError("Could not start the webcam.");
                yield break;
            }

            textureFramePool =
                new Experimental.TextureFramePool(
                    imageSource.textureWidth,
                    imageSource.textureHeight,
                    TextureFormat.RGBA32,
                    10
                );

            if (annotationController == null)
            {
                annotationController =
                    FindFirstObjectByType<
                        HandLandmarkerResultAnnotationController
                    >();
            }

            if (annotationController == null)
            {
                Debug.LogError(
                    "Could not find the hand skeleton display."
                );

                yield break;
            }

            if (screen == null)
            {
                Debug.LogError(
                    "Could not find the webcam display."
                );

                yield break;
            }

            screen.Initialize(imageSource);

            SetupAnnotationController(
                annotationController,
                imageSource
            );

            var transformOptions =
                imageSource.GetTransformationOptions();

            var imageOptions =
                new Tasks.Vision.Core.ImageProcessingOptions(
                    rotationDegrees:
                        (int)transformOptions.rotationAngle
                );

            var waitForEndOfFrame = new WaitForEndOfFrame();

            var result = HandLandmarkerResult.Alloc(
                options.numHands
            );

            while (true)
            {
                if (isPaused)
                {
                    yield return new WaitWhile(
                        () => isPaused
                    );
                }

                if (!textureFramePool.TryGetTextureFrame(
                    out var textureFrame
                ))
                {
                    yield return waitForEndOfFrame;
                    continue;
                }

                yield return waitForEndOfFrame;

                textureFrame.ReadTextureOnCPU(
                    imageSource.GetCurrentTexture(),
                    transformOptions.flipHorizontally,
                    transformOptions.flipVertically
                );

                var image = textureFrame.BuildCPUImage();

                textureFrame.Release();

                if (taskApi.TryDetectForVideo(
                    image,
                    GetCurrentTimestampMillisec(),
                    imageOptions,
                    ref result
                ))
                {
                    SaveLatestHands(result);
                    annotationController.DrawNow(result);
                }
                else
                {
                    annotationController.DrawNow(default);
                }
            }
        }

        private void SaveLatestHands(
            HandLandmarkerResult result
        )
        {
            if (result.handLandmarks == null ||
                result.handLandmarks.Count == 0)
            {
                hasLatestHand = false;
                hasSecondHand = false;
                return;
            }

            var firstLandmarks =
                result.handLandmarks[0].landmarks;

            if (firstLandmarks == null ||
                firstLandmarks.Count <= IndexTipIndex)
            {
                hasLatestHand = false;
                hasSecondHand = false;
                return;
            }

            latestHand = new SculptHandFrame(
                ToUnityPoint(firstLandmarks[WristIndex]),
                ToUnityPoint(firstLandmarks[ThumbTipIndex]),
                ToUnityPoint(firstLandmarks[IndexTipIndex])
            );

            hasLatestHand = true;
            hasSecondHand = false;

            if (result.handLandmarks.Count > 1)
            {
                var otherLandmarks =
                    result.handLandmarks[1].landmarks;

                if (otherLandmarks != null &&
                    otherLandmarks.Count > IndexTipIndex)
                {
                    secondHand = new SculptHandFrame(
                        ToUnityPoint(otherLandmarks[WristIndex]),
                        ToUnityPoint(otherLandmarks[ThumbTipIndex]),
                        ToUnityPoint(otherLandmarks[IndexTipIndex])
                    );

                    hasSecondHand = true;
                }
            }

            lastResultTime = Time.unscaledTime;
        }

        private static Vector3 ToUnityPoint(
            Mediapipe.Tasks.Components.Containers
                .NormalizedLandmark landmark
        )
        {
            return new Vector3(
                landmark.x,
                1f - landmark.y,
                landmark.z
            );
        }
    }
}