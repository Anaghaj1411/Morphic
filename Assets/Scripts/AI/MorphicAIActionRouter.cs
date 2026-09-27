using UnityEngine;

public class MorphicAIActionRouter : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private MorphicAIFeedbackUI feedbackUI;

    [Header("Debug")]
    [SerializeField] private bool showDebugLogs = true;

    private void Awake()
    {
        if (feedbackUI == null)
        {
            feedbackUI =
                FindFirstObjectByType<MorphicAIFeedbackUI>();
        }

        if (feedbackUI == null)
        {
            Debug.LogWarning(
                "MorphicAIActionRouter: " +
                "MorphicAIFeedbackUI could not be found."
            );
        }
    }

    public void HandleAIAction(
        string actionType,
        string target,
        float value
    )
    {
        if (string.IsNullOrWhiteSpace(actionType))
        {
            LogRejectedAction(
                "AI returned an empty action type."
            );

            return;
        }

        switch (actionType)
        {
            case "none":

                LogAcceptedAction(
                    "none",
                    target,
                    value
                );

                break;

            case "show_hint":

                LogAcceptedAction(
                    "show_hint",
                    target,
                    value
                );

                HandleShowHint(
                    target,
                    value
                );

                break;

            case "show_summary":

                LogAcceptedAction(
                    "show_summary",
                    target,
                    value
                );

                HandleShowSummary(
                    target,
                    value
                );

                break;

            case "play_encouragement":

                LogAcceptedAction(
                    "play_encouragement",
                    target,
                    value
                );

                HandlePlayEncouragement(
                    target,
                    value
                );

                break;

            default:

                LogRejectedAction(
                    "Unknown AI action: " +
                    actionType
                );

                break;
        }
    }

    private void HandleShowHint(
        string target,
        float value
    )
    {
        Debug.Log(
            "MORPHIC AI ROUTER: " +
            "Validated show_hint action.\n" +
            "Target: " + target + "\n" +
            "Value: " + value
        );

        if (feedbackUI == null)
        {
            Debug.LogWarning(
                "MORPHIC AI ROUTER: " +
                "Cannot display hint because Feedback UI is missing."
            );

            return;
        }

        // The actual human-readable message has already
        // been sent to the feedback UI by MorphicAIClient.
        // This action confirms that Unity accepted the hint.
        Debug.Log(
            "MORPHIC AI ROUTER: " +
            "show_hint action forwarded to Feedback UI."
        );
    }

    private void HandleShowSummary(
        string target,
        float value
    )
    {
        Debug.Log(
            "MORPHIC AI ROUTER: " +
            "Validated show_summary action.\n" +
            "Target: " + target + "\n" +
            "Value: " + value
        );

        if (feedbackUI == null)
        {
            Debug.LogWarning(
                "MORPHIC AI ROUTER: " +
                "Cannot display summary because Feedback UI is missing."
            );

            return;
        }

        Debug.Log(
            "MORPHIC AI ROUTER: " +
            "show_summary action accepted."
        );
    }

    private void HandlePlayEncouragement(
        string target,
        float value
    )
    {
        Debug.Log(
            "MORPHIC AI ROUTER: " +
            "Validated play_encouragement action.\n" +
            "Target: " + target + "\n" +
            "Value: " + value
        );

        Debug.Log(
            "MORPHIC AI ROUTER: " +
            "play_encouragement action accepted."
        );
    }

    private void LogAcceptedAction(
        string actionType,
        string target,
        float value
    )
    {
        if (!showDebugLogs)
        {
            return;
        }

        Debug.Log(
            "MORPHIC AI ROUTER: " +
            "Accepted action = " +
            actionType
        );
    }

    private void LogRejectedAction(
        string reason
    )
    {
        Debug.LogWarning(
            "MORPHIC AI ROUTER: " +
            "Rejected AI action.\n" +
            reason
        );
    }
}