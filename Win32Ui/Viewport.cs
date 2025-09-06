using nfm.menu;

public class Viewport
{
    private int _endRow;
    private List<(object Obj, TerminalEscapedLine Text)> _items;
    public int ViewportSelectedIndex;
    public int StartLinesToClip { get; private set; }

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
    private bool _wrap;

    public int EndRow => _endRow;

    public void SetItems(List<(object Obj, TerminalEscapedLine Text)> items, bool wrap)
    {
        var previousSelectedIndex = SelectedIndex;
        var previousStartRow = StartRow;
        
        if (items.Count == 0)
        {
            SelectedIndex = 0;
            ViewportSelectedIndex = 0;
            StartRow = 0;
            _endRow = 0;
            return;
        }
        
        _wrap = wrap;
        _items = items;
        
        if (previousSelectedIndex < items.Count)
        {
            SelectedIndex = previousSelectedIndex;
            StartRow = Math.Min(previousStartRow, Math.Max(0, items.Count - _viewportRows));
        }
        else
        {
            SelectedIndex = 0;
            ViewportSelectedIndex = 0;
            StartRow = 0;
        }
        
        ReflowFromTop();
        
        ViewportSelectedIndex = SelectedIndex - StartRow;
        if (ViewportSelectedIndex < 0)
        {
            ViewportSelectedIndex = 0;
        }
    }
    
    private void ReflowFromBottom(bool stop)
    {
        var accumulatedLines = 0;
        var i = 0;
        while (accumulatedLines < _viewportRows && _endRow - i >= 0)
        {
            var item = _items[_endRow -  i];
            accumulatedLines += _wrap ? item.Text.WrappedLines().Count : item.Text.Lines.Count;
            
            i++;
        }

        if (EndRow - i + 1 <= 0 && accumulatedLines < _viewportRows && !stop)
        {
            StartRow = 0;
            ReflowFromTop();
            return;
        }
                
        StartRow = Math.Max(0, EndRow - i + 1);
        StartLinesToClip = Math.Max(0, accumulatedLines - _viewportRows);
    }

    public void SelectNext()
    {
        if (SelectedIndex + 1 < _items.Count())
        {
            if (SelectedIndex + 1 <= EndRow)
            {
                ViewportSelectedIndex++;
            }
            else
            {
                if (_endRow <= _items.Count)
                {
                    _endRow++;
                    ReflowFromBottom(true);
                    ViewportSelectedIndex = Math.Max(0, EndRow - StartRow);
                }
            }
            SelectedIndex++;
        }
    }
    
    private void ReflowFromTop()
    {
        var accumulatedLines = 0;
        var i = 0;
        while (accumulatedLines < _viewportRows && StartRow + i < _items.Count)
        {
            var item = _items[StartRow + i];
            accumulatedLines += _wrap ? item.Text.WrappedLines().Count : item.Text.Lines.Count;
            i++;
        }

        if (StartRow + i >= _items.Count)
        {
            _endRow = _items.Count - 1;
            ReflowFromBottom(true);
            return;
        }
                
        _endRow = Math.Min(_items.Count - 1, StartRow + i - 1);
        StartLinesToClip = 0;
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
            else if(StartRow > 0)
            {
                StartRow--;
                ReflowFromTop();
                ViewportSelectedIndex = 0;
            }
        }
        else
        {
            ReflowFromTop();
        }
    }

    public void PageDown()
    {
        if (_endRow + 1 < _items.Count)
        {
            StartRow = _endRow + 1;
            ReflowFromTop();
            SelectedIndex = StartRow + ViewportSelectedIndex;
        }
        else
        {
            SelectedIndex = _items.Count - 1;
            ViewportSelectedIndex = _viewportRows - 1;
        }
    }

    public void PageUp()
    {
        if (StartRow > 0)
        {
            _endRow = StartRow - 1;
            ReflowFromBottom(false);
            var numberOfRows = EndRow - StartRow;
            if (ViewportSelectedIndex > numberOfRows)
            {
                ViewportSelectedIndex = numberOfRows;
            }
            SelectedIndex = StartRow + ViewportSelectedIndex;
        }
        else
        {
            SelectedIndex = 0;
            ViewportSelectedIndex = 0;
            ReflowFromTop();
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