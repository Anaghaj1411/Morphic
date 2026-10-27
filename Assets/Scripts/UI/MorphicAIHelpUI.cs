using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class MorphicAIHelpUI : MonoBehaviour
{
    private Button helpButton;
    private TMP_Text helpText;
    private GameObject root;
    private GameObject dialogueBox;
    private MorphicAITriggerEngine trigger;
    private MorphicAIClient client;
    private string pendingLocalHelp;

    public void SetVisible(bool visible)
    {
        EnsureUi();
        if (root == null)
            return;
        root.SetActive(visible);
        if (visible)
            root.transform.SetAsLastSibling();
    }

    private void Awake()
    {
        EnsureAi();
        EnsureUi();
        SetVisible(false);
    }

    private void OnDestroy()
    {
        if (client != null)
        {
            client.OnFeedbackMessageReceived -= HandleAiMessage;
            client.OnRequestFailed -= HandleAiFailed;
        }
    }

    private void EnsureAi()
    {
        trigger = FindFirstObjectByType<MorphicAITriggerEngine>(FindObjectsInactive.Include);
        client = FindFirstObjectByType<MorphicAIClient>(FindObjectsInactive.Include);
        if (trigger != null && client != null)
        {
            BindClient();
            return;
        }

        GameObject host = GameObject.Find("MorphicAI");
        if (host == null)
            host = new GameObject("MorphicAI");

        if (host.GetComponent<MorphicAISettings>() == null)
            host.AddComponent<MorphicAISettings>();
        if (host.GetComponent<MorphicAIBehaviorTracker>() == null)
            host.AddComponent<MorphicAIBehaviorTracker>();
        if (host.GetComponent<MorphicAIActionRouter>() == null)
            host.AddComponent<MorphicAIActionRouter>();
        // Add dependencies before the client: AddComponent invokes Awake
        // immediately, and the client resolves its router from this object.
        if (host.GetComponent<MorphicAIClient>() == null)
            host.AddComponent<MorphicAIClient>();
        if (trigger == null)
            trigger = host.GetComponent<MorphicAITriggerEngine>() ??
                host.AddComponent<MorphicAITriggerEngine>();
        if (client == null)
            client = host.GetComponent<MorphicAIClient>();
        BindClient();
    }

    private void BindClient()
    {
        if (client == null)
            return;
        client.OnFeedbackMessageReceived -= HandleAiMessage;
        client.OnFeedbackMessageReceived += HandleAiMessage;
        client.OnRequestFailed -= HandleAiFailed;
        client.OnRequestFailed += HandleAiFailed;
    }

    private void EnsureUi()
    {
        if (root != null)
            return;

        Canvas canvas = FindFirstObjectByType<Canvas>();
        if (canvas == null)
            return;

        Transform existing = canvas.transform.Find("MorphicAIHelp");
        if (existing != null)
        {
            root = existing.gameObject;
            StretchFull(root.GetComponent<RectTransform>());
            helpButton = existing.Find("AiHelp") != null
                ? existing.Find("AiHelp").GetComponent<Button>()
                : existing.GetComponentInChildren<Button>(true);
            Transform dialogue = existing.Find("LisaDialogue");
            if (dialogue != null)
            {
                dialogueBox = dialogue.gameObject;
                helpText = dialogue.GetComponentInChildren<TMP_Text>(true);
            }
            if (helpButton != null)
            {
                helpButton.onClick.RemoveListener(AskForHelp);
                helpButton.onClick.AddListener(AskForHelp);
            }
            if (dialogueBox == null || helpText == null)
                BuildDialogueBox(root.transform);
            return;
        }

        root = new GameObject("MorphicAIHelp", typeof(RectTransform));
        RectTransform rootRect = root.GetComponent<RectTransform>();
        rootRect.SetParent(canvas.transform, false);
        StretchFull(rootRect);

        Image frame = LanguagePageBuilder.CreateRoundedImage(
            "AiHelp", root.transform, 28, LanguagePageBuilder.Green);
        RectTransform buttonRect = frame.rectTransform;
        buttonRect.anchorMin = buttonRect.anchorMax = new Vector2(1f, 1f);
        buttonRect.pivot = new Vector2(1f, 1f);
        buttonRect.anchoredPosition = new Vector2(-24f, -24f);
        buttonRect.sizeDelta = new Vector2(150f, 54f);
        frame.raycastTarget = true;

        Image fill = LanguagePageBuilder.CreateRoundedImage(
            "Fill", frame.transform, 24, new Color32(155, 92, 130, 255));
        RectTransform fillRect = fill.rectTransform;
        fillRect.anchorMin = Vector2.zero;
        fillRect.anchorMax = Vector2.one;
        fillRect.offsetMin = new Vector2(5f, 5f);
        fillRect.offsetMax = new Vector2(-5f, -5f);
        fill.raycastTarget = true;

        helpButton = frame.gameObject.AddComponent<Button>();
        helpButton.targetGraphic = fill;
        helpButton.onClick.AddListener(AskForHelp);

        TMP_Text label = CreateLabel(fill.transform, "AI HELP", 20f, TextAlignmentOptions.Center);
        label.fontStyle = FontStyles.Bold;

        BuildDialogueBox(root.transform);
    }

    private void BuildDialogueBox(Transform parent)
    {
        Image outline = LanguagePageBuilder.CreateRoundedImage(
            "LisaDialogue", parent, 32, LanguagePageBuilder.Green);
        dialogueBox = outline.gameObject;
        RectTransform boxRect = outline.rectTransform;
        boxRect.anchorMin = boxRect.anchorMax = new Vector2(0.5f, 0f);
        boxRect.pivot = new Vector2(0.5f, 0f);
        boxRect.anchoredPosition = new Vector2(0f, 28f);
        boxRect.sizeDelta = new Vector2(860f, 156f);
        outline.raycastTarget = false;

        Image cream = LanguagePageBuilder.CreateRoundedImage(
            "Fill", outline.transform, 28, new Color32(255, 239, 214, 255));
        RectTransform creamRect = cream.rectTransform;
        creamRect.anchorMin = Vector2.zero;
        creamRect.anchorMax = Vector2.one;
        creamRect.offsetMin = new Vector2(8f, 8f);
        creamRect.offsetMax = new Vector2(-8f, -8f);
        cream.raycastTarget = false;

        helpText = CreateLabel(
            cream.transform, "", 20f, TextAlignmentOptions.TopLeft);
        helpText.color = new Color32(52, 40, 28, 255);
        helpText.enableWordWrapping = true;
        helpText.overflowMode = TextOverflowModes.Ellipsis;
        helpText.lineSpacing = 4f;
        RectTransform textRect = helpText.rectTransform;
        textRect.offsetMin = new Vector2(24f, 18f);
        textRect.offsetMax = new Vector2(-24f, -18f);

        dialogueBox.SetActive(false);
    }

    private static void StretchFull(RectTransform rect)
    {
        if (rect == null)
            return;
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = Vector2.zero;
        rect.sizeDelta = Vector2.zero;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
    }

    private static TMP_Text CreateLabel(
        Transform parent,
        string text,
        float size,
        TextAlignmentOptions align)
    {
        GameObject go = new GameObject("Label", typeof(RectTransform));
        go.transform.SetParent(parent, false);
        TMP_Text tmp = go.AddComponent<TextMeshProUGUI>();
        tmp.text = text;
        tmp.fontSize = size;
        tmp.alignment = align;
        tmp.color = LanguagePageBuilder.Green;
        tmp.raycastTarget = false;
        RectTransform rect = tmp.rectTransform;
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        return tmp;
    }

    private void AskForHelp()
    {
        MorphicCraftHelpState state = BuildHelpState();
        pendingLocalHelp = LocalHelpFromState(state);
        ShowMessage("LISA: Asking...");

        EnsureAi();
        if (trigger == null || !trigger.RequestSculptHelp(state))
            ShowMessage(pendingLocalHelp);
    }

    private MorphicCraftHelpState BuildHelpState()
    {
        MorphicCraftHelpState state = new MorphicCraftHelpState
        {
            userQuestion = "Next sculpt step?",
            currentStage = "sculpt",
            currentTool = "lantern"
        };

        SculptingEnhancementTeapotColorController teapotColors =
            FindFirstObjectByType<SculptingEnhancementTeapotColorController>(
                FindObjectsInactive.Include);
        TeapotGestureController teapotGestures =
            FindFirstObjectByType<TeapotGestureController>(FindObjectsInactive.Include);
        bool teapotActive = teapotColors != null &&
            teapotColors.isActiveAndEnabled &&
            teapotColors.gameObject.activeInHierarchy;

        if (teapotActive)
        {
            if (teapotGestures != null)
                teapotGestures.WriteHelpWeights(state);
            teapotColors.WriteHelpDecor(state);
            return state;
        }

        TaiwanLanternGestureController gestures =
            FindFirstObjectByType<TaiwanLanternGestureController>(FindObjectsInactive.Include);
        if (gestures != null)
            gestures.WriteHelpWeights(state);

        SculptingEnhancementLanternColorController colors =
            FindFirstObjectByType<SculptingEnhancementLanternColorController>(
                FindObjectsInactive.Include);
        if (colors != null)
            colors.WriteHelpDecor(state);

        return state;
    }

    private static string LocalHelpFromState(MorphicCraftHelpState state)
    {
        if (state == null)
            return "LISA: Try a gesture, then ask again.";
        if (state.currentTool == "teapot")
        {
            if (state.tassel < 20)
                return "LISA: Two fists, pull down to bring the handle out.";
            if (state.height < 40)
                return "LISA: Point both index fingers and lift to make it taller.";
            if (state.bamboo < 20)
                return "LISA: Two V shapes, spread them to shape the middle.";
            if (state.hasStamp == 0 && state.hasSelfie == 0)
                return "LISA: Add a stamp or a selfie on the teapot body.";
            if (state.width < 20)
                return "LISA: Spread both index fingers to give it more width.";
            return "LISA: This looks ready. Open Finish when you are done.";
        }

        if (state.tassel < 20)
            return "LISA: Two fists, pull down to bring the tassels out.";
        if (state.height < 40)
            return "LISA: Point both index fingers and lift to make it taller.";
        if (state.bamboo < 20)
            return "LISA: Two V shapes, spread them to pop the bamboo.";
        if (state.hasStamp == 0 && state.hasSelfie == 0)
            return "LISA: Add a stamp or a selfie on the body.";
        if (state.width < 20)
            return "LISA: Spread both index fingers to give it more width.";
        return "LISA: This looks ready. Open Review and put it on the shelf.";
    }

    private void HandleAiMessage(string message)
    {
        if (string.IsNullOrWhiteSpace(message))
            return;
        ShowMessage(message.StartsWith("LISA") ? message : "LISA: " + message);
    }

    private void HandleAiFailed()
    {
        if (!string.IsNullOrEmpty(pendingLocalHelp))
            ShowMessage(pendingLocalHelp);
    }

    private void ShowMessage(string message)
    {
        if (dialogueBox != null)
            dialogueBox.SetActive(!string.IsNullOrWhiteSpace(message));
        if (helpText != null)
            helpText.text = message;

        MorphicAIFeedbackUI feedback =
            FindFirstObjectByType<MorphicAIFeedbackUI>(FindObjectsInactive.Include);
        if (feedback != null)
            feedback.ShowHint(message);
    }
}
