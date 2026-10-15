
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
    [Tooltip("Teapot_test scene instance to show when India sculpting starts.")]
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

    private void Awake()
    {
        FindMissingReferences();

        lanternColorController =
            FindFirstObjectByType<SculptingEnhancementLanternColorController>(
                FindObjectsInactive.Include);
        if (lanternColorController != null)
            lanternColorController.SetPaletteAvailable(false);

        // The teapot is placed in the scene like the lantern, but stays hidden
        // until the user chooses India and presses Start Sculpting.
        if (indiaTeapot == null)
            indiaTeapot = FindSceneObject("Teapot_test");

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
                    "Tell me about this place.", () => ShowNode(3)
                );

                SetChoice(
                    choice2Button, choice2Text,
                    "Why did you open a pottery shop?", () => ShowNode(4)
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
                    "Tell me about your journey.", () => ShowNode(5)
                );
                break;

            case 3:
                SetDialogue(
                    "LISA",
                    "Yuanlin is full of life, food, people and little places worth discovering. I wanted my shop to become one of those places."
                );

                SetChoice(
                    choice1Button, choice1Text,
                    "That sounds lovely.", () => ShowNode(6)
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
                    "That sounds amazing.", () => ShowNode(6)
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
                    "Explore the Pottery Studio", EnterPotteryStudio
                );

                SetChoice(
                    choice2Button, choice2Text,
                    "Visit the Restaurant", EnterRestaurant
                );

                SetChoice(
                    choice3Button, choice3Text,
                    "Ask Lisa something", () => ShowNode(9)
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
                    "Return to the shop entrance",
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
                    "Explore the Pottery Studio", EnterPotteryStudio
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

    private void SetChoice(
        Button button,
        TMP_Text text,
        string label,
        UnityAction action)
    {
        if (button == null)
            return;

        button.gameObject.SetActive(true);

        if (text != null)
            text.text = label;

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

        if (lanternColorController == null)
        {
            lanternColorController =
                FindFirstObjectByType<SculptingEnhancementLanternColorController>(
                    FindObjectsInactive.Include);
        }

        if (lanternColorController != null)
        {
            bool supportsColorPalette =
                culture == "Taiwan" || culture == "India";
            lanternColorController.SetPaletteAvailable(supportsColorPalette);
        }

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

                // Activate webcam display with border in top-left
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

                break;

            case "India":
                Debug.Log("MORPHIC: India selected - Teapot");

                if (indiaTeapot != null)
                {
                    // Keep the teapot's Inspector-configured position, rotation,
                    // and scale, just as the Taiwan lantern keeps its scene setup.
                    indiaTeapot.SetActive(true);
                    Debug.Log("MORPHIC: India Teapot activated.");
                }
                else
                {
                    Debug.LogWarning(
                        "MORPHIC: India Teapot reference is missing. " +
                        "Add the Teapot_test scene object and assign it to " +
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
}
