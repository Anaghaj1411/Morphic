using UnityEngine;
using UnityEngine.InputSystem;

public class BasicSculptController : MonoBehaviour
{
    [Header("Control Speeds")]
    [SerializeField] private float scaleSpeed = 1f;
    [SerializeField] private float rotationSpeed = 80f;

    [Header("Scale Limits")]
    [SerializeField] private float minimumScale = 0.5f;
    [SerializeField] private float maximumScale = 4f;

    private Vector3 startingScale;
    private Quaternion startingRotation;

    void Start()
    {
        startingScale = transform.localScale;
        startingRotation = transform.rotation;
    }

    void Update()
    {
        Keyboard keyboard = Keyboard.current;

        if (keyboard == null)
            return;

        if (keyboard.aKey.isPressed)
            ChangeWidth(-scaleSpeed * Time.deltaTime);

        if (keyboard.dKey.isPressed)
            ChangeWidth(scaleSpeed * Time.deltaTime);

        if (keyboard.sKey.isPressed)
            ChangeHeight(-scaleSpeed * Time.deltaTime);

        if (keyboard.wKey.isPressed)
            ChangeHeight(scaleSpeed * Time.deltaTime);

        if (keyboard.qKey.isPressed)
            RotateSculpture(-rotationSpeed * Time.deltaTime);

        if (keyboard.eKey.isPressed)
            RotateSculpture(rotationSpeed * Time.deltaTime);

        if (keyboard.rKey.wasPressedThisFrame)
            ResetSculpture();
    }

    public void ChangeWidth(float amount)
    {
        Vector3 newScale = transform.localScale;
        newScale.x = Mathf.Clamp(newScale.x + amount, minimumScale, maximumScale);
        transform.localScale = newScale;
    }

    public void ChangeHeight(float amount)
    {
        Vector3 newScale = transform.localScale;
        newScale.y = Mathf.Clamp(newScale.y + amount, minimumScale, maximumScale);
        transform.localScale = newScale;
    }

    public void RotateSculpture(float amount)
    {
        transform.Rotate(0f, amount, 0f, Space.World);
    }

    public void ResetSculpture()
    {
        transform.localScale = startingScale;
        transform.rotation = startingRotation;
    }
}