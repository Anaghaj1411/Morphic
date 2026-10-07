using System.Collections;
using UnityEngine;

[RequireComponent(typeof(MeshFilter))]
[RequireComponent(typeof(MeshRenderer))]
public class ParametricSkyLantern : MonoBehaviour
{
    [Header("Lantern Geometry")]
    [SerializeField] private int radialSegments = 12;
    [SerializeField] private int heightSegments = 10;

    [SerializeField] private float radius = 0.30f;
    [SerializeField] private float height = 0.75f;

    [Header("Visuals")]
    [SerializeField] private MeshFilter lanternMeshFilter;
    [SerializeField] private MeshRenderer lanternRenderer;
    [SerializeField] private Transform burnerRoot;
    [SerializeField] private Material lanternMaterial;

    [Header("Authored Limits")]
    [SerializeField] private float minWidthScale = 0.78f;
    [SerializeField] private float maxWidthScale = 1.35f;

    [SerializeField] private float minHeightScale = 0.82f;
    [SerializeField] private float maxHeightScale = 1.45f;

    [Header("Top Shape")]
    [SerializeField] private float topRadiusMultiplier = 0.58f;
    [SerializeField] private float minimumTopNarrow = 0f;
    [SerializeField] private float maximumTopNarrow = 1f;

    [Header("Smoothing")]
    [SerializeField] private float visualSmoothingSpeed = 8f;

    [Header("Glow")]
    [SerializeField] private Color offColor =
        new Color(0.08f, 0.035f, 0.015f);

    [SerializeField] private Color glowColor =
        new Color(1f, 0.48f, 0.12f);

    [SerializeField] private float glowIntensity = 2.4f;
    [SerializeField] private float glowFadeSeconds = 0.7f;

    [Header("State")]
    [Range(0f, 1f)]
    [SerializeField] private float widthAmount = 0.35f;

    [Range(0f, 1f)]
    [SerializeField] private float heightAmount = 0.30f;

    [Range(0f, 1f)]
    [SerializeField] private float taperAmount = 0f;

    [Range(0f, 1f)]
    [SerializeField] private float topNarrowAmount = 0f;

    [Range(0f, 1f)]
    [SerializeField] private float burnerAmount = 0f;

    public static ParametricSkyLantern Active { get; private set; }

    public bool GuidedModeEnabled { get; private set; }
    public bool IsGlowing { get; private set; }

    public float WidthAmount => widthAmount;
    public float HeightAmount => heightAmount;
    public float TaperAmount => taperAmount;
    public float TopNarrowAmount => topNarrowAmount;
    public float BurnerAmount => burnerAmount;

    private Mesh workingMesh;

    private Vector3[] baseVertices;
    private Vector3[] workingVertices;

    private float displayWidth;
    private float displayHeight;
    private float displayTaper;
    private float displayTopNarrow;
    private float displayBurner;

    private Material runtimeMaterial;
    private Coroutine glowRoutine;

    private void Awake()
    {
        FindComponents();
        GenerateLanternMesh();
        EnsureRuntimeMaterial();
        EnsureBurner();

        GuidedModeEnabled = false;
        ApplyVisuals();
    }

    private void OnEnable()
    {
        Active = this;
    }

    private void OnDisable()
    {
        if (Active == this)
        {
            Active = null;
        }
    }

    private void Update()
    {
        if (!GuidedModeEnabled)
        {
            return;
        }

        float smooth =
            1f -
            Mathf.Exp(
                -visualSmoothingSpeed *
                Time.deltaTime);

        displayWidth =
            Mathf.Lerp(
                displayWidth,
                widthAmount,
                smooth);

        displayHeight =
            Mathf.Lerp(
                displayHeight,
                heightAmount,
                smooth);

        displayTaper =
            Mathf.Lerp(
                displayTaper,
                taperAmount,
                smooth);

        displayTopNarrow =
            Mathf.Lerp(
                displayTopNarrow,
                topNarrowAmount,
                smooth);

        displayBurner =
            Mathf.Lerp(
                displayBurner,
                burnerAmount,
                smooth);

        ApplyVisuals();
    }

