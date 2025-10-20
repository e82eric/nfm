using System.Runtime.Versioning;
using nfm.Ui.Core;

namespace nfm.Win32Ui;

[SupportedOSPlatform("windows")]
public class PreviewViewport
{
    public enum PreviewMode
    {
        Normal,
        Visual
    }
    
    public PreviewViewport(int viewportRows)
    {
        _viewportRows = viewportRows;
        SelectedLineStart = 0;
        SelectedLineEnd = 0;
    }

    public int StartRow { get; private set; }
    private readonly int _viewportRows;
    private List<List<TextSegment>>? _lines;
    private List<List<TextSegment>> Lines => _lines ?? throw new InvalidOperationException("Lines cannot be null");
    public int SelectedLineStart { get; private set; }
    public int SelectedLineEnd { get; private set; }
    public int SelectedLineFocused { get; private set; }
    private int PivotLine { get; set; }
    public int YankLineStart { get; private set; }
    public int YankLineEnd { get; private set; }
    public bool YankInProgress;
    private Timer? _yankTimer;

    public PreviewMode Mode { get; private set; } 

    private int EndRow => Math.Min(StartRow + _viewportRows, Lines.Count - 1);
    
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

    public void SetLines(List<List<TextSegment>> lines, int startLine)
    {
        _lines = lines;
        StartRow = startLine;
    }

    public List<List<TextSegment>> ViewportLines()
    {
        return Lines.GetRange(StartRow, EndRow + 1 - StartRow).ToList();
    }

    public void SelectNextLine()
    {
        if (SelectedLineEnd < Lines.Count - 1)
        {
            if (Mode == PreviewMode.Normal)
            {
                SelectedLineEnd++;
                SelectedLineStart = SelectedLineEnd;
                SelectedLineFocused = SelectedLineEnd;
            }
            else
            {
                if (SelectedLineStart < PivotLine)
                {
                    SelectedLineStart++;
                    SelectedLineFocused = SelectedLineStart;
                }
                else
                {
                    SelectedLineEnd++;
                    SelectedLineFocused = SelectedLineEnd;
                }
            }

            if (SelectedLineEnd >= EndRow && StartRow + 1 < Lines.Count)
            {
                StartRow++;
            }
        }
    }

    public void SelectPreviousLine()
    {
        if (SelectedLineFocused > 0)
        {
            if (Mode == PreviewMode.Normal)
            {
                SelectedLineStart--;
                SelectedLineEnd = SelectedLineStart;
                SelectedLineFocused = SelectedLineStart;
            }
            else
            {
                if (SelectedLineEnd > PivotLine)
                {
                    SelectedLineEnd--;
                    SelectedLineFocused = SelectedLineEnd;
                }
                else
                {
                    SelectedLineStart--;
                    SelectedLineFocused = SelectedLineStart;
                }
            }

            if (SelectedLineStart <= StartRow && StartRow > 0)
            {
                StartRow--;
            }
        }
    }

    public void ToggleVisualMode()
    {
        Mode = Mode == PreviewMode.Visual ? PreviewMode.Normal : PreviewMode.Visual;
        if (Mode == PreviewMode.Normal)
        {
            SelectedLineEnd = SelectedLineStart;
            SelectedLineFocused = SelectedLineStart;
        }
        PivotLine = SelectedLineStart;
    }
    
    public void SelectHalfPageDown()
    {
        if (Mode != PreviewMode.Normal || Lines.Count == 0 || _viewportRows <= 0)
            return;

        int half = Math.Max(1, _viewportRows / 2);
        int lastIndex = Lines.Count - 1;

        // Move the selection down by half a page, clamped to last item
        int targetSel = Math.Min(SelectedLineStart + half, lastIndex);

        // Max top row so a full viewport fits (or 0 if list is shorter)
        int maxStart = Math.Max(0, Lines.Count - _viewportRows);

        // Put the selected line at the top if possible, but don't exceed maxStart
        int targetStart = Math.Min(targetSel, maxStart);

        SelectedLineStart   = targetSel;
        SelectedLineEnd     = targetSel;
        SelectedLineFocused = targetSel;
        StartRow            = targetStart;
    }
    
    public void SelectHalfPageUp()
    {
        if (Mode != PreviewMode.Normal || Lines.Count == 0 || _viewportRows <= 0)
        {
            return;
        }

        int half = Math.Max(1, _viewportRows / 2);

        int targetSel = Math.Max(SelectedLineStart - half, 0);

        int maxStart = Math.Max(0, Lines.Count - _viewportRows);

        int targetStart = Math.Min(targetSel, maxStart);

        SelectedLineStart = targetSel;
        SelectedLineEnd = targetSel;
        SelectedLineFocused = targetSel;
        StartRow = targetStart;
    }
    
    public void SelectToTop()
    {
        if (Mode == PreviewMode.Normal)
        {
            SelectedLineStart = 0;
            SelectedLineEnd = SelectedLineStart;
            SelectedLineFocused = SelectedLineStart;
        }
        else
        {
            if (SelectedLineEnd > PivotLine)
            {
                SelectedLineStart = 0;
                SelectedLineEnd = PivotLine;
                SelectedLineFocused = SelectedLineStart;
            }
            else
            {
                SelectedLineStart = 0;
                SelectedLineFocused = SelectedLineStart;
            }
        }

        StartRow = 0;
    }

    public void SelectToBottom()
    {
        if (Mode == PreviewMode.Normal)
        {
            SelectedLineStart = Lines.Count - 2;
            SelectedLineEnd = SelectedLineStart;
            SelectedLineFocused = SelectedLineStart;
        }
        else
        {
            if (SelectedLineStart < PivotLine)
            {
                SelectedLineEnd = Lines.Count - 1;
                SelectedLineStart = PivotLine;
                SelectedLineFocused = SelectedLineEnd;
            }
            else
            {
                SelectedLineEnd = Lines.Count - 2;
                SelectedLineFocused = SelectedLineEnd;
            }
        }

        StartRow = Math.Max(0, Lines.Count - _viewportRows - 1);
    }

    public void CopySelected(Win32Window view)
    {
        int count = SelectedLineEnd - SelectedLineStart + 1;
        if (count <= 0)
        {
            return;
        }

        var selectedLines = Lines.GetRange(SelectedLineStart, count);

        var linesText = selectedLines.Select(line => 
            string.Concat(line.Select(segment => segment.Text))
        );

        string textToCopy = string.Join("\r\n", linesText);

        ClipboardHelper.Copy(textToCopy);

        YankLineStart = SelectedLineStart;
        YankLineEnd = SelectedLineEnd;
        YankInProgress = true;
        _yankTimer?.Dispose();
        view.TriggerPreviewRender();

        _yankTimer = new Timer(a =>
        {
            YankLineEnd = 0;
            YankLineStart = 0;
            YankInProgress = false;
            SelectedLineEnd = SelectedLineStart;
            SelectedLineFocused = SelectedLineEnd;
            Mode = PreviewMode.Normal;
            _yankTimer?.Dispose();
            view.TriggerPreviewRender();
        }, null, TimeSpan.FromMilliseconds(400), Timeout.InfiniteTimeSpan);
    }
}