using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

public class DialogueManager : MonoBehaviour
{
    // =========================================================
    // DIALOGUE UI
    // =========================================================

    [Header("Dialogue UI")]

    [SerializeField]
    private GameObject dialoguePanel;

    [SerializeField]
    private TMP_Text speakerName;

    [SerializeField]
    private TMP_Text dialogueText;


    // =========================================================
    // CAMERA TRANSITION
    // =========================================================

    [Header("Camera Transition")]

    [Tooltip("MainMenuStartTransition used to enter the Pottery Studio.")]
    [SerializeField]
    private MainMenuStartTransition cameraTransition;


    // =========================================================
    // CHOICE 1
    // =========================================================

    [Header("Choice 1")]

    [SerializeField]
    private Button choice1Button;

    [SerializeField]
    private TMP_Text choice1Text;


    // =========================================================
    // CHOICE 2
    // =========================================================

    [Header("Choice 2")]

    [SerializeField]
    private Button choice2Button;

    [SerializeField]
    private TMP_Text choice2Text;


    // =========================================================
    // CHOICE 3
    // =========================================================

    [Header("Choice 3")]

    [SerializeField]
    private Button choice3Button;

    [SerializeField]
    private TMP_Text choice3Text;


    // =========================================================
    // CHOICE 4
    // =========================================================

    [Header("Choice 4")]

    [SerializeField]
    private Button choice4Button;

    [SerializeField]
    private TMP_Text choice4Text;


    // =========================================================
    // CULTURE CHOICES
    // =========================================================

    [Header("Culture Choices")]

    [SerializeField]
    private GameObject cultureChoicesPanel;

    [SerializeField]
    private Button taiwanButton;

    [SerializeField]
    private Button indiaButton;

    [SerializeField]
    private Button indonesiaButton;

    [SerializeField]
    private Button freestyleButton;


    // =========================================================
    // SETTINGS
    // =========================================================

    [Header("Timing")]

    [SerializeField]
    private float startDelay = 2.1f;
    [Header("Taiwan Lantern")]

[SerializeField]
private GameObject taiwanLantern;

[SerializeField]
private Transform taiwanLanternSpawnPoint;
[Header("Taiwan Hand Tracking")]
[SerializeField] private GameObject taiwanHandTracking;
[SerializeField] private GameObject taiwanGestureController;
[SerializeField] private GameObject taiwanGestureIndicator;


    // =========================================================
    // INTERNAL
    // =========================================================

    private Coroutine dialogueCoroutine;

    private bool dialogueStarted;


    // =========================================================
    // UNITY
    // =========================================================

    private void Awake()
    {
        FindMissingReferences();

        AddHoverEffect(taiwanButton);
        AddHoverEffect(indiaButton);
        AddHoverEffect(indonesiaButton);
        AddHoverEffect(freestyleButton);

        HideAllChoices();
        HideCultureChoices();
    }

    private void AddHoverEffect(Button btn)
    {
        if (btn != null && btn.gameObject.GetComponent<ButtonHoverEffect>() == null)
        {
            btn.gameObject.AddComponent<ButtonHoverEffect>();
        }
    }


    private void OnEnable()
    {
        // Do not restart the dialogue every time the Gameplay UI
        // is temporarily enabled again.

        if (dialogueStarted)
        {
            return;
        }

        if (dialogueCoroutine != null)
        {
            StopCoroutine(dialogueCoroutine);
        }

        dialogueCoroutine =
            StartCoroutine(StartDialogueAfterDelay());
    }


    // =========================================================
    // AUTOMATIC REFERENCE SEARCH
    // =========================================================

    private void FindMissingReferences()
    {
        if (cameraTransition == null)
        {
            cameraTransition =
                FindObjectOfType<MainMenuStartTransition>();
        }
    }


    // =========================================================
    // START DIALOGUE
    // =========================================================

    private IEnumerator StartDialogueAfterDelay()
    {
        yield return new WaitForSeconds(startDelay);

        BeginDialogue();
    }


