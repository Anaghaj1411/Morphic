using System.Collections.Generic;
using Mediapipe.Unity.Sample.HandLandmarkDetection;
using TMPro;
using UnityEngine;

/// <summary>
/// Teapot-specific hand-control component. It binds to the Teapot renderer and
/// resolves its imported blend shapes by their exact FBX names.
/// </summary>
[DefaultExecutionOrder(250)]
public sealed class TeapotGestureController : MonoBehaviour
{
    private static readonly string[] RequiredBlendShapeNames =
    {
        "Width",
        "Height",
        "Middle",
        "Bevel Top and Bottom",
        "Lid 1",
        "Lid 2",
        "Shaft 1",
        "Shaft 2",
        "Handle 1",
        "Handle 2"
    };

    [Header("References")]
    [SerializeField] private SculptHandLandmarkerRunner handRunner;
    [SerializeField] private TMP_Text gestureStatus;
    [SerializeField] private SkinnedMeshRenderer teapotRenderer;

    [Header("Body Gesture Controls")]
    [SerializeField, Min(1f)] private float widthSensitivity = 360f;
    [SerializeField, Min(1f)] private float heightSensitivity = 550f;
    [SerializeField, Min(1f)] private float middleSensitivity = 360f;
    [SerializeField, Range(0f, 100f)] private float middleMaximumWeight = 35.4f;
    [SerializeField, Min(1f)] private float smoothing = 24f;
    [SerializeField, Range(0f, 0.03f)] private float movementDeadZone = 0.002f;
    [SerializeField, Range(1f, 2.5f)] private float exclusiveAxisRatio = 1.2f;
    [SerializeField, Range(90f, 175f)] private float minimumIndexAngle = 125f;
    [SerializeField, Range(0.8f, 1.8f)] private float minimumIndexTipExtension = 1.08f;
    [SerializeField, Range(0.5f, 2f)] private float maximumOtherFingerExtension = 1.45f;
    [SerializeField, Range(0.2f, 0.8f)] private float minimumThumbIndexSeparation = 0.42f;

    [Header("Pinch Up Attachments")]
    [SerializeField, Range(0.1f, 1.2f)] private float pinchThreshold = 0.88f;
    [SerializeField, Range(0.2f, 1.6f)] private float pinchReleaseThreshold = 1.20f;
    [SerializeField, Min(1f)] private float leftPinchBoost = 1.15f;
    [SerializeField, Min(1f)] private float pinchLiftSensitivity = 1200f;
    [SerializeField, Min(1f)] private float pinchEase = 26f;
    [SerializeField, Min(1f)] private float shaftEase = 26f;

    [Header("Two Fists Down - Handle")]
    [SerializeField, Min(1f)] private float handleDownSensitivity = 1150f;
    [SerializeField, Min(1f)] private float handleEase = 24f;

    [Header("Right V Up - Bevel")]
    [SerializeField, Min(1f)] private float bevelLiftSensitivity = 920f;
    [SerializeField, Min(1f)] private float bevelEase = 18f;

    [Header("Band Appearance")]
    [Tooltip("The two wrapping rings on Teapot_Updated. Inspected FBX slot 0 is Material.002.")]
    [SerializeField] private Material bandGoldMaterial;
    [SerializeField] private int[] bandMaterialSlotIndices = { 0 };

    private readonly Dictionary<string, int> blendShapeIndices =
        new Dictionary<string, int>();

    private float width;
    private float height;
    private float middle;
    private float targetWidth;
    private float targetHeight;
    private float targetMiddle;
    private float widthHeightBaselineX;
    private float widthHeightBaselineY;
    private float widthHeightStart;
    private float middleBaseline;
    private float middleStart;
    private bool rightPinchHeld;
    private bool leftPinchHeld;
    private bool rightPinchArmed;
    private bool leftPinchArmed;
    private float rightPinchStartY;
    private float leftPinchStartY;
    private float rightPinchStartWeight;
    private float leftPinchStartWeight;
    private float rightPinchGrace;
    private float leftPinchGrace;
    private const float PinchGraceDuration = 0.20f;
    private bool handleDownArmed;
    private float handleDownStartY;
    private float handleStartWeight;
    private bool bevelArmed;
    private float exclusiveReleaseHold;
    private GestureMode pendingExclusive;
    private const float ExclusiveIdleSeconds = 0.08f;
    private float bevelStartY;
    private float bevelStartWeight;
    private int lockedWidthHeightAxis;
    private GestureMode currentGesture;

    private enum GestureMode
    {
        None,
        WidthHeight,
        Middle,
        PinchUp,
        HandleDown,
        BevelUp
    }

    public bool suppressDriving;
    public bool IsDeformationLocked => deformationLocked;
    public bool ownWidthHeight = true;

    private bool externalFistRotationActive;
    private float[] restBlendWeights;
    private Quaternion restLocalRotation = Quaternion.identity;
    private bool practiceRightPinchHeld;
    private bool practiceLeftPinchHeld;
    private bool deformationLocked;
    private bool madeDeformation;
    private bool useLid2;
    private bool useShaft2;
    private bool useHandle2;

    public enum PracticeGesture
    {
        None,
        TwoIndex,
        TwoFists,
        RightPinch,
        LeftPinch,
        RightV,
        TwoV,
        SingleFist,
        OpenPalm,
        PointIndex
    }

