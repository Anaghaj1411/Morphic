using System;
using System.Collections.Generic;
using UnityEngine;

public static class TeapotCollectionStore
{
    public const int MaxCount = 9;
    private const string PrefsKey = "Morphic.TeapotShelf.v1";

    [Serializable]
    public sealed class Record
    {
        public string Id;
        public float[] BlendWeights;
        public Color[] MaterialColors;
        public float[] LocalRotation;
        public bool StampVisible;
        public Color StampTint;
    }

    private static readonly List<Record> records = new List<Record>();

    public static IReadOnlyList<Record> All => records;
    public static int Count => records.Count;
    public static bool IsFull => records.Count >= MaxCount;

    public static void Clear()
    {
        records.Clear();
        if (PlayerPrefs.HasKey(PrefsKey))
        {
            PlayerPrefs.DeleteKey(PrefsKey);
            PlayerPrefs.Save();
        }
    }

    public static bool Add(Record record)
    {
        if (record == null || IsFull)
            return false;
        records.Add(record);
        return true;
    }

    public static void RemoveLast()
    {
        if (records.Count > 0)
            records.RemoveAt(records.Count - 1);
    }
}
