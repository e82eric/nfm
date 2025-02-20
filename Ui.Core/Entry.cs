using Core;

namespace nfm.menu;

public readonly struct Entry(object item, int length, int score, int index)
{
    public readonly object Item = item;
    public readonly int Score = score;
    public readonly int Index = index;
    public readonly int Length = length;
}

public class TerminalEscapedLine
{
    private IList<int> _pos = new List<int>();
    public List<EscapedLine> Lines { get; } = new();

    public void SetPos(IList<int> val)
    {
        _pos = val;
        var accumulatedLength = 0;
        foreach (var line in Lines)
        {
            var linePos = new List<int>();
            foreach (var p in _pos)
            {
                if (p >= accumulatedLength && p < accumulatedLength + line.LineText().Length)
                {
                    linePos.Add(p - accumulatedLength);
                }
            }
            
            line.SetPos(linePos);
            accumulatedLength += line.LineText().Length;
        }
    }
    
    public override string ToString()
    {
        return string.Join("\r\n", Lines.Select(l => l.LineText()));
    }
}

public class EscapedLine
{
    private readonly string _parsedText;
    public IList<int> Pos { get; private set; }

    public EscapedLine(List<TextSegment> segments)
    {
        Pos = new List<int>();
        Segments = segments;
        _parsedText = string.Concat(Segments.Select(s => s.Text));
    }

    public void SetPos(IList<int> val)
    {
        Pos = val;
    }
    
    public readonly List<TextSegment> Segments;
    
    public string LineText()
    {
        return _parsedText;
    }
}

public class Line(string text, List<TextSegment> segments, IList<int> pos)
{
    public readonly string Text = text;
    public readonly List<TextSegment> Segments = segments;
    public readonly IList<int> Pos = pos;
}