    public SculptHandLandmarkerRunner HandRunner => handRunner;
    public TMP_Text GestureStatus => gestureStatus;
    public SkinnedMeshRenderer TeapotRenderer => teapotRenderer;

    /// <summary>
    /// Uses the scene's configured hand-tracking runner without adding a
    /// second camera or changing the Lantern's own mesh/controller setup.
    /// </summary>
    public void SetHandRunner(SculptHandLandmarkerRunner runner)
    {
        handRunner = runner;
        EnsureTeapotFistRotator();
    }

    /// <summary>
    /// Stops teapot sculpt gestures while the one-fist rotator is steering.
    /// Two closed fists still fall through to the handle gesture.
    /// </summary>
    public void SetExternalFistRotationActive(bool active)
    {
        externalFistRotationActive = active;
        if (!active)
            return;

        ResetPinchTracking();
        ResetHandleDownTracking();
        ResetBevelTracking();
        currentGesture = GestureMode.None;
        lockedWidthHeightAxis = 0;
        SetStatus("FIST");
    }

    public void SynchronizeWeightsFromRenderer()
    {
        ReadBodyWeights();
    }

    public void CaptureRestState()
    {
        if (restBlendWeights != null)
            return;
        if (teapotRenderer == null || teapotRenderer.sharedMesh == null)
            return;

        restLocalRotation = teapotRenderer.transform.localRotation;
        Mesh mesh = teapotRenderer.sharedMesh;
        restBlendWeights = new float[mesh.blendShapeCount];
        for (int i = 0; i < restBlendWeights.Length; i++)
            restBlendWeights[i] = teapotRenderer.GetBlendShapeWeight(i);
    }

    public void RestoreRestState()
    {
        if (teapotRenderer == null)
            return;

        teapotRenderer.transform.localRotation = restLocalRotation;
        if (teapotRenderer.sharedMesh != null)
        {
            int count = teapotRenderer.sharedMesh.blendShapeCount;
            for (int i = 0; i < count; i++)
                teapotRenderer.SetBlendShapeWeight(i, 0f);
        }

        ReadBodyWeights();
        ResetPinchTracking();
        ResetHandleDownTracking();
        ResetBevelTracking();
        currentGesture = GestureMode.None;
        lockedWidthHeightAxis = 0;
        externalFistRotationActive = false;
        deformationLocked = false;
        madeDeformation = false;
    }

    public void ArmLidVariant(bool useSecond)
    {
        useLid2 = useSecond;
        ZeroUnusedShape(useLid2 ? "Lid 1" : "Lid 2");
    }

    public void ArmShaftVariant(bool useSecond)
    {
        useShaft2 = useSecond;
        ZeroUnusedShape(useShaft2 ? "Shaft 1" : "Shaft 2");
    }

    public void ArmHandleVariant(bool useSecond)
    {
        useHandle2 = useSecond;
        ZeroUnusedShape(useHandle2 ? "Handle 1" : "Handle 2");
    }

    public void WriteHelpWeights(MorphicCraftHelpState state)
    {
        if (state == null || teapotRenderer == null)
            return;

        state.width = Mathf.RoundToInt(ReadShape("Width"));
        state.height = Mathf.RoundToInt(ReadShape("Height"));
        state.bamboo = Mathf.RoundToInt(ReadShape("Middle"));
        state.topper = Mathf.RoundToInt(ReadShape("Bevel Top and Bottom"));
        state.lidTop = Mathf.RoundToInt(ReadShape(ActiveLidName));
        state.lidBottom = Mathf.RoundToInt(ReadShape(ActiveShaftName));
        state.tassel = Mathf.RoundToInt(ReadShape(ActiveHandleName));
        state.locked = deformationLocked ? 1 : 0;
    }

    private string ActiveLidName => useLid2 ? "Lid 2" : "Lid 1";
    private string ActiveShaftName => useShaft2 ? "Shaft 2" : "Shaft 1";
    private string ActiveHandleName => useHandle2 ? "Handle 2" : "Handle 1";

    private float ReadShape(string exactName)
    {
        return blendShapeIndices.TryGetValue(exactName, out int index)
            ? teapotRenderer.GetBlendShapeWeight(index)
            : 0f;
    }

    private void ZeroUnusedShape(string exactName)
    {
        if (teapotRenderer != null && blendShapeIndices.TryGetValue(exactName, out int index))
            teapotRenderer.SetBlendShapeWeight(index, 0f);
    }

