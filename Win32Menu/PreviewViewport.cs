using Core;

class PreviewViewport
{
    public PreviewViewport(int viewportRows)
    {
        _viewportRows = viewportRows;
    }

    private int _startRow;
    private readonly int _viewportRows;
    private List<List<TextSegment>>? _lines;
    private List<List<TextSegment>> Lines => _lines ?? throw new InvalidOperationException("Lines cannot be null");

    private int EndRow => Math.Min(_startRow + _viewportRows, Lines.Count);
    
    public void HalfPageDown()
    {
        var half = _viewportRows / 2;
        if (_startRow + half < Lines.Count && EndRow < Lines.Count)
        {
            _startRow += half;
        }
    }
    
    public void HalfPageUp()
    {
        var half = _viewportRows / 2;
        if (_startRow - half > 0)
        {
            _startRow -= half;
        }
        else
        {
            _startRow = 0;
        }
    }

    public void SetLines(List<List<TextSegment>> lines, int startLine)
    {
        _lines = lines;
        _startRow = startLine;
    }

    public List<List<TextSegment>> ViewportLines()
    {
        return Lines.GetRange(_startRow, EndRow - _startRow).ToList();
    }
}