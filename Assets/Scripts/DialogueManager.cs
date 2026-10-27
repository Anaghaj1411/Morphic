
using System.Collections;
using Mediapipe.Unity.Sample;
using Mediapipe.Unity.Sample.HandLandmarkDetection;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

public class DialogueManager : MonoBehaviour
{
    [Header("Dialogue UI")]
    [SerializeField] private GameObject dialoguePanel;
    [SerializeField] private TMP_Text speakerName;
    [SerializeField] private TMP_Text dialogueText;

    [Header("Camera Transition")]
    [Tooltip("MainMenuStartTransition used to enter the Pottery Studio.")]
    [SerializeField] private MainMenuStartTransition cameraTransition;

    [Header("Choice 1")]
    [SerializeField] private Button choice1Button;
    [SerializeField] private TMP_Text choice1Text;

    [Header("Choice 2")]
    [SerializeField] private Button choice2Button;
    [SerializeField] private TMP_Text choice2Text;

    [Header("Choice 3")]
    [SerializeField] private Button choice3Button;
    [SerializeField] private TMP_Text choice3Text;

    [Header("Choice 4")]
    [SerializeField] private Button choice4Button;
    [SerializeField] private TMP_Text choice4Text;

    [Header("Culture Choices")]
    [SerializeField] private GameObject cultureChoicesPanel;
    [SerializeField] private Button taiwanButton;
    [SerializeField] private Button indiaButton;
    [SerializeField] private Button indonesiaButton;
    [SerializeField] private Button freestyleButton;

    [Header("Restaurant Choices")]
    [SerializeField] private GameObject restaurantChoicesPanel;
    [SerializeField] private Button taiwanFoodButton;
    [SerializeField] private Button indoAsiaFoodButton;
    [SerializeField] private Button indiaFoodButton;

    [Header("Timing")]
    [SerializeField] private float startDelay = 2.1f;

    [Header("Taiwan Lantern")]
    [SerializeField] private GameObject taiwanLantern;
    [SerializeField] private Transform taiwanLanternSpawnPoint;

    [Header("India Teapot")]
    [Tooltip("Teapot_Updated scene instance to show when India sculpting starts.")]
    [SerializeField] private GameObject indiaTeapot;

    [Header("Taiwan Hand Tracking")]
    [SerializeField] private GameObject taiwanHandTracking;
    [SerializeField] private GameObject taiwanGestureController;
    [SerializeField] private GameObject taiwanGestureIndicator;

    [Header("Hand Tracking Solution")]
    [Tooltip("The Solution GameObject that has the hand tracking properly configured")]
    [SerializeField] private GameObject handTrackingSolution;

    [Header("Bootstrap")]
    [Tooltip("The Bootstrap GameObject for MediaPipe initialization")]
    [SerializeField] private GameObject bootstrapGameObject;

    [Header("Webcam Display")]
    [SerializeField] private GameObject webcamDisplayController;
    [SerializeField] private GameObject handAnnotationController;

    private Coroutine dialogueCoroutine;
    private bool dialogueStarted;
    private SculptingEnhancementLanternColorController lanternColorController;
    private SculptingEnhancementTeapotColorController teapotColorController;
    private MorphicAIHelpUI aiHelpUi;
    private GameObject sculptBackRoot;

    private void Awake()
    {
        FindMissingReferences();
        StyleDialogueUi();
        StyleCultureChoices();

        // The Teapot gesture label is a scene UI element, so hide it on the
        // main menu even if it is not parented under the inactive Teapot.
        GameObject teapotGestureStatus = FindSceneObject("Teapot Gesture Status");
        if (teapotGestureStatus != null)
            teapotGestureStatus.SetActive(false);

        lanternColorController =
            FindFirstObjectByType<SculptingEnhancementLanternColorController>(
                FindObjectsInactive.Include);
        if (lanternColorController != null)
            lanternColorController.SetPaletteAvailable(false);

        aiHelpUi = FindFirstObjectByType<MorphicAIHelpUI>(FindObjectsInactive.Include);
        if (aiHelpUi == null)
            aiHelpUi = gameObject.AddComponent<MorphicAIHelpUI>();
        aiHelpUi.SetVisible(false);

        // The teapot is placed in the scene like the lantern, but stays hidden
        // until the user chooses India and presses Start Sculpting.
        if (indiaTeapot == null)
        {
            indiaTeapot = FindSceneObject("Teapot_Updated");
            // Keep compatibility with older scene copies that still use the
            // temporary name from the initial Teapot setup.
            if (indiaTeapot == null)
                indiaTeapot = FindSceneObject("Teapot_test");
        }

        if (indiaTeapot != null)
            indiaTeapot.SetActive(false);

        AddHoverEffect(taiwanButton);
        AddHoverEffect(indiaButton);
        AddHoverEffect(indonesiaButton);
        AddHoverEffect(freestyleButton);

        HideAllChoices();
        HideCultureChoices();

        // Ensure webcam display is deactivated initially
        if (webcamDisplayController != null)
        {
            webcamDisplayController.SetActive(false);
            WebcamDisplayController displayController = webcamDisplayController.GetComponent<WebcamDisplayController>();
            if (displayController != null)
            {
                displayController.Deactivate();
            }
        }

        // Ensure hand annotation is deactivated initially
        if (handAnnotationController != null)
        {
            handAnnotationController.SetActive(false);
        }

        // Ensure hand tracking solution is deactivated initially
        if (handTrackingSolution != null)
        {
            handTrackingSolution.SetActive(false);
        }

        // Ensure bootstrap is deactivated initially
        if (bootstrapGameObject != null)
        {
            bootstrapGameObject.SetActive(false);
        }
    }

    private void AddHoverEffect(Button btn)
    {
        if (btn != null &&
            btn.gameObject.GetComponent<ButtonHoverEffect>() == null)
        {
            btn.gameObject.AddComponent<ButtonHoverEffect>();
        }
    }

    private void OnEnable()
    {
        if (dialogueStarted)
            return;

        if (dialogueCoroutine != null)
            StopCoroutine(dialogueCoroutine);

        dialogueCoroutine = StartCoroutine(StartDialogueAfterDelay());
    }

    private void FindMissingReferences()
    {
        if (cameraTransition == null)
        {
            cameraTransition =
                FindObjectOfType<MainMenuStartTransition>();
        }
    }

    private IEnumerator StartDialogueAfterDelay()
    {
        yield return new WaitForSeconds(startDelay);
        BeginDialogue();
    }

