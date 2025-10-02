using System.Drawing;
using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Text;
using nfm.Ui.Core;
using static nfm.Win32Ui.Native;

namespace nfm.Win32Ui;

[SupportedOSPlatform("windows")]
public class Win32Window
{
    private readonly WndProc _delegWndProc;
    private readonly SubclassProc _summaryTextControlProc;
    private readonly SubclassProc _listBoxControlProc;
    private readonly SubclassProc _listBoxPanelControlProc;
    private readonly SubclassProc _previewPanelControlProc;
    private readonly SubclassProc _previewControlProc;
    private readonly SubclassProc _textBoxPanelControlProc;
    private readonly SubclassProc _editControlProc;
    private readonly SubclassProc _autocompleteControlProc;
    private readonly SubclassProc _autocompletePanelControlProc;

    private int _spinnerCtr = 0;
    private const int PS_SOLID = 0;
    private const int BORDER_THICKNESS = 2;
    private const int BORDER_COLOR = 0x00888545; //0x00bbggrr
    private const int BACKGROUND_COLOR = 0x00282828; //0x00bbggrr
    private const int AUTOCOMPLETE_BACKGROUND_COLOR = 0x00302f32;
    private const int SELECTED_BACKGROUND_COLOR = 0x00454950; //0x00bbggrr
    private const int SELECTED_BACKGROUND_COLOR_2 = 0x00545c66; //0x00665c54
    private const int TEXT_COLOR = 0x008499a8; //0x00a88499
    
    private const int GAP_COLOR = 0x00454950; //0x00504945
    private const int SPINNER_COLOR = BORDER_COLOR;
    //private const int HIGHLIGHTED_TEXT_COLOR = 0x000e5dd6; //0x00bbggrr
    private const int HIGHLIGHTED_TEXT_COLOR = 0x0000a5ff; //ffa500
    private const int CORNER_RADIUS = 7;
    
    private const UInt32 WM_ITEMS_UPDATED = WM_USER + 1;
    private const UInt32 WM_SUMMARY_TIMER = WM_USER + 2;
    private const UInt32 WM_TOGGLE_PREVIEW = WM_USER + 3;
    private const UInt32 WM_SHOW_ROOT = WM_USER + 4;
    private const UInt32 WM_HIDE_ROOT = WM_USER + 5;
    private const UInt32 WM_INITALIZED = WM_USER + 6;
    private const UInt32 WM_SET_SEARCH_STRING = WM_USER + 7;
    private const UInt32 WM_FOCUS_PREVIEW = WM_USER + 8;
    private const UInt32 WM_FOCUS_SEARCH = WM_USER + 9;
    private const UInt32 WM_SHOW_SUGGESTIONS = WM_USER + 10;
    private const UInt32 WM_HIDE_SUGGESTIONS = WM_USER + 11;
    private const UInt32 WS_VISIBLE = 0x10000000;
    
    private const int RDW_INVALIDATE = 0x0001;
    private const int RDW_ERASE = 0x0004;
    private const int RDW_FRAME = 0x0400;
    private const int RDW_ALLCHILDREN = 0x0080;
    private const int RDW_UPDATENOW = 0x0100;
    const uint WM_SIZE = 0x0005;
    const int SW_HIDE = 0;
    const int SW_SHOW = 5;

    [DllImport("user32.dll", SetLastError = false)]
    private static extern bool RedrawWindow(IntPtr hWnd, IntPtr lprcUpdate, IntPtr hrgnUpdate, uint flags);
    
    private void ClearUI()
    {
        _timer?.Change(Timeout.Infinite, Timeout.Infinite);

        if (_textBoxHwnd != IntPtr.Zero)
        {
            SetWindowText(_textBoxHwnd, string.Empty);
            SendMessage(_textBoxHwnd, EM_SETSEL, IntPtr.Zero, IntPtr.Zero);
        }

        lock (_itemsLock)
        {
            _snapshot = null;
        }

        _lines = null;
        _bitmap?.Dispose();
        _bitmap = null;
        Interlocked.Exchange(ref _previewType, PreviewType.Text);
        Interlocked.Exchange(ref _previewVersion, 0);
        Interlocked.Exchange(ref _lastPreviewVersion, 0);

        _toastVisible = false;
        _toastString = null;
        _toastExpirationTicks = 0;
        _spinnerCtr = 0;

        _hasHeader = false;
        _headerText = null;

        HideAutocomplete();

        if (_rootHwnd != IntPtr.Zero)
        {
            RedrawWindow(
                _rootHwnd,
                IntPtr.Zero,
                IntPtr.Zero,
                RDW_ERASE | RDW_INVALIDATE | RDW_ALLCHILDREN | RDW_FRAME | RDW_UPDATENOW);
        }
    }

    static ModifierKeys GetModifiersPressed()
    {
        ModifierKeys modifiersPressed = ModifierKeys.None;

        if ((GetAsyncKeyState(VK_LSHIFT) & 0x8000) != 0)
        {
            modifiersPressed |= ModifierKeys.LShift;
        }
        if ((GetAsyncKeyState(VK_RSHIFT) & 0x8000) != 0)
        {
            modifiersPressed |= ModifierKeys.RShift;
        }
        if ((GetAsyncKeyState(VK_LMENU) & 0x8000) != 0)
        {
            modifiersPressed |= ModifierKeys.LAlt;
        }
        if ((GetAsyncKeyState(VK_RMENU) & 0x8000) != 0)
        {
            modifiersPressed |= ModifierKeys.RAlt;
        }
        if ((GetAsyncKeyState(VK_LCONTROL) & 0x8000) != 0)
        {
            modifiersPressed |= ModifierKeys.LCtl;
        }
        if ((GetAsyncKeyState(VK_LWIN) & 0x8000) != 0)
        {
            modifiersPressed |= ModifierKeys.LWin;
        }
        if ((GetAsyncKeyState(VK_RWIN) & 0x8000) != 0)
        {
            modifiersPressed |= ModifierKeys.RWin;
        }

        return modifiersPressed;
    }
    
    private static GraphicsPath GetRoundedRect(Rectangle rect, int radius)
    {
        GraphicsPath path = new GraphicsPath();
        int diameter = radius * 2;

        // Top left arc
        path.AddArc(rect.X, rect.Y, diameter, diameter, 180, 90);
        // Top edge
        path.AddLine(rect.X + radius, rect.Y, rect.Right - radius, rect.Y);
        // Top right arc
        path.AddArc(rect.Right - diameter, rect.Y, diameter, diameter, 270, 90);
        // Right edge
        path.AddLine(rect.Right, rect.Y + radius, rect.Right, rect.Bottom - radius);
        // Bottom right arc
        path.AddArc(rect.Right - diameter, rect.Bottom - diameter, diameter, diameter, 0, 90);
        // Bottom edge
        path.AddLine(rect.Right - radius, rect.Bottom, rect.X + radius, rect.Bottom);
        // Bottom left arc
        path.AddArc(rect.X, rect.Bottom - diameter, diameter, diameter, 90, 90);
        // Left edge
        path.AddLine(rect.X, rect.Bottom - radius, rect.X, rect.Y + radius);
        path.CloseFigure();
        return path;
    }

