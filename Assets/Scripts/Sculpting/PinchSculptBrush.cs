using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(MeshFilter))]
public class PinchSculptBrush : MonoBehaviour
{
    [Header("Sculpt Mode")]
    [SerializeField] private SculptToolMode sculptMode =
        SculptToolMode.Inflate;

    [Header("Tool Strength")]
    [Range(0.01f, 1f)]
    [SerializeField] private float inflateStrength = 0.18f;

    [Range(0.01f, 1f)]
    [SerializeField] private float indentStrength = 0.18f;

    [Range(0.01f, 1f)]
    [SerializeField] private float smoothStrength = 0.30f;

    [Range(0.01f, 1f)]
    [SerializeField] private float grabStrength = 1.0f;

    [Range(0.01f, 1f)]
    [SerializeField] private float flattenStrength = 0.30f;

    [Header("Crease")]
    [Range(0.01f, 1f)]
    [SerializeField] private float creaseStrength = 0.35f;

    // Fraction of the brush radius that forms the groove itself.
    // Smaller = a narrower, harder line.
    [Range(0.05f, 0.9f)]
    [SerializeField] private float creaseWidth = 0.35f;

    // Height of the raised shoulders flanking the groove, as a
    // fraction of the groove depth. This is what turns a dent into
    // a ridge with a sharp crease running down its middle.
    [Range(0f, 2f)]
    [SerializeField] private float creaseShoulder = 0.6f;

    // Per-frame strength of the carve, as a fraction of the brush
    // radius applied per second.
    [Range(0.05f, 3f)]
    [SerializeField] private float creaseRate = 0.8f;

    [Header("Stretch")]
    // How strongly clay follows the drag direction.
    [Range(0.01f, 1f)]
    [SerializeField] private float stretchStrength = 0.5f;

    // Fraction of the brush radius that actually stretches. Small
    // values pull a narrow strand (good for loops and handles);
    // large values smear a wide area.
    [Range(0.05f, 1f)]
    [SerializeField] private float stretchFalloffWidth = 0.4f;

    // Counteracts the clay thinning as it is pulled, so long
    // stretches stay thick instead of degenerating into a thread.
    [Range(0f, 1f)]
    [SerializeField] private float stretchVolumeKeep = 0.5f;

    // How far clay may travel from its start position, as a
    // multiple of the brush radius.
    [Range(1f, 10f)]
    [SerializeField] private float stretchMaxLengthFactor = 4f;

    // Growth while the pinch is held still, per second. Lets clay
    // be drawn out into a loop by holding rather than by moving the
    // hand, which is hard to do with hand tracking.
    [Range(0f, 3f)]
    [SerializeField] private float stretchHoldGrowth = 1.0f;

    // Seconds of holding before growth kicks in, so a quick tap
    // does not leave a lump.
    [Range(0f, 2f)]
    [SerializeField] private float stretchHoldDelay = 0.25f;

    // Seconds for growth to reach full strength.
    [Range(0.1f, 5f)]
    [SerializeField] private float stretchHoldRampSeconds = 1.2f;

    [Header("Cursor Colors")]
    [SerializeField] private Color inflateCursorColor =
        Color.cyan;

    [SerializeField] private Color indentCursorColor =
        new Color(1f, 0.45f, 0f);

    [SerializeField] private Color smoothCursorColor =
        new Color(0.65f, 0.25f, 0.95f);

    [SerializeField] private Color grabCursorColor =
        new Color(0.2f, 0.85f, 0.3f);

    [SerializeField] private Color flattenCursorColor =
        new Color(1f, 0.2f, 0.65f);

    [SerializeField] private Color creaseCursorColor =
        new Color(0.95f, 0.85f, 0.25f);

    [SerializeField] private Color stretchCursorColor =
        new Color(0.35f, 0.95f, 0.95f);

    [Header("Tracking")]
    [SerializeField] private PinchDetector pinchDetector;

    [Header("Brush")]
    [SerializeField] private float brushRadius = 0.30f;
    [SerializeField] private float sculptSpeed = 0.18f;

    [Header("Hand Mapping")]
    [Range(0.1f, 2.0f)]
    [SerializeField] private float handMappingRange = 1.25f;

    [Header("Brush Cursor")]
    [SerializeField] private bool showBrushCursor = true;

    [SerializeField] private float cursorSize = 0.12f;

    [SerializeField] private float cursorCameraOffset = 0.75f;

    [SerializeField] private float cursorSurfaceClearance = 0.15f;

    [SerializeField] private float cursorHoldSeconds = 0.80f;

    [Header("Cursor Smoothing")]
    [SerializeField] private float cursorSmoothTime = 0.012f;

    [SerializeField] private float cursorMaxSmoothSpeed = 100f;

    [Header("Symmetry")]
    [SerializeField] private bool enableSymmetry = false;

    [Header("Other Controls")]
    [SerializeField] private TwoHandScaleSculpture twoHandScale;

    [SerializeField] private MeshHistory meshHistory;

    [Header("AI Behavior Tracking")]
    [SerializeField] private MorphicAIBehaviorTracker behaviorTracker;

    [Header("Debug")]
    [SerializeField] private bool showDebugOverlay = false;

    // A vertex this far from its starting position is treated as
    // blown out and snapped back. Sized well above any legitimate
    // sculpt stroke, but far below the spikes a bad frame creates.
    [SerializeField] private float maxVertexDisplacement = 2.0f;

    private bool lastRayHit;
    private int lastAffectedGroups;
    private Vector3 lastBrushWorld;

    private Mesh sculptMesh;

    private Vector3[] vertices;

    private Vector3[] initialVertices;

    private List<int[]> weldedVertexGroups;





    private Collider clayCollider;

    private readonly RaycastHit[] surfaceHits = new RaycastHit[8];

    private GameObject brushCursor;

    private GameObject mirroredBrushCursor;

    private Material cursorMaterial;

    private bool hasLastCursorPosition;

    private Vector3 lastCursorPosition;

    private Vector3 lastCursorNormal;

    private float lastTrackingTime;

    private Vector3 prevPrimaryLocalPos;

    private bool wasPinching;

