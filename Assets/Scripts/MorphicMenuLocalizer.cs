using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// Covers the baked English on the homepage START / LANGUAGE / SETTINGS /
/// EXIT sprites and draws live labels so the language page can change them.
/// The Morphic logo is left as artwork.
public sealed class MorphicMenuLocalizer : MonoBehaviour
{
    // Inner pink inside the green ring of the 2000x1414 menu sprites.
    // Outer opaque is ~0.689 x 0.201; using that size covered the green stroke.
    private const float PinkWidthFrac = 0.628f;
    private const float PinkHeightFrac = 0.112f;
    private static readonly Color32 LabelGreen = new Color32(52, 101, 29, 255);

    private static readonly string[] ButtonNames =
    {
        "Start Button",
        "Language Button",
        "Settings Button",
        "Exit Button"
    };

    private static readonly string[] ButtonKeys =
    {
        "START",
        "LANGUAGE",
        "SETTINGS",
        "EXIT"
    };

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Install()
    {
        MorphicMenuLocalizer existing =
            Object.FindFirstObjectByType<MorphicMenuLocalizer>();
        if (existing != null)
        {
            existing.ApplyMenuLabels();
            return;
        }

        GameObject host = new GameObject("MorphicMenuLocalizer");
        Object.DontDestroyOnLoad(host);
        host.AddComponent<MorphicMenuLocalizer>();
    }

    private void OnEnable()
    {
        MorphicLoc.Changed += ApplyMenuLabels;
        SceneManager.sceneLoaded += OnSceneLoaded;
        ApplyMenuLabels();
        StartCoroutine(Retry());
    }

    private void OnDisable()
    {
        MorphicLoc.Changed -= ApplyMenuLabels;
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        ApplyMenuLabels();
        StartCoroutine(Retry());
    }

    private IEnumerator Retry()
    {
        yield return null;
        ApplyMenuLabels();
        yield return null;
        ApplyMenuLabels();
    }

    private void ApplyMenuLabels()
    {
        ShowHomeSettingsButton();
        for (int i = 0; i < ButtonNames.Length; i++)
            OverlayButton(ButtonNames[i], ButtonKeys[i]);
    }

    public static void ShowHomeSettingsButton()
    {
        Transform button = FindNamed("Settings Button");
        if (button != null)
            button.gameObject.SetActive(true);
    }

    private static void OverlayButton(string objectName, string english)
    {
        Transform button = FindNamed(objectName);
        if (button == null)
            return;

        Transform visual = FindVisual(button);
        if (visual == null)
            visual = button;

        RectTransform visualRect = visual as RectTransform;
        if (visualRect == null)
            return;

        Transform overlay = visual.Find("MorphicMenuLabel");
        Image spriteImage = visual.GetComponent<Image>();
        Vector2 size = OverlaySize(spriteImage, visualRect);
        TMP_Text label;
        if (overlay == null)
        {
            Image fill = LanguagePageBuilder.CreateRoundedImage(
                "MorphicMenuLabel",
                visual,
                20,
                LanguagePageBuilder.Pink);
            fill.raycastTarget = false;
            RectTransform fillRt = fill.rectTransform;
            fillRt.anchorMin = new Vector2(0.5f, 0.5f);
            fillRt.anchorMax = new Vector2(0.5f, 0.5f);
            fillRt.pivot = new Vector2(0.5f, 0.5f);
            fillRt.anchoredPosition = Vector2.zero;
            fillRt.sizeDelta = size;
            fillRt.SetAsLastSibling();

            LanguagePageBuilder.TextLabel text =
                LanguagePageBuilder.CreateLabel(
                    "Label",
                    fill.transform,
                    english,
                    26);
            LanguagePageBuilder.Stretch(text.Rect, new Vector2(18f, 4f));
            label = text.Label;
            overlay = fill.transform;
        }
        else
        {
            label = overlay.GetComponentInChildren<TMP_Text>(true);
            Image fill = overlay.GetComponent<Image>();
            if (fill != null)
            {
                fill.color = LanguagePageBuilder.Pink;
                fill.rectTransform.sizeDelta = size;
            }
        }

        StyleMenuLabel(label, size.y);
        if (label != null)
            MorphicLoc.Bind(label, english);
    }

    private static void StyleMenuLabel(TMP_Text label, float innerHeight)
    {
        if (label == null)
            return;

        float maxSize = Mathf.Clamp(innerHeight * 0.52f, 20f, 28f);
        label.enableAutoSizing = true;
        label.fontSizeMin = 14f;
        label.fontSizeMax = maxSize;
        label.fontStyle = FontStyles.Bold;
        label.characterSpacing = 8f;
        label.overflowMode = TextOverflowModes.Overflow;
        label.alignment = TextAlignmentOptions.Center;
        label.color = LabelGreen;
        label.raycastTarget = false;
    }

    private static Vector2 OverlaySize(Image image, RectTransform visual)
    {
        Vector2 visualSize = visual.rect.size;
        if (visualSize.x < 8f || visualSize.y < 8f)
            visualSize = visual.sizeDelta;
        if (visualSize.x < 8f)
            visualSize = new Vector2(1700f, 450f);

        Vector2 drawn = visualSize;
        if (image != null && image.sprite != null && image.preserveAspect)
        {
            Vector2 spriteSize = image.sprite.rect.size;
            if (spriteSize.y > 0.01f && visualSize.y > 0.01f)
            {
                float spriteAspect = spriteSize.x / spriteSize.y;
                float rectAspect = visualSize.x / visualSize.y;
                if (spriteAspect > rectAspect)
                    drawn = new Vector2(visualSize.x, visualSize.x / spriteAspect);
                else
                    drawn = new Vector2(visualSize.y * spriteAspect, visualSize.y);
            }
        }

        return new Vector2(
            Mathf.Max(160f, drawn.x * PinkWidthFrac),
            Mathf.Max(40f, drawn.y * PinkHeightFrac));
    }

    private static Transform FindVisual(Transform button)
    {
        Transform named = FindChild(button, button.name + " Visual");
        if (named != null)
            return named;

        for (int i = 0; i < button.childCount; i++)
        {
            Transform child = button.GetChild(i);
            if (child.name.EndsWith(" Visual") || child.name.EndsWith("Visual"))
                return child;
        }

        return null;
    }

    private static Transform FindNamed(string objectName)
    {
        Canvas[] canvases = Object.FindObjectsByType<Canvas>(
            FindObjectsInactive.Include,
            FindObjectsSortMode.None);
        for (int i = 0; i < canvases.Length; i++)
        {
            if (canvases[i] == null)
                continue;
            Transform found = FindChild(canvases[i].transform, objectName);
            if (found != null)
                return found;
        }

        GameObject go = GameObject.Find(objectName);
        return go != null ? go.transform : null;
    }

    private static Transform FindChild(Transform root, string objectName)
    {
        if (root == null)
            return null;
        if (root.name == objectName)
            return root;

        for (int i = 0; i < root.childCount; i++)
        {
            Transform nested = FindChild(root.GetChild(i), objectName);
            if (nested != null)
                return nested;
        }

        return null;
    }
}
