using System.Collections;
using Mediapipe.Tasks.Vision.HandLandmarker;
using UnityEngine;
using UnityEngine.Rendering;

namespace Mediapipe.Unity.Sample.HandLandmarkDetection
{
    public readonly struct SculptHandFrame
    {
        public readonly Vector3 Wrist;
        public readonly Vector3 PalmCenter;
        public readonly Vector3 ThumbTip;
        public readonly Vector3 IndexTip;
        public readonly Vector3 MiddleTip;
        public readonly Vector3 RingTip;
        public readonly Vector3 PinkyTip;

        private readonly float palmWidth;

        public float PalmWidth => palmWidth;

        public SculptHandFrame(
            Vector3 wrist,
            Vector3 palmCenter,
            Vector3 thumbTip,
            Vector3 indexTip,
            Vector3 middleTip,
            Vector3 ringTip,
            Vector3 pinkyTip,
            float handPalmWidth
        )
        {
            Wrist = wrist;
            PalmCenter = palmCenter;
            ThumbTip = thumbTip;
            IndexTip = indexTip;
            MiddleTip = middleTip;
            RingTip = ringTip;
            PinkyTip = pinkyTip;

            palmWidth = Mathf.Max(handPalmWidth, 0.001f);
        }

        public float PinchDistance =>
            Vector2.Distance(
                new Vector2(ThumbTip.x, ThumbTip.y),
                new Vector2(IndexTip.x, IndexTip.y)
            );

        public float IndexToWristDistance =>
            Vector2.Distance(
                new Vector2(IndexTip.x, IndexTip.y),
                new Vector2(Wrist.x, Wrist.y)
            );

        public float FingerCurlRatio
        {
            get
            {
                float indexDistance = DistanceToPalm(IndexTip);
                float middleDistance = DistanceToPalm(MiddleTip);
                float ringDistance = DistanceToPalm(RingTip);
                float pinkyDistance = DistanceToPalm(PinkyTip);

                float averageDistance =
                    (
                        indexDistance +
                        middleDistance +
                        ringDistance +
                        pinkyDistance
                    ) / 4f;

                return averageDistance / palmWidth;
            }
        }

        public float OtherFingersCurlRatio
        {
            get
            {
                float middleDistance = DistanceToPalm(MiddleTip);
                float ringDistance = DistanceToPalm(RingTip);
                float pinkyDistance = DistanceToPalm(PinkyTip);

                float averageDistance =
                    (
                        middleDistance +
                        ringDistance +
                        pinkyDistance
                    ) / 3f;

                return averageDistance / palmWidth;
            }
        }

        private float DistanceToPalm(Vector3 fingerTip)
        {
            return Vector2.Distance(
                new Vector2(fingerTip.x, fingerTip.y),
                new Vector2(PalmCenter.x, PalmCenter.y)
            );
        }
    }

    public class SculptHandLandmarkerRunner :
        VisionTaskApiRunner<HandLandmarker>
    {
        private const int WristIndex = 0;
        private const int ThumbTipIndex = 4;

        private const int IndexMcpIndex = 5;
        private const int IndexTipIndex = 8;

        private const int MiddleMcpIndex = 9;
        private const int MiddleTipIndex = 12;

        private const int RingTipIndex = 16;

        private const int PinkyMcpIndex = 17;
        private const int PinkyTipIndex = 20;

        [SerializeField]
        private HandLandmarkerResultAnnotationController
            annotationController;

        [Header("Landmark Smoothing")]
        [Range(1f, 100f)]
        [SerializeField] private float landmarkSmoothingSpeed = 45f;

        [Range(0f, 0.02f)]
        [SerializeField] private float landmarkDeadZone = 0.0001f;

        private Experimental.TextureFramePool textureFramePool;

        private SculptHandFrame latestHand;
        private SculptHandFrame secondHand;

        private bool hasLatestHand;
        private bool hasSecondHand;

        private bool hasSmoothedLatestHand;
        private bool hasSmoothedSecondHand;

        private float lastResultTime;

        public readonly HandLandmarkDetectionConfig config =
            new HandLandmarkDetectionConfig();

        private void Awake()
        {
            config.RunningMode =
                Tasks.Vision.Core.RunningMode.VIDEO;

            config.NumHands = 2;

            config.MinHandDetectionConfidence = 0.20f;
            config.MinHandPresenceConfidence = 0.15f;
            config.MinTrackingConfidence = 0.15f;

            screen = FindFirstObjectByType<Screen>();
        }

        public bool TryGetLatestHand(
            out SculptHandFrame hand
        )
        {
            hand = latestHand;

            return hasLatestHand &&
                   Time.unscaledTime - lastResultTime < 0.75f;
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
                   Time.unscaledTime - lastResultTime < 0.75f;
        }

        public override void Stop()
        {
            base.Stop();

            textureFramePool?.Dispose();
            textureFramePool = null;

            hasLatestHand = false;
            hasSecondHand = false;

            hasSmoothedLatestHand = false;
            hasSmoothedSecondHand = false;
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
                ClearHandTracking();
                return;
            }

            if (!TryCreateHandFrame(
                result.handLandmarks[0].landmarks,
                out SculptHandFrame rawFirstHand
            ))
            {
                ClearHandTracking();
                return;
            }

            latestHand = SmoothHandFrame(
                latestHand,
                rawFirstHand,
                ref hasSmoothedLatestHand
            );

            hasLatestHand = true;
            hasSecondHand = false;
            hasSmoothedSecondHand = false;

            if (result.handLandmarks.Count > 1 &&
                TryCreateHandFrame(
                    result.handLandmarks[1].landmarks,
                    out SculptHandFrame rawSecondHand
                ))
            {
                secondHand = SmoothHandFrame(
                    secondHand,
                    rawSecondHand,
                    ref hasSmoothedSecondHand
                );

                hasSecondHand = true;
            }

            lastResultTime = Time.unscaledTime;
        }