    private IntPtr SummaryTextControlProc(IntPtr hWnd, uint uMsg, IntPtr wParam, IntPtr lParam, uint uIdSubclass, IntPtr dwRefData)
    {
        PAINTSTRUCT ps;
        switch (uMsg)
        {
            case WM_PAINT:
                char[] spinner = { '\u280B', '\u2819', '\u2839', '\u2838', '\u283C', '\u2834', '\u2827', '\u2807', '\u280F' };

                char[] spinnerBuffer = new char[10];
                char[] countBuffer = new char[256];

                spinnerBuffer[0] = spinner[_spinnerCtr];
                spinnerBuffer[1] = ' ';
                spinnerBuffer[2] = '\0';

                int numberOfItemsMatched = _viewModel.NumberOfScoredItems;
                int numberOfItems = _viewModel.NumberOfItems;
                string countText = $"{numberOfItemsMatched}/{numberOfItems}";
                countText.CopyTo(0, countBuffer, 0, countText.Length);
                countBuffer[countText.Length] = '\0';

                if (_spinnerCtr < spinner.Length - 1)
                {
                    _spinnerCtr++;
                }
                else
                {
                    _spinnerCtr = 0;
                }

                IntPtr hdc = BeginPaint(hWnd, out ps);
                var hBufferedPaint = BeginBufferedPaint(
                    hdc,
                    ref ps.rcPaint,
                    BPBF_COMPATIBLEBITMAP,
                    IntPtr.Zero,
                    out var hNewDc);
                if (hBufferedPaint == IntPtr.Zero)
                {
                    return IntPtr.Zero;
                }

                SetTextAlign(hNewDc, TA_LEFT);
                FillRect(hNewDc, ref ps.rcPaint, _backgroundBrush);
                SelectObject(hNewDc, _font);
                SetBkMode(hNewDc, TRANSPARENT);

                SIZE sz;
                int width = ps.rcPaint.right - ps.rcPaint.left;
                SetTextColor(hNewDc, TEXT_COLOR);

                GetTextExtentPoint32(hNewDc, new string(countBuffer), countText.Length, out sz);
                int offset = width - sz.cx - 2;

                TextOut(hNewDc, offset, 2, new string(countBuffer), countText.Length);

                if (_viewModel.Reading || _viewModel.Searching)
                {
                    GetTextExtentPoint32(hNewDc, new string(spinnerBuffer), 1, out sz);
                    SetTextColor(hNewDc, SPINNER_COLOR);
                    TextOut(hNewDc, offset - sz.cx - 5, 2, new string(spinnerBuffer), 1);
                }

                EndBufferedPaint(hBufferedPaint, true);
                EndPaint(hWnd, ref ps);
                return 1;
            case WM_ERASEBKGND:
                return 1;
            default:
                return DefSubclassProc(hWnd, uMsg, wParam, lParam);
        }
    }
    
    private IntPtr ListBoxControlProc(IntPtr hWnd, uint uMsg, IntPtr wParam, IntPtr lParam, uint uIdSubclass, IntPtr dwRefData)
    {
        PAINTSTRUCT ps;
        switch (uMsg)
        {
            case WM_PAINT:
                Snapshot? snapshot;
                lock (_itemsLock)
                {
                    snapshot = _snapshot;
                }
                if (snapshot == null)
                {
                    _ = BeginPaint(hWnd, out ps);
                    FillRect(ps.hdc, ref ps.rcPaint, _backgroundBrush); // clear
                    EndPaint(hWnd, ref ps);
                    return 0;
                }
                
                var hdc = BeginPaint(_listBoxHwnd, out ps);
                var hBufferedPaint = BeginBufferedPaint(
                    hdc,
                    ref ps.rcPaint,
                    BPBF_COMPATIBLEBITMAP,
                    IntPtr.Zero,
                    out var hNewDc);
                if (hBufferedPaint == IntPtr.Zero)
                {
                    return IntPtr.Zero;
                }
                
                TEXTMETRIC tm;
                GetTextMetrics(hNewDc, out tm);
                int textHeight = tm.tmHeight;
                
                FillRect(hNewDc, ref ps.rcPaint, _backgroundBrush);

                var listBoxStartY = ps.rcPaint.top;
                SelectObject(hNewDc, _font);
                
                if (_hasHeader && _headerText != null)
                {
                    IntPtr hOldPen = SelectObject(hNewDc, _borderPen);
                    SelectObject(hNewDc, _font);

                    SetBkColor(hNewDc, BACKGROUND_COLOR);
                    SetTextColor(hNewDc, HIGHLIGHTED_TEXT_COLOR);
                    TextOut(
                        hNewDc,
                        5,
                        0,
                        _headerText,
                        _headerText.Length);
                
                    MoveToEx(hNewDc, 0, textHeight + _listboxItemPadding, IntPtr.Zero);
                    LineTo(hNewDc, ps.rcPaint.right, textHeight + _listboxItemPadding);
                    listBoxStartY = _listBoxItemHeight;
                }
                
                //Offset the x of item so that selected item background has some padding
                var itemXOffset = 5;
                lock (_itemsLock)
                {
                    var nextItemY = 0;
                    for (var i = 0; i < snapshot.Items.Count; i++)
                    {
                        var item = snapshot.Items[i];
                        var startLine = 0;
                        var linesToRender = snapshot.WrapLines ? item.Text.WrappedLines() : item.Text.Lines;
                        var itemLines = linesToRender.Count;
                        if (i == 0)
                        {
                            itemLines = linesToRender.Count - snapshot.StartLinesToClip;
                            startLine = snapshot.StartLinesToClip;
                        }
                        int totalHeight = itemLines * _listBoxItemHeight;
                        
                        var itemTop = listBoxStartY + nextItemY;
                        var rcItem = new RECT
                        {
                            top = itemTop,
                            bottom = itemTop + totalHeight,
                            left = ps.rcPaint.left,
                            right = ps.rcPaint.right
                        };
                        if (i == snapshot.SelectedIndex)
                        {
                            FillRect(hNewDc, ref rcItem, _selectedBackgroundBrush);
                            SetBkColor(hNewDc, SELECTED_BACKGROUND_COLOR);
                        }
                        else
                        {
                            FillRect(hNewDc, ref rcItem, _backgroundBrush);
                            SetBkColor(hNewDc, BACKGROUND_COLOR);
                        }

                        SetTextColor(hNewDc, TEXT_COLOR);

                        if (i < snapshot.Items.Count)
                        {
                            for (var iIndex = 0; iIndex < itemLines; iIndex++)
                            {
                                var line = linesToRender[startLine + iIndex];
                                var xOffset = 0;
                                var centeredY = rcItem.top + (_listBoxItemHeight * iIndex) + (textHeight / 2);
                                foreach (var segment in line.Segments)
                                {
                                    SIZE sz;
                                    GetTextExtentPoint32(hNewDc, segment.Text, segment.Text.Length, out sz);
                                    var color = segment.State.Foreground;
                                    int colorRef = (color.B << 16) | (color.G << 8) | color.R;
                                    var backgroundColor = segment.State.Background;
                                    int backgroundColorRef = (backgroundColor.B << 16) | (backgroundColor.G << 8) |
                                                             backgroundColor.R;
                                    SetTextColor(hNewDc, colorRef);
                                    TextOut(
                                        hNewDc,
                                        itemXOffset + xOffset,
                                        centeredY,
                                        segment.Text,
                                        segment.Text.Length);
                                    xOffset += sz.cx;
                                }

                                SetTextColor(hNewDc, HIGHLIGHTED_TEXT_COLOR);
                                for (int j = 0; j < line.Pos.Count; j++)
                                {
                                    var pos = line.Pos[j];
                                    SIZE sz;
                                    GetTextExtentPoint32(hNewDc, line.LineText(), pos, out sz);
                                    TextOut(
                                        hNewDc,
                                        sz.cx + itemXOffset,
                                        centeredY,
                                        line.LineText()[pos].ToString(),
                                        1);
                                }
                            }
                        }
                        
                        if (snapshot.ShowGap)
                        {
                            SelectObject(hNewDc, _gapPen);
                            MoveToEx(hNewDc, 0, itemTop + totalHeight - 2, IntPtr.Zero);
                            LineTo(hNewDc, ps.rcPaint.right, itemTop + totalHeight - 2);
                        }

                        nextItemY += totalHeight;
                    }
                }

                if (_toastVisible && _toastString != null)
                {
                    SetBkColor(hNewDc, BACKGROUND_COLOR);
                    SetTextColor(hNewDc, TEXT_COLOR);
                    var padding = 10;
                
                    var height = ps.rcPaint.bottom - ps.rcPaint.top;
                    var middle = ps.rcPaint.top + height / 2;
                
                    SIZE toastTextSize;
                    GetTextExtentPoint32(hNewDc, _toastString, _toastString.Length, out toastTextSize);

                    var width = ps.rcPaint.right - ps.rcPaint.left;
                    var hMiddle = ps.rcPaint.left + (width / 2);
                    var toastTextWidth = toastTextSize.cx;
                
                    var toastRect = new Rectangle(
                        hMiddle - (toastTextWidth / 2) - padding,
                        middle,
                        toastTextWidth + (padding * 2),
                        padding + tm.tmHeight + padding
                    );
                    
                    using (Graphics g = Graphics.FromHdc(hNewDc))
                    {
                        g.SmoothingMode = SmoothingMode.HighQuality;

                        using (GraphicsPath path = GetRoundedRect(toastRect, CORNER_RADIUS))
                        {
                            using (SolidBrush brush = new SolidBrush(ColorTranslator.FromWin32(BACKGROUND_COLOR)))
                            {
                                g.FillPath(brush, path);
                            }

                            using (Pen pen = new Pen(ColorTranslator.FromWin32(BORDER_COLOR), BORDER_THICKNESS))
                            {
                                g.DrawPath(pen, path);
                            }
                        }
                    }

                    TextOut(
                        hNewDc,
                        toastRect.Left + padding,
                        toastRect.Top + padding,
                        _toastString,
                        _toastString.Length);
                }
                
                EndBufferedPaint(hBufferedPaint, true);
                EndPaint(hWnd, ref ps);
                
                return 1;
            case WM_ERASEBKGND:
                return 1;
            default:
                return DefSubclassProc(hWnd, uMsg, wParam, lParam);
        }
    }
    
