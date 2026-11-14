using TMPro;
using UnityEngine;

public sealed class MorphicLocKey : MonoBehaviour
{
    [SerializeField] private string key;

    public void SetKey(string english)
    {
        key = MorphicLoc.EnglishKey(english);
    }

    private void Awake()
    {
        MorphicLoc.Changed += Refresh;
    }

    private void OnEnable()
    {
        Refresh();
    }

    private void OnDestroy()
    {
        MorphicLoc.Changed -= Refresh;
    }

    public void Refresh()
    {
        if (string.IsNullOrEmpty(key))
            return;
        key = MorphicLoc.EnglishKey(key);
        TMP_Text label = GetComponent<TMP_Text>();
        if (label == null)
            return;
        MorphicLoc.Write(label, key);
    }
}
