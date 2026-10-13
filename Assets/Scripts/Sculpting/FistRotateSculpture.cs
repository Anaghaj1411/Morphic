using UnityEngine;

public class FistRotateSculpture : MonoBehaviour
{
    [Header("Tracking")]
    [SerializeField] private PinchDetector pinchDetector;

    [Header("Hand Rotation")]
    [SerializeField] private float horizontalSensitivity = 90f;
    [SerializeField] private float verticalSensitivity = 70f;

    [SerializeField] private float deadZone = 0.0025f;

    [SerializeField] private float smoothing = 10f;

    [SerializeField] private bool invertHorizontal = true;
    [SerializeField] private bool invertVertical = false;

    [Header("Safety")]
    [SerializeField] private float maxRotationSpeed = 180f;

    [Header("Vertical Rotation")]
    [SerializeField] private bool allowVerticalRotation = true;
    [SerializeField] private float maxPitch = 75f;

    [Header("Mouse Rotation")]
    [SerializeField] private bool enableMouseRotation = true;
    [SerializeField] private float mouseSensitivity = 0.25f;

    private bool wasFist;

    private Vector3 previousHandPosition;

    private float yawSpeed;
    private float pitchSpeed;

    private float targetYawSpeed;
    private float targetPitchSpeed;

    private float currentPitch;

    private void Awake()
    {
        if (pinchDetector == null)
        {
            pinchDetector =
                FindFirstObjectByType<PinchDetector>();
        }

        currentPitch = 0f;
    }

    private void Update()
    {
        if (pinchDetector == null)
        {
            pinchDetector =
                FindFirstObjectByType<PinchDetector>();
        }

        bool isMouseRotating =
            enableMouseRotation &&
            (
                Input.GetMouseButton(1) ||
                (
                    Input.GetKey(KeyCode.LeftShift) &&
                    Input.GetMouseButton(0)
                )
            );

        bool isHandFistRotating =
            pinchDetector != null &&
            pinchDetector.IsTracking &&
            pinchDetector.IsFist;

        /*
         * Nothing is controlling rotation.
         */
        if (!isMouseRotating &&
            !isHandFistRotating)
        {
            StopRotation();
            return;
        }

        /*
         * -------------------------
         * MOUSE ROTATION
         * -------------------------
         */
        if (isMouseRotating)
        {
            wasFist = false;

            float mouseX =
                Input.GetAxis("Mouse X");

            float mouseY =
                Input.GetAxis("Mouse Y");

            float yaw =
                mouseX *
                mouseSensitivity;

            float pitch =
                -mouseY *
                mouseSensitivity;

            transform.Rotate(
                Vector3.up,
                yaw,
                Space.World
            );

            if (allowVerticalRotation)
            {
                Camera mainCamera =
                    Camera.main;

                Vector3 rightAxis =
                    mainCamera != null
                        ? mainCamera.transform.right
                        : Vector3.right;

                transform.Rotate(
                    rightAxis,
                    pitch,
                    Space.World
                );
            }

            return;
        }

        /*
         * -------------------------
         * FIST ROTATION
         * -------------------------
         */

        Vector3 handPosition =
            pinchDetector.SmoothedPalmCenter;

        /*
         * When fist is first detected,
         * simply remember its position.
         */
        if (!wasFist)
        {
            previousHandPosition =
                handPosition;

            targetYawSpeed = 0f;
            targetPitchSpeed = 0f;

            yawSpeed = 0f;
            pitchSpeed = 0f;

            wasFist = true;

            return;
        }

        /*
         * Frame-to-frame movement.
         */
        Vector3 movement =
            handPosition -
            previousHandPosition;

        previousHandPosition =
            handPosition;

        /*
         * Ignore tiny tracking noise.
         */
        if (Mathf.Abs(movement.x) <
            deadZone)
        {
            movement.x = 0f;
        }

        if (Mathf.Abs(movement.y) <
            deadZone)
        {
            movement.y = 0f;
        }

        /*
         * Convert movement to velocity.
         *
         * IMPORTANT:
         * movement is a per-frame change,
         * so divide by deltaTime to get
         * a stable value independent of FPS.
         */
        float deltaTime =
            Mathf.Max(
                Time.unscaledDeltaTime,
                0.0001f
            );

        /*
         * Horizontal direction is inverted
         * because the webcam view is mirrored.
         */
        float horizontalDirection =
            invertHorizontal
                ? -1f
                : 1f;

        float verticalDirection =
            invertVertical
                ? -1f
                : 1f;

        targetYawSpeed =
            movement.x *
            horizontalSensitivity *
            horizontalDirection /
            deltaTime;

        targetPitchSpeed =
            movement.y *
            verticalSensitivity *
            verticalDirection /
            deltaTime;

        /*
         * Prevent one bad tracking frame from
         * spinning the sculpture.
         */
        targetYawSpeed =
            Mathf.Clamp(
                targetYawSpeed,
                -maxRotationSpeed,
                maxRotationSpeed
            );

        targetPitchSpeed =
            Mathf.Clamp(
                targetPitchSpeed,
                -maxRotationSpeed,
                maxRotationSpeed
            );

        /*
         * Smooth the rotation speed.
         */
        float smoothingAmount =
            1f -
            Mathf.Exp(
                -smoothing *
                deltaTime
            );

        yawSpeed =
            Mathf.Lerp(
                yawSpeed,
                targetYawSpeed,
                smoothingAmount
            );

        pitchSpeed =
            Mathf.Lerp(
                pitchSpeed,
                targetPitchSpeed,
                smoothingAmount
            );

        /*
         * -------------------------
         * HORIZONTAL ROTATION
         * -------------------------
         */
        float yawAmount =
            yawSpeed *
            deltaTime;

        transform.Rotate(
            Vector3.up,
            yawAmount,
            Space.World
        );

        /*
         * -------------------------
         * VERTICAL ROTATION
         * -------------------------
         */
        if (allowVerticalRotation)
        {
            float pitchAmount =
                pitchSpeed *
                deltaTime;

            float newPitch =
                Mathf.Clamp(
                    currentPitch +
                    pitchAmount,
                    -maxPitch,
                    maxPitch
                );

            float actualPitch =
                newPitch -
                currentPitch;

            currentPitch =
                newPitch;

            Camera mainCamera =
                Camera.main;

            Vector3 rightAxis =
                mainCamera != null
                    ? mainCamera.transform.right
                    : Vector3.right;

            transform.Rotate(
                rightAxis,
                actualPitch,
                Space.World
            );
        }
    }

    private void StopRotation()
    {
        wasFist = false;

        targetYawSpeed = 0f;
        targetPitchSpeed = 0f;

        float deltaTime =
            Mathf.Max(
                Time.unscaledDeltaTime,
                0.0001f
            );

        float smoothingAmount =
            1f -
            Mathf.Exp(
                -smoothing *
                deltaTime
            );

        yawSpeed =
            Mathf.Lerp(
                yawSpeed,
                0f,
                smoothingAmount
            );

        pitchSpeed =
            Mathf.Lerp(
                pitchSpeed,
                0f,
                smoothingAmount
            );
    }
}