    private static IntPtr PreviewPanelControlProc(IntPtr hWnd, uint uMsg, IntPtr wParam, IntPtr lParam, uint uIdSubclass, IntPtr dwRefData)
    {
        switch (uMsg)
        {
            case WM_PAINT:
            {
                return PaintPanelBorder(hWnd);
            }
            case WM_ERASEBKGND:
                return 0;
            default:
                return DefSubclassProc(hWnd, uMsg, wParam, lParam);
        }
    }
    
    private IntPtr ListBoxPanelControlProc(IntPtr hWnd, uint uMsg, IntPtr wParam, IntPtr lParam, uint uIdSubclass, IntPtr dwRefData)
    {
        switch (uMsg)
        {
            case WM_PAINT:
            {
                return PaintPanelBorder(hWnd);
            }
            case WM_COMMAND:
                var notificationCode = (ushort)((wParam.ToInt64() >> 16) & 0xFFFF);
                if (notificationCode == EN_CHANGE)
                {
                    var text = new StringBuilder(GetWindowTextLength(_textBoxHwnd) + 1);
                    GetWindowText(_textBoxHwnd, text, text.Capacity);
                    _viewModel.SetSearchString(text.ToString());
                }
                return 0;
            case WM_CTLCOLOREDIT:
            {
                var hdc = wParam;
                SetBkColor(hdc, BACKGROUND_COLOR);
                SetTextColor(hdc, TEXT_COLOR);
                return _backgroundBrush;
            }
            case WM_ERASEBKGND:
                return 0;
            default:
                return DefSubclassProc(hWnd, uMsg, wParam, lParam);
        }
    }

    private static IntPtr PaintSquarePanelBorder(IntPtr hWnd)
    {
        var hdc = BeginPaint(hWnd, out var ps);

        // Get the actual client rectangle of the window
        GetClientRect(hWnd, out RECT clientRect);

        var hBufferedPaint = BeginBufferedPaint(
            hdc,
            ref clientRect,
            BPBF_COMPATIBLEBITMAP,
            IntPtr.Zero,
            out var hNewDc);
        if (hBufferedPaint == IntPtr.Zero || hNewDc == IntPtr.Zero)
        {
            return IntPtr.Zero;
        }

        var blackBrush = CreateSolidBrush(BACKGROUND_COLOR);
        FillRect(hNewDc, ref clientRect, blackBrush);
        using (Graphics g = Graphics.FromHdc(hNewDc))
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            Rectangle rect = new Rectangle(
                clientRect.left + BORDER_THICKNESS,
                clientRect.top + BORDER_THICKNESS,
                clientRect.right - clientRect.left - (BORDER_THICKNESS * 2),
                clientRect.bottom - clientRect.top - (BORDER_THICKNESS * 2)
            );

            using (GraphicsPath path = GetRoundedRect(rect, CORNER_RADIUS))
            {
                using (SolidBrush brush = new SolidBrush(ColorTranslator.FromWin32(BACKGROUND_COLOR)))
                {
                    g.FillPath(brush, path);
                }

                using (Pen pen = new Pen(ColorTranslator.FromWin32(BORDER_COLOR), BORDER_THICKNESS))
                {
                    g.DrawPath(pen, path);
                }
            }
        }

