using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Channels;

namespace nfm.menu;
public class ListWindows
{
    public class BoolWrapper
    {
        public bool Value;
    }
    
    public class ListWindowsWorkspace
    {
        public ListWindowsClient Clients { get; set; }
        public ListWindowsClient LastClient { get; set; }
        public int MaxProcessNameLen { get; set; }
    }

    public class ListWindowsClient
    {
        public ListWindowsClientData Data { get; set; }
        public ListWindowsClient Next { get; set; }
    }

    public class ListWindowsClientData
    {
        public IntPtr Hwnd { get; set; }
        public uint ProcessId { get; set; }
        public string ProcessName { get; set; }
        public string ClassName { get; set; }
        public string Title { get; set; }
        public bool IsMinimized { get; set; }
    }

    const uint GA_ROOTOWNER = 3;
    const uint DWMWA_CLOAKED = 14;
    const int GWL_STYLE = -16;
    const int GWL_EXSTYLE = -20;
    const uint WS_EX_TOOLWINDOW = 0x00000080;
    const uint WS_EX_NOACTIVATE = 0x08000000;
    const uint WS_CHILD = 0x40000000;
    const uint WS_VISIBLE = 0x10000000;

    public delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);
    public delegate bool PropEnumProcEx(IntPtr hwnd, IntPtr lpszString, IntPtr hData, IntPtr dwData);

    [DllImport("user32.dll")]
    static extern IntPtr GetAncestor(IntPtr hwnd, uint gaFlags);

    [DllImport("user32.dll")]
    static extern IntPtr GetLastActivePopup(IntPtr hWnd);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    static extern bool IsWindowVisible(IntPtr hWnd);

    [DllImport("dwmapi.dll")]
    static extern int DwmGetWindowAttribute(IntPtr hwnd, uint dwAttribute, out bool pvAttribute, int cbAttribute);

    [DllImport("user32.dll")]
    static extern bool EnumPropsEx(IntPtr hWnd, PropEnumProcEx lpEnumFunc, IntPtr lParam);

    [DllImport("user32.dll")]
    static extern IntPtr GetParent(IntPtr hWnd);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    static extern bool IsIconic(IntPtr hWnd);

    [DllImport("user32.dll")]
    static extern IntPtr GetShellWindow();

    [DllImport("user32.dll")]
    static extern int EnumWindows(EnumWindowsProc lpEnumFunc, IntPtr lParam);

    [DllImport("user32.dll", SetLastError = true)]
    static extern int GetWindowLong(IntPtr hWnd, int nIndex);

    [DllImport("user32.dll", CharSet = CharSet.Auto)]
    static extern int GetClassName(IntPtr hWnd, StringBuilder lpClassName, int nMaxCount);

    [DllImport("user32.dll", CharSet = CharSet.Auto)]
    static extern int GetWindowText(IntPtr hWnd, StringBuilder lpString, int nMaxCount);

    [DllImport("user32.dll")]
    static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

    [DllImport("kernel32.dll")]
    static extern IntPtr OpenProcess(uint dwDesiredAccess, bool bInheritHandle, uint dwProcessId);

    [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    static extern bool QueryFullProcessImageName(IntPtr hProcess, int dwFlags, StringBuilder lpExeName, ref int lpdwSize);

    [DllImport("kernel32.dll")]
    static extern bool CloseHandle(IntPtr hObject);

    [DllImport("shlwapi.dll", CharSet = CharSet.Auto)]
    static extern IntPtr PathFindFileName(string pPath);

    static bool ListWindowsIsAltTabWindow(IntPtr hwnd)
    {
        IntPtr hwndWalk = GetAncestor(hwnd, GA_ROOTOWNER);
        IntPtr hwndTry;
        while ((hwndTry = GetLastActivePopup(hwndWalk)) != hwndTry)
        {
            if (IsWindowVisible(hwndTry)) break;
            hwndWalk = hwndTry;
        }
        return hwndWalk == hwnd;
    }

    static bool ListWindowsIsWindowCloaked(IntPtr hwnd)
    {
        bool isCloaked = false;
        int result = DwmGetWindowAttribute(hwnd, DWMWA_CLOAKED, out isCloaked, Marshal.SizeOf(typeof(bool)));
        return (result == 0) && isCloaked;
    }

    static bool ListWindowsPropEnumCallback(IntPtr hwndSubclass, IntPtr lpszString, IntPtr hData, IntPtr dwData)
    {
        if ((lpszString & 0xffff0000) != 0)
        {
            string propName = Marshal.PtrToStringUni(lpszString);
            if (propName == "ApplicationViewCloakType")
            {
                bool hasAppropriateApplicationViewCloakType = hData == IntPtr.Zero;
                Marshal.WriteInt32(dwData, hasAppropriateApplicationViewCloakType ? 1 : 0);
                return false;
            }
        }
        return true;
    }

    static bool ListWindowsIsRootWindow(IntPtr hwnd, int styles, int exStyles)
    {
        bool isWindowVisible = IsWindowVisible(hwnd);
        IntPtr desktopWindow = GetDesktopWindow();

        if (hwnd == desktopWindow)
        {
            return false;
        }

        if (!isWindowVisible)
        {
            return false;
        }

        if ((exStyles & (int)WS_EX_TOOLWINDOW) != 0)
        {
            return false;
        }

        if (ListWindowsIsWindowCloaked(hwnd))
        {
            return false;
        }

        StringBuilder className = new StringBuilder(256);
        GetClassName(hwnd, className, className.Capacity);
        if (className.ToString().Contains("ApplicationFrameWindow"))
        {
            BoolWrapper hasCorrectCloakedProperty = new BoolWrapper();
            GCHandle gcHandle = GCHandle.Alloc(hasCorrectCloakedProperty);
            PropEnumProcEx callback = new PropEnumProcEx(ListWindowsPropEnumCallback);
            EnumPropsEx(hwnd, callback, GCHandle.ToIntPtr(gcHandle));
            if (hasCorrectCloakedProperty.Value)
            {
                return true;
            }
            else
            {
                return false;
            }
        }

        if ((exStyles & (int)WS_EX_NOACTIVATE) != 0)
        {
            return false;
        }

        if ((styles & (int)WS_CHILD) != 0)
        {
            return false;
        }

        IntPtr parentHwnd = GetParent(hwnd);

        if (parentHwnd != IntPtr.Zero)
        {
            return false;
        }

        bool isAltTabWindow = ListWindowsIsAltTabWindow(hwnd);

        if (!isAltTabWindow)
        {
            return false;
        }

        return true;
    }

    static ListWindowsClient ClientFactoryCreateFromHwnd(IntPtr hwnd)
    {
        GetWindowThreadProcessId(hwnd, out uint processId);

        const uint PROCESS_QUERY_INFORMATION = 0x0400;
        const uint PROCESS_VM_READ = 0x0010;
        IntPtr hProcess = OpenProcess(PROCESS_QUERY_INFORMATION | PROCESS_VM_READ, false, processId);

        StringBuilder processImageFileName = new StringBuilder(1024);
        int size = processImageFileName.Capacity;
        bool success = QueryFullProcessImageName(hProcess, 0, processImageFileName, ref size);
        CloseHandle(hProcess);

        if (!success)
        {
            int error = Marshal.GetLastWin32Error();
            StringBuilder buf = new StringBuilder(256);
            FormatMessage(FormatMessageFlags.FORMAT_MESSAGE_FROM_SYSTEM | FormatMessageFlags.FORMAT_MESSAGE_IGNORE_INSERTS,
                IntPtr.Zero, error, 0, buf, buf.Capacity, IntPtr.Zero);
            Console.WriteLine("Error: " + buf.ToString());
        }

        StringBuilder title = new StringBuilder(1024);
        GetWindowText(hwnd, title, title.Capacity);

        StringBuilder className = new StringBuilder(256);
        GetClassName(hwnd, className, className.Capacity);
        bool isMinimized = IsIconic(hwnd);

        string processShortFileName = System.IO.Path.GetFileName(processImageFileName.ToString());
        ListWindowsClientData clientData = new ListWindowsClientData
        {
            Hwnd = hwnd,
            ProcessId = processId,
            ClassName = className.ToString(),
            ProcessName = processShortFileName,
            Title = title.ToString(),
            IsMinimized = isMinimized
        };

        ListWindowsClient client = new ListWindowsClient
        {
            Data = clientData
        };

        return client;
    }

    static bool EnumWindowsCallback(IntPtr hwnd, IntPtr lParam)
    {
        IntPtr shellHwnd = GetShellWindow();
        if (hwnd == shellHwnd)
        {
            return true;
        }

        ListWindowsWorkspace workspace = (ListWindowsWorkspace)GCHandle.FromIntPtr(lParam).Target;

        int styles = GetWindowLong(hwnd, GWL_STYLE);
        int exStyles = GetWindowLong(hwnd, GWL_EXSTYLE);
        bool isRootWindow = ListWindowsIsRootWindow(hwnd, styles, exStyles);
        if (!isRootWindow)
        {
            return true;
        }

        if ((styles & (int)WS_VISIBLE) == 0)
        {
            return true;
        }

        ListWindowsClient client = ClientFactoryCreateFromHwnd(hwnd);

        if (client.Data.ClassName.Contains("Progman"))
        {
            return true;
        }

        if (client.Data.Title.Contains("ApplicationFrameWindow"))
        {
            return true;
        }

        if (workspace.Clients == null)
        {
            workspace.Clients = client;
            workspace.LastClient = client;
        }
        else
        {
            workspace.LastClient.Next = client;
            workspace.LastClient = client;
        }

        int processFileNameLen = client.Data.ProcessName.Length;
        if (processFileNameLen > workspace.MaxProcessNameLen)
        {
            workspace.MaxProcessNameLen = processFileNameLen;
        }

        return true;
    }

    public static async Task Run(ChannelWriter<string> writer, CancellationToken cancellationToken)
    {
        ListWindowsWorkspace workspace = new ListWindowsWorkspace();
        GCHandle handle = GCHandle.Alloc(workspace);

        EnumWindowsProc callback = EnumWindowsCallback;
        EnumWindows(callback, GCHandle.ToIntPtr(handle));

        string header = string.Format("{0,-8} {1,-8} {2,-" + workspace.MaxProcessNameLen + "} {3}", "HWND", "PID", "Name", "Title");

        ListWindowsClient c = workspace.Clients;

        int numberOfResults = 1;

        while (c != null)
        {
            string line = string.Format("{0:X8} {1,8} {2,-" + workspace.MaxProcessNameLen + "} {3}",
                c.Data.Hwnd.ToInt64(), c.Data.ProcessId, c.Data.ProcessName, c.Data.Title);
            await writer.WriteAsync(line);
            c = c.Next;
            numberOfResults++;
        }
        writer.Complete();

        handle.Free();
    }

    [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    static extern uint FormatMessage(FormatMessageFlags dwFlags, IntPtr lpSource,
        int dwMessageId, int dwLanguageId, StringBuilder lpBuffer, int nSize, IntPtr Arguments);

    [Flags]
    enum FormatMessageFlags : uint
    {
        FORMAT_MESSAGE_FROM_SYSTEM = 0x00001000,
        FORMAT_MESSAGE_IGNORE_INSERTS = 0x00000200,
    }

    [DllImport("user32.dll")]
    static extern IntPtr GetDesktopWindow();
}
