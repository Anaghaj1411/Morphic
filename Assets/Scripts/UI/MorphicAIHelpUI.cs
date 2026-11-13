using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class MorphicAIHelpUI : MonoBehaviour
{
    private Button helpButton;
    private TMP_Text helpText;
    private TMP_InputField questionInput;
    private GameObject root;
    private GameObject askCard;
    private GameObject askDimmer;
    private bool openedFromStudio;
    private MorphicAITriggerEngine trigger;
    private MorphicAIClient client;

    public void SetVisible(bool visible)
    {
        EnsureUi();
        if (root == null)
            return;
        root.SetActive(visible);
        if (visible)
        {
            openedFromStudio = false;
            if (helpButton != null)
                helpButton.gameObject.SetActive(true);
            root.transform.SetAsLastSibling();
            WarmBackend();
        }
        else
        {
            MorphicLisaVoice.Stop();
            HideAskCard();
        }
    }

    public void OpenAskFromStudio()
    {
        openedFromStudio = true;
        MorphicStudioHelpUI studioHelp =
            FindFirstObjectByType<MorphicStudioHelpUI>(FindObjectsInactive.Include);
        if (studioHelp != null)
            studioHelp.HideSheet();

        EnsureUi();
        if (root == null)
            return;
        root.SetActive(true);
        if (helpButton != null)
            helpButton.gameObject.SetActive(false);
        root.transform.SetAsLastSibling();
        WarmBackend();
        OpenAskCard();
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

        if (questionInput != null)
            questionInput.onSubmit.RemoveListener(HandleInputSubmit);
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
        WarmBackend();
    }

    public void WarmBackend()
    {
        EnsureAi();
        if (client != null)
            client.Warmup();
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
        {
            if (askCard == null)
                BuildAskCard(root.transform);
            return;
        }

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
            WireHelpButton();
            Transform oldDialogue = existing.Find("LisaDialogue");
            if (oldDialogue != null)
                oldDialogue.gameObject.SetActive(false);
            BuildAskCard(root.transform);
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
        WireHelpButton();

        TMP_Text label = CreateLabel(fill.transform, "AI HELP", 20f, TextAlignmentOptions.Center);

        BuildAskCard(root.transform);
    }

    private void WireHelpButton()
    {
        if (helpButton == null)
            return;
        helpButton.onClick.RemoveAllListeners();
        helpButton.onClick.AddListener(OpenAskCard);
    }

    private void BuildAskCard(Transform parent)
    {
        if (askCard != null)
            return;

        Transform existingAsk = parent.Find("LisaAsk");
        if (existingAsk != null)
            Destroy(existingAsk.gameObject);

        Image outline = LanguagePageBuilder.CreateRoundedImage(
            "LisaAsk", parent, 32, LanguagePageBuilder.Green);
        askCard = outline.gameObject;
        RectTransform boxRect = outline.rectTransform;
        boxRect.anchorMin = boxRect.anchorMax = new Vector2(0.5f, 0f);
        boxRect.pivot = new Vector2(0.5f, 0f);
        boxRect.anchoredPosition = new Vector2(0f, 28f);
        boxRect.sizeDelta = new Vector2(760f, 108f);
        outline.raycastTarget = true;

        Image cream = LanguagePageBuilder.CreateRoundedImage(
            "Fill", outline.transform, 28, new Color32(255, 239, 214, 255));
        RectTransform creamRect = cream.rectTransform;
        creamRect.anchorMin = Vector2.zero;
        creamRect.anchorMax = Vector2.one;
        creamRect.offsetMin = new Vector2(8f, 8f);
        creamRect.offsetMax = new Vector2(-8f, -8f);
        cream.raycastTarget = true;

        helpText = CreateLabel(
            cream.transform, "", 24f, TextAlignmentOptions.TopLeft);
        helpText.color = new Color32(32, 24, 18, 255);
        helpText.enableWordWrapping = true;
        helpText.overflowMode = TextOverflowModes.Overflow;
        helpText.lineSpacing = 4f;
        RectTransform textRect = helpText.rectTransform;
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.pivot = new Vector2(0.5f, 1f);
        textRect.offsetMin = new Vector2(16f, 80f);
        textRect.offsetMax = new Vector2(-16f, -12f);
        helpText.gameObject.SetActive(false);

        Image sendFrame = LanguagePageBuilder.CreateRoundedImage(
            "Send", cream.transform, 18, LanguagePageBuilder.Green);
        RectTransform sendRect = sendFrame.rectTransform;
        sendRect.anchorMin = sendRect.anchorMax = new Vector2(1f, 0f);
        sendRect.pivot = new Vector2(1f, 0f);
        sendRect.anchoredPosition = new Vector2(-64f, 12f);
        sendRect.sizeDelta = new Vector2(118f, 52f);
        sendFrame.raycastTarget = true;

        Image sendFill = LanguagePageBuilder.CreateRoundedImage(
            "Fill", sendFrame.transform, 14, new Color32(155, 92, 130, 255));
        RectTransform sendFillRect = sendFill.rectTransform;
        sendFillRect.anchorMin = Vector2.zero;
        sendFillRect.anchorMax = Vector2.one;
        sendFillRect.offsetMin = new Vector2(4f, 4f);
        sendFillRect.offsetMax = new Vector2(-4f, -4f);
        sendFill.raycastTarget = true;

        Button sendButton = sendFrame.gameObject.AddComponent<Button>();
        sendButton.targetGraphic = sendFill;
        sendButton.onClick.AddListener(SendQuestion);
        TMP_Text sendLabel = CreateLabel(sendFill.transform, "SEND", 20f, TextAlignmentOptions.Center);

        CreateCloseButton(cream.transform);
        questionInput = CreateQuestionInput(cream.transform);
        BuildAskDimmer(parent);
        askCard.SetActive(false);
    }

    private void CreateCloseButton(Transform parent)
    {
        Image frame = LanguagePageBuilder.CreateRoundedImage(
            "Close", parent, 18, LanguagePageBuilder.Green);
        RectTransform rect = frame.rectTransform;
        rect.anchorMin = rect.anchorMax = new Vector2(1f, 0f);
        rect.pivot = new Vector2(1f, 0f);
        rect.anchoredPosition = new Vector2(-12f, 12f);
        rect.sizeDelta = new Vector2(44f, 52f);
        frame.raycastTarget = true;

        Image fill = LanguagePageBuilder.CreateRoundedImage(
            "Fill", frame.transform, 14, LanguagePageBuilder.Pink);
        RectTransform fillRect = fill.rectTransform;
        fillRect.anchorMin = Vector2.zero;
        fillRect.anchorMax = Vector2.one;
        fillRect.offsetMin = new Vector2(4f, 4f);
        fillRect.offsetMax = new Vector2(-4f, -4f);
        fill.raycastTarget = true;

        Button button = frame.gameObject.AddComponent<Button>();
        button.targetGraphic = fill;
        button.colors = LanguagePageBuilder.ButtonColors();
        button.onClick.AddListener(CloseAskDialog);
        TMP_Text label = CreateLabel(fill.transform, "X", 22f, TextAlignmentOptions.Center);
    }

    private void BuildAskDimmer(Transform parent)
    {
        if (askDimmer != null)
            return;

        Transform existing = parent.Find("AskDimmer");
        if (existing != null)
            Destroy(existing.gameObject);

        GameObject dimmer = new GameObject("AskDimmer", typeof(RectTransform), typeof(Image), typeof(Button));
        dimmer.transform.SetParent(parent, false);
        dimmer.transform.SetAsFirstSibling();
        StretchFull(dimmer.GetComponent<RectTransform>());

        Image image = dimmer.GetComponent<Image>();
        image.color = new Color(0f, 0f, 0f, 0.18f);
        image.raycastTarget = true;

        Button button = dimmer.GetComponent<Button>();
        button.targetGraphic = image;
        button.transition = Selectable.Transition.None;
        button.onClick.RemoveAllListeners();
        button.interactable = false;
        image.raycastTarget = false;
        askDimmer = dimmer;
        askDimmer.SetActive(false);
    }

    private TMP_InputField CreateQuestionInput(Transform parent)
    {
        GameObject inputRoot = new GameObject("QuestionInput", typeof(RectTransform), typeof(Image));
        inputRoot.transform.SetParent(parent, false);

        Image inputBg = inputRoot.GetComponent<Image>();
        LanguagePageBuilder.ApplyRoundedSprite(inputBg, 14);
        inputBg.color = Color.white;
        inputBg.raycastTarget = true;

        RectTransform inputRect = inputRoot.GetComponent<RectTransform>();
        inputRect.anchorMin = new Vector2(0f, 0f);
        inputRect.anchorMax = new Vector2(1f, 0f);
        inputRect.pivot = new Vector2(0.5f, 0f);
        inputRect.offsetMin = new Vector2(12f, 12f);
        inputRect.offsetMax = new Vector2(-198f, 64f);

        GameObject textArea = new GameObject("Text Area", typeof(RectTransform));
        textArea.transform.SetParent(inputRoot.transform, false);
        RectTransform areaRect = textArea.GetComponent<RectTransform>();
        areaRect.anchorMin = Vector2.zero;
        areaRect.anchorMax = Vector2.one;
        areaRect.offsetMin = new Vector2(14f, 8f);
        areaRect.offsetMax = new Vector2(-14f, -8f);
        textArea.AddComponent<RectMask2D>();

        TextMeshProUGUI placeholder = CreateInputText(
            textArea.transform, "Placeholder", "Ask Lisa anything...");
        placeholder.fontStyle = FontStyles.Italic;
        placeholder.color = new Color(0.4f, 0.35f, 0.3f, 0.65f);

        TextMeshProUGUI inputText = CreateInputText(
            textArea.transform, "Text", "");
        inputText.color = new Color32(32, 24, 18, 255);

        TMP_InputField field = inputRoot.AddComponent<TMP_InputField>();
        field.textViewport = areaRect;
        field.textComponent = inputText;
        field.placeholder = placeholder;
        field.fontAsset = inputText.font;
        field.pointSize = 22f;
        field.lineType = TMP_InputField.LineType.SingleLine;
        field.customCaretColor = true;
        field.caretColor = new Color32(32, 24, 18, 255);
        field.onSubmit.AddListener(HandleInputSubmit);
        return field;
    }

    private static TextMeshProUGUI CreateInputText(
        Transform parent,
        string name,
        string text)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        TextMeshProUGUI tmp = go.AddComponent<TextMeshProUGUI>();
        if (!string.IsNullOrEmpty(text))
            MorphicLoc.Bind(tmp, text);
        else
            tmp.text = text;
        tmp.fontSize = 22f;
        tmp.alignment = TextAlignmentOptions.MidlineLeft;
        tmp.raycastTarget = false;
        StretchFull(tmp.rectTransform);
        return tmp;
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
        MorphicLoc.Bind(tmp, text);
        tmp.fontSize = MorphicText.Size(size);
        tmp.alignment = align;
        tmp.raycastTarget = false;
        MorphicText.FitButton(tmp);
        RectTransform rect = tmp.rectTransform;
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        return tmp;
    }

    private void OpenAskCard()
    {
        EnsureUi();
        if (askCard == null)
            return;

        if (askDimmer != null)
            askDimmer.SetActive(false);
        askCard.SetActive(true);
        askCard.transform.SetAsLastSibling();
        if (helpText != null)
        {
            helpText.text = "";
            helpText.gameObject.SetActive(false);
        }

        UpdateAskCardHeight();
        WarmBackend();
        if (questionInput != null)
        {
            questionInput.text = "";
            questionInput.ActivateInputField();
            if (EventSystem.current != null)
                EventSystem.current.SetSelectedGameObject(questionInput.gameObject);
        }
    }

    private void HideAskCard()
    {
        if (askCard != null)
            askCard.SetActive(false);
        if (askDimmer != null)
            askDimmer.SetActive(false);
        if (questionInput != null)
            questionInput.text = "";
    }

    private void CloseAskDialog()
    {
        MorphicLisaVoice.Stop();
        HideAskCard();
        if (EventSystem.current != null)
            EventSystem.current.SetSelectedGameObject(null);
        if (openedFromStudio && root != null)
            root.SetActive(false);
    }

    private void HandleInputSubmit(string _)
    {
        SendQuestion();
    }

    private void SendQuestion()
    {
        string question = questionInput != null ? questionInput.text.Trim() : "";
        if (string.IsNullOrEmpty(question))
        {
            ShowMessage("LISA: Type a question first.");
            return;
        }

        MorphicCraftHelpState state = BuildHelpState();
        if (questionInput != null)
        {
            questionInput.SetTextWithoutNotify("");
            questionInput.ActivateInputField();
            if (EventSystem.current != null)
                EventSystem.current.SetSelectedGameObject(questionInput.gameObject);
        }

        ShowMessage("LISA: Asking...");
        state.userQuestion = MorphicLoc.WithAiLanguage(question);
        EnsureAi();

        if (trigger == null || !trigger.RequestSculptHelp(state))
            ShowMessage("LISA: Help is waking up. Send again.");
    }

    private MorphicCraftHelpState BuildHelpState()
    {
        MorphicCraftHelpState state = new MorphicCraftHelpState();

        SculptingEnhancementTeapotColorController teapotColors =
            FindFirstObjectByType<SculptingEnhancementTeapotColorController>(
                FindObjectsInactive.Include);
        TeapotGestureController teapotGestures =
            FindFirstObjectByType<TeapotGestureController>(FindObjectsInactive.Include);
        if (IsLive(teapotColors) || IsLive(teapotGestures))
        {
            if (teapotGestures != null)
                teapotGestures.WriteHelpWeights(state);
            if (teapotColors != null)
                teapotColors.WriteHelpDecor(state);
            else
                state.currentTool = "teapot";
            return state;
        }

        SculptingEnhancementKerisColorController kerisColors =
            FindFirstObjectByType<SculptingEnhancementKerisColorController>(
                FindObjectsInactive.Include);
        KerisGestureController kerisGestures =
            FindFirstObjectByType<KerisGestureController>(FindObjectsInactive.Include);
        if (IsLive(kerisColors) || IsLive(kerisGestures))
        {
            if (kerisGestures != null)
                kerisGestures.WriteHelpWeights(state);
            if (kerisColors != null)
                kerisColors.WriteHelpDecor(state);
            else
                state.currentTool = "keris";
            return state;
        }

        TaiwanLanternGestureController lanternGestures =
            FindFirstObjectByType<TaiwanLanternGestureController>(FindObjectsInactive.Include);
        SculptingEnhancementLanternColorController lanternColors =
            FindFirstObjectByType<SculptingEnhancementLanternColorController>(
                FindObjectsInactive.Include);
        if (lanternGestures != null)
            lanternGestures.WriteHelpWeights(state);
        if (lanternColors != null)
            lanternColors.WriteHelpDecor(state);
        else
            state.currentTool = "lantern";

        return state;
    }

    private static bool IsLive(Behaviour behaviour)
    {
        return behaviour != null &&
            behaviour.isActiveAndEnabled &&
            behaviour.gameObject.activeInHierarchy;
    }

    private void HandleAiMessage(string message)
    {
        if (string.IsNullOrWhiteSpace(message))
            return;
        ShowMessage(message.StartsWith("LISA") ? message : "LISA: " + message);
    }

    private void HandleAiFailed()
    {
        ShowMessage("LISA: Help is waking up. Send again.");
    }

    private void ShowMessage(string message)
    {
        if (askCard != null)
            askCard.SetActive(true);
        if (helpText != null)
        {
            bool hasText = !string.IsNullOrWhiteSpace(message);
            helpText.gameObject.SetActive(hasText);
            MorphicLoc.ApplyFont(helpText);
            helpText.text = MorphicLoc.LocalizeAiReply(message);
            UpdateAskCardHeight();
            MorphicLisaVoice.SpeakReply(helpText.text);
        }

        MorphicAIFeedbackUI feedback =
            FindFirstObjectByType<MorphicAIFeedbackUI>(FindObjectsInactive.Include);
        if (feedback != null)
            feedback.ShowHint(message);
    }

    private void UpdateAskCardHeight()
    {
        if (askCard == null)
            return;

        RectTransform boxRect = askCard.GetComponent<RectTransform>();
        if (boxRect == null)
            return;

        float inputRow = 84f;
        float answerHeight = 0f;
        if (helpText != null && helpText.gameObject.activeSelf &&
            !string.IsNullOrWhiteSpace(helpText.text))
        {
            helpText.ForceMeshUpdate();
            answerHeight = Mathf.Clamp(helpText.preferredHeight + 20f, 36f, 200f);
            RectTransform textRect = helpText.rectTransform;
            textRect.offsetMin = new Vector2(16f, inputRow);
            textRect.offsetMax = new Vector2(-16f, -12f);
        }

        boxRect.sizeDelta = new Vector2(760f, inputRow + answerHeight);
    }
}