        private void ClearHandTracking()
        {
            hasLatestHand = false;
            hasSecondHand = false;

            if (Time.unscaledTime - lastResultTime > 1.0f)
            {
                hasSmoothedLatestHand = false;
                hasSmoothedSecondHand = false;
            }
        }

        private SculptHandFrame SmoothHandFrame(
            SculptHandFrame previousHand,
            SculptHandFrame rawHand,
            ref bool hasPreviousHand
        )
        {
            if (!hasPreviousHand)
            {
                hasPreviousHand = true;
                return rawHand;
            }

            return new SculptHandFrame(
                SmoothPoint(
                    previousHand.Wrist,
                    rawHand.Wrist
                ),
                SmoothPoint(
                    previousHand.PalmCenter,
                    rawHand.PalmCenter
                ),
                SmoothPoint(
                    previousHand.ThumbTip,
                    rawHand.ThumbTip
                ),
                SmoothPoint(
                    previousHand.IndexTip,
                    rawHand.IndexTip
                ),
                SmoothPoint(
                    previousHand.MiddleTip,
                    rawHand.MiddleTip
                ),
                SmoothPoint(
                    previousHand.RingTip,
                    rawHand.RingTip
                ),
                SmoothPoint(
                    previousHand.PinkyTip,
                    rawHand.PinkyTip
                ),
                rawHand.PalmWidth
            );
        }

        private Vector3 SmoothPoint(
            Vector3 currentPoint,
            Vector3 targetPoint
        )
        {
            float distSqr = (targetPoint - currentPoint).sqrMagnitude;
            float deadZoneSquared =
                landmarkDeadZone * landmarkDeadZone;

            if (distSqr <= deadZoneSquared)
            {
                return currentPoint;
            }

            float t =
                1f - Mathf.Exp(
                    -landmarkSmoothingSpeed *
                    Time.deltaTime
                );

            return Vector3.Lerp(
                currentPoint,
                targetPoint,
                t
            );
        }

        private bool TryCreateHandFrame(
            System.Collections.Generic.IReadOnlyList<
                Mediapipe.Tasks.Components.Containers
                    .NormalizedLandmark
            > landmarks,
            out SculptHandFrame hand
        )
        {
            hand = default;

            if (landmarks == null ||
                landmarks.Count <= PinkyTipIndex)
            {
                return false;
            }

            Vector3 wrist = ToUnityPoint(
                landmarks[WristIndex]
            );

            Vector3 indexMcp = ToUnityPoint(
                landmarks[IndexMcpIndex]
            );

            Vector3 middleMcp = ToUnityPoint(
                landmarks[MiddleMcpIndex]
            );

            Vector3 pinkyMcp = ToUnityPoint(
                landmarks[PinkyMcpIndex]
            );

            Vector3 palmCenter =
                (
                    wrist +
                    indexMcp +
                    middleMcp +
                    pinkyMcp
                ) / 4f;

            float palmWidth = Vector2.Distance(
                new Vector2(indexMcp.x, indexMcp.y),
                new Vector2(pinkyMcp.x, pinkyMcp.y)
            );

            hand = new SculptHandFrame(
                wrist,
                palmCenter,
                ToUnityPoint(landmarks[ThumbTipIndex]),
                ToUnityPoint(landmarks[IndexTipIndex]),
                ToUnityPoint(landmarks[MiddleTipIndex]),
                ToUnityPoint(landmarks[RingTipIndex]),
                ToUnityPoint(landmarks[PinkyTipIndex]),
                palmWidth
            );

            return true;
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