    public void BeginDialogue()
    {
        if (dialogueStarted)
        {
            return;
        }

        dialogueStarted = true;

        if (dialoguePanel != null)
        {
            dialoguePanel.SetActive(true);
        }

        ShowNode(0);
    }


    // =========================================================
    // DIALOGUE NODES
    // =========================================================

    private void ShowNode(int node)
    {
        HideAllChoices();
        HideCultureChoices();

        switch (node)
        {
            // =================================================
            // NODE 0
            // =================================================

            case 0:

                SetDialogue(
                    "LISA",
                    "Hi! I'm Lisa. Nice to meet you!"
                );

                SetChoice(
                    choice1Button,
                    choice1Text,
                    "Hi, Lisa!",
                    () => ShowNode(1)
                );

                SetChoice(
                    choice2Button,
                    choice2Text,
                    "Where are we?",
                    () => ShowNode(2)
                );

                break;


            // =================================================
            // NODE 1
            // =================================================

            case 1:

                SetDialogue(
                    "LISA",
                    "It's wonderful to finally be back in my beloved city of Yuanlin."
                );

                SetChoice(
                    choice1Button,
                    choice1Text,
                    "Tell me about this place.",
                    () => ShowNode(3)
                );

                SetChoice(
                    choice2Button,
                    choice2Text,
                    "Why did you open a pottery shop?",
                    () => ShowNode(4)
                );

                break;


            // =================================================
            // NODE 2
            // =================================================

            case 2:

                SetDialogue(
                    "LISA",
                    "We're in Yuanlin, my hometown. And this little pottery shop is my new home for creating art."
                );

                SetChoice(
                    choice1Button,
                    choice1Text,
                    "Why pottery?",
                    () => ShowNode(4)
                );

                SetChoice(
                    choice2Button,
                    choice2Text,
                    "Tell me about your journey.",
                    () => ShowNode(5)
                );

                break;


            // =================================================
            // NODE 3
            // =================================================

            case 3:

                SetDialogue(
                    "LISA",
                    "Yuanlin is full of life, food, people and little places worth discovering. I wanted my shop to become one of those places."
                );

                SetChoice(
                    choice1Button,
                    choice1Text,
                    "That sounds lovely.",
                    () => ShowNode(6)
                );

                SetChoice(
                    choice2Button,
                    choice2Text,
                    "So why pottery?",
                    () => ShowNode(4)
                );

                break;


            // =================================================
            // NODE 4
            // =================================================

            case 4:

                SetDialogue(
                    "LISA",
                    "Because clay lets you turn an idea into something you can actually touch. And during my studies, I discovered how differently cultures approach the same material."
                );

                SetChoice(
                    choice1Button,
                    choice1Text,
                    "What did you learn?",
                    () => ShowNode(5)
                );

                SetChoice(
                    choice2Button,
                    choice2Text,
                    "Let's make something!",
                    () => ShowNode(6)
                );

                break;


            // =================================================
            // NODE 5
            // =================================================

            case 5:

                SetDialogue(
                    "LISA",
                    "I studied art in India and Indonesia. Their sculpting traditions inspired me to create a place where different cultures could meet through clay."
                );

                SetChoice(
                    choice1Button,
                    choice1Text,
                    "I want to see!",
                    () => ShowNode(6)
                );

                SetChoice(
                    choice2Button,
                    choice2Text,
                    "That sounds amazing.",
                    () => ShowNode(6)
                );

                break;


            // =================================================
            // NODE 6
            // =================================================

            case 6:

                SetDialogue(
                    "LISA",
                    "Exactly! I want this place to be more than a shop. I want people to experiment, learn and create something of their own."
                );

                SetChoice(
                    choice1Button,
                    choice1Text,
                    "Let's start!",
                    () => ShowNode(7)
                );

                break;


            // =================================================
            // NODE 7
            // MAIN LOCATION CHOICE
            // =================================================

            case 7:

                SetDialogue(
                    "LISA",
                    "So, where would you like to begin?"
                );

                SetChoice(
                    choice1Button,
                    choice1Text,
                    "Explore the Pottery Studio",
                    EnterPotteryStudio
                );

                SetChoice(
                    choice2Button,
                    choice2Text,
                    "Visit the Restaurant",
                    () => ShowNode(8)
                );

                SetChoice(
                    choice3Button,
                    choice3Text,
                    "Ask Lisa something",
                    () => ShowNode(9)
                );

                break;


            // =================================================
            // NODE 8
            // RESTAURANT
            // =================================================

            case 8:

                SetDialogue(
                    "LISA",
                    "The restaurant is just next door. We'll explore it soon!"
                );

                SetChoice(
                    choice1Button,
                    choice1Text,
                    "Go back.",
                    () => ShowNode(7)
                );

                break;


            // =================================================
            // NODE 9
            // ASK LISA
            // =================================================

            case 9:

                SetDialogue(
                    "LISA",
                    "There is a lot to discover here. Why don't you start by exploring the pottery studio?"
                );

                SetChoice(
                    choice1Button,
                    choice1Text,
                    "Explore the Pottery Studio",
                    EnterPotteryStudio
                );

                SetChoice(
                    choice2Button,
                    choice2Text,
                    "Go back.",
                    () => ShowNode(7)
                );

                break;


            // =================================================
            // NODE 10
            // POTTERY STUDIO WELCOME
            // =================================================

            case 10:

                SetDialogue(
                    "LISA",
                    "Welcome to my pottery studio! " +
                    "Here, you can explore different artistic traditions through clay. " +
                    "Which creation would you like to try?"
                );

                // IMPORTANT:
                // Do NOT use the normal Choice 1-4 buttons here.
                // The culture choices appear in the middle of the screen.

                ShowCultureChoices();

                break;
        }
    }


