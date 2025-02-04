using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using nfm.menu;
using System.Drawing;
using System.Drawing.Drawing2D;

namespace Win32FromForms;

delegate IntPtr WndProc(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);

class Win32Window
{
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
    
    private const int CORNER_RADIUS = 7;
    [DllImport("user32.dll")]
    private static extern short GetAsyncKeyState(int vKey);

    private const int VK_LSHIFT = 0xA0;
    private const int VK_RSHIFT = 0xA1;
    private const int VK_LMENU = 0xA4; // Left Alt
    private const int VK_RMENU = 0xA5; // Right Alt
    private const int VK_LCONTROL = 0xA2;
    private const int VK_LWIN = 0x5B;
    private const int VK_RWIN = 0x5C;

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
    
    [DllImport("shlwapi.dll", CharSet = CharSet.Unicode)]
    public static extern int SHAutoComplete(IntPtr hwndEdit, uint dwFlags);

    public const uint SHACF_DEFAULT = 0;
    
    [DllImport("gdi32.dll", SetLastError = true)]
    private static extern bool RoundRect(
        IntPtr hdc,
        int left,
        int top,
        int right,
        int bottom,
        int ellipseWidth,
        int ellipseHeight);
    
    [StructLayout(LayoutKind.Sequential)]
    private struct NCCALCSIZE_PARAMS
    {
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 3)]
        public RECT[] rgrc;
        public IntPtr lppos;
    }
    
    private struct MONITORINFO
    {
        public int cbSize;
        public RECT rcMonitor;
        public RECT rcWork;
        public uint dwFlags;
    }
    
    [StructLayout(LayoutKind.Sequential)]
    private struct DRAWITEMSTRUCT
    {
        public int CtlType;
        public int CtlID;
        public int itemID;
        public int itemAction;
        public int itemState;
        public IntPtr hwndItem;
        public IntPtr hDC;
        public RECT rcItem;
        public IntPtr itemData;
    }
        
    [StructLayout(LayoutKind.Sequential)]
    struct COLORREF {
        public byte R;
        public byte G;
        public byte B;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct RECT
    {
        public int left, top, right, bottom;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct POINT
    {
        public int x, y;
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

    [DllImport("gdiplus.dll", CharSet = CharSet.Unicode)]
    static extern int GdipDrawImageI(IntPtr graphics, IntPtr image, int x, int y);
    
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

    [DllImport("user32.dll", SetLastError = true)]
    private static extern int MapWindowPoints(IntPtr hWndFrom, IntPtr hWndTo, ref POINT lpPoints, uint cPoints);

    [DllImport("gdi32.dll")]
    private static extern bool Rectangle(IntPtr hdc, int left, int top, int right, int bottom);
        
    [DllImport("gdi32.dll", CharSet = CharSet.Auto)]
    static extern bool GetTextMetrics(IntPtr hdc, out TEXTMETRIC lptm);
        
    [DllImport("user32.dll")]
    static extern bool GetClientRect(IntPtr hWnd, out RECT lpRect);
        
    [DllImport("Comctl32.dll", SetLastError = true)]
    private static extern IntPtr DefSubclassProc(IntPtr hWnd, uint uMsg, IntPtr wParam, IntPtr lParam);
    
    [DllImport("gdi32.dll")]
    public static extern bool BitBlt(
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
        
    [StructLayout(LayoutKind.Sequential)]
    public struct MEASUREITEMSTRUCT
    {
        public uint ctlType;
        public uint CtlID;
        public uint itemID;
        public uint itemWidth;
        public uint itemHeight;
        public IntPtr itemData;
    }
        
    private const int PS_SOLID = 0;
    private const int BORDER_THICKNESS = 2;
    private const int BORDER_COLOR = 0x00888545; //0x00bbggrr
    private const int BACKGROUND_COLOR = 0x00282828; //0x00bbggrr
    private const int SELECTED_BACKGROUND_COLOR = 0x00454950; //0x00bbggrr
    private const int TEXT_COLOR = 0x008499a8; //0x00bbggrr
    private const int SPINNER_COLOR = BORDER_COLOR;
    //private const int HIGHLIGHTED_TEXT_COLOR = 0x000e5dd6; //0x00bbggrr
    private const int HIGHLIGHTED_TEXT_COLOR = 0x0000a5ff; //ffa500

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

    public const uint ETO_OPAQUE = 0x0002;
    private const UInt32 WM_USER = 0x0400;
    private const UInt32 WM_ITEMS_UPDATED = WM_USER + 1;
    private const UInt32 WM_SUMMARY_TIMER = WM_USER + 2;
    private const UInt32 WM_TOGGLE_PREVIEW = WM_USER + 3;
    private const UInt32 WS_OVERLAPPEDWINDOW = 0xcf0000;
    private const UInt32 WS_VISIBLE = 0x10000000;
    private const UInt32 CS_USEDEFAULT = 0x80000000;
    private const UInt32 CS_DBLCLKS = 8;
    private const UInt32 CS_VREDRAW = 1;
    private const UInt32 CS_HREDRAW = 2;
    private const UInt32 COLOR_WINDOW = 5;
    private const UInt32 COLOR_BACKGROUND = 1;
    private const UInt32 IDC_CROSS = 32515;
    private const int IDC_ARROW = 32512;
    private const UInt32 WM_CTLCOLORLISTBOX = 0x0134;
    private const UInt32 WM_CTLCOLOREDIT = 0x0133;
    private const UInt32 WM_DESTROY = 2;
    private const UInt32 WM_PAINT = 0x0f;
    private const UInt32 WM_COMMAND = 0x0111;
    private const int EN_CHANGE = 0x0300;
    private const UInt32 WM_CREATE = 0x0001;
    private const int WM_ERASEBKGND = 0x14;
    private const int WM_DRAWITEM = 0x002B;
    private const int WM_MEASUREITEM = 0x002C;
    private const UInt32 WM_LBUTTONUP = 0x0202;
    private const UInt32 WM_LBUTTONDBLCLK = 0x0203;
    private const UInt32 WS_POPUP = 0x80000000;
    private const UInt32 WS_CHILD = 0x40000000;
    private const UInt32 WS_BORDER = 0x00800000;
    private const int WS_VSCROLL = 0x00200000;
    private const UInt32 WS_TABSTOP = 0x00010000;
    private const int ES_CENTER = 0x0001;
    private const int WS_EX_LAYERED = 0x00080000;
    private const int WS_EX_TOPMOST = 0x00000008;
    private const uint LWA_COLORKEY = 0x00000001;
    private const int LB_ADDSTRING = 0x0180;
    private const int LB_SETCURSEL = 0x0186;
    private const int LB_INSERTSTRING = 0x0181;
    private const int LB_DELETESTRING = 0x0182;
    private const int LBS_OWNERDRAWFIXED = 0x0010;
    private const int ODS_SELECTED     = 0x0001;
    const int LB_ERR = -1;
    const int LB_ERRSPACE = -2;
    private const int LB_RESETCONTENT = 0x0184;
    private const int WM_SETFONT = 0x30;
    private const int LB_SETITEMDATA = 0x019A;
    private const int LB_GETITEMDATA = 0x0199;
    private const int WM_GETFONT = 0x31;
    private const int DT_CENTER = 0x0001;
    private const int DT_VCENTER = 0x0004;
    private const int DT_SINGLELINE = 0x0020;
    public const int ODT_LISTBOX = 2;
    private const int WM_CHAR = 0x0102;
    private const int VK_CONTROL = 0x11;
    private const int VK_LEFT = 0x25;
    private const int VK_RIGHT = 0x27;
    private const int WM_NCPAINT = 0x0085;
    private const int WM_NCCALCSIZE = 0x0083;
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
    private const int LBS_STANDARD = 0x000000A00003; // Includes LBS_NOTIFY, LBS_SORT, WS_BORDER
    private const int LBS_HASSTRINGS = 0x00000040;
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
    private const int EM_REPLACESEL = 0x00C2;
    private const int MAX_TEXT_LENGTH = 1024;
    const int VK_BACK = 0x08;

    [DllImport("user32.dll", CharSet = CharSet.Auto)]
    private static extern int SendMessage(IntPtr hWnd, int msg, ref int wParam, ref int lParam);

    [DllImport("user32.dll", CharSet = CharSet.Auto)]
    private static extern int SendMessage(IntPtr hWnd, int msg, bool wParam, string lParam);

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
        public string lpszMenuName;
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
    
    [DllImport("user32.dll")]
    private static extern IntPtr GetWindowDC(IntPtr hWnd);

    [DllImport("gdi32.dll")]
    private static extern int GetDeviceCaps(IntPtr hdc, int nIndex);

    [DllImport("user32.dll")]
    private static extern int ReleaseDC(IntPtr hWnd, IntPtr hDC);

    private WndProc delegWndProc = MainWndProc;

    [DllImport("user32.dll")]
    static extern bool UpdateWindow(IntPtr hWnd);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);
        
    [DllImport("gdi32.dll", CharSet = CharSet.Auto)]
    static extern bool TextOut(IntPtr hdc, int nXStart, int nYStart,
        string lpString, int cbString);
    
    [DllImport("gdi32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    public static extern bool ExtTextOut(
        IntPtr hdc, 
        int x, 
        int y, 
        uint options, 
        IntPtr lprect, 
        string lpString, 
        int c, 
        IntPtr lpDx);

    [System.Runtime.InteropServices.DllImport("user32.dll", SetLastError = true)]
    static extern bool DestroyWindow(IntPtr hWnd);

    [DllImport("user32.dll", SetLastError = true, EntryPoint="CreateWindowEx")]  
    public static extern IntPtr CreateWindowEx2(  
        int dwExStyle,  
        UInt16 lpClassName,  
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
    static extern void PostQuitMessage(int nExitCode);

    [DllImport("user32.dll")]
    static extern sbyte GetMessage(out uint lpMsg, IntPtr hWnd, uint wMsgFilterMin,
        uint wMsgFilterMax);

    [DllImport("user32.dll")]
    static extern IntPtr LoadCursor(IntPtr hInstance, int lpCursorName);

    [DllImport("user32.dll")]
    static extern bool TranslateMessage([In] ref uint lpMsg);

    [DllImport("user32.dll")]
    static extern IntPtr DispatchMessage([In] ref uint lpmsg);
        
    [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    private static extern IntPtr SetWindowLongPtr(IntPtr hWnd, int nIndex, IntPtr dwNewLong);
        
    [DllImport("user32.dll", CharSet = CharSet.Auto)]
    private static extern int DrawText(IntPtr hdc, string lpString, int nCount, ref RECT lpRect, int uFormat);
        
    private delegate bool MonitorEnumProc(IntPtr hMonitor, IntPtr hdcMonitor, IntPtr lprcMonitor, IntPtr dwData);
    private delegate IntPtr SubclassProc(IntPtr hWnd, uint uMsg, IntPtr wParam, IntPtr lParam, uint uIdSubclass, IntPtr dwRefData);
    
    [DllImport("Comctl32.dll", SetLastError = true)]
    private static extern bool SetWindowSubclass(IntPtr hWnd, SubclassProc pfnSubclass, uint uIdSubclass, IntPtr dwRefData);
        
    [DllImport("user32.dll")]
    private static extern int GetWindowLong(IntPtr hWnd, int nIndex);

    [DllImport("user32.dll")]
    private static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);

    static int spinnerCtr = 0;

    private static IntPtr SummaryTextControlProc(IntPtr hWnd, uint uMsg, IntPtr wParam, IntPtr lParam, uint uIdSubclass, IntPtr dwRefData)
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

                spinnerBuffer[0] = spinner[spinnerCtr];
                spinnerBuffer[1] = ' ';
                spinnerBuffer[2] = '\0';

                int numberOfItemsMatched = _viewModel.NumberOfScoredItems;
                int numberOfItems = _viewModel.NumberOfItems;
                string countText = $"{numberOfItemsMatched}/{numberOfItems}";
                countText.CopyTo(0, countBuffer, 0, countText.Length);
                countBuffer[countText.Length] = '\0';

                if (spinnerCtr < spinner.Length - 1)
                {
                    spinnerCtr++;
                }
                else
                {
                    spinnerCtr = 0;
                }

                IntPtr hdc = BeginPaint(hWnd, out ps);

                SetTextAlign(hdc, TA_LEFT);
                FillRect(hdc, ref ps.rcPaint, BACKGROUND_BRUSH);
                SelectObject(hdc, font);
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
    
    private static IntPtr ListBoxControlProc(IntPtr hWnd, uint uMsg, IntPtr wParam, IntPtr lParam, uint uIdSubclass, IntPtr dwRefData)
    {
        PAINTSTRUCT ps;
        switch (uMsg)
        {
            case WM_PAINT:
                if (_snapshot == null || _viewModel == null)
                {
                    _ = BeginPaint(hWnd, out ps);
                    EndPaint(hWnd, ref ps);
                    return 0;
                }
                var hdc = BeginPaint(listBoxHwnd, out ps);

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
                
                FillRect(hNewDc, ref ps.rcPaint, BACKGROUND_BRUSH);

                var listBoxStartY = ps.rcPaint.top;
                SelectObject(hNewDc, font);
                
                if (_hasHeader)
                {
                    IntPtr hPen = CreatePen(PS_SOLID, 1, BORDER_COLOR);
                    IntPtr hOldPen = SelectObject(hNewDc, hPen);
                    SelectObject(hNewDc, font);

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
                    for (var i = 0; i < _snapshot.Items.Count; i++)
                    {
                        var itemTop = listBoxStartY + (i * _listBoxItemHeight);
                        var rcItem = new RECT
                        {
                            top = itemTop,
                            bottom = itemTop + _listBoxItemHeight,
                            left = ps.rcPaint.left,
                            right = ps.rcPaint.right
                        };
                        if (i == _snapshot.SelectedIndex)
                        {
                            FillRect(hNewDc, ref rcItem, SELECTED_BACKGROUND_BRUSH);
                            SetBkColor(hNewDc, SELECTED_BACKGROUND_COLOR);
                        }
                        else
                        {
                            FillRect(hNewDc, ref rcItem, BACKGROUND_BRUSH);
                            SetBkColor(hNewDc, BACKGROUND_COLOR);
                        }

                        SetTextColor(hNewDc, TEXT_COLOR);

                        int centeredY = rcItem.top + (_listBoxItemHeight / 2) - (textHeight / 2);

                        if (i < _snapshot.Items.Count)
                        {
                            TextOut(
                                hNewDc,
                                itemXOffset,
                                centeredY,
                                _snapshot.Items[i].Text,
                                _snapshot.Items[i].Text.Length);
                            SetTextColor(hNewDc, HIGHLIGHTED_TEXT_COLOR);
                            for (int j = 0; j < _snapshot.Items[i].Pos.Count; j++)
                            {
                                SIZE sz;
                                var textIndex = _snapshot.Items[i].Pos[j];
                                GetTextExtentPoint32(hNewDc, _snapshot.Items[i].Text, textIndex, out sz);
                                TextOut(
                                    hNewDc,
                                    sz.cx + itemXOffset,
                                    centeredY,
                                    _snapshot.Items[i].Text[textIndex].ToString(),
                                    1);
                            }
                        }

                        SetTextColor(hNewDc, TEXT_COLOR);
                    }
                }

                if (_toastVisible && _toastString != null)
                {
                    SetBkColor(hNewDc, BACKGROUND_COLOR);
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
                return 1;
            case WM_ERASEBKGND:
                return 0;
            default:
                return DefSubclassProc(hWnd, uMsg, wParam, lParam);
        }
    }
    
    private static IntPtr ListBoxPanelControlProc(IntPtr hWnd, uint uMsg, IntPtr wParam, IntPtr lParam, uint uIdSubclass, IntPtr dwRefData)
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
                        var text = new StringBuilder(GetWindowTextLength(textBoxHwnd) + 1);
                        GetWindowText(textBoxHwnd, text, text.Capacity);
                        Task.Run(() => _viewModel.SetSearchString(text.ToString()));
                    }
                }
                return 0;
            case WM_CTLCOLOREDIT:
            {
                var hdc = wParam;
                SetBkColor(hdc, BACKGROUND_COLOR);
                SetTextColor(hdc, TEXT_COLOR);
                return BACKGROUND_BRUSH;
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

    private static IntPtr TextBoxPanelControlProc(IntPtr hWnd, uint uMsg, IntPtr wParam, IntPtr lParam, uint uIdSubclass, IntPtr dwRefData)
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
                        var text = new StringBuilder(GetWindowTextLength(textBoxHwnd) + 1);
                        GetWindowText(textBoxHwnd, text, text.Capacity);
                        Task.Run(() => _viewModel.SetSearchString(text.ToString()));
                    }
                }
                return 0;
            case WM_CTLCOLOREDIT:
            {
                var hdc = wParam;
                SetBkColor(hdc, BACKGROUND_COLOR);
                SetTextColor(hdc, TEXT_COLOR);
                return BACKGROUND_BRUSH;
            }
            case WM_ERASEBKGND:
                return 0;
            default:
                return DefSubclassProc(hWnd, uMsg, wParam, lParam);
        }
    }
    
    private static IntPtr PreviewControlProc(IntPtr hWnd, uint uMsg, IntPtr wParam, IntPtr lParam, uint uIdSubclass, IntPtr dwRefData)
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
                    FillRect(imageHdc, ref imagePs.rcPaint, BACKGROUND_BRUSH);

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
                SelectObject(hNewDc, font);
                FillRect(hNewDc, ref ps.rcPaint, BACKGROUND_BRUSH);

                TEXTMETRIC tm;
                GetTextMetrics(hNewDc, out tm);
                for (var i = 0; i < _lines.Count; i++)
                {
                    var itemHeight = tm.tmHeight + 3;
                    var itemTop = rect.top + (i * itemHeight);
                    var rcItem = new RECT
                    {
                        top = itemTop,
                        bottom = itemTop + itemHeight,
                        left = rect.left,
                        right = rect.right
                    };

                    int textHeight = tm.tmHeight;
                    int centeredY = rcItem.top + (itemHeight - textHeight) / 2;

                    var line = _lines[i];

                    TextOut(hNewDc, rect.left, centeredY, line, line.Length);
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
        
    private static IntPtr EditControlProc(IntPtr hWnd, uint uMsg, IntPtr wParam, IntPtr lParam, uint uIdSubclass, IntPtr dwRefData)
    {
        switch (uMsg)
        {
            case WM_CHAR:
                if ((GetAsyncKeyState(VK_CONTROL) & 0x8000) != 0 && wParam != VK_LEFT && wParam != VK_RIGHT)
                {
                    return 0;
                }

                if (wParam == VK_RETURN)
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
                            lock (_itemsLock)
                            {
                                Task.Run(async () => { await _viewModel.OnReturn(); });
                            }
                            return 0;
                        case VK_ESCAPE:
                            _viewModel.OnEscape();
                            break;
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
        
    internal bool Create(ViewModel viewModel, Action onInit)
    {
        _onInit = onInit;
        IntPtr foregroundWindow = GetForegroundWindow();
        if (foregroundWindow == IntPtr.Zero)
        {
            Console.WriteLine("No active window found.");
            return false;
        }

        if (!GetWindowRect(foregroundWindow, out RECT windowRect))
        {
            Console.WriteLine("Failed to get window rect.");
            return false;
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
            return false;
        }

        MONITORINFO bestMonitorInfo = new MONITORINFO();
        bestMonitorInfo.cbSize = Marshal.SizeOf(bestMonitorInfo);
        if (!GetMonitorInfo(bestMonitor, ref bestMonitorInfo))
        {
            Console.WriteLine("Failed to get monitor info.");
            return false;
        }

        int monitorCenterX = (bestMonitorInfo.rcMonitor.left + bestMonitorInfo.rcMonitor.right) / 2;
        int monitorCenterY = (bestMonitorInfo.rcMonitor.top + bestMonitorInfo.rcMonitor.bottom) / 2;
        
        int windowWidth = 1250;
        int windowHeight = 630 * 2;

        int windowX = monitorCenterX - (windowWidth / 2);
        int windowY = monitorCenterY - (windowHeight / 2);
        
        _viewModel = viewModel;
        WNDCLASSEX wind_class = new WNDCLASSEX();
        wind_class.cbSize = Marshal.SizeOf<WNDCLASSEX>();
        wind_class.style = (int)(CS_HREDRAW | CS_VREDRAW | CS_DBLCLKS );
        wind_class.hbrBackground = (IntPtr) COLOR_BACKGROUND  + 1;
        wind_class.cbClsExtra = 0;
        wind_class.cbWndExtra = 0;
        wind_class.hInstance = Process.GetCurrentProcess().MainModule!.BaseAddress;
        wind_class.hIcon = IntPtr.Zero;
        wind_class.hCursor = LoadCursor(IntPtr.Zero, (int)IDC_ARROW);
        wind_class.lpszClassName = "myClass";
        wind_class.lpfnWndProc = Marshal.GetFunctionPointerForDelegate(delegWndProc);
        wind_class.hIconSm = IntPtr.Zero;
        ushort regResult = RegisterClassEx(ref wind_class);

        if (regResult == 0)
        {
            uint error = GetLastError();
            return false;
        }
        string wndClass = wind_class.lpszClassName;

        rootHwnd = CreateWindowEx2(
            WS_EX_LAYERED | WS_EX_TOPMOST,
            regResult,
            "nfm",
            WS_VISIBLE | WS_POPUP,
            windowX,
            windowY,
            windowWidth,
            windowHeight,
            IntPtr.Zero,
            IntPtr.Zero,
            wind_class.hInstance,
            IntPtr.Zero);

        if (rootHwnd == 0)
        {
            uint error = GetLastError();
            return false;
        }

        INSTANCE = wind_class.hInstance;
        SetLayeredWindowAttributes(rootHwnd, 0x000000, 0, 0x00000001);
        ShowWindow(rootHwnd, 1);
        UpdateWindow(rootHwnd);
        
        uint msg;
        while (GetMessage(out msg, IntPtr.Zero, 0, 0) != 0)
        {
            TranslateMessage(ref msg);
            DispatchMessage(ref msg);
        }

        return true;
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

    private static Action? _onInit;
    private static Snapshot? _snapshot;
    private static List<string>? _lines;
    private static int _previewVersion;
    private static int _lastPreviewVersion;
    private static readonly object _itemsLock = new();
    private static IntPtr font;
    private static IntPtr rootHwnd;
    private static IntPtr toastHwnd;
    private static IntPtr textBoxHwnd;
    private static IntPtr textBoxPanelHwnd;
    private static IntPtr staticTextHwnd;
    private static IntPtr previewHwnd;
    private static IntPtr previewPanelHwnd;
    private static IntPtr listBoxHwnd;
    private static IntPtr listBoxPanelHwnd;
    private static IntPtr BACKGROUND_BRUSH;
    private static IntPtr SELECTED_BACKGROUND_BRUSH;
    private static IntPtr INSTANCE;
    private static ViewModel? _viewModel;
    private static Timer? _timer;
    private static bool _toastVisible;
    private static string _toastString;
    private static long _toastExpirationTicks;
    private static bool _showPreview;
    private static int _listboxItemPadding = 7;
    private static int _maxListboxItems;
    private static PreviewType _previewType;
    private static Bitmap _bitmap;
    private static bool _hasHeader;
    public static string _headerText;
    private static int _listBoxItemHeight;

    private static IntPtr MainWndProc(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam)
    {
        IntPtr hdc;
        switch (msg)
        {
            case WM_CREATE:
                hdc = GetDC(hWnd);
                font = InitializeFont("Consolas", 16);
                SelectObject(hdc, font);
                GetTextMetrics(hdc, out var tm);
                ReleaseDC(hWnd, hdc);
                    
                BACKGROUND_BRUSH = CreateSolidBrush(BACKGROUND_COLOR);
                SELECTED_BACKGROUND_BRUSH = CreateSolidBrush(SELECTED_BACKGROUND_COLOR);

                _maxListboxItems = 15;
                _listBoxItemHeight = (tm.tmHeight + (_listboxItemPadding * 2));
                var controlHeight = _maxListboxItems * _listBoxItemHeight;
                var padding = 15;
                var panelX = 11;
                var panelWidth = 1226;
                var controlWidth = panelWidth - (padding * 2);
                var panelHeight = controlHeight + (padding * 2);
                var searchInputHeight = tm.tmHeight + padding + padding;
                var panelGap = 7;
                _viewModel.SetPreviewHeight(controlHeight);
                _viewModel.SetNumberOfRows(_maxListboxItems);
                
                previewPanelHwnd = CreateWindowEx(
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
                    INSTANCE,
                    IntPtr.Zero);
                SetWindowSubclass(previewPanelHwnd, PreviewPanelControlProc, 0, IntPtr.Zero);
                ShowWindow(previewPanelHwnd, _showPreview ? 1 : 0);
                
                previewHwnd = CreateWindowEx(
                    0,
                    "static",
                    "",
                    WS_VISIBLE | WS_CHILD | SS_RIGHT | SS_OWNERDRAW | WS_CLIPSIBLINGS,
                    padding,
                    padding,
                    controlWidth,
                    controlHeight,
                    previewPanelHwnd,
                    2,
                    INSTANCE,
                    IntPtr.Zero);
                SetWindowSubclass(previewHwnd, PreviewControlProc, 0, IntPtr.Zero);
                
                textBoxPanelHwnd = CreateWindowEx(
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
                    INSTANCE,
                    IntPtr.Zero);
                SetWindowSubclass(textBoxPanelHwnd, TextBoxPanelControlProc, 0, IntPtr.Zero);

                var summaryTextWidth = 400;
                var searchInputWidth = controlWidth - summaryTextWidth;
                textBoxHwnd = CreateWindowEx(
                    0,
                    "edit",
                    "",
                    WS_CHILD | WS_VISIBLE | WS_TABSTOP,
                    padding,
                    padding,
                    searchInputWidth,
                    tm.tmHeight,
                    textBoxPanelHwnd,
                    4,
                    INSTANCE,
                    IntPtr.Zero);
                SetWindowSubclass(textBoxHwnd, EditControlProc, 0, IntPtr.Zero);
                
                staticTextHwnd = CreateWindowEx(
                    0,
                    "static",
                    "",
                    WS_VISIBLE | WS_CHILD | SS_RIGHT | SS_OWNERDRAW | WS_CLIPSIBLINGS,
                    padding + searchInputWidth,
                    padding,
                    summaryTextWidth,
                    tm.tmHeight,
                    textBoxPanelHwnd,
                    5,
                    INSTANCE,
                    IntPtr.Zero);
                SetWindowSubclass(staticTextHwnd, SummaryTextControlProc, 0, IntPtr.Zero);
                
                listBoxPanelHwnd = CreateWindowEx(
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
                    INSTANCE,
                    IntPtr.Zero);
                SetWindowSubclass(listBoxPanelHwnd, ListBoxPanelControlProc, 0, IntPtr.Zero);
                    
                listBoxHwnd = CreateWindowEx(
                    0,
                    "static",
                    "",
                    WS_VISIBLE | WS_CHILD | SS_RIGHT | SS_OWNERDRAW | WS_CLIPSIBLINGS,
                    padding,
                    padding,
                    controlWidth,
                    controlHeight,
                    listBoxPanelHwnd,
                    7,
                    INSTANCE,
                    IntPtr.Zero);
                SetWindowSubclass(listBoxHwnd, ListBoxControlProc, 0, IntPtr.Zero);
                    
                long style = GetWindowLong(listBoxHwnd, GWL_STYLE);
                style &= ~WS_BORDER;
                style &= ~WS_VSCROLL;
                SetWindowLong(listBoxHwnd, GWL_STYLE, (int)style);

                int exStyle = GetWindowLong(listBoxHwnd, GWL_EXSTYLE);
                exStyle &= ~WS_EX_CLIENTEDGE;
                SetWindowLong(listBoxHwnd, GWL_EXSTYLE, exStyle);
                    
                SendMessage(textBoxHwnd, WM_SETFONT, font, 1);
                SetFocus(textBoxHwnd);

                _timer = new Timer(s =>
                {
                    SendMessage(rootHwnd, WM_SUMMARY_TIMER, IntPtr.Zero, IntPtr.Zero);
                    if (_toastVisible && DateTime.UtcNow.Ticks > _toastExpirationTicks)
                    {
                        Interlocked.Exchange(ref _toastVisible, false);
                        PostMessage(rootHwnd, WM_ITEMS_UPDATED, IntPtr.Zero, IntPtr.Zero);
                    }
                }, null, 0, 70);

                if (_onInit == null)
                {
                    Console.WriteLine("_onInit must be set");
                    ExitProcess(1);
                    return 1;
                }
                
                Task.Run(() => _onInit());
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
                InvalidateRect(staticTextHwnd, IntPtr.Zero, false);
                UpdateWindow(staticTextHwnd);
                break;
                
            case WM_ITEMS_UPDATED:
                if (_snapshot != null && _viewModel != null)
                {
                    InvalidateRect(listBoxHwnd, IntPtr.Zero, true);
                    UpdateWindow(listBoxHwnd);
                }
                break;
            
            case WM_TOGGLE_PREVIEW:
                ShowWindow(previewPanelHwnd, _showPreview ? 1 : 0);
                break;
        }
        return DefWindowProc(hWnd, msg, wParam, lParam);
    }
    
    public static void SetListBoxItems()
    {
        if (_viewModel == null)
        {
            return;
        }
        
        var snapshot = new Snapshot();
        _viewModel.FillSnapshot(snapshot, _maxListboxItems);

        lock (_itemsLock)
        {
            _snapshot = snapshot;
        }

        PostMessage(rootHwnd, WM_ITEMS_UPDATED, IntPtr.Zero, IntPtr.Zero);
    }

    public static void ShowToast(string text, int duration)
    {
        _toastExpirationTicks = DateTime.UtcNow.Add(TimeSpan.FromMilliseconds(duration)).Ticks;
        _toastString = text;
        Interlocked.Exchange(ref _toastVisible, true);
        PostMessage(rootHwnd, WM_ITEMS_UPDATED, IntPtr.Zero, IntPtr.Zero);
    }

    public static void SetPreviewLines(List<string> lines)
    {
        _lines = lines.ToList();
        Interlocked.Increment(ref _previewVersion);
        Interlocked.Exchange(ref _previewType, PreviewType.Text);
        InvalidateRect(previewHwnd, IntPtr.Zero, true);
    }

    public static void TogglePreview(bool visible)
    {
        Interlocked.Exchange(ref _showPreview, visible);
        Interlocked.Exchange(ref _lastPreviewVersion, 0);
        PostMessage(rootHwnd, WM_TOGGLE_PREVIEW, IntPtr.Zero, IntPtr.Zero);
    }

    public static void ShowImagePreview(Bitmap image)
    {
        _bitmap = image;
        Interlocked.Increment(ref _previewVersion);
        Interlocked.Exchange(ref _previewType, PreviewType.Image);
        InvalidateRect(previewHwnd, IntPtr.Zero, true);
    }

    public static void SetHeader(string text)
    {
        _hasHeader = true;
        _headerText = text;
        //This should really trigger a re-calc...
        _maxListboxItems -= 1;
        _viewModel.SetNumberOfRows(_maxListboxItems);
    }

    public static void HideHeader()
    {
        _hasHeader = false;
    }
}