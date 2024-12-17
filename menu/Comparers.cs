using System;
using System.Collections.Generic;

namespace nfm.menu;

public static class Comparers
{
    public static readonly IComparer<Entry> ScoreLengthAndValue = Comparer<Entry>.Create((x, y) =>
    {
        int scoreComparison = y.Score.CompareTo(x.Score);
        if (scoreComparison != 0) return scoreComparison;

        int lengthComparison = x.Length.CompareTo(y.Length);
        if (lengthComparison != 0) return lengthComparison;

        return string.Compare(x.Item.ToString(), y.Item.ToString(), StringComparison.Ordinal);
    });
    
    public static readonly IComparer<Entry> ScoreOnly = Comparer<Entry>.Create((x, y) =>
    {
        int scoreComparison = y.Score.CompareTo(x.Score);
        if (scoreComparison != 0) return scoreComparison;

        return x.Index.CompareTo(y.Index);
    });
}
