using UnityEngine;

public class MorphicAIBehaviorTracker : MonoBehaviour
{
    [Header("Current State")]
    [SerializeField] private string currentTool = "";
    [SerializeField] private string currentStage = "";

    [Header("Behavior Counters")]
    [SerializeField] private int strokeCount = 0;
    [SerializeField] private int undoCount = 0;
    [SerializeField] private int consecutiveFailures = 0;

    [Header("Idle Tracking")]
    [SerializeField] private bool trackIdleTime = true;
    [SerializeField] private float idleSeconds = 0f;

    private float lastActivityTime;

    // Read-only access for other AI scripts.
    public string CurrentTool => currentTool;
    public string CurrentStage => currentStage;

    public int StrokeCount => strokeCount;
    public int UndoCount => undoCount;
    public int ConsecutiveFailures => consecutiveFailures;

    public float IdleSeconds => idleSeconds;

    private void Awake()
    {
        lastActivityTime = Time.time;
    }

    private void Update()
    {
        if (!trackIdleTime)
        {
            return;
        }

        idleSeconds = Mathf.Max(
            0f,
            Time.time - lastActivityTime
        );
    }

    // Call this whenever a meaningful sculpting stroke occurs.
    public void RecordStroke()
    {
        strokeCount++;

        RecordActivity();

        Debug.Log(
            $"MORPHIC Behavior: Stroke recorded. Total strokes = {strokeCount}"
        );
    }

    // Call this whenever the player performs an undo.
    public void RecordUndo()
    {
        undoCount++;

        RecordActivity();

        Debug.Log(
            $"MORPHIC Behavior: Undo recorded. Total undos = {undoCount}"
        );
    }

    // Call this when the application determines that an operation failed.
    public void RecordFailure()
    {
        consecutiveFailures++;

        RecordActivity();

        Debug.Log(
            "MORPHIC Behavior: Failure recorded. " +
            $"Consecutive failures = {consecutiveFailures}"
        );
    }

    // Call this when the player successfully performs an operation.
    public void RecordSuccess()
    {
        consecutiveFailures = 0;

        RecordActivity();

        Debug.Log(
            "MORPHIC Behavior: Successful operation recorded."
        );
    }

    // Change the currently active sculpting tool.
    public void SetCurrentTool(string toolName)
    {
        currentTool = toolName;

        RecordActivity();

        Debug.Log(
            "MORPHIC Behavior: Current tool = " +
            currentTool
        );
    }

    // Change the current stage of the game.
    public void SetCurrentStage(string stageName)
    {
        currentStage = stageName;

        RecordActivity();

        Debug.Log(
            "MORPHIC Behavior: Current stage = " +
            currentStage
        );
    }

    // Records meaningful player activity and resets idle time.
    public void RecordActivity()
    {
        lastActivityTime = Time.time;
        idleSeconds = 0f;
    }

    // Clears the current session's tracked values.
    public void ResetBehaviorData()
    {
        strokeCount = 0;
        undoCount = 0;
        consecutiveFailures = 0;
        idleSeconds = 0f;

        lastActivityTime = Time.time;

        Debug.Log(
            "MORPHIC Behavior: Behavior data reset."
        );
    }
}