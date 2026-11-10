using System;
using System.Collections;
using UnityEngine;

/// <summary>
/// Defers an action to the next active frame.
///
/// TextMeshPro builds its mesh during the layout pass, which can
/// overwrite a colour assigned in the same frame. Scheduling a
/// follow-up keeps the intended text colour.
/// </summary>
public sealed class TextMeshProUGUIRunner : MonoBehaviour
{
    private Action pending;

    public static void Run(
        Component context,
        Action action)
    {
        if (context == null ||
            action == null)
        {
            return;
        }

        TextMeshProUGUIRunner runner =
            context.GetComponent<TextMeshProUGUIRunner>();
        if (runner == null)
            runner = context.gameObject.AddComponent<TextMeshProUGUIRunner>();

        runner.pending = action;
        runner.TryStart();
    }

    private void OnEnable()
    {
        TryStart();
    }

    private void TryStart()
    {
        if (pending == null ||
            !isActiveAndEnabled)
        {
            return;
        }

        StopAllCoroutines();
        StartCoroutine(RunNextFrame());
    }

    private IEnumerator RunNextFrame()
    {
        Action action = pending;
        pending = null;

        yield return null;

        if (action != null)
            action();

        Destroy(this);
    }
}