    public PracticeGesture ReadPracticeGesture()
    {
        if (handRunner == null)
            return PracticeGesture.None;

        bool hasTwo = handRunner.TryGetLatestTwoHands(
            out SculptHandFrame first, out SculptHandFrame second);
        if (!hasTwo && !handRunner.TryGetLatestHand(out first))
            return PracticeGesture.None;
        if (!hasTwo)
            second = default;

        bool twoV = hasTwo && IsVSign(first) && IsVSign(second);
        bool twoIndex = !twoV && hasTwo &&
                        IsIndexPointing(first) && IsIndexPointing(second);
        bool twoFists = !twoV && !twoIndex && hasTwo &&
                        LooksLikeHandleFist(first) && LooksLikeHandleFist(second);
        bool rightV = !twoV && !twoIndex && !twoFists &&
                      TryGetRightVHand(first, second, hasTwo, out _);
        bool rotateFist = !hasTwo && !twoV && !twoIndex && !twoFists && !rightV &&
                          LooksLikeRotateFist(first);

        bool rightPinch = false;
        bool leftPinch = false;
        if (!twoV && !twoIndex && !twoFists && !rightV && !rotateFist)
        {
            bool firstRight = IsPhysicalRightHand(first, second, hasTwo);
            if (firstRight)
                rightPinch = IsPinching(first, true, ref practiceRightPinchHeld);
            else
                leftPinch = IsPinching(first, false, ref practiceLeftPinchHeld);
            if (hasTwo)
            {
                bool secondRight = IsPhysicalRightHand(second, first, true);
                if (secondRight)
                    rightPinch = rightPinch || IsPinching(second, true, ref practiceRightPinchHeld);
                else
                    leftPinch = leftPinch || IsPinching(second, false, ref practiceLeftPinchHeld);
            }

            if (hasTwo)
            {
                if (rightPinch && IsCompetingPose(firstRight ? second : first))
                    rightPinch = false;
                if (leftPinch && IsCompetingPose(firstRight ? first : second))
                    leftPinch = false;
            }
        }

        if (twoV)
            return PracticeGesture.TwoV;
        if (twoIndex)
            return PracticeGesture.TwoIndex;
        if (twoFists)
            return PracticeGesture.TwoFists;
        if (rightV)
            return PracticeGesture.RightV;
        if (rotateFist)
            return PracticeGesture.SingleFist;
        if (rightPinch)
            return PracticeGesture.RightPinch;
        if (leftPinch)
            return PracticeGesture.LeftPinch;
        if ((IsIndexPointing(first) || (hasTwo && IsIndexPointing(second))) && !twoIndex)
            return PracticeGesture.PointIndex;
        if (IsOpenPalm(first) || (hasTwo && IsOpenPalm(second)))
            return PracticeGesture.OpenPalm;
        return PracticeGesture.None;
    }

    private void EnsureResponsiveDefaults()
    {
        if (shaftEase < 22f) shaftEase = 26f;
        if (pinchEase < 22f) pinchEase = 26f;
        if (handleEase < 20f) handleEase = 24f;
        if (pinchLiftSensitivity < 1050f) pinchLiftSensitivity = 1200f;
        if (handleDownSensitivity < 950f) handleDownSensitivity = 1150f;
        if (pinchThreshold < 0.82f) pinchThreshold = 0.88f;
        if (pinchReleaseThreshold < 1.10f) pinchReleaseThreshold = 1.20f;
        if (leftPinchBoost < 1.10f) leftPinchBoost = 1.15f;
    }

    private void Awake()
    {
        EnsureResponsiveDefaults();
        if (teapotRenderer == null)
            teapotRenderer = GetComponentInChildren<SkinnedMeshRenderer>(true);

        if (handRunner == null)
            Debug.LogWarning(
                "TeapotGestureController: assign the same configured SculptHandLandmarkerRunner used by the Lantern.",
                this);

        ResolveBlendShapes();
        ApplyBandGoldMaterial();
        ReadBodyWeights();
        middle = targetMiddle = Mathf.Clamp(middle, 0f, middleMaximumWeight);
        SetBlendShapeWeight("Middle", middle);
        EnsureTeapotFistRotator();
        CaptureRestState();
    }

    private void OnValidate()
    {
        EnsureResponsiveDefaults();
    }

    /// <summary>
    /// Teapot_Updated Cylinder.002 slot 0 is the two wrapping rings
    /// (FBX Material.002). Slot 1 / Material.001 is the body.
    /// </summary>
    private void ApplyBandGoldMaterial()
    {
        if (teapotRenderer == null)
            return;

        Material[] slots = teapotRenderer.sharedMaterials;
        if (slots == null || slots.Length == 0)
        {
            Debug.LogError("TeapotGestureController: Teapot_Updated renderer has no material slots.", this);
            return;
        }

        var names = new string[slots.Length];
        for (int i = 0; i < slots.Length; i++)
            names[i] = slots[i] != null ? slots[i].name : "<empty>";
        Debug.Log(
            "TeapotGestureController: Teapot_Updated slots = " + string.Join(", ", names),
            this);

        if (bandGoldMaterial == null)
        {
            Debug.LogError(
                "TeapotGestureController: assign TeapotBandGold to Band Gold Material.",
                this);
            return;
        }

        var bandSlots = new List<int>();
        for (int i = 0; i < slots.Length; i++)
        {
            if (slots[i] == null)
                continue;

            string materialName = slots[i].name.Replace(" (Instance)", string.Empty);
            if (materialName == "Material.002")
                bandSlots.Add(i);
        }

        if (bandSlots.Count == 0 && bandMaterialSlotIndices != null)
        {
            foreach (int slot in bandMaterialSlotIndices)
            {
                if (slot >= 0 && slot < slots.Length && !bandSlots.Contains(slot))
                    bandSlots.Add(slot);
            }
        }

        if (bandSlots.Count == 0)
        {
            Debug.LogError(
                "TeapotGestureController: the two-band slot 0 / Material.002 was not found.",
                this);
            return;
        }

        Material[] next = new Material[slots.Length];
        for (int i = 0; i < slots.Length; i++)
            next[i] = slots[i];

        bool changed = false;
        foreach (int slot in bandSlots)
        {
            if (next[slot] == bandGoldMaterial)
            {
                Debug.Log(
                    $"TeapotGestureController: band slot {slot} already uses {bandGoldMaterial.name}.",
                    this);
                continue;
            }

            next[slot] = bandGoldMaterial;
            changed = true;
            Debug.Log(
                $"TeapotGestureController: assigned {bandGoldMaterial.name} to Teapot_Updated slot {slot}.",
                this);
        }

        if (changed)
            teapotRenderer.sharedMaterials = next;
    }

