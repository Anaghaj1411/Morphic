using UnityEngine;

public class ClayColorUIController : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private ClayColorPainter colorPainter;
    [SerializeField] private PinchSculptBrush sculptBrush;

    private bool paintModeActive = false;
    private bool originalSculptBrushState = true;

    private void Start()
    {
        if (sculptBrush != null)
        {
            originalSculptBrushState = sculptBrush.enabled;
        }

        if (colorPainter != null)
        {
            colorPainter.SetPaintMode(false);
        }
    }

    public void SetRed()
    {
        if (colorPainter != null)
            colorPainter.SetPaintColor(Color.red);
    }

    public void SetBlue()
    {
        if (colorPainter != null)
            colorPainter.SetPaintColor(Color.blue);
    }

    public void SetYellow()
    {
        if (colorPainter != null)
            colorPainter.SetPaintColor(Color.yellow);
    }

    public void SetPink()
    {
        if (colorPainter != null)
            colorPainter.SetPaintColor(
                new Color(1f, 0.4f, 0.7f)
            );
    }

    public void SetGreen()
    {
        if (colorPainter != null)
            colorPainter.SetPaintColor(Color.green);
    }

    public void SetPurple()
    {
        if (colorPainter != null)
            colorPainter.SetPaintColor(
                new Color(0.6f, 0.2f, 1f)
            );
    }

    public void TogglePaintMode()
    {
        paintModeActive = !paintModeActive;

        // Tell the color painter whether painting is active.
        if (colorPainter != null)
        {
            colorPainter.SetPaintMode(paintModeActive);
        }

        // Disable normal sculpting while painting.
        if (sculptBrush != null)
        {
            if (paintModeActive)
            {
                sculptBrush.enabled = false;
            }
            else
            {
                sculptBrush.enabled = originalSculptBrushState;
            }
        }
    }
}