    // ============================================================
    // GUIDED MODE
    // ============================================================

    public void EnterGuidedMode()
    {
        FindComponents();

        if (workingMesh == null)
        {
            GenerateLanternMesh();
        }

        EnsureRuntimeMaterial();
        EnsureBurner();

        widthAmount = 0.35f;
        heightAmount = 0.30f;
        taperAmount = 0f;
        topNarrowAmount = 0f;
        burnerAmount = 0f;

        IsGlowing = false;

        displayWidth = widthAmount;
        displayHeight = heightAmount;
        displayTaper = taperAmount;
        displayTopNarrow = topNarrowAmount;
        displayBurner = burnerAmount;

        GuidedModeEnabled = true;
        enabled = true;

        ApplyVisuals();
    }

    public void ExitGuidedMode()
    {
        GuidedModeEnabled = false;
        IsGlowing = false;

        if (glowRoutine != null)
        {
            StopCoroutine(glowRoutine);
            glowRoutine = null;
        }

        if (burnerRoot != null)
        {
            burnerRoot.gameObject.SetActive(false);
        }

        RestoreEmission(offColor);
    }

    // ============================================================
    // GESTURE VALUES
    // ============================================================

    public void SetWidthAmount(float value)
    {
        widthAmount =
            Mathf.Clamp01(value);
    }

    public void SetHeightAmount(float value)
    {
        heightAmount =
            Mathf.Clamp01(value);
    }

    public void SetTaperAmount(float value)
    {
        taperAmount =
            Mathf.Clamp01(value);
    }

    public void SetTopNarrowAmount(float value)
    {
        topNarrowAmount =
            Mathf.Clamp(
                value,
                minimumTopNarrow,
                maximumTopNarrow);
    }

    public void SetBurnerAmount(float value)
    {
        burnerAmount =
            Mathf.Clamp01(value);
    }

    // ============================================================
    // GLOW
    // ============================================================

    public void ActivateGlow()
    {
        if (IsGlowing ||
            !GuidedModeEnabled)
        {
            return;
        }

        IsGlowing = true;

        if (glowRoutine != null)
        {
            StopCoroutine(glowRoutine);
        }

        glowRoutine =
            StartCoroutine(
                GlowRoutine());
    }

    private IEnumerator GlowRoutine()
    {
        if (runtimeMaterial == null)
        {
            yield break;
        }

        float elapsed = 0f;

        while (elapsed < glowFadeSeconds)
        {
            elapsed += Time.deltaTime;

            float t =
                Mathf.Clamp01(
                    elapsed /
                    glowFadeSeconds);

            Color emission =
                Color.Lerp(
                    offColor,
                    glowColor * glowIntensity,
                    t);

            RestoreEmission(emission);

            yield return null;
        }

        RestoreEmission(
            glowColor *
            glowIntensity);

        glowRoutine = null;
    }

    // ============================================================
    // PROCEDURAL LANTERN BODY
    // ============================================================

