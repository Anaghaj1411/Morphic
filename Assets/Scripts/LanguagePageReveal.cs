using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Reveals the language option buttons one at a time.
///
/// Each button fades in and grows from a slightly smaller scale,
/// staggered by <see cref="Stagger"/> so the list reads as a
/// sequence instead of appearing all at once.
/// </summary>
public sealed class LanguagePageReveal : MonoBehaviour
{
    private const float Stagger = 0.14f;
    private const float Duration = 0.28f;
    private const float FromScale = 0.88f;

    public static void Play(
        RectTransform context,
        List<Image> buttons)
    {
        if (context == null ||
            buttons == null)
        {
            return;
        }

        LanguagePageReveal reveal =
            context.gameObject.AddComponent<
                LanguagePageReveal>();

        reveal.BeginReveal(buttons);
    }

    private void BeginReveal(List<Image> buttons)
    {
        StartCoroutine(Run(buttons));
    }

    private IEnumerator Run(List<Image> buttons)
    {
        // Hide everything first so nothing flashes before the
        // first frame of the stagger begins.
        for (int i = 0; i < buttons.Count; i++)
        {
            Prepare(buttons[i]);
        }

        for (int i = 0; i < buttons.Count; i++)
        {
            yield return new WaitForSeconds(Stagger);

            yield return FadeIn(buttons[i]);
        }
    }

    private static void Prepare(Image button)
    {
        if (button == null)
        {
            return;
        }

        CanvasGroup group = button.gameObject
            .AddComponent<CanvasGroup>();

        group.alpha = 0f;

        // Ignore clicks until the button is actually visible.
        group.blocksRaycasts = false;
        group.interactable = false;

        button.rectTransform.localScale = Vector3.one * FromScale;
    }

    private static IEnumerator FadeIn(Image button)
    {
        if (button == null)
        {
            yield break;
        }

        CanvasGroup group = button.GetComponent<CanvasGroup>();

        if (group == null)
        {
            yield break;
        }

        float elapsed = 0f;

        while (elapsed < Duration)
        {
            elapsed += Time.deltaTime;

            float t = Mathf.Clamp01(elapsed / Duration);

            // Ease-out so the button settles gently.
            float eased = 1f - (1f - t) * (1f - t);

            group.alpha = eased;

            button.rectTransform.localScale =
                Vector3.one * Mathf.Lerp(
                    FromScale,
                    1f,
                    eased);

            yield return null;
        }

        group.alpha = 1f;
        group.blocksRaycasts = true;
        group.interactable = true;

        button.rectTransform.localScale = Vector3.one;
    }
}