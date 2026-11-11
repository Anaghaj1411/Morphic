using UnityEngine;

public sealed class MorphicShelfSlotMarker : MonoBehaviour
{
#if UNITY_EDITOR
    private void OnDrawGizmos()
    {
        bool teapot = name.StartsWith("TeapotShelfSlot", System.StringComparison.Ordinal);
        bool keris = name.StartsWith("KerisShelfSlot", System.StringComparison.Ordinal);
        if (!teapot && !keris)
            return;

        Gizmos.color = teapot
            ? new Color(0.15f, 0.72f, 1f, 0.95f)
            : new Color(1f, 0.52f, 0.12f, 0.95f);
        Gizmos.DrawWireSphere(transform.position, 0.07f);
        Gizmos.DrawLine(transform.position, transform.position + Vector3.up * 0.14f);
    }
#endif

    public static void EnsureOn(Transform slot)
    {
#if UNITY_EDITOR
        if (slot == null)
            return;
        if (slot.GetComponent<MorphicShelfSlotMarker>() == null)
            slot.gameObject.AddComponent<MorphicShelfSlotMarker>();
#endif
    }
}