    private void GenerateLanternMesh()
{
    FindComponents();

    // High enough topology for smooth guided sculpting.
    radialSegments = Mathf.Max(radialSegments, 32);
    heightSegments = Mathf.Max(heightSegments, 24);

    int ringCount = heightSegments + 1;

    // Body rings + one center vertex for the closed top.
    int bodyVertexCount = ringCount * radialSegments;
    int topCenterIndex = bodyVertexCount;
    int vertexCount = bodyVertexCount + 1;

    Vector3[] vertices = new Vector3[vertexCount];
    Vector2[] uv = new Vector2[vertexCount];

    // Side triangles:
    // heightSegments * radialSegments * 2 triangles
    //
    // Top cap:
    // radialSegments triangles
    int sideTriangleCount =
        heightSegments * radialSegments * 6;

    int topCapTriangleCount =
        radialSegments * 3;

    int[] triangles = new int[
        sideTriangleCount +
        topCapTriangleCount
    ];

    // ------------------------------------------------------------
    // BODY
    // ------------------------------------------------------------

    for (int y = 0; y <= heightSegments; y++)
    {
        float vertical =
            (float)y / heightSegments;

        float yPosition =
            -height * 0.5f +
            vertical * height;

        // --------------------------------------------------------
        // SKY LANTERN PROFILE
        //
        // Bottom: slightly narrow
        // Lower-middle: expands
        // Middle: fullest section
        // Upper-middle: gently narrows
        // Top: narrower neck
        // --------------------------------------------------------

        float profile;

        if (vertical < 0.18f)
        {
            float t =
                vertical / 0.18f;

            t = Mathf.SmoothStep(0f, 1f, t);

            profile =
                Mathf.Lerp(
                    0.76f,
                    0.96f,
                    t
                );
        }
        else if (vertical < 0.42f)
        {
            float t =
                (vertical - 0.18f) /
                0.24f;

            t = Mathf.SmoothStep(0f, 1f, t);

            profile =
                Mathf.Lerp(
                    0.96f,
                    1.08f,
                    t
                );
        }
        else if (vertical < 0.72f)
        {
            float t =
                (vertical - 0.42f) /
                0.30f;

            t = Mathf.SmoothStep(0f, 1f, t);

            profile =
                Mathf.Lerp(
                    1.08f,
                    1.04f,
                    t
                );
        }
        else if (vertical < 0.90f)
        {
            float t =
                (vertical - 0.72f) /
                0.18f;

            t = Mathf.SmoothStep(0f, 1f, t);

            profile =
                Mathf.Lerp(
                    1.04f,
                    0.82f,
                    t
                );
        }
        else
        {
            float t =
                (vertical - 0.90f) /
                0.10f;

            t = Mathf.SmoothStep(0f, 1f, t);

            profile =
                Mathf.Lerp(
                    0.82f,
                    topRadiusMultiplier,
                    t
                );
        }

        for (int x = 0; x < radialSegments; x++)
        {
            float angle =
                (float)x /
                radialSegments *
                Mathf.PI *
                2f;

            int index =
                y * radialSegments +
                x;

            float ringRadius =
                radius * profile;

            vertices[index] =
                new Vector3(
                    Mathf.Cos(angle) * ringRadius,
                    yPosition,
                    Mathf.Sin(angle) * ringRadius
                );

            uv[index] =
                new Vector2(
                    (float)x / radialSegments,
                    vertical
                );
        }
    }

    // ------------------------------------------------------------
    // TOP CENTER
    // ------------------------------------------------------------

    topCenterIndex = bodyVertexCount;

    vertices[topCenterIndex] =
        new Vector3(
            0f,
            height * 0.5f,
            0f
        );

    uv[topCenterIndex] =
        new Vector2(0.5f, 1f);

    // ------------------------------------------------------------
    // SIDE TRIANGLES
    // ------------------------------------------------------------

    int triangleIndex = 0;

    for (int y = 0; y < heightSegments; y++)
    {
        for (int x = 0; x < radialSegments; x++)
        {
            int nextX =
                (x + 1) % radialSegments;

            int current =
                y * radialSegments +
                x;

            int next =
                y * radialSegments +
                nextX;

            int upper =
                (y + 1) * radialSegments +
                x;

            int upperNext =
                (y + 1) * radialSegments +
                nextX;

            // First triangle
triangles[triangleIndex++] =
    current;

triangles[triangleIndex++] =
    upper;

triangles[triangleIndex++] =
    next;

// Second triangle
triangles[triangleIndex++] =
    next;

triangles[triangleIndex++] =
    upper;

triangles[triangleIndex++] =
    upperNext;
        }
    }

    // ------------------------------------------------------------
    // CLOSED TOP CAP
    // ------------------------------------------------------------

    int topRingStart =
        heightSegments * radialSegments;

    for (int x = 0; x < radialSegments; x++)
    {
        int nextX =
            (x + 1) % radialSegments;

        int current =
            topRingStart + x;

        int next =
            topRingStart + nextX;

        // Winding chosen so the top faces outward.
        triangles[triangleIndex++] =
            current;

        triangles[triangleIndex++] =
            topCenterIndex;

        triangles[triangleIndex++] =
            next;
    }

    // ------------------------------------------------------------
    // CREATE MESH
    // ------------------------------------------------------------

    workingMesh = new Mesh();

    workingMesh.name =
        "Procedural_Taiwan_Sky_Lantern";

    // 801 vertices with the current 32 x 24 topology,
    // so UInt16 indices are more than enough.
    workingMesh.vertices =
        vertices;

    workingMesh.triangles =
        triangles;

    workingMesh.uv =
        uv;

    workingMesh.RecalculateNormals();
    workingMesh.RecalculateBounds();

    lanternMeshFilter.mesh =
        workingMesh;

    // Store the untouched procedural shape.
    baseVertices =
        workingMesh.vertices;

    workingVertices =
        new Vector3[
            baseVertices.Length
        ];
}

