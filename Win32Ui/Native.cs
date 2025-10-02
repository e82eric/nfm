using System.Runtime.InteropServices;
using System.Text;

namespace nfm.Win32Ui;

internal static class Native
{
    [DllImport("user32.dll")]
    internal static extern IntPtr MonitorFromRect(ref RECT lprc, uint dwFlags);
    internal const uint MONITOR_DEFAULTTONEAREST = 2;
    internal static readonly IntPtr HWND_TOP = new IntPtr(0);
    
    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool SetWindowPos(
        IntPtr hWnd,
        IntPtr hWndInsertAfter,
        int X,
        int Y,
        int cx,
        int cy,
        SetWindowPosFlags uFlags);

    [Flags]
    internal enum SetWindowPosFlags : uint
    {
        SWP_NOSIZE = 0x0001,
        SWP_NOMOVE = 0x0002,
        SWP_NOZORDER = 0x0004,
        SWP_NOREDRAW = 0x0008,
        SWP_NOACTIVATE = 0x0010,
        SWP_FRAMECHANGED = 0x0020,
        SWP_SHOWWINDOW = 0x0040,
        SWP_HIDEWINDOW = 0x0080,
        SWP_NOCOPYBITS = 0x0100,
        SWP_NOOWNERZORDER = 0x0200,
        SWP_NOSENDCHANGING = 0x0400,
        SWP_DEFERERASE = 0x2000,
        SWP_ASYNCWINDOWPOS = 0x4000,
    }
    
    // P/Invokes
    [DllImport("user32.dll", SetLastError = true)]
    public static extern IntPtr BeginDeferWindowPos(int nNumWindows);

    [DllImport("user32.dll", SetLastError = true)]
    public static extern IntPtr DeferWindowPos(
        IntPtr hWinPosInfo,     // HDWP from BeginDeferWindowPos
        IntPtr hWnd,            // window to move/size
        IntPtr hWndInsertAfter, // Z-order target (e.g., HWND_TOP)
        int x,
        int y,
        int cx,
        int cy,
        SetWindowPosFlags uFlags);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool EndDeferWindowPos(IntPtr hWinPosInfo);

    [DllImport("user32.dll")]
    internal static extern short GetAsyncKeyState(int vKey);

    internal const int VK_LSHIFT = 0xA0;
    internal const int VK_RSHIFT = 0xA1;
    internal const int VK_LMENU = 0xA4; // Left Alt
    internal const int VK_RMENU = 0xA5; // Right Alt
    internal const int VK_LCONTROL = 0xA2;
    internal const int VK_LWIN = 0x5B;
    internal const int VK_RWIN = 0x5C;

    internal struct MONITORINFO
    {
        public int cbSize;
        public RECT rcMonitor;
        public RECT rcWork;
        public uint dwFlags;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct RECT
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
        public ushort tmFirstChar;
        public ushort tmLastChar;
        public ushort tmDefaultChar;
        public ushort tmBreakChar;
        public byte tmItalic;
        public byte tmUnderlined;
        public byte tmStruckOut;
        public byte tmPitchAndFamily;
        public byte tmCharSet;
    }

    [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    internal static extern IntPtr GetModuleHandle(string? lpModuleName);

    [DllImport("gdi32.dll")]
    internal static extern bool MoveToEx(IntPtr hdc, int X, int Y, IntPtr lpPoint);

    [DllImport("gdi32.dll")]
    internal static extern bool LineTo(IntPtr hdc, int nXEnd, int nYEnd);

    [DllImport("gdi32.dll")]
    internal static extern bool Rectangle(IntPtr hdc, int nLeftRect, int nTopRect, int nRightRect, int nBottomRect);

    [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Auto)]
    internal static extern bool SetWindowText(IntPtr hWnd, string lpString);

    [DllImport("gdi32.dll")]
    internal static extern IntPtr CreateCompatibleDC(IntPtr hdc);

    [DllImport("gdi32.dll")]
    internal static extern bool DeleteDC(IntPtr hdc);

