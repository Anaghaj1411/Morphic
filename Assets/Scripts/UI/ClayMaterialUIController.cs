using UnityEngine;

public class ClayMaterialUIController : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private MeshRenderer clayRenderer;

    [Header("Materials")]
    [SerializeField] private Material matteClayMaterial;
    [SerializeField] private Material ceramicMaterial;
    [SerializeField] private Material metalMaterial;

    public void SetMatteClay()
    {
        SetMaterial(matteClayMaterial);
    }

    public void SetCeramic()
    {
        SetMaterial(ceramicMaterial);
    }

    public void SetMetal()
    {
        SetMaterial(metalMaterial);
    }

    private void SetMaterial(Material selectedMaterial)
    {
        if (clayRenderer == null)
        {
            Debug.LogWarning("Clay Material UI Controller: Clay Renderer is missing.");
            return;
        }

        if (selectedMaterial == null)
        {
            Debug.LogWarning("Clay Material UI Controller: Material is missing.");
            return;
        }

        clayRenderer.material = selectedMaterial;
    }
}