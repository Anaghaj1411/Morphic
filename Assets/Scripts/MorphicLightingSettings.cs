using UnityEngine;

[CreateAssetMenu(menuName = "Morphic/Lighting Settings", fileName = "MorphicLightingSettings")]
public sealed class MorphicLightingSettings : ScriptableObject
{
    [Header("Master")]
    [Range(0f, 3f)]
    [Tooltip("Scales every interior light.")]
    public float globalBrightness = 1f;

    [Range(0f, 2f)]
    [Tooltip("0 = cooler daylight, 1 = default lamps, 2 = extra warm.")]
    public float warmth = 1f;

    [Header("Restaurant")]
    [Range(0f, 4f)] public float restaurantKey = 1f;
    [Range(0f, 4f)] public float restaurantFill = 1f;

    [Header("Pottery")]
    [Range(0f, 4f)] public float potteryShop = 1f;
    [Range(0f, 4f)] public float workbench = 1f;

    [Header("Outside")]
    [Range(0f, 4f)] public float street = 1f;

    [Header("Clouds")]
    [Range(0f, 8f)]
    [Tooltip("Brightness of the big sky light. This is what actually drives Intensity.")]
    public float clouds = 3.5f;

    [Range(40f, 250f)]
    [Tooltip("How far the cloud light reaches.")]
    public float cloudRange = 180f;

    [Header("Scene positions")]
    [Tooltip("Set automatically when you drag a light in the Scene view.")]
    public bool useSavedPositions;

    public Vector3 restaurantKeyPosition;
    public Vector3 restaurantFillPosition;
    public Vector3 potteryShopPosition;
    public Vector3 workbenchPosition;
    public Vector3 streetPosition;
    public Vector3 cloudPosition;
    public Vector3 cloudPosition2;
    public Vector3 cloudPosition3;
    public Vector3 cloudPosition4;
    public Vector3 cloudPosition5;
}