    private void OnEnable()
    {
        EnsureResponsiveDefaults();
        ApplyBandGoldMaterial();
        EnsureTeapotFistRotator();
        if (gestureStatus != null)
            gestureStatus.gameObject.SetActive(true);
    }

    private void OnDisable()
    {
        if (gestureStatus != null)
            gestureStatus.gameObject.SetActive(false);

        ResetExclusiveTracking();
        currentGesture = GestureMode.None;
        pendingExclusive = GestureMode.None;
        exclusiveReleaseHold = 0f;
        lockedWidthHeightAxis = 0;
    }

    private void Update()
    {
        if (handRunner == null || teapotRenderer == null || suppressDriving)
            return;

        bool hasTwoHands = handRunner.TryGetLatestTwoHands(
            out SculptHandFrame first,
            out SculptHandFrame second);
        if (externalFistRotationActive && !hasTwoHands)
            return;

        bool hasAnyHand = hasTwoHands || handRunner.TryGetLatestHand(out first);
        if (!hasAnyHand)
        {
            bool wasDriving = currentGesture != GestureMode.None;
            ResetExclusiveTracking();
            EndGesture("NO HANDS");
            pendingExclusive = GestureMode.None;
            if (wasDriving)
                exclusiveReleaseHold = ExclusiveIdleSeconds;
            return;
        }

        bool twoV = hasTwoHands && IsVSign(first) && IsVSign(second);
        bool twoIndex = !twoV && hasTwoHands &&
                        IsIndexPointing(first) && IsIndexPointing(second);
        bool twoFists = !twoV && !twoIndex && hasTwoHands &&
                        LooksLikeHandleFist(first) && LooksLikeHandleFist(second);
        SculptHandFrame rightVHand;
        bool foundRightV = TryGetRightVHand(first, second, hasTwoHands, out rightVHand);
        bool rightV = !twoV && !twoIndex && !twoFists && foundRightV;
        bool firstRight = IsPhysicalRightHand(first, second, hasTwoHands);
        bool rotateFist = !hasTwoHands && LooksLikeRotateFist(first);
        bool firstPinch = false;
        bool secondPinch = false;
        if (!twoV && !twoIndex && !twoFists && !rightV && !rotateFist)
        {
            ref bool firstHeld = ref (firstRight ? ref rightPinchHeld : ref leftPinchHeld);
            firstPinch = IsPinching(first, firstRight, ref firstHeld);
            if (hasTwoHands)
            {
                bool secondRight = IsPhysicalRightHand(second, first, true);
                ref bool secondHeld = ref (secondRight ? ref rightPinchHeld : ref leftPinchHeld);
                secondPinch = IsPinching(second, secondRight, ref secondHeld);
            }
        }

        bool firstCompeting = hasTwoHands && IsCompetingPose(second);
        bool secondCompeting = hasTwoHands && IsCompetingPose(first);
        if (firstCompeting)
            firstPinch = false;
        if (secondCompeting)
            secondPinch = false;
        bool pinch = firstPinch || secondPinch;

        GestureMode detected = GestureMode.None;
        if (twoV)
            detected = GestureMode.Middle;
        else if (twoIndex)
            detected = GestureMode.WidthHeight;
        else if (twoFists)
            detected = GestureMode.HandleDown;
        else if (rightV)
            detected = GestureMode.BevelUp;
        else if (pinch)
            detected = GestureMode.PinchUp;

        bool sculptGesture = detected != GestureMode.None;
        bool openPalm = IsOpenPalm(first) || (hasTwoHands && IsOpenPalm(second));
        if (openPalm && madeDeformation && !sculptGesture)
            deformationLocked = true;
        if (sculptGesture)
            deformationLocked = false;

        if (deformationLocked)
        {
            ResetExclusiveTracking();
            currentGesture = GestureMode.None;
            pendingExclusive = GestureMode.None;
            exclusiveReleaseHold = 0f;
            SetStatus("OPEN PALM");
            return;
        }

        if (currentGesture != GestureMode.None && detected != currentGesture)
        {
            ResetExclusiveTracking();
            currentGesture = GestureMode.None;
            pendingExclusive = detected;
            exclusiveReleaseHold = ExclusiveIdleSeconds;
            SetStatus("NO HANDS");
            return;
        }

        if (exclusiveReleaseHold > 0f)
        {
            if (detected != pendingExclusive)
            {
                pendingExclusive = detected;
                exclusiveReleaseHold = ExclusiveIdleSeconds;
            }

            exclusiveReleaseHold -= Time.deltaTime;
            ResetExclusiveTracking();
            currentGesture = GestureMode.None;
            if (exclusiveReleaseHold > 0f)
            {
                SetStatus("NO HANDS");
                return;
            }

            pendingExclusive = GestureMode.None;
        }

        if (detected == GestureMode.WidthHeight)
        {
            ResetPinchTracking();
            ResetHandleDownTracking();
            ResetBevelTracking();
            if (ownWidthHeight)
            {
                if (currentGesture != GestureMode.WidthHeight)
                    BeginWidthHeight(first, second);
                DriveWidthHeight(first, second);
            }

            currentGesture = GestureMode.WidthHeight;
            madeDeformation = true;
            SetStatus("WIDTH & HEIGHT");
        }
        else if (detected == GestureMode.Middle)
        {
            ResetPinchTracking();
            ResetHandleDownTracking();
            ResetBevelTracking();
            if (currentGesture != GestureMode.Middle)
                BeginMiddle(first, second);

            DriveMiddle(first, second);
            madeDeformation = true;
            SetStatus("TWO V");
        }
        else if (detected == GestureMode.HandleDown)
        {
            ResetPinchTracking();
            ResetBevelTracking();
            currentGesture = GestureMode.HandleDown;
            DriveHandleDown(first, second);
            madeDeformation = true;
            SetStatus("TWO FISTS");
        }
        else if (detected == GestureMode.BevelUp)
        {
            ResetPinchTracking();
            ResetHandleDownTracking();
            currentGesture = GestureMode.BevelUp;
            DriveBevelUp(rightVHand);
            madeDeformation = true;
            SetStatus("RIGHT V");
        }
        else if (detected == GestureMode.PinchUp)
        {
            ResetHandleDownTracking();
            ResetBevelTracking();
            bool lidPinching = false;
            bool shaftPinching = false;

            if (firstRight)
                lidPinching |= DrivePinchUp(first, true, firstCompeting);
            else
                shaftPinching |= DrivePinchUp(first, false, firstCompeting);

            if (hasTwoHands)
            {
                bool secondIsRight = IsPhysicalRightHand(second, first, true);
                if (secondIsRight)
                    lidPinching |= DrivePinchUp(second, true, secondCompeting);
                else
                    shaftPinching |= DrivePinchUp(second, false, secondCompeting);
            }

            if (lidPinching || shaftPinching)
            {
                currentGesture = GestureMode.PinchUp;
                madeDeformation = true;
            }

            if (lidPinching && shaftPinching)
                SetStatus("RIGHT PINCH + LEFT PINCH");
            else if (lidPinching)
                SetStatus("RIGHT PINCH");
            else if (shaftPinching)
                SetStatus("LEFT PINCH");
            else
            {
                ResetPinchTracking();
                EndGesture("NO HANDS");
            }
        }
        else
        {
            ResetExclusiveTracking();
            EndGesture("NO HANDS");
        }
    }

