namespace nfm.Ui.Core;

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