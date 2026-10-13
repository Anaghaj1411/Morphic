using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(MeshFilter))]
public class BaseShapeSelector : MonoBehaviour
{
    [Header("Base Shape Templates")]
    [SerializeField] private GameObject sphereBase;
    [SerializeField] private GameObject bowlBase;
    [SerializeField] private GameObject cylinderBase;
    [SerializeField] private GameObject cubeBase;
    [SerializeField] private GameObject lanternBase;

    [Header("Tessellation")]
    [Tooltip(
        "Extra subdivision applied at runtime. The imported base " +
        "shapes are very low poly (lantern is ~900 polys), which is " +
        "too coarse for sharp detail like creases. Each level roughly " +
        "quadruples the polygon count."
    )]
    // Opt-in. Subdividing every base shape broke the previously
    // working Inflate/Indent path, so this defaults to off until
    // the lantern and sphere are confirmed stable at a raised
    // level.
    [Range(0, 3)]
    [SerializeField] private int subdivisionLevel = 0;

    [SerializeField] private int maxSubdividedVertices = 200000;

    private MeshFilter targetMeshFilter;
    private MeshCollider targetMeshCollider;
    private Vector3 startingScale;
    private Quaternion startingRotation;
    private float referenceMeshDiameter;

    private void Awake()
    {
        targetMeshFilter = GetComponent<MeshFilter>();
        targetMeshCollider = GetComponent<MeshCollider>();

        startingScale = transform.localScale;
        startingRotation = transform.rotation;

        if (targetMeshFilter.sharedMesh != null)
        {
            referenceMeshDiameter = GetLargestDimension(
                targetMeshFilter.sharedMesh.bounds.size
            );
        }
    }

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.Alpha6))
        {
            SelectSphere();
        }
        else if (Input.GetKeyDown(KeyCode.Alpha7))
        {
            SelectBowl();
        }
        else if (Input.GetKeyDown(KeyCode.Alpha8))
        {
            SelectCylinder();
        }
        else if (Input.GetKeyDown(KeyCode.Alpha9))
        {
            SelectCube();
        }
        else if (Input.GetKeyDown(KeyCode.Alpha0))
        {
            SelectLantern();
        }
    }

    public void SelectSphere()
    {
        SetGuidedLanternMode(false);
        SetBaseShape(sphereBase, "Sphere");
    }

    public void SelectBowl()
    {
        SetGuidedLanternMode(false);
        SetBaseShape(bowlBase, "Bowl");
    }

    public void SelectCylinder()
    {
        SetGuidedLanternMode(false);
        SetBaseShape(cylinderBase, "Cylinder");
    }

    public void SelectCube()
    {
        SetGuidedLanternMode(false);
        SetBaseShape(cubeBase, "Cube");
    }

    public void SelectLantern()
    {
        SetBaseShape(lanternBase, "Lantern");
        SetGuidedLanternMode(true);
    }

    private void SetGuidedLanternMode(bool enabled)
    {
        PinchSculptBrush brush = GetComponent<PinchSculptBrush>();
        TwoHandScaleSculpture twoHandScale = GetComponent<TwoHandScaleSculpture>();
        SculptUIController sculptUi = GetComponent<SculptUIController>();
        ParametricSkyLantern lantern = GetComponent<ParametricSkyLantern>();
        LanternGestureInterpreter interpreter =
            GetComponent<LanternGestureInterpreter>();

        if (enabled)
        {
            if (brush != null)
            {
                brush.enabled = false;
            }

            if (twoHandScale != null)
            {
                twoHandScale.enabled = false;
            }

            if (sculptUi != null)
            {
                sculptUi.enabled = false;
            }

            if (lantern == null)
            {
                lantern = gameObject.AddComponent<ParametricSkyLantern>();
            }

            if (interpreter == null)
            {
                interpreter = gameObject.AddComponent<LanternGestureInterpreter>();
            }

            interpreter.enabled = true;
            lantern.EnterGuidedMode();
            return;
        }

        if (lantern != null && lantern.GuidedModeEnabled)
        {
            lantern.ExitGuidedMode();
        }

        if (interpreter != null)
        {
            interpreter.enabled = false;
        }

        if (brush != null)
        {
            brush.enabled = true;
        }

        if (twoHandScale != null)
        {
            twoHandScale.enabled = true;
        }

        if (sculptUi != null)
        {
            sculptUi.enabled = true;
        }
    }

    private void SetBaseShape(
        GameObject baseShape,
        string shapeName
    )
    {
        if (baseShape == null)
        {
            Debug.LogWarning(
                shapeName + " base shape has not been assigned."
            );

            return;
        }

        MeshFilter sourceMeshFilter =
            baseShape.GetComponentInChildren<MeshFilter>();

        if (sourceMeshFilter == null ||
            sourceMeshFilter.sharedMesh == null)
        {
            Debug.LogWarning(
                shapeName + " does not contain a mesh."
            );

            return;
        }

        Mesh editableMesh = Instantiate(
            sourceMeshFilter.sharedMesh
        );

        editableMesh.name = shapeName + " Editable Mesh";

        FitMeshToReferenceSize(editableMesh);

        SubdivideForDetail(editableMesh, shapeName);

        targetMeshFilter.mesh = editableMesh;

        if (targetMeshCollider != null)
        {
            targetMeshCollider.sharedMesh = null;
            targetMeshCollider.sharedMesh = editableMesh;
        }

        transform.localScale = startingScale;
        transform.rotation = startingRotation;

        SendMessage(
            "RefreshMesh",
            editableMesh,
            SendMessageOptions.DontRequireReceiver
        );

        SendMessage(
            "ClearHistory",
            SendMessageOptions.DontRequireReceiver
        );

        Debug.Log("Selected base shape: " + shapeName);
    }

    private void SubdivideForDetail(Mesh mesh, string shapeName)
    {
        if (mesh == null ||
            subdivisionLevel <= 0)
        {
            return;
        }

        if (!mesh.isReadable)
        {
            Debug.LogWarning(
                "Cannot subdivide the " + shapeName +
                " base shape because Read/Write is disabled. " +
                "Enable it on the model import settings.",
                this
            );

            return;
        }

        int appliedLevels = 0;

        // Imported models default to a 16-bit index buffer, which
        // silently wraps past 65535 vertices and shreds the mesh.
        // Promote to 32-bit before subdividing so dense meshes stay
        // valid.
        if (mesh.vertexCount > 60000)
        {
            mesh.indexFormat =
                UnityEngine.Rendering.IndexFormat.UInt32;
        }

        for (
            int level = 0;
            level < subdivisionLevel;
            level++)
        {
            // Each pass roughly quadruples the triangle count, so stop
            // before the mesh becomes too heavy to sculpt every frame.
            if (mesh.vertexCount >= maxSubdividedVertices)
            {
                Debug.Log(
                    "Stopped subdividing the " + shapeName +
                    " at " + appliedLevels + " level(s); " +
                    mesh.vertexCount + " vertices reached the cap.",
                    this
                );

                break;
            }

            if (SubdivideOnce(mesh))
            {
                appliedLevels++;
            }
            else
            {
                break;
            }

            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
        }

        Debug.Log(
            "Subdivided " + shapeName + " by " + appliedLevels +
            " level(s): " + mesh.vertexCount + " verts / " +
            mesh.triangles.Length / 3 + " tris.",
            this
        );
    }

    /// <summary>
