class PreviewViewport
{
    public int ViewportSelectedIndex;

    public PreviewViewport(int viewportRows)
    {
        _viewportRows = viewportRows;
        StartRow = 0;
        ViewportSelectedIndex = 0;
    }
    
    public int StartRow;
    private readonly int _viewportRows;
    private List<string>? _lines;
    private List<string> Lines => _lines ?? throw new InvalidOperationException("Lines cannot be null");

    public int EndRow => Math.Min(StartRow + _viewportRows, Lines.Count);
    
    public void HalfPageDown()
    {
        var half = _viewportRows / 2;
        if (StartRow + half < Lines.Count && EndRow < Lines.Count)
        {
            StartRow += half;
        }
    }
    
    public void HalfPageUp()
    {
        var half = _viewportRows / 2;
        if (StartRow - half > 0)
        {
            StartRow -= half;
        }
        else
        {
            StartRow = 0;
        }
    }

    private void Reset()
    {
        StartRow = 0;
    }

    public void SetLines(List<string> lines)
    {
        _lines = lines;
        Reset();
    }

    public List<string> ViewportLines()
    {
        return Lines.GetRange(StartRow, EndRow - StartRow).ToList();
    }
}
class Viewport
{
    public int ViewportSelectedIndex;

    public Viewport(int viewportRows)
    {
        _viewportRows = viewportRows;
        StartRow = 0;
        ViewportSelectedIndex = 0;
        _totalRows = 0;
    }
    
    public int SelectedIndex;
    public int StartRow;
    private readonly int _viewportRows;
    private int _totalRows;

    public int EndRow => Math.Min(StartRow + _viewportRows, _totalRows);

    public void SelectNext()
    {
        if (SelectedIndex + 1 < _totalRows)
        {
            SelectedIndex++;

            if (ViewportSelectedIndex + 1 < _viewportRows)
            {
                ViewportSelectedIndex++;
            }
            else
            {
                StartRow++;
            }
        }
    }

    public void SelectPrevious()
    {
        if (SelectedIndex > 0)
        {
            SelectedIndex--;

            if (ViewportSelectedIndex > 0)
            {
                ViewportSelectedIndex--;
            }
            else
            {
                StartRow--;
            }
        }
    }

    public void SelectHalfPageDown()
    {
        var half = _viewportRows / 2;
        if (SelectedIndex + half < _totalRows)
        {
            SelectedIndex += half;

            if (ViewportSelectedIndex + half < _viewportRows)
            {
                ViewportSelectedIndex += half;
            }
            else
            {
                StartRow += half;
            }
        }
        else
        {
            SelectedIndex = _totalRows - 1;
            ViewportSelectedIndex = _totalRows - 1;
        }
    }
    
    public void HalfPageDown()
    {
        var half = _viewportRows / 2;
        if (StartRow + half < _totalRows && EndRow < _totalRows)
        {
            StartRow += half;
        }
    }
    
    public void HalfPageUp()
    {
        var half = _viewportRows / 2;
        if (StartRow - half > 0)
        {
            StartRow -= half;
        }
        else
        {
            StartRow = 0;
        }
    }

    public void Reset()
    {
        StartRow = 0;
    }

    public void SelectHalfPageUp()
    {
        var half = _viewportRows / 2;
        if (SelectedIndex - half > 0)
        {
            SelectedIndex -= half;

            if (ViewportSelectedIndex - half > 0)
            {
                ViewportSelectedIndex -= half;
            }
            else
            {
                StartRow -= half;
            }
        }
        else
        {
            SelectedIndex = 0;
            ViewportSelectedIndex = 0;
        }
    }

    public void SetTotalRows(int itemsCount)
    {
        _totalRows = itemsCount;
        if (ViewportSelectedIndex > _totalRows)
        {
            ViewportSelectedIndex = Math.Max(0, _totalRows - 1);
            SelectedIndex = Math.Max(0, _totalRows - 1);
            StartRow = 0;
        }
    }
}