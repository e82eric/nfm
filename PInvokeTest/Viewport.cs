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
    private int _viewportRows;
    private int _totalRows;

    public int EndRow
    {
        get { return StartRow + Math.Min(_viewportRows, _totalRows) ; }
    }

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