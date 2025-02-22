using System.Runtime.InteropServices;
using System.Text;
using nfm.menu;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Runtime.Versioning;
using Core;

namespace Win32FromForms;

[SupportedOSPlatform("windows")]
public class Win32Window
{
    [DllImport("user32.dll")]
    private static extern short GetAsyncKeyState(int vKey);

    private const int VK_LSHIFT = 0xA0;
    private const int VK_RSHIFT = 0xA1;
    private const int VK_LMENU = 0xA4; // Left Alt
    private const int VK_RMENU = 0xA5; // Right Alt
    private const int VK_LCONTROL = 0xA2;
    private const int VK_LWIN = 0x5B;
    private const int VK_RWIN = 0x5C;
    
    private struct MONITORINFO
    {
        public int cbSize;
        public RECT rcMonitor;
        public RECT rcWork;
        public uint dwFlags;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct RECT
    {
        public int left, top, right, bottom;
    }

    [Serializable, StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
    public struct TEXTMETRIC
    {
        public int tmHeight;
        public int tmAscent;
        public int tmDescent;
        public int tmInternalLeading;
        public int tmExternalLeading;
        public int tmAveCharWidth;
        public int tmMaxCharWidth;
        public int tmWeight;
        public int tmOverhang;
        public int tmDigitizedAspectX;
        public int tmDigitizedAspectY;
        public ushort tmFirstChar;    // Changed from byte to ushort
        public ushort tmLastChar;     // Changed from byte to ushort
        public ushort tmDefaultChar;  // Changed from byte to ushort
        public ushort tmBreakChar;    // Changed from byte to ushort
        public byte tmItalic;
        public byte tmUnderlined;
        public byte tmStruckOut;
        public byte tmPitchAndFamily;
        public byte tmCharSet;
    }
        
    [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    public static extern IntPtr GetModuleHandle(string? lpModuleName);
    
    [DllImport("gdi32.dll")]
    private static extern bool MoveToEx(IntPtr hdc, int X, int Y, IntPtr lpPoint);

    [DllImport("gdi32.dll")]
    private static extern bool LineTo(IntPtr hdc, int nXEnd, int nYEnd);
    
    [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Auto)]
    private static extern bool SetWindowText(IntPtr hWnd, string lpString);
    
    [DllImport("gdi32.dll")]
    static extern IntPtr CreateCompatibleDC(IntPtr hdc);

    [DllImport("gdi32.dll")]
    static extern bool DeleteDC(IntPtr hdc);

    [DllImport("user32.dll")]
    private static extern IntPtr GetForegroundWindow();

    [DllImport("user32.dll")]
    private static extern bool EnumDisplayMonitors(IntPtr hdc, IntPtr lprcClip, MonitorEnumProc lpfnEnum, IntPtr dwData);

    [DllImport("user32.dll")]
    private static extern bool GetMonitorInfo(IntPtr hMonitor, ref MONITORINFO lpmi);
    
    [DllImport("UxTheme.dll", SetLastError = true)]
    private static extern IntPtr BeginBufferedPaint(IntPtr hdcTarget, ref RECT prcTarget,
        int dwFormat, IntPtr pPaintParams, out IntPtr phdc);

    [DllImport("UxTheme.dll", SetLastError = true)]
    private static extern int EndBufferedPaint(IntPtr hBufferedPaint, bool fUpdateTarget);
        
    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool InvalidateRect(IntPtr hWnd, IntPtr lpRect, bool bErase);
        
    [DllImport("user32.dll")]
    static extern int FillRect(IntPtr hDC, [In] ref RECT lprc, IntPtr hbr);
        
    [DllImport("user32.dll", SetLastError = true)]
    static extern IntPtr SetFocus(IntPtr hWnd);
        
    [DllImport("user32.dll", CharSet = CharSet.Auto)]
    public static extern IntPtr SendMessage(IntPtr hWnd, uint Msg, IntPtr wParam, IntPtr lParam);
        
    [return: MarshalAs(UnmanagedType.Bool)]
    [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Auto)]
    static extern bool PostMessage(IntPtr hWnd, uint Msg, IntPtr wParam, IntPtr lParam);
        
    [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Auto)]
    static extern int GetWindowText(IntPtr hWnd, StringBuilder lpString, int nMaxCount);

