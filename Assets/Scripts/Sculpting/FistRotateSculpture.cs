using UnityEngine;

public class FistRotateSculpture : MonoBehaviour
{
    [SerializeField] private PinchDetector pinchDetector;

    [Header("Continuous Rotation")]
    [SerializeField] private float maximumRotationSpeed = 70f;

    [Range(0f, 0.4f)]
    [SerializeField] private float centerDeadZone = 0.18f;

    [Header("Smoothing")]
    [SerializeField] private float rotationSmoothingSpeed = 10f;

    private float currentHorizontalVelocity;
    private float currentVerticalVelocity;

    private void Awake()
    {
        FindPinchDetector();
    }

    private void Update()
    {
        FindPinchDetector();

        float targetHorizontalInput = 0f;
        float targetVerticalInput = 0f;

        if (pinchDetector != null &&
            pinchDetector.IsTracking &&
            pinchDetector.IsFist)
        {
            Vector3 handPosition = pinchDetector.SmoothedIndexTip;

            float horizontalOffset = (handPosition.x - 0.5f) * 2f;
            float verticalOffset = (handPosition.y - 0.5f) * 2f;

            targetHorizontalInput = GetRotationInput(horizontalOffset);
            targetVerticalInput = GetRotationInput(verticalOffset);
        }

        float smoothFactor = 1f - Mathf.Exp(-rotationSmoothingSpeed * Time.deltaTime);

        currentHorizontalVelocity = Mathf.Lerp(
            currentHorizontalVelocity,
            targetHorizontalInput * maximumRotationSpeed,
            smoothFactor
        );

        currentVerticalVelocity = Mathf.Lerp(
            currentVerticalVelocity,
            targetVerticalInput * maximumRotationSpeed,
            smoothFactor
        );

        if (Mathf.Abs(currentHorizontalVelocity) > 0.01f)
        {
            transform.Rotate(
                Vector3.up,
                -currentHorizontalVelocity * Time.deltaTime,
                Space.World
            );
        }

        if (Mathf.Abs(currentVerticalVelocity) > 0.01f)
        {
            Camera mainCamera = Camera.main;
            Vector3 verticalRotationAxis = mainCamera != null
                ? mainCamera.transform.right
                : Vector3.right;

            transform.Rotate(
                verticalRotationAxis,
                currentVerticalVelocity * Time.deltaTime,
                Space.World
            );
        }
    }

    private float GetRotationInput(float offset)
    {
        float absoluteOffset = Mathf.Abs(offset);

        if (absoluteOffset <= centerDeadZone)
        {
            return 0f;
        }

        float linearInput = (absoluteOffset - centerDeadZone) / (1f - centerDeadZone);
        float curvedInput = linearInput * linearInput;

        return curvedInput * Mathf.Sign(offset);
    }

    private void FindPinchDetector()
    {
        if (pinchDetector == null)
        {
            pinchDetector = FindFirstObjectByType<PinchDetector>();
        }
    }
}