using TMPro;
using UnityEngine;

public class ModeIndicatorUI : MonoBehaviour
{
    [SerializeField] private TMP_Text modeText;

    [Header("Labels")]
    [SerializeField] private string sculptModeText = "SCULPT MODE";
    [SerializeField] private string paintModeText = "PAINT MODE";

    private bool paintModeActive;

    private void Awake()
    {
        if (modeText == null)
            modeText = GetComponent<TMP_Text>();

        UpdateText();
    }

    public void ToggleMode()
    {
        paintModeActive = !paintModeActive;
        UpdateText();
    }

    private void UpdateText()
    {
        if (modeText == null)
            return;

        modeText.text = paintModeActive
            ? paintModeText
            : sculptModeText;
    }
}