    // ============================================================
    // SHAPE APPLICATION
    // ============================================================

    private void ApplyVisuals()
    {
        if (workingMesh == null ||
            baseVertices == null)
        {
            return;
        }

        float widthScale =
            Mathf.Lerp(
                minWidthScale,
                maxWidthScale,
                displayWidth);

        float heightScale =
            Mathf.Lerp(
                minHeightScale,
                maxHeightScale,
                displayHeight);

        for (int i = 0;
             i < baseVertices.Length;
             i++)
        {
            Vector3 vertex =
                baseVertices[i];

            float normalizedY =
                Mathf.InverseLerp(
                    -height * 0.5f,
                    height * 0.5f,
                    vertex.y);

            /*
             * Taper:
             * makes the lower and upper
             * sections narrower while
             * keeping the middle fuller.
             */
            float taperProfile =
                1f;

            if (normalizedY < 0.45f)
            {
                float t =
                    Mathf.SmoothStep(
                        0f,
                        1f,
                        normalizedY /
                        0.45f);

                taperProfile =
                    Mathf.Lerp(
                        0.78f,
                        1f,
                        t);
            }
            else
            {
                float t =
                    Mathf.SmoothStep(
                        0f,
                        1f,
                        (normalizedY - 0.45f) /
                        0.55f);

                taperProfile =
                    Mathf.Lerp(
                        1f,
                        0.88f,
                        t);
            }

            taperProfile =
                Mathf.Lerp(
                    1f,
                    taperProfile,
                    displayTaper);

            /*
             * Top-narrow gesture.
             *
             * Only the upper portion
             * of the lantern is affected.
             */
            float topFactor =
                1f;

            if (normalizedY > 0.55f)
            {
                float t =
                    Mathf.InverseLerp(
                        0.55f,
                        1f,
                        normalizedY);

                t =
                    Mathf.SmoothStep(
                        0f,
                        1f,
                        t);

                topFactor =
                    Mathf.Lerp(
                        1f,
                        0.48f,
                        displayTopNarrow *
                        t);
            }

            float finalRadius =
                widthScale *
                taperProfile *
                topFactor;

            workingVertices[i] =
                new Vector3(
                    vertex.x *
                    finalRadius,
                    vertex.y *
                    heightScale,
                    vertex.z *
                    finalRadius);
        }

        workingMesh.vertices =
            workingVertices;

        workingMesh.RecalculateNormals();
        workingMesh.RecalculateBounds();

        UpdateBurner(
            -height *
            0.5f *
            heightScale,
            widthScale);
    }

    // ============================================================
    // BURNER
    // ============================================================

