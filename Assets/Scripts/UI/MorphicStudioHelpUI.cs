using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// One Help pill for the sculpt studio. Gestures and Ask Lisa live in a
/// sheet. Reset Rotation sits beside Help, not inside the sheet.
public sealed class MorphicStudioHelpUI : MonoBehaviour
{
    private GameObject root;
    private GameObject sheet;
    private Button helpButton;

    public void SetVisible(bool visible)
    {
        EnsureUi();
        if (root == null)
            return;
        root.SetActive(visible);
        if (!visible)
            HideSheet();
        else
            root.transform.SetAsLastSibling();
    }

    private void Awake()
    {
        EnsureUi();
        SetVisible(false);
    }

    private void EnsureUi()
    {
        if (root != null)
        {
            WireHelp();
            return;
        }

        Canvas canvas = FindFirstObjectByType<Canvas>();
        if (canvas == null)
            return;

        Transform existing = canvas.transform.Find("MorphicStudioHelp");
        if (existing != null)
        {
            root = existing.gameObject;
            Transform helpFrame = existing.Find("Help");
            helpButton = helpFrame != null
                ? helpFrame.GetComponent<Button>()
                : existing.GetComponentInChildren<Button>(true);
            Transform existingSheet = existing.Find("HelpSheet");
            if (existingSheet != null)
            {
                sheet = existingSheet.gameObject;
                SizeSheet(existingSheet.GetComponent<RectTransform>());
                Transform leftoverReset = existingSheet.Find("Fill/ResetRotation");
                if (leftoverReset != null)
                    leftoverReset.gameObject.SetActive(false);
                Transform close = existingSheet.Find("Fill/Close");
                if (close is RectTransform closeRect)
                    closeRect.anchoredPosition = new Vector2(0f, -222f);
            }
            WireHelp();
            if (sheet == null)
                BuildSheet(root.transform);
            return;
        }

        root = new GameObject("MorphicStudioHelp", typeof(RectTransform));
        RectTransform rootRect = root.GetComponent<RectTransform>();
        rootRect.SetParent(canvas.transform, false);
        StretchFull(rootRect);

        Image frame = LanguagePageBuilder.CreateRoundedImage(
            "Help", root.transform, 28, LanguagePageBuilder.Green);
        RectTransform buttonRect = frame.rectTransform;
        buttonRect.anchorMin = buttonRect.anchorMax = new Vector2(1f, 1f);
        buttonRect.pivot = new Vector2(1f, 1f);
        buttonRect.anchoredPosition = new Vector2(-24f, -24f);
        buttonRect.sizeDelta = new Vector2(132f, 54f);
        frame.raycastTarget = true;

        Image fill = LanguagePageBuilder.CreateRoundedImage(
            "Fill", frame.transform, 24, LanguagePageBuilder.Pink);
        LanguagePageBuilder.Stretch(fill.rectTransform, new Vector2(5f, 5f));
        fill.raycastTarget = true;

        LanguagePageBuilder.TextLabel label = LanguagePageBuilder.CreateLabel(
            "Label", fill.transform, "HELP", 20);
        LanguagePageBuilder.Stretch(label.Rect);
        if (label.Label != null)
            MorphicText.FitButton(label.Label);

        helpButton = frame.gameObject.AddComponent<Button>();
        helpButton.targetGraphic = fill;
        helpButton.colors = LanguagePageBuilder.ButtonColors();
        WireHelp();
        BuildSheet(root.transform);
    }

    private void WireHelp()
    {
        if (helpButton == null)
            return;
        helpButton.onClick.RemoveAllListeners();
        helpButton.onClick.AddListener(ToggleSheet);
    }

