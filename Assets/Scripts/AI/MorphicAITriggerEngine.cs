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
        public int width;
        public int height;
        public int topper;
        public int bottom;
        public int lidTop;
        public int lidBottom;
        public int bamboo;
        public int tassel;
        public int hasStamp;
        public int hasSelfie;
        public int locked;
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
        TriggerAI("user_question", new MorphicCraftHelpState
        {
            userQuestion = userQuestion,
            currentStage = currentStage
        }, false);
    }

    public bool RequestSculptHelp(MorphicCraftHelpState state)
    {
        if (state == null)
            return false;

        return TriggerAI("user_requested_help", state, true);
    }

    public void TriggerMeaningfulEvent(
        string eventType,
        string currentStage
    )
    {
    }

    private bool TriggerAI(
        string eventType,
        MorphicCraftHelpState state,
        bool live
    )
    {
        if (aiClient == null)
            return false;

        if (Time.time < nextAllowedRequestTime)
            return false;

        if (aiClient.IsRequestRunning)
            return false;

        SculptContext context = new SculptContext
        {
            userQuestion = string.IsNullOrEmpty(state.userQuestion)
                ? "Next sculpt step?"
                : state.userQuestion,
            currentStage = state.currentStage,
            currentTool = state.currentTool,
            width = state.width,
            height = state.height,
            topper = state.topper,
            bottom = state.bottom,
            lidTop = state.lidTop,
            lidBottom = state.lidBottom,
            bamboo = state.bamboo,
            tassel = state.tassel,
            hasStamp = state.hasStamp,
            hasSelfie = state.hasSelfie,
            locked = state.locked
        };

        if (behaviorTracker != null)
        {
            if (string.IsNullOrEmpty(context.currentStage))
                context.currentStage = behaviorTracker.CurrentStage;
            if (string.IsNullOrEmpty(context.currentTool))
                context.currentTool = behaviorTracker.CurrentTool;
        }

        AIEventData eventData = new AIEventData
        {
            projectType = "digital_sculpture",
            sessionId = sessionId,
            eventType = eventType,
            context = context
        };

        nextAllowedRequestTime = Time.time + minimumRequestInterval;
        aiClient.SendFeedback(JsonUtility.ToJson(eventData), live);
        return true;
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