    private void LateUpdate()
    {
        if (suppressDriving || teapotRenderer == null)
            return;

        if (ownWidthHeight)
        {
            SetBlendShapeWeight("Width", width);
            SetBlendShapeWeight("Height", height);
        }

        SetBlendShapeWeight("Middle", middle);
    }

    /// <summary>Returns the imported FBX blend-shape index for an exact name.</summary>
    public bool TryGetBlendShapeIndex(string exactName, out int index)
    {
        return blendShapeIndices.TryGetValue(exactName, out index);
    }

    /// <summary>Sets one resolved Teapot blend shape using Unity's 0–100 weight.</summary>
    public bool SetBlendShapeWeight(string exactName, float weight)
    {
        if (teapotRenderer == null ||
            !blendShapeIndices.TryGetValue(exactName, out int index))
        {
            return false;
        }

        float maximum = exactName == "Middle" ? middleMaximumWeight : 100f;
        teapotRenderer.SetBlendShapeWeight(index, Mathf.Clamp(weight, 0f, maximum));
        return true;
    }

    private void ResolveBlendShapes()
    {
        blendShapeIndices.Clear();

        if (teapotRenderer == null)
        {
            Debug.LogError(
                "TeapotGestureController: no SkinnedMeshRenderer was assigned or found under Teapot_Updated.",
                this);
            SetStatus("TEAPOT: RENDERER MISSING");
            return;
        }

        Mesh mesh = teapotRenderer.sharedMesh;
        if (mesh == null)
        {
            Debug.LogError(
                "TeapotGestureController: the assigned SkinnedMeshRenderer has no mesh.",
                this);
            SetStatus("TEAPOT: MESH MISSING");
            return;
        }

        int missingCount = 0;
        foreach (string shapeName in RequiredBlendShapeNames)
        {
            int index = mesh.GetBlendShapeIndex(shapeName);
            if (index < 0)
                index = FindBlendShapeIndex(mesh, shapeName);
            if (index < 0)
            {
                missingCount++;
                Debug.LogWarning(
                    $"TeapotGestureController: blend shape '{shapeName}' was not found on mesh '{mesh.name}'. " +
                    "Check the FBX shape-key spelling and make sure Import BlendShapes is enabled.",
                    this);
                continue;
            }

            blendShapeIndices[shapeName] = index;
        }

        SetStatus(missingCount == 0 ? "NO HANDS" : "NO HANDS");

        Debug.Log(
            $"TeapotGestureController: resolved {blendShapeIndices.Count}/{RequiredBlendShapeNames.Length} " +
            $"Teapot blend shapes on '{mesh.name}'.",
            this);
    }

