using UnityEngine;

[ExecuteAlways]
public class MainMenuCloudMover : MonoBehaviour
{
    [Header("Color")]
    [Tooltip("Tint for this cloud. Uses a real Cloud material, not Unity's default Lit.")]
    [SerializeField] private Color cloudColor = new Color(0.92f, 0.95f, 1f, 1f);

    [SerializeField]
    [Range(0f, 2f)]
    [Tooltip("How much the cloud glows so the color shows even when the scene sun is dim.")]
    private float glow = 0.55f;

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
    private Material cloudMaterial;
    private Material runtimeMaterial;
    private Color lastColor;
    private float lastGlow = float.NaN;

    private void OnEnable()
    {
        EnsureCloudMaterial();
        ApplyColor();
    }

    private void Start()
    {
        startPosition = transform.position;

        if (driftDirection.sqrMagnitude < 0.0001f)
        {
            driftDirection = Vector3.right;
        }

        normalizedDirection = driftDirection.normalized;

        randomOffset = Random.Range(0f, loopDistance);
        EnsureCloudMaterial();
        ApplyColor();
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        EnsureCloudMaterial();
        ApplyColor();
    }
#endif

    private void Update()
    {
        if (cloudColor != lastColor || !Mathf.Approximately(glow, lastGlow))
            ApplyColor();

        if (!Application.isPlaying)
            return;

        float time = Time.time + randomOffset;

        float travelOffset =
            Mathf.Repeat(time * moveSpeed, loopDistance)
            - (loopDistance * 0.5f);

        Vector3 horizontalDrift = normalizedDirection * travelOffset;
        float verticalBob = Mathf.Sin(time * bobSpeed) * bobAmount;

        transform.position =
            startPosition
            + horizontalDrift
            + Vector3.up * verticalBob;
    }

    private void EnsureCloudMaterial()
    {
        Renderer rend = GetComponent<Renderer>();
        if (rend == null)
            return;

        if (cloudMaterial == null)
            cloudMaterial = Resources.Load<Material>("Cloud");

        if (cloudMaterial == null)
            return;

        if (Application.isPlaying)
        {
            if (runtimeMaterial == null)
            {
                runtimeMaterial = new Material(cloudMaterial);
                rend.material = runtimeMaterial;
            }
        }
        else if (rend.sharedMaterial != cloudMaterial)
        {
            rend.sharedMaterial = cloudMaterial;
        }
    }

    private void ApplyColor()
    {
        Renderer rend = GetComponent<Renderer>();
        if (rend == null)
            return;

        Material mat = Application.isPlaying ? runtimeMaterial : rend.sharedMaterial;
        if (mat == null)
            return;

        mat.SetColor("_BaseColor", cloudColor);
        mat.SetColor("_Color", cloudColor);
        mat.EnableKeyword("_EMISSION");
        mat.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
        mat.SetColor("_EmissionColor", cloudColor * glow);
        lastColor = cloudColor;
        lastGlow = glow;
    }

    private void OnDestroy()
    {
        if (runtimeMaterial != null)
            Destroy(runtimeMaterial);
    }
}
