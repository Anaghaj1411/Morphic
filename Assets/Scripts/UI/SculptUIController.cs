using UnityEngine;

public class SculptUIController : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private PinchSculptBrush sculptBrush;
    [SerializeField] private PinchDetector pinchDetector;

    [Header("Strength Control")]
    [SerializeField] private float strengthStep = 0.05f;

    [Header("Panel")]
    [SerializeField] private bool showSidePanel = true;
    [SerializeField] private Vector2 panelPosition =
        new Vector2(15f, 15f);

    private Vector2 panelSize =
        new Vector2(145f, 335f);

    private GUIStyle titleStyle;
    private GUIStyle buttonStyle;
    private GUIStyle labelStyle;
    private GUIStyle statusStyle;

    private void Awake()
    {
        if (sculptBrush == null)
        {
            sculptBrush =
                FindFirstObjectByType<PinchSculptBrush>();
        }

        if (pinchDetector == null)
        {
            pinchDetector =
                FindFirstObjectByType<PinchDetector>();
        }
    }

    private void Update()
    {
        if (sculptBrush == null)
        {
            sculptBrush =
                FindFirstObjectByType<PinchSculptBrush>();
        }

        if (pinchDetector == null)
        {
            pinchDetector =
                FindFirstObjectByType<PinchDetector>();
        }
    }

    private void OnGUI()
    {
        if (!showSidePanel ||
            sculptBrush == null ||
            (ParametricSkyLantern.Active != null &&
             ParametricSkyLantern.Active.GuidedModeEnabled))
        {
            return;
        }

        CreateStyles();

        panelSize =
            new Vector2(145f, 430f);

        Rect panelRect =
            new Rect(
                panelPosition.x,
                panelPosition.y,
                panelSize.x,
                panelSize.y
            );

        GUI.Box(
            panelRect,
            GUIContent.none
        );

        GUILayout.BeginArea(
            new Rect(
                panelRect.x + 8f,
                panelRect.y + 8f,
                panelRect.width - 16f,
                panelRect.height - 16f
            )
        );

        DrawTitle();

        GUILayout.Space(5f);

        DrawToolButtons();

        GUILayout.Space(5f);

        DrawStrengthControls();

        GUILayout.Space(5f);

        DrawBrushSizeControls();

        GUILayout.Space(5f);

        DrawSymmetryControl();

        GUILayout.Space(5f);

        DrawResetButton();

        GUILayout.Space(7f);

        DrawStatus();

        GUILayout.EndArea();
    }

    // ============================================================
    // STYLES
    // ============================================================

    private void CreateStyles()
    {
        if (titleStyle != null)
        {
            return;
        }

        titleStyle =
            new GUIStyle(GUI.skin.label);

        titleStyle.fontSize = 15;
        titleStyle.fontStyle =
            FontStyle.Bold;

        titleStyle.alignment =
            TextAnchor.MiddleCenter;

        buttonStyle =
            new GUIStyle(GUI.skin.button);

        buttonStyle.fontSize = 11;

        labelStyle =
            new GUIStyle(GUI.skin.label);

        labelStyle.fontSize = 11;

        labelStyle.alignment =
            TextAnchor.MiddleCenter;

        statusStyle =
            new GUIStyle(GUI.skin.label);

        statusStyle.fontSize = 10;

        statusStyle.alignment =
            TextAnchor.MiddleCenter;

        statusStyle.wordWrap = true;
    }

    // ============================================================
    // TITLE
    // ============================================================

    private void DrawTitle()
    {
        GUILayout.Label(
            "SCULPT TOOLS",
            titleStyle,
            GUILayout.Height(25f)
        );
    }

    // ============================================================
    // TOOL BUTTONS
    // ============================================================

    private void DrawToolButtons()
    {
        DrawToolButton(
            "Inflate",
            SculptToolMode.Inflate
        );

        DrawToolButton(
            "Indent",
            SculptToolMode.Indent
        );

        DrawToolButton(
            "Smooth",
            SculptToolMode.Smooth
        );

        DrawToolButton(
            "Grab",
            SculptToolMode.Grab
        );

        DrawToolButton(
            "Flatten",
            SculptToolMode.Flatten
        );

        DrawToolButton(
            "Crease",
            SculptToolMode.Crease
        );

        DrawToolButton(
            "Stretch",
            SculptToolMode.Stretch
        );
    }

    private void DrawToolButton(
        string buttonText,
        SculptToolMode mode
    )
    {
        bool isSelected =
            sculptBrush.CurrentSculptMode == mode;

        GUIStyle style =
            new GUIStyle(buttonStyle);

        if (isSelected)
        {
            style.fontStyle =
                FontStyle.Bold;
        }

        if (GUILayout.Button(
            buttonText,
            style,
            GUILayout.Height(28f)
        ))
        {
            sculptBrush.SetSculptMode(mode);
        }
    }

    // ============================================================
    // STRENGTH CONTROL
    // ============================================================

    private void DrawStrengthControls()
    {
        GUILayout.Label(
            "Strength",
            labelStyle,
            GUILayout.Height(18f)
        );

        float currentStrength =
            sculptBrush.GetCurrentToolStrength();

        GUILayout.BeginHorizontal();

        if (GUILayout.Button(
            "-",
            buttonStyle,
            GUILayout.Width(32f),
            GUILayout.Height(27f)
        ))
        {
            sculptBrush.AdjustCurrentToolStrength(
                -strengthStep
            );
        }

        GUILayout.Label(
            Mathf.RoundToInt(
                currentStrength * 100f
            ) + "%",
            labelStyle,
            GUILayout.Height(27f)
        );

        if (GUILayout.Button(
            "+",
            buttonStyle,
            GUILayout.Width(32f),
            GUILayout.Height(27f)
        ))
        {
            sculptBrush.AdjustCurrentToolStrength(
                strengthStep
            );
        }

        GUILayout.EndHorizontal();
    }

    // ============================================================
    // BRUSH SIZE
    // ============================================================

    private void DrawBrushSizeControls()
    {
        GUILayout.Label(
            "Brush Size",
            labelStyle,
            GUILayout.Height(18f)
        );

        GUILayout.BeginHorizontal();

        if (GUILayout.Button(
            "-",
            buttonStyle,
            GUILayout.Width(32f),
            GUILayout.Height(27f)
        ))
        {
            sculptBrush.AdjustBrushRadius(
                -0.05f
            );
        }

        GUILayout.Label(
            sculptBrush.BrushRadius.ToString(
                "0.00"
            ),
            labelStyle,
            GUILayout.Height(27f)
        );

        if (GUILayout.Button(
            "+",
            buttonStyle,
            GUILayout.Width(32f),
            GUILayout.Height(27f)
        ))
        {
            sculptBrush.AdjustBrushRadius(
                0.05f
            );
        }

        GUILayout.EndHorizontal();
    }

    // ============================================================
    // SYMMETRY
    // ============================================================

    private void DrawSymmetryControl()
    {
        string symmetryText =
            sculptBrush.EnableSymmetry
                ? "Symmetry: ON"
                : "Symmetry: OFF";

        if (GUILayout.Button(
            symmetryText,
            buttonStyle,
            GUILayout.Height(27f)
        ))
        {
            sculptBrush.ToggleSymmetry();
        }
    }

    // ============================================================
    // RESET
    // ============================================================

    private void DrawResetButton()
    {
        if (GUILayout.Button(
            "Reset Model",
            buttonStyle,
            GUILayout.Height(28f)
        ))
        {
            sculptBrush.ResetMesh();
        }
    }

    // ============================================================
    // STATUS
    // ============================================================

    private void DrawStatus()
    {
        bool tracking =
            pinchDetector != null &&
            pinchDetector.IsTracking;

        bool fist =
            pinchDetector != null &&
            pinchDetector.IsFist;

        bool pinching =
            pinchDetector != null &&
            pinchDetector.IsPinching;

        float fistDist =
            pinchDetector != null ? pinchDetector.FistDistance : 0f;

        string statusText;

        if (fist)
        {
            statusText = $"FIST ROTATING\n(Curl: {fistDist:F2})";
        }
        else if (pinching)
        {
            statusText = "SCULPTING (PINCH)";
        }
        else if (tracking)
        {
            statusText = $"TRACKING HAND\n(Curl: {fistDist:F2})";
        }
        else
        {
            statusText = "NO HAND DETECTED";
        }

        GUILayout.Label(
            statusText,
            statusStyle,
            GUILayout.Height(35f)
        );
    }
}