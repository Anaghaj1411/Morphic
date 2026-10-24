using System.Collections;
using System.Collections.Generic;
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
        [Range(1f, 100f)]
        [SerializeField] private float landmarkSmoothingSpeed = 32f;

        [Range(0f, 0.02f)]
        [SerializeField] private float landmarkDeadZone = 0.002f;

        [SerializeField] private float landmarkJumpLimit = 0.12f;

        private Experimental.TextureFramePool textureFramePool;

        private readonly object resultLock = new object();
        private Vector3[][] pendingLandmarks;
        private bool[] pendingIsRightHand;
        private bool[] pendingHasHandedness;
        private bool hasPendingResult;
        private volatile bool inferenceInFlight;
        private bool isStopping;
        private bool hasStartedRun;
        private Coroutine ensureTeapotRunCoroutine;

        private SculptHandFrame latestHand;
        private SculptHandFrame secondHand;

        private bool hasLatestHand;
        private bool hasSecondHand;

        private bool hasSmoothedLatestHand;
        private bool hasSmoothedSecondHand;

        private float lastResultTime;
        private float handSampleDeltaTime = 1f / 30f;

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

            config.MinHandDetectionConfidence = 0.45f;
            config.MinHandPresenceConfidence = 0.50f;
            config.MinTrackingConfidence = 0.50f;

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
            hasStartedRun = false;
            isStopping = true;
            inferenceInFlight = false;
            lock (resultLock)
            {
                pendingLandmarks = null;
                pendingIsRightHand = null;
                pendingHasHandedness = null;
                hasPendingResult = false;
            }

            base.Stop();

            textureFramePool?.Dispose();
            textureFramePool = null;

            hasLatestHand = false;
            hasSecondHand = false;

            hasSmoothedLatestHand = false;
            hasSmoothedSecondHand = false;
            skeletonOverlay?.SetHands(null);
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
            {
                SetupAnnotationController(
                    annotationController,
                    imageSource
                );
            }

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
            var waitForEndOfFrame = new WaitForEndOfFrame();

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

                // Never queue old camera frames behind a slow inference. Drop
                // this frame while the previous request finishes, then use the
                // newest available webcam image.
                if (inferenceInFlight)
                {
                    textureFrame.Release();
                    yield return waitForEndOfFrame;
                    continue;
                }

                readbackRequest = textureFrame.ReadTextureAsync(
                    imageSource.GetCurrentTexture(),
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

                inferenceInFlight = true;
                try
                {
                    taskApi.DetectAsync(
                        image,
                        GetCurrentTimestampMillisec(),
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
            Vector3[][] copiedHands = null;
            bool[] copiedIsRightHand = null;
            bool[] copiedHasHandedness = null;

            if (result.handLandmarks != null && result.handLandmarks.Count > 0)
            {
                copiedHands = new Vector3[result.handLandmarks.Count][];
                copiedIsRightHand = new bool[result.handLandmarks.Count];
                copiedHasHandedness = new bool[result.handLandmarks.Count];
                for (int handIndex = 0; handIndex < result.handLandmarks.Count; handIndex++)
                {
                    if (result.handedness != null && handIndex < result.handedness.Count)
                    {
                        var categories = result.handedness[handIndex].categories;
                        if (categories != null && categories.Count > 0)
                        {
                            string label = categories[0].categoryName;
                            copiedHasHandedness[handIndex] =
                                label == "Left" || label == "Right";
                            copiedIsRightHand[handIndex] = label == "Right";
                        }
                    }

                    var source = result.handLandmarks[handIndex].landmarks;
                    if (source == null)
                    {
                        copiedHands[handIndex] = System.Array.Empty<Vector3>();
                        continue;
                    }

                    Vector3[] points = new Vector3[source.Count];
                    for (int pointIndex = 0; pointIndex < source.Count; pointIndex++)
                    {
                        var point = source[pointIndex];
                        points[pointIndex] = new Vector3(point.x, point.y, point.z);
                    }

                    copiedHands[handIndex] = points;
                }
            }

            lock (resultLock)
            {
                if (!isStopping)
                {
                    pendingLandmarks = copiedHands;
                    pendingIsRightHand = copiedIsRightHand;
                    pendingHasHandedness = copiedHasHandedness;
                    hasPendingResult = true;
                }

                inferenceInFlight = false;
            }
        }

        private void Update()
        {
            Vector3[][] latestLandmarks = null;
            bool[] latestIsRightHand = null;
            bool[] latestHasHandedness = null;
            bool hasResult = false;

            lock (resultLock)
            {
                if (hasPendingResult)
                {
                    latestLandmarks = pendingLandmarks;
                    latestIsRightHand = pendingIsRightHand;
                    latestHasHandedness = pendingHasHandedness;
                    pendingLandmarks = null;
                    pendingIsRightHand = null;
                    pendingHasHandedness = null;
                    hasPendingResult = false;
                    hasResult = true;
                }
            }

            if (!hasResult)
            {
                return;
            }

            if (latestLandmarks == null || latestLandmarks.Length == 0)
            {
                ClearHandTracking();
                skeletonOverlay?.SetHands(null);
                return;
            }

            float now = Time.unscaledTime;
            handSampleDeltaTime = lastResultTime > 0f
                ? Mathf.Clamp(now - lastResultTime, 1f / 120f, 0.25f)
                : 1f / 30f;

            SaveLatestHands(
                latestLandmarks,
                latestIsRightHand,
                latestHasHandedness);
            UpdateSkeletonOverlay(latestLandmarks);
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

        private void UpdateSkeletonOverlay(Vector3[][] normalizedHands)
        {
            if (skeletonOverlay == null ||
                normalizedHands == null ||
                normalizedHands.Length == 0)
            {
                skeletonOverlay?.SetHands(null);
                return;
            }

            var hands = new List<Vector2[]>(normalizedHands.Length);
            foreach (Vector3[] handLandmarks in normalizedHands)
            {
                if (handLandmarks == null ||
                    handLandmarks.Length < PinkyTipIndex + 1)
                {
                    continue;
                }

                var points = new Vector2[handLandmarks.Length];
                for (int i = 0; i < handLandmarks.Length; i++)
                {
                    points[i] = new Vector2(
                        handLandmarks[i].x,
                        handLandmarks[i].y
                    );
                }

                hands.Add(points);
            }

            // Keep the overlay's filter attached to the same hand when the
            // MediaPipe result order swaps between frames.
            hands.Sort((a, b) => GetHandCenterX(a).CompareTo(GetHandCenterX(b)));

            skeletonOverlay.SetHands(hands);
        }

        private static float GetHandCenterX(Vector2[] points)
        {
            if (points == null || points.Length == 0)
            {
                return 0f;
            }

            float total = 0f;
            for (int i = 0; i < points.Length; i++)
            {
                total += points[i].x;
            }

            return total / points.Length;
        }

        private void SaveLatestHands(
            Vector3[][] normalizedHands,
            bool[] isRightHand,
            bool[] hasHandedness)
        {
            if (normalizedHands == null || normalizedHands.Length == 0)
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
                normalizedHands.Length > 1 &&
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
                latestHand = SmoothHandFrame(
                    secondHand,
                    rawHand,
                    ref hasSmoothedLatestHand
                );
                hasSmoothedSecondHand = false;
            }
            else
            {
                latestHand = SmoothHandFrame(
                    latestHand,
                    rawHand,
                    ref hasSmoothedLatestHand
                );
            }

            hasLatestHand = true;
            hasSecondHand = false;
        }

        private void AssignTwoHands(
            SculptHandFrame rawFirstHand,
            SculptHandFrame rawSecondHand
        )
        {
            if (!hasSmoothedLatestHand)
            {
                latestHand = rawFirstHand;
                secondHand = rawSecondHand;
                hasSmoothedLatestHand = true;
                hasSmoothedSecondHand = true;
                hasLatestHand = true;
                hasSecondHand = true;
                return;
            }

            if (!hasSmoothedSecondHand)
            {
                bool firstCloser =
                    PalmDistance(rawFirstHand, latestHand) <=
                    PalmDistance(rawSecondHand, latestHand);

                if (firstCloser)
                {
                    latestHand = SmoothHandFrame(
                        latestHand,
                        rawFirstHand,
                        ref hasSmoothedLatestHand
                    );
                    secondHand = SmoothHandFrame(
                        secondHand,
                        rawSecondHand,
                        ref hasSmoothedSecondHand
                    );
                }
                else
                {
                    latestHand = SmoothHandFrame(
                        latestHand,
                        rawSecondHand,
                        ref hasSmoothedLatestHand
                    );
                    secondHand = SmoothHandFrame(
                        secondHand,
                        rawFirstHand,
                        ref hasSmoothedSecondHand
                    );
                }

                hasLatestHand = true;
                hasSecondHand = true;
                return;
            }

            float keepOrder =
                PalmDistance(rawFirstHand, latestHand) +
                PalmDistance(rawSecondHand, secondHand);

            float swapOrder =
                PalmDistance(rawSecondHand, latestHand) +
                PalmDistance(rawFirstHand, secondHand);

            if (swapOrder < keepOrder)
            {
                latestHand = SmoothHandFrame(
                    latestHand,
                    rawSecondHand,
                    ref hasSmoothedLatestHand
                );
                secondHand = SmoothHandFrame(
                    secondHand,
                    rawFirstHand,
                    ref hasSmoothedSecondHand
                );
            }
            else
            {
                latestHand = SmoothHandFrame(
                    latestHand,
                    rawFirstHand,
                    ref hasSmoothedLatestHand
                );
                secondHand = SmoothHandFrame(
                    secondHand,
                    rawSecondHand,
                    ref hasSmoothedSecondHand
                );
            }

            hasLatestHand = true;
            hasSecondHand = true;
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

            float palmWidth = SmoothScalar(
                previousHand.PalmWidth,
                rawHand.PalmWidth
            );

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
                    previousHand.ThumbMcp,
                    rawHand.ThumbMcp
                ),
                SmoothPoint(
                    previousHand.ThumbTip,
                    rawHand.ThumbTip
                ),
                rawHand.IsRightHand,
                rawHand.HasHandedness,
                SmoothPoint(
                    previousHand.IndexMcp,
                    rawHand.IndexMcp
                ),
                SmoothPoint(
                    previousHand.IndexPip,
                    rawHand.IndexPip
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
                palmWidth
            );
        }

        private Vector3 SmoothPoint(
            Vector3 currentPoint,
            Vector3 targetPoint
        )
        {
            // Normalized screen-space XY drives the gesture. MediaPipe Z is
            // noisier, so it must not make the whole hand freeze or jump.
            float distance = Vector2.Distance(
                new Vector2(currentPoint.x, currentPoint.y),
                new Vector2(targetPoint.x, targetPoint.y)
            );

            if (distance <= landmarkDeadZone)
            {
                return currentPoint;
            }

            float dt = handSampleDeltaTime;
            float movementSpeed = distance / dt;
            float adaptiveSpeed = landmarkSmoothingSpeed +
                Mathf.Clamp(movementSpeed, 0f, 3f) * 2f;
            float t = 1f - Mathf.Exp(-adaptiveSpeed * dt);

            if (distance > landmarkJumpLimit)
            {
                // Reject most of a one-frame landmark spike while still
                // allowing a genuine quick movement to catch up over frames.
                t *= 0.75f;
            }

            return Vector3.Lerp(
                currentPoint,
                targetPoint,
                t
            );
        }

        private float SmoothScalar(float currentValue, float targetValue)
        {
            if (Mathf.Abs(targetValue - currentValue) <= landmarkDeadZone)
            {
                return currentValue;
            }

            float t =
                1f - Mathf.Exp(
                    -landmarkSmoothingSpeed *
                    handSampleDeltaTime
                );

            return Mathf.Lerp(currentValue, targetValue, t);
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
    }
}