    public void BeginDialogue()
    {
        if (dialogueStarted)
            return;

        dialogueStarted = true;

        if (dialoguePanel != null)
            dialoguePanel.SetActive(true);

        ShowNode(0);
    }

    private void ShowNode(int node)
    {
        HideAllChoices();
        HideCultureChoices();

        switch (node)
        {
            case 0:
                SetDialogue(
                    "LISA",
                    "Hi! I'm Lisa. Nice to meet you!"
                );

                SetChoice(
                    choice1Button, choice1Text,
                    "Hi, Lisa!", () => ShowNode(1)
                );

                SetChoice(
                    choice2Button, choice2Text,
                    "Where are we?", () => ShowNode(2)
                );
                break;

            case 1:
                SetDialogue(
                    "LISA",
                    "It's wonderful to finally be back in my beloved city of Yuanlin."
                );

                SetChoice(
                    choice1Button, choice1Text,
                    "About this place", () => ShowNode(3)
                );

                SetChoice(
                    choice2Button, choice2Text,
                    "Why this shop?", () => ShowNode(4)
                );
                break;

            case 2:
                SetDialogue(
                    "LISA",
                    "We're in Yuanlin, my hometown. And this little pottery shop is my new home for creating art."
                );

                SetChoice(
                    choice1Button, choice1Text,
                    "Why pottery?", () => ShowNode(4)
                );

                SetChoice(
                    choice2Button, choice2Text,
                    "Your journey", () => ShowNode(5)
                );
                break;

            case 3:
                SetDialogue(
                    "LISA",
                    "Yuanlin is full of life, food, people and little places worth discovering. I wanted my shop to become one of those places."
                );

                SetChoice(
                    choice1Button, choice1Text,
                    "That's lovely", () => ShowNode(6)
                );

                SetChoice(
                    choice2Button, choice2Text,
                    "So why pottery?", () => ShowNode(4)
                );
                break;

            case 4:
                SetDialogue(
                    "LISA",
                    "Because clay lets you turn an idea into something you can actually touch. And during my studies, I discovered how differently cultures approach the same material."
                );

                SetChoice(
                    choice1Button, choice1Text,
                    "What did you learn?", () => ShowNode(5)
                );

                SetChoice(
                    choice2Button, choice2Text,
                    "Let's make something!", () => ShowNode(6)
                );
                break;

            case 5:
                SetDialogue(
                    "LISA",
                    "I studied art in India and Indonesia. Their sculpting traditions inspired me to create a place where different cultures could meet through clay."
                );

                SetChoice(
                    choice1Button, choice1Text,
                    "I want to see!", () => ShowNode(6)
                );

                SetChoice(
                    choice2Button, choice2Text,
                    "Amazing!", () => ShowNode(6)
                );
                break;

            case 6:
                SetDialogue(
                    "LISA",
                    "Exactly! I want this place to be more than a shop. I want people to experiment, learn and create something of their own."
                );

                SetChoice(
                    choice1Button, choice1Text,
                    "Let's start!", () => ShowNode(7)
                );
                break;

            case 7:
                SetDialogue(
                    "LISA",
                    "Welcome to my little corner of Yuanlin! " +
                    "This is where I bring together art, food, and traditions " +
                    "from different cultures. Where would you like to explore first?"
                );

                SetChoice(
                    choice1Button, choice1Text,
                    "Pottery Studio", EnterPotteryStudio
                );

                SetChoice(
                    choice2Button, choice2Text,
                    "Restaurant", EnterRestaurant
                );

                SetChoice(
                    choice3Button, choice3Text,
                    "Ask Lisa", () => ShowNode(9)
                );
                break;

            case 8:
                SetDialogue(
                    "LISA",
                    "Welcome to the restaurant! Here, we explore " +
                    "the food and flavours of Taiwan. " +
                    "Take a look at the ingredients on the table. " +
                    "What would you like to do?"
                );

                SetChoice(
                    choice1Button, choice1Text,
                    "Back to shop",
                    ReturnToLocationChoices
                );
                break;

            case 9:
                SetDialogue(
                    "LISA",
                    "There is a lot to discover here. Why don't you start by exploring the pottery studio?"
                );

                SetChoice(
                    choice1Button, choice1Text,
                    "Pottery Studio", EnterPotteryStudio
                );

                SetChoice(
                    choice2Button, choice2Text,
                    "Go back.", () => ShowNode(7)
                );
                break;

            case 10:
                SetDialogue(
                    "LISA",
                    "Welcome to my pottery studio! " +
                    "Here, you can explore different artistic traditions through clay. " +
                    "Which creation would you like to try?"
                );

                ShowCultureChoices();
                break;
        }
    }

    private void SetDialogue(string speaker, string message)
    {
        if (speakerName != null)
            speakerName.text = speaker;

        if (dialogueText != null)
            dialogueText.text = message;
    }

    private void StyleDialogueUi()
    {
        if (dialoguePanel == null)
            return;

        RectTransform panelRect = dialoguePanel.transform as RectTransform;
        RectTransform canvasRect = dialoguePanel.transform.parent as RectTransform;
        if (panelRect != null)
        {
            float availableWidth = canvasRect != null && canvasRect.rect.width > 0f
                ? canvasRect.rect.width - 80f
                : 1320f;

            panelRect.anchorMin = new Vector2(0.5f, 0f);
            panelRect.anchorMax = new Vector2(0.5f, 0f);
            panelRect.pivot = new Vector2(0.5f, 0f);
            panelRect.anchoredPosition = new Vector2(0f, 24f);
            panelRect.sizeDelta = new Vector2(
                Mathf.Min(1180f, Mathf.Max(640f, availableWidth)),
                340f);
        }

        Image panelImage = dialoguePanel.GetComponent<Image>();
        if (panelImage != null)
        {
            LanguagePageBuilder.ApplyRoundedSprite(panelImage, 32);
            panelImage.color = new Color32(255, 239, 214, 255);
        }

        Outline panelOutline = dialoguePanel.GetComponent<Outline>();
        if (panelOutline != null)
        {
            panelOutline.effectColor = new Color32(42, 83, 49, 235);
            panelOutline.effectDistance = new Vector2(2f, -3f);
        }

        Shadow panelShadow = dialoguePanel.GetComponent<Shadow>();
        if (panelShadow == null)
            panelShadow = dialoguePanel.AddComponent<Shadow>();
        panelShadow.effectColor = new Color(0.08f, 0.12f, 0.08f, 0.24f);
        panelShadow.effectDistance = new Vector2(0f, -7f);
        panelShadow.useGraphicAlpha = true;

        StyleDialogueText(speakerName, 23f, 18f, 26f, false);
        StyleDialogueText(dialogueText, 21f, 17f, 24f, true);

        if (speakerName != null)
        {
            speakerName.color = new Color32(42, 83, 49, 255);
            speakerName.fontStyle = FontStyles.Bold;
            speakerName.alignment = TextAlignmentOptions.MidlineLeft;
            SetTextRect(speakerName.rectTransform,
                new Vector2(36f, -20f), new Vector2(280f, 40f),
                new Vector2(0f, 1f), new Vector2(0f, 1f),
                new Vector2(0f, 1f));
        }

        if (dialogueText != null)
        {
            dialogueText.color = new Color32(48, 48, 39, 255);
            dialogueText.alignment = TextAlignmentOptions.TopLeft;
            SetTextRect(dialogueText.rectTransform,
                Vector2.zero, Vector2.zero,
                Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f));
            dialogueText.rectTransform.offsetMin = new Vector2(36f, 108f);
            dialogueText.rectTransform.offsetMax = new Vector2(-36f, -72f);
        }