    // =========================================================
    // SET DIALOGUE TEXT
    // =========================================================

    private void SetDialogue(
        string speaker,
        string message)
    {
        if (speakerName != null)
        {
            speakerName.text = speaker;
        }

        if (dialogueText != null)
        {
            dialogueText.text = message;
        }
    }


    // =========================================================
    // SET NORMAL CHOICE
    // =========================================================

    private void SetChoice(
        Button button,
        TMP_Text text,
        string label,
        UnityAction action)
    {
        if (button == null)
        {
            return;
        }

        button.gameObject.SetActive(true);

        if (text != null)
        {
            text.text = label;
        }

        button.onClick.RemoveAllListeners();

        if (action != null)
        {
            button.onClick.AddListener(action);
        }
    }


    // =========================================================
    // SHOW CULTURE CHOICES
    // =========================================================

    private void ShowCultureChoices()
    {
        // Make absolutely sure the normal dialogue choices
        // are not visible at the same time.

        HideAllChoices();

        if (cultureChoicesPanel == null)
        {
            Debug.LogWarning(
                "MORPHIC: Culture Choices Panel is not assigned in DialogueManager."
            );

            return;
        }

        cultureChoicesPanel.SetActive(true);


        // -----------------------------------------------------
        // TAIWAN
        // -----------------------------------------------------

        if (taiwanButton != null)
        {
            taiwanButton.gameObject.SetActive(true);

            taiwanButton.onClick.RemoveAllListeners();

            taiwanButton.onClick.AddListener(
                () => SelectCulture("Taiwan")
            );
        }


        // -----------------------------------------------------
        // INDIA
        // -----------------------------------------------------

        if (indiaButton != null)
        {
            indiaButton.gameObject.SetActive(true);

            indiaButton.onClick.RemoveAllListeners();

            indiaButton.onClick.AddListener(
                () => SelectCulture("India")
            );
        }


        // -----------------------------------------------------
        // INDONESIA
        // -----------------------------------------------------

        if (indonesiaButton != null)
        {
            indonesiaButton.gameObject.SetActive(true);

            indonesiaButton.onClick.RemoveAllListeners();

            indonesiaButton.onClick.AddListener(
                () => SelectCulture("Indonesia")
            );
        }


        // -----------------------------------------------------
        // FREESTYLE
        // -----------------------------------------------------

        if (freestyleButton != null)
        {
            freestyleButton.gameObject.SetActive(true);

            freestyleButton.onClick.RemoveAllListeners();

            freestyleButton.onClick.AddListener(
                () => SelectCulture("Freestyle")
            );
        }
    }


