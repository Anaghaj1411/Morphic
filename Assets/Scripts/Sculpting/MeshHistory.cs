using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(MeshFilter))]
public class MeshHistory : MonoBehaviour
{
    [Header("History")]
    [SerializeField] private int maximumHistoryStates = 25;

    private Mesh sculptMesh;
    private MeshCollider meshCollider;

    private readonly List<Vector3[]> undoStates =
        new List<Vector3[]>();

    private readonly List<Vector3[]> redoStates =
        new List<Vector3[]>();

    private void Awake()
    {
        RefreshMesh(
            GetComponent<MeshFilter>().mesh
        );
    }

    private void Update()
    {
        bool controlPressed =
            Input.GetKey(KeyCode.LeftControl) ||
            Input.GetKey(KeyCode.RightControl);

        if (controlPressed &&
            Input.GetKeyDown(KeyCode.Z))
        {
            Undo();
        }

        if (controlPressed &&
            Input.GetKeyDown(KeyCode.Y))
        {
            Redo();
        }
    }

    public void RefreshMesh(Mesh newMesh)
    {
        sculptMesh = newMesh;
        meshCollider = GetComponent<MeshCollider>();

        ClearHistory();
    }

    public void ClearHistory()
    {
        undoStates.Clear();
        redoStates.Clear();
    }

    public void SaveState()
    {
        if (sculptMesh == null)
        {
            return;
        }

        undoStates.Add(
            CopyVertices(sculptMesh.vertices)
        );

        if (undoStates.Count >
            maximumHistoryStates)
        {
            undoStates.RemoveAt(0);
        }

        redoStates.Clear();
    }

    public void Undo()
    {
        if (sculptMesh == null ||
            undoStates.Count == 0)
        {
            return;
        }

        redoStates.Add(
            CopyVertices(sculptMesh.vertices)
        );

        int lastStateIndex =
            undoStates.Count - 1;

        RestoreState(
            undoStates[lastStateIndex]
        );

        undoStates.RemoveAt(lastStateIndex);
    }

    public void Redo()
    {
        if (sculptMesh == null ||
            redoStates.Count == 0)
        {
            return;
        }

        undoStates.Add(
            CopyVertices(sculptMesh.vertices)
        );

        int lastStateIndex =
            redoStates.Count - 1;

        RestoreState(
            redoStates[lastStateIndex]
        );

        redoStates.RemoveAt(lastStateIndex);
    }

    private void RestoreState(
        Vector3[] savedVertices
    )
    {
        sculptMesh.vertices =
            CopyVertices(savedVertices);

        sculptMesh.RecalculateNormals();
        sculptMesh.RecalculateBounds();

        if (meshCollider != null)
        {
            meshCollider.sharedMesh = null;
            meshCollider.sharedMesh = sculptMesh;
        }
    }

    private Vector3[] CopyVertices(
        Vector3[] sourceVertices
    )
    {
        Vector3[] copiedVertices =
            new Vector3[sourceVertices.Length];

        System.Array.Copy(
            sourceVertices,
            copiedVertices,
            sourceVertices.Length
        );

        return copiedVertices;
    }
}