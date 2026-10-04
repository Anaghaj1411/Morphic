using UnityEngine;

public class LanguagePageController : MonoBehaviour
{
    [SerializeField] private Animator languagePageAnimator;

    public void OpenLanguagePage()
    {
        if (languagePageAnimator == null) return;

        // Activate the GameObject first
        languagePageAnimator.gameObject.SetActive(true);

        // Wait one frame then trigger animation
        StartCoroutine(TriggerOpenNextFrame());
    }

    private System.Collections.IEnumerator TriggerOpenNextFrame()
    {
        yield return null; // wait one frame so Animator initializes
        languagePageAnimator.SetTrigger("Open");
    }
}
