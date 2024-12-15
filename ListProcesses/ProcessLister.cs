using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using System.Threading.Channels;
using Microsoft.Win32.SafeHandles;

namespace nfzf.ListProcesses;
public class ProcessLister
{
    private const uint TH32CS_SNAPPROCESS = 0x00000002;
    private const uint PROCESS_QUERY_LIMITED_INFORMATION = 0x1000;

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    struct PROCESSENTRY32
    {
        public uint dwSize;
        public uint cntUsage;
        public uint th32ProcessID;
        public IntPtr th32DefaultHeapID;
        public uint th32ModuleID;
        public uint cntThreads;
        public uint th32ParentProcessID;
        public int pcPriClassBase;
        public uint dwFlags;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)]
        public string szExeFile;
    }

    [StructLayout(LayoutKind.Sequential)]
    struct PROCESS_MEMORY_COUNTERS_EX
    {
        public uint cb;
        public uint PageFaultCount;
        public ulong PeakWorkingSetSize;
        public ulong WorkingSetSize;
        public ulong QuotaPeakPagedPoolUsage;
        public ulong QuotaPagedPoolUsage;
        public ulong QuotaPeakNonPagedPoolUsage;
        public ulong QuotaNonPagedPoolUsage;
        public ulong PagefileUsage;
        public ulong PeakPagefileUsage;
        public ulong PrivateUsage;
    }

    [StructLayout(LayoutKind.Sequential)]
    struct FILETIME
    {
        public uint dwLowDateTime;
        public uint dwHighDateTime;
    }

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    static extern SafeSnapshotHandle CreateToolhelp32Snapshot(uint dwFlags, uint th32ProcessID);

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true, EntryPoint = "Process32FirstW")]
    static extern bool Process32First(SafeSnapshotHandle hSnapshot, ref PROCESSENTRY32 lppe);

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true, EntryPoint = "Process32NextW")]
    static extern bool Process32Next(SafeSnapshotHandle hSnapshot, ref PROCESSENTRY32 lppe);

    [DllImport("kernel32.dll", SetLastError = true)]
    static extern SafeProcessHandle OpenProcess(uint dwDesiredAccess, bool bInheritHandle, uint dwProcessId);

    [DllImport("psapi.dll", SetLastError = true)]
    static extern bool GetProcessMemoryInfo(SafeProcessHandle hProcess, out PROCESS_MEMORY_COUNTERS_EX ppsmemCounters, uint cb);

    [DllImport("kernel32.dll", SetLastError = true)]
    static extern bool GetProcessTimes(SafeProcessHandle hProcess, out FILETIME lpCreationTime,
        out FILETIME lpExitTime, out FILETIME lpKernelTime, out FILETIME lpUserTime);

    class SafeSnapshotHandle : SafeHandleZeroOrMinusOneIsInvalid
    {
        public SafeSnapshotHandle() : base(true) { }

        protected override bool ReleaseHandle()
        {
            return CloseHandle(this.handle);
        }
    }

    class SafeProcessHandle : SafeHandleZeroOrMinusOneIsInvalid
    {
        public SafeProcessHandle() : base(true) { }

        protected override bool ReleaseHandle()
        {
            return CloseHandle(this.handle);
        }
    }

    [DllImport("kernel32.dll")]
    static extern bool CloseHandle(IntPtr hObject);

    static long FileTimeToInt64(FILETIME ft)
    {
        ulong high = ft.dwHighDateTime;
        uint low = ft.dwLowDateTime;
        return (long)((high << 32) | low);
    }

    class ProcessInfo
    {
        public uint Pid;
        public ulong WorkingSet;
        public ulong PrivateBytes;
        public long Cpu;
        public string FileName;
    }

    static void FillProcessStats(ProcessInfo process)
    {
        using (SafeProcessHandle hProcess = OpenProcess(PROCESS_QUERY_LIMITED_INFORMATION, false, process.Pid))
        {
            if (!hProcess.IsInvalid)
            {
                PROCESS_MEMORY_COUNTERS_EX pmc = new PROCESS_MEMORY_COUNTERS_EX();
                if (GetProcessMemoryInfo(hProcess, out pmc, (uint)Marshal.SizeOf(typeof(PROCESS_MEMORY_COUNTERS_EX))))
                {
                    process.WorkingSet = pmc.WorkingSetSize / 1024;
                    process.PrivateBytes = pmc.PrivateUsage / 1024;
                }
                else
                {
                    process.WorkingSet = 0;
                    process.PrivateBytes = 0;
                }

                FILETIME ftCreation, ftExit, ftKernel, ftUser;
                if (GetProcessTimes(hProcess, out ftCreation, out ftExit, out ftKernel, out ftUser))
                {
                    long userTime = FileTimeToInt64(ftUser);
                    long kernelTime = FileTimeToInt64(ftKernel);

                    process.Cpu = (userTime + kernelTime) / 10_000_000;
                }
                else
                {
                    process.Cpu = 0;
                }
            }
            else
            {
                process.WorkingSet = 0;
                process.PrivateBytes = 0;
                process.Cpu = 0;
            }
        }
    }

    static int CompareProcessPrivateBytes(ProcessInfo a, ProcessInfo b)
    {
        return b.PrivateBytes.CompareTo(a.PrivateBytes);
    }

    static int CompareProcessCpu(ProcessInfo a, ProcessInfo b)
    {
        return b.Cpu.CompareTo(a.Cpu);
    }

    static int CompareProcessWorkingSet(ProcessInfo a, ProcessInfo b)
    {
        return b.WorkingSet.CompareTo(a.WorkingSet);
    }

    static int CompareProcessPid(ProcessInfo a, ProcessInfo b)
    {
        return b.Pid.CompareTo(a.Pid);
    }

    static string FormatProcessLine(ProcessInfo process)
    {
        string privateBytesStr = process.PrivateBytes.ToString("N0");
        string workingSetStr = process.WorkingSet.ToString("N0");
        string cpuSecondsStr = process.Cpu.ToString("N0");

        return String.Format("{0,-75} {1,8} {2,20} {3,20} {4,10}",
            process.FileName, process.Pid, workingSetStr, privateBytesStr, cpuSecondsStr);
    }
    
    static async Task Run(bool sort, Comparison<ProcessInfo> sortFunc, ChannelWriter<string> writer)
    {
        List<string> linesToFill = new List<string>();

        string header = String.Format("{0,-75} {1,8} {2,20} {3,20} {4,10}",
            "Name", "PID", "WorkingSet(kb)", "PrivateBytes(kb)", "CPU(s)");
        linesToFill.Add(header);

        List<ProcessInfo> processes = new List<ProcessInfo>();

        using (SafeSnapshotHandle hSnapshot = CreateToolhelp32Snapshot(TH32CS_SNAPPROCESS, 0))
        {
            if (!hSnapshot.IsInvalid)
            {
                PROCESSENTRY32 pEntry = new PROCESSENTRY32();
                pEntry.dwSize = (uint)Marshal.SizeOf(typeof(PROCESSENTRY32));

                if (Process32First(hSnapshot, ref pEntry))
                {
                    do
                    {
                        ProcessInfo processInfo = new ProcessInfo
                        {
                            Pid = pEntry.th32ProcessID,
                            FileName = pEntry.szExeFile
                        };
                        FillProcessStats(processInfo);
                        processes.Add(processInfo);

                    } while (Process32Next(hSnapshot, ref pEntry));
                }
            }
        }

        if (sort && sortFunc != null)
        {
            processes.Sort(sortFunc);
        }

        int count = 0;
        foreach (var process in processes)
        {
            string line = FormatProcessLine(process);
            await writer.WriteAsync(line);
            linesToFill.Add(line);
            count++;
        }
        writer.Complete();
    }

    static async Task Run(bool sort, Comparison<ProcessInfo> sortFunc, ChannelWriter<object> writer)
    {
        List<string> linesToFill = new List<string>();

        string header = String.Format("{0,-75} {1,8} {2,20} {3,20} {4,10}",
            "Name", "PID", "WorkingSet(kb)", "PrivateBytes(kb)", "CPU(s)");
        linesToFill.Add(header);

        List<ProcessInfo> processes = new List<ProcessInfo>();

        using (SafeSnapshotHandle hSnapshot = CreateToolhelp32Snapshot(TH32CS_SNAPPROCESS, 0))
        {
            if (!hSnapshot.IsInvalid)
            {
                PROCESSENTRY32 pEntry = new PROCESSENTRY32();
                pEntry.dwSize = (uint)Marshal.SizeOf(typeof(PROCESSENTRY32));

                if (Process32First(hSnapshot, ref pEntry))
                {
                    do
                    {
                        ProcessInfo processInfo = new ProcessInfo
                        {
                            Pid = pEntry.th32ProcessID,
                            FileName = pEntry.szExeFile
                        };
                        FillProcessStats(processInfo);
                        processes.Add(processInfo);

                    } while (Process32Next(hSnapshot, ref pEntry));
                }
            }
        }

        if (sort && sortFunc != null)
        {
            processes.Sort(sortFunc);
        }

        int count = 0;
        foreach (var process in processes)
        {
            string line = FormatProcessLine(process);
            await writer.WriteAsync(line);
            linesToFill.Add(line);
            count++;
        }
        writer.Complete();
    }

    public static async Task RunNoSort(ChannelWriter<object> writer, CancellationToken cancellationToken)
    {
        await Run(false, null, writer);
    }
    
    public static async Task RunSortedByCpu2(ChannelWriter<object> writer, CancellationToken cancellationToken)
    {
        await Run(true, CompareProcessCpu, writer);
    }

    public static async Task RunSortedByPrivateBytes2(ChannelWriter<object> writer, CancellationToken cancellationToken)
    {
        await Run(true, CompareProcessPrivateBytes, writer);
    }

    public static async Task RunSortedByWorkingSet2(ChannelWriter<object> writer, CancellationToken cancellationToken)
    {
        await Run(true, CompareProcessWorkingSet, writer);
    }

    public static async Task RunSortedByPid2(ChannelWriter<object> writer, CancellationToken cancellationToken)
    {
        await Run(true, CompareProcessPid, writer);
    }

    public static async Task RunSortedByCpu(ChannelWriter<string> writer, CancellationToken cancellationToken)
    {
        await Run(true, CompareProcessCpu, writer);
    }

    public static async Task RunSortedByPrivateBytes(ChannelWriter<string> writer, CancellationToken cancellationToken)
    {
        await Run(true, CompareProcessPrivateBytes, writer);
    }

    public static async Task RunSortedByWorkingSet(ChannelWriter<string> writer, CancellationToken cancellationToken)
    {
        await Run(true, CompareProcessWorkingSet, writer);
    }

    public static async Task RunSortedByPid(ChannelWriter<string> writer, CancellationToken cancellationToken)
    {
        await Run(true, CompareProcessPid, writer);
    }
    
    public static async Task KillProcessById(string line, int pid)
    {
        var process = Process.GetProcessById(pid);
        process.Kill();
        await process.WaitForExitAsync();
    }
}
public static class MemoryDumpTaker
{
    [DllImport("DbgHelp.dll", SetLastError = true)]
    private static extern bool MiniDumpWriteDump(
        IntPtr hProcess,
        int processId,
        SafeHandle hFile,
        MINIDUMP_TYPE dumpType,
        IntPtr exceptionParam,
        IntPtr userStreamParam,
        IntPtr callbackParam);

