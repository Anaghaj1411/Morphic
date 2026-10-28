using UnityEngine;
using UnityEngine.UI;

/// Clones the homepage Language button and swaps in the EXIT sprite so
/// pop, color, size, and spacing match Start / Language / Settings.
public sealed class HomeMenuExitButton : MonoBehaviour
{
    private RectTransform selfRect;
    private RectTransform language;
    private RectTransform settings;
    private RectTransform start;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Install()
    {
        if (FindFirstObjectByType<HomeMenuExitButton>() != null)
            return;

        RectTransform language = FindNamedRect("Language Button");
        if (language == null || language.parent == null)
            return;

        Sprite exitSprite = Resources.Load<Sprite>("MorphicExitButton");
        if (exitSprite == null)
        {
            Sprite[] sprites = Resources.LoadAll<Sprite>("MorphicExitButton");
            if (sprites != null && sprites.Length > 0)
                exitSprite = sprites[0];
        }

        GameObject clone = Object.Instantiate(language.gameObject, language.parent);
        clone.name = "Exit Button";
        clone.transform.SetSiblingIndex(language.GetSiblingIndex() + 1);

        LanguagePageController languageController =
            clone.GetComponent<LanguagePageController>();
        if (languageController != null)
        {
            languageController.enabled = false;
            Object.Destroy(languageController);
        }

        Button button = clone.GetComponent<Button>();
        if (button != null)
        {
            button.onClick = new Button.ButtonClickedEvent();
            button.onClick.AddListener(MorphicExitButton.Quit);
        }

        Transform visual = FindChild(clone.transform, "Language Button Visual");
        if (visual != null)
        {
            visual.name = "Exit Button Visual";
            Image image = visual.GetComponent<Image>();
            if (image != null && exitSprite != null)
                image.sprite = exitSprite;

            visual.localScale = new Vector3(1f, 0.88f, 1f);
            MenuButtonHover hover = clone.GetComponent<MenuButtonHover>();
            if (hover != null)
            {
                LanguagePageBuilder.SetField(hover, "normalScale", visual.localScale);
                LanguagePageBuilder.SetField(hover, "targetScale", visual.localScale);
            }
        }

        HomeMenuExitButton follower = clone.AddComponent<HomeMenuExitButton>();
        follower.selfRect = clone.GetComponent<RectTransform>();
        follower.language = language;
        follower.settings = FindNamedRect("Settings Button");
        follower.start = FindNamedRect("Start Button");
        follower.FollowCluster();
    }

    private void LateUpdate()
    {
        FollowCluster();
    }

    private void FollowCluster()
    {
        if (selfRect == null || language == null)
            return;

        selfRect.anchorMin = language.anchorMin;
        selfRect.anchorMax = language.anchorMax;
        selfRect.pivot = language.pivot;
        selfRect.sizeDelta = language.sizeDelta;
        selfRect.localRotation = Quaternion.identity;
        selfRect.localScale = Vector3.one;

        float gap = Spacing(language, settings);
        if (gap < 8f)
            gap = Spacing(start, language);
        if (gap < 8f)
            gap = 96f;

        RectTransform lowest = Lowest(language, settings);
        selfRect.anchoredPosition = new Vector2(
            language.anchoredPosition.x,
            lowest.anchoredPosition.y - gap);
    }

    private static float Spacing(RectTransform a, RectTransform b)
    {
        if (a == null || b == null)
            return 0f;
        return Mathf.Abs(a.anchoredPosition.y - b.anchoredPosition.y);
    }

    private static RectTransform Lowest(RectTransform a, RectTransform b)
    {
        if (a == null)
            return b;
        if (b == null)
            return a;
        return a.anchoredPosition.y <= b.anchoredPosition.y ? a : b;
    }

    private static RectTransform FindNamedRect(string objectName)
    {
        Canvas[] canvases = FindObjectsByType<Canvas>(
            FindObjectsInactive.Include,
            FindObjectsSortMode.None);
        for (int i = 0; i < canvases.Length; i++)
        {
            if (canvases[i] == null)
                continue;
            Transform found = FindChild(canvases[i].transform, objectName);
            if (found != null)
                return found as RectTransform;
        }

        return null;
    }

    private static Transform FindChild(Transform root, string objectName)
    {
        if (root == null)
            return null;

        for (int i = 0; i < root.childCount; i++)
        {
            Transform child = root.GetChild(i);
            if (child.name == objectName)
                return child;
            Transform nested = FindChild(child, objectName);
            if (nested != null)
                return nested;
        }

        return null;
    }
}
