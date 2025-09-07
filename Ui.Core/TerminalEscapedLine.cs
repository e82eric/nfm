namespace nfm.Ui.Core;

public class TerminalEscapedLine
{
    private IList<int> _pos = new List<int>();
    public List<EscapedLine> Lines { get; } = new();

    public List<EscapedLine> WrappedLines()
    {
        var result = new List<EscapedLine>();
        foreach (var line in Lines)
        {
            if (line.LineText().Length < 131)
            {
                result.Add(line);
            }
            else
            {
                foreach (var wrappedLine in line.WrapLines())
                {
                    result.Add(wrappedLine);
                }
            }
            
        }
        return result;
    }

    public void SetPos(IList<int> val)
    {
        _pos = val;
        var accumulatedLength = 0;
        foreach (var line in Lines)
        {
            var linePos = new List<int>();
            foreach (var p in _pos)
            {
                if (p >= accumulatedLength && p < accumulatedLength + line.LineText().Length + 2)
                {
                    linePos.Add(p - accumulatedLength);
                }
            }
            
            line.SetPos(linePos);
            accumulatedLength += line.LineText().Length + 2;
        }
    }

    public static TerminalEscapedLine SimpleText(string value)
    {
        var result = new TerminalEscapedLine();
        var segments = new List<TextSegment>();
        segments.Add(new TextSegment {State = new AnsiState(), Text = value});
        result.Lines.Add(new EscapedLine(segments));
        return result;
    }
    
    public override string ToString()
    {
        return string.Join("\r\n", Lines.Select(l => l.LineText()));
    }
}