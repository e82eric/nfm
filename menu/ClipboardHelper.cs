using System;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using nfm.menu;

public class ClipboardHelper
{
    // Clipboard formats
    private const uint CF_UNICODETEXT = 13;

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool OpenClipboard(IntPtr hWndNewOwner);
    
    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool EmptyClipboard();
    
    [DllImport("user32.dll", SetLastError = true)]
    private static extern IntPtr SetClipboardData(uint uFormat, IntPtr hMem);
    
    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool CloseClipboard();
    
    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern IntPtr GlobalAlloc(uint uFlags, UIntPtr dwBytes);
    
    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern IntPtr GlobalLock(IntPtr hMem);
    
    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool GlobalUnlock(IntPtr hMem);
    
    private const uint GMEM_MOVEABLE = 0x0002;
    
    public static async Task CopyStringToClipboard(object t, MainViewModel viewModel)
    {
        var text = t.ToString();
        // Ensure we're running on STA thread
        if (Thread.CurrentThread.GetApartmentState() != ApartmentState.STA)
        {
            Thread thread = new Thread(() => CopyStringToClipboard(t, viewModel));
            thread.SetApartmentState(ApartmentState.STA);
            thread.Start();
            thread.Join();
            return;
        }

        Copy(text);

        await viewModel.ShowToast($"Copied '{text}' to clipboard");
    }

    private static void Copy(string text)
    {
        IntPtr hGlobal = IntPtr.Zero;
        try
        {
            if (!OpenClipboard(IntPtr.Zero))
            {
                throw new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error(), "Failed to open clipboard");
            }

            if (!EmptyClipboard())
            {
                throw new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error(), "Failed to empty clipboard");
            }

            // Allocate global memory for the string
            int bytes = (text.Length + 1) * 2; // Unicode characters are 2 bytes
            hGlobal = GlobalAlloc(GMEM_MOVEABLE, (UIntPtr)bytes);
            if (hGlobal == IntPtr.Zero)
            {
                throw new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error(), "Failed to allocate global memory");
            }

            // Lock the global memory and copy the string
            IntPtr target = GlobalLock(hGlobal);
            if (target == IntPtr.Zero)
            {
                throw new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error(), "Failed to lock global memory");
            }

            // Copy the string into the allocated memory
            Marshal.Copy(text.ToCharArray(), 0, target, text.Length);
            // Add null terminator
            Marshal.WriteInt16(target, text.Length * 2, 0);

            GlobalUnlock(hGlobal);

            if (SetClipboardData(CF_UNICODETEXT, hGlobal) == IntPtr.Zero)
            {
                throw new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error(), "Failed to set clipboard data");
            }

            // Do not free hGlobal here because SetClipboardData takes ownership of the memory
            hGlobal = IntPtr.Zero;
        }
        finally
        {
            if (hGlobal != IntPtr.Zero)
            {
                GlobalUnlock(hGlobal);
                // Free the memory if we failed to set clipboard data
                Marshal.FreeHGlobal(hGlobal);
            }
            CloseClipboard();
        }
    }
}
