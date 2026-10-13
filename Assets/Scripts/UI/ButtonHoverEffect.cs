using UnityEngine;
using UnityEngine.EventSystems;

public class ButtonHoverEffect : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    private Vector3 originalScale;
    public float scaleMultiplier = 1.05f;
    public float animationSpeed = 15f;
    
    private Vector3 targetScale;
    private bool initialized = false;

    private void Start()
    {
        if (!initialized)
        {
            originalScale = transform.localScale;
            targetScale = originalScale;
            initialized = true;
        }
    }

    private void Update()
    {
        if (initialized)
        {
            transform.localScale = Vector3.Lerp(transform.localScale, targetScale, Time.deltaTime * animationSpeed);
        }
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (!initialized) Start();
        targetScale = originalScale * scaleMultiplier;
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        if (!initialized) Start();
        targetScale = originalScale;
    }
    
    private void OnDisable()
    {
        // Reset scale when disabled so it doesn't stay large if hidden while hovered
        if (initialized)
        {
            transform.localScale = originalScale;
            targetScale = originalScale;
        }
    }
}