    private void EnsureBurner()
    {
        if (burnerRoot != null)
        {
            return;
        }

        Transform existing =
            transform.Find(
                "LanternBurner");

        if (existing != null)
        {
            burnerRoot =
                existing;

            return;
        }

        GameObject root =
            new GameObject(
                "LanternBurner");

        root.transform.SetParent(
            transform,
            false);

        burnerRoot =
            root.transform;

        GameObject ring =
            GameObject.CreatePrimitive(
                PrimitiveType.Cylinder);

        ring.name =
            "BurnerRing";

        ring.transform.SetParent(
            burnerRoot,
            false);

        ring.transform.localScale =
            new Vector3(
                1f,
                0.035f,
                1f);

        DestroyCollider(ring);

        for (int i = 0; i < 4; i++)
        {
            GameObject leg =
                GameObject.CreatePrimitive(
                    PrimitiveType.Cube);

            leg.name =
                "BurnerLeg_" + i;

            leg.transform.SetParent(
                burnerRoot,
                false);

            float angle =
                i *
                90f *
                Mathf.Deg2Rad;

            leg.transform.localPosition =
                new Vector3(
                    Mathf.Cos(angle) *
                    0.38f,
                    -0.22f,
                    Mathf.Sin(angle) *
                    0.38f);

            leg.transform.localScale =
                new Vector3(
                    0.045f,
                    0.42f,
                    0.045f);

            DestroyCollider(leg);
        }

        GameObject pan =
            GameObject.CreatePrimitive(
                PrimitiveType.Cylinder);

        pan.name =
            "BurnerPan";

        pan.transform.SetParent(
            burnerRoot,
            false);

        pan.transform.localPosition =
            new Vector3(
                0f,
                -0.42f,
                0f);

        pan.transform.localScale =
            new Vector3(
                0.42f,
                0.02f,
                0.42f);

        DestroyCollider(pan);

        Color frameColor =
            new Color(
                0.28f,
                0.16f,
                0.07f);

        ApplyColor(
            burnerRoot,
            frameColor);

        burnerRoot.gameObject.SetActive(
            false);
    }

    private void UpdateBurner(
        float bottomY,
        float widthScale)
    {
        if (burnerRoot == null)
        {
            return;
        }

        float reveal =
            displayBurner;

        burnerRoot.gameObject.SetActive(
            reveal > 0.02f);

        burnerRoot.localPosition =
            new Vector3(
                0f,
                bottomY -
                0.04f *
                reveal,
                0f);

        burnerRoot.localScale =
            new Vector3(
                widthScale *
                (0.55f +
                 0.25f *
                 reveal),

                reveal,

                widthScale *
                (0.55f +
                 0.25f *
                 reveal));
    }

    // ============================================================
    // MATERIAL
    // ============================================================

    private void EnsureRuntimeMaterial()
    {
        if (lanternRenderer == null)
        {
            return;
        }

        if (runtimeMaterial == null)
        {
            if (lanternMaterial != null)
            {
                runtimeMaterial =
                    new Material(
                        lanternMaterial);
            }
            else
            {
                Shader shader =
                    Shader.Find(
                        "Universal Render Pipeline/Lit");

                if (shader == null)
                {
                    shader =
                        Shader.Find(
                            "Standard");
                }

                runtimeMaterial =
                    new Material(shader);

                runtimeMaterial.color =
                    new Color(
                        0.95f,
                        0.55f,
                        0.22f,
                        1f);
            }

            runtimeMaterial.name =
                "TaiwanLantern_RuntimeMaterial";

            lanternRenderer.material =
                runtimeMaterial;
        }

        lanternMaterial =
            runtimeMaterial;

        RestoreEmission(
            offColor);
    }

    private void RestoreEmission(
        Color color)
    {
        if (lanternMaterial == null)
        {
            return;
        }

        lanternMaterial.EnableKeyword(
            "_EMISSION");

        lanternMaterial.SetColor(
            "_EmissionColor",
            color);
    }

    // ============================================================
    // HELPERS
    // ============================================================

    private void FindComponents()
    {
        if (lanternMeshFilter == null)
        {
            lanternMeshFilter =
                GetComponent<MeshFilter>();
        }

        if (lanternRenderer == null)
        {
            lanternRenderer =
                GetComponent<MeshRenderer>();
        }
    }

    private static void DestroyCollider(
        GameObject part)
    {
        Collider collider =
            part.GetComponent<Collider>();

        if (collider != null)
        {
            Destroy(collider);
        }
    }

    private static void ApplyColor(
        Transform root,
        Color color)
    {
        MeshRenderer[] renderers =
            root.GetComponentsInChildren<
                MeshRenderer>();

        for (int i = 0;
             i < renderers.Length;
             i++)
        {
            renderers[i].material.color =
                color;
        }
    }
}