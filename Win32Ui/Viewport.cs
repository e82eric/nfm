using System.Diagnostics.CodeAnalysis;
using nfm.Ui.Core;

namespace nfm.Win32Ui;

public class Viewport
{
    private readonly object _sync = new();
    private int _endRow;
    private List<(object Obj, TerminalEscapedLine Text)>? _items;

    public int ViewportSelectedIndex
    {
        get { lock(_sync) {return _viewportSelectedIndex;} }
    }

    public int StartLinesToClip
    {
        get { lock(_sync) {return _startLinesToClip;} }
    }

    public Viewport(int viewportRows)
    {
        _viewportRows = viewportRows;
        _startRow = 0;
        _endRow = 0;
        _viewportSelectedIndex = 0;
        _totalRows = 0;
    }
    
    private List<(object Obj, TerminalEscapedLine Text)> Items => _items ?? throw new InvalidOperationException("Lines cannot be null");

    public int SelectedIndex
    {
        get { lock(_sync) {return _selectedIndex;} }
    }

    public int StartRow
    {
        get { lock(_sync) {return _startRow;} }
    }

    private readonly int _viewportRows;
    private int _totalRows;
    private bool _wrap;
    private int _viewportSelectedIndex;
    private int _startLinesToClip;
    private int _selectedIndex;
    private int _startRow;

    public int EndRow
    {
        get { lock(_sync) {return _endRow;} }
    }

    public void Reset()
    {
        lock (_sync)
        {
            _selectedIndex = 0;
            _startRow = 0;
        }
    }

    public void SetItems(List<(object Obj, TerminalEscapedLine Text)> items, bool wrap)
    {
        lock (_sync)
        {
            var previousSelectedIndex = _selectedIndex;
            var previousStartRow = _startRow;
        
            _items = items;
            if (items.Count == 0)
            {
                _selectedIndex = 0;
                _viewportSelectedIndex = 0;
                _startRow = 0;
                _endRow = 0;
                return;
            }
        
            _wrap = wrap;
        
            if (previousSelectedIndex < items.Count)
            {
                _selectedIndex = previousSelectedIndex;
                _startRow = Math.Min(previousStartRow, Math.Max(0, items.Count - _viewportRows));
            }
            else
            {
                _selectedIndex = 0;
                _viewportSelectedIndex = 0;
                _startRow = 0;
            }
        
            ReflowFromTop();
        
            _viewportSelectedIndex = _selectedIndex - _startRow;
            if (_viewportSelectedIndex < 0)
            {
                _viewportSelectedIndex = 0;
            }
        }
    }
    
    public bool TryGetSelectedItem([NotNullWhen(true)]out object? result)
    {
        lock (_sync)
        {
            result = null;

            var target = _startRow + _viewportSelectedIndex;
            if (target < 0 || target >= Items.Count)
            {
                return false;
            }

            result = Items[target].Obj;
            return true;
        }
    }

    public List<TerminalEscapedLine> GetVisibleItems()
    {
        lock (_sync)
        {
            var items = new List<TerminalEscapedLine>();
            for (var i = _startRow; i <= _endRow; i++)
            {
                var item = Items[i];
                var itemStr = item.Obj.ToString();
                if (itemStr != null)
                {
                    items.Add(item.Text);
                }
            }

            return items;
        }
    }
    
    private void ReflowFromBottom(bool stop)
    {
        var accumulatedLines = 0;
        var i = 0;
        while (accumulatedLines < _viewportRows && _endRow - i >= 0)
        {
            var item = Items[_endRow -  i];
            accumulatedLines += _wrap ? item.Text.WrappedLines().Count : item.Text.Lines.Count;
            
            i++;
        }

        if (_endRow - i + 1 <= 0 && accumulatedLines < _viewportRows && !stop)
        {
            _startRow = 0;
            ReflowFromTop();
            return;
        }
                
        _startRow = Math.Max(0, _endRow - i + 1);
        _startLinesToClip = Math.Max(0, accumulatedLines - _viewportRows);
    }

    public void SelectNext()
    {
        lock (_sync)
        {
            if (_selectedIndex + 1 < Items.Count)
            {
                if (_selectedIndex + 1 <= _endRow)
                {
                    _viewportSelectedIndex++;
                }
                else
                {
                    if (_endRow <= Items.Count)
                    {
                        _endRow++;
                        ReflowFromBottom(true);
                        _viewportSelectedIndex = Math.Max(0, _endRow - _startRow);
                    }
                }
                _selectedIndex++;
            }
        }
    }
    
    private void ReflowFromTop()
    {
        var accumulatedLines = 0;
        var i = 0;
        while (accumulatedLines < _viewportRows && _startRow + i < Items.Count)
        {
            var item = Items[_startRow + i];
            accumulatedLines += _wrap ? item.Text.WrappedLines().Count : item.Text.Lines.Count;
            i++;
        }

        if (_startRow + i >= Items.Count)
        {
            _endRow = Items.Count - 1;
            ReflowFromBottom(true);
            return;
        }
                
        _endRow = Math.Min(Items.Count - 1, _startRow + i - 1);
        _startLinesToClip = 0;
    }

    public void SelectPrevious()
    {
        lock (_sync)
        {
            if (_selectedIndex > 0)
            {
                _selectedIndex--;

                if (_viewportSelectedIndex > 0)
                {
                    _viewportSelectedIndex--;
                }
                else if(_startRow > 0)
                {
                    _startRow--;
                    ReflowFromTop();
                    _viewportSelectedIndex = 0;
                }
            }
            else
            {
                ReflowFromTop();
            }
        }
    }

    public void PageDown()
    {
        lock (_sync)
        {
            if (_endRow + 1 < Items.Count)
            {
                _startRow = _endRow + 1;
                ReflowFromTop();
                _selectedIndex = _startRow + _viewportSelectedIndex;
            }
            else
            {
                _selectedIndex = Items.Count - 1;
                _viewportSelectedIndex = _viewportRows - 1;
            }
        }
    }

    public void PageUp()
    {
        lock (_sync)
        {
            if (_startRow > 0)
            {
                _endRow = _startRow - 1;
                ReflowFromBottom(false);
                var numberOfRows = _endRow - _startRow;
                if (_viewportSelectedIndex > numberOfRows)
                {
                    _viewportSelectedIndex = numberOfRows;
                }
                _selectedIndex = _startRow + _viewportSelectedIndex;
            }
            else
            {
                _selectedIndex = 0;
                _viewportSelectedIndex = 0;
                ReflowFromTop();
            }
        }
    }

    public void SetTotalRows(int itemsCount)
    {
        lock (_sync)
        {
            _totalRows = itemsCount;
            if (_viewportSelectedIndex > _totalRows)
            {
                _viewportSelectedIndex = Math.Max(0, _totalRows - 1);
                _selectedIndex = Math.Max(0, _totalRows - 1);
                _startRow = 0;
            }
        }
    }
}