    // =========================================================
    // SELECT CULTURE
    // =========================================================

    private void SelectCulture(string culture)
    {
        Debug.Log(
            "MORPHIC: Culture selected = " + culture
        );

        // Remove the middle-screen culture buttons.

        HideCultureChoices();

        // Show the next dialogue.

        StartSculpting(culture);
    }


    // =========================================================
    // ENTER POTTERY STUDIO
    // =========================================================

    private void EnterPotteryStudio()
    {
        HideAllChoices();
        HideCultureChoices();

        if (cameraTransition == null)
        {
            cameraTransition =
                FindObjectOfType<MainMenuStartTransition>();
        }

        if (cameraTransition != null)
        {
            // Listen for the camera to finish moving.

            cameraTransition.PotteryStudioTransitionCompleted
                -= OnCameraFinished;

            cameraTransition.PotteryStudioTransitionCompleted
                += OnCameraFinished;

            cameraTransition.EnterPotteryStudio();
        }
        else
        {
            Debug.LogWarning(
                "MORPHIC: MainMenuStartTransition was not found."
            );
        }
    }


    // =========================================================
    // CAMERA FINISHED
    // =========================================================

    private void OnCameraFinished()
    {
        Debug.Log(
            "MORPHIC: Pottery camera finished. Enabling dialogue UI."
        );


        // Stop listening to the camera.

        if (cameraTransition != null)
        {
            cameraTransition.PotteryStudioTransitionCompleted
                -= OnCameraFinished;
        }


        // -----------------------------------------------------
        // IMPORTANT:
        //
        // Dialogue Panel is inside Gameplay UI.
        // The camera transition disables Gameplay UI,
        // so we must enable the parent first.
        // -----------------------------------------------------

        if (dialoguePanel != null)
        {
            Transform parent =
                dialoguePanel.transform.parent;

            if (parent != null)
            {
                parent.gameObject.SetActive(true);
            }

            dialoguePanel.SetActive(true);
        }


        // Show Lisa's pottery studio welcome.

        ShowNode(10);
    }


    // =========================================================
    // AFTER CULTURE SELECTION
    // =========================================================