        EndBufferedPaint(hBufferedPaint, true);
        EndPaint(hWnd, ref ps);
        return IntPtr.Zero;
    }

    private static IntPtr PaintPanelBorder(IntPtr hWnd)
    {
        var hdc = BeginPaint(hWnd, out var ps);

        // Get the actual client rectangle of the window
        GetClientRect(hWnd, out RECT clientRect);

        var hBufferedPaint = BeginBufferedPaint(
            hdc,
            ref clientRect,
            BPBF_COMPATIBLEBITMAP,
            IntPtr.Zero,
            out var hNewDc);
        if (hBufferedPaint == IntPtr.Zero || hNewDc == IntPtr.Zero)
        {
            return IntPtr.Zero;
        }

        var blackBrush = CreateSolidBrush(BACKGROUND_COLOR);
        FillRect(hNewDc, ref clientRect, blackBrush);
        using (Graphics g = Graphics.FromHdc(hNewDc))
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            Rectangle rect = new Rectangle(
                clientRect.left + BORDER_THICKNESS,
                clientRect.top + BORDER_THICKNESS,
                clientRect.right - clientRect.left - (BORDER_THICKNESS * 2),
                clientRect.bottom - clientRect.top - (BORDER_THICKNESS * 2)
            );

            using (GraphicsPath path = GetRoundedRect(rect, CORNER_RADIUS))
            {
                using (SolidBrush brush = new SolidBrush(ColorTranslator.FromWin32(BACKGROUND_COLOR)))
                {
                    g.FillPath(brush, path);
                }

                using (Pen pen = new Pen(ColorTranslator.FromWin32(BORDER_COLOR), BORDER_THICKNESS))
                {
                    g.DrawPath(pen, path);
                }
            }
        }

        EndBufferedPaint(hBufferedPaint, true);
        EndPaint(hWnd, ref ps);
        return IntPtr.Zero;
    }

    private IntPtr TextBoxPanelControlProc(IntPtr hWnd, uint uMsg, IntPtr wParam, IntPtr lParam, uint uIdSubclass, IntPtr dwRefData)
    {
        switch (uMsg)
        {
            case WM_PAINT:
            {
                return PaintPanelBorder(hWnd);
            }
            case WM_COMMAND:
                var notificationCode = (ushort)((wParam.ToInt64() >> 16) & 0xFFFF);
                if (notificationCode == EN_CHANGE)
                {
                    var text = new StringBuilder(GetWindowTextLength(_textBoxHwnd) + 1);
                    GetWindowText(_textBoxHwnd, text, text.Capacity);
                    var searchText = text.ToString();

                    Task.Run(() => _viewModel.SetSearchString(searchText));
                }
                return 0;
            case WM_CTLCOLOREDIT:
            {
                var hdc = wParam;
                SetBkColor(hdc, BACKGROUND_COLOR);
                SetTextColor(hdc, TEXT_COLOR);
                return _backgroundBrush;
            }
            case WM_ERASEBKGND:
                return 1;
            default:
                return DefSubclassProc(hWnd, uMsg, wParam, lParam);
        }
    }
    
    private IntPtr PreviewControlProc(IntPtr hWnd, uint uMsg, IntPtr wParam, IntPtr lParam, uint uIdSubclass, IntPtr dwRefData)
    {
        switch (uMsg)
        {
            case WM_PAINT:
            {
                if (_previewType == PreviewType.Image && _bitmap == null)
                {
                    PAINTSTRUCT ps2; var hdc2 = BeginPaint(hWnd, out ps2);
                    FillRect(hdc2, ref ps2.rcPaint, _backgroundBrush);
                    EndPaint(hWnd, ref ps2);
                    return 0;
                }
                
                if (_previewType == PreviewType.Image && _bitmap != null)
                {
                    PAINTSTRUCT imagePs;
                    IntPtr imageHdc = BeginPaint(hWnd, out imagePs);

                    IntPtr memDC = CreateCompatibleDC(imageHdc);
                    IntPtr hBitmap = _bitmap.GetHbitmap();
                    IntPtr oldBitmap = SelectObject(memDC, hBitmap);
                    FillRect(imageHdc, ref imagePs.rcPaint, _backgroundBrush);

                    var x = ((imagePs.rcPaint.right - imagePs.rcPaint.left) / 2) - (_bitmap.Width / 2);
                    
                    BitBlt(imageHdc, x, 0, _bitmap.Width, _bitmap.Height, memDC, 0, 0, SRCCOPY);

                    SelectObject(memDC, oldBitmap);
                    DeleteDC(memDC);
                    DeleteObject(hBitmap);

                    EndPaint(hWnd, ref imagePs);
                    return 0;
                }
                
                if (_lines == null || _lastPreviewVersion == _previewVersion)
                {
                    PAINTSTRUCT ps2; var hdc2 = BeginPaint(hWnd, out ps2);
                    FillRect(hdc2, ref ps2.rcPaint, _backgroundBrush);
                    EndPaint(hWnd, ref ps2);
                    return 0;
                }

                Interlocked.Exchange(ref _lastPreviewVersion, _previewVersion);
                var hdc = BeginPaint(hWnd, out var ps);
                var hBufferedPaint = BeginBufferedPaint(
                    hdc,
                    ref ps.rcPaint,
                    BPBF_COMPATIBLEBITMAP,
                    IntPtr.Zero,
                    out var hNewDc);

                if (hBufferedPaint == IntPtr.Zero || hNewDc == IntPtr.Zero)
                {
                    return IntPtr.Zero;
                }

                var rect = ps.rcPaint;

                SetBkColor(hNewDc, BACKGROUND_COLOR);
                SetTextColor(hNewDc, TEXT_COLOR);
                SelectObject(hNewDc, _font);
                FillRect(hNewDc, ref ps.rcPaint, _backgroundBrush);

                TEXTMETRIC tm;
                GetTextMetrics(hNewDc, out tm);
                for (var i = 0; i < _lines.Count; i++)
                {
                    var itemTop = rect.top + (i * _previewItemHeight);
                    var rcItem = new RECT
                    {
                        top = itemTop,
                        bottom = itemTop + _previewItemHeight,
                        left = rect.left,
                        right = rect.right
                    };

                    int textHeight = tm.tmHeight;
                    int centeredY = rcItem.top + (_previewItemHeight - textHeight) / 2;

                    var line = _lines[i];

                    var selectedLine = false;
                    var selectedAndFocused = false;
                    if (_viewModel.PreviewViewPort.Focused)
                    {
                        if (i + _viewModel.PreviewViewPort.StartRow >= _viewModel.PreviewViewPort.SelectedLineStart && i + _viewModel.PreviewViewPort.StartRow <= _viewModel.PreviewViewPort.SelectedLineEnd)
                        {
                            selectedLine = true;
                            if (i + _viewModel.PreviewViewPort.StartRow ==
                                _viewModel.PreviewViewPort.SelectedLineFocused)
                            {
                                selectedAndFocused = true;
                            }
                            FillRect(hNewDc, ref rcItem, _viewModel.PreviewViewPort.YankInProgress ? _highlightBackgroundBrush : selectedAndFocused ? _selectedBackgroundBrush : _selectedBackgroundBrush2);
                        }
                    }

                    var xOffset = 0;
                    foreach (var segment in line)
                    {
                        SIZE sz;
                        GetTextExtentPoint32(hNewDc, segment.Text, segment.Text.Length, out sz);
                        var color = segment.State.Foreground;
                        int colorRef = (color.B << 16) | (color.G << 8) | color.R;
                        var backgroundColor = segment.State.Background;
                        int backgroundColorRef = _viewModel.PreviewViewPort.YankInProgress && selectedLine ? SELECTED_BACKGROUND_COLOR_2 : selectedAndFocused ? SELECTED_BACKGROUND_COLOR : selectedLine ? SELECTED_BACKGROUND_COLOR_2 : (backgroundColor.B << 16) | (backgroundColor.G << 8) | backgroundColor.R;
                        SetBkColor(hNewDc, backgroundColorRef);
                        SetTextColor(hNewDc, _viewModel.PreviewViewPort.YankInProgress && selectedLine ? BACKGROUND_COLOR : colorRef);
                        TextOut(hNewDc, rect.left + xOffset, centeredY, segment.Text, segment.Text.Length);
                        xOffset += sz.cx;
                    }
                }

                EndBufferedPaint(hBufferedPaint, true);
                EndPaint(hWnd, ref ps);
            }
                return 1;
            case WM_ERASEBKGND:
                return 0;
            case WM_KEYDOWN:
                var modifiers = GetModifiersPressed();
                _viewModel.HandlePreviewKey((int)wParam, modifiers);
                return 1;
            default:
                return DefSubclassProc(hWnd, uMsg, wParam, lParam);
        }
    }
        
    private IntPtr EditControlProc(IntPtr hWnd, uint uMsg, IntPtr wParam, IntPtr lParam, uint uIdSubclass, IntPtr dwRefData)
    {
        switch (uMsg)
        {
            case WM_CHAR:
                if ((GetAsyncKeyState(VK_CONTROL) & 0x8000) != 0 && wParam != VK_LEFT && wParam != VK_RIGHT)
                {
                    return 0;
                }

                if (wParam == VK_RETURN || wParam == VK_ESCAPE)
                {
                    return 0;
                }
                return DefSubclassProc(hWnd, uMsg, wParam, lParam);
            case WM_KEYDOWN:
                if (_snapshot == null)
                {
                    return 1;
                }
                
                var modifiers = GetModifiersPressed();
                if (modifiers != ModifierKeys.None)
                {
                    if (modifiers == ModifierKeys.LCtl && wParam == VK_PAGEUP)
                    {
                        _viewModel.PreviewHalfPageUp();
                        return 0;
                    }
                    
                    if (modifiers == ModifierKeys.LCtl && wParam == VK_PAGEDOWN)
                    {
                        _viewModel.PreviewHalfPageDown();
                        return 0;
                    }
                    
                    if (modifiers == ModifierKeys.LCtl && wParam == VK_BACK)
                    {
                        DeletePreviousWord(hWnd);
                        return 0;
                    }

                    if ((modifiers & ModifierKeys.LShift) == 0)
                    {
                        Task.Run(() => _viewModel.HandleKeyUp(_snapshot.Items[_snapshot.SelectedIndex].Item, (int)wParam, modifiers));
                        return 0;
                    }
                }
                else
                {
                    // Handle autocomplete navigation first
                    if (_autocompleteVisible && _autocompleteSuggestions.Count > 0)
                    {
                        switch (wParam)
                        {
                            case VK_DOWN:
                                UpdateAutocompleteSelection(1);
                                return 0;
                            case VK_UP:
                                UpdateAutocompleteSelection(-1);
                                return 0;
                            case VK_TAB:
                            case VK_RETURN:
                                ApplyAutocompleteSelection();
                                return 0;
                            case VK_ESCAPE:
                                HideAutocomplete();
                                return 0;
                        }
                    }

                    switch (wParam)
                    {
                        case VK_DOWN:
                            if (_viewModel.PreviewViewPort.Focused)
                            {
                                _viewModel.PreviewViewPort.SelectNextLine();
                                SetPreviewLines(_viewModel.PreviewViewPort.ViewportLines());
                            }
                            else
                            {
                                _viewModel.SelectNext();
                            }
                            return 0;
                        case VK_UP:
                            if (_viewModel.PreviewViewPort.Focused)
                            {
                                _viewModel.PreviewViewPort.SelectPreviousLine();
                                SetPreviewLines(_viewModel.PreviewViewPort.ViewportLines());
                            }
                            else
                            {
                                _viewModel.SelectPrevious();
                            }

                            return 0;
                        case VK_PAGEDOWN:
                            _viewModel.SelectPageDown();
                            return 0;
                        case VK_PAGEUP:
                            _viewModel.SelectPageUp();
                            return 0;
                        case VK_RETURN:
                            lock (_itemsLock)
                            {
                                Task.Run(async () => { await _viewModel.OnReturn(_snapshot.Items[_snapshot.SelectedIndex].Item); });
                            }
                            return 0;
                        case VK_ESCAPE:
                            _viewModel.OnEscape();
                            return 0;
                    }
                }
                return DefSubclassProc(hWnd, uMsg, wParam, lParam);
            default:
                return DefSubclassProc(hWnd, uMsg, wParam, lParam);
        }
    }
    
    private static void DeletePreviousWord(IntPtr hWnd)
    {
        var length = GetWindowTextLength(hWnd);
        var sb = new StringBuilder(length + 1);
        GetWindowText(hWnd, sb, sb.Capacity);
        var text = sb.ToString();

        var startPosSel = 0;
        var endPosSel = 0;
        SendMessage(hWnd, EM_GETSEL, ref startPosSel, ref endPosSel);
        var caretPos = endPosSel;

        if (caretPos <= 0) return;

        var startPos = caretPos - 1;

        while (startPos > 0 && char.IsWhiteSpace(text[startPos]))
        {
            startPos--;
        }
        while (startPos > 0 && !char.IsWhiteSpace(text[startPos]))
        {
            startPos--;
        }
        
        if (char.IsWhiteSpace(text[startPos]))
        {
            startPos++;
        }

        if (startPos < caretPos)
        {
            text = text.Remove(startPos, caretPos - startPos);
            SetWindowText(hWnd, text);
            SendMessage(hWnd, EM_SETSEL, (IntPtr)startPos, (IntPtr)startPos);
        }
    }

    private static IntPtr AutocompletePanelControlProc(IntPtr hWnd, uint uMsg, IntPtr wParam, IntPtr lParam, uint uIdSubclass, IntPtr dwRefData)
    {
        switch (uMsg)
        {
            case WM_PAINT:
            {
                return PaintSquarePanelBorder(hWnd);
            }
            case WM_ERASEBKGND:
                return 1;
            default:
                return DefSubclassProc(hWnd, uMsg, wParam, lParam);
        }
    }

    private IntPtr AutocompleteControlProc(IntPtr hWnd, uint uMsg, IntPtr wParam, IntPtr lParam, uint uIdSubclass, IntPtr dwRefDatk)
    {
        switch (uMsg)
        {
            case WM_PAINT:
                if (!_autocompleteVisible || _autocompleteSuggestions.Count == 0)
                {
                    var hdc = BeginPaint(hWnd, out var ps);
                    FillRect(hdc, ref ps.rcPaint, _autoCompleteBackgroundBrush);
                    EndPaint(hWnd, ref ps);
                    return 0;
                }

                var paintDc = BeginPaint(hWnd, out var paintStruct);
                var hBufferedPaint = BeginBufferedPaint(
                    paintDc,
                    ref paintStruct.rcPaint,
                    BPBF_COMPATIBLEBITMAP,
                    IntPtr.Zero,
                    out var hNewDc);

                if (hBufferedPaint == IntPtr.Zero)
                {
                    EndPaint(hWnd, ref paintStruct);
                    return IntPtr.Zero;
                }

                FillRect(hNewDc, ref paintStruct.rcPaint, _autoCompleteBackgroundBrush);

                GetTextMetrics(hNewDc, out var tm);
                SelectObject(hNewDc, _font);
                SetBkMode(hNewDc, TRANSPARENT);

                int padding = 2;

                for (int i = 0; i < _autocompleteSuggestions.Count; i++)
                {
                    var suggestion = _autocompleteSuggestions[i];
                    var y = i * _listBoxItemHeight + padding;
                    var itemRect = new RECT
                    {
                        left = paintStruct.rcPaint.left + padding,
                        top = y,
                        right = paintStruct.rcPaint.right - padding,
                        bottom = y + _listBoxItemHeight
                    };

                    if (i == _autocompleteSelectedIndex)
                    {
                        SetBkColor(hNewDc, SELECTED_BACKGROUND_COLOR);
                        FillRect(hNewDc, ref itemRect, _selectedBackgroundBrush);
                        SetTextColor(hNewDc, TEXT_COLOR);
                    }
                    else
                    {
                        SetBkColor(hNewDc, AUTOCOMPLETE_BACKGROUND_COLOR);
                        SetTextColor(hNewDc, TEXT_COLOR);
                    }

                    TextOut(hNewDc, padding + 3, y, suggestion, suggestion.Length);
                }

                EndBufferedPaint(hBufferedPaint, true);
                EndPaint(hWnd, ref paintStruct);
                return 0;

            case WM_ERASEBKGND:
                return 1;

            default:
                return DefSubclassProc(hWnd, uMsg, wParam, lParam);
        }
    }

    private void ShowAutocomplete()
    {
        var suggestions = _autocompleteSuggestions;
        _autocompleteSelectedIndex = suggestions.Count > 0 ? 0 : -1;
        _autocompleteVisible = true;

        int maxItems = Math.Min(suggestions.Count, 7);
        int contentHeight = maxItems * _listBoxItemHeight;
        int padding = BORDER_THICKNESS + 10;
        int panelHeight = contentHeight + (padding * 2);

        GetWindowRect(_textBoxHwnd, out var textBoxRect);
        GetWindowRect(_rootHwnd, out var rootRect);

        var textBoxLeft = textBoxRect.left - rootRect.left;
        var textBoxBottom = textBoxRect.bottom - rootRect.top;
        var textBoxWidth = textBoxRect.right - textBoxRect.left;

        SetWindowPos(_autocompletePanelHwnd, HWND_TOP,
            textBoxLeft, textBoxBottom,
            textBoxWidth, panelHeight,
            SetWindowPosFlags.SWP_SHOWWINDOW);
        
        SetWindowPos(_autocompleteHwnd, HWND_TOP,
            padding, padding,
            textBoxWidth - (padding * 2), contentHeight,
            SetWindowPosFlags.SWP_SHOWWINDOW);
        
        InvalidateRect(_autocompletePanelHwnd, IntPtr.Zero, true);
        InvalidateRect(_autocompleteHwnd, IntPtr.Zero, true);
    }

    private void HideAutocomplete()
    {
        _autocompleteVisible = false;
        _autocompleteSuggestions.Clear();
        _autocompleteSelectedIndex = -1;
        ShowWindow(_autocompletePanelHwnd, SW_HIDE);
        ShowWindow(_autocompleteHwnd, SW_HIDE);
    }

    private void UpdateAutocompleteSelection(int direction)
    {
        if (!_autocompleteVisible || _autocompleteSuggestions.Count == 0)
        {
            return;
        }

        _autocompleteSelectedIndex += direction;

        if (_autocompleteSelectedIndex < 0)
        {
            _autocompleteSelectedIndex = _autocompleteSuggestions.Count - 1;
        }
        else if (_autocompleteSelectedIndex >= _autocompleteSuggestions.Count)
        {
            _autocompleteSelectedIndex = 0;
        }

        InvalidateRect(_autocompleteHwnd, IntPtr.Zero, true);
    }

    private void ApplyAutocompleteSelection()
    {
        if (!_autocompleteVisible || _autocompleteSelectedIndex < 0 || _autocompleteSelectedIndex >= _autocompleteSuggestions.Count)
        {
            return;
        }

        var selectedSuggestion = _autocompleteSuggestions[_autocompleteSelectedIndex];
        
        var length = GetWindowTextLength(_textBoxHwnd);
        var sb = new StringBuilder(length + 1);
        GetWindowText(_textBoxHwnd, sb, sb.Capacity);
        var currentText = sb.ToString();
        
        var startPos = 0;
        var endPos = 0;
        SendMessage(_textBoxHwnd, EM_GETSEL, ref startPos, ref endPos);
        var currentCursorPosition = endPos;
        
        var result = AutocompleteService.ApplySelection(currentText, currentCursorPosition, selectedSuggestion);

        if (result.Success)
        {
            SetWindowText(_textBoxHwnd, result.NewText);
            SendMessage(_textBoxHwnd, EM_SETSEL, (IntPtr)result.NewCursorPosition, (IntPtr)result.NewCursorPosition);
        }
    }

    private struct ScreenLocation
    {
        public int x;
        public int y;
        public int width;
        public int height;
    }
    
    [DllImport("user32.dll", SetLastError = false)]
    internal static extern IntPtr MonitorFromWindow(IntPtr hwnd, uint dwFlags);

    private ScreenLocation CalculateOptimalWindowSize(bool includePreview)
    {
        const int BORDER_MARGIN = 10; // 10 pixels larger than content
        const int PANEL_MARGIN = 11; // panelX from original code
        const int PANEL_GAP = 7;
        const int PADDING = 15;

        // Calculate basic component dimensions
        var searchInputHeight = _listBoxItemHeight + PADDING + PADDING; // tm.tmHeight + padding * 2
        var listBoxHeight = _maxListboxItems * _listBoxItemHeight + (PADDING * 2);
        var previewHeight = includePreview ? listBoxHeight : 0;

        // Get monitor info first to calculate width
        var hwnd = GetForegroundWindow();
        int contentWidth = 1200; // Fallback default width

        if (hwnd != IntPtr.Zero)
        {
            IntPtr hmon = MonitorFromWindow(hwnd, MONITOR_DEFAULTTONEAREST);
            if (hmon != IntPtr.Zero)
            {
                var mi = new MONITORINFO { cbSize = Marshal.SizeOf<MONITORINFO>() };
                if (GetMonitorInfo(hmon, ref mi))
                {
                    RECT work = mi.rcWork;
                    int monW = work.right - work.left;

                    // Calculate width as 2/3 of monitor width, minus margins and border
                    contentWidth = (int)((monW * 2.0 / 3.0) - (PANEL_MARGIN * 2) - BORDER_MARGIN);
                }
            }
        }

        // Calculate total window dimensions
        var totalWidth = contentWidth + (PANEL_MARGIN * 2) + BORDER_MARGIN;

        // Start with top and bottom margins, plus search input and listbox
        var totalHeight = (PANEL_MARGIN * 2) + searchInputHeight + PANEL_GAP + listBoxHeight + BORDER_MARGIN;
        if (includePreview)
        {
            totalHeight += previewHeight + PANEL_GAP;
        }

        // Get monitor info for centering (reuse if already obtained)
        if (hwnd != IntPtr.Zero)
        {
            IntPtr hmon = MonitorFromWindow(hwnd, MONITOR_DEFAULTTONEAREST);
            if (hmon != IntPtr.Zero)
            {
                var mi = new MONITORINFO { cbSize = Marshal.SizeOf<MONITORINFO>() };
                if (GetMonitorInfo(hmon, ref mi))
                {
                    RECT work = mi.rcWork;
                    int monW = work.right - work.left;
                    int monH = work.bottom - work.top;

                    // Center the window
                    int x = work.left + (monW - totalWidth) / 2;
                    int y = work.top + (monH - totalHeight) / 2;

                    return new ScreenLocation
                    {
                        x = x,
                        y = y,
                        width = totalWidth,
                        height = totalHeight
                    };
                }
            }
        }

        // Fallback to screen center
        return new ScreenLocation
        {
            x = 100,
            y = 100,
            width = totalWidth,
            height = totalHeight
        };
    }

    private void ResizeWindowForPreview(bool includePreview)
    {
        var screenLocation = CalculateOptimalWindowSize(includePreview);
        SetWindowPos(
            _rootHwnd,
            IntPtr.Zero,
            screenLocation.x,
            screenLocation.y,
            screenLocation.width,
            screenLocation.height,
            SetWindowPosFlags.SWP_NOREDRAW | SetWindowPosFlags.SWP_NOZORDER);

        // Reposition components based on preview visibility
        RepositionComponentsForPreview(includePreview);
    }

    private void RepositionComponentsForPreview(bool includePreview)
    {
        const int PANEL_GAP = 7;
        const int PANEL_MARGIN = 11;
        const int PADDING = 15;

        // Calculate component positions
        GetWindowRect(_rootHwnd, out RECT windowRect);
        int windowWidth = windowRect.right - windowRect.left;
        int panelWidth = windowWidth - (PANEL_MARGIN * 2);
        int panelHeight = _maxListboxItems * _listBoxItemHeight + (PADDING * 2);
        int searchInputHeight = _listBoxItemHeight + PADDING + PADDING;

        // Start with top margin to match left/right margins
        int currentY = PANEL_MARGIN;

        // Position preview panel (always at top when visible)
        if (includePreview)
        {
            SetWindowPos(_previewPanelHwnd, IntPtr.Zero,
                PANEL_MARGIN, currentY, panelWidth, panelHeight,
                SetWindowPosFlags.SWP_NOZORDER);
            currentY += panelHeight + PANEL_GAP;
        }

        // Position search input panel
        SetWindowPos(_textBoxPanelHwnd, IntPtr.Zero,
            PANEL_MARGIN, currentY, panelWidth, searchInputHeight,
            SetWindowPosFlags.SWP_NOZORDER);
        currentY += searchInputHeight + PANEL_GAP;

        // Position listbox panel
        SetWindowPos(_listBoxPanelHwnd, IntPtr.Zero,
            PANEL_MARGIN, currentY, panelWidth, panelHeight,
            SetWindowPosFlags.SWP_NOZORDER);
    }
        
    public void Run()
    {
        var screenLocation = CalculateOptimalWindowSize(false); // Start without preview
        var tmpBrush = CreateSolidBrush(BACKGROUND_COLOR);
        WNDCLASSEX wind_class = new WNDCLASSEX();
        wind_class.cbSize = (int)Marshal.SizeOf<WNDCLASSEX>();
        wind_class.style = (int)(CS_HREDRAW | CS_VREDRAW | CS_DBLCLKS );
        wind_class.hbrBackground = tmpBrush;
        wind_class.cbClsExtra = 0;
        wind_class.cbWndExtra = 0;
        wind_class.hInstance = GetModuleHandle(null);;
        wind_class.hIcon = IntPtr.Zero;
        wind_class.hCursor = LoadCursor(IntPtr.Zero, (int)IDC_ARROW);
        wind_class.lpszClassName = "nfmRoot";
        wind_class.lpfnWndProc = Marshal.GetFunctionPointerForDelegate(_delegWndProc);
        wind_class.hIconSm = IntPtr.Zero;
        ushort regResult = RegisterClassEx(ref wind_class);

        if (regResult == 0)
        {
            uint error = GetLastError();
            return;
        }

        var extendedStyle = WS_EX_LAYERED;
        if (!_excludeTopmost)
        {
            extendedStyle |= WS_EX_TOPMOST;
        }

        _rootHwnd = CreateWindowEx2(
            extendedStyle,
            wind_class.lpszClassName,
            "nfm",
            WS_POPUP,
            screenLocation.x,
            screenLocation.y,
            screenLocation.width,
            screenLocation.height,
            IntPtr.Zero,
            IntPtr.Zero,
            wind_class.hInstance,
            IntPtr.Zero);

        if (_rootHwnd == 0)
        {
            uint error = GetLastError();
            return;
        }

        _instance = wind_class.hInstance;
        SetLayeredWindowAttributes(_rootHwnd, BACKGROUND_COLOR + 10, 250, 0x00000001);
        UpdateWindow(_rootHwnd);
        
        if (_rootHwnd == 0)
        {
            uint error = GetLastError();
            return;
        }
        
        uint msg;
        while (GetMessage(out msg, IntPtr.Zero, 0, 0) != 0 && !_cts.IsCancellationRequested)
        {
            TranslateMessage(ref msg);
            DispatchMessage(ref msg);
        }
    }
    private static int MulDiv(int number, int numerator, int denominator)
    {
        return (int)((long)number * numerator / denominator);
    }

    private static IntPtr InitializeFont(string fontName, int size)
    {
        IntPtr screen = GetDC(IntPtr.Zero);
        int dpi = GetDeviceCaps(screen, LOGPIXELSX);
        ReleaseDC(IntPtr.Zero, screen);

        int scaledFontSize = -MulDiv(size, dpi, 96);

        return CreateFont(
            scaledFontSize,
            0,
            0,
            0,
            FW_NORMAL,
            0,
            0,
            0,
            DEFAULT_CHARSET,
            OUT_DEFAULT_PRECIS,
            CLIP_DEFAULT_PRECIS,
            DEFAULT_QUALITY,
            DEFAULT_PITCH | FF_DONTCARE,
            fontName);
    }

    private readonly Action _onInit;
    private Snapshot? _snapshot;
    private List<List<TextSegment>>? _lines;
    private int _previewVersion;
    private int _lastPreviewVersion;
    private readonly Lock _itemsLock = new();
    private IntPtr _font;

    private List<string> _autocompleteSuggestions = [];
    private int _autocompleteSelectedIndex = -1;
    private bool _autocompleteVisible = false;
    private IntPtr _rootHwnd;
    private IntPtr _textBoxHwnd;
    private IntPtr _textBoxPanelHwnd;
    private IntPtr _staticTextHwnd;
    private IntPtr _previewHwnd;
    private IntPtr _previewPanelHwnd;
    private IntPtr _listBoxHwnd;
    private IntPtr _listBoxPanelHwnd;
    private IntPtr _autocompleteHwnd;
    private IntPtr _autocompletePanelHwnd;
    private IntPtr _backgroundBrush;
    private IntPtr _autoCompleteBackgroundBrush;
    private IntPtr _gapPen;
    private IntPtr _borderPen;
    private IntPtr _selectedBackgroundBrush;
    private IntPtr _selectedBackgroundBrush2;
    private IntPtr _highlightBackgroundBrush;
    private IntPtr _instance;
    private readonly ViewModel _viewModel;
    private Timer? _timer;
    private bool _toastVisible;
    private string? _toastString;
    private long _toastExpirationTicks;
    private bool _showPreview;
    private readonly int _listboxItemPadding = 7;
    private int _maxListboxItems;
    private PreviewType _previewType;
    private Bitmap? _bitmap;
    private bool _hasHeader;
    private string? _headerText;
    private int _listBoxItemHeight;
    private int _previewItemHeight;
    private static CancellationTokenSource _cts = new();
    private static List<Win32Window> s_instances = new();
    private readonly bool _excludeTopmost;

    public Win32Window(ViewModel viewModel, Action onInit, bool excludeTopmost = false)
    {
        _excludeTopmost = excludeTopmost;
        _delegWndProc = MainWndProc;;
        _summaryTextControlProc = SummaryTextControlProc;
        _listBoxControlProc = ListBoxControlProc;
        _listBoxPanelControlProc = ListBoxPanelControlProc;
        _previewPanelControlProc = PreviewPanelControlProc;
        _previewControlProc = PreviewControlProc;
        _textBoxPanelControlProc = TextBoxPanelControlProc;
        _editControlProc = EditControlProc;
        _autocompleteControlProc = AutocompleteControlProc;
        _autocompletePanelControlProc = AutocompletePanelControlProc;
        _viewModel = viewModel;
        _onInit = onInit;
        _viewModel.SetView(this);
        s_instances.Add(this);
    }

    private IntPtr MainWndProc(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam)
    {
        IntPtr hdc;
        switch (msg)
        {
            case WM_CREATE:
                hdc = GetDC(hWnd);
                _font = InitializeFont("Consolas", 16);
                SelectObject(hdc, _font);
                GetTextMetrics(hdc, out var tm);
                ReleaseDC(hWnd, hdc);

                _borderPen = CreatePen(PS_SOLID, 1, BORDER_COLOR);
                _gapPen = CreatePen(PS_SOLID, 1, GAP_COLOR);
                _backgroundBrush = CreateSolidBrush(BACKGROUND_COLOR);
                _autoCompleteBackgroundBrush = CreateSolidBrush(BACKGROUND_COLOR);
                _selectedBackgroundBrush = CreateSolidBrush(SELECTED_BACKGROUND_COLOR);
                _selectedBackgroundBrush2 = CreateSolidBrush(SELECTED_BACKGROUND_COLOR_2);
                _highlightBackgroundBrush = CreateSolidBrush(SELECTED_BACKGROUND_COLOR_2);

                _maxListboxItems = 15;
                _listBoxItemHeight = (tm.tmHeight + (_listboxItemPadding * 2));
                _previewItemHeight = tm.tmHeight + 3;
                var controlHeight = _maxListboxItems * _listBoxItemHeight;
                var padding = 15;
                var panelX = 11;
                
                // Calculate panel width based on window dimensions
                GetWindowRect(hWnd, out RECT windowRect);
                int windowWidth = windowRect.right - windowRect.left;
                int panelWidth = windowWidth - (panelX * 2); // Leave margins on both sides
                int controlWidth = panelWidth - (padding * 2);
                var panelHeight = controlHeight + (padding * 2);
                var searchInputHeight = tm.tmHeight + padding + padding;
                var panelGap = 7;
                var previewItems = controlHeight / _previewItemHeight;
                _viewModel.SetPreviewHeight(controlHeight);
                _viewModel.SetNumberOfRows(_maxListboxItems);
                _viewModel.SetPreviewNumberOfRow(previewItems);
                
                const int WS_CLIPCHILDREN = 0x02000000;
                int rootStyle = GetWindowLong(_rootHwnd, GWL_STYLE);
                SetWindowLong(_rootHwnd, GWL_STYLE, rootStyle | WS_CLIPCHILDREN);
                
                _previewPanelHwnd = CreateWindowEx(
                    0,
                    "static",
                    "",
                    WS_VISIBLE | WS_CHILD | SS_RIGHT | SS_OWNERDRAW | WS_CLIPSIBLINGS,
                    panelX,
                    0,
                    panelWidth,
                    panelHeight,
                    hWnd,
                    1,
                    _instance,
                    IntPtr.Zero);
                SetWindowSubclass(_previewPanelHwnd, _previewPanelControlProc, 0, IntPtr.Zero);
                ShowWindow(_previewPanelHwnd, _showPreview ? 1 : 0);
                
                _previewHwnd = CreateWindowEx(
                    0,
                    "static",
                    "",
                    WS_VISIBLE | WS_CHILD | SS_RIGHT | SS_OWNERDRAW | WS_CLIPSIBLINGS,
                    padding,
                    padding,
                    controlWidth,
                    controlHeight,
                    _previewPanelHwnd,
                    2,
                    _instance,
                    IntPtr.Zero);
                SetWindowSubclass(_previewHwnd, _previewControlProc, 0, IntPtr.Zero);

                _textBoxPanelHwnd = CreateWindowEx(
                    0,
                    "static",
                    "",
                    WS_VISIBLE | WS_CHILD | SS_RIGHT | SS_OWNERDRAW | WS_CLIPSIBLINGS,
                    panelX,
                    panelHeight + panelGap,
                    panelWidth,
                    searchInputHeight,
                    hWnd,
                    3,
                    _instance,
                    IntPtr.Zero);
                SetWindowSubclass(_textBoxPanelHwnd, _textBoxPanelControlProc, 0, IntPtr.Zero);

                var summaryTextWidth = 400;
                var searchInputWidth = controlWidth - summaryTextWidth;
                _textBoxHwnd = CreateWindowEx(
                    0,
                    "edit",
                    "",
                    WS_CHILD | WS_VISIBLE | WS_TABSTOP,
                    padding,
                    padding,
                    searchInputWidth,
                    tm.tmHeight,
                    _textBoxPanelHwnd,
                    4,
                    _instance,
                    IntPtr.Zero);
                SetWindowSubclass(_textBoxHwnd, _editControlProc, 0, IntPtr.Zero);
                
                _staticTextHwnd = CreateWindowEx(
                    0,
                    "static",
                    "",
                    WS_VISIBLE | WS_CHILD | SS_RIGHT | SS_OWNERDRAW | WS_CLIPSIBLINGS,
                    padding + searchInputWidth,
                    padding,
                    summaryTextWidth,
                    tm.tmHeight,
                    _textBoxPanelHwnd,
                    5,
                    _instance,
                    IntPtr.Zero);
                SetWindowSubclass(_staticTextHwnd, _summaryTextControlProc, 0, IntPtr.Zero);

                // Create autocomplete panel (initially hidden) as child of root window for overlay
                var autocompleteExtendedStyle = _excludeTopmost ? 0u : WS_EX_TOPMOST;
                _autocompletePanelHwnd = CreateWindowEx(
                    autocompleteExtendedStyle,
                    "static",
                    "",
                    WS_CHILD | SS_OWNERDRAW | WS_CLIPSIBLINGS | WS_CLIPCHILDREN,
                    0, // Will be positioned in ShowAutocomplete
                    0,
                    searchInputWidth,
                    1, // Will be sized dynamically in ShowAutocomplete
                    hWnd, // Parent is root window
                    8,
                    _instance,
                    IntPtr.Zero);
                SetWindowSubclass(_autocompletePanelHwnd, _autocompletePanelControlProc, 0, IntPtr.Zero);

                // Create autocomplete content window as child of the panel
                _autocompleteHwnd = CreateWindowEx(
                    0,
                    "static",
                    "",
                    WS_CHILD | SS_OWNERDRAW | WS_CLIPSIBLINGS | WS_CLIPCHILDREN,
                    padding, // Inside the panel border
                    padding,
                    searchInputWidth - (padding * 2),
                    1, // Will be sized dynamically in ShowAutocomplete
                    _autocompletePanelHwnd, // Parent is the panel
                    9,
                    _instance,
                    IntPtr.Zero);
                SetWindowSubclass(_autocompleteHwnd, _autocompleteControlProc, 0, IntPtr.Zero);

                _listBoxPanelHwnd = CreateWindowEx(
                    0,
                    "static",
                    "",
                    WS_VISIBLE | WS_CHILD | SS_RIGHT | SS_OWNERDRAW | WS_CLIPSIBLINGS,
                    panelX,
                    panelHeight + panelGap + searchInputHeight + panelGap,
                    panelWidth,
                    panelHeight,
                    hWnd,
                    6,
                    _instance,
                    IntPtr.Zero);
                SetWindowSubclass(_listBoxPanelHwnd, _listBoxPanelControlProc, 0, IntPtr.Zero);
                    
                _listBoxHwnd = CreateWindowEx(
                    0,
                    "static",
                    "",
                    WS_VISIBLE | WS_CHILD | SS_RIGHT | SS_OWNERDRAW | WS_CLIPSIBLINGS,
                    padding,
                    padding,
                    controlWidth,
                    controlHeight,
                    _listBoxPanelHwnd,
                    7,
                    _instance,
                    IntPtr.Zero);
                SetWindowSubclass(_listBoxHwnd, _listBoxControlProc, 0, IntPtr.Zero);
                    
                long style = GetWindowLong(_listBoxHwnd, GWL_STYLE);
                style &= ~WS_BORDER;
                style &= ~WS_VSCROLL;
                SetWindowLong(_listBoxHwnd, GWL_STYLE, (int)style);

                int exStyle = GetWindowLong(_listBoxHwnd, GWL_EXSTYLE);
                exStyle &= ~WS_EX_CLIENTEDGE;
                SetWindowLong(_listBoxHwnd, GWL_EXSTYLE, exStyle);
                    
                SendMessage(_textBoxHwnd, WM_SETFONT, _font, 1);
                SetFocus(_textBoxHwnd);

                _timer = new Timer(s =>
                {
                    SendMessage(_rootHwnd, WM_SUMMARY_TIMER, IntPtr.Zero, IntPtr.Zero);
                    if (_toastVisible && DateTime.UtcNow.Ticks > _toastExpirationTicks)
                    {
                        Interlocked.Exchange(ref _toastVisible, false);
                        PostMessage(_rootHwnd, WM_ITEMS_UPDATED, IntPtr.Zero, IntPtr.Zero);
                    }
                }, null, 0, 70);

                if (_onInit == null)
                {
                    ExitProcess(1);
                    return 1;
                }

                PostMessage(hWnd, WM_INITALIZED, 0, 0);
                break;
            
            case WM_INITALIZED:
                _onInit();
                break;
                
            case WM_ERASEBKGND:
                break;
            
            case WM_LBUTTONDBLCLK :
                break;

            case WM_DESTROY:
                DestroyWindow(hWnd);
                ExitProcess(0);
                break;
            
            case WM_SUMMARY_TIMER:
                InvalidateRect(_staticTextHwnd, IntPtr.Zero, false);
                UpdateWindow(_staticTextHwnd);
                break;
                
            case WM_ITEMS_UPDATED:
                if (_snapshot != null)
                {
                    InvalidateRect(_listBoxHwnd, IntPtr.Zero, true);
                    UpdateWindow(_listBoxHwnd);
                }
                break;
            
            case WM_TOGGLE_PREVIEW:
                ShowWindow(_previewPanelHwnd, _showPreview ? 1 : 0);
                UpdateWindow(_listBoxPanelHwnd);
                UpdateWindow(_textBoxPanelHwnd);
                break;
            
            case WM_SHOW_ROOT:
                ShowRootWindow(lParam);
                break;
            
            case WM_SET_SEARCH_STRING:
                string? searchStr = Marshal.PtrToStringUni(lParam);
                if (searchStr != null)
                {
                    SetWindowText(_textBoxHwnd, searchStr);
                    SendMessage(_textBoxHwnd, EM_SETSEL, searchStr.Length - 1, searchStr.Length - 1);
                }

                Marshal.FreeHGlobal(lParam);

                break;
            
            case WM_SIZE:
                RedrawWindow(hWnd, IntPtr.Zero, IntPtr.Zero,
                    RDW_ERASE | RDW_INVALIDATE | RDW_ALLCHILDREN | RDW_FRAME);
                break;
            
            case WM_FOCUS_PREVIEW:
                SetFocus(_previewHwnd);
                break;
            
            case WM_FOCUS_SEARCH:
                SetFocus(_textBoxHwnd);
                break;
            
            case WM_SHOW_SUGGESTIONS:
                ShowAutocomplete();
                break;
            
            case WM_HIDE_SUGGESTIONS:
                HideAutocomplete();
                break;
            
            case WM_HIDE_ROOT:
                ClearUI();
                ShowWindow(_rootHwnd, 0);
                break;

            case WM_SETFOCUS:
                SetFocus(_textBoxHwnd);
                break;
        }
        return DefWindowProc(hWnd, msg, wParam, lParam);
    }

    private void ShowRootWindow(IntPtr lParam)
    {
        var showPreview = (int)lParam;
        if (showPreview == 1)
        {
            PostMessage(_rootHwnd, WM_TOGGLE_PREVIEW, 0, 0);
        }
        ResizeWindowForPreview(Convert.ToBoolean(showPreview));
        _timer?.Change(0, 70);

        if (_rootHwnd != IntPtr.Zero)
        {
            RedrawWindow(
                _rootHwnd,
                IntPtr.Zero,
                IntPtr.Zero,
                RDW_ERASE | RDW_INVALIDATE | RDW_ALLCHILDREN | RDW_FRAME | RDW_UPDATENOW);
        }

        InvalidateRect(_previewHwnd, IntPtr.Zero, true);
        InvalidateRect(_listBoxHwnd, IntPtr.Zero, true);
        InvalidateRect(_staticTextHwnd, IntPtr.Zero, true);
        ShowWindow(_rootHwnd, 1);
        _showPreview = Convert.ToBoolean(showPreview);
        ShowWindow(_previewPanelHwnd, showPreview);
        SetWindowText(_textBoxHwnd, string.Empty);
        SetFocus(_textBoxHwnd);
        SetListBoxItems();
        UpdateWindow(_rootHwnd);
        FocusStealer.BringToForeground(_rootHwnd);
    }

    public void SetListBoxItems()
    {
        var snapshot = new Snapshot();
        _viewModel.FillSnapshot(snapshot);

        lock (_itemsLock)
        {
            _snapshot = snapshot;
        }

        PostMessage(_rootHwnd, WM_ITEMS_UPDATED, IntPtr.Zero, IntPtr.Zero);
    }

    public void ShowToast(string text, int duration)
    {
        _toastExpirationTicks = DateTime.UtcNow.Add(TimeSpan.FromMilliseconds(duration)).Ticks;
        _toastString = text;
        Interlocked.Exchange(ref _toastVisible, true);
        PostMessage(_rootHwnd, WM_ITEMS_UPDATED, IntPtr.Zero, IntPtr.Zero);
    }

    public void SetPreviewLines(List<List<TextSegment>> lines)
    {
        _lines = lines.ToList();
        TriggerPreviewRender();
    }
    
    public void TriggerPreviewRender()
    {
        Interlocked.Increment(ref _previewVersion);
        Interlocked.Exchange(ref _previewType, PreviewType.Text);
        InvalidateRect(_previewHwnd, IntPtr.Zero, true);
    }

    public void TogglePreview(bool visible)
    {
        Interlocked.Exchange(ref _showPreview, visible);
        Interlocked.Exchange(ref _lastPreviewVersion, 0);
        ResizeWindowForPreview(visible);
        PostMessage(_rootHwnd, WM_TOGGLE_PREVIEW, IntPtr.Zero, IntPtr.Zero);
    }

    public void ShowImagePreview(Bitmap image)
    {
        _bitmap = image;
        Interlocked.Increment(ref _previewVersion);
        Interlocked.Exchange(ref _previewType, PreviewType.Image);
        InvalidateRect(_previewHwnd, IntPtr.Zero, true);
    }

    public void SetHeader(string text)
    {
        _hasHeader = true;
        _headerText = text;
        //This should really trigger a re-calc...
        var numberOfRows = _maxListboxItems - 1;
        _viewModel.SetNumberOfRows(numberOfRows);
    }

    public void HideHeader()
    {
        _hasHeader = false;
        _viewModel.SetNumberOfRows(_maxListboxItems);
    }

    public void Hide(bool quit)
    {
        PostMessage(_rootHwnd, WM_HIDE_ROOT, 0, 0);
        if (quit)
        {
            _cts.Cancel();
        }
    }

    public void Show(bool showPreview)
    {
        PostMessage(_rootHwnd, WM_SHOW_ROOT, 0, showPreview ? 1 : 0);
    }
    
    public void SetSearchString(string searchString)
    {
        IntPtr searchStrPtr = Marshal.StringToHGlobalUni(searchString);
        PostMessage(_rootHwnd, WM_SET_SEARCH_STRING, 0, searchStrPtr);
    }

    public void FocusPreview()
    {
        PostMessage(_rootHwnd, WM_FOCUS_PREVIEW, 0, 0);
    }

    public void FocusSearch()
    {
        PostMessage(_rootHwnd, WM_FOCUS_SEARCH, 0, 0);
    }

    public void ShowSuggestions(List<string> suggestions)
    {
        _autocompleteSuggestions = suggestions;
        PostMessage(_rootHwnd, WM_SHOW_SUGGESTIONS, 0, 0);
    }
    
    public void HideSuggestions()
    {
        PostMessage(_rootHwnd, WM_HIDE_SUGGESTIONS, 0, 0);
    }

    public (object Item, TerminalEscapedLine Text)? GetSelectedItem()
    {
        lock (_itemsLock)
        {
            if (_snapshot?.Items == null || _snapshot.SelectedIndex >= _snapshot.Items.Count ||
                _snapshot.SelectedIndex < 0)
            {
                return null;
            }
            return _snapshot.Items[_snapshot.SelectedIndex];
        }
    }
}