/// Splits every triangle into four. Unity 6 removed
/// Mesh.SimpleSubdivision, so this does the same job directly.
/// Returns false if the mesh has no triangle data.
/// </summary>
private bool SubdivideOnce(Mesh mesh)
    {
        Vector3[] sourceVertices =
            mesh.vertices;

        int[] sourceTriangles =
            mesh.triangles;

        if (sourceVertices == null ||
            sourceTriangles == null ||
            sourceTriangles.Length < 3)
        {
            return false;
        }

        // Upper bound: every triangle can contribute three new
        // midpoints. The list is trimmed once the real count is known
        // so the mesh is not padded with empty vertices.
        List<Vector3> vertexList =
            new List<Vector3>(sourceVertices);

        List<int> newTriangles =
            new List<int>(
                sourceTriangles.Length * 4);

        // Reused so neighbouring triangles that share an edge also
        // share the same midpoint vertex, which keeps the surface
        // watertight instead of splitting into loose shards.
        Dictionary<long, int> midpointCache =
            new Dictionary<long, int>();

        for (
            int i = 0;
            i < sourceTriangles.Length;
            i += 3
        ) {
            int a = sourceTriangles[i];
            int b = sourceTriangles[i + 1];
            int c = sourceTriangles[i + 2];

            int ab = GetMidpoint(
                vertexList,
                midpointCache,
                a,
                b
            );

            int bc = GetMidpoint(
                vertexList,
                midpointCache,
                b,
                c
            );

            int ca = GetMidpoint(
                vertexList,
                midpointCache,
                c,
                a
            );

            newTriangles.Add(a);
            newTriangles.Add(ab);
            newTriangles.Add(ca);

            newTriangles.Add(b);
            newTriangles.Add(bc);
            newTriangles.Add(ab);

            newTriangles.Add(c);
            newTriangles.Add(ca);
            newTriangles.Add(bc);

            newTriangles.Add(ab);
            newTriangles.Add(bc);
            newTriangles.Add(ca);
        }

        mesh.vertices = vertexList.ToArray();
        mesh.triangles = newTriangles.ToArray();

        return true;
    }

    private static int GetMidpoint(
        List<Vector3> vertices,
        Dictionary<long, int> cache,
        int a,
        int b
    )
    {
        int low = a < b ? a : b;
        int high = a < b ? b : a;

        long key =
            ((long)low << 32) | (uint)high;

        int existing;

        if (cache.TryGetValue(key, out existing))
        {
            return existing;
        }

        int index =
            vertices.Count;

        vertices.Add(
            (vertices[a] + vertices[b]) * 0.5f
        );

        cache[key] = index;

        return index;
    }

    private void FitMeshToReferenceSize(Mesh mesh)
    {
        float sourceDiameter = GetLargestDimension(
            mesh.bounds.size
        );

        if (referenceMeshDiameter <= Mathf.Epsilon ||
            sourceDiameter <= Mathf.Epsilon)
        {
            return;
        }

        float scaleFactor = referenceMeshDiameter / sourceDiameter;
        Vector3 center = mesh.bounds.center;
        Vector3[] meshVertices = mesh.vertices;

        for (int i = 0; i < meshVertices.Length; i++)
        {
            meshVertices[i] = center +
                (meshVertices[i] - center) * scaleFactor;
        }

        mesh.vertices = meshVertices;
        mesh.RecalculateBounds();
        mesh.RecalculateNormals();
    }

    private float GetLargestDimension(Vector3 size)
    {
        return Mathf.Max(size.x, size.y, size.z);
    }
}
