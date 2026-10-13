using System;
using System.Collections;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;

public class MorphicAIClient : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private MorphicAISettings aiSettings;
    [SerializeField] private MorphicAIActionRouter actionRouter;

    [Header("Debug")]
    [SerializeField] private bool showDebugLogs = true;

    public bool IsRequestRunning { get; private set; }

    // Sends the human-readable AI message to the feedback UI.
    public event Action<string> OnFeedbackMessageReceived;

    [Serializable]
    private class AIResponse
    {
        public bool success;
        public bool testMode;

        public AIRequestFeedback feedback;
        public string error;

        public string knowledgeQuery;
        public string preparedInput;
    }

    [Serializable]
    private class AIRequestFeedback
    {
        public string message;
        public string feedbackType;
        public AIAction action;
        public string priority;
        public float cooldownSeconds;
    }

    [Serializable]
    private class AIAction
    {
        public string type;
        public string target;
        public float value;
    }

    private void Awake()
    {
        if (aiSettings == null)
        {
            aiSettings = GetComponent<MorphicAISettings>();
        }

        if (actionRouter == null)
        {
            actionRouter = GetComponent<MorphicAIActionRouter>();
        }

        if (aiSettings == null)
        {
            Debug.LogError(
                "MorphicAIClient: MorphicAISettings reference is missing."
            );
        }

        if (actionRouter == null)
        {
            Debug.LogError(
                "MorphicAIClient: MorphicAIActionRouter reference is missing."
            );
        }
    }

    public void SendFeedback(string jsonData)
    {
        if (IsRequestRunning)
        {
            if (showDebugLogs)
            {
                Debug.Log(
                    "MorphicAIClient: A request is already running. Ignoring new request."
                );
            }

            return;
        }

        if (aiSettings == null)
        {
            Debug.LogError(
                "MorphicAIClient: Cannot send request because " +
                "MorphicAISettings is missing."
            );

            return;
        }

        if (string.IsNullOrWhiteSpace(jsonData))
        {
            Debug.LogError(
                "MorphicAIClient: JSON data is empty."
            );

            return;
        }

        StartCoroutine(
            SendFeedbackCoroutine(jsonData)
        );
    }

    private IEnumerator SendFeedbackCoroutine(
        string jsonData
    )
    {
        IsRequestRunning = true;

        string endpoint =
            aiSettings.GetFeedbackEndpoint();

        if (showDebugLogs)
        {
            Debug.Log(
                "MorphicAIClient: Sending request to:\n" +
                endpoint
            );

            Debug.Log(
                "MorphicAIClient: JSON being sent:\n" +
                jsonData
            );
        }

        byte[] bodyRaw =
            Encoding.UTF8.GetBytes(jsonData);

        using (
            UnityWebRequest request =
                new UnityWebRequest(
                    endpoint,
                    "POST"
                )
        )
        {
            request.uploadHandler =
                new UploadHandlerRaw(bodyRaw);

            request.downloadHandler =
                new DownloadHandlerBuffer();

            request.SetRequestHeader(
                "Content-Type",
                "application/json"
            );

            yield return request.SendWebRequest();

            if (
                request.result ==
                UnityWebRequest.Result.Success
            )
            {
                string responseText =
                    request.downloadHandler.text;

                if (showDebugLogs)
                {
                    Debug.Log(
                        "MorphicAIClient: Response received:\n" +
                        responseText
                    );
                }

                HandleResponse(responseText);
            }
            else
            {
                Debug.LogError(
                    "MorphicAIClient: Request failed.\n" +
                    "Error: " + request.error + "\n" +
                    "HTTP Status: " + request.responseCode
                );
            }
        }

        IsRequestRunning = false;
    }

    private void HandleResponse(
        string responseText
    )
    {
        try
        {
            AIResponse response =
                JsonUtility.FromJson<AIResponse>(
                    responseText
                );

            if (response == null)
            {
                Debug.LogError(
                    "MorphicAIClient: Could not parse backend response."
                );

                return;
            }

            if (!response.success)
            {
                Debug.LogError(
                    "MorphicAIClient: Backend returned an error: " +
                    response.error
                );

                return;
            }

            // ---------------------------------------------
            // TEST MODE
            // ---------------------------------------------

            if (response.testMode)
            {
                Debug.Log(
                    "MORPHIC AI TEST: Backend and RAG connection succeeded."
                );

                Debug.Log(
                    "MORPHIC AI TEST QUERY: " +
                    response.knowledgeQuery
                );

                Debug.Log(
                    "MORPHIC AI TEST: No OpenAI request was made."
                );

                OnFeedbackMessageReceived?.Invoke(
                    "Test connection successful.\n" +
                    "Backend and RAG are connected."
                );

                return;
            }

            // ---------------------------------------------
            // REAL AI RESPONSE
            // ---------------------------------------------

            if (response.feedback == null)
            {
                Debug.LogError(
                    "MorphicAIClient: Successful AI response " +
                    "contained no feedback object."
                );

                return;
            }

            Debug.Log(
                "MORPHIC AI MESSAGE: " +
                response.feedback.message
            );

            Debug.Log(
                "MORPHIC AI TYPE: " +
                response.feedback.feedbackType
            );

            Debug.Log(
                "MORPHIC AI PRIORITY: " +
                response.feedback.priority
            );

            Debug.Log(
                "MORPHIC AI COOLDOWN: " +
                response.feedback.cooldownSeconds +
                " seconds"
            );

            // ---------------------------------------------
            // SEND MESSAGE TO UI
            // ---------------------------------------------

            OnFeedbackMessageReceived?.Invoke(
                response.feedback.message
            );

            // ---------------------------------------------
            // SEND VALIDATED ACTION TO ROUTER
            // ---------------------------------------------

            if (response.feedback.action != null)
            {
                string actionType =
                    response.feedback.action.type;

                string target =
                    response.feedback.action.target;

                float value =
                    response.feedback.action.value;

                Debug.Log(
                    "MORPHIC AI ACTION: " +
                    actionType
                );

                Debug.Log(
                    "MORPHIC AI TARGET: " +
                    target
                );

                Debug.Log(
                    "MORPHIC AI VALUE: " +
                    value
                );

                if (actionRouter != null)
                {
                    actionRouter.HandleAIAction(
                        actionType,
                        target,
                        value
                    );
                }
                else
                {
                    Debug.LogWarning(
                        "MorphicAIClient: No Action Router assigned. " +
                        "AI action was not processed."
                    );
                }
            }
        }
        catch (Exception exception)
        {
            Debug.LogError(
                "MorphicAIClient: Response parsing failed.\n" +
                exception.Message
            );
        }
    }

    [ContextMenu("Test AI Connection")]
    private void TestAIConnection()
    {
        string testJson =
            "{"
            + "\"projectType\":\"digital_sculpture\","
            + "\"sessionId\":\"unity-test-session\","
            + "\"eventType\":\"user_question\","
            + "\"context\":{"
            + "\"userQuestion\":\"How can I improve my sculpture?\","
            + "\"currentStage\":\"test\""
            + "}"
            + "}";

        SendFeedback(testJson);
    }
}