    [DllImport("user32.dll", SetLastError = true)]
    static extern int GetWindowTextLength(IntPtr hWnd);
    
    [DllImport("kernel32.dll")]
    static extern void ExitProcess(uint uExitCode);
        
    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);

    [DllImport("gdi32.dll", CharSet = CharSet.Auto)]
    static extern bool GetTextMetrics(IntPtr hdc, out TEXTMETRIC lptm);

    [DllImport("Comctl32.dll", SetLastError = true)]
    private static extern IntPtr DefSubclassProc(IntPtr hWnd, uint uMsg, IntPtr wParam, IntPtr lParam);
    
    [DllImport("gdi32.dll")]
    private static extern bool BitBlt(
        IntPtr hdcDest, int nXDest, int nYDest, int nWidth, int nHeight,
        IntPtr hdcSrc, int nXSrc, int nYSrc, int dwRop);
        
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    struct SIZE
    {
        public int cx;
        public int cy;
    }
        
    [DllImport("gdi32.dll")]
    static extern bool GetTextExtentPoint32(IntPtr hdc, string lpString,
        int cbString, out SIZE lpSize);
        
    [StructLayout(LayoutKind.Sequential)]
    private struct PAINTSTRUCT
    {
        public IntPtr hdc;
        public bool fErase;
        public RECT rcPaint;
        public bool fRestore;
        public bool fIncUpdate;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 32)]
        public byte[] rgbReserved;
    }

    [DllImport("user32.dll")]
    private static extern IntPtr BeginPaint(IntPtr hWnd, out PAINTSTRUCT lpPaint);

    [DllImport("user32.dll")]
    private static extern bool EndPaint(IntPtr hWnd, ref PAINTSTRUCT lpPaint);

    [DllImport("gdi32.dll")]
    private static extern IntPtr CreatePen(int fnPenStyle, int nWidth, uint crColor);

    [DllImport("gdi32.dll")]
    private static extern IntPtr SelectObject(IntPtr hdc, IntPtr hObject);

    [DllImport("gdi32.dll")]
    private static extern bool DeleteObject(IntPtr hObject);

    private const UInt32 WM_USER = 0x0400;
    private const UInt32 CS_DBLCLKS = 8;
    private const UInt32 CS_VREDRAW = 1;
    private const UInt32 CS_HREDRAW = 2;
    private const UInt32 COLOR_BACKGROUND = 1;
    private const int IDC_ARROW = 32512;
    private const UInt32 WM_CTLCOLOREDIT = 0x0133;
    private const UInt32 WM_DESTROY = 2;
    private const UInt32 WM_PAINT = 0x0f;
    private const UInt32 WM_COMMAND = 0x0111;
    private const int EN_CHANGE = 0x0300;
    private const UInt32 WM_CREATE = 0x0001;
    private const int WM_ERASEBKGND = 0x14;
    private const UInt32 WM_LBUTTONDBLCLK = 0x0203;
    private const UInt32 WS_POPUP = 0x80000000;
    private const UInt32 WS_CHILD = 0x40000000;
    private const UInt32 WS_BORDER = 0x00800000;
    private const int WS_VSCROLL = 0x00200000;
    private const UInt32 WS_TABSTOP = 0x00010000;
    private const int WS_EX_LAYERED = 0x00080000;
    private const int WS_EX_TOPMOST = 0x00000008;
    private const int WM_SETFONT = 0x30;
    private const int WM_CHAR = 0x0102;
    private const int VK_CONTROL = 0x11;
    private const int VK_LEFT = 0x25;
    private const int VK_RIGHT = 0x27;
    private const int SRCCOPY = 0x00CC0020;
        
    private const int LOGPIXELSX = 88;
    private const int FW_NORMAL = 400;
    private const uint DEFAULT_CHARSET = 1;
    private const uint OUT_DEFAULT_PRECIS = 0;
    private const uint CLIP_DEFAULT_PRECIS = 0;
    private const uint DEFAULT_QUALITY = 0;
    private const uint DEFAULT_PITCH = 0;
    private const uint FF_DONTCARE = 0;
    private const int TRANSPARENT = 1;
    private const int GWL_STYLE = -16;
    private const int GWL_EXSTYLE = -20;
    private const int WS_EX_CLIENTEDGE = 0x00000200;
    private const int BPBF_COMPATIBLEBITMAP = 0;
    private const int WS_CLIPSIBLINGS = 0x04000000;
    private const int WM_KEYDOWN = 0x0100;
    private const int SS_RIGHT = 0x00000002;
    private const int SS_OWNERDRAW = 0x0000000D;
        
    private const int VK_PAGEDOWN = 0x22;
    private const int VK_PAGEUP = 0x21;
    private const int VK_DOWN = 0x28;
    private const int VK_UP = 0x26;
    private const ushort VK_RETURN = 0x0D;
    private const ushort VK_ESCAPE = 0x1B;
    private const int EM_GETSEL = 0x00B0;
    private const int EM_SETSEL = 0x00B1;
    const int VK_BACK = 0x08;

    [DllImport("user32.dll", CharSet = CharSet.Auto)]
    private static extern int SendMessage(IntPtr hWnd, int msg, ref int wParam, ref int lParam);

    [DllImport("gdi32.dll")]
    private static extern int SetBkColor(IntPtr hdc, int color);

    [DllImport("gdi32.dll")]
    private static extern int SetTextColor(IntPtr hdc, int color);

    [DllImport("gdi32.dll")]
    private static extern IntPtr CreateSolidBrush(int color);
        
    [DllImport("user32.dll")]
    public static extern bool SetLayeredWindowAttributes(IntPtr hwnd, uint crKey, byte bAlpha, uint dwFlags);
        
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
    struct WNDCLASSEX
    {
        [MarshalAs(UnmanagedType.U4)]
        public int cbSize;
        [MarshalAs(UnmanagedType.U4)]
        public int style;
        public IntPtr lpfnWndProc;
        public int cbClsExtra;
        public int cbWndExtra;
        public IntPtr hInstance;
        public IntPtr hIcon;
        public IntPtr hCursor;
        public IntPtr hbrBackground;
        [MarshalAs(UnmanagedType.LPStr)]
        public string lpszMenuName;
        [MarshalAs(UnmanagedType.LPStr)]
        public string lpszClassName;
        public IntPtr hIconSm;
    }

    [DllImport("gdi32.dll", CharSet = CharSet.Unicode)]
    private static extern IntPtr CreateFont(
        int nHeight, int nWidth, int nEscapement, int nOrientation, int fnWeight,
        uint fdwItalic, uint fdwUnderline, uint fdwStrikeOut, uint fdwCharSet,
        uint fdwOutputPrecision, uint fdwClipPrecision, uint fdwQuality,
        uint fdwPitchAndFamily, string lpszFace);

    [DllImport("user32.dll")]
    private static extern IntPtr GetDC(IntPtr hWnd);

    [DllImport("gdi32.dll")]
    private static extern int GetDeviceCaps(IntPtr hdc, int nIndex);

    [DllImport("user32.dll")]
    private static extern int ReleaseDC(IntPtr hWnd, IntPtr hDC);

    [DllImport("user32.dll")]
    static extern bool UpdateWindow(IntPtr hWnd);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);
    
    [DllImport("user32.dll", SetLastError = true)]
    public static extern bool EnableWindow(IntPtr hWnd, bool bEnable);
        
    [DllImport("gdi32.dll", CharSet = CharSet.Auto)]
    static extern bool TextOut(IntPtr hdc, int nXStart, int nYStart,
        string lpString, int cbString);

    [DllImport("user32.dll", SetLastError = true)]
    static extern bool DestroyWindow(IntPtr hWnd);

    [DllImport("user32.dll", SetLastError = true, EntryPoint = "CreateWindowEx")]
    public static extern IntPtr CreateWindowEx2(
       int dwExStyle,
       [MarshalAs(UnmanagedType.LPStr)]
       string lpClassName,
       [MarshalAs(UnmanagedType.LPStr)]
       string lpWindowName,
       UInt32 dwStyle,
       int x,
       int y,
       int nWidth,
       int nHeight,
       IntPtr hWndParent,
       IntPtr hMenu,
       IntPtr hInstance,
       IntPtr lpParam);
        
    [DllImport("user32.dll", SetLastError = true, EntryPoint = "CreateWindowEx")]
    static extern IntPtr CreateWindowEx(
        uint dwExStyle,
        string lpClassName,
        string lpWindowName,
        uint dwStyle,
        int x,
        int y,
        int nWidth,
        int nHeight,
        IntPtr hWndParent,
        IntPtr hMenu,
        IntPtr hInstance,
        IntPtr lpParam);
        
    private const uint TA_LEFT = 0x0000;
    [DllImport("gdi32.dll", SetLastError = true)]
    private static extern uint SetTextAlign(IntPtr hdc, uint fMode);
    
    [DllImport("gdi32.dll")]
    static extern int SetBkMode(IntPtr hdc, int iBkMode);
        
    [DllImport("user32.dll", SetLastError = true, EntryPoint = "RegisterClassEx")]
    static extern System.UInt16 RegisterClassEx([In] ref WNDCLASSEX lpWndClass);

    [DllImport("kernel32.dll")]
    static extern uint GetLastError();

    [DllImport("user32.dll")]
    static extern IntPtr DefWindowProc(IntPtr hWnd, uint uMsg, IntPtr wParam, IntPtr lParam);

    [DllImport("user32.dll")]
    static extern sbyte GetMessage(out uint lpMsg, IntPtr hWnd, uint wMsgFilterMin, uint wMsgFilterMax);

    [DllImport("user32.dll")]
    static extern IntPtr LoadCursor(IntPtr hInstance, int lpCursorName);

    [DllImport("user32.dll")]
    static extern bool TranslateMessage([In] ref uint lpMsg);

    [DllImport("user32.dll")]
    static extern IntPtr DispatchMessage([In] ref uint lpmsg);
    
    [DllImport("Comctl32.dll", SetLastError = true)]
    private static extern bool SetWindowSubclass(IntPtr hWnd, SubclassProc pfnSubclass, uint uIdSubclass, IntPtr dwRefData);
        
    [DllImport("user32.dll")]
    private static extern int GetWindowLong(IntPtr hWnd, int nIndex);

    [DllImport("user32.dll")]
    private static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);
    
    delegate IntPtr WndProc(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);
    private readonly WndProc _delegWndProc;
    private readonly SubclassProc _summaryTextControlProc;
    private readonly SubclassProc _listBoxControlProc;
    private readonly SubclassProc _listBoxPanelControlProc;
    private readonly SubclassProc _previewPanelControlProc;
    private readonly SubclassProc _previewControlProc;
    private readonly SubclassProc _textBoxPanelControlProc;
    private readonly SubclassProc _editControlProc;
    private delegate bool MonitorEnumProc(IntPtr hMonitor, IntPtr hdcMonitor, IntPtr lprcMonitor, IntPtr dwData);
    private delegate IntPtr SubclassProc(IntPtr hWnd, uint uMsg, IntPtr wParam, IntPtr lParam, uint uIdSubclass, IntPtr dwRefData);

    private int _spinnerCtr = 0;
    private const int PS_SOLID = 0;
    private const int BORDER_THICKNESS = 2;
    private const int BORDER_COLOR = 0x00888545; //0x00bbggrr
    private const int BACKGROUND_COLOR = 0x00282828; //0x00bbggrr
    private const int SELECTED_BACKGROUND_COLOR = 0x00454950; //0x00bbggrr
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
    private const UInt32 WS_VISIBLE = 0x10000000;
    
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
                if (_viewModel == null)
                {
                    _ = BeginPaint(hWnd, out ps);
                    EndPaint(hWnd, ref ps);
                    return 0;
                }
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

                SetTextAlign(hdc, TA_LEFT);
                FillRect(hdc, ref ps.rcPaint, _backgroundBrush);
                SelectObject(hdc, _font);
                SetBkMode(hdc, TRANSPARENT);

                SIZE sz;
                int width = ps.rcPaint.right - ps.rcPaint.left;
                SetTextColor(hdc, TEXT_COLOR);

                GetTextExtentPoint32(hdc, new string(countBuffer), countText.Length, out sz);
                int offset = width - sz.cx - 2;

                TextOut(hdc, offset, 2, new string(countBuffer), countText.Length);

                if (_viewModel.Reading || _viewModel.Searching)
                {
                    GetTextExtentPoint32(hdc, new string(spinnerBuffer), 1, out sz);
                    SetTextColor(hdc, SPINNER_COLOR);
                    TextOut(hdc, offset - sz.cx - 5, 2, new string(spinnerBuffer), 1);
                }

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
                if (_snapshot == null)
                {
                    _ = BeginPaint(hWnd, out ps);
                    EndPaint(hWnd, ref ps);
                    return 0;
                }
                
                IntPtr gapPen = CreatePen(PS_SOLID, 1, GAP_COLOR);
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
                    IntPtr hPen = CreatePen(PS_SOLID, 1, BORDER_COLOR);
                    IntPtr hOldPen = SelectObject(hNewDc, hPen);
                    SelectObject(hNewDc, _font);

                    SetBkColor(hNewDc, BACKGROUND_COLOR);
                    SetTextColor(hNewDc, HIGHLIGHTED_TEXT_COLOR);
                    TextOut(
                        hNewDc,
                        5,
                        0,
                        _headerText,
                        _headerText.Length);
                
                    MoveToEx(hNewDc, 0, textHeight + ListboxItemPadding, IntPtr.Zero);
                    LineTo(hNewDc, ps.rcPaint.right, textHeight + ListboxItemPadding);
                    listBoxStartY = _listBoxItemHeight;
                }
                
                //Offset the x of item so that selected item background has some padding
                var itemXOffset = 5;
                lock (ItemsLock)
                {
                    var nextItemY = 0;
                    for (var i = 0; i < _snapshot.Items.Count; i++)
                    {
                        var item = _snapshot.Items[i];
                        var startLine = 0;
                        var linesToRender = _snapshot.WrapLines ? item.WrappedLines() : item.Lines;
                        var itemLines = linesToRender.Count;
                        if (i == 0)
                        {
                            itemLines = linesToRender.Count - _snapshot.StartLinesToClip;
                            startLine = _snapshot.StartLinesToClip;
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
                        if (i == _snapshot.SelectedIndex)
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

                        if (i < _snapshot.Items.Count)
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
                        
                        if (_snapshot.ShowGap)
                        {
                            SelectObject(hNewDc, gapPen);
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
                        g.SmoothingMode = SmoothingMode.AntiAlias;

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
                if (_viewModel != null)
                {
                    var notificationCode = (ushort)((wParam.ToInt64() >> 16) & 0xFFFF);
                    if (notificationCode == EN_CHANGE)
                    {
                        var text = new StringBuilder(GetWindowTextLength(_textBoxHwnd) + 1);
                        GetWindowText(_textBoxHwnd, text, text.Capacity);
                        _viewModel.SetSearchString(text.ToString());
                    }
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

    private static IntPtr PaintPanelBorder(IntPtr hWnd)
    {
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

        using (Graphics g = Graphics.FromHdc(hNewDc))
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;

            Rectangle rect = new Rectangle(
                ps.rcPaint.left + BORDER_THICKNESS,
                ps.rcPaint.top + BORDER_THICKNESS,
                ps.rcPaint.right - ps.rcPaint.left - (BORDER_THICKNESS * 2),
                ps.rcPaint.bottom - ps.rcPaint.top - (BORDER_THICKNESS * 2)
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
                if (_viewModel != null)
                {
                    var notificationCode = (ushort)((wParam.ToInt64() >> 16) & 0xFFFF);
                    if (notificationCode == EN_CHANGE)
                    {
                        var text = new StringBuilder(GetWindowTextLength(_textBoxHwnd) + 1);
                        GetWindowText(_textBoxHwnd, text, text.Capacity);
                        Task.Run(() => _viewModel.SetSearchString(text.ToString()));
                    }
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
    
    private IntPtr PreviewControlProc(IntPtr hWnd, uint uMsg, IntPtr wParam, IntPtr lParam, uint uIdSubclass, IntPtr dwRefData)
    {
        switch (uMsg)
        {
            case WM_PAINT:
            {
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
                    return 1;
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

                    var xOffset = 0;
                    foreach (var segment in line)
                    {
                        SIZE sz;
                        GetTextExtentPoint32(hNewDc, segment.Text, segment.Text.Length, out sz);
                        var color = segment.State.Foreground;
                        int colorRef = (color.B << 16) | (color.G << 8) | color.R;
                        var backgroundColor = segment.State.Background;
                        int backgroundColorRef = (backgroundColor.B << 16) | (backgroundColor.G << 8) | backgroundColor.R;
                        SetTextColor(hNewDc, colorRef);
                        SetBkColor(hNewDc, backgroundColorRef);
                        TextOut(hNewDc, rect.left + xOffset, centeredY, segment.Text, segment.Text.Length);
                        xOffset += sz.cx;
                    }
                }

                EndBufferedPaint(hBufferedPaint, true);
            }
                return 1;
            case WM_ERASEBKGND:
                return 0;
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
                if (_snapshot == null || _viewModel == null)
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
                        Task.Run(() => _viewModel.HandleKeyUp((int)wParam, modifiers));
                        return 0;
                    }
                }
                else
                {
                    switch (wParam)
                    {
                        case VK_DOWN:
                            _viewModel.SelectNext();
                            return 0;
                        case VK_UP:
                            _viewModel.SelectPrevious();
                            return 0;
                        case VK_PAGEDOWN:
                            _viewModel.SelectPageDown();
                            return 0;
                        case VK_PAGEUP:
                            _viewModel.SelectPageUp();
                            return 0;
                        case VK_RETURN:
                            lock (ItemsLock)
                            {
                                Task.Run(async () => { await _viewModel.OnReturn(); });
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
        
    public void Run()
    {
        IntPtr foregroundWindow = GetForegroundWindow();
        if (foregroundWindow == IntPtr.Zero)
        {
            Console.WriteLine("No active window found.");
            return;
        }

        if (!GetWindowRect(foregroundWindow, out RECT windowRect))
        {
            Console.WriteLine("Failed to get window rect.");
            return;
        }

        IntPtr bestMonitor = IntPtr.Zero;
        int bestOverlap = 0;

        EnumDisplayMonitors(IntPtr.Zero, IntPtr.Zero, (hMonitor, hdcMonitor, lprcMonitor, dwData) =>
        {
            MONITORINFO mi = new MONITORINFO();
            mi.cbSize = Marshal.SizeOf(mi);

            if (GetMonitorInfo(hMonitor, ref mi))
            {
                RECT monitorRect = mi.rcMonitor;

                int overlapWidth = Math.Min(windowRect.right, monitorRect.right) - Math.Max(windowRect.left, monitorRect.left);
                int overlapHeight = Math.Min(windowRect.bottom, monitorRect.bottom) - Math.Max(windowRect.top, monitorRect.top);

                if (overlapWidth > 0 && overlapHeight > 0)
                {
                    int overlapArea = overlapWidth * overlapHeight;
                    if (overlapArea > bestOverlap)
                    {
                        bestOverlap = overlapArea;
                        bestMonitor = hMonitor;
                    }
                }
            }

            return true;
        }, IntPtr.Zero);

        if (bestMonitor == IntPtr.Zero)
        {
            Console.WriteLine("No monitor found for the focused window.");
            return;
        }

        MONITORINFO bestMonitorInfo = new MONITORINFO();
        bestMonitorInfo.cbSize = Marshal.SizeOf(bestMonitorInfo);
        if (!GetMonitorInfo(bestMonitor, ref bestMonitorInfo))
        {
            Console.WriteLine("Failed to get monitor info.");
            return;
        }

        int monitorCenterX = (bestMonitorInfo.rcMonitor.left + bestMonitorInfo.rcMonitor.right) / 2;
        int monitorCenterY = (bestMonitorInfo.rcMonitor.top + bestMonitorInfo.rcMonitor.bottom) / 2;
        
        int windowWidth = 1250;
        int windowHeight = 630 * 2;

        int windowX = monitorCenterX - (windowWidth / 2);
        int windowY = monitorCenterY - (windowHeight / 2);
        
        WNDCLASSEX wind_class = new WNDCLASSEX();
        wind_class.cbSize = (int)Marshal.SizeOf<WNDCLASSEX>();
        wind_class.style = (int)(CS_HREDRAW | CS_VREDRAW | CS_DBLCLKS );
        wind_class.hbrBackground = (IntPtr) COLOR_BACKGROUND  + 1;
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

        _rootHwnd = CreateWindowEx2(
            WS_EX_LAYERED | WS_EX_TOPMOST,
            wind_class.lpszClassName,
            "nfm",
             WS_POPUP,
            windowX,
            windowY,
            windowWidth,
            windowHeight,
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
        SetLayeredWindowAttributes(_rootHwnd, 0x000000, 0, 0x00000001);
        UpdateWindow(_rootHwnd);
        
        if (_rootHwnd == 0)
        {
            uint error = GetLastError();
            return;
        }
        
        uint msg;
        _cts = new CancellationTokenSource();
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
    private readonly object ItemsLock = new();
    private IntPtr _font;
    private IntPtr _rootHwnd;
    private IntPtr _textBoxHwnd;
    private IntPtr _textBoxPanelHwnd;
    private IntPtr _staticTextHwnd;
    private IntPtr _previewHwnd;
    private IntPtr _previewPanelHwnd;
    private IntPtr _listBoxHwnd;
    private IntPtr _listBoxPanelHwnd;
    private IntPtr _backgroundBrush;
    private IntPtr _selectedBackgroundBrush;
    private IntPtr _instance;
    private readonly ViewModel _viewModel;
    private Timer? _timer;
    private bool _toastVisible;
    private string? _toastString;
    private long _toastExpirationTicks;
    private bool _showPreview;
    private int ListboxItemPadding = 7;
    private int _maxListboxItems;
    private PreviewType _previewType;
    private Bitmap? _bitmap;
    private bool _hasHeader;
    private string? _headerText;
    private int _listBoxItemHeight;
    private int _previewItemHeight;
    public static CancellationTokenSource _cts;
    private static List<Win32Window> s_instances = new();

    public Win32Window(ViewModel viewModel, Action onInit)
    {
        _delegWndProc = MainWndProc;;
        _summaryTextControlProc = SummaryTextControlProc;
        _listBoxControlProc = ListBoxControlProc;
        _listBoxPanelControlProc = ListBoxPanelControlProc;
        _previewPanelControlProc = PreviewPanelControlProc;
        _previewControlProc = PreviewControlProc;
        _textBoxPanelControlProc = TextBoxPanelControlProc;
        _editControlProc = EditControlProc;
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
                    
                _backgroundBrush = CreateSolidBrush(BACKGROUND_COLOR);
                _selectedBackgroundBrush = CreateSolidBrush(SELECTED_BACKGROUND_COLOR);

                _maxListboxItems = 15;
                _listBoxItemHeight = (tm.tmHeight + (ListboxItemPadding * 2));
                _previewItemHeight = tm.tmHeight + 3;
                var controlHeight = _maxListboxItems * _listBoxItemHeight;
                var padding = 15;
                var panelX = 11;
                var panelWidth = 1226;
                var controlWidth = panelWidth - (padding * 2);
                var panelHeight = controlHeight + (padding * 2);
                var searchInputHeight = tm.tmHeight + padding + padding;
                var panelGap = 7;
                var previewItems = controlHeight / _previewItemHeight;
                _viewModel.SetPreviewHeight(controlHeight);
                _viewModel.SetNumberOfRows(_maxListboxItems);
                _viewModel.SetPreviewNumberOfRow(previewItems);
                
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
                break;
            
            case WM_SHOW_ROOT:
                var showPreview = (int)lParam;
                if (showPreview == 1)
                {
                    PostMessage(_rootHwnd, WM_TOGGLE_PREVIEW, 0, 0);
                }
                ShowWindow(_rootHwnd, 1);
                _showPreview = Convert.ToBoolean(showPreview);
                ShowWindow(_previewPanelHwnd, showPreview);
                SetWindowText(_textBoxHwnd, string.Empty);
                SetFocus(_textBoxHwnd);
                SetListBoxItems();
                UpdateWindow(_rootHwnd);
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
            
            case WM_HIDE_ROOT:
                ShowWindow(_rootHwnd, 0);
                break;
        }
        return DefWindowProc(hWnd, msg, wParam, lParam);
    }
    
    public void SetListBoxItems()
    {
        var snapshot = new Snapshot();
        _viewModel.FillSnapshot(snapshot);

        lock (ItemsLock)
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
        Interlocked.Increment(ref _previewVersion);
        Interlocked.Exchange(ref _previewType, PreviewType.Text);
        InvalidateRect(_previewHwnd, IntPtr.Zero, true);
    }

    public void TogglePreview(bool visible)
    {
        Interlocked.Exchange(ref _showPreview, visible);
        Interlocked.Exchange(ref _lastPreviewVersion, 0);
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
        _maxListboxItems -= 1;
        _viewModel.SetNumberOfRows(_maxListboxItems);
    }

    public void HideHeader()
    {
        _hasHeader = false;
    }

    public void Hide()
    {
        PostMessage(_rootHwnd, WM_HIDE_ROOT, 0, 0);
        _cts.Cancel();
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
}