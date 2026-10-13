using System.Collections;
using UnityEngine;

public class LanguagePageController : MonoBehaviour
{
    /// <summary>
    /// A language choice coming from one of the page buttons.
    /// </summary>
    [System.Serializable]
    public struct LanguageChoice
    {
        public string code;
        public string label;
    }

    [Header("References")]
    [SerializeField] private Animator languagePageAnimator;

    [Tooltip("Optional CanvasGroup on the Language Page, faded in alongside the animation.")]
    [SerializeField] private CanvasGroup languagePageCanvasGroup;

    [Tooltip("Trigger that plays the open animation.")]
    [SerializeField] private string openTrigger = "Open";

    public string CurrentLanguageCode { get; private set; } = "en";

    public event System.Action<LanguageChoice> LanguageChanged;

    private bool hasBuilt;

    private void Awake()
    {
        CurrentLanguageCode =
            PlayerPrefs.GetString(
                "Morphic.Language",
                "en");
    }

    public void OpenLanguagePage()
    {
        if (languagePageAnimator == null)
        {
            Debug.LogError(
                "LanguagePageController has no Animator assigned, " +
                "so the Language Page cannot be opened.",
                this
            );

            return;
        }

        StartCoroutine(OpenRoutine());
    }

    private IEnumerator OpenRoutine()
    {
        GameObject page =
            languagePageAnimator.gameObject;

        // Activate first, otherwise a disabled Animator silently
        // discards the trigger.
        page.SetActive(true);

        // Let the Animator initialise before touching it. A trigger
        // set on the same frame the object is enabled can be
        // dropped before the state machine is live.
        yield return null;

        // (Re)build the buttons each time so the page is always in
        // sync with the current language list.
        BuildUI();

        languagePageAnimator.ResetTrigger(openTrigger);

        // An Animator that was disabled with its GameObject can stay
        // disabled on reactivation even though the inspector shows
        // it enabled.
        languagePageAnimator.enabled = true;

        languagePageAnimator.SetTrigger(openTrigger);
        languagePageAnimator.Update(0f);

        if (languagePageCanvasGroup != null)
        {
            languagePageCanvasGroup.alpha = 1f;
            languagePageCanvasGroup.interactable = true;
            languagePageCanvasGroup.blocksRaycasts = true;
        }

        // Rebuild layout so the page is not drawn for a frame at a
        // stale position.
        Canvas.ForceUpdateCanvases();
    }

    private void BuildUI()
    {
        if (languagePageAnimator == null)
        {
            return;
        }

        RectTransform root =
            languagePageAnimator
                .GetComponent<RectTransform>();

        LanguagePageBuilder.Build(root, this);

        hasBuilt = true;
    }

    /// <summary>
    /// Called by each language button.
    /// </summary>
    public void SelectLanguage(LanguageChoice choice)
    {
        CurrentLanguageCode = choice.code;

        PlayerPrefs.SetString(
            "Morphic.Language",
            choice.code);

        PlayerPrefs.Save();

        if (LanguageChanged != null)
        {
            LanguageChanged(choice);
        }

        Debug.Log(
            "Language selected: " + choice.label +
            " (" + choice.code + ")");

        CloseLanguagePage();
    }

    /// <summary>
    /// Called by the back button.
    /// </summary>
    public void CloseLanguagePage()
    {
        if (languagePageAnimator == null)
        {
            return;
        }

        StopAllCoroutines();

        languagePageAnimator.gameObject.SetActive(false);
    }

    private void OnDisable()
    {
        // Nothing to persist; selection is saved on click.
    }
}