    private void StartSculpting(string culture)
    {
        HideAllChoices();
        HideCultureChoices();


        switch (culture)
        {
            // =================================================
            // TAIWAN
            // =================================================

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


            // =================================================
            // INDIA
            // =================================================

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


            // =================================================
            // INDONESIA
            // =================================================

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


            // =================================================
            // FREESTYLE
            // =================================================

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


    // =========================================================
    // BEGIN SCULPTING
    // =========================================================

    private void BeginSculpting(string culture)
{
    HideAllChoices();

    if (dialoguePanel != null)
    {
        dialoguePanel.SetActive(false);
    }

    Debug.Log("MORPHIC: Starting sculpting for " + culture);

    switch (culture)
    {
        // =====================================================
        // TAIWAN
        // =====================================================

        case "Taiwan":

    Debug.Log("MORPHIC: Taiwan selected - Lantern");

    if (taiwanLantern != null)
    {
        if (taiwanLanternSpawnPoint != null)
        {
            taiwanLantern.transform.position =
                taiwanLanternSpawnPoint.position;

            taiwanLantern.transform.rotation =
                taiwanLanternSpawnPoint.rotation;
        }

        taiwanLantern.SetActive(true);

        ParametricSkyLantern lantern =
            taiwanLantern.GetComponent<ParametricSkyLantern>();

        if (lantern != null)
        {
            lantern.EnterGuidedMode();
            Debug.Log("MORPHIC: Taiwan Lantern guided mode started.");
        }
        else
        {
            Debug.LogWarning(
                "MORPHIC: ParametricSkyLantern component is missing."
            );
        }

        Debug.Log("MORPHIC: Taiwan Lantern activated.");
    }
    else
    {
        Debug.LogWarning(
            "MORPHIC: Taiwan Lantern reference is missing."
        );
    }


    // ===== START TAIWAN HAND TRACKING =====

    if (taiwanHandTracking != null)
    {
        taiwanHandTracking.SetActive(true);
    }

    if (taiwanGestureController != null)
    {
        taiwanGestureController.SetActive(true);
    }

    if (taiwanGestureIndicator != null)
    {
        taiwanGestureIndicator.SetActive(true);
    }

    // ===== END TAIWAN HAND TRACKING =====

    break;
        // =====================================================
        // INDIA
        // =====================================================

        case "India":

            Debug.Log("MORPHIC: India selected - Teapot");

            break;


        // =====================================================
        // INDONESIA
        // =====================================================

        case "Indonesia":

            Debug.Log("MORPHIC: Indonesia selected - Keris");

            break;


        // =====================================================
        // FREESTYLE
        // =====================================================

        case "Freestyle":

            Debug.Log("MORPHIC: Freestyle selected");

            break;
    }
}

    private IEnumerator LoadSculptingScene(string culture)
    {
        // Check if SculptingTest is already loaded
        UnityEngine.SceneManagement.Scene sculptScene = UnityEngine.SceneManagement.SceneManager.GetSceneByName("SculptingTest");
        if (!sculptScene.isLoaded)
        {
            AsyncOperation asyncLoad = UnityEngine.SceneManagement.SceneManager.LoadSceneAsync("SculptingTest", UnityEngine.SceneManagement.LoadSceneMode.Additive);
            
            if (asyncLoad == null)
            {
                Debug.LogError("MORPHIC: Failed to load SculptingTest scene. Please ensure it is added to File -> Build Profiles (or Build Settings) -> Scenes In Build.");
                yield break;
            }

            while (!asyncLoad.isDone)
            {
                yield return null;
            }
        }

        BaseShapeSelector shapeSelector = FindFirstObjectByType<BaseShapeSelector>(FindObjectsInactive.Include);
        if (shapeSelector != null)
        {
            shapeSelector.gameObject.SetActive(true);
                        if (culture == "Taiwan")
            {
                // Spawn a cube on the table instead of a lantern
                shapeSelector.SelectCube(); 
                
                // Immediately turn on the deformation logic so gestures work
                ParametricSkyLantern lantern = FindFirstObjectByType<ParametricSkyLantern>();
                if (lantern != null)
                {
                    lantern.EnterGuidedMode();
                }
            }

        }

        SculptUIController uiController = FindFirstObjectByType<SculptUIController>(FindObjectsInactive.Include);
        if (uiController != null)
        {
            uiController.gameObject.SetActive(true);
            uiController.enabled = true;
        }
    }


    // =========================================================
    // HIDE CULTURE CHOICES
    // =========================================================

    private void HideCultureChoices()
    {
        if (cultureChoicesPanel != null)
        {
            cultureChoicesPanel.SetActive(false);
        }
    }


    // =========================================================
    // HIDE ALL NORMAL CHOICES
    // =========================================================

    private void HideAllChoices()
    {
        if (choice1Button != null)
        {
            choice1Button.gameObject.SetActive(false);
        }

        if (choice2Button != null)
        {
            choice2Button.gameObject.SetActive(false);
        }

        if (choice3Button != null)
        {
            choice3Button.gameObject.SetActive(false);
        }

        if (choice4Button != null)
        {
            choice4Button.gameObject.SetActive(false);
        }
    }
}