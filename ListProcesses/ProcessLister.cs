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

    public static async Task RunNoSort(ChannelWriter<string> writer)
    {
        await Run(false, null, writer);
    }

    //public static IEnumerable<string> RunSortedByCpu()
    //{
    //    return Run(true, CompareProcessCpu);
    //}

    //public static IEnumerable<string> RunSortedByPrivateBytes()
    //{
    //    return Run(true, CompareProcessPrivateBytes);
    //}

    //public static IEnumerable<string> RunSortedByWorkingSet()
    //{
    //    return Run(true, CompareProcessWorkingSet);
    //}

    //public static IEnumerable<string> RunSortedByPid()
    //{
    //    return Run(true, CompareProcessPid);
    //}
    
    public static void KillProcessById(string line)
    {
        var match = Regex.Match(line, @"\s+([0-9]+)\s+");
        if (!match.Success)
        {
            return;
        }

        var pidString = match.Groups[1].Value;

        var pid = Convert.ToInt32(pidString);
        var process = Process.GetProcessById(pid);
        process.Kill();
        process.WaitForExit();
        Console.WriteLine($"Process with PID {pid} has been terminated.");
    }
}