    // Seconds the current pinch has been held, and the direction the
    // hand was last seen moving. Lets Stretch keep drawing clay out
    // while the hand is held still, which is far easier than
    // pulling a long loop with hand tracking.
    private float pinchHoldSeconds;

    private Vector3 lastMoveDirection = Vector3.zero;

    private Vector3 smoothedCursorPosition;

    private Vector3 smoothedCursorVelocity;

    private bool hasSmoothedCursorPosition;

    public SculptToolMode CurrentSculptMode => sculptMode;

    public bool EnableSymmetry => enableSymmetry;

    public float BrushRadius => brushRadius;

    public void SetBrushRadius(float radius)
    {
        brushRadius = Mathf.Clamp(radius, 0.05f, 1.0f);
    }

    public void AdjustBrushRadius(float delta)
    {
        SetBrushRadius(brushRadius + delta);
    }

    public float GetCurrentToolStrength()
    {
        switch (sculptMode)
        {
            case SculptToolMode.Inflate:
                return inflateStrength;

            case SculptToolMode.Indent:
                return indentStrength;

            case SculptToolMode.Smooth:
                return smoothStrength;

            case SculptToolMode.Grab:
                return grabStrength;

            case SculptToolMode.Flatten:
                return flattenStrength;

            case SculptToolMode.Crease:
                return creaseStrength;

            case SculptToolMode.Stretch:
                return stretchStrength;

            default:
                return inflateStrength;
        }
    }

    public void AdjustCurrentToolStrength(float amount)
    {
        switch (sculptMode)
        {
            case SculptToolMode.Inflate:

                inflateStrength = Mathf.Clamp(
                    inflateStrength + amount,
                    0.01f,
                    1f
                );

                break;

            case SculptToolMode.Indent:

                indentStrength = Mathf.Clamp(
                    indentStrength + amount,
                    0.01f,
                    1f
                );

                break;

            case SculptToolMode.Smooth:

                smoothStrength = Mathf.Clamp(
                    smoothStrength + amount,
                    0.01f,
                    1f
                );

                break;

            case SculptToolMode.Grab:

                grabStrength = Mathf.Clamp(
                    grabStrength + amount,
                    0.01f,
                    1f
                );

                break;

            case SculptToolMode.Flatten:

                flattenStrength = Mathf.Clamp(
                    flattenStrength + amount,
                    0.01f,
                    1f
                );

                break;

            case SculptToolMode.Crease:

                creaseStrength = Mathf.Clamp(
                    creaseStrength + amount,
                    0.01f,
                    1f
                );

                break;

            case SculptToolMode.Stretch:

                stretchStrength = Mathf.Clamp(
                    stretchStrength + amount,
                    0.01f,
                    1f
                );

                break;
        }
    }

    public void RefreshMesh(Mesh newMesh)
    {
        if (newMesh == null)
        {
            return;
        }

        if (!EnsureMeshIsWritable(newMesh))
        {
            return;
        }

        sculptMesh = newMesh;

        vertices = sculptMesh.vertices;

        initialVertices =
            new Vector3[vertices.Length];

        System.Array.Copy(
            vertices,
            initialVertices,
            vertices.Length
        );

        BuildWeldedVertexGroups();

        prevPrimaryLocalPos =
            Vector3.zero;

        wasPinching = false;

        Debug.Log(
            "Sculpt brush refreshed for mesh: " +
            sculptMesh.name
        );
    }

    /// <summary>
    /// Unity refuses CPU vertex access on meshes imported with
    /// "Read/Write Enabled" turned off. Sculpting then fails silently every
    /// frame, so surface it loudly with the exact fix instead.
    /// </summary>
    private bool EnsureMeshIsWritable(Mesh mesh)
    {
        if (mesh == null)
        {
            return false;
        }

        if (!mesh.isReadable)
        {
            Debug.LogError(
                "PinchSculptBrush cannot sculpt '" + mesh.name +
                "' because Read/Write is disabled on that mesh. " +
                "Fix: select the source model, open the Model tab, tick " +
                "'Read/Write Enabled', then press Apply.",
                this
            );

            return false;
        }

        return true;
    }

