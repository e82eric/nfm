using System.Runtime.InteropServices;
using System.Text;
using nfzf.FileSystem;

namespace Win32FromForms;

delegate IntPtr WndProc(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);
class Win32Window
{
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
        public byte tmFirstChar;    // this assumes the ANSI charset; for the UNICODE charset the type is char (or short)
        public byte tmLastChar;     // this assumes the ANSI charset; for the UNICODE charset the type is char (or short)
        public byte tmDefaultChar;  // this assumes the ANSI charset; for the UNICODE charset the type is char (or short)
        public byte tmBreakChar;    // this assumes the ANSI charset; for the UNICODE charset the type is char (or short)
        public byte tmItalic;
        public byte tmUnderlined;
        public byte tmStruckOut;
        public byte tmPitchAndFamily;
        public byte tmCharSet;
    }
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
        
    [StructLayout(LayoutKind.Sequential)]
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
    private const int HIGHLIGHTED_TEXT_COLOR = 0x000e5dd6; //0x00bbggrr

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
    private const UInt32 WM_ITEMS_UPDATED = WM_USER + 1;
    private const UInt32 WS_OVERLAPPEDWINDOW = 0xcf0000;
    private const UInt32 WS_VISIBLE = 0x10000000;
    private const UInt32 CS_USEDEFAULT = 0x80000000;
    private const UInt32 CS_DBLCLKS = 8;
    private const UInt32 CS_VREDRAW = 1;
    private const UInt32 CS_HREDRAW = 2;
    private const UInt32 COLOR_WINDOW = 5;
    private const UInt32 COLOR_BACKGROUND = 1;
    private const UInt32 IDC_CROSS = 32515;
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
        
    private const int VK_DOWN = 0x28;
    private const int VK_UP = 0x26;
    private const ushort VK_RETURN = 0x0D;
    private const ushort VK_ESCAPE = 0x1B;

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

    [DllImport("gdi32.dll")]
    private static extern int GetDeviceCaps(IntPtr hdc, int nIndex);

    [DllImport("user32.dll")]
    private static extern int ReleaseDC(IntPtr hWnd, IntPtr hDC);

    private WndProc delegWndProc = myWndProc;

    [DllImport("user32.dll")]
    static extern bool UpdateWindow(IntPtr hWnd);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);
        
    [DllImport("gdi32.dll", CharSet = CharSet.Auto)]
    static extern bool TextOut(IntPtr hdc, int nXStart, int nYStart,
        string lpString, int cbString);

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
        
    private delegate IntPtr SubclassProc(IntPtr hWnd, uint uMsg, IntPtr wParam, IntPtr lParam, uint uIdSubclass, IntPtr dwRefData);
    [DllImport("Comctl32.dll", SetLastError = true)]
    private static extern bool SetWindowSubclass(IntPtr hWnd, SubclassProc pfnSubclass, uint uIdSubclass, IntPtr dwRefData);
        
    [DllImport("user32.dll")]
    private static extern int GetWindowLong(IntPtr hWnd, int nIndex);

    [DllImport("user32.dll")]
    private static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);

    private static IntPtr ListBoxControlProc(IntPtr hWnd, uint uMsg, IntPtr wParam, IntPtr lParam, uint uIdSubclass, IntPtr dwRefData)
    {
        switch (uMsg)
        {
            case WM_ERASEBKGND:
                return 1;
            default:
                return DefSubclassProc(hWnd, uMsg, wParam, lParam);
        }
    }
        
    private static IntPtr EditControlProc(IntPtr hWnd, uint uMsg, IntPtr wParam, IntPtr lParam, uint uIdSubclass, IntPtr dwRefData)
    {
        switch (uMsg)
        {
            case WM_KEYDOWN:
                switch (wParam)
                {
                    case VK_DOWN:
                        _viewModel.SelectedIndex++;
                        break;
                    case VK_UP:
                        _viewModel.SelectedIndex--;
                        break;
                    case VK_RETURN:
                        Console.WriteLine(_viewModel.Items[_viewModel.SelectedIndex].Text);
                        ExitProcess(0);
                        break;
                    case VK_ESCAPE:
                        ExitProcess(0);
                        break;
                }
                return 1;
            default:
                return DefSubclassProc(hWnd, uMsg, wParam, lParam);
        }
    }
        
    internal bool Create(ViewModel viewModel)
    {
        _viewModel = viewModel;
        WNDCLASSEX wind_class = new WNDCLASSEX();
        wind_class.cbSize = Marshal.SizeOf(typeof(WNDCLASSEX));
        wind_class.style = (int)(CS_HREDRAW | CS_VREDRAW | CS_DBLCLKS ) ;
        wind_class.hbrBackground = (IntPtr) COLOR_BACKGROUND  +1 ;
        wind_class.cbClsExtra = 0;
        wind_class.cbWndExtra = 0;
        wind_class.hInstance = Marshal.GetHINSTANCE(this.GetType().Module);
        wind_class.hIcon = IntPtr.Zero;
        wind_class.hCursor = LoadCursor(IntPtr.Zero, (int)IDC_CROSS);
        wind_class.lpszMenuName = null;
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

        hwnd = CreateWindowEx2(
            WS_EX_LAYERED | WS_EX_TOPMOST,
            //WS_EX_TOPMOST,
            regResult,
            "Hello Win32",
            WS_VISIBLE | WS_POPUP,
            500,
            500,
            1500,
            1500,
            IntPtr.Zero,
            IntPtr.Zero,
            wind_class.hInstance,
            IntPtr.Zero);

        if (hwnd == ((IntPtr)0))
        {
            uint error = GetLastError();
            return false;
        }

        INSTANCE = wind_class.hInstance;
        SetLayeredWindowAttributes(hwnd, 0x000000, 0, 0x00000001);
        ShowWindow(hwnd, 1);
        UpdateWindow(hwnd);
        //return true;

        //The explicit message pump is not necessary, messages are obviously dispatched by the framework.
        //However, if the while loop is implemented, the functions are called... Windows mysteries...
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
        
    public static IntPtr InitializeFont(string fontName, int size)
    {
        IntPtr screen = GetDC(IntPtr.Zero);
        int dpi = GetDeviceCaps(screen, LOGPIXELSX);
        ReleaseDC(IntPtr.Zero, screen);

        int scaledFontSize = -MulDiv(size, dpi, 96); // Same scaling logic as in C

        return CreateFont(scaledFontSize, 0, 0, 0, FW_NORMAL, 0, 0, 0,
            DEFAULT_CHARSET, OUT_DEFAULT_PRECIS, CLIP_DEFAULT_PRECIS, DEFAULT_QUALITY,
            DEFAULT_PITCH | FF_DONTCARE, fontName);
    }

    private static IntPtr font;
    private static IntPtr hwnd;
    private static IntPtr textBoxHwnd;
    private static IntPtr listBoxHwnd;
    private static IntPtr BACKGROUND_BRUSH;
    private static IntPtr SELECTED_BACKGROUND_BRUSH;
    private static IntPtr INSTANCE;
    private static ViewModel _viewModel;
    private static IntPtr myWndProc(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam)
    {
        IntPtr hdc = IntPtr.Zero;
        TEXTMETRIC tm;
        switch (msg)
        {
            case WM_CREATE:
                hdc = GetDC(hWnd);
                //font = InitializeFont("JetBrainsMonoNL NFP Regular", 14);
                font = InitializeFont("Consolas", 16);
                SelectObject(hdc, font);
                GetTextMetrics(hdc, out tm);
                ReleaseDC(hWnd, hdc);
                    
                BACKGROUND_BRUSH = CreateSolidBrush(BACKGROUND_COLOR);
                SELECTED_BACKGROUND_BRUSH = CreateSolidBrush(SELECTED_BACKGROUND_COLOR);
                textBoxHwnd = CreateWindowEx(
                    0,
                    "edit",
                    "",
                    WS_CHILD | WS_VISIBLE | WS_TABSTOP,
                    25,
                    25,
                    1200,
                    tm.tmHeight,
                    hWnd,
                    1,
                    INSTANCE,
                    IntPtr.Zero);
                IntPtr refData = IntPtr.Zero;
                SetWindowSubclass(textBoxHwnd, EditControlProc, 0, refData);
                    
                listBoxHwnd = CreateWindowEx(
                    0,
                    "LISTBOX",
                    "",
                    WS_VISIBLE | WS_CHILD | LBS_STANDARD | LBS_HASSTRINGS | WS_CLIPSIBLINGS | LBS_OWNERDRAWFIXED,
                    25,
                    tm.tmHeight + 25 + (12 * 3),
                    1200,
                    550,
                    hWnd,
                    2,
                    INSTANCE,
                    IntPtr.Zero);
                SetWindowSubclass(listBoxHwnd, ListBoxControlProc, 0, refData);
                    
                long style = GetWindowLong(listBoxHwnd, GWL_STYLE);
                style &= ~WS_BORDER;
                style &= ~WS_VSCROLL;
                SetWindowLong(listBoxHwnd, GWL_STYLE, (int)style);

                // Remove WS_EX_CLIENTEDGE
                int exStyle = GetWindowLong(listBoxHwnd, GWL_EXSTYLE);
                exStyle &= ~WS_EX_CLIENTEDGE;
                SetWindowLong(listBoxHwnd, GWL_EXSTYLE, exStyle);
                    
                SendMessage(textBoxHwnd, WM_SETFONT, font, 1);
                SetFocus(textBoxHwnd);
                    
                //Task.Run(() => _viewModel.Run(new FileWalker()));
                break;
                
            case WM_ERASEBKGND:
                break;
                
            case WM_PAINT:
                PAINTSTRUCT ps;
                hdc = BeginPaint(hWnd, out ps);

                if (hdc != IntPtr.Zero)
                {
                    IntPtr hPen = CreatePen(PS_SOLID, BORDER_THICKNESS, BORDER_COLOR);
                    IntPtr hOldPen = SelectObject(hdc, hPen);
                    SelectObject(hdc, BACKGROUND_BRUSH);

                    PaintBorder(hWnd, hdc, textBoxHwnd, BORDER_THICKNESS, 10);
                    PaintBorder(hWnd, hdc, listBoxHwnd, BORDER_THICKNESS, 10);

                    SelectObject(hdc, hOldPen);
                    DeleteObject(hPen);
                }

                EndPaint(hWnd, ref ps);
                break;
                
            case WM_CTLCOLORLISTBOX:
                hdc = wParam;
                SetBkColor(hdc, BACKGROUND_COLOR);
                SetTextColor(hdc, TEXT_COLOR);
                return BACKGROUND_BRUSH;
                
            case WM_CTLCOLOREDIT:
                hdc = wParam;
                SetBkColor(hdc, BACKGROUND_COLOR);
                SetTextColor(hdc, TEXT_COLOR);
                return BACKGROUND_BRUSH;

            case WM_LBUTTONDBLCLK :
                break;

            case WM_DESTROY:
                DestroyWindow(hWnd);
                ExitProcess(0);
                break;
                
            case WM_ITEMS_UPDATED:
                for (var i = 0;  i < 16; i++)
                {
                    IntPtr ptr = Marshal.StringToHGlobalUni(_viewModel.Items[i].Text);
                        
                    SendMessage(listBoxHwnd, LB_DELETESTRING, i, IntPtr.Zero);
                    SendMessage(listBoxHwnd, LB_INSERTSTRING, i, ptr);
                }
                    
                SendMessage(listBoxHwnd, LB_SETCURSEL, _viewModel.SelectedIndex, 0);
                    
                break;

            case WM_DRAWITEM:
                DRAWITEMSTRUCT dis = Marshal.PtrToStructure<DRAWITEMSTRUCT>(lParam);
                IntPtr hNewDC;
                IntPtr hBufferedPaint = BeginBufferedPaint(
                    dis.hDC,
                    ref dis.rcItem,
                    BPBF_COMPATIBLEBITMAP,
                    IntPtr.Zero,
                    out hNewDC);
                if (hBufferedPaint == IntPtr.Zero)
                {
                    return IntPtr.Zero;
                }

                SelectObject(hNewDC, font);
                var backgroundBrush = BACKGROUND_BRUSH;
                if ((dis.itemState & ODS_SELECTED) == ODS_SELECTED)
                {
                    backgroundBrush = SELECTED_BACKGROUND_BRUSH;
                    SetBkColor(hNewDC, SELECTED_BACKGROUND_COLOR);
                }
                else
                {
                    SetBkColor(hNewDC, BACKGROUND_COLOR);
                }
                    
                FillRect(hNewDC, ref dis.rcItem, backgroundBrush);
                GetTextMetrics(hNewDC, out tm);
                SetTextColor(hNewDC, TEXT_COLOR);
                    
                int textHeight = tm.tmHeight;
                int itemHeight = dis.rcItem.bottom - dis.rcItem.top;
                int centeredY = dis.rcItem.top + (itemHeight - textHeight) / 2;
                    
                TextOut(
                    hNewDC,
                    5,
                    centeredY,
                    _viewModel.Items[dis.itemID].Text,
                    _viewModel.Items[dis.itemID].Text.Length);
                SetTextColor(hNewDC, HIGHLIGHTED_TEXT_COLOR);
                for (int i = 0; i < _viewModel.Items[dis.itemID].Pos.Count; i++)
                {
                    SIZE sz;
                    var textIndex = _viewModel.Items[dis.itemID].Pos[i];
                    GetTextExtentPoint32(hNewDC, _viewModel.Items[dis.itemID].Text, textIndex, out sz); 
                    TextOut(hNewDC, sz.cx + 5, centeredY, _viewModel.Items[dis.itemID].Text[textIndex].ToString(), 1);
                }
                SetTextColor(hNewDC, TEXT_COLOR);
                EndBufferedPaint(hBufferedPaint, true);
                    
                break;
            case WM_MEASUREITEM:
                var pmis = Marshal.PtrToStructure<MEASUREITEMSTRUCT>(lParam);
                switch (pmis.CtlID)
                {
                    case ODT_LISTBOX:
                        hdc = GetDC(hWnd);
                        SelectObject(hdc, font);
                        GetTextMetrics(hdc, out tm);
                        pmis.itemHeight = (uint)(tm.tmHeight + 15);
                        Marshal.StructureToPtr(pmis, lParam, false);
                        break;
                }
                break;
                
            case WM_COMMAND:
                var id = (ushort)(wParam.ToInt64() & 0xFFFF);
                var notificationCode = (ushort)((wParam.ToInt64() >> 16) & 0xFFFF);
                if (notificationCode == EN_CHANGE)
                {
                    StringBuilder text = new StringBuilder(GetWindowTextLength(textBoxHwnd) + 1);
                    GetWindowText(textBoxHwnd, text, text.Capacity);
                    Task.Run(() => _viewModel.SetSearchString(text.ToString()));
                }
                break;

            default:
                break;
        }
        return DefWindowProc(hWnd, msg, wParam, lParam);
    }

    private static void PaintBorder(IntPtr windowHwnd, IntPtr hdc, IntPtr targetHwnd, int borderThickness, int padding)
    {
        if (!GetWindowRect(targetHwnd, out RECT rect))
        {
            throw new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error());
        }

        POINT[] points = new POINT[]
        {
            new POINT { x = rect.left, y = rect.top },
            new POINT { x = rect.right, y = rect.bottom }
        };

        MapWindowPoints(IntPtr.Zero, windowHwnd, ref points[0], 2);

        bool success = Rectangle(
            hdc,
            points[0].x - (borderThickness + padding),
            points[0].y - (borderThickness + padding),
            points[1].x + (borderThickness + padding),
            points[1].y + (borderThickness + padding)
        );

        if (!success)
        {
            throw new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error());
        }
    }

    public static void SetListBoxItems()
    {
        PostMessage(hwnd, WM_ITEMS_UPDATED, IntPtr.Zero, IntPtr.Zero);
    }
}