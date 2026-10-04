using UnityEngine;

public class MainMenuCloudMover : MonoBehaviour
{
    [Header("Movement")]
    [SerializeField] private float moveSpeed = 0.15f;

    [Tooltip("World-space horizontal drift direction. Y should normally stay 0.")]
    [SerializeField] private Vector3 driftDirection = Vector3.right;

    [Header("Vertical Bob")]
    [SerializeField] private float bobAmount = 0.03f;
    [SerializeField] private float bobSpeed = 0.4f;

    [Header("Loop")]
    [SerializeField] private float loopDistance = 20f;

    private Vector3 startPosition;
    private Vector3 normalizedDirection;
    private float randomOffset;

    private void Start()
    {
        startPosition = transform.position;

        if (driftDirection.sqrMagnitude < 0.0001f)
        {
            driftDirection = Vector3.right;
        }

        normalizedDirection = driftDirection.normalized;

        // Gives each cloud a different starting point in its cycle.
        randomOffset = Random.Range(0f, loopDistance);
    }

    private void Update()
    {
        float time = Time.time + randomOffset;

        // Repeats the cloud's movement cycle indefinitely.
        float travelOffset =
            Mathf.Repeat(time * moveSpeed, loopDistance)
            - (loopDistance * 0.5f);

        Vector3 horizontalDrift =
            normalizedDirection * travelOffset;

        float verticalBob =
            Mathf.Sin(time * bobSpeed) * bobAmount;

        transform.position =
            startPosition
            + horizontalDrift
            + Vector3.up * verticalBob;
    }
}