    private void BuildSheet(Transform parent)
    {
        Image outline = LanguagePageBuilder.CreateRoundedImage(
            "HelpSheet", parent, 32, LanguagePageBuilder.Green);
        sheet = outline.gameObject;
        RectTransform box = outline.rectTransform;
        box.anchorMin = box.anchorMax = new Vector2(0.5f, 0.5f);
        box.pivot = new Vector2(0.5f, 0.5f);
        SizeSheet(box);
        outline.raycastTarget = true;

        Image cream = LanguagePageBuilder.CreateRoundedImage(
            "Fill", outline.transform, 28, new Color32(255, 239, 214, 255));
        LanguagePageBuilder.Stretch(cream.rectTransform, new Vector2(8f, 8f));
        cream.raycastTarget = true;

        LanguagePageBuilder.TextLabel title = LanguagePageBuilder.CreateLabel(
            "Title", cream.transform, "HELP", 28);
        RectTransform titleRect = title.Rect;
        titleRect.anchorMin = titleRect.anchorMax = new Vector2(0.5f, 1f);
        titleRect.pivot = new Vector2(0.5f, 1f);
        titleRect.anchoredPosition = new Vector2(0f, -18f);
        titleRect.sizeDelta = new Vector2(360f, 44f);
        if (title.Label != null)
            MorphicText.FitTitle(title.Label);

        CreateSheetButton(cream.transform, "Gestures", "GESTURES", new Vector2(0f, -78f), OpenGestures);
        CreateSheetButton(cream.transform, "AskLisa", "ASK LISA", new Vector2(0f, -150f), OpenAskLisa);
        CreateSheetButton(cream.transform, "Close", "DONE", new Vector2(0f, -222f), HideSheet);
        Transform leftoverReset = cream.transform.Find("ResetRotation");
        if (leftoverReset != null)
            leftoverReset.gameObject.SetActive(false);
        sheet.SetActive(false);
    }

    private static void SizeSheet(RectTransform box)
    {
        if (box == null)
            return;
        box.sizeDelta = new Vector2(420f, 348f);
    }

    private void CreateSheetButton(
        Transform parent,
        string name,
        string english,
        Vector2 pos,
        UnityEngine.Events.UnityAction click)
    {
        Image frame = LanguagePageBuilder.CreateRoundedImage(
            name, parent, 24, LanguagePageBuilder.Green);
        RectTransform rect = frame.rectTransform;
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 1f);
        rect.pivot = new Vector2(0.5f, 1f);
        rect.anchoredPosition = pos;
        rect.sizeDelta = new Vector2(340f, 60f);
        frame.raycastTarget = true;

        Image fill = LanguagePageBuilder.CreateRoundedImage(
            "Fill", frame.transform, 20, LanguagePageBuilder.Pink);
        LanguagePageBuilder.Stretch(fill.rectTransform, new Vector2(5f, 5f));
        fill.raycastTarget = true;

        LanguagePageBuilder.TextLabel label = LanguagePageBuilder.CreateLabel(
            "Label", fill.transform, english, 22);
        LanguagePageBuilder.Stretch(label.Rect);
        if (label.Label != null)
            MorphicText.FitButton(label.Label);

        Button button = frame.gameObject.AddComponent<Button>();
        button.targetGraphic = fill;
        button.colors = LanguagePageBuilder.ButtonColors();
        button.onClick.AddListener(click);
    }

    private void ToggleSheet()
    {
        EnsureUi();
        if (sheet == null)
            return;
        bool open = !sheet.activeSelf;
        sheet.SetActive(open);
        if (open)
        {
            sheet.transform.SetAsLastSibling();
            MorphicAIHelpUI ai = FindFirstObjectByType<MorphicAIHelpUI>(FindObjectsInactive.Include);
            if (ai != null)
                ai.WarmBackend();
        }
    }

    public void HideSheet()
    {
        if (root != null)
        {
            Transform found = root.transform.Find("HelpSheet");
            if (found != null)
                sheet = found.gameObject;
        }

        if (sheet != null)
            sheet.SetActive(false);
    }

    private void OpenGestures()
    {
        HideSheet();
        DialogueManager dialogue = FindFirstObjectByType<DialogueManager>(FindObjectsInactive.Include);
        if (dialogue != null)
            dialogue.OpenGestureCards();
    }

    private void OpenAskLisa()
    {
        HideSheet();
        StartCoroutine(OpenAskAfterClose());
    }

    private IEnumerator OpenAskAfterClose()
    {
        HideSheet();
        yield return null;
        HideSheet();
        MorphicAIHelpUI ai = FindFirstObjectByType<MorphicAIHelpUI>(FindObjectsInactive.Include);
        if (ai != null)
            ai.OpenAskFromStudio();
    }

    private static void StretchFull(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
    }
}
