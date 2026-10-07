using System.Collections;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Drives the Settings popup: builds the panel, opens and closes it,
/// and owns the persisted Music / Sound Effects / Fullscreen values.
///
/// There is no dedicated Settings Page object in the scene, so the
/// panel is created on demand. That keeps the scene free of
/// hand-authored duplicates.
/// </summary>
public class SettingsPageController : MonoBehaviour
{
    private const string MusicKey = "Morphic.MusicVolume";
    private const string SfxKey = "Morphic.SfxVolume";
    private const string FullscreenKey = "Morphic.Fullscreen";

    private const float DefaultVolume = 0.7f;
    private const float Step = 0.1f;

    private const float OpenDuration = 0.30f;
    private const float FromScale = 0.82f;

    private static SettingsPageController instance;

    private RectTransform pageRoot;
    private Coroutine openRoutine;

    // True once the Settings button has been connected at runtime.
    private static bool s_IsBound;

    // Current values, mirrored by the buttons.
    public float MusicVolume { get; private set; }
    public float SfxVolume { get; private set; }
    public bool IsFullscreen { get; private set; }

    /// <summary>
    /// Raised whenever any setting changes, so an audio mixer or
    /// other system can react.
    /// </summary>
    public event System.Action SettingsChanged;

    private void Awake()
    {
        instance = this;

        LoadValues();

        TryBind();
    }

    /// <summary>
    /// Connects the main menu Settings button automatically.
    ///
    /// The Settings button in the scene has no OnClick binding, and
    /// there is no Settings Page object to reference, so the wiring is
    /// done here instead of relying on inspector setup.
    /// </summary>
    public void TryBind()
    {
        // Already bound: nothing to do.
        if (s_IsBound)
        {
            return;
        }

        Button button = FindSettingsButton();

        if (button == null)
        {
            Debug.LogWarning(
                "SettingsPageController could not find a button " +
                "named \"Settings Button\"; the Settings page will " +
                "not open.",
                this
            );

            return;
        }

        // The Settings Button in the scene is a 1700x450 invisible hit
        // area that overlaps the Language Button. Rather than resizing
        // or re-parenting it, leave it completely untouched and add a
        // small, correctly sized clickable area as a child of it.
        BuildHitArea(button);

        s_IsBound = true;
    }

    /// <summary>
    /// Adds a transparent, correctly sized Button over the visible
    /// Settings icon, and routes its clicks to the page.
    /// </summary>
    private static void BuildHitArea(Button original)
    {
        if (original == null)
        {
            return;
        }

        RectTransform parent =
            original.transform as RectTransform;

        if (parent == null)
        {
            return;
        }

        // Make sure the oversized original cannot swallow clicks that
        // belong to the Language Button above it.
        Graphic existing = original.targetGraphic;

        if (existing != null)
        {
            existing.raycastTarget = false;
        }

        GameObject go = new GameObject(
            "Settings Hit Area",
            typeof(RectTransform),
            typeof(CanvasRenderer),
            typeof(Image),
            typeof(Button));

        go.layer = parent.gameObject.layer;

        RectTransform rt = go.GetComponent<RectTransform>();

        rt.SetParent(parent, false);
        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = Vector2.zero;

        // Same footprint as the other menu buttons.
        rt.sizeDelta = new Vector2(500f, 75f);

        Image image = go.GetComponent<Image>();
        image.color = new Color(1f, 1f, 1f, 0f);
        image.raycastTarget = true;

        Button button = go.GetComponent<Button>();
        button.targetGraphic = image;
        button.transition = Selectable.Transition.None;
        button.interactable = true;

        button.onClick.AddListener(Instance.OpenSettingsPage);
    }

    private static Button FindButtonInHierarchy(
        Transform root,
        string name)
    {
        if (root == null)
        {
            return null;
        }

        for (int i = 0; i < root.childCount; i++)
        {
            Transform child = root.GetChild(i);

            if (child.name == name)
            {
                Button direct =
                    child.GetComponent<Button>();

                if (direct != null)
                {
                    return direct;
                }

                // The Button may sit on a child of the named object.
                Button nested =
                    child.GetComponentInChildren<Button>();

                if (nested != null)
                {
                    return nested;
                }
            }

            Button found = FindButtonInHierarchy(child, name);

            if (found != null)
            {
                return found;
            }
        }

        return null;
    }

