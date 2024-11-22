using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.ComTypes;
using System.Threading.Channels;
using Microsoft.Win32.SafeHandles;

namespace nfzf.FileSystem;

public class StreamingWin32DriveScanner
{
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct WIN32_FIND_DATA
    {
        public FileAttributes dwFileAttributes;
        public FILETIME ftCreationTime;
        public FILETIME ftLastAccessTime;
        public FILETIME ftLastWriteTime;
        public uint nFileSizeHigh;
        public uint nFileSizeLow;
        public uint dwReserved0;
        public uint dwReserved1;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)]
        public string cFileName;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 14)]
        public string cAlternateFileName;
    }
    
    private enum FINDEX_INFO_LEVELS
    {
        FindExInfoStandard = 0,
        FindExInfoBasic = 1
    }

    private enum FINDEX_SEARCH_OPS
    {
        FindExSearchNameMatch = 0,
        FindExSearchLimitToDirectories = 1,
        FindExSearchLimitToDevices = 2
    }

    [Flags]
    private enum FIND_FIRST_EX_FLAGS : int
    {
        FIND_FIRST_EX_CASE_SENSITIVE = 0x1,
        FIND_FIRST_EX_LARGE_FETCH = 0x2,
        FIND_FIRST_EX_ON_DISK_ENTRIES_ONLY = 0x4
    }

    [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern SafeFindHandle FindFirstFile(string lpFileName, out WIN32_FIND_DATA lpFindFileData);

    [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern bool FindNextFile(SafeFindHandle hFindFile, out WIN32_FIND_DATA lpFindFileData);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool FindClose(IntPtr hFindFile);
    
    [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern SafeFindHandle FindFirstFileEx(
        string lpFileName,
        FINDEX_INFO_LEVELS fInfoLevelId,
        out WIN32_FIND_DATA lpFindFileData,
        FINDEX_SEARCH_OPS fSearchOp,
        IntPtr lpSearchFilter,
        FIND_FIRST_EX_FLAGS dwAdditionalFlags);

    private class SafeFindHandle : SafeHandleZeroOrMinusOneIsInvalid
    {
        public SafeFindHandle() : base(true) { }

        protected override bool ReleaseHandle()
        {
            return FindClose(handle);
        }
    }

    private readonly bool _includeHidden;
    
    public StreamingWin32DriveScanner(bool includeHidden = true)
    {
        _includeHidden = includeHidden;
    }


    public class ScanState
    {
        public int PendingDirectoryCount = 0;
        public int ChannelsCompleted = 0;
        public ChannelWriter<string> DirectoryChannelWriter { get; set; }
        public ChannelWriter<string> FileChannelWriter { get; set; }
        public TaskCompletionSource<bool> CompletionSource { get; } = new();
    }
    
    public async Task StartScanForDirectoriesAsync(IEnumerable<string> initialDirectory, ChannelWriter<string> cw)
    {
        var sw = Stopwatch.StartNew();
        var directoryChannel = Channel.CreateUnbounded<string>(new UnboundedChannelOptions
        {
            SingleReader = true,
            SingleWriter = false
        });

        var scanState = new ScanState
        {
            DirectoryChannelWriter = directoryChannel.Writer,
            FileChannelWriter = cw
        };

        for (int i = 0; i < initialDirectory.Count(); i++)
        {
            Interlocked.Increment(ref scanState.PendingDirectoryCount);
        }

        var tasks = new List<Task>();
        foreach (var directory in initialDirectory)
        {
            var task = ScanDirectoryForDirectoriesAsync(directory, scanState);
            tasks.Add(task);
        }

        await Parallel.ForEachAsync(directoryChannel.Reader.ReadAllAsync(), new ParallelOptions
            {
                MaxDegreeOfParallelism = Environment.ProcessorCount
            },
            async (directory, ct) =>
            {
                await ScanDirectoryForDirectoriesAsync(directory, scanState);
            });

        await Task.WhenAll(tasks);

        await scanState.CompletionSource.Task;
        //Console.WriteLine($"ScanDir for dirs: {sw}");
    }
    
    public async Task StartScanMultiAsync(IEnumerable<string> initialDirectory, ChannelWriter<string> cw)
    {
        var sw = Stopwatch.StartNew();
        var directoryChannel = Channel.CreateUnbounded<string>(new UnboundedChannelOptions
        {
            SingleReader = true,
            SingleWriter = false
        });

        var scanState = new ScanState
        {
            DirectoryChannelWriter = directoryChannel.Writer,
            FileChannelWriter = cw
        };

        for (int i = 0; i < initialDirectory.Count(); i++)
        {
            Interlocked.Increment(ref scanState.PendingDirectoryCount);
        }

        var tasks = new List<Task>();
        foreach (var directory in initialDirectory)
        {
            var task = ScanDirectoryAsync(directory, scanState);
            tasks.Add(task);
        }

        await Parallel.ForEachAsync(directoryChannel.Reader.ReadAllAsync(), new ParallelOptions
            {
                MaxDegreeOfParallelism = Environment.ProcessorCount
            },
            async (directory, ct) =>
            {
                await ScanDirectoryAsync(directory, scanState);
            });

        await Task.WhenAll(tasks);

        await scanState.CompletionSource.Task;
        //Console.WriteLine($"ScanDir: {sw}");
    }

    public async Task StartScanAsync(string initialDirectory, ChannelWriter<string> cw)
    {
        var sw = Stopwatch.StartNew();
        var directoryChannel = Channel.CreateUnbounded<string>(new UnboundedChannelOptions
        {
            SingleReader = true,
            SingleWriter = false
        });

        var scanState = new ScanState
        {
            DirectoryChannelWriter = directoryChannel.Writer,
            FileChannelWriter = cw
        };

        Interlocked.Increment(ref scanState.PendingDirectoryCount);

        var initialTask = ScanDirectoryAsync(initialDirectory, scanState);

        await Parallel.ForEachAsync(directoryChannel.Reader.ReadAllAsync(), new ParallelOptions
            {
                MaxDegreeOfParallelism = Environment.ProcessorCount
            },
            async (directory, ct) =>
            {
                await ScanDirectoryAsync(directory, scanState);
            });

        await initialTask;

        await scanState.CompletionSource.Task;
        //Console.WriteLine($"ScanDir: {sw}");
    }

    private async Task ScanDirectoryAsync(string path, ScanState scanState)
    {
        try
        {
            var newDirectories = new List<string>();

            var findData = new WIN32_FIND_DATA();
            using var findHandle = FindFirstFile(CreateSearchPath(path), out findData);
            if (!findHandle.IsInvalid)
            {
                do
                {
                    if (findData.cFileName is "." or "..")
                        continue;

                    string fullPath = CombinePath(path, findData.cFileName);

                    if (_includeHidden || (findData.dwFileAttributes & FileAttributes.Hidden) == 0)
                    {
                        if ((findData.dwFileAttributes & FileAttributes.Directory) != 0)
                        {
                            newDirectories.Add(fullPath);
                        }
                        else
                        {
                            await scanState.FileChannelWriter.WriteAsync(fullPath);
                        }
                    }
                }
                while (FindNextFile(findHandle, out findData));
            }

            foreach (var dir in newDirectories)
            {
                Interlocked.Increment(ref scanState.PendingDirectoryCount);
                await scanState.DirectoryChannelWriter.WriteAsync(dir);
            }
        }
        catch (Exception ex) when (ex is UnauthorizedAccessException or DirectoryNotFoundException or IOException)
        {
        }
        finally
        {
            if (Interlocked.Decrement(ref scanState.PendingDirectoryCount) == 0)
            {
                if (Interlocked.Exchange(ref scanState.ChannelsCompleted, 1) == 0)
                {
                    scanState.DirectoryChannelWriter.Complete();
                    scanState.FileChannelWriter.Complete();
                    scanState.CompletionSource.SetResult(true);
                }
            }
        }
    }
    
    private async Task ScanDirectoryForDirectoriesAsync(string path, ScanState scanState)
    {
        try
        {
            var newDirectories = new List<string>();

            var findData = new WIN32_FIND_DATA();
            using var findHandle = FindFirstFile(CreateSearchPath(path), out findData);
            if (!findHandle.IsInvalid)
            {
                do
                {
                    if (findData.cFileName is "." or "..")
                        continue;

                    string fullPath = CombinePath(path, findData.cFileName);

                    if (_includeHidden || (findData.dwFileAttributes & FileAttributes.Hidden) == 0)
                    {
                        if ((findData.dwFileAttributes & FileAttributes.Directory) != 0)
                        {
                            await scanState.FileChannelWriter.WriteAsync(fullPath);
                            newDirectories.Add(fullPath);
                        }
                    }
                }
                while (FindNextFile(findHandle, out findData));
            }

            foreach (var dir in newDirectories)
            {
                Interlocked.Increment(ref scanState.PendingDirectoryCount);
                await scanState.DirectoryChannelWriter.WriteAsync(dir);
            }
        }
        catch (Exception ex) when (ex is UnauthorizedAccessException or DirectoryNotFoundException or IOException)
        {
        }
        finally
        {
            if (Interlocked.Decrement(ref scanState.PendingDirectoryCount) == 0)
            {
                if (Interlocked.Exchange(ref scanState.ChannelsCompleted, 1) == 0)
                {
                    scanState.DirectoryChannelWriter.Complete();
                    scanState.FileChannelWriter.Complete();
                    scanState.CompletionSource.SetResult(true);
                }
            }
        }
    }

    private static string CreateSearchPath(string path)
    {
        Span<char> searchPath = stackalloc char[path.Length + 2];
        path.AsSpan().CopyTo(searchPath);
        searchPath[path.Length] = '\\';
        searchPath[path.Length + 1] = '*';
        return searchPath.ToString();
    }

    private static string CombinePath(string path, ReadOnlySpan<char> filename)
    {
        int fullPathLength = path.Length + 1 + filename.Length;
        Span<char> fullPath = stackalloc char[fullPathLength];
    
        path.AsSpan().CopyTo(fullPath);
        fullPath[path.Length] = '\\';
        filename.CopyTo(fullPath[(path.Length + 1)..]);
    
        return fullPath.ToString();
    }
}

public class StreamingWin32DriveScanner2
{
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct WIN32_FIND_DATA
    {
        public FileAttributes dwFileAttributes;
        public FILETIME ftCreationTime;
        public FILETIME ftLastAccessTime;
        public FILETIME ftLastWriteTime;
        public uint nFileSizeHigh;
        public uint nFileSizeLow;
        public uint dwReserved0;
        public uint dwReserved1;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)]
        public string cFileName;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 14)]
        public string cAlternateFileName;
    }
    
    private enum FINDEX_INFO_LEVELS
    {
        FindExInfoStandard = 0,
        FindExInfoBasic = 1
    }

    private enum FINDEX_SEARCH_OPS
    {
        FindExSearchNameMatch = 0,
        FindExSearchLimitToDirectories = 1,
        FindExSearchLimitToDevices = 2
    }

    [Flags]
    private enum FIND_FIRST_EX_FLAGS : int
    {
        FIND_FIRST_EX_CASE_SENSITIVE = 0x1,
        FIND_FIRST_EX_LARGE_FETCH = 0x2,
        FIND_FIRST_EX_ON_DISK_ENTRIES_ONLY = 0x4
    }

    [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern SafeFindHandle FindFirstFile(string lpFileName, out WIN32_FIND_DATA lpFindFileData);

    [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern bool FindNextFile(SafeFindHandle hFindFile, out WIN32_FIND_DATA lpFindFileData);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool FindClose(IntPtr hFindFile);
    
    [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern SafeFindHandle FindFirstFileEx(
        string lpFileName,
        FINDEX_INFO_LEVELS fInfoLevelId,
        out WIN32_FIND_DATA lpFindFileData,
        FINDEX_SEARCH_OPS fSearchOp,
        IntPtr lpSearchFilter,
        FIND_FIRST_EX_FLAGS dwAdditionalFlags);

    private class SafeFindHandle : SafeHandleZeroOrMinusOneIsInvalid
    {
        public SafeFindHandle() : base(true) { }

        protected override bool ReleaseHandle()
        {
            return FindClose(handle);
        }
    }

    private readonly bool _includeHidden;
    
    public StreamingWin32DriveScanner2(bool includeHidden = true)
    {
        _includeHidden = includeHidden;
    }

    private class ScanState
    {
        public int PendingDirectoryCount = 0;
        public int ChannelsCompleted = 0;
        public ChannelWriter<(int depth, string path)> DirectoryChannelWriter { get; set; }
        public ChannelWriter<string> FileChannelWriter { get; set; }
        public TaskCompletionSource<bool> CompletionSource { get; } = new();
    }
    
    private int GetDirectoryDepth(string path)
    {
        int depth = 0;
        var directoryInfo = new DirectoryInfo(path);
        while (directoryInfo.Parent != null)
        {
            depth++;
            directoryInfo = directoryInfo.Parent;
        }
        return depth;
    }
    
    public async Task StartScanForDirectoriesAsync(
        IEnumerable<string> initialDirectory,
        ChannelWriter<string> cw,
        int maxDepth,
        CancellationToken cancellationToken)
    {
        var sw = Stopwatch.StartNew();
        var directoryChannel = Channel.CreateUnbounded<(int, string)>(new UnboundedChannelOptions
        {
            SingleReader = false,
            SingleWriter = false
        });

        var scanState = new ScanState
        {
            DirectoryChannelWriter = directoryChannel.Writer,
            FileChannelWriter = cw
        };

        for (int i = 0; i < initialDirectory.Count(); i++)
        {
            Interlocked.Increment(ref scanState.PendingDirectoryCount);
        }

        foreach (var directory in initialDirectory)
        {
            await cw.WriteAsync(directory, cancellationToken);
            await directoryChannel.Writer.WriteAsync((0, directory), cancellationToken);
        }

        await Parallel.ForEachAsync(directoryChannel.Reader.ReadAllAsync(cancellationToken), new ParallelOptions
            {
                MaxDegreeOfParallelism = Environment.ProcessorCount
            },
            async (item, ct) =>
            {
                await ScanDirectoryForDirectoriesAsync(item.Item2, item.Item1, maxDepth, scanState, cancellationToken);
            });

        await scanState.CompletionSource.Task;
        //Console.WriteLine($"ScanDir for dirs: {sw}");
    }
    
    private async Task ScanDirectoryForDirectoriesAsync(string path, int currentDepth, int maxDepth, ScanState scanState, CancellationToken cancellationToken)
    {
        if (currentDepth >= maxDepth || cancellationToken.IsCancellationRequested)
        {
            if (Interlocked.Decrement(ref scanState.PendingDirectoryCount) == 0)
            {
                CompleteScan(scanState);
            }
            return;
        }

        try
        {
            var newDirectories = new List<string>();
            if (cancellationToken.IsCancellationRequested)
            {
                if (Interlocked.Decrement(ref scanState.PendingDirectoryCount) == 0)
                {
                    CompleteScan(scanState);
                }

                return;
            }

            var findData = new WIN32_FIND_DATA();
            using var findHandle = FindFirstFile(CreateSearchPath(path), out findData);
            if (!findHandle.IsInvalid)
            {
                do
                {
                    if (cancellationToken.IsCancellationRequested)
                    {
                        if (Interlocked.Decrement(ref scanState.PendingDirectoryCount) == 0)
                        {
                            CompleteScan(scanState);
                        }

                        return;
                    }

                    if (findData.cFileName is "." or "..")
                        continue;

                    string fullPath = CombinePath(path, findData.cFileName);

                    if (_includeHidden || (findData.dwFileAttributes & FileAttributes.Hidden) == 0)
                    {
                        if ((findData.dwFileAttributes & FileAttributes.Directory) != 0)
                        {
                            await scanState.FileChannelWriter.WriteAsync(fullPath, cancellationToken);
                            newDirectories.Add(fullPath);
                        }
                        else
                        {
                            await scanState.FileChannelWriter.WriteAsync(fullPath, cancellationToken);
                        }
                    }
                } while (FindNextFile(findHandle, out findData));
            }

            foreach (var dir in newDirectories)
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    if (Interlocked.Decrement(ref scanState.PendingDirectoryCount) == 0)
                    {
                        CompleteScan(scanState);
                    }

                    return;
                }

                Interlocked.Increment(ref scanState.PendingDirectoryCount);
                await scanState.DirectoryChannelWriter.WriteAsync((currentDepth + 1, dir), cancellationToken);
            }
        }
        catch (Exception ex) when (ex is UnauthorizedAccessException or DirectoryNotFoundException or IOException)
        {
        }
        catch (Exception e)
        {
            //Console.WriteLine(e);
        }
        finally
        {
            if (Interlocked.Decrement(ref scanState.PendingDirectoryCount) == 0)
            {
                CompleteScan(scanState);
            }
        }
    }

    private static string CreateSearchPath(string path)
    {
        Span<char> searchPath = stackalloc char[path.Length + 2];
        path.AsSpan().CopyTo(searchPath);
        searchPath[path.Length] = '\\';
        searchPath[path.Length + 1] = '*';
        return searchPath.ToString();
    }

    private static string CombinePath(string path, ReadOnlySpan<char> filename)
    {
        int fullPathLength = path.Length + 1 + filename.Length;
        Span<char> fullPath = stackalloc char[fullPathLength];
    
        path.AsSpan().CopyTo(fullPath);
        fullPath[path.Length] = '\\';
        filename.CopyTo(fullPath[(path.Length + 1)..]);
    
        return fullPath.ToString();
    }
    
    private void CompleteScan(ScanState scanState)
    {
        if (Interlocked.Exchange(ref scanState.ChannelsCompleted, 1) == 0)
        {
            scanState.DirectoryChannelWriter.Complete();
            scanState.FileChannelWriter.Complete();
            scanState.CompletionSource.SetResult(true);
        }
    }
}