    [DllImport("user32.dll")]
    internal static extern IntPtr GetForegroundWindow();

    [DllImport("user32.dll")]
    internal static extern bool EnumDisplayMonitors(IntPtr hdc, IntPtr lprcClip, MonitorEnumProc lpfnEnum, IntPtr dwData);

    [DllImport("user32.dll")]
    internal static extern bool GetMonitorInfo(IntPtr hMonitor, ref MONITORINFO lpmi);

    [DllImport("UxTheme.dll", SetLastError = true)]
    internal static extern IntPtr BeginBufferedPaint(IntPtr hdcTarget, ref RECT prcTarget,
        int dwFormat, IntPtr pPaintParams, out IntPtr phdc);

    [DllImport("UxTheme.dll", SetLastError = true)]
    internal static extern int EndBufferedPaint(IntPtr hBufferedPaint, bool fUpdateTarget);

    [DllImport("user32.dll", SetLastError = true)]
    internal static extern bool InvalidateRect(IntPtr hWnd, IntPtr lpRect, bool bErase);

    [DllImport("user32.dll")]
    internal static extern int FillRect(IntPtr hDC, [In] ref RECT lprc, IntPtr hbr);

    [DllImport("user32.dll", SetLastError = true)]
    internal static extern IntPtr SetFocus(IntPtr hWnd);

    [DllImport("user32.dll", CharSet = CharSet.Auto)]
    internal static extern IntPtr SendMessage(IntPtr hWnd, uint Msg, IntPtr wParam, IntPtr lParam);

