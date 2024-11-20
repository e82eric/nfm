using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
using Avalonia.Threading;

namespace nfm.menu;

public class GlobalKeyHandler
{
    private const int VK_LWIN = 0x5B;
    private const int VK_RWIN = 0x5C;
    private const int VK_O = 0x4F;
    private const int VK_I = 0x49;
    private const int VK_U = 0x55;
    private const int VK_L = 0x4c;
    private const int WH_KEYBOARD_LL = 13;
    private const int WM_KEYDOWN = 0x0100;
    private const int WM_SYSKEYDOWN = 0x0104;
    private static LowLevelKeyboardProc _proc = HookCallback;
    private static IntPtr _hookID = IntPtr.Zero;
    private static bool isWinKeyPressed;
    private static App _app;
    private static Dictionary<(Modifiers, int), Action> _keyBindings;
    

    public static void SetHook(App app)
    {
        _app = app;
        _keyBindings = new Dictionary<(Modifiers, int), Action>();
        _keyBindings.Add((Modifiers.LAlt, VK_O), _app.Show);
        _keyBindings.Add((Modifiers.LAlt, VK_I), _app.ShowListWindows);
        _keyBindings.Add((Modifiers.LAlt, VK_U), _app.ShowProcesses);
        _keyBindings.Add((Modifiers.LAlt, VK_L), _app.ShowFiles);
        using (Process curProcess = Process.GetCurrentProcess())
        using (ProcessModule curModule = curProcess.MainModule)
        {
            SetWindowsHookEx(WH_KEYBOARD_LL, _proc, GetModuleHandle(curModule.ModuleName), 0);
        }
    }
    
    [Flags]
    public enum Modifiers
    {
        None = 0,
        LShift = 1 << 0,
        RShift = 1 << 1,
        LAlt = 1 << 2,
        RAlt = 1 << 3,
        LCtl = 1 << 4,
        LWin = 1 << 5,
        RWin = 1 << 6
    }
    
    [DllImport("user32.dll")]
    private static extern short GetAsyncKeyState(int vKey);

    // Virtual key codes
    private const int VK_LSHIFT = 0xA0;
    private const int VK_RSHIFT = 0xA1;
    private const int VK_LMENU = 0xA4;  // Left Alt
    private const int VK_RMENU = 0xA5;  // Right Alt
    private const int VK_CONTROL = 0x11; // Control key (either left or right)

    public static Modifiers GetModifiersPressed()
    {
        Modifiers modifiersPressed = Modifiers.None;

        if ((GetAsyncKeyState(VK_LSHIFT) & 0x8000) != 0)
        {
            modifiersPressed |= Modifiers.LShift;
        }
        if ((GetAsyncKeyState(VK_RSHIFT) & 0x8000) != 0)
        {
            modifiersPressed |= Modifiers.RShift;
        }
        if ((GetAsyncKeyState(VK_LMENU) & 0x8000) != 0)
        {
            modifiersPressed |= Modifiers.LAlt;
        }
        if ((GetAsyncKeyState(VK_RMENU) & 0x8000) != 0)
        {
            modifiersPressed |= Modifiers.RAlt;
        }
        if ((GetAsyncKeyState(VK_CONTROL) & 0x8000) != 0)
        {
            modifiersPressed |= Modifiers.LCtl;
        }
        if ((GetAsyncKeyState(VK_LWIN) & 0x8000) != 0)
        {
            modifiersPressed |= Modifiers.LWin;
        }
        if ((GetAsyncKeyState(VK_RWIN) & 0x8000) != 0)
        {
            modifiersPressed |= Modifiers.RWin;
        }

        return modifiersPressed;
    }

    private delegate IntPtr LowLevelKeyboardProc(int nCode, IntPtr wParam, IntPtr lParam);

    private static IntPtr HookCallback(int nCode, IntPtr wParam, IntPtr lParam)
    {
        if (nCode >= 0 && (wParam == WM_KEYDOWN || wParam == WM_SYSKEYDOWN))
        {
            var modifiers = GetModifiersPressed();
            int vkCode = Marshal.ReadInt32(lParam);
            if (_keyBindings.TryGetValue((modifiers, vkCode), out var action))
            {
                Dispatcher.UIThread.Post(() => action());
                return 1;
            }
        }

        return CallNextHookEx(_hookID, nCode, wParam, lParam);
    }

    [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    private static extern IntPtr SetWindowsHookEx(int idHook, LowLevelKeyboardProc lpfn, IntPtr hMod, uint dwThreadId);

    [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool UnhookWindowsHookEx(IntPtr hhk);

    [DllImport("user32.dll", CharSet = CharSet.Auto)]
    private static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);

    [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    private static extern IntPtr GetModuleHandle(string lpModuleName);
}