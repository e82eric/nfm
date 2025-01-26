namespace nfm.menu;

public readonly struct Entry(object item, int length, int score, int index)
{
    public readonly object Item = item;
    public readonly int Score = score;
    public readonly int Index = index;
    public readonly int Length = length;
}

public class StringWithPos(string text, IList<int> pos)
{
    public readonly string Text = text;
    public readonly IList<int> Pos = pos;
}