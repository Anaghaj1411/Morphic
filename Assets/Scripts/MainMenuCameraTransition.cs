using UnityEngine;
using System.Collections;

public class MainMenuCameraTransition : MonoBehaviour
{
    [Header("Destination")]
    public Transform gameplayCameraTarget;

    [Header("Transition")]
    public float duration = 2.0f;

    private bool isMoving = false;

    public void MoveToGameplay()
    {
        if (!isMoving)
            StartCoroutine(MoveCamera());
    }

    private IEnumerator MoveCamera()
    {
        isMoving = true;

        Vector3 startPosition = transform.position;
        Quaternion startRotation = transform.rotation;

        Vector3 targetPosition = gameplayCameraTarget.position;
        Quaternion targetRotation = gameplayCameraTarget.rotation;

        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;

            float t = Mathf.Clamp01(elapsed / duration);

            // Smooth cinematic movement
            t = t * t * (3f - 2f * t);

            transform.position = Vector3.Lerp(
                startPosition,
                targetPosition,
                t
            );

            transform.rotation = Quaternion.Slerp(
                startRotation,
                targetRotation,
                t
            );

            yield return null;
        }

        transform.position = targetPosition;
        transform.rotation = targetRotation;

        isMoving = false;
    }
}