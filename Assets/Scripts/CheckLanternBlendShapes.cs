
using UnityEngine;

public class CheckLanternBlendShapes : MonoBehaviour
{
    [ContextMenu("Check Lantern Blend Shapes")]
    private void CheckBlendShapes()
    {
        MeshFilter meshFilter = GetComponent<MeshFilter>();

        if (meshFilter == null || meshFilter.sharedMesh == null)
        {
            Debug.LogWarning("No MeshFilter or mesh found.", this);
            return;
        }

        Mesh mesh = meshFilter.sharedMesh;
        Debug.Log($"Mesh: {mesh.name} | Blend Shape count: {mesh.blendShapeCount}", this);

        for (int i = 0; i < mesh.blendShapeCount; i++)
        {
            Debug.Log($"Blend Shape {i}: {mesh.GetBlendShapeName(i)}", this);
        }
    }
}
