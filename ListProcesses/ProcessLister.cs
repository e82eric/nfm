using System.Diagnostics;
using System.Globalization;

namespace nfzf.ListProcesses;
public class ProcessLister
{
    public class ProcessInfo
    {
        public int Pid;
        public ulong WorkingSet;
        public ulong PrivateBytes;
        public long Cpu; // in seconds
        public string FileName;
    }

    static void FillProcessStats(ProcessInfo process)
    {
        try
        {
            Process p = Process.GetProcessById(process.Pid);
            process.WorkingSet = (ulong)(p.WorkingSet64 / 1024);
            process.PrivateBytes = (ulong)(p.PrivateMemorySize64 / 1024);
            process.Cpu = (long)p.TotalProcessorTime.TotalSeconds;
        }
        catch (Exception)
        {
            process.WorkingSet = 0;
            process.PrivateBytes = 0;
            process.Cpu = 0;
        }
    }

    public static int CompareProcessPrivateBytes(ProcessInfo a, ProcessInfo b)
    {
        return b.PrivateBytes.CompareTo(a.PrivateBytes);
    }

    public static int CompareProcessCpu(ProcessInfo a, ProcessInfo b)
    {
        return b.Cpu.CompareTo(a.Cpu);
    }

    public static int CompareProcessWorkingSet(ProcessInfo a, ProcessInfo b)
    {
        return b.WorkingSet.CompareTo(a.WorkingSet);
    }

    public static int CompareProcessPid(ProcessInfo a, ProcessInfo b)
    {
        return b.Pid.CompareTo(a.Pid);
    }

    static string FormatProcessLine(ProcessInfo process)
    {
        string privateBytesStr = process.PrivateBytes.ToString("N0", CultureInfo.CurrentCulture);
        string workingSetStr = process.WorkingSet.ToString("N0", CultureInfo.CurrentCulture);
        string cpuSecondsStr = process.Cpu.ToString("N0", CultureInfo.CurrentCulture);

        return String.Format("{0,-75} {1,8} {2,20} {3,20} {4,10}",
            process.FileName, process.Pid, workingSetStr, privateBytesStr, cpuSecondsStr);
    }

    static IEnumerable<string> Run(bool sort, Comparison<ProcessInfo> sortFunc)
    {
        List<string> linesToFill = new List<string>();

        string header = String.Format("{0,-75} {1,8} {2,20} {3,20} {4,10}",
            "Name", "PID", "WorkingSet(kb)", "PrivateBytes(kb)", "CPU(s)");
        linesToFill.Add(header);

        List<ProcessInfo> processes = new List<ProcessInfo>();

        foreach (Process p in Process.GetProcesses())
        {
            ProcessInfo processInfo = new ProcessInfo();
            processInfo.Pid = p.Id;
            processInfo.FileName = p.ProcessName;
            FillProcessStats(processInfo);
            processes.Add(processInfo);
        }

        if (sort && sortFunc != null)
        {
            processes.Sort(sortFunc);
        }

        int count = 0;
        foreach (var process in processes)
        {
            string line = FormatProcessLine(process);
            yield return line;
            linesToFill.Add(line);
            count++;
        }
    }

    public static IEnumerable<string> RunNoSort()
    {
        return Run(false, null);
    }

    public static IEnumerable<string> RunSortedByCpu()
    {
        return Run(true, CompareProcessCpu);
    }

    public static IEnumerable<string> RunSortedByPrivateBytes()
    {
        return Run(true, CompareProcessPrivateBytes);
    }

    public static IEnumerable<string> RunSortedByWorkingSet()
    {
        return Run(true, CompareProcessWorkingSet);
    }

    public static IEnumerable<string> RunSortedByPid()
    {
        return Run(true, CompareProcessPid);
    }
}
