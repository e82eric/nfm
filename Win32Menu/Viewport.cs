using nfm.menu;

class Viewport
{
    private int _endRow;
    private List<TerminalEscapedLine> _items;
    public int ViewportSelectedIndex;
    public int StartLinesToClip { get; set; }

    public Viewport(int viewportRows)
    {
        _viewportRows = viewportRows;
        StartRow = 0;
        _endRow = 0;
        ViewportSelectedIndex = 0;
        _totalRows = 0;
    }
    
    public int SelectedIndex;
    public int StartRow;
    private readonly int _viewportRows;
    private int _totalRows;

    public int EndRow => _endRow;

    public void SetItems(List<TerminalEscapedLine> items)
    {
        _items = items;
        var accumulatedLines = 0;
        var i = 0;
        while (accumulatedLines < _viewportRows && i < items.Count())
        {
            var item = _items[StartRow + i];
            accumulatedLines += item.Lines.Count;
            i++;
        }
                                                 
        _endRow = StartRow + i;
    }

    public void SelectNext()
    {
        if (SelectedIndex + 1 < _totalRows)
        {
            if (SelectedIndex + 1 < EndRow)
            {
                ViewportSelectedIndex++;
            }
            else
            {
                var accumulatedLines = 0;
                var i = 0;
                _endRow++;
                while (accumulatedLines < _viewportRows)
                {
                    var item = _items[(_endRow - 1) - i];
                    accumulatedLines += item.Lines.Count;
                    i++;
                }
                
                StartRow = EndRow - i;
                StartLinesToClip = Math.Max(0, accumulatedLines - _viewportRows);
                ViewportSelectedIndex = EndRow - StartRow - 1;
            }
            SelectedIndex++;
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
                var accumulatedLines = 0;
                var i = 0;
                StartRow--;
                while (accumulatedLines < _viewportRows)
                {
                    var item = _items[StartRow + i];
                    accumulatedLines += item.Lines.Count;
                    i++;
                }
                
                _endRow = StartRow + i;
                ViewportSelectedIndex = 0;
                StartLinesToClip = 0;
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