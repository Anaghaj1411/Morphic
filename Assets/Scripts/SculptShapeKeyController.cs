using UnityEngine;

public class SculptShapeKeyController : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private SkinnedMeshRenderer skinnedMeshRenderer;

    [Header("Shape Key Indices")]
    [SerializeField] private int widthIndex = 0;
    [SerializeField] private int puffIndex = 1;
    [SerializeField] private int bottomFlattenIndex = 2;
    [SerializeField] private int topGatherIndex = 3;
    [SerializeField] private int topPinchIndex = 4;

    [Header("Current Values")]
    [Range(0f, 1f)]
    [SerializeField] private float width = 0f;

    [Range(0f, 1f)]
    [SerializeField] private float puff = 0f;

    [Range(0f, 1f)]
    [SerializeField] private float bottomFlatten = 0f;

    [Range(0f, 1f)]
    [SerializeField] private float topGather = 0f;

    [Range(0f, 1f)]
    [SerializeField] private float topPinch = 0f;

    public float Width => width;
    public float Puff => puff;
    public float BottomFlatten => bottomFlatten;
    public float TopGather => topGather;
    public float TopPinch => topPinch;

    private void Awake()
    {
        if (skinnedMeshRenderer == null)
        {
            skinnedMeshRenderer =
                GetComponent<SkinnedMeshRenderer>();
        }

        if (skinnedMeshRenderer == null)
        {
            Debug.LogError(
                "SculptShapeKeyController: " +
                "SkinnedMeshRenderer reference is missing."
            );

            return;
        }

        ResolveBlendShapeIndices();

        width = 0f;
        puff = 0f;
        bottomFlatten = 0f;
        topGather = 0f;
        topPinch = 0f;

        ApplyAllShapeKeys();
    }

    private void ResolveBlendShapeIndices()
    {
        Mesh mesh = skinnedMeshRenderer.sharedMesh;

        if (mesh == null)
        {
            return;
        }

        TryResolveIndex(mesh, ref widthIndex, "width");
        TryResolveIndex(mesh, ref puffIndex, "puff");
        TryResolveIndex(mesh, ref bottomFlattenIndex, "flatten");
        TryResolveIndex(mesh, ref topGatherIndex, "gather");
        if (!TryResolveIndex(mesh, ref topPinchIndex, "toppinch") &&
            !TryResolveIndex(mesh, ref topPinchIndex, "top pinch"))
        {
            TryResolveIndex(mesh, ref topPinchIndex, "pinch");
        }
    }

    private static bool TryResolveIndex(Mesh mesh, ref int index, string namePart)
    {
        for (int i = 0; i < mesh.blendShapeCount; i++)
        {
            string blendName = mesh.GetBlendShapeName(i);

            if (!string.IsNullOrEmpty(blendName) &&
                blendName.IndexOf(namePart, System.StringComparison.OrdinalIgnoreCase) >= 0)
            {
                index = i;
                return true;
            }
        }

        return false;
    }

    public void SetWidth(float value)
    {
        width = Mathf.Clamp01(value);
        ApplyShapeKey(widthIndex, width);
    }

    public void SetPuff(float value)
    {
        puff = Mathf.Clamp01(value);
        ApplyShapeKey(puffIndex, puff);
    }

    public void SetBottomFlatten(float value)
    {
        bottomFlatten = Mathf.Clamp01(value);
        ApplyShapeKey(
            bottomFlattenIndex,
            bottomFlatten
        );
    }

    public void SetTopGather(float value)
    {
        topGather = Mathf.Clamp01(value);
        ApplyShapeKey(
            topGatherIndex,
            topGather
        );
    }

    public void SetTopPinch(float value)
    {
        topPinch = Mathf.Clamp01(value);
        ApplyShapeKey(
            topPinchIndex,
            topPinch
        );
    }

    public void SetAllShapeKeys(
        float widthValue,
        float puffValue,
        float bottomFlattenValue,
        float topGatherValue,
        float topPinchValue
    )
    {
        width = Mathf.Clamp01(widthValue);
        puff = Mathf.Clamp01(puffValue);
        bottomFlatten =
            Mathf.Clamp01(bottomFlattenValue);
        topGather =
            Mathf.Clamp01(topGatherValue);
        topPinch =
            Mathf.Clamp01(topPinchValue);

        ApplyAllShapeKeys();
    }

    private void ApplyAllShapeKeys()
    {
        if (skinnedMeshRenderer == null)
        {
            return;
        }

        ApplyShapeKey(widthIndex, width);
        ApplyShapeKey(puffIndex, puff);
        ApplyShapeKey(
            bottomFlattenIndex,
            bottomFlatten
        );
        ApplyShapeKey(
            topGatherIndex,
            topGather
        );
        ApplyShapeKey(
            topPinchIndex,
            topPinch
        );
    }

    private void ApplyShapeKey(int index, float value)
    {
        if (skinnedMeshRenderer == null)
        {
            return;
        }

        if (index < 0 ||
            index >= skinnedMeshRenderer.sharedMesh.blendShapeCount)
        {
            Debug.LogError(
                "SculptShapeKeyController: Invalid " +
                $"BlendShape index {index}."
            );

            return;
        }

        skinnedMeshRenderer.SetBlendShapeWeight(
            index,
            value * 100f
        );
    }
}