    private static int FindBlendShapeIndex(Mesh mesh, string shapeName)
    {
        if (mesh == null || string.IsNullOrEmpty(shapeName))
            return -1;

        string wanted = CompactBlendShapeName(shapeName);
        for (int i = 0; i < mesh.blendShapeCount; i++)
        {
            if (CompactBlendShapeName(mesh.GetBlendShapeName(i)) == wanted)
                return i;
        }

        return -1;
    }

    private static string CompactBlendShapeName(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return string.Empty;

        int dot = value.LastIndexOf('.');
        if (dot >= 0 && dot < value.Length - 1)
            value = value.Substring(dot + 1);

        char[] chars = value.ToCharArray();
        int count = 0;
        for (int i = 0; i < chars.Length; i++)
        {
            if (char.IsLetterOrDigit(chars[i]))
                chars[count++] = char.ToUpperInvariant(chars[i]);
        }

        return new string(chars, 0, count);
    }

    private void ReadBodyWeights()
    {
        if (teapotRenderer == null)
            return;

        if (blendShapeIndices.TryGetValue("Width", out int widthIndex))
            width = targetWidth = teapotRenderer.GetBlendShapeWeight(widthIndex);
        if (blendShapeIndices.TryGetValue("Height", out int heightIndex))
            height = targetHeight = teapotRenderer.GetBlendShapeWeight(heightIndex);
        if (blendShapeIndices.TryGetValue("Middle", out int middleIndex))
            middle = targetMiddle = teapotRenderer.GetBlendShapeWeight(middleIndex);
    }

    private void BeginWidthHeight(SculptHandFrame first, SculptHandFrame second)
    {
        currentGesture = GestureMode.WidthHeight;
        DriveWidthHeight(first, second);
    }

    private void DriveWidthHeight(SculptHandFrame first, SculptHandFrame second)
    {
        float gain = SculptHandFrame.CombinedDistanceGain(first, second);
        float spanX = Mathf.Abs(first.IndexTip.x - second.IndexTip.x) * gain;
        float spanY = Mathf.Abs(first.IndexTip.y - second.IndexTip.y) * gain;
        targetWidth = Mathf.Clamp01(Mathf.InverseLerp(0.06f, 0.52f, spanX)) * 100f;
        targetHeight = Mathf.Clamp01(Mathf.InverseLerp(0.04f, 0.42f, spanY)) * 100f;
        float blend = 1f - Mathf.Exp(-smoothing * Time.deltaTime);
        width = Mathf.Lerp(width, targetWidth, blend);
        height = Mathf.Lerp(height, targetHeight, blend);
        SetBlendShapeWeight("Width", width);
        SetBlendShapeWeight("Height", height);
    }

    private void BeginMiddle(SculptHandFrame first, SculptHandFrame second)
    {
        currentGesture = GestureMode.Middle;
        DriveMiddle(first, second);
    }

    private void DriveMiddle(SculptHandFrame first, SculptHandFrame second)
    {
        float spread = VSignSpread(first, second) *
                       SculptHandFrame.CombinedDistanceGain(first, second);
        targetMiddle = Mathf.Clamp(
            Mathf.InverseLerp(0.06f, 0.50f, spread) * middleMaximumWeight,
            0f,
            middleMaximumWeight);
        float blend = 1f - Mathf.Exp(-smoothing * Time.deltaTime);
        middle = Mathf.Lerp(middle, targetMiddle, blend);
        SetBlendShapeWeight("Middle", middle);
    }

    private bool DrivePinchUp(SculptHandFrame hand, bool physicalRight, bool otherHandCompeting)
    {
        ref bool held = ref (physicalRight ? ref rightPinchHeld : ref leftPinchHeld);
        ref bool armed = ref (physicalRight ? ref rightPinchArmed : ref leftPinchArmed);
        ref float startY = ref (physicalRight ? ref rightPinchStartY : ref leftPinchStartY);
        ref float startWeight = ref (physicalRight ? ref rightPinchStartWeight : ref leftPinchStartWeight);
        string shapeName = physicalRight ? ActiveLidName : ActiveShaftName;

        if (!blendShapeIndices.TryGetValue(shapeName, out int index))
            return false;

        if (otherHandCompeting)
        {
            held = false;
            armed = false;
            return false;
        }

        bool pinching = IsPinching(hand, physicalRight, ref held);
        if (!pinching)
        {
            armed = false;
            return false;
        }

        if (!armed)
        {
            armed = true;
            startY = PinchHeight(hand);
            startWeight = teapotRenderer.GetBlendShapeWeight(index);
        }

        float verticalMotion = PinchHeight(hand) - startY;
        float target = Mathf.Clamp(
            startWeight + verticalMotion * pinchLiftSensitivity * hand.DistanceGain,
            0f,
            100f);
        float currentWeight = teapotRenderer.GetBlendShapeWeight(index);
        float ease = physicalRight ? pinchEase : shaftEase;
        float blend = 1f - Mathf.Exp(-ease * Time.deltaTime);
        SetBlendShapeWeight(shapeName, Mathf.Lerp(currentWeight, target, blend));
        return true;
    }