    [return: MarshalAs(UnmanagedType.Bool)]
    [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Auto)]
    internal static extern bool PostMessage(IntPtr hWnd, uint Msg, IntPtr wParam, IntPtr lParam);

    [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Auto)]
    internal static extern int GetWindowText(IntPtr hWnd, StringBuilder lpString, int nMaxCount);

    [DllImport("user32.dll", SetLastError = true)]
    internal static extern int GetWindowTextLength(IntPtr hWnd);

    [DllImport("kernel32.dll")]
    internal static extern void ExitProcess(uint uExitCode);

    [DllImport("user32.dll", SetLastError = true)]
    internal static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);

    [DllImport("user32.dll", SetLastError = true)]
    internal static extern bool GetClientRect(IntPtr hWnd, out RECT lpRect);

    [DllImport("user32.dll")]
    internal static extern IntPtr GetParent(IntPtr hWnd);

    [DllImport("user32.dll")]
    internal static extern bool ScreenToClient(IntPtr hWnd, ref RECT lpRect);

    [DllImport("gdi32.dll", CharSet = CharSet.Auto)]
    internal static extern bool GetTextMetrics(IntPtr hdc, out TEXTMETRIC lptm);

    [DllImport("Comctl32.dll", SetLastError = true)]
    internal static extern IntPtr DefSubclassProc(IntPtr hWnd, uint uMsg, IntPtr wParam, IntPtr lParam);

    [DllImport("gdi32.dll")]
    internal static extern bool BitBlt(
        IntPtr hdcDest, int nXDest, int nYDest, int nWidth, int nHeight,
        IntPtr hdcSrc, int nXSrc, int nYSrc, int dwRop);

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    internal struct SIZE
    {
        public int cx;
        public int cy;
    }

    [DllImport("gdi32.dll")]
    internal static extern bool GetTextExtentPoint32(IntPtr hdc, string lpString,
        int cbString, out SIZE lpSize);

    [StructLayout(LayoutKind.Sequential)]
    internal struct PAINTSTRUCT
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
    internal static extern IntPtr BeginPaint(IntPtr hWnd, out PAINTSTRUCT lpPaint);

    [DllImport("user32.dll")]
    internal static extern bool EndPaint(IntPtr hWnd, ref PAINTSTRUCT lpPaint);

    [DllImport("gdi32.dll")]
    internal static extern IntPtr CreatePen(int fnPenStyle, int nWidth, uint crColor);

    [DllImport("gdi32.dll")]
    internal static extern IntPtr SelectObject(IntPtr hdc, IntPtr hObject);

    [DllImport("gdi32.dll")]
    internal static extern bool DeleteObject(IntPtr hObject);

    internal const UInt32 WM_USER = 0x0400;
    internal const UInt32 CS_DBLCLKS = 8;
    internal const UInt32 CS_VREDRAW = 1;
    internal const UInt32 CS_HREDRAW = 2;
    internal const UInt32 COLOR_BACKGROUND = 1;
    internal const int IDC_ARROW = 32512;
    internal const UInt32 WM_CTLCOLOREDIT = 0x0133;
    internal const UInt32 WM_DESTROY = 2;
    internal const UInt32 WM_PAINT = 0x0f;
    internal const UInt32 WM_COMMAND = 0x0111;
    internal const int EN_CHANGE = 0x0300;
    internal const UInt32 WM_CREATE = 0x0001;
    internal const int WM_ERASEBKGND = 0x14;
    internal const UInt32 WM_LBUTTONDBLCLK = 0x0203;
    internal const UInt32 WS_POPUP = 0x80000000;
    internal const UInt32 WS_CHILD = 0x40000000;
    internal const UInt32 WS_BORDER = 0x00800000;
    internal const int WS_VSCROLL = 0x00200000;
    internal const UInt32 WS_TABSTOP = 0x00010000;
    internal const int WS_EX_LAYERED = 0x00080000;
    internal const int WS_EX_TOPMOST = 0x00000008;
    internal const int WM_SETFONT = 0x30;
    internal const int WM_CHAR = 0x0102;
    internal const int VK_CONTROL = 0x11;
    internal const int VK_LEFT = 0x25;
    internal const int VK_RIGHT = 0x27;
    internal const int SRCCOPY = 0x00CC0020;

    internal const int LOGPIXELSX = 88;
    internal const int FW_NORMAL = 400;
    internal const uint DEFAULT_CHARSET = 1;
    internal const uint OUT_DEFAULT_PRECIS = 0;
    internal const uint CLIP_DEFAULT_PRECIS = 0;
    internal const uint DEFAULT_QUALITY = 0;
    internal const uint DEFAULT_PITCH = 0;
    internal const uint FF_DONTCARE = 0;
    internal const int TRANSPARENT = 1;
    internal const int GWL_STYLE = -16;
    internal const int GWL_EXSTYLE = -20;
    internal const int WS_EX_CLIENTEDGE = 0x00000200;
    internal const int BPBF_COMPATIBLEBITMAP = 0;
    internal const int WS_CLIPSIBLINGS = 0x04000000;
    internal const int WM_KEYDOWN = 0x0100;
    internal const int WM_SETFOCUS = 0x0007;
    internal const int SS_RIGHT = 0x00000002;
    internal const int SS_OWNERDRAW = 0x0000000D;

    internal const int VK_PAGEDOWN = 0x22;
    internal const int VK_PAGEUP = 0x21;
    internal const int VK_DOWN = 0x28;
    internal const int VK_UP = 0x26;
    internal const ushort VK_RETURN = 0x0D;
    internal const ushort VK_ESCAPE = 0x1B;
    internal const ushort VK_TAB = 0x09;
    internal const int EM_GETSEL = 0x00B0;
    internal const int EM_SETSEL = 0x00B1;
    internal const int VK_BACK = 0x08;

    [DllImport("user32.dll", CharSet = CharSet.Auto)]
    internal static extern int SendMessage(IntPtr hWnd, int msg, ref int wParam, ref int lParam);

    [DllImport("gdi32.dll")]
    internal static extern int SetBkColor(IntPtr hdc, int color);

    [DllImport("gdi32.dll")]
    internal static extern int SetTextColor(IntPtr hdc, int color);

    [DllImport("gdi32.dll")]
    internal static extern IntPtr CreateSolidBrush(int color);

    [DllImport("user32.dll")]
    internal static extern bool SetLayeredWindowAttributes(IntPtr hwnd, uint crKey, byte bAlpha, uint dwFlags);

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
    internal struct WNDCLASSEX
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
    internal static extern IntPtr CreateFont(
        int nHeight, int nWidth, int nEscapement, int nOrientation, int fnWeight,
        uint fdwItalic, uint fdwUnderline, uint fdwStrikeOut, uint fdwCharSet,
        uint fdwOutputPrecision, uint fdwClipPrecision, uint fdwQuality,
        uint fdwPitchAndFamily, string lpszFace);

    [DllImport("user32.dll")]
    internal static extern IntPtr GetDC(IntPtr hWnd);

    [DllImport("gdi32.dll")]
    internal static extern int GetDeviceCaps(IntPtr hdc, int nIndex);

    [DllImport("user32.dll")]
    internal static extern int ReleaseDC(IntPtr hWnd, IntPtr hDC);

    [DllImport("user32.dll")]
    internal static extern bool UpdateWindow(IntPtr hWnd);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

    [DllImport("user32.dll", SetLastError = true)]
    internal static extern bool EnableWindow(IntPtr hWnd, bool bEnable);

    [DllImport("gdi32.dll", CharSet = CharSet.Auto)]
    internal static extern bool TextOut(IntPtr hdc, int nXStart, int nYStart,
        string lpString, int cbString);

    [DllImport("user32.dll", SetLastError = true)]
    internal static extern bool DestroyWindow(IntPtr hWnd);

    [DllImport("user32.dll", SetLastError = true, EntryPoint = "CreateWindowEx")]
    internal static extern IntPtr CreateWindowEx2(
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
    internal static extern IntPtr CreateWindowEx(
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

    internal const uint TA_LEFT = 0x0000;
    [DllImport("gdi32.dll", SetLastError = true)]
    internal static extern uint SetTextAlign(IntPtr hdc, uint fMode);

    [DllImport("gdi32.dll")]
    internal static extern int SetBkMode(IntPtr hdc, int iBkMode);

    [DllImport("user32.dll", SetLastError = true, EntryPoint = "RegisterClassEx")]
    internal static extern System.UInt16 RegisterClassEx([In] ref WNDCLASSEX lpWndClass);

    [DllImport("kernel32.dll")]
    internal static extern uint GetLastError();

    [DllImport("user32.dll")]
    internal static extern IntPtr DefWindowProc(IntPtr hWnd, uint uMsg, IntPtr wParam, IntPtr lParam);

    [DllImport("user32.dll")]
    internal static extern sbyte GetMessage(out uint lpMsg, IntPtr hWnd, uint wMsgFilterMin, uint wMsgFilterMax);

    [DllImport("user32.dll")]
    internal static extern IntPtr LoadCursor(IntPtr hInstance, int lpCursorName);

    [DllImport("user32.dll")]
    internal static extern bool TranslateMessage([In] ref uint lpMsg);

    [DllImport("user32.dll")]
    internal static extern IntPtr DispatchMessage([In] ref uint lpmsg);

    [DllImport("Comctl32.dll", SetLastError = true)]
    internal static extern bool SetWindowSubclass(IntPtr hWnd, SubclassProc pfnSubclass, uint uIdSubclass, IntPtr dwRefData);

    [DllImport("user32.dll")]
    internal static extern int GetWindowLong(IntPtr hWnd, int nIndex);

    [DllImport("user32.dll")]
    internal static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);

    [DllImport("user32.dll")]
    internal static extern uint GetDpiForWindow(IntPtr hwnd);

    [DllImport("user32.dll")]
    internal static extern uint GetDpiForSystem();

    internal delegate IntPtr WndProc(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);
    internal delegate bool MonitorEnumProc(IntPtr hMonitor, IntPtr hdcMonitor, IntPtr lprcMonitor, IntPtr dwData);
    internal delegate IntPtr SubclassProc(IntPtr hWnd, uint uMsg, IntPtr wParam, IntPtr lParam, uint uIdSubclass, IntPtr dwRefData);
}