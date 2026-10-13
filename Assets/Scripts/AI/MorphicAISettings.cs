using UnityEngine;

public class MorphicAISettings : MonoBehaviour
{
    [Header("AI Safety")]
    [SerializeField] private bool liveAIEnabled = false;

    [Header("Backend")]
    [SerializeField]
    private string backendBaseUrl =
        "https://morphic-openai-backend.onrender.com";

    public bool LiveAIEnabled => liveAIEnabled;

    public string GetFeedbackEndpoint()
    {
        if (liveAIEnabled)
        {
            return backendBaseUrl + "/api/feedback";
        }

        return backendBaseUrl + "/api/feedback-test";
    }

    [ContextMenu("Show Selected Endpoint")]
    private void ShowSelectedEndpoint()
    {
        Debug.Log(
            "MORPHIC AI MODE: " +
            (liveAIEnabled ? "LIVE AI" : "TEST") +
            "\nEndpoint: " +
            GetFeedbackEndpoint()
        );
    }
}