    private bool IsPinching(SculptHandFrame hand, bool physicalRight, ref bool held)
    {
        if (IsOpenPalm(hand) || IsIndexPointing(hand) || IsVSign(hand) ||
            LooksLikeClosedFist(hand) || LooksLikeRotateFist(hand))
        {
            held = false;
            return false;
        }

        float palm = Mathf.Max(hand.PalmWidth, 0.001f);
        float thumbIndex = hand.PinchDistance / palm;
        float start = pinchThreshold;
        float release = Mathf.Max(pinchReleaseThreshold, pinchThreshold + 0.25f);
        if (!physicalRight)
        {
            start *= leftPinchBoost;
            release *= leftPinchBoost;
        }
        else
        {
            start *= 0.86f;
            release *= 0.94f;
        }

        held = held ? thumbIndex < release : thumbIndex < start;
        return held;
    }

    private bool IsOpenPalm(SculptHandFrame hand)
    {
        float palm = Mathf.Max(hand.PalmWidth, 0.001f);
        return hand.PinchDistance >= palm * 0.55f &&
               hand.FingerCurlRatio >= 1.45f &&
               hand.OtherFingersCurlRatio >= 1.33f &&
               hand.IndexTipPalmRatio >= 1.05f &&
               !IsIndexPointing(hand);
    }

    private static bool LooksLikeFist(SculptHandFrame hand)
    {
        float palm = Mathf.Max(hand.PalmWidth, 0.001f);
        if (hand.PinchDistance / palm < 0.65f)
            return false;
        return FingersClosed(hand, 1.18f, 1.08f) && !HasExtendedThumb(hand);
    }

    private static bool LooksLikeClosedFist(SculptHandFrame hand)
    {
        return hand.IndexTipPalmRatio <= 1.04f &&
               hand.MiddleTipPalmRatio <= 1.18f &&
               hand.FingerCurlRatio <= 1.14f &&
               hand.OtherFingersCurlRatio <= 1.12f;
    }

    private static bool LooksLikeRotateFist(SculptHandFrame hand)
    {
        return hand.IndexExtensionAngle < 130f &&
               hand.FingerCurlRatio <= 1.32f &&
               hand.OtherFingersCurlRatio <= 1.28f &&
               hand.IndexTipPalmRatio <= 1.22f;
    }

    private bool IsCompetingPose(SculptHandFrame hand)
    {
        return IsVSign(hand) ||
               IsIndexPointing(hand) ||
               LooksLikeClosedFist(hand) ||
               LooksLikeRotateFist(hand) ||
               LooksLikeFist(hand);
    }

    private static bool FingersClosed(
        SculptHandFrame hand,
        float maximumCurl,
        float maximumIndex)
    {
        return hand.FingerCurlRatio <= maximumCurl &&
               hand.IndexTipPalmRatio <= maximumIndex &&
               hand.OtherFingersCurlRatio <= maximumCurl;
    }

    private static bool HasExtendedThumb(SculptHandFrame hand)
    {
        float palm = Mathf.Max(hand.PalmWidth, 0.001f);
        Vector2 thumb = new Vector2(
            hand.ThumbTip.x - hand.ThumbMcp.x,
            hand.ThumbTip.y - hand.ThumbMcp.y);
        float thumbToPalm = Vector2.Distance(
            new Vector2(hand.ThumbTip.x, hand.ThumbTip.y),
            new Vector2(hand.PalmCenter.x, hand.PalmCenter.y)) / palm;
        return thumbToPalm >= 1.32f &&
               thumb.magnitude >= palm * 0.32f &&
               hand.PinchDistance >= palm * 0.52f &&
               TipDistance(hand.ThumbTip, hand.IndexTip) / palm >= 0.55f &&
               TipDistance(hand.ThumbTip, hand.MiddleTip) / palm >= 0.50f &&
               !ThumbAgainstFingers(hand);
    }

    private static bool ThumbAgainstFingers(SculptHandFrame hand)
    {
        float palm = Mathf.Max(hand.PalmWidth, 0.001f);
        return TipDistance(hand.ThumbTip, hand.IndexTip) / palm < 0.52f ||
               TipDistance(hand.ThumbTip, hand.MiddleTip) / palm < 0.50f;
    }

    private static float TipDistance(Vector3 first, Vector3 second)
    {
        return Vector2.Distance(
            new Vector2(first.x, first.y),
            new Vector2(second.x, second.y));
    }

    private bool IsPhysicalRightHand(
        SculptHandFrame hand,
        SculptHandFrame other,
        bool hasTwoHands)
    {
        if (hand.HasHandedness)
            return !hand.IsRightHand;

        if (hasTwoHands)
            return hand.PalmCenter.x >= other.PalmCenter.x;

        return hand.PalmCenter.x >= 0.5f;
    }

    private static float PinchHeight(SculptHandFrame hand)
    {
        return (hand.ThumbTip.y + hand.IndexTip.y) * 0.5f;
    }