    [Flags]
    private enum MINIDUMP_TYPE : uint
    {
        MiniDumpNormal = 0x00000000,
        MiniDumpWithDataSegs = 0x00000001,
        MiniDumpWithFullMemory = 0x00000002,
        MiniDumpWithHandleData = 0x00000004,
        MiniDumpFilterMemory = 0x00000008,
        MiniDumpScanMemory = 0x00000010,
        MiniDumpWithUnloadedModules = 0x00000020,
        MiniDumpWithIndirectlyReferencedMemory = 0x00000040,
        MiniDumpFilterModulePaths = 0x00000080,
        MiniDumpWithProcessThreadData = 0x00000100,
        MiniDumpWithPrivateReadWriteMemory = 0x00000200,
        MiniDumpWithoutOptionalData = 0x00000400,
        MiniDumpWithFullMemoryInfo = 0x00000800,
        MiniDumpWithThreadInfo = 0x00001000,
        MiniDumpWithCodeSegs = 0x00002000,
        MiniDumpWithoutAuxiliaryState = 0x00004000,
        MiniDumpWithFullAuxiliaryState = 0x00008000,
        MiniDumpWithPrivateWriteCopyMemory = 0x00010000,
        MiniDumpIgnoreInaccessibleMemory = 0x00020000,
        MiniDumpWithTokenInformation = 0x00040000
    }

    public static void TakeMemoryDump(int processId, string dumpFilePath)
    {
        var process = Process.GetProcessById(processId);

        using (var dumpFile = new FileStream(dumpFilePath, FileMode.Create, FileAccess.Write, FileShare.None))
        {
            bool success = MiniDumpWriteDump(
                process.Handle,
                process.Id,
                dumpFile.SafeFileHandle,
                MINIDUMP_TYPE.MiniDumpWithFullMemory,
                IntPtr.Zero,
                IntPtr.Zero,
                IntPtr.Zero);

            if (!success)
            {
                throw new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error());
            }
        }

        Console.WriteLine($"Memory dump saved to: {dumpFilePath}");
    }
}

public static class JitDebugLauncher
{
    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool DebugBreakProcess(IntPtr hProcess);

    public static void LaunchJitDebugger(int pid)
    {
        var process = Process.GetProcessById(pid);
        bool result = DebugBreakProcess(process.Handle);
        if (!result)
        {
            throw new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error());
        }
    }
}
