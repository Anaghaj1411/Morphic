using UnityEngine;
using TMPro;

public class MorphicAIFeedbackUI : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private MorphicAIClient aiClient;
    [SerializeField] private TMP_Text feedbackText;

    [Header("Display")]
    [SerializeField] private string waitingMessage =
        "Morphic AI\nWaiting for feedback...";

    private void Awake()
    {
        if (feedbackText == null)
        {
            feedbackText = GetComponentInChildren<TMP_Text>();
        }

        if (aiClient == null)
        {
            aiClient = FindFirstObjectByType<MorphicAIClient>();
        }

        if (feedbackText == null)
        {
            Debug.LogError(
                "MorphicAIFeedbackUI: AI Feedback Text reference is missing."
            );
        }

        if (aiClient == null)
        {
            Debug.LogError(
                "MorphicAIFeedbackUI: MorphicAIClient could not be found."
            );
        }

        if (feedbackText != null)
        {
            feedbackText.text = waitingMessage;
        }
    }

    private void OnEnable()
    {
        if (aiClient != null)
        {
            aiClient.OnFeedbackMessageReceived += HandleFeedbackMessage;
        }
    }

    private void OnDisable()
    {
        if (aiClient != null)
        {
            aiClient.OnFeedbackMessageReceived -= HandleFeedbackMessage;
        }
    }

    private void HandleFeedbackMessage(string message)
    {
        if (feedbackText == null)
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(message))
        {
            return;
        }

        feedbackText.text =
            "Morphic AI\n" +
            message;
    }

    public void ShowHint(string message)
    {
        if (feedbackText == null)
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(message))
        {
            return;
        }

        feedbackText.text =
            "Morphic AI — Hint\n" +
            message;

        Debug.Log(
            "MORPHIC AI UI: Hint displayed."
        );
    }

    public void ShowSummary(string message)
    {
        if (feedbackText == null)
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(message))
        {
            return;
        }

        feedbackText.text =
            "Morphic AI — Summary\n" +
            message;

        Debug.Log(
            "MORPHIC AI UI: Summary displayed."
        );
    }

    public void ShowEncouragement(string message)
    {
        if (feedbackText == null)
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(message))
        {
            return;
        }

        feedbackText.text =
            "Morphic AI\n" +
            message;

        Debug.Log(
            "MORPHIC AI UI: Encouragement displayed."
        );
    }
}