    private void ResetPinchTracking()
    {
        rightPinchHeld = false;
        leftPinchHeld = false;
        rightPinchArmed = false;
        leftPinchArmed = false;
        rightPinchGrace = 0f;
        leftPinchGrace = 0f;
    }

    private static bool LooksLikeHandleFist(SculptHandFrame hand)
    {
        return LooksLikeClosedFist(hand);
    }

    private void DriveHandleDown(SculptHandFrame first, SculptHandFrame second)
    {
        if (!blendShapeIndices.TryGetValue(ActiveHandleName, out int handleIndex))
        {
            return;
        }

        float fistY = (first.PalmCenter.y + second.PalmCenter.y) * 0.5f;
        if (!handleDownArmed)
        {
            handleDownArmed = true;
            handleDownStartY = fistY;
            handleStartWeight = teapotRenderer.GetBlendShapeWeight(handleIndex);
        }

        // Lowering both fists extends the handle; raising them retracts it.
        float target = Mathf.Clamp(
            handleStartWeight + (handleDownStartY - fistY) * handleDownSensitivity,
            0f,
            100f);
        float blend = 1f - Mathf.Exp(-handleEase * Time.deltaTime);
        SetBlendShapeWeight(
            ActiveHandleName,
            Mathf.Lerp(teapotRenderer.GetBlendShapeWeight(handleIndex), target, blend));
    }

    private void ResetHandleDownTracking()
    {
        handleDownArmed = false;
    }

    private void ResetExclusiveTracking()
    {
        ResetPinchTracking();
        ResetHandleDownTracking();
        ResetBevelTracking();
        lockedWidthHeightAxis = 0;
    }

    private bool TryGetRightVHand(
        SculptHandFrame first,
        SculptHandFrame second,
        bool hasTwoHands,
        out SculptHandFrame rightVHand)
    {
        rightVHand = default;
        if (IsVSign(first) && IsPhysicalRightHand(first, second, hasTwoHands))
        {
            rightVHand = first;
            return true;
        }

        if (hasTwoHands && IsVSign(second) && IsPhysicalRightHand(second, first, true))
        {
            rightVHand = second;
            return true;
        }

        return false;
    }

    private void DriveBevelUp(SculptHandFrame hand)
    {
        if (!blendShapeIndices.TryGetValue("Bevel Top and Bottom", out int index))
            return;

        float handY = VSignHeight(hand);
        if (!bevelArmed)
        {
            bevelArmed = true;
            bevelStartY = handY;
            bevelStartWeight = teapotRenderer.GetBlendShapeWeight(index);
        }

        float verticalMotion = handY - bevelStartY;
        float target = Mathf.Clamp(
            bevelStartWeight + verticalMotion * bevelLiftSensitivity,
            0f,
            100f);
        float currentWeight = teapotRenderer.GetBlendShapeWeight(index);
        float blend = 1f - Mathf.Exp(-bevelEase * Time.deltaTime);
        SetBlendShapeWeight(
            "Bevel Top and Bottom",
            Mathf.Lerp(currentWeight, target, blend));
    }

    private void ResetBevelTracking()
    {
        bevelArmed = false;
    }

    private static float VSignHeight(SculptHandFrame hand)
    {
        return (hand.IndexTip.y + hand.MiddleTip.y) * 0.5f;
    }

    private void EndGesture(string status)
    {
        currentGesture = GestureMode.None;
        lockedWidthHeightAxis = 0;
        targetWidth = width;
        targetHeight = height;
        targetMiddle = middle;
        SetStatus(status);
    }

    private float ApplyDeadZone(float value)
    {
        float magnitude = Mathf.Abs(value);
        if (magnitude <= movementDeadZone)
            return 0f;

        return Mathf.Sign(value) * (magnitude - movementDeadZone);
    }

    private bool IsIndexPointing(SculptHandFrame hand)
    {
        return hand.IndexExtensionAngle >= 110f &&
               hand.IndexTipPalmRatio >= 1.05f &&
               hand.MiddleTipPalmRatio < 1.38f &&
               hand.PinchDistance >= hand.PalmWidth * 0.28f &&
               !LooksLikeFist(hand);
    }

    private static bool IsVSign(SculptHandFrame hand)
    {
        return hand.IndexTipPalmRatio >= 1.08f &&
               hand.MiddleTipPalmRatio >= 1.52f &&
               hand.RingTipPalmRatio < 1.28f &&
               hand.PinkyTipPalmRatio < 1.28f &&
               hand.PinchDistance >= hand.PalmWidth * 0.32f;
    }

    private static float VSignSpread(SculptHandFrame first, SculptHandFrame second)
    {
        float firstX = (first.IndexTip.x + first.MiddleTip.x) * 0.5f;
        float secondX = (second.IndexTip.x + second.MiddleTip.x) * 0.5f;
        return Mathf.Abs(secondX - firstX);
    }

    private void SetStatus(string message)
    {
        if (gestureStatus != null)
            MorphicLoc.Apply(gestureStatus, message);
    }

    private void EnsureTeapotFistRotator()
    {
        TeapotFistRotationController rotator = GetComponent<TeapotFistRotationController>();
        if (rotator == null)
            rotator = gameObject.AddComponent<TeapotFistRotationController>();
        rotator.BindFrom(handRunner, teapotRenderer, this);
    }
}