    private void Awake()
    {
        enableSymmetry = false;

        sculptMesh =
            GetComponent<MeshFilter>().mesh;

        if (!EnsureMeshIsWritable(sculptMesh))
        {
            enabled = false;

            return;
        }

        vertices =
            sculptMesh.vertices;

        initialVertices =
            new Vector3[vertices.Length];

        System.Array.Copy(
            vertices,
            initialVertices,
            vertices.Length
        );

        BuildWeldedVertexGroups();

        if (clayCollider == null)
        {
            clayCollider = GetComponent<Collider>();
        }

        if (pinchDetector == null)
        {
            pinchDetector =
                FindFirstObjectByType<PinchDetector>();
        }

        if (twoHandScale == null)
        {
            twoHandScale =
                GetComponent<TwoHandScaleSculpture>();
        }

        if (meshHistory == null)
        {
            meshHistory =
                GetComponent<MeshHistory>();
        }

        if (behaviorTracker == null)
        {
            behaviorTracker =
                FindFirstObjectByType<MorphicAIBehaviorTracker>();
        }

        CreateBrushCursor();
    }

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.F1))
        {
            showDebugOverlay = !showDebugOverlay;
        }

        CheckKeyboardModeSwitch();

        if (pinchDetector == null)
        {
            pinchDetector =
                FindFirstObjectByType<PinchDetector>();
        }

        if (twoHandScale == null)
        {
            twoHandScale =
                GetComponent<TwoHandScaleSculpture>();
        }

        if (meshHistory == null)
        {
            meshHistory =
                GetComponent<MeshHistory>();
        }

        if (twoHandScale != null &&
            twoHandScale.IsDualPinching)
        {
            wasPinching = false;

            SetCursorVisible(false);

            return;
        }

        if (pinchDetector != null &&
            pinchDetector.IsTracking)
        {
            GetBrushPoint(
                out Vector3 worldBrushPosition,
                out Vector3 worldBrushNormal
            );

            lastCursorPosition =
                worldBrushPosition;

            lastCursorNormal =
                worldBrushNormal;

            lastTrackingTime =
                Time.unscaledTime;

            hasLastCursorPosition = true;

            UpdateBrushCursor(
                worldBrushPosition,
                worldBrushNormal
            );

            if (pinchDetector.IsPinching)
            {
                if (!wasPinching)
                {
                    // Fresh pinch: reset the hold timer and the
                    // remembered direction so growth starts from
                    // this gesture, not the previous one.
                    pinchHoldSeconds = 0f;
                    lastMoveDirection = Vector3.zero;

                    if (meshHistory != null)
                    {
                        meshHistory.SaveState();
                    }

                    if (behaviorTracker != null)
                    {
                        behaviorTracker.RecordStroke();
                    }
                }

                SculptAt(
                    worldBrushPosition
                );

                wasPinching = true;
            }
            else
            {
                wasPinching = false;
            }

            return;
        }

        wasPinching = false;

        if (hasLastCursorPosition &&
            Time.unscaledTime -
            lastTrackingTime <=
            cursorHoldSeconds)
        {
            UpdateBrushCursor(
                lastCursorPosition,
                lastCursorNormal
            );
        }
        else
        {
            SetCursorVisible(false);

            hasSmoothedCursorPosition = false;

            smoothedCursorVelocity =
                Vector3.zero;
        }
    }

    public void SetSculptMode(
        SculptToolMode mode
    )
    {
        sculptMode = mode;

        if (behaviorTracker != null)
        {
            behaviorTracker.SetCurrentTool(
                mode.ToString()
            );
        }

        UpdateCursorColor();
    }

    public void ToggleSculptMode()
    {
        if (sculptMode ==
            SculptToolMode.Inflate)
        {
            sculptMode =
                SculptToolMode.Indent;
        }
        else if (
            sculptMode ==
            SculptToolMode.Indent)
        {
            sculptMode =
                SculptToolMode.Smooth;
        }
        else if (
            sculptMode ==
            SculptToolMode.Smooth)
        {
            sculptMode =
                SculptToolMode.Grab;
        }
        else if (
            sculptMode ==
            SculptToolMode.Grab)
        {
            sculptMode =
                SculptToolMode.Flatten;
        }
        else if (
            sculptMode ==
            SculptToolMode.Flatten)
        {
            sculptMode =
                SculptToolMode.Crease;
        }
        else if (
            sculptMode ==
            SculptToolMode.Crease)
        {
            sculptMode =
                SculptToolMode.Stretch;
        }
        else
        {
            sculptMode =
                SculptToolMode.Inflate;
        }

        UpdateCursorColor();
    }

    public void ToggleSymmetry()
    {
        enableSymmetry =
            !enableSymmetry;
    }

    public void ResetMesh()
    {
        if (initialVertices == null ||
            vertices == null)
        {
            return;
        }

        System.Array.Copy(
            initialVertices,
            vertices,
            initialVertices.Length
        );

        sculptMesh.vertices =
            vertices;

        sculptMesh.RecalculateNormals();

        sculptMesh.RecalculateBounds();
    }

    private void CheckKeyboardModeSwitch()
    {
        if (Input.GetKeyDown(KeyCode.Alpha1))
        {
            SetSculptMode(
                SculptToolMode.Inflate
            );
        }
        else if (
            Input.GetKeyDown(
                KeyCode.Alpha2
            ))
        {
            SetSculptMode(
                SculptToolMode.Indent
            );
        }
        else if (
            Input.GetKeyDown(
                KeyCode.Alpha3
            ))
        {
            SetSculptMode(
                SculptToolMode.Smooth
            );
        }
        else if (
            Input.GetKeyDown(
                KeyCode.Alpha4
            ))
        {
            SetSculptMode(
                SculptToolMode.Grab
            );
        }
        else if (
            Input.GetKeyDown(
                KeyCode.Alpha5
            ))
        {
            SetSculptMode(
                SculptToolMode.Flatten
            );
        }
        else if (
            Input.GetKeyDown(
                KeyCode.Alpha6
            ))
        {
            SetSculptMode(
                SculptToolMode.Crease
            );
        }
        else if (
            Input.GetKeyDown(
                KeyCode.Alpha8
            ))
        {
            SetSculptMode(
                SculptToolMode.Stretch
            );
        }
        else if (
            Input.GetKeyDown(
                KeyCode.LeftBracket
            ))
        {
            AdjustBrushRadius(
                -0.05f
            );
        }
        else if (
            Input.GetKeyDown(
                KeyCode.RightBracket
            ))
        {
            AdjustBrushRadius(
                0.05f
            );
        }
        else if (
            Input.GetKeyDown(
                KeyCode.Tab
            ))
        {
            ToggleSculptMode();
        }
        else if (
            Input.GetKeyDown(
                KeyCode.S
            ))
        {
            ToggleSymmetry();
        }
        else if (
            Input.GetKeyDown(
                KeyCode.R
            ))
        {
            ResetMesh();
        }
    }

    private void GetBrushPoint(
        out Vector3 worldPosition,
        out Vector3 worldNormal
    )
    {
        Vector3 indexTip =
            pinchDetector.SmoothedIndexTip;

        Camera mainCamera =
            Camera.main;

        if (mainCamera == null)
        {
            worldPosition =
                transform.position;

            worldNormal =
                Vector3.forward;

            return;
        }

        Vector2 handPosition =
            new Vector2(
                (indexTip.x - 0.5f) * 2f,
                (indexTip.y - 0.5f) * 2f
            ) *
            handMappingRange;

        handPosition.x =
            Mathf.Clamp(
                handPosition.x,
                -1f,
                1f
            );

        handPosition.y =
            Mathf.Clamp(
                handPosition.y,
                -1f,
                1f
            );

        float mappedX =
            handPosition.x *
            Mathf.Sqrt(
                Mathf.Max(
                    0f,
                    1f -
                    handPosition.y *
                    handPosition.y *
                    0.5f
                )
            );

        float mappedY =
            handPosition.y *
            Mathf.Sqrt(
                Mathf.Max(
                    0f,
                    1f -
                    handPosition.x *
                    handPosition.x *
                    0.5f
                )
            );

        handPosition =
            new Vector2(
                mappedX,
                mappedY
            );

        /*
         * Preferred: aim at the REAL clay surface.
         *
         * The analytic sphere below assumes a ball-shaped mesh. Its radius is
         * the mesh half-width, so the top and bottom of a taller shape (a
         * lantern, a vase) sit outside the brush's reach and simply cannot be
         * sculpted. Casting against the actual collider puts the brush exactly
         * on the visible surface for ANY shape. If the ray misses the clay we
         * fall back to the original sphere maths.
         */
        if (clayCollider != null &&
            mainCamera != null)
        {
            Vector3 viewportPoint = new Vector3(
                handPosition.x * 0.5f + 0.5f,
                handPosition.y * 0.5f + 0.5f,
                0f
            );

            Ray surfaceRay =
                mainCamera.ViewportPointToRay(viewportPoint);

            int hitCount = Physics.RaycastNonAlloc(
                surfaceRay,
                surfaceHits,
                200f
            );

            for (int i = 0; i < hitCount; i++)
            {
                if (surfaceHits[i].collider == clayCollider)
                {
                    worldPosition = surfaceHits[i].point;
                    worldNormal = surfaceHits[i].normal;

                    lastRayHit = true;
                    lastBrushWorld = worldPosition;

                    return;
                }
            }
        }

        lastRayHit = false;
        lastBrushWorld = Vector3.zero;

        float meshRadius =
            sculptMesh.bounds.extents.x *
            transform.lossyScale.x;

        if (meshRadius <= 0.001f)
        {
            meshRadius = 0.5f;
        }

        float depth =
            Mathf.Sqrt(
                Mathf.Max(
                    0f,
                    1f -
                    handPosition.sqrMagnitude
                )
            ) *
            meshRadius;

        worldPosition =
            transform.position +
            mainCamera.transform.right *
                (
                    handPosition.x *
                    meshRadius
                ) +
            mainCamera.transform.up *
                (
                    handPosition.y *
                    meshRadius
                ) -
            mainCamera.transform.forward *
                depth;

        worldNormal =
            (
                worldPosition -
                transform.position
            ).normalized;
    }

    private void BuildWeldedVertexGroups()
    {
        Dictionary<Vector3Int, List<int>>
            groups =
            new Dictionary<Vector3Int, List<int>>();

        for (
            int i = 0;
            i < vertices.Length;
            i++
        )
        {
            Vector3 vertex =
                vertices[i];

            Vector3Int key =
                new Vector3Int(
                    Mathf.RoundToInt(
                        vertex.x *
                        100000f
                    ),
                    Mathf.RoundToInt(
                        vertex.y *
                        100000f
                    ),
                    Mathf.RoundToInt(
                        vertex.z *
                        100000f
                    )
                );

            if (!groups.ContainsKey(key))
            {
                groups[key] =
                    new List<int>();
            }

            groups[key].Add(i);
        }

        weldedVertexGroups =
            new List<int[]>();

        foreach (
            List<int> group
            in groups.Values
        )
        {
            weldedVertexGroups.Add(
                group.ToArray()
            );
        }
    }
    private void SculptAt(
        Vector3 worldBrushPosition
    )
    {
        Vector3 primaryLocalPos =
            transform.InverseTransformPoint(
                worldBrushPosition
            );

        Vector3 localDelta =
            Vector3.zero;

        if (wasPinching)
        {
            localDelta =
                primaryLocalPos -
                prevPrimaryLocalPos;
        }

        // Remember which way the hand was last travelling so a held
        // pinch can keep growing along that direction.
        if (localDelta.sqrMagnitude > 1e-8f)
        {
            lastMoveDirection =
                localDelta.normalized;
        }

        pinchHoldSeconds += Time.deltaTime;

        if (showDebugOverlay)
        {
            lastAffectedGroups =
                CountGroupsInRange(primaryLocalPos);
        }

        ApplySculptPass(
            primaryLocalPos,
            localDelta
        );

        if (enableSymmetry)
        {
            Vector3 mirroredLocalPos =
                new Vector3(
                    -primaryLocalPos.x,
                    primaryLocalPos.y,
                    primaryLocalPos.z
                );

            Vector3 mirroredLocalDelta =
                new Vector3(
                    -localDelta.x,
                    localDelta.y,
                    localDelta.z
                );

            ApplySculptPass(
                mirroredLocalPos,
                mirroredLocalDelta
            );
        }

        prevPrimaryLocalPos =
            primaryLocalPos;

        SanitizeSculptedVertices();

        sculptMesh.vertices =
            vertices;

        sculptMesh.RecalculateNormals();

        sculptMesh.RecalculateBounds();
    }

    /// <summary>
    /// Rejects non-finite or absurdly displaced vertices. Once a
    /// NaN enters the vertex array every later pass reads it back
    /// and spreads it, which is what tears the mesh into spikes.
    /// </summary>
    private void SanitizeSculptedVertices()
    {
        if (vertices == null ||
            initialVertices == null)
        {
            return;
        }

        for (
            int i = 0;
            i < vertices.Length;
            i++)
        {
            Vector3 candidate =
                vertices[i];

            bool invalid =
                float.IsNaN(candidate.x) ||
                float.IsNaN(candidate.y) ||
                float.IsNaN(candidate.z) ||
                float.IsInfinity(candidate.x) ||
                float.IsInfinity(candidate.y) ||
                float.IsInfinity(candidate.z);

            Vector3 fallback =
                i < initialVertices.Length
                    ? initialVertices[i]
                    : Vector3.zero;

            if (!invalid &&
                Vector3.Distance(
                    candidate,
                    fallback
                ) <= maxVertexDisplacement)
            {
                continue;
            }

            if (invalid)
            {
                Debug.LogWarning(
                    "Sculpt brush discarded an invalid vertex at " +
                    "index " + i +
                    "; restored it to stop the mesh tearing.",
                    this
                );
            }

            vertices[i] = fallback;
        }
    }

    private int CountGroupsInRange(Vector3 localBrushPosition)
    {
        float localRadius =
            brushRadius /
            transform.lossyScale.x;

        int count = 0;

        foreach (int[] group in weldedVertexGroups)
        {
            float distance = Vector3.Distance(
                vertices[group[0]],
                localBrushPosition
            );

            if (distance <= localRadius)
            {
                count++;
            }
        }

        return count;
    }

    private void OnGUI()
    {
        if (!showDebugOverlay)
        {
            return;
        }

        GUILayout.BeginArea(
            new Rect(12, 12, 450, 300),
            GUI.skin.box
        );

        GUILayout.Label("MORPHIC BRUSH DEBUG   (F1 closes)");
        GUILayout.Space(4);

        if (pinchDetector == null)
        {
            GUILayout.Label(
                "pinchDetector : NULL   <-- PROBLEM"
            );
        }
        else
        {
            GUILayout.Label(
                "Tracking : " + pinchDetector.IsTracking
            );

            GUILayout.Label(
                "PINCHING : " + pinchDetector.IsPinching
                + "   (must be True)"
            );

            GUILayout.Label(
                "IsFist   : " + pinchDetector.IsFist
            );

            GUILayout.Label(
                "PinchDist: "
                + pinchDetector.PinchDistance.ToString("0.0000")
                + "  (smaller = closer)"
            );

            GUILayout.Label(
                "FistDist : "
                + pinchDetector.FistDistance.ToString("0.000")
                + "  (curl ratio)"
            );
        }

        GUILayout.Space(6);

        GUILayout.Label(
            "Mesh     : "
            + (sculptMesh != null ? sculptMesh.name : "NULL")
            + "  ("
            + (sculptMesh != null
                ? sculptMesh.vertexCount
                : 0)
            + " verts)"
        );

        GUILayout.Label(
            "Collider : "
            + (clayCollider != null
                ? clayCollider.GetType().Name
                : "NULL")
        );

        GUILayout.Label("Ray hit  : " + lastRayHit);

        GUILayout.Label(
            "Verts in brush : " + lastAffectedGroups
        );

        GUILayout.Label(
            "brushRadius: " + brushRadius
            + "  lossyScale.x: "
            + transform.lossyScale.x.ToString("0.000")
        );

        GUILayout.Label("Mode     : " + sculptMode);

        GUILayout.EndArea();
    }

    /// <summary>
    /// Drags clay along the pinch movement direction with a narrow
    /// falloff, so a small region is pulled out into a long strand
    /// (a loop or handle) instead of the whole area sliding rigidly
    /// the way Grab does.
    ///
    /// Two details make loops possible:
    /// the pull is confined to a narrow core so only a strand
    /// moves, and clay already displaced is allowed to travel
    /// further than untouched clay, which lets a strand grow
    /// further and further instead of dragging its base along.
    /// </summary>
    private void ApplyStretchPass(
        Vector3 localBrushPosition,
        Vector3 localDelta,
        float localRadius,
        Vector3[] normals
    )
    {
        if (localDelta.sqrMagnitude <= 1e-10f)
        {
            // Hand held still. Grow along the last travel
            // direction (or outward along the surface normal if the
            // hand has not moved yet) so holding draws the clay out
            // instead of doing nothing.
            Vector3 holdDelta =
                GetStretchHoldGrowth(
                    localBrushPosition,
                    localRadius,
                    normals
                );

            localDelta = holdDelta;
        }

        if (localDelta.sqrMagnitude <= 1e-10f)
        {
            return;
        }

        float maxTravel =
            localRadius *
            stretchMaxLengthFactor;

        foreach (
            int[] group
            in weldedVertexGroups
        )
        {
            int firstVertexIndex =
                group[0];

            Vector3 currentPos =
                vertices[firstVertexIndex];

            float distance =
                Vector3.Distance(
                    currentPos,
                    localBrushPosition
                );

            // Only a narrow core is pulled. The rest of the
            // surface stays put so the strand keeps its anchor.
            float coreRadius =
                localRadius *
                stretchFalloffWidth;

            if (distance >
                coreRadius)
            {
                continue;
            }

            float falloff =
                1f -
                distance /
                Mathf.Max(
                    coreRadius,
                    0.0001f
                );

            // Smooth the core edge so the strand does not end in a
            // hard step.
            falloff = falloff * falloff *
                (3f - 2f * falloff);

            // How far this vertex already sits from where the clay
            // started. Vertices that have been carried along the
            // strand keep moving; the base stays put, so length
            // grows from the tip outward.
            float carried =
                Vector3.Distance(
                    currentPos,
                    initialVertices[
                        firstVertexIndex
                    ]
                );

            // Ramp in over the first part of the strand, then allow
            // full travel. Without this ramp every pulled vertex
            // advances equally and the whole core just slides.
            float travelScale =
                Mathf.Clamp01(
                    carried /
                    Mathf.Max(
                        localRadius,
                        0.0001f
                    )
                );

            travelScale =
                0.25f +
                0.75f * travelScale;

            // Stop pulling a vertex once it is fully extended, so a
            // long hold cannot fling clay to infinity.
            if (carried >= maxTravel)
            {
                continue;
            }

            float remaining =
                1f -
                carried / maxTravel;

            Vector3 movement =
                localDelta *
                stretchStrength *
                falloff *
                travelScale *
                remaining;

            // Volume preservation: clay thinning into a thread is
            // the usual failure mode when pulling loops. Widening
            // the region slightly as it is drawn out counteracts it.
            if (stretchVolumeKeep > 0f)
            {
                Vector3 groupNormal =
                    Vector3.zero;

                foreach (
                    int vertexIndex
                    in group
                ) {
                    groupNormal +=
                        normals[vertexIndex];
                }

                if (groupNormal.sqrMagnitude > 1e-8f)
                {
                    groupNormal.Normalize();

                    // Bulge perpendicular to the pull, scaled by how
                    // far the strand has been drawn out.
                    Vector3 along =
                        Vector3.Dot(
                            movement,
                            groupNormal
                        ) * groupNormal;

                    Vector3 sideways =
                        movement - along;

                    float bulge =
                        sideways.magnitude *
                        stretchVolumeKeep *
                        travelScale;

                    if (bulge > 1e-8f)
                    {
                        movement +=
                            sideways.normalized *
                            bulge;
                    }
                }
            }

            foreach (
                int vertexIndex
                in group
            ) {
                vertices[vertexIndex] += movement;
            }
        }
    }

    /// <summary>
    /// Pseudo-movement produced while the pinch is held still, so
    /// clay keeps being drawn out without moving the hand. Ramps in
    /// after a short delay and scales with stretchHoldGrowth.
    /// </summary>
    private Vector3 GetStretchHoldGrowth(
        Vector3 localBrushPosition,
        float localRadius,
        Vector3[] normals
    )
    {
        if (stretchHoldGrowth <= 0f)
        {
            return Vector3.zero;
        }

        float held =
            pinchHoldSeconds -
            stretchHoldDelay;

        if (held <= 0f)
        {
            return Vector3.zero;
        }

        // Ease in so growth starts gently rather than jerking.
        float ramp =
            Mathf.Clamp01(
                held /
                Mathf.Max(
                    stretchHoldRampSeconds,
                    0.01f
                )
            );

        float step =
            stretchHoldGrowth *
            ramp *
            Time.deltaTime *
            localRadius;

        if (step <= 0f)
        {
            return Vector3.zero;
        }

        // Prefer the direction the hand was last travelling, so a
        // held pinch keeps extending the strand the way it was
        // already being pulled.
        if (lastMoveDirection.sqrMagnitude > 0.5f)
        {
            return lastMoveDirection * step;
        }

        // Hand never moved: push outward along the surface normal
        // at the brush centre.
        Vector3 outward =
            Vector3.zero;

        foreach (
            int[] group
            in weldedVertexGroups
        )
        {
            int firstVertexIndex =
                group[0];

            if (Vector3.Distance(
                    vertices[firstVertexIndex],
                    localBrushPosition
                ) > localRadius)
            {
                continue;
            }

            foreach (
                int vertexIndex
                in group
            ) {
                outward += normals[vertexIndex];
            }
        }

        if (outward.sqrMagnitude <= 1e-8f)
        {
            return Vector3.zero;
        }

        return outward.normalized * step;
    }

    private void ApplySculptPass(
        Vector3 localBrushPosition,
        Vector3 localDelta
    )
    {
        float localRadius =
            brushRadius /
            transform.lossyScale.x;

        float localStrength =
            sculptSpeed *
            Time.deltaTime /
            transform.lossyScale.x;

        Vector3[] normals =
            sculptMesh.normals;

        if (sculptMode ==
            SculptToolMode.Stretch)
        {
            ApplyStretchPass(
                localBrushPosition,
                localDelta,
                localRadius,
                normals
            );

            return;
        }

        if (sculptMode ==
            SculptToolMode.Grab)
        {
            if (localDelta ==
                Vector3.zero)
            {
                return;
            }

            foreach (
                int[] group
                in weldedVertexGroups
            )
            {
                int firstVertexIndex =
                    group[0];

                Vector3 currentPos =
                    vertices[
                        firstVertexIndex
                    ];

                float distance =
                    Vector3.Distance(
                        currentPos,
                        localBrushPosition
                    );

                if (distance >
                    localRadius)
                {
                    continue;
                }

                float falloff =
                    1f -
                    distance /
                    localRadius;

                Vector3 movement =
                    localDelta *
                    grabStrength *
                    falloff *
                    falloff;

                foreach (
                    int vertexIndex
                    in group
                )
                {
                    vertices[
                        vertexIndex
                    ] += movement;
                }
            }

            return;
        }

        if (sculptMode ==
            SculptToolMode.Flatten)
        {
            Vector3 planeCenter =
                Vector3.zero;

            Vector3 planeNormal =
                Vector3.zero;

            int affectedGroupCount =
                0;

            foreach (
                int[] group
                in weldedVertexGroups
            )
            {
                int firstVertexIndex =
                    group[0];

                Vector3 currentPos =
                    vertices[
                        firstVertexIndex
                    ];

                float distance =
                    Vector3.Distance(
                        currentPos,
                        localBrushPosition
                    );

                if (distance >
                    localRadius)
                {
                    continue;
                }

                planeCenter +=
                    currentPos;

                Vector3 groupNormal =
                    Vector3.zero;

                foreach (
                    int vertexIndex
                    in group
                )
                {
                    groupNormal +=
                        normals[
                            vertexIndex
                        ];
                }

                planeNormal +=
                    groupNormal.normalized;

                affectedGroupCount++;
            }

            if (affectedGroupCount > 0)
            {
                planeCenter /=
                    affectedGroupCount;

                planeNormal.Normalize();

                if (planeNormal ==
                    Vector3.zero)
                {
                    planeNormal =
                        Vector3.up;
                }

                float flattenRate =
                    Mathf.Clamp01(
                        12f *
                        Time.deltaTime *
                        (
                            flattenStrength /
                            0.30f
                        )
                    );

                foreach (
                    int[] group
                    in weldedVertexGroups
                )
                {
                    int firstVertexIndex =
                        group[0];

                    Vector3 currentPos =
                        vertices[
                            firstVertexIndex
                        ];

                    float distance =
                        Vector3.Distance(
                            currentPos,
                            localBrushPosition
                        );

                    if (distance >
                        localRadius)
                    {
                        continue;
                    }

                    float distanceToPlane =
                        Vector3.Dot(
                            currentPos -
                            planeCenter,
                            planeNormal
                        );

                    Vector3 targetPos =
                        currentPos -
                        planeNormal *
                        distanceToPlane;

                    float falloff =
                        1f -
                        distance /
                        localRadius;

                    float lerpAmount =
                        Mathf.Clamp01(
                            flattenRate *
                            falloff *
                            falloff
                        );

                    Vector3 delta =
                        (
                            targetPos -
                            currentPos
                        ) *
                        lerpAmount;

                    foreach (
                        int vertexIndex
                        in group
                    )
                    {
                        vertices[
                            vertexIndex
                        ] += delta;
                    }
                }
            }

            return;
        }

        if (sculptMode ==
            SculptToolMode.Smooth)
        {
            float smoothRate =
                6f *
                Time.deltaTime *
                (
                    smoothStrength /
                    0.30f
                );

            foreach (
                int[] group
                in weldedVertexGroups
            )
            {
                int firstVertexIndex =
                    group[0];

                Vector3 currentPos =
                    vertices[
                        firstVertexIndex
                    ];

                float distance =
                    Vector3.Distance(
                        currentPos,
                        localBrushPosition
                    );

                if (distance >
                    localRadius)
                {
                    continue;
                }

                Vector3 neighborAverage =
                    Vector3.zero;

                int neighborCount =
                    0;

                foreach (
                    int[] otherGroup
                    in weldedVertexGroups
                )
                {
                    int otherFirstIndex =
                        otherGroup[0];

                    if (otherFirstIndex ==
                        firstVertexIndex)
                    {
                        continue;
                    }

                    float neighborDistance =
                        Vector3.Distance(
                            currentPos,
                            vertices[
                                otherFirstIndex
                            ]
                        );

                    if (neighborDistance <
                        localRadius * 0.6f)
                    {
                        neighborAverage +=
                            vertices[
                                otherFirstIndex
                            ];

                        neighborCount++;
                    }
                }

                if (neighborCount > 0)
                {
                    neighborAverage /=
                        neighborCount;

                    float falloff =
                        1f -
                        distance /
                        localRadius;

                    float lerpAmount =
                        Mathf.Clamp01(
                            smoothRate *
                            falloff *
                            falloff
                        );

                    Vector3 targetPos =
                        Vector3.Lerp(
                            currentPos,
                            neighborAverage,
                            lerpAmount
                        );

                    Vector3 delta =
                        targetPos -
                        currentPos;

                    foreach (
                        int vertexIndex
                        in group
                    )
                    {
                        vertices[
                            vertexIndex
                        ] += delta;
                    }
                }
            }

            return;
        }

        if (sculptMode ==
            SculptToolMode.Crease)
        {
            // Deliberately simple and topology-free. Earlier
            // versions pulled vertices toward their neighbour
            // centroid, which required adjacency data and acted
            // like Laplacian smoothing: it shrank round forms to
            // nothing and tore the mesh when held.
            //
            // This instead carves a narrow V groove. Two terms
            // combine: a tight negative spike at the centre and a
            // positive shoulder either side of it. The shoulder is
            // what makes the result read as a raised ridge with a
            // sharp crease line down the middle rather than a soft
            // dent, and the two roughly cancel so the surface does
            // not lose volume overall.
            foreach (
                int[] group
                in weldedVertexGroups
            ) {
                int firstVertexIndex =
                    group[0];

                Vector3 currentPos =
                    vertices[
                        firstVertexIndex
                    ];

                float distance =
                    Vector3.Distance(
                        currentPos,
                        localBrushPosition
                    );

                if (distance >
                    localRadius)
                {
                    continue;
                }

                float normalized =
                    distance / localRadius;

                // Spike: 1 dead centre, 0 by creaseWidth.
                float spike =
                    Mathf.Clamp01(
                        1f -
                        normalized /
                        Mathf.Max(
                            creaseWidth,
                            0.0001f
                        )
                    );

                spike = spike * spike;

                // Shoulder: peaks part way out from the centre, then
                // tapers back to zero at the brush edge. Without the
                // taper the shoulder would pile clay into a dome; with
                // it, only the crease line itself is left behind.
                float shoulder =
                    Mathf.Sin(
                        normalized *
                        Mathf.PI
                    );

                shoulder *=
                    1f - normalized;

                float profile =
                    (
                        -spike +
                        shoulder *
                        creaseShoulder
                    );

                Vector3 groupNormal =
                    Vector3.zero;

                foreach (
                    int vertexIndex
                    in group
                ) {
                    groupNormal +=
                        normals[
                            vertexIndex
                        ];
                }

                if (groupNormal.sqrMagnitude <
                    0.000001f)
                {
                    continue;
                }

                groupNormal.Normalize();

                // The profile is signed: negative in the groove, positive on
                // the shoulders. It must NOT be passed through
                // Clamp01, which would flatten every negative
                // value to zero and leave the crease line
                // completely immobile.
                float amount =
                    creaseRate *
                    profile *
                    Time.deltaTime;

                // Cap the per-frame move so a held pinch cannot
                // run away. Scaled by brush radius so the cap
                // behaves the same on any sized object.
                Vector3 movement =
                    groupNormal *
                    amount *
                    localRadius;

                float limit =
                    Mathf.Max(
                        localRadius * 0.02f,
                        0.0001f
                    );

                if (movement.magnitude >
                    limit)
                {
                    movement =
                        movement.normalized *
                        limit;
                }

                foreach (
                    int vertexIndex
                    in group
                ) {
                    vertices[
                        vertexIndex
                    ] += movement;
                }
            }

            return;
        }


        foreach (
            int[] group
            in weldedVertexGroups
        )
        {
            int firstVertexIndex =
                group[0];

            float distance =
                Vector3.Distance(
                    vertices[
                        firstVertexIndex
                    ],
                    localBrushPosition
                );

            if (distance >
                localRadius)
            {
                continue;
            }

            Vector3 averagedNormal =
                Vector3.zero;

            foreach (
                int vertexIndex
                in group
            )
            {
                averagedNormal +=
                    normals[
                        vertexIndex
                    ];
            }

            averagedNormal.Normalize();

            float falloff =
                1f -
                distance /
                localRadius;

            float toolStrength =
                sculptMode ==
                SculptToolMode.Indent
                    ? indentStrength
                    : inflateStrength;

            float displacement =
                localStrength *
                (
                    toolStrength /
                    0.18f
                ) *
                falloff *
                falloff;

            Vector3 direction =
                sculptMode ==
                SculptToolMode.Indent
                    ? -averagedNormal
                    : averagedNormal;

            Vector3 movement =
                direction *
                displacement;

            foreach (
                int vertexIndex
                in group
            )
            {
                vertices[
                    vertexIndex
                ] += movement;
            }
        }
    }

    private void CreateBrushCursor()
    {
        brushCursor =
            GameObject.CreatePrimitive(
                PrimitiveType.Sphere
            );

        brushCursor.name =
            "Brush Cursor";

        Collider cursorCollider =
            brushCursor.GetComponent<Collider>();

        if (cursorCollider != null)
        {
            Destroy(cursorCollider);
        }

        MeshRenderer renderer =
            brushCursor.GetComponent<MeshRenderer>();

        Shader cursorShader =
            Shader.Find(
                "Universal Render Pipeline/Lit"
            );

        if (cursorShader != null)
        {
            cursorMaterial =
                new Material(cursorShader);

            /*
             * Render the cursor after normal geometry.
             */
            cursorMaterial.renderQueue = 5000;

            /*
             * Always pass the depth test.
             * This prevents the clay from hiding the cursor.
             */
            if (cursorMaterial.HasProperty("_ZTest"))
            {
                cursorMaterial.SetInt(
                    "_ZTest",
                    (int)
                        UnityEngine.Rendering
                            .CompareFunction
                            .Always
                );
            }

            /*
             * Do not write cursor depth back into
             * the depth buffer.
             */
            if (cursorMaterial.HasProperty("_ZWrite"))
            {
                cursorMaterial.SetInt(
                    "_ZWrite",
                    0
                );
            }

            if (cursorMaterial.HasProperty("_Smoothness"))
            {
                cursorMaterial.SetFloat(
                    "_Smoothness",
                    0.8f
                );
            }

            if (cursorMaterial.HasProperty("_BaseColor"))
            {
                cursorMaterial.SetColor(
                    "_BaseColor",
                    inflateCursorColor
                );
            }

            renderer.material =
                cursorMaterial;
        }

        renderer.shadowCastingMode =
            UnityEngine.Rendering
                .ShadowCastingMode.Off;

        renderer.receiveShadows = false;

        brushCursor.transform.localScale =
            Vector3.one *
            cursorSize;

        if (mirroredBrushCursor != null)
        {
            Destroy(
                mirroredBrushCursor
            );
        }

        mirroredBrushCursor =
            Instantiate(
                brushCursor
            );

        mirroredBrushCursor.name =
            "Mirrored Brush Cursor";

        mirroredBrushCursor.SetActive(
            false
        );

        UpdateCursorColor();
    }

    private void UpdateCursorColor()
    {
        if (cursorMaterial == null)
        {
            return;
        }

        Color targetColor =
            inflateCursorColor;

        if (sculptMode ==
            SculptToolMode.Indent)
        {
            targetColor =
                indentCursorColor;
        }
        else if (
            sculptMode ==
            SculptToolMode.Smooth)
        {
            targetColor =
                smoothCursorColor;
        }
        else if (
            sculptMode ==
            SculptToolMode.Grab)
        {
            targetColor =
                grabCursorColor;
        }
        else if (
            sculptMode ==
            SculptToolMode.Flatten)
        {
            targetColor =
                flattenCursorColor;
        }
        else if (
            sculptMode ==
            SculptToolMode.Crease)
        {
            targetColor =
                creaseCursorColor;
        }
        else if (
            sculptMode ==
            SculptToolMode.Stretch)
        {
            targetColor =
                stretchCursorColor;
        }

        if (cursorMaterial.HasProperty("_BaseColor"))
        {
            cursorMaterial.SetColor(
                "_BaseColor",
                targetColor
            );
        }
    }

    private void UpdateBrushCursor(
        Vector3 worldPosition,
        Vector3 worldNormal
    )
    {
        if (!showBrushCursor)
        {
            SetCursorVisible(false);

            return;
        }

        Camera mainCamera =
            Camera.main;

        if (mainCamera == null)
        {
            SetCursorVisible(false);

            return;
        }

        /*
         * Smooth ONLY the visual cursor.
         * The actual sculpting position remains
         * the original hand position, so sculpting
         * does not become laggy.
         */
        if (!hasSmoothedCursorPosition)
        {
            smoothedCursorPosition =
                worldPosition;

            smoothedCursorVelocity =
                Vector3.zero;

            hasSmoothedCursorPosition =
                true;
        }
        else
        {
            smoothedCursorPosition =
                Vector3.SmoothDamp(
                    smoothedCursorPosition,
                    worldPosition,
                    ref smoothedCursorVelocity,
                    cursorSmoothTime,
                    cursorMaxSmoothSpeed,
                    Time.unscaledDeltaTime
                );
        }

        /*
         * Put the cursor toward the camera,
         * slightly in front of the clay.
         */
        Vector3 towardCamera =
            (
                mainCamera.transform.position -
                smoothedCursorPosition
            ).normalized;

        float cursorOffset =
            cursorCameraOffset +
            cursorSize +
            cursorSurfaceClearance;

        brushCursor.transform.position =
            smoothedCursorPosition +
            towardCamera *
            cursorOffset;

        /*
         * Symmetry cursor.
         */
        if (enableSymmetry &&
            mirroredBrushCursor != null)
        {
            Vector3 localPosition =
                transform.InverseTransformPoint(
                    smoothedCursorPosition
                );

            Vector3 mirroredLocalPosition =
                new Vector3(
                    -localPosition.x,
                    localPosition.y,
                    localPosition.z
                );

            Vector3 mirroredWorldPosition =
                transform.TransformPoint(
                    mirroredLocalPosition
                );

            Vector3 mirroredTowardCamera =
                (
                    mainCamera.transform.position -
                    mirroredWorldPosition
                ).normalized;

            mirroredBrushCursor.transform.position =
                mirroredWorldPosition +
                mirroredTowardCamera *
                cursorOffset;
        }

        UpdateCursorColor();

        SetCursorVisible(true);
    }

    private void SetCursorVisible(
        bool isVisible
    )
    {
        if (brushCursor != null &&
            brushCursor.activeSelf !=
            isVisible)
        {
            brushCursor.SetActive(
                isVisible
            );
        }

        if (mirroredBrushCursor != null)
        {
            mirroredBrushCursor.SetActive(
                isVisible &&
                enableSymmetry
            );
        }
    }
}