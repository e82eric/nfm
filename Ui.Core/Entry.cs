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

    public IReadOnlyList<EscapedLine> GetLinesToRender(bool wrap, int max)
    {
        return (wrap ? WrappedLines() : Lines)
            .Take(max)
            .ToList();
    }

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
    
    // New overload to return wrapped lines at 131 chars
    public List<EscapedLine> WrapLines(int maxLength = 131)
    {
        var wrappedLines = new List<EscapedLine>();
        var currentSegments = new List<TextSegment>();
        var currentPos = new List<int>();
        int currentLength = 0;
        int accumulatedLength = 0;

        foreach (var segment in Segments)
        {
            int segmentOffset = 0;
            while (segmentOffset < segment.Text.Length)
            {
                int remainingSpace = maxLength - currentLength;
                int chunkSize = Math.Min(segment.Text.Length - segmentOffset, remainingSpace);

                var chunkText = segment.Text.Substring(segmentOffset, chunkSize);
                var chunkSegment = new TextSegment
                {
                    Text = chunkText,
                    State = segment.State
                };

                currentSegments.Add(chunkSegment);

                currentLength += chunkSize;
                segmentOffset += chunkSize;

                // If the line is full, finalize it and start a new one
                if (currentLength == maxLength)
                {
                    wrappedLines.Add(new EscapedLine(new List<TextSegment>(currentSegments)) { Pos = new List<int>(currentPos) });
                    currentSegments.Clear();
                    currentPos.Clear();
                    currentLength = 0;
                    accumulatedLength += chunkSize;
                }
            }
        }

        // Add any remaining text as the final line
        if (currentSegments.Count > 0)
        {
            wrappedLines.Add(new EscapedLine(currentSegments) { Pos = currentPos });
        }
        
        var aLength = 0;
        foreach (var line in wrappedLines)
        {
            var linePos = new List<int>();
            foreach (var p in Pos)
            {
                if (p >= aLength && p < aLength + line.LineText().Length)
                {
                    linePos.Add(p - aLength);
                }
            }
            
            line.SetPos(linePos);
            aLength += line.LineText().Length;
        }

        return wrappedLines;
    }
}