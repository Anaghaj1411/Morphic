using System;
using UnityEngine;

/// <summary>
/// Defers an action to the next frame.
///
/// TextMeshPro builds its mesh during the layout pass, which can
/// overwrite a colour assigned in the same frame. Scheduling a
/// follow-up keeps the intended text colour.
/// </summary>
public sealed class TextMeshProUGUIRunner : MonoBehaviour
{
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
            context.gameObject.AddComponent<
                TextMeshProUGUIRunner>();

        runner.BeginNextFrame(action);
    }

    private void BeginNextFrame(Action action)
    {
        StartCoroutine(RunNextFrame(action));
    }

    private System.Collections.IEnumerator RunNextFrame(
        Action action)
    {
        yield return null;

        action();

        Destroy(this);
    }
}