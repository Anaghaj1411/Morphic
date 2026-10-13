using UnityEngine;
using UnityEngine.UI;
using Mediapipe.Unity;

public class WebcamDisplayController : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Mediapipe.Unity.Screen webcamScreen;
    [SerializeField] private RectTransform borderPanel;

    [Header("Positioning")]
    [SerializeField] private Vector2 displaySize = new Vector2(280, 210);
    [SerializeField] private Vector2 anchoredPosition = new Vector2(150, -120);
    [SerializeField] private int borderThickness = 3;

    [Header("Border Appearance")]
    [SerializeField] private Color borderColor = new Color(0.3f, 0.3f, 0.3f, 1f);
    [SerializeField] private Color backgroundColor = new Color(0.1f, 0.1f, 0.1f, 0.8f);

    private void Awake()
    {
        SetupDisplay();
        // Start deactivated - will be activated by DialogueManager
        Deactivate();
    }

    private void SetupDisplay()
    {
        // Setup border panel - use top-left anchors
        if (borderPanel == null)
        {
            borderPanel = GetComponent<RectTransform>();
        }

        if (borderPanel != null)
        {
            // Set anchors to top-left corner
            borderPanel.anchorMin = new Vector2(0, 1);
            borderPanel.anchorMax = new Vector2(0, 1);
            borderPanel.pivot = new Vector2(0, 1);
            borderPanel.anchoredPosition = new Vector2(20, -20); // 20px from left, 20px from top
            borderPanel.sizeDelta = displaySize + new Vector2(borderThickness * 2, borderThickness * 2);

            // Add background and border
            Image borderImage = borderPanel.GetComponent<Image>();
            if (borderImage == null)
            {
                borderImage = borderPanel.gameObject.AddComponent<Image>();
            }
            borderImage.color = borderColor;
        }

        // Setup webcam screen
        if (webcamScreen == null)
        {
            webcamScreen = GetComponentInChildren<Mediapipe.Unity.Screen>();
        }

        if (webcamScreen != null)
        {
            RectTransform screenRect = webcamScreen.GetComponent<RectTransform>();
            if (screenRect != null)
            {
                // Child should stretch to fill parent
                screenRect.anchorMin = Vector2.zero;
                screenRect.anchorMax = Vector2.one;
                screenRect.pivot = new Vector2(0.5f, 0.5f);
                screenRect.anchoredPosition = Vector2.zero;
                screenRect.sizeDelta = Vector2.zero;
                screenRect.offsetMin = new Vector2(borderThickness, borderThickness);
                screenRect.offsetMax = new Vector2(-borderThickness, -borderThickness);
            }

            // Add background behind the webcam feed
            RawImage screenImage = webcamScreen.GetComponent<RawImage>();
            if (screenImage != null)
            {
                // The webcam feed will be displayed here
                // We'll keep it as is since MediaPipe handles the texture
            }
        }
    }

    public void UseScreen(Mediapipe.Unity.Screen screen)
    {
        if (screen == null)
        {
            Debug.LogError("MORPHIC: Cannot display a null MediaPipe Screen.");
            return;
        }

        if (webcamScreen != null && webcamScreen != screen)
        {
            webcamScreen.gameObject.SetActive(false);
        }

        webcamScreen = screen;
        webcamScreen.transform.SetParent(transform, false);
        SetupDisplay();
    }

    public void Activate()
    {
        if (borderPanel != null)
        {
            borderPanel.gameObject.SetActive(true);
        }

        if (webcamScreen != null)
        {
            webcamScreen.gameObject.SetActive(true);
            // Re-apply sizing when activating to ensure it's correct
            SetupDisplay();

            // Force the screen size to our desired size
            RectTransform screenRect = webcamScreen.GetComponent<RectTransform>();
            if (screenRect != null)
            {
                screenRect.sizeDelta = displaySize;
            }
        }
    }

    public void Deactivate()
    {
        if (webcamScreen != null)
        {
            webcamScreen.gameObject.SetActive(false);
        }

        if (borderPanel != null)
        {
            borderPanel.gameObject.SetActive(false);
        }
    }

    private void LateUpdate()
    {
        // Continuously enforce the size to prevent MediaPipe from resizing it
        if (webcamScreen != null && webcamScreen.gameObject.activeInHierarchy)
        {
            RectTransform screenRect = webcamScreen.GetComponent<RectTransform>();
            if (screenRect != null)
            {
                screenRect.sizeDelta = displaySize;
            }
        }
    }
}
