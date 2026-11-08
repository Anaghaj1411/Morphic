using UnityEngine;

/// <summary>
/// Select KerisHandle in the Scene view to move, rotate, and scale it.
/// Use this dropdown to switch Handle 1 / Handle 2.
/// </summary>
[ExecuteAlways]
[DisallowMultipleComponent]
[SelectionBase]
public sealed class KerisHandleSceneEdit : MonoBehaviour
{
    public enum Variant
    {
        Handle1 = 0,
        Handle2 = 1
    }

    [Header("Which handle")]
    public Variant handle = Variant.Handle1;

    private void OnValidate()
    {
        PushToOverlay();
    }

    private void LateUpdate()
    {
        PushToOverlay();
    }

    private void PushToOverlay()
    {
        KerisHandleOverlay overlay = GetComponentInParent<KerisHandleOverlay>();
        if (overlay == null)
            return;
        overlay.useHandle2 = handle == Variant.Handle2;
    }
}
