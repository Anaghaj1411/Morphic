using UnityEngine;

[RequireComponent(typeof(MeshFilter))]
public class BaseShapeSelector : MonoBehaviour
{
    [Header("Base Shape Templates")]
    [SerializeField] private GameObject sphereBase;
    [SerializeField] private GameObject bowlBase;
    [SerializeField] private GameObject cylinderBase;
    [SerializeField] private GameObject cubeBase;

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
    }

    public void SelectSphere()
    {
        SetBaseShape(sphereBase, "Sphere");
    }

    public void SelectBowl()
    {
        SetBaseShape(bowlBase, "Bowl");
    }

    public void SelectCylinder()
    {
        SetBaseShape(cylinderBase, "Cylinder");
    }

    public void SelectCube()
    {
        SetBaseShape(cubeBase, "Cube");
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
