using UnityEngine;

[ExecuteAlways]
public class SculptShapeKeyController : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private SkinnedMeshRenderer skinnedMeshRenderer;

    [Header("Shape Key Indexes")]
    [SerializeField] private int widthIndex = 0;
    [SerializeField] private int heightIndex = 1;
    [SerializeField] private int depthIndex = 2;

    [Header("Current Values")]
    [Range(0f, 100f)]
    [SerializeField] private float width = 0f;

    [Range(0f, 100f)]
    [SerializeField] private float height = 0f;

    [Range(0f, 100f)]
    [SerializeField] private float depth = 0f;

    private void OnEnable()
    {
        FindRenderer();
        ApplyShapeKeys();
    }

    private void Update()
    {
        FindRenderer();
        ApplyShapeKeys();
    }

    private void OnValidate()
    {
        FindRenderer();
        ApplyShapeKeys();
    }

    private void FindRenderer()
    {
        if (skinnedMeshRenderer == null)
            skinnedMeshRenderer = GetComponent<SkinnedMeshRenderer>();
    }

    private void ApplyShapeKeys()
    {
        if (skinnedMeshRenderer == null)
            return;

        if (skinnedMeshRenderer.sharedMesh == null)
            return;

        if (widthIndex >= 0 && widthIndex < skinnedMeshRenderer.sharedMesh.blendShapeCount)
            skinnedMeshRenderer.SetBlendShapeWeight(widthIndex, width);

        if (heightIndex >= 0 && heightIndex < skinnedMeshRenderer.sharedMesh.blendShapeCount)
            skinnedMeshRenderer.SetBlendShapeWeight(heightIndex, height);

        if (depthIndex >= 0 && depthIndex < skinnedMeshRenderer.sharedMesh.blendShapeCount)
            skinnedMeshRenderer.SetBlendShapeWeight(depthIndex, depth);
    }

    public void SetWidth(float value)
    {
        width = Mathf.Clamp(value, 0f, 100f);
        ApplyShapeKeys();
    }

    public void SetHeight(float value)
    {
        height = Mathf.Clamp(value, 0f, 100f);
        ApplyShapeKeys();
    }

    public void SetDepth(float value)
    {
        depth = Mathf.Clamp(value, 0f, 100f);
        ApplyShapeKeys();
    }

    public float GetWidth()
    {
        return width;
    }

    public float GetHeight()
    {
        return height;
    }

    public float GetDepth()
    {
        return depth;
    }
}