    private Button FindSettingsButton()
    {
        // Look through every Canvas, since the controller may live
        // outside the menu hierarchy.
        Canvas[] canvases = FindObjectsOfType<Canvas>();

        for (int i = 0; i < canvases.Length; i++)
        {
            Button found = FindButtonInHierarchy(
                canvases[i].transform,
                "Settings Button");

            if (found != null)
            {
                return found;
            }
        }

        return null;
    }

    /// <summary>
    /// Creates the controller if the scene does not already have one,
    /// then lets it bind to the Settings button once the hierarchy is
    /// fully built.
    /// </summary>
    public static void Install()
    {
        if (instance != null)
        {
            return;
        }

        Canvas canvas = FindObjectOfType<Canvas>();

        if (canvas == null)
        {
            Debug.LogWarning(
                "SettingsPageBootstrap found no Canvas in the " +
                "scene, so the Settings page cannot open.");

            return;
        }

        SettingsPageController controller =
            canvas.gameObject.AddComponent<
                SettingsPageController>();

        // Awake has already run by the time AddComponent returns, so
        // the button may not be discoverable yet. Wait a frame and
        // bind again to be safe.
        controller.StartCoroutine(BindNextFrame());
    }

    private static System.Collections.IEnumerator BindNextFrame()
    {
        yield return null;

        if (instance != null)
        {
            instance.TryBind();
        }
    }

    private void OnDestroy()
    {
        if (instance == this)
        {
            instance = null;
        }
    }

    [RuntimeInitializeOnLoadMethod(
        RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        instance = null;
        s_IsBound = false;
    }

    private void LoadValues()
    {
        MusicVolume = PlayerPrefs.GetFloat(MusicKey, DefaultVolume);
        SfxVolume = PlayerPrefs.GetFloat(SfxKey, DefaultVolume);

        IsFullscreen = PlayerPrefs.GetInt(
            FullscreenKey,
            Screen.fullScreen ? 1 : 0) == 1;
    }

    // ------------------------------------------------------------
    // Open / close
    // ------------------------------------------------------------

    /// <summary>
    /// Hook this to the Settings button's OnClick.
    /// </summary>
    public void OpenSettingsPage()
    {
        EnsurePage();

        if (pageRoot == null)
        {
            return;
        }

        pageRoot.gameObject.SetActive(true);

        // Rebuild every time so the panel reflects saved values.
        SettingsPageBuilder.Build(pageRoot, this);

        if (openRoutine != null)
        {
            StopCoroutine(openRoutine);
        }

        openRoutine = StartCoroutine(OpenRoutine(pageRoot));

        Debug.Log("Settings page opened.", this);
    }

    private IEnumerator OpenRoutine(RectTransform root)
    {
        // The root is stretched to fill the canvas, so anchoredPosition
        // is driven by its offsets and animating it does nothing. A scale
        // pop on the root plus a fade is what actually reads on screen.
        CanvasGroup group = root.GetComponent<CanvasGroup>();

        if (group == null)
        {
            group = root.gameObject.AddComponent<CanvasGroup>();
        }

        group.alpha = 0f;
        group.blocksRaycasts = true;
        group.interactable = true;

        root.localScale = Vector3.one * FromScale;

        float elapsed = 0f;

        while (elapsed < OpenDuration)
        {
            elapsed += Time.unscaledDeltaTime;

            float t = Mathf.Clamp01(elapsed / OpenDuration);

            // Ease-out back: overshoots slightly then settles, which
            // gives the panel a springy pop.
            float eased = EaseOutBack(t);

            group.alpha = Mathf.Clamp01(t * 1.4f);

            root.localScale = Vector3.one * Mathf.Lerp(
                FromScale,
                1f,
                eased);

            yield return null;
        }

        group.alpha = 1f;
        root.localScale = Vector3.one;
    }

    /// <summary>
    /// Ease-out back curve. Returns a value that rises past 1 before
    /// settling on it, producing a small overshoot.
    /// </summary>
    private static float EaseOutBack(float t)
    {
        const float c1 = 1.70158f;
        const float c3 = c1 + 1f;

        float p = t - 1f;

        return 1f + c3 * p * p * p + c1 * p * p;
    }

    /// <summary>
    /// Called by the BACK button and by the backdrop.
    /// </summary>
    public void CloseSettingsPage()
    {
        if (pageRoot == null)
        {
            return;
        }

        if (openRoutine != null)
        {
            StopCoroutine(openRoutine);
            openRoutine = null;
        }

        // Reset so the next open always plays the pop from the start.
        pageRoot.localScale = Vector3.one;

        CanvasGroup group = pageRoot.GetComponent<CanvasGroup>();

        if (group != null)
        {
            group.alpha = 1f;
            group.blocksRaycasts = false;
            group.interactable = false;
        }

        pageRoot.gameObject.SetActive(false);
    }

    public bool IsOpen
    {
        get
        {
            return pageRoot != null &&
                   pageRoot.gameObject.activeSelf;
        }
    }

    /// <summary>
    /// Creates the page object on first use, as a child of this
    /// controller so it shares the same Canvas.
    /// </summary>
    private void EnsurePage()
    {
        if (pageRoot != null)
        {
            return;
        }

        GameObject go = new GameObject(
            "Settings Page",
            typeof(RectTransform),
            typeof(CanvasGroup));

        Canvas canvas = GetComponentInParent<Canvas>();

        if (canvas == null)
        {
            canvas = FindObjectOfType<Canvas>();
        }

        if (canvas != null)
        {
            go.layer = canvas.gameObject.layer;
        }

        pageRoot = go.GetComponent<RectTransform>();

        // Fill the whole canvas.
        pageRoot.anchorMin = Vector2.zero;
        pageRoot.anchorMax = Vector2.one;
        pageRoot.pivot = new Vector2(0.5f, 0.5f);
        pageRoot.offsetMin = Vector2.zero;
        pageRoot.offsetMax = Vector2.zero;

        // Parent to the canvas so the page renders in the same
        // hierarchy. This controller may live on a plain GameObject,
        // where `transform` is not a RectTransform.
        RectTransform parent = canvas != null
            ? canvas.transform as RectTransform
            : null;

        if (parent != null)
        {
            pageRoot.SetParent(parent, false);
        }

        pageRoot.gameObject.SetActive(false);
    }

    // ------------------------------------------------------------
    // Setting changes
    // ------------------------------------------------------------

    public void IncreaseMusic()
    {
        SetMusic(MusicVolume + Step);
    }

    public void DecreaseMusic()
    {
        SetMusic(MusicVolume - Step);
    }

    public void IncreaseSfx()
    {
        SetSfx(SfxVolume + Step);
    }

    public void DecreaseSfx()
    {
        SetSfx(SfxVolume - Step);
    }

    public void ToggleFullscreen()
    {
        IsFullscreen = !IsFullscreen;

        Screen.fullScreen = IsFullscreen;

        PlayerPrefs.SetInt(FullscreenKey, IsFullscreen ? 1 : 0);
        PlayerPrefs.Save();

        NotifyChanged();
    }

    private void SetMusic(float value)
    {
        MusicVolume = Mathf.Clamp01(value);

        PlayerPrefs.SetFloat(MusicKey, MusicVolume);
        PlayerPrefs.Save();

        NotifyChanged();
    }

    private void SetSfx(float value)
    {
        SfxVolume = Mathf.Clamp01(value);

        PlayerPrefs.SetFloat(SfxKey, SfxVolume);
        PlayerPrefs.Save();

        NotifyChanged();
    }

    private void NotifyChanged()
    {
        if (SettingsChanged != null)
        {
            SettingsChanged();
        }
    }

    /// <summary>
    /// Convenience accessor for scripts that change settings
    /// without a direct inspector reference.
    /// </summary>
    public static SettingsPageController Instance
    {
        get { return instance; }
    }
}

/// <summary>
/// Creates the SettingsPageController automatically at runtime.
///
/// The Main Menu scene has no Settings Page object, so without this
/// the controller would never exist and the Settings button would do
/// nothing. Waiting one frame ensures the Canvas hierarchy is fully
/// built before the button is located.
/// </summary>
public static class SettingsPageBootstrap
{
    [RuntimeInitializeOnLoadMethod(
        RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Install()
    {
        SettingsPageController.Install();
    }
}