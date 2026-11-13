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
        public readonly Vector3 ThumbMcp;
        public readonly Vector3 ThumbTip;
        public readonly bool IsRightHand;
        public readonly bool HasHandedness;
        public readonly Vector3 IndexMcp;
        public readonly Vector3 IndexPip;
        public readonly Vector3 IndexTip;
        public readonly Vector3 MiddleTip;
        public readonly Vector3 RingTip;
        public readonly Vector3 PinkyTip;

        private readonly float palmWidth;

        public float PalmWidth => palmWidth;
        public float IndexTipPalmRatio => DistanceToPalm(IndexTip) / palmWidth;
        public float MiddleTipPalmRatio => DistanceToPalm(MiddleTip) / palmWidth;
        public float RingTipPalmRatio => DistanceToPalm(RingTip) / palmWidth;
        public float PinkyTipPalmRatio => DistanceToPalm(PinkyTip) / palmWidth;

        public SculptHandFrame(
            Vector3 wrist,
            Vector3 palmCenter,
            Vector3 thumbMcp,
            Vector3 thumbTip,
            bool isRightHand,
            bool hasHandedness,
            Vector3 indexMcp,
            Vector3 indexPip,
            Vector3 indexTip,
            Vector3 middleTip,
            Vector3 ringTip,
            Vector3 pinkyTip,
            float handPalmWidth
        )
        {
            Wrist = wrist;
            PalmCenter = palmCenter;
            ThumbMcp = thumbMcp;
            ThumbTip = thumbTip;
            IsRightHand = isRightHand;
            HasHandedness = hasHandedness;
            IndexMcp = indexMcp;
            IndexPip = indexPip;
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

        public float IndexExtensionAngle
        {
            get
            {
                return FingerExtensionAngle(IndexMcp, IndexPip, IndexTip);
            }
        }

        private static float FingerExtensionAngle(
            Vector3 mcp, Vector3 pip, Vector3 tip)
        {
            Vector2 mcpToPip = new Vector2(mcp.x - pip.x, mcp.y - pip.y);
            Vector2 tipToPip = new Vector2(tip.x - pip.x, tip.y - pip.y);
            return Vector2.Angle(mcpToPip, tipToPip);
        }

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

        private HandSkeletonOverlayGraphic skeletonOverlay;

        [Header("Webcam Display")]
        [Tooltip("Optional: Assign the specific Screen component for webcam display. If not assigned, will search for one.")]
        [SerializeField] public Screen webcamScreen;

        [Header("Landmark Smoothing")]
        [Tooltip("Resting follow rate in Hz. Higher is snappier.")]
        [Range(0.5f, 20f)]
        [SerializeField] private float landmarkMinCutoff = 7.5f;

        [Tooltip("How quickly follow speeds up when the hand moves.")]
        [Range(0.05f, 2f)]
        [SerializeField] private float landmarkSpeedBeta = 0.9f;

        private Experimental.TextureFramePool textureFramePool;

        private readonly object resultLock = new object();
        private readonly Vector3[][] landmarkScratchA =
        {
            new Vector3[21],
            new Vector3[21]
        };
        private readonly Vector3[][] landmarkScratchB =
        {
            new Vector3[21],
            new Vector3[21]
        };
        private readonly bool[] handednessScratchA = new bool[2];
        private readonly bool[] handednessScratchB = new bool[2];
        private readonly bool[] hasHandednessScratchA = new bool[2];
        private readonly bool[] hasHandednessScratchB = new bool[2];
        private readonly Vector2[][] overlayHands =
        {
            new Vector2[21],
            new Vector2[21]
        };
        private readonly Vector3[][] overlayRaw =
        {
            new Vector3[21],
            new Vector3[21]
        };
        private readonly Vector3[][] overlayDx =
        {
            new Vector3[21],
            new Vector3[21]
        };
        private int overlayCount;
        private bool overlayHasPose;
        private Vector3[][] pendingLandmarks;
        private bool[] pendingIsRightHand;
        private bool[] pendingHasHandedness;
        private int pendingHandCount;
        private bool writeUseA = true;
        private bool hasPendingResult;
        private volatile bool inferenceInFlight;
        private bool isStopping;
        private bool hasStartedRun;
        private Coroutine ensureTeapotRunCoroutine;
        private long lastSentTimestamp = -1;
        private float lastDetectRealtime;
        private readonly HandEuro latestEuro = new HandEuro();
        private readonly HandEuro secondEuro = new HandEuro();

        private SculptHandFrame latestHand;
        private SculptHandFrame secondHand;
        private SculptHandFrame rawLatestHand;
        private SculptHandFrame rawSecondHand;

        private bool hasLatestHand;
        private bool hasSecondHand;
        private bool hasRawLatestHand;
        private bool hasRawSecondHand;

        private bool hasSmoothedLatestHand;
        private bool hasSmoothedSecondHand;

        private float lastResultTime;
        private float handSampleDeltaTime = 1f / 60f;
        private const float LostHandGrace = 0.7f;
        private const float MinPalmWidth = 0.003f;
        private const float DetectTimeout = 0.12f;
        private const float MinDetectInterval = 0.016f;
        private const float JitterDeadzone = 0.001f;

        public readonly HandLandmarkDetectionConfig config =
            new HandLandmarkDetectionConfig();

        public bool HasStartedRun => hasStartedRun;

        protected override IEnumerator Start()
        {
            bootstrap = FindBootstrap();
            yield return new WaitUntil(() => bootstrap != null && bootstrap.isFinished);

            if (!hasStartedRun)
                Play();
        }

        /// <summary>Starts tracking for the Teapot after MediaPipe initialization.</summary>
        public void EnsureStartedForTeapot()
        {
            if (hasStartedRun || ensureTeapotRunCoroutine != null)
                return;

            if (!isActiveAndEnabled)
            {
                Debug.LogWarning(
                    "MORPHIC: India Teapot hand runner is not active in the hierarchy, " +
                    "so Start/Play cannot open the webcam. Enable TaiwanHandTracking " +
                    "before Solution.",
                    this);
                return;
            }

            ensureTeapotRunCoroutine = StartCoroutine(EnsureTeapotRunAfterBootstrap());
        }

        private IEnumerator EnsureTeapotRunAfterBootstrap()
        {
            if (bootstrap == null)
                bootstrap = FindBootstrap();

            yield return new WaitUntil(() => bootstrap != null && bootstrap.isFinished);
            yield return null;

            if (!hasStartedRun && isActiveAndEnabled)
            {
                Debug.Log("MORPHIC: Explicitly starting the hand runner for the India Teapot.");
                Play();
            }

            ensureTeapotRunCoroutine = null;
        }

        private void Awake()
        {
            config.RunningMode =
                Tasks.Vision.Core.RunningMode.LIVE_STREAM;

            config.NumHands = 2;
            config.MinHandDetectionConfidence = 0.08f;
            config.MinHandPresenceConfidence = 0.1f;
            config.MinTrackingConfidence = 0.1f;

            landmarkMinCutoff = 4f;
            landmarkSpeedBeta = 0.8f;

            // Try to use assigned webcam screen first, otherwise search
            if (webcamScreen == null)
            {
                // The serialized base Screen belongs to the Annotatable Screen
                // prefab and shares its hierarchy with the annotation controller.
                // Prefer it over a scene-wide search that can select the menu's
                // separate, non-annotatable preview Screen.
                webcamScreen = screen != null
                    ? screen
                    : FindFirstObjectByType<Screen>();
            }

            screen = webcamScreen;
        }

        public bool TryGetLatestHand(
            out SculptHandFrame hand
        )
        {
            hand = latestHand;

            return hasLatestHand &&
                   Time.unscaledTime - lastResultTime < LostHandGrace;
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
                   Time.unscaledTime - lastResultTime < LostHandGrace;
        }

        public override void Stop()
        {
            hasStartedRun = false;
            isStopping = true;
            inferenceInFlight = false;
            lastSentTimestamp = -1;
            lastDetectRealtime = 0f;
            latestEuro.Reset();
            secondEuro.Reset();
            lock (resultLock)
            {
                pendingLandmarks = null;
                pendingIsRightHand = null;
                pendingHasHandedness = null;
                pendingHandCount = 0;
                hasPendingResult = false;
            }

            base.Stop();

            textureFramePool?.Dispose();
            textureFramePool = null;

            hasLatestHand = false;
            hasSecondHand = false;
            hasRawLatestHand = false;
            hasRawSecondHand = false;
            hasSmoothedLatestHand = false;
            hasSmoothedSecondHand = false;
            overlayHasPose = false;
            overlayCount = 0;
            skeletonOverlay?.SetHands(null, 0);
        }

        protected override IEnumerator Run()
        {
            hasStartedRun = true;
            isStopping = false;
            yield return AssetLoader.PrepareAssetAsync(
                config.ModelPath
            );

            var options = config.GetHandLandmarkerOptions(
                OnHandLandmarkDetectionOutput
            );

            taskApi = HandLandmarker.CreateFromOptions(
                options,
                GpuManager.GpuResources
            );

            var imageSource = ImageSourceProvider.ImageSource;

            Debug.Log("MORPHIC: Image source type = " + imageSource.GetType().Name);
Debug.Log("MORPHIC: Available cameras = " +
    string.Join(", ", imageSource.sourceCandidateNames ?? System.Array.Empty<string>()));

            yield return imageSource.Play();

            Debug.Log("MORPHIC: Selected camera = " + imageSource.sourceName);
Debug.Log("MORPHIC: Camera prepared = " + imageSource.isPrepared);
Debug.Log("MORPHIC: Camera playing = " + imageSource.isPlaying);
Debug.Log("MORPHIC: Camera resolution = " +
    imageSource.textureWidth + " x " + imageSource.textureHeight);

            if (!imageSource.isPrepared)
            {
                hasStartedRun = false;
                Debug.LogError("Could not start the webcam.");
                yield break;
            }

            textureFramePool =
                new Experimental.TextureFramePool(
                    imageSource.textureWidth,
                    imageSource.textureHeight,
                    TextureFormat.RGBA32,
                    8
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
                Debug.LogWarning(
                    "MORPHIC: SDK annotation controller is missing; using the direct skeleton overlay."
                );
            }

            if (screen == null)
            {
                hasStartedRun = false;
                Debug.LogError(
                    "Could not find the webcam display."
                );

                yield break;
            }

            screen.Initialize(imageSource);
            Debug.Log(
                "MORPHIC: Webcam screen initialized on '" +
                screen.name +
                "' with texture " +
                (imageSource.GetCurrentTexture() != null
                    ? imageSource.GetCurrentTexture().width + "x" + imageSource.GetCurrentTexture().height
                    : "null") +
                ".");

            if (annotationController != null)
                annotationController.enabled = false;

            EnsureSkeletonOverlay();

            var transformOptions =
                imageSource.GetTransformationOptions();

            var imageOptions =
                new Tasks.Vision.Core.ImageProcessingOptions(
                    rotationDegrees:
                        (int)transformOptions.rotationAngle
                );

            AsyncGPUReadbackRequest readbackRequest = default;
            var waitForReadback = new WaitUntil(
                () => readbackRequest.done
            );
            while (true)
            {
                if (isPaused)
                {
                    yield return new WaitWhile(
                        () => isPaused
                    );
                }

                if (inferenceInFlight &&
                    Time.realtimeSinceStartup - lastDetectRealtime > DetectTimeout)
                    inferenceInFlight = false;

                if (!textureFramePool.TryGetTextureFrame(
                    out var textureFrame
                ))
                {
                    yield return null;
                    continue;
                }

                // Live-stream may drop a callback. Never wait forever — send
                // the newest webcam frame after a short timeout so tracking
                // cannot freeze for the rest of the session.
                if (inferenceInFlight &&
                    Time.realtimeSinceStartup - lastDetectRealtime < DetectTimeout)
                {
                    textureFrame.Release();
                    yield return null;
                    continue;
                }

                if (Time.realtimeSinceStartup - lastDetectRealtime < MinDetectInterval)
                {
                    textureFrame.Release();
                    yield return null;
                    continue;
                }

                Texture webcamTexture = imageSource.GetCurrentTexture();
                if (webcamTexture == null)
                {
                    textureFrame.Release();
                    yield return null;
                    continue;
                }

                readbackRequest = textureFrame.ReadTextureAsync(
                    webcamTexture,
                    transformOptions.flipHorizontally,
                    transformOptions.flipVertically
                );

                yield return waitForReadback;

                if (readbackRequest.hasError)
                {
                    textureFrame.Release();
                    continue;
                }

                var image = textureFrame.BuildCPUImage();
                textureFrame.Release();

                if (isStopping || taskApi == null)
                {
                    image.Dispose();
                    continue;
                }

                long timestamp = GetCurrentTimestampMillisec();
                if (timestamp <= lastSentTimestamp)
                    timestamp = lastSentTimestamp + 1;
                lastSentTimestamp = timestamp;

                inferenceInFlight = true;
                lastDetectRealtime = Time.realtimeSinceStartup;
                try
                {
                    taskApi.DetectAsync(
                        image,
                        timestamp,
                        imageOptions
                    );
                }
                catch (System.Exception exception)
                {
                    inferenceInFlight = false;
                    image.Dispose();
                    Debug.LogException(exception, this);
                }
            }
        }

        private void OnHandLandmarkDetectionOutput(
            HandLandmarkerResult result,
            Image image,
            long timestamp)
        {
            Vector3[][] destHands = writeUseA ? landmarkScratchA : landmarkScratchB;
            bool[] destRight = writeUseA ? handednessScratchA : handednessScratchB;
            bool[] destHas = writeUseA ? hasHandednessScratchA : hasHandednessScratchB;
            int copiedCount = 0;

            if (result.handLandmarks != null && result.handLandmarks.Count > 0)
            {
                int handCount = Mathf.Min(2, result.handLandmarks.Count);
                for (int handIndex = 0; handIndex < handCount; handIndex++)
                {
                    destRight[handIndex] = false;
                    destHas[handIndex] = false;
                    if (result.handedness != null && handIndex < result.handedness.Count)
                    {
                        var categories = result.handedness[handIndex].categories;
                        if (categories != null && categories.Count > 0)
                        {
                            string label = categories[0].categoryName;
                            destHas[handIndex] = label == "Left" || label == "Right";
                            destRight[handIndex] = label == "Right";
                        }
                    }

                    var source = result.handLandmarks[handIndex].landmarks;
                    Vector3[] dest = destHands[handIndex];
                    if (source == null || dest == null)
                        continue;

                    int pointCount = Mathf.Min(dest.Length, source.Count);
                    for (int pointIndex = 0; pointIndex < pointCount; pointIndex++)
                    {
                        var point = source[pointIndex];
                        dest[pointIndex] = new Vector3(point.x, point.y, point.z);
                    }

                    copiedCount++;
                }
            }

            lock (resultLock)
            {
                if (!isStopping)
                {
                    pendingLandmarks = copiedCount > 0 ? destHands : null;
                    pendingIsRightHand = destRight;
                    pendingHasHandedness = destHas;
                    pendingHandCount = copiedCount;
                    hasPendingResult = true;
                    writeUseA = !writeUseA;
                }

                inferenceInFlight = false;
            }
        }

        private void Update()
        {
            if (inferenceInFlight &&
                Time.realtimeSinceStartup - lastDetectRealtime > DetectTimeout)
                inferenceInFlight = false;

            Vector3[][] latestLandmarks = null;
            bool[] latestIsRightHand = null;
            bool[] latestHasHandedness = null;
            int latestCount = 0;
            bool hasResult = false;

            lock (resultLock)
            {
                if (hasPendingResult)
                {
                    latestLandmarks = pendingLandmarks;
                    latestIsRightHand = pendingIsRightHand;
                    latestHasHandedness = pendingHasHandedness;
                    latestCount = pendingHandCount;
                    pendingLandmarks = null;
                    pendingIsRightHand = null;
                    pendingHasHandedness = null;
                    pendingHandCount = 0;
                    hasPendingResult = false;
                    hasResult = true;
                }
            }

            if (hasResult && latestLandmarks != null && latestCount > 0)
            {
                SaveLatestHands(
                    latestLandmarks,
                    latestIsRightHand,
                    latestHasHandedness,
                    latestCount);
                CaptureOverlayTargets(latestLandmarks, latestCount);
            }

            FollowRawHands();
            TickOverlay();
        }

        private void FollowRawHands()
        {
            if (!hasRawLatestHand ||
                Time.unscaledTime - lastResultTime > LostHandGrace)
            {
                ClearHandTracking();
                return;
            }

            handSampleDeltaTime = Mathf.Clamp(Time.unscaledDeltaTime, 1f / 240f, 0.05f);
            latestHand = SmoothHandFrame(
                latestHand,
                rawLatestHand,
                latestEuro,
                ref hasSmoothedLatestHand);
            hasLatestHand = true;

            if (hasRawSecondHand)
            {
                secondHand = SmoothHandFrame(
                    secondHand,
                    rawSecondHand,
                    secondEuro,
                    ref hasSmoothedSecondHand);
                hasSecondHand = true;
            }
            else
            {
                hasSecondHand = false;
                hasSmoothedSecondHand = false;
                secondEuro.Reset();
            }
        }

        private void EnsureSkeletonOverlay()
        {
            if (skeletonOverlay != null || screen == null)
            {
                return;
            }

            GameObject overlayObject = new GameObject(
                "Detected Hand Skeleton Overlay",
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(HandSkeletonOverlayGraphic)
            );

            RectTransform overlayRect = overlayObject.GetComponent<RectTransform>();
            overlayRect.SetParent(screen.transform, false);
            overlayRect.anchorMin = Vector2.zero;
            overlayRect.anchorMax = Vector2.one;
            overlayRect.pivot = new Vector2(0.5f, 0.5f);
            overlayRect.anchoredPosition = Vector2.zero;
            overlayRect.sizeDelta = Vector2.zero;
            overlayRect.offsetMin = Vector2.zero;
            overlayRect.offsetMax = Vector2.zero;
            overlayRect.SetAsLastSibling();

            skeletonOverlay = overlayObject.GetComponent<HandSkeletonOverlayGraphic>();
            skeletonOverlay.raycastTarget = false;
        }

        private void CaptureOverlayTargets(Vector3[][] normalizedHands, int handCount)
        {
            if (normalizedHands == null || handCount <= 0)
                return;

            int visible = 0;
            int limit = Mathf.Min(2, Mathf.Min(handCount, normalizedHands.Length));
            for (int handIndex = 0; handIndex < limit; handIndex++)
            {
                Vector3[] handLandmarks = normalizedHands[handIndex];
                if (handLandmarks == null ||
                    handLandmarks.Length < PinkyTipIndex + 1)
                    continue;

                Vector3[] dest = overlayRaw[visible];
                int count = Mathf.Min(dest.Length, handLandmarks.Length);
                for (int i = 0; i < count; i++)
                    dest[i] = handLandmarks[i];

                if (!overlayHasPose)
                {
                    Vector2[] drawn = overlayHands[visible];
                    Vector3[] dx = overlayDx[visible];
                    for (int i = 0; i < count; i++)
                    {
                        drawn[i] = new Vector2(dest[i].x, dest[i].y);
                        dx[i] = Vector3.zero;
                    }
                }

                visible++;
            }

            overlayCount = visible;
            overlayHasPose = visible > 0;
            if (overlayHasPose)
                skeletonOverlay?.SetHands(overlayHands, overlayCount);
        }

        private void TickOverlay()
        {
            if (skeletonOverlay == null || !overlayHasPose || overlayCount <= 0)
                return;

            for (int handIndex = 0; handIndex < overlayCount; handIndex++)
            {
                Vector2[] drawn = overlayHands[handIndex];
                Vector3[] raw = overlayRaw[handIndex];
                Vector3[] dx = overlayDx[handIndex];
                for (int i = 0; i < 21; i++)
                {
                    Vector3 current = new Vector3(drawn[i].x, drawn[i].y, 0f);
                    Vector3 smoothed = SmoothPoint(current, raw[i], ref dx[i]);
                    drawn[i] = new Vector2(smoothed.x, smoothed.y);
                }
            }

            skeletonOverlay.SetHands(overlayHands, overlayCount);
        }

        private void SaveLatestHands(
            Vector3[][] normalizedHands,
            bool[] isRightHand,
            bool[] hasHandedness,
            int handCount)
        {
            if (normalizedHands == null || handCount <= 0)
            {
                ClearHandTracking();
                return;
            }

            bool hasRawFirst = TryCreateHandFrame(
                normalizedHands[0],
                isRightHand != null && isRightHand.Length > 0 && isRightHand[0],
                hasHandedness != null && hasHandedness.Length > 0 && hasHandedness[0],
                out SculptHandFrame rawFirstHand
            );

            if (!hasRawFirst)
            {
                ClearHandTracking();
                return;
            }

            SculptHandFrame rawSecondHand = default;
            bool hasRawSecond =
                handCount > 1 &&
                TryCreateHandFrame(
                    normalizedHands[1],
                    isRightHand != null && isRightHand.Length > 1 && isRightHand[1],
                    hasHandedness != null && hasHandedness.Length > 1 && hasHandedness[1],
                    out rawSecondHand
                );

            if (hasRawSecond)
            {
                AssignTwoHands(rawFirstHand, rawSecondHand);
            }
            else
            {
                AssignOneHand(rawFirstHand);
            }

            lastResultTime = Time.unscaledTime;
        }

        private void AssignOneHand(SculptHandFrame rawHand)
        {
            bool preferSecond =
                hasSmoothedLatestHand &&
                hasSmoothedSecondHand &&
                PalmDistance(rawHand, secondHand) <
                PalmDistance(rawHand, latestHand);

            if (preferSecond)
            {
                latestHand = secondHand;
                SwapEuro(latestEuro, secondEuro);
            }

            rawLatestHand = rawHand;
            hasRawLatestHand = true;
            hasRawSecondHand = false;
        }

        private void AssignTwoHands(
            SculptHandFrame rawFirstHand,
            SculptHandFrame rawSecondHand
        )
        {
            bool swap =
                hasSmoothedLatestHand &&
                PalmDistance(rawSecondHand, latestHand) +
                PalmDistance(rawFirstHand, secondHand) <
                PalmDistance(rawFirstHand, latestHand) +
                PalmDistance(rawSecondHand, secondHand);

            if (swap)
            {
                rawLatestHand = rawSecondHand;
                this.rawSecondHand = rawFirstHand;
                SwapEuro(latestEuro, secondEuro);
            }
            else
            {
                rawLatestHand = rawFirstHand;
                this.rawSecondHand = rawSecondHand;
            }

            hasRawLatestHand = true;
            hasRawSecondHand = true;
        }

        private static float PalmDistance(
            SculptHandFrame firstHand,
            SculptHandFrame otherHand
        )
        {
            return Vector2.Distance(
                new Vector2(firstHand.PalmCenter.x, firstHand.PalmCenter.y),
                new Vector2(otherHand.PalmCenter.x, otherHand.PalmCenter.y)
            );
        }

        private void ClearHandTracking()
        {
            if (hasRawLatestHand &&
                Time.unscaledTime - lastResultTime <= LostHandGrace)
                return;

            hasLatestHand = false;
            hasSecondHand = false;
            hasRawLatestHand = false;
            hasRawSecondHand = false;
            hasSmoothedLatestHand = false;
            hasSmoothedSecondHand = false;
            overlayHasPose = false;
            overlayCount = 0;
            latestEuro.Reset();
            secondEuro.Reset();
            skeletonOverlay?.SetHands(null, 0);
        }

        private SculptHandFrame SmoothHandFrame(
            SculptHandFrame previousHand,
            SculptHandFrame rawHand,
            HandEuro euro,
            ref bool hasPreviousHand
        )
        {
            if (!hasPreviousHand || euro == null || !euro.ready)
            {
                hasPreviousHand = true;
                if (euro != null)
                    euro.ready = true;
                return rawHand;
            }

            float palmWidth = SmoothScalar(
                previousHand.PalmWidth,
                rawHand.PalmWidth,
                ref euro.palmWidthDx
            );

            return new SculptHandFrame(
                SmoothPoint(previousHand.Wrist, rawHand.Wrist, ref euro.wristDx),
                SmoothPoint(previousHand.PalmCenter, rawHand.PalmCenter, ref euro.palmDx),
                SmoothPoint(previousHand.ThumbMcp, rawHand.ThumbMcp, ref euro.thumbMcpDx),
                SmoothPoint(previousHand.ThumbTip, rawHand.ThumbTip, ref euro.thumbTipDx),
                rawHand.IsRightHand,
                rawHand.HasHandedness,
                SmoothPoint(previousHand.IndexMcp, rawHand.IndexMcp, ref euro.indexMcpDx),
                SmoothPoint(previousHand.IndexPip, rawHand.IndexPip, ref euro.indexPipDx),
                SmoothPoint(previousHand.IndexTip, rawHand.IndexTip, ref euro.indexTipDx),
                SmoothPoint(previousHand.MiddleTip, rawHand.MiddleTip, ref euro.middleTipDx),
                SmoothPoint(previousHand.RingTip, rawHand.RingTip, ref euro.ringTipDx),
                SmoothPoint(previousHand.PinkyTip, rawHand.PinkyTip, ref euro.pinkyTipDx),
                palmWidth
            );
        }

        private Vector3 SmoothPoint(
            Vector3 currentPoint,
            Vector3 targetPoint,
            ref Vector3 dx
        )
        {
            if ((targetPoint - currentPoint).sqrMagnitude < JitterDeadzone * JitterDeadzone)
                return currentPoint;

            float dt = Mathf.Max(handSampleDeltaTime, 0.0001f);
            Vector3 rawDx = (targetPoint - currentPoint) / dt;
            float derivAlpha = 1f - Mathf.Exp(-2f * Mathf.PI * 1.2f * dt);
            dx = Vector3.Lerp(dx, rawDx, Mathf.Clamp01(derivAlpha));
            float speed = new Vector2(dx.x, dx.y).magnitude;
            float cutoff = landmarkMinCutoff + landmarkSpeedBeta * speed;
            float t = 1f - Mathf.Exp(-2f * Mathf.PI * cutoff * dt);
            return Vector3.Lerp(currentPoint, targetPoint, Mathf.Clamp01(t));
        }

        private float SmoothScalar(float currentValue, float targetValue, ref float dx)
        {
            float dt = Mathf.Max(handSampleDeltaTime, 0.0001f);
            float rawDx = (targetValue - currentValue) / dt;
            float derivAlpha = 1f - Mathf.Exp(-2f * Mathf.PI * 1.2f * dt);
            dx = Mathf.Lerp(dx, rawDx, Mathf.Clamp01(derivAlpha));
            float cutoff = landmarkMinCutoff + landmarkSpeedBeta * Mathf.Abs(dx);
            float t = 1f - Mathf.Exp(-2f * Mathf.PI * cutoff * dt);
            return Mathf.Lerp(currentValue, targetValue, Mathf.Clamp01(t));
        }

        private static void SwapEuro(HandEuro first, HandEuro second)
        {
            first.SwapWith(second);
        }

        private bool TryCreateHandFrame(
            Vector3[] landmarks,
            bool isRightHand,
            bool hasHandedness,
            out SculptHandFrame hand)
        {
            hand = default;

            if (landmarks == null ||
                landmarks.Length <= PinkyTipIndex)
            {
                return false;
            }

            Vector3 wrist = ToUnityPoint(landmarks[WristIndex]);

            Vector3 indexMcp = ToUnityPoint(landmarks[IndexMcpIndex]);

            Vector3 middleMcp = ToUnityPoint(landmarks[MiddleMcpIndex]);

            Vector3 pinkyMcp = ToUnityPoint(landmarks[PinkyMcpIndex]);

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

            if (palmWidth < MinPalmWidth)
                return false;

            hand = new SculptHandFrame(
                wrist,
                palmCenter,
                ToUnityPoint(landmarks[2]),
                ToUnityPoint(landmarks[ThumbTipIndex]),
                isRightHand,
                hasHandedness,
                indexMcp,
                ToUnityPoint(landmarks[6]),
                ToUnityPoint(landmarks[IndexTipIndex]),
                ToUnityPoint(landmarks[MiddleTipIndex]),
                ToUnityPoint(landmarks[RingTipIndex]),
                ToUnityPoint(landmarks[PinkyTipIndex]),
                palmWidth
            );

            return true;
        }

        private static Vector3 ToUnityPoint(Vector3 normalizedLandmark)
        {
            return new Vector3(
                normalizedLandmark.x,
                1f - normalizedLandmark.y,
                normalizedLandmark.z
            );
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

        private sealed class HandEuro
        {
            public bool ready;
            public Vector3 wristDx;
            public Vector3 palmDx;
            public Vector3 thumbMcpDx;
            public Vector3 thumbTipDx;
            public Vector3 indexMcpDx;
            public Vector3 indexPipDx;
            public Vector3 indexTipDx;
            public Vector3 middleTipDx;
            public Vector3 ringTipDx;
            public Vector3 pinkyTipDx;
            public float palmWidthDx;

            public void Reset()
            {
                ready = false;
                wristDx = palmDx = thumbMcpDx = thumbTipDx = Vector3.zero;
                indexMcpDx = indexPipDx = indexTipDx = Vector3.zero;
                middleTipDx = ringTipDx = pinkyTipDx = Vector3.zero;
                palmWidthDx = 0f;
            }

            public void SwapWith(HandEuro other)
            {
                if (other == null)
                    return;

                bool readySwap = ready;
                ready = other.ready;
                other.ready = readySwap;

                Swap(ref wristDx, ref other.wristDx);
                Swap(ref palmDx, ref other.palmDx);
                Swap(ref thumbMcpDx, ref other.thumbMcpDx);
                Swap(ref thumbTipDx, ref other.thumbTipDx);
                Swap(ref indexMcpDx, ref other.indexMcpDx);
                Swap(ref indexPipDx, ref other.indexPipDx);
                Swap(ref indexTipDx, ref other.indexTipDx);
                Swap(ref middleTipDx, ref other.middleTipDx);
                Swap(ref ringTipDx, ref other.ringTipDx);
                Swap(ref pinkyTipDx, ref other.pinkyTipDx);

                float palm = palmWidthDx;
                palmWidthDx = other.palmWidthDx;
                other.palmWidthDx = palm;
            }

            private static void Swap(ref Vector3 a, ref Vector3 b)
            {
                Vector3 t = a;
                a = b;
                b = t;
            }
        }
    }
}