        Button[] choices = { choice1Button, choice2Button, choice3Button, choice4Button };
        for (int i = 0; i < choices.Length; i++)
            StyleChoiceButton(choices[i]);

        if (choice1Button != null)
        {
            Transform row = choice1Button.transform.parent;
            RectTransform rowRect = row as RectTransform;
            if (rowRect != null)
            {
                rowRect.anchorMin = new Vector2(0f, 0f);
                rowRect.anchorMax = new Vector2(1f, 0f);
                rowRect.pivot = new Vector2(0.5f, 0f);
                rowRect.anchoredPosition = new Vector2(0f, 16f);
                rowRect.sizeDelta = new Vector2(-40f, 84f);
            }

            HorizontalLayoutGroup rowLayout = row.GetComponent<HorizontalLayoutGroup>();
            if (rowLayout != null)
            {
                rowLayout.padding = new RectOffset(12, 12, 6, 6);
                rowLayout.spacing = 18f;
                rowLayout.childAlignment = TextAnchor.MiddleCenter;
                rowLayout.childForceExpandWidth = false;
                rowLayout.childForceExpandHeight = false;
                rowLayout.childControlWidth = true;
                rowLayout.childControlHeight = true;
            }
        }
    }

    private void ApplyChoiceRowSize(bool compact)
    {
        if (choice1Button == null)
            return;

        Transform row = choice1Button.transform.parent;
        RectTransform rowRect = row as RectTransform;
        if (rowRect != null)
        {
            rowRect.sizeDelta = new Vector2(-40f, compact ? 56f : 84f);
            rowRect.anchoredPosition = new Vector2(0f, compact ? 12f : 16f);
        }

        if (dialogueText != null)
            dialogueText.rectTransform.offsetMin = new Vector2(36f, compact ? 68f : 108f);
    }

    private static void StyleChoiceButton(Button button, bool compact = false)
    {
        if (button == null)
            return;

        Image frame = button.GetComponent<Image>();
        if (frame != null)
        {
            LanguagePageBuilder.ApplyRoundedSprite(frame, 24);
            frame.color = LanguagePageBuilder.Green;
            frame.raycastTarget = true;
        }

        Outline outline = button.GetComponent<Outline>();
        if (outline != null)
            outline.enabled = false;

        Transform fillTransform = button.transform.Find("MorphicFill");
        Image fill;
        if (fillTransform == null)
        {
            fill = LanguagePageBuilder.CreateRoundedImage(
                "MorphicFill", button.transform, 20, LanguagePageBuilder.Pink);
            LanguagePageBuilder.Stretch(fill.rectTransform, new Vector2(5f, 5f));
        }
        else
        {
            fill = fillTransform.GetComponent<Image>();
            if (fill != null)
            {
                LanguagePageBuilder.ApplyRoundedSprite(fill, 20);
                fill.color = LanguagePageBuilder.Pink;
                LanguagePageBuilder.Stretch(fill.rectTransform, new Vector2(5f, 5f));
            }
        }

        if (fill != null)
        {
            fill.raycastTarget = true;
            button.targetGraphic = fill;
        }

        LayoutElement layout = button.GetComponent<LayoutElement>();
        if (layout == null)
            layout = button.gameObject.AddComponent<LayoutElement>();
        layout.minWidth = compact ? 176f : 260f;
        layout.preferredWidth = compact ? 210f : 320f;
        layout.flexibleWidth = 0f;
        layout.minHeight = compact ? 44f : 70f;
        layout.preferredHeight = compact ? 46f : 72f;
        layout.flexibleHeight = 0f;

        TMP_Text label = button.GetComponentInChildren<TMP_Text>(true);
        if (label == null)
            return;

        if (fill != null)
            label.transform.SetParent(fill.transform, false);
        label.gameObject.SetActive(true);
        label.enabled = true;
        label.enableAutoSizing = false;
        label.fontSize = compact ? 20f : 26f;
        label.fontStyle = FontStyles.Bold;
        label.characterSpacing = 1.5f;
        label.color = LanguagePageBuilder.Green;
        label.alignment = TextAlignmentOptions.Center;
        label.enableWordWrapping = false;
        label.overflowMode = TextOverflowModes.Overflow;
        label.raycastTarget = false;
        RectTransform labelRect = label.rectTransform;
        labelRect.anchorMin = Vector2.zero;
        labelRect.anchorMax = Vector2.one;
        labelRect.pivot = new Vector2(0.5f, 0.5f);
        labelRect.offsetMin = new Vector2(12f, 6f);
        labelRect.offsetMax = new Vector2(-12f, -6f);

        Outline textOutline = label.GetComponent<Outline>();
        if (textOutline == null)
            textOutline = label.gameObject.AddComponent<Outline>();
        textOutline.effectColor = new Color32(255, 255, 255, 160);
        textOutline.effectDistance = new Vector2(1.2f, -1.2f);
        textOutline.useGraphicAlpha = true;
        label.ForceMeshUpdate();
    }

    private void StyleCultureChoices()
    {
        if (cultureChoicesPanel == null)
            return;

        RectTransform panelRect = cultureChoicesPanel.transform as RectTransform;
        if (panelRect != null)
        {
            panelRect.anchorMin = panelRect.anchorMax = new Vector2(0.5f, 0.5f);
            panelRect.pivot = new Vector2(0.5f, 0.5f);
            panelRect.anchoredPosition = new Vector2(0f, 36f);
            panelRect.sizeDelta = new Vector2(1480f, 420f);
        }

        Image background = cultureChoicesPanel.GetComponent<Image>();
        if (background != null)
            background.enabled = false;

        if (cultureChoicesPanel.transform.Find("MorphicFrame") == null)
        {
            Image frame = LanguagePageBuilder.CreateRoundedImage(
                "MorphicFrame", cultureChoicesPanel.transform, 36, LanguagePageBuilder.Green);
            frame.transform.SetAsFirstSibling();
            LanguagePageBuilder.Stretch(frame.rectTransform);
            frame.raycastTarget = false;
            IgnoreLayout(frame.gameObject);

            Image fill = LanguagePageBuilder.CreateRoundedImage(
                "Fill", frame.transform, 32, new Color32(255, 239, 214, 245));
            LanguagePageBuilder.Stretch(fill.rectTransform, new Vector2(10f, 10f));
            fill.raycastTarget = false;
        }

        if (cultureChoicesPanel.transform.Find("Title") == null)
        {
            Image title = LanguagePageBuilder.CreateRoundedImage(
                "Title", cultureChoicesPanel.transform, 22, LanguagePageBuilder.Green);
            IgnoreLayout(title.gameObject);
            RectTransform titleRect = title.rectTransform;
            titleRect.anchorMin = titleRect.anchorMax = new Vector2(0.5f, 1f);
            titleRect.pivot = new Vector2(0.5f, 0.5f);
            titleRect.anchoredPosition = new Vector2(0f, 18f);
            titleRect.sizeDelta = new Vector2(420f, 56f);
            title.raycastTarget = false;

            Image titleFill = LanguagePageBuilder.CreateRoundedImage(
                "Fill", title.transform, 18, LanguagePageBuilder.Pink);
            LanguagePageBuilder.Stretch(titleFill.rectTransform, new Vector2(5f, 5f));
            titleFill.raycastTarget = false;

            GameObject labelObject = new GameObject("Label", typeof(RectTransform));
            labelObject.transform.SetParent(titleFill.transform, false);
            TextMeshProUGUI label = labelObject.AddComponent<TextMeshProUGUI>();
            label.text = "CHOOSE A CRAFT";
            label.fontSize = 24f;
            label.fontStyle = FontStyles.Bold;
            label.alignment = TextAlignmentOptions.Center;
            label.color = LanguagePageBuilder.Green;
            label.raycastTarget = false;
            LanguagePageBuilder.Stretch(label.rectTransform);
        }

        HorizontalLayoutGroup layout = cultureChoicesPanel.GetComponent<HorizontalLayoutGroup>();
        if (layout != null)
        {
            layout.padding = new RectOffset(40, 40, 52, 32);
            layout.spacing = 28f;
            layout.childAlignment = TextAnchor.MiddleCenter;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = false;
            layout.childControlWidth = false;
            layout.childControlHeight = false;
        }

        Button[] crafts = { taiwanButton, indiaButton, indonesiaButton, freestyleButton };
        for (int i = 0; i < crafts.Length; i++)
        {
            Button button = crafts[i];
            if (button == null)
                continue;

            RectTransform rect = button.transform as RectTransform;
            if (rect != null)
                rect.sizeDelta = new Vector2(300f, 300f);

            Outline outline = button.GetComponent<Outline>();
            if (outline == null)
                outline = button.gameObject.AddComponent<Outline>();
            outline.effectColor = LanguagePageBuilder.Green;
            outline.effectDistance = new Vector2(4f, -4f);
        }
    }

    private static void StyleDialogueText(
        TMP_Text text,
        float size,
        float minimumSize,
        float maximumSize,
        bool wrap)
    {
        if (text == null)
            return;

        text.enableAutoSizing = true;
        text.fontSize = size;
        text.fontSizeMin = minimumSize;
        text.fontSizeMax = maximumSize;
        text.enableWordWrapping = wrap;
        text.overflowMode = TextOverflowModes.Overflow;
        text.raycastTarget = false;
    }

    private static void SetTextRect(
        RectTransform rect,
        Vector2 position,
        Vector2 size,
        Vector2 anchorMin,
        Vector2 anchorMax,
        Vector2 pivot)
    {
        if (rect == null)
            return;

        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.pivot = pivot;
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
    }

    private static void IgnoreLayout(GameObject target)
    {
        if (target == null)
            return;
        LayoutElement element = target.GetComponent<LayoutElement>();
        if (element == null)
            element = target.AddComponent<LayoutElement>();
        element.ignoreLayout = true;
    }

    private void SetChoice(
        Button button,
        TMP_Text text,
        string label,
        UnityAction action)
    {
        if (button == null)
            return;

        button.gameObject.SetActive(true);

        TMP_Text labelText = text != null
            ? text
            : button.GetComponentInChildren<TMP_Text>(true);
        if (labelText != null)
            labelText.text = label;
        bool compact = label == "Start Sculpting";
        StyleChoiceButton(button, compact);
        if (button == choice1Button)
            ApplyChoiceRowSize(compact);

        button.onClick.RemoveAllListeners();

        DialogueButtonClickSound clickSound = dialoguePanel != null
            ? dialoguePanel.GetComponent<DialogueButtonClickSound>()
            : null;
        if (clickSound != null)
            clickSound.RegisterButton(button);

        if (action != null)
            button.onClick.AddListener(action);
    }

    private void ShowCultureChoices()
    {
        HideAllChoices();

        if (cultureChoicesPanel == null)
        {
            Debug.LogWarning(
                "MORPHIC: Culture Choices Panel is not assigned in DialogueManager."
            );
            return;
        }

        cultureChoicesPanel.SetActive(true);

        ConfigureCultureButton(taiwanButton, "Taiwan");
        ConfigureCultureButton(indiaButton, "India");
        ConfigureCultureButton(indonesiaButton, "Indonesia");
        ConfigureCultureButton(freestyleButton, "Freestyle");
    }

    private void ConfigureCultureButton(
        Button button,
        string culture)
    {
        if (button == null)
            return;

        button.gameObject.SetActive(true);
        button.onClick.RemoveAllListeners();
        button.onClick.AddListener(() => SelectCulture(culture));
    }

    private void ShowRestaurantChoices()
    {
        HideAllChoices();
        HideCultureChoices();

        if (dialoguePanel != null)
            dialoguePanel.SetActive(false);

        if (restaurantChoicesPanel != null)
            restaurantChoicesPanel.SetActive(true);

        ConfigureRestaurantButton(taiwanFoodButton, "Taiwan");
        ConfigureRestaurantButton(indoAsiaFoodButton, "IndoAsia");
        ConfigureRestaurantButton(indiaFoodButton, "India");
    }

    private void ConfigureRestaurantButton(
        Button button,
        string cuisine)
    {
        if (button == null)
            return;

        button.onClick.RemoveAllListeners();
        button.onClick.AddListener(
            () => SelectRestaurantCuisine(cuisine)
        );
    }

    private void SelectRestaurantCuisine(string cuisine)
    {
        Debug.Log("MORPHIC: Selected restaurant cuisine: " + cuisine);
    }

    private void SelectCulture(string culture)
    {
        Debug.Log("MORPHIC: Culture selected = " + culture);

        HideCultureChoices();
        StartSculpting(culture);
    }

    private void EnterPotteryStudio()
    {
        HideAllChoices();
        HideCultureChoices();

        if (cameraTransition == null)
        {
            cameraTransition =
                FindObjectOfType<MainMenuStartTransition>();
        }

        if (cameraTransition == null)
        {
            Debug.LogWarning(
                "MORPHIC: MainMenuStartTransition was not found."
            );
            return;
        }

        cameraTransition.PotteryStudioTransitionCompleted
            -= OnCameraFinished;

        cameraTransition.PotteryStudioTransitionCompleted
            += OnCameraFinished;

        cameraTransition.EnterPotteryStudio();
    }

    private void ReturnToLocationChoices()
    {
        HideAllChoices();
        HideCultureChoices();

        if (dialoguePanel != null)
            dialoguePanel.SetActive(false);

        if (cameraTransition == null)
        {
            cameraTransition =
                FindObjectOfType<MainMenuStartTransition>();
        }

        if (cameraTransition == null)
        {
            Debug.LogWarning(
                "MORPHIC: MainMenuStartTransition was not found."
            );
            return;
        }

        Debug.Log(
            "MORPHIC: Return-to-entrance camera transition is not implemented yet."
        );
    }

    private void EnterRestaurant()
    {
        HideAllChoices();
        HideCultureChoices();

        if (cameraTransition == null)
        {
            cameraTransition =
                FindObjectOfType<MainMenuStartTransition>();
        }

        if (cameraTransition == null)
        {
            Debug.LogWarning(
                "MORPHIC: MainMenuStartTransition was not found."
            );
            ShowNode(8);
            return;
        }

        cameraTransition.RestaurantTransitionCompleted
            -= OnRestaurantCameraFinished;

        cameraTransition.RestaurantTransitionCompleted
            += OnRestaurantCameraFinished;

        cameraTransition.EnterRestaurant();
    }

    private void OnRestaurantCameraFinished()
    {
        if (cameraTransition != null)
        {
            cameraTransition.RestaurantTransitionCompleted
                -= OnRestaurantCameraFinished;
        }

        HideAllChoices();
        HideCultureChoices();

        if (dialoguePanel != null)
        {
            Transform parent = dialoguePanel.transform.parent;

            if (parent != null)
                parent.gameObject.SetActive(true);

            dialoguePanel.SetActive(true);
        }

        ShowRestaurantChoices();
    }

    private void OnCameraFinished()
    {
        Debug.Log(
            "MORPHIC: Pottery camera finished. Enabling dialogue UI."
        );

        if (cameraTransition != null)
        {
            cameraTransition.PotteryStudioTransitionCompleted
                -= OnCameraFinished;
        }

        if (dialoguePanel != null)
        {
            Transform parent = dialoguePanel.transform.parent;

            if (parent != null)
                parent.gameObject.SetActive(true);

            dialoguePanel.SetActive(true);
        }

        ShowNode(10);
    }

    private void StartSculpting(string culture)
    {
        HideAllChoices();
        HideCultureChoices();

        switch (culture)
        {
            case "Taiwan":
                SetDialogue(
                    "LISA",
                    "Let's begin with Taiwan. " +
                    "This piece is inspired by the traditional sky lanterns " +
                    "that have become such a beautiful part of Taiwanese culture. " +
                    "But this isn't a finished lantern. I've given you the basic form. " +
                    "You decide what it becomes."
                );

                SetChoice(
                    choice1Button,
                    choice1Text,
                    "Start Sculpting",
                    () => BeginSculpting("Taiwan")
                );
                break;

            case "India":
                SetDialogue(
                    "LISA",
                    "India has a long and diverse history of pottery and ceramic art. " +
                    "I wanted to capture some of that inspiration in this piece. " +
                    "Here's the basic form. Shape it however you like."
                );

                SetChoice(
                    choice1Button,
                    choice1Text,
                    "Start Sculpting",
                    () => BeginSculpting("India")
                );
                break;

            case "Indonesia":
                SetDialogue(
                    "LISA",
                    "Indonesia was another important part of my art studies. " +
                    "Its traditional objects and sculptural forms inspired me " +
                    "in a completely different way. " +
                    "Let's see what you can create from this base."
                );

                SetChoice(
                    choice1Button,
                    choice1Text,
                    "Start Sculpting",
                    () => BeginSculpting("Indonesia")
                );
                break;

            case "Freestyle":
                SetDialogue(
                    "LISA",
                    "Don't want to follow a particular tradition? " +
                    "That's completely fine. " +
                    "This time, the clay is yours."
                );

                SetChoice(
                    choice1Button,
                    choice1Text,
                    "Create Freely",
                    () => BeginSculpting("Freestyle")
                );
                break;
        }
    }

    private void BeginSculpting(string culture)
    {
        HideAllChoices();
        HideCultureChoices();

        if (lanternColorController == null)
        {
            lanternColorController =
                FindFirstObjectByType<SculptingEnhancementLanternColorController>(
                    FindObjectsInactive.Include);
        }

        if (lanternColorController != null && culture != "Taiwan" && culture != "India")
            lanternColorController.SetPaletteAvailable(false);

        if (dialoguePanel != null)
            dialoguePanel.SetActive(false);

        Debug.Log("MORPHIC: Starting sculpting for " + culture);

        switch (culture)
        {
            case "Taiwan":
                Debug.Log("MORPHIC: Taiwan selected - Lantern");

                if (taiwanLantern != null)
                {
                    // Preserve the manually configured Transform.
                    // The lantern uses BlendShapes, not ParametricSkyLantern.
                    taiwanLantern.SetActive(true);

                    Debug.Log("MORPHIC: Taiwan Lantern activated.");
                }
                else
                {
                    Debug.LogWarning(
                        "MORPHIC: Taiwan Lantern reference is missing."
                    );
                }

                SculptHandLandmarkerRunner handRunner = null;
                if (handTrackingSolution != null)
                {
                    handRunner = handTrackingSolution.GetComponent<SculptHandLandmarkerRunner>();
                }

                if (handRunner == null && taiwanHandTracking != null)
                {
                    handRunner = taiwanHandTracking.GetComponentInChildren<SculptHandLandmarkerRunner>(true);
                }

                // The legacy TaiwanHandTracking root contains an old runner
                // as well as the configured Solution child. Activate the root
                // for its screen hierarchy, but only run the configured child.
                if (taiwanHandTracking != null)
                    taiwanHandTracking.SetActive(true);

                // Activate the hand tracking solution (Solution GameObject)
                if (handTrackingSolution != null)
                {
                    handTrackingSolution.SetActive(true);

                    // BaseRunner.Start creates/awaits Bootstrap and starts
                    // MediaPipe only after its ResourceManager is ready.
                    if (handRunner != null)
                    {
                        Debug.Log("MORPHIC: Hand tracking runner activated; waiting for Bootstrap initialization.");
                    }
                }

                if (taiwanGestureController != null)
                    taiwanGestureController.SetActive(true);

                if (taiwanGestureIndicator != null)
                    taiwanGestureIndicator.SetActive(true);

                // TaiwanLanternGestureController drives the Taiwan lantern's
                // blendshapes directly. TaiwanLanternIndexTracker is for the
                // separate ParametricSkyLantern path and is not used here.

                // Activate gesture mode manager for gesture detection
                GestureModeManager gestureModeManager = FindObjectOfType<GestureModeManager>();
                if (gestureModeManager != null)
                {
                    gestureModeManager.gameObject.SetActive(true);
                    Debug.Log("MORPHIC: Gesture Mode Manager activated.");
                }

                // Activate webcam display with a bubbly border in the bottom-left
                if (webcamDisplayController != null)
                {
                    webcamDisplayController.SetActive(true);
                    WebcamDisplayController displayController = webcamDisplayController.GetComponent<WebcamDisplayController>();
                    if (displayController != null)
                    {
                        if (handRunner != null && handRunner.webcamScreen != null)
                        {
                            // Reuse the runner's complete Annotatable Screen
                            // prefab so its Screen, annotation controller, and
                            // landmark drawing hierarchy remain together.
                            displayController.UseScreen(handRunner.webcamScreen);
                        }
                        else
                        {
                            Debug.LogError("MORPHIC: Hand runner or its Annotatable Screen is missing.");
                        }

                        displayController.Activate();
                    }
                    Debug.Log("MORPHIC: Webcam display activated.");
                }
                else
                {
                    Debug.LogWarning("MORPHIC: Webcam display controller reference is missing.");
                }

                // Activate hand skeleton annotation
                if (handAnnotationController != null)
                {
                    handAnnotationController.SetActive(true);
                    Debug.Log("MORPHIC: Hand landmark annotation activated with the runner's Annotatable Screen.");
                }
                else
                {
                    Debug.LogWarning("MORPHIC: Hand annotation controller reference is missing.");
                }

                // Debug: Check if hand tracking is running
                if (handTrackingSolution != null)
                {
                    if (handRunner != null)
                    {
                        Debug.Log("MORPHIC: Hand tracking solution activated. Checking if tracking is running...");
                        // The tracking should start automatically when the GameObject is active
                    }
                }

                if (handRunner != null)
                {
                    StartCoroutine(CheckHandDetection(handRunner));
                }

                if (lanternColorController != null)
                {
                    lanternColorController.SetTutorialSculptLock(true);
                    lanternColorController.SetPaletteAvailable(false);
                }
                if (teapotColorController != null)
                    teapotColorController.SetPaletteAvailable(false);
                ShowAiHelp();
                SetLanternSculptDriving(false);
                StartLanternGestureTutorial();

                break;

            case "India":
                Debug.Log("MORPHIC: India selected - Teapot");

                // India and Taiwan share the configured hand-tracking source,
                // but only the selected craft's gesture controller should be
                // allowed to drive its own mesh.
                if (taiwanGestureController != null)
                    taiwanGestureController.SetActive(false);
                if (taiwanGestureIndicator != null)
                    taiwanGestureIndicator.SetActive(false);

                if (indiaTeapot != null)
                {
                    SculptHandLandmarkerRunner teapotHandRunner =
                        ResolveConfiguredHandRunner();

                    TeapotGestureController teapotGestures =
                        indiaTeapot.GetComponentInChildren<TeapotGestureController>(true);
                    if (teapotGestures != null && teapotHandRunner != null)
                        teapotGestures.SetHandRunner(teapotHandRunner);
                    else if (teapotHandRunner == null)
                        Debug.LogError("MORPHIC: India Teapot could not find the configured hand-tracking runner.");

                    // Solution is a child of the inactive TaiwanHandTracking
                    // root. Enabling Solution alone never runs Start/Play.
                    ActivateSharedHandTrackingRoot();

                    if (teapotHandRunner != null)
                    {
                        Debug.Log(
                            "MORPHIC: India Teapot using shared hand-tracking runner on '" +
                            teapotHandRunner.gameObject.name +
                            "'; waiting for Bootstrap initialization.");
                        teapotHandRunner.EnsureStartedForTeapot();
                    }

                    if (webcamDisplayController != null)
                    {
                        webcamDisplayController.SetActive(true);
                        WebcamDisplayController displayController =
                            webcamDisplayController.GetComponent<WebcamDisplayController>();
                        if (displayController != null)
                        {
                            if (teapotHandRunner != null && teapotHandRunner.webcamScreen != null)
                                displayController.UseScreen(teapotHandRunner.webcamScreen);
                            else
                                Debug.LogError("MORPHIC: Hand runner or its Annotatable Screen is missing.");

                            displayController.Activate();
                        }
                        Debug.Log("MORPHIC: Webcam display activated.");
                    }
                    else
                    {
                        Debug.LogWarning("MORPHIC: Webcam display controller reference is missing.");
                    }

                    if (handAnnotationController != null)
                    {
                        handAnnotationController.SetActive(true);
                        Debug.Log("MORPHIC: Hand landmark annotation activated with the runner's Annotatable Screen.");
                    }

                    // Keep the teapot's Inspector-configured position, rotation,
                    // and scale, just as the Taiwan lantern keeps its scene setup.
                    indiaTeapot.SetActive(true);
                    Debug.Log("MORPHIC: India Teapot activated.");

                    if (teapotHandRunner != null)
                        StartCoroutine(CheckHandDetection(teapotHandRunner));

                    if (lanternColorController != null)
                        lanternColorController.SetPaletteAvailable(false);

                    teapotColorController = indiaTeapot.GetComponentInChildren<SculptingEnhancementTeapotColorController>(true);
                    if (teapotColorController == null)
                        teapotColorController = indiaTeapot.AddComponent<SculptingEnhancementTeapotColorController>();
                    if (teapotGestures != null)
                        teapotColorController.BindFrom(teapotHandRunner, teapotGestures.TeapotRenderer, teapotGestures);
                    teapotColorController.SetTutorialSculptLock(true);
                    teapotColorController.SetPaletteAvailable(false);
                    if (teapotGestures != null)
                        teapotGestures.suppressDriving = true;
                    ShowAiHelp();
                    StartTeapotGestureTutorial();
                }
                else
                {
                    Debug.LogWarning(
                        "MORPHIC: India Teapot reference is missing. " +
                        "Add the Teapot_Updated scene object and assign it to " +
                        "DialogueManager > India Teapot."
                    );
                }
                break;

            case "Indonesia":
                Debug.Log("MORPHIC: Indonesia selected - Keris");
                break;

            case "Freestyle":
                Debug.Log("MORPHIC: Freestyle selected");
                break;
        }

        SetSculptBackVisible(true);
    }

    private IEnumerator LoadSculptingScene(string culture)
    {
        UnityEngine.SceneManagement.Scene sculptScene =
            UnityEngine.SceneManagement.SceneManager.GetSceneByName(
                "SculptingTest"
            );

        if (!sculptScene.isLoaded)
        {
            AsyncOperation asyncLoad =
                UnityEngine.SceneManagement.SceneManager.LoadSceneAsync(
                    "SculptingTest",
                    UnityEngine.SceneManagement.LoadSceneMode.Additive
                );

            if (asyncLoad == null)
            {
                Debug.LogError(
                    "MORPHIC: Failed to load SculptingTest scene. " +
                    "Please ensure it is added to File -> Build Profiles " +
                    "(or Build Settings) -> Scenes In Build."
                );
                yield break;
            }

            while (!asyncLoad.isDone)
                yield return null;
        }

        BaseShapeSelector shapeSelector =
            FindFirstObjectByType<BaseShapeSelector>(
                FindObjectsInactive.Include
            );

        if (shapeSelector != null)
        {
            shapeSelector.gameObject.SetActive(true);

            if (culture == "Taiwan")
            {
                // TaiwanLantern_Test is controlled by
                // TaiwanLanternGestureController.
            }
        }

        SculptUIController uiController =
            FindFirstObjectByType<SculptUIController>(
                FindObjectsInactive.Include
            );

        if (uiController != null)
        {
            uiController.gameObject.SetActive(true);
            uiController.enabled = true;
        }
    }

    private void HideCultureChoices()
    {
        if (cultureChoicesPanel != null)
            cultureChoicesPanel.SetActive(false);
    }

    private static GameObject FindSceneObject(string objectName)
    {
        // Find inactive scene instances too; exclude prefab assets returned by
        // Resources.FindObjectsOfTypeAll so this never toggles an asset itself.
        foreach (GameObject candidate in Resources.FindObjectsOfTypeAll<GameObject>())
        {
            if (candidate.name == objectName && candidate.scene.IsValid())
                return candidate;
        }

        return null;
    }

    private void HideAllChoices()
    {
        if (choice1Button != null)
            choice1Button.gameObject.SetActive(false);

        if (choice2Button != null)
            choice2Button.gameObject.SetActive(false);

        if (choice3Button != null)
            choice3Button.gameObject.SetActive(false);

        if (choice4Button != null)
            choice4Button.gameObject.SetActive(false);
    }

    /// <summary>
    /// Prefers the configured Solution runner and skips the disabled leftover
    /// runner on the TaiwanHandTracking root.
    /// </summary>
    private SculptHandLandmarkerRunner ResolveConfiguredHandRunner()
    {
        if (handTrackingSolution != null)
        {
            SculptHandLandmarkerRunner solutionRunner =
                handTrackingSolution.GetComponent<SculptHandLandmarkerRunner>();
            if (solutionRunner != null)
                return solutionRunner;
        }

        if (taiwanHandTracking == null)
            return null;

        SculptHandLandmarkerRunner[] runners =
            taiwanHandTracking.GetComponentsInChildren<SculptHandLandmarkerRunner>(true);
        for (int i = 0; i < runners.Length; i++)
        {
            SculptHandLandmarkerRunner runner = runners[i];
            if (runner != null && runner.enabled)
                return runner;
        }

        return null;
    }

    /// <summary>
    /// Activates the shared MediaPipe hierarchy used by both crafts. The
    /// Solution GameObject cannot start while its TaiwanHandTracking parent
    /// stays inactive.
    /// </summary>
    private void ActivateSharedHandTrackingRoot()
    {
        if (taiwanHandTracking != null)
            taiwanHandTracking.SetActive(true);

        if (handTrackingSolution != null)
            handTrackingSolution.SetActive(true);
    }

    private IEnumerator CheckHandDetection(SculptHandLandmarkerRunner handRunner)
    {
        yield return new WaitForSeconds(2f); // Wait for hand tracking to initialize

        int checkCount = 0;
        while (checkCount < 10)
        {
            if (handRunner.TryGetLatestHand(out SculptHandFrame hand))
            {
                Debug.Log($"MORPHIC: ✓ Hand detected! Check #{checkCount + 1}");
            }
            else
            {
                Debug.LogWarning($"MORPHIC: ✗ No hand detected. Check #{checkCount + 1}");
            }

            if (handRunner.TryGetLatestTwoHands(out SculptHandFrame first, out SculptHandFrame second))
            {
                Debug.Log($"MORPHIC: ✓ Two hands detected! Check #{checkCount + 1}");
            }

            yield return new WaitForSeconds(1f);
            checkCount++;
        }

        Debug.Log("MORPHIC: Hand detection check complete.");
    }

    private void StartTeapotGestureTutorial()
    {
        TeapotGestureTutorial tutorial = GetComponent<TeapotGestureTutorial>();
        if (tutorial == null)
            tutorial = gameObject.AddComponent<TeapotGestureTutorial>();

        TeapotGestureController teapotGestures = indiaTeapot != null
            ? indiaTeapot.GetComponentInChildren<TeapotGestureController>(true)
            : FindFirstObjectByType<TeapotGestureController>(FindObjectsInactive.Include);

        tutorial.Begin(teapotGestures, FinishTeapotGestureTutorial);
    }

    private void FinishTeapotGestureTutorial()
    {
        if (teapotColorController == null && indiaTeapot != null)
            teapotColorController = indiaTeapot.GetComponentInChildren<SculptingEnhancementTeapotColorController>(true);

        TeapotGestureController teapotGestures = indiaTeapot != null
            ? indiaTeapot.GetComponentInChildren<TeapotGestureController>(true)
            : null;
        if (teapotGestures != null)
            teapotGestures.suppressDriving = false;

        if (teapotColorController != null)
        {
            teapotColorController.SetTutorialSculptLock(false);
            teapotColorController.SetPaletteAvailable(true);
        }

        ShowAiHelp();
    }

    private void ShowAiHelp()
    {
        if (aiHelpUi == null)
            aiHelpUi = FindFirstObjectByType<MorphicAIHelpUI>(FindObjectsInactive.Include);
        if (aiHelpUi == null)
            aiHelpUi = gameObject.AddComponent<MorphicAIHelpUI>();
        aiHelpUi.SetVisible(true);
    }

    private void EnsureSculptBackButton()
    {
        if (sculptBackRoot != null)
            return;

        Canvas canvas = null;
        if (dialoguePanel != null)
            canvas = dialoguePanel.GetComponentInParent<Canvas>(true);
        if (canvas == null)
            canvas = FindFirstObjectByType<Canvas>(FindObjectsInactive.Include);
        if (canvas == null)
            return;

        Image frame = LanguagePageBuilder.CreateRoundedImage(
            "SculptBackButton",
            canvas.transform,
            28,
            LanguagePageBuilder.Green);
        MorphicBackButton.PlaceOnPanelLeft(frame.rectTransform, 88f);
        frame.raycastTarget = true;

        Image fill = LanguagePageBuilder.CreateRoundedImage(
            "Fill",
            frame.transform,
            24,
            LanguagePageBuilder.Pink);
        LanguagePageBuilder.Stretch(fill.rectTransform, new Vector2(5f, 5f));
        fill.raycastTarget = true;

        Sprite icon = MorphicBackButton.LoadSprite();
        Image iconImage = LanguagePageBuilder.CreateImage(
            "Icon",
            fill.transform,
            icon);
        LanguagePageBuilder.Stretch(iconImage.rectTransform, new Vector2(10f, 10f));
        if (icon != null)
            MorphicBackButton.ApplyIcon(iconImage);
        else
            iconImage.color = Color.white;
        iconImage.raycastTarget = false;

        Button uiButton = frame.gameObject.AddComponent<Button>();
        uiButton.targetGraphic = fill;
        uiButton.onClick.AddListener(ReturnToCraftSelect);

        sculptBackRoot = frame.gameObject;
        sculptBackRoot.SetActive(false);
    }

    private void SetSculptBackVisible(bool visible)
    {
        EnsureSculptBackButton();
        if (sculptBackRoot == null)
            return;

        sculptBackRoot.SetActive(visible);
        if (visible)
            sculptBackRoot.transform.SetAsLastSibling();
    }

    private void ReturnToCraftSelect()
    {
        SetSculptBackVisible(false);

        if (aiHelpUi != null)
            aiHelpUi.SetVisible(false);

        SculptingGestureTutorial lanternTutorial =
            GetComponent<SculptingGestureTutorial>();
        if (lanternTutorial != null)
            lanternTutorial.Hide();

        TeapotGestureTutorial teapotTutorial =
            GetComponent<TeapotGestureTutorial>();
        if (teapotTutorial != null)
            teapotTutorial.Hide();

        if (lanternColorController != null)
            lanternColorController.SetPaletteAvailable(false);

        if (teapotColorController != null)
            teapotColorController.SetPaletteAvailable(false);

        SetLanternSculptDriving(false);
        if (taiwanGestureIndicator != null)
            taiwanGestureIndicator.SetActive(false);

        TeapotGestureController teapotGestures = indiaTeapot != null
            ? indiaTeapot.GetComponentInChildren<TeapotGestureController>(true)
            : null;
        if (teapotGestures != null)
            teapotGestures.suppressDriving = true;

        if (indiaTeapot != null)
            indiaTeapot.SetActive(false);

        GameObject teapotStatus = FindSceneObject("Teapot Gesture Status");
        if (teapotStatus != null)
            teapotStatus.SetActive(false);

        if (webcamDisplayController != null)
            webcamDisplayController.SetActive(false);
        if (handAnnotationController != null)
            handAnnotationController.SetActive(false);

        if (dialoguePanel != null)
            dialoguePanel.SetActive(true);

        ShowNode(10);
    }

    private void StartLanternGestureTutorial()
    {
        SculptingGestureTutorial tutorial =
            GetComponent<SculptingGestureTutorial>();
        if (tutorial == null)
            tutorial = gameObject.AddComponent<SculptingGestureTutorial>();

        TaiwanLanternGestureController lanternGestures =
            taiwanGestureController != null
                ? taiwanGestureController.GetComponentInChildren<TaiwanLanternGestureController>(true)
                : FindFirstObjectByType<TaiwanLanternGestureController>(
                    FindObjectsInactive.Include);

        tutorial.Begin(lanternGestures, FinishLanternGestureTutorial);
    }

    private void FinishLanternGestureTutorial()
    {
        SculptingEnhancementIndexGestureController index =
            FindFirstObjectByType<SculptingEnhancementIndexGestureController>(
                FindObjectsInactive.Include);
        if (index != null)
            index.ResetAfterTutorial();

        if (lanternColorController != null)
            lanternColorController.SetTutorialSculptLock(false);
        SetLanternSculptDriving(true);
        if (lanternColorController != null)
            lanternColorController.SetPaletteAvailable(true);
    }

    private void SetLanternSculptDriving(bool enabled)
    {
        TaiwanLanternGestureController original =
            taiwanGestureController != null
                ? taiwanGestureController.GetComponentInChildren<TaiwanLanternGestureController>(true)
                : FindFirstObjectByType<TaiwanLanternGestureController>(
                    FindObjectsInactive.Include);
        if (original != null)
        {
            original.suppressDriving = !enabled;
            original.enabled = true;
        }

        SculptingEnhancementIndexGestureController index =
            FindFirstObjectByType<SculptingEnhancementIndexGestureController>(
                FindObjectsInactive.Include);
        if (index != null)
        {
            index.suppressDriving = !enabled;
            index.enabled = true;
        }
    }
}
