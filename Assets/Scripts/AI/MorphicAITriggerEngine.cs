using System;
using UnityEngine;

public class MorphicAITriggerEngine : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private MorphicAIClient aiClient;
    [SerializeField] private MorphicAIBehaviorTracker behaviorTracker;

    [Header("AI Request Safety")]
    [SerializeField] private float minimumRequestInterval = 20f;

    [Header("Debug")]
    [SerializeField] private bool showDebugLogs = true;

    private float nextAllowedRequestTime = 0f;

    // One session ID for the entire current Play session.
    private string sessionId;

    [Serializable]
    private class SculptContext
    {
        public string userQuestion;
        public string currentStage;
        public string currentTool;

        public int strokeCount;
        public int undoCount;
        public int consecutiveFailures;

        public float idleSeconds;
    }

    [Serializable]
    private class AIEventData
    {
        public string projectType;
        public string sessionId;
        public string eventType;
        public SculptContext context;
    }

    private void Awake()
    {
        if (aiClient == null)
        {
            aiClient = GetComponent<MorphicAIClient>();
        }

        if (behaviorTracker == null)
        {
            behaviorTracker = GetComponent<MorphicAIBehaviorTracker>();
        }

        if (aiClient == null)
        {
            Debug.LogError(
                "MorphicAITriggerEngine: MorphicAIClient reference is missing."
            );
        }

        if (behaviorTracker == null)
        {
            Debug.LogError(
                "MorphicAITriggerEngine: MorphicAIBehaviorTracker reference is missing."
            );
        }

        // Create ONE session ID when Play Mode starts.
        sessionId = Guid.NewGuid().ToString();

        if (showDebugLogs)
        {
            Debug.Log(
                "MorphicAITriggerEngine: New session started.\n" +
                "Session ID: " + sessionId
            );
        }
    }

    public void RequestUserHelp(
        string userQuestion,
        string currentStage
    )
    {
        TriggerAI(
            "user_question",
            userQuestion,
            currentStage
        );
    }

    public void TriggerMeaningfulEvent(
        string eventType,
        string currentStage
    )
    {
        TriggerAI(
            eventType,
            "",
            currentStage
        );
    }

    private void TriggerAI(
        string eventType,
        string userQuestion,
        string currentStage
    )
    {
        if (aiClient == null)
        {
            Debug.LogError(
                "MorphicAITriggerEngine: Cannot trigger AI because " +
                "MorphicAIClient is missing."
            );

            return;
        }

        if (behaviorTracker == null)
        {
            Debug.LogError(
                "MorphicAITriggerEngine: Cannot trigger AI because " +
                "MorphicAIBehaviorTracker is missing."
            );

            return;
        }

        if (Time.time < nextAllowedRequestTime)
        {
            if (showDebugLogs)
            {
                Debug.Log(
                    "MorphicAITriggerEngine: AI request blocked by local cooldown."
                );
            }

            return;
        }

        if (aiClient.IsRequestRunning)
        {
            if (showDebugLogs)
            {
                Debug.Log(
                    "MorphicAITriggerEngine: AI request already running."
                );
            }

            return;
        }

        AIEventData eventData = new AIEventData
        {
            projectType = "digital_sculpture",
            sessionId = sessionId,
            eventType = eventType,

            context = new SculptContext
            {
                userQuestion = userQuestion,

                currentStage = string.IsNullOrEmpty(currentStage)
                    ? behaviorTracker.CurrentStage
                    : currentStage,

                currentTool = behaviorTracker.CurrentTool,

                strokeCount = behaviorTracker.StrokeCount,
                undoCount = behaviorTracker.UndoCount,
                consecutiveFailures =
                    behaviorTracker.ConsecutiveFailures,

                idleSeconds = behaviorTracker.IdleSeconds
            }
        };

        string jsonData = JsonUtility.ToJson(eventData);

        nextAllowedRequestTime =
            Time.time + minimumRequestInterval;

        if (showDebugLogs)
        {
            Debug.Log(
                "MorphicAITriggerEngine: AI trigger created.\n" +
                jsonData
            );
        }

        aiClient.SendFeedback(jsonData);
    }

    [ContextMenu("Test User Question Trigger")]
    private void TestUserQuestionTrigger()
    {
        RequestUserHelp(
            "How can I improve my sculpture?",
            ""
        );
    }
}