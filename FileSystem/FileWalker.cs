using System.Collections.Concurrent;
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
    
    public StreamingWin32DriveScanner(bool includeHidden = false)
    {
        _includeHidden = includeHidden;
    }
    
    public void StartScanAsync2(string rootPath, ChannelWriter<string> channelWriter)
    {
        Task.Run(async () => {
            try
            {
                await ScanDirectoryAsync(rootPath, channelWriter);
            }
            finally
            {
                channelWriter.Complete();
            }
        });
    }
    
    
    public class ScanState
    {
        public int PendingDirectoryCount = 0;
        public int ChannelsCompleted = 0;
        public ChannelWriter<string> DirectoryChannelWriter { get; set; }
        public ChannelWriter<string> FileChannelWriter { get; set; }
        public TaskCompletionSource<bool> CompletionSource { get; } = new();
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
        Console.WriteLine($"ScanDir: {sw}");
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
    
    public async Task StartScanAsync3(string initialDirectory, ChannelWriter<string> cw)
    {
        var directoryChannel = Channel.CreateUnbounded<string>(new UnboundedChannelOptions 
        { 
            SingleReader = true,
            SingleWriter = false
        });

        Task.Run(async () =>
        {
            await ScanDirectory(initialDirectory, directoryChannel.Writer, cw);
        });
            
        await Parallel.ForEachAsync(directoryChannel.Reader.ReadAllAsync(), new ParallelOptions()
            {
                MaxDegreeOfParallelism = Environment.ProcessorCount
            },
            async (directory, ct) =>
            {
                await ScanDirectory(directory, directoryChannel.Writer, cw);
            });
    }

    private async Task ScanDirectory(string path, ChannelWriter<string> directoryChannel, ChannelWriter<string> fileChannel)
    {
        try
        {
            var findData = new WIN32_FIND_DATA();
            using var findHandle = FindFirstFile(CreateSearchPath(path), out findData);
            if (!findHandle.IsInvalid)
            {
                do
                {
                    if (findData.cFileName is "." or "..")
                        continue;

                    string fullPath = CombinePath(path, findData.cFileName);

                    if ((findData.dwFileAttributes & FileAttributes.Directory) != 0)
                    {
                        if (_includeHidden || (findData.dwFileAttributes & FileAttributes.Hidden) != FileAttributes.Hidden)
                        {
                            await directoryChannel.WriteAsync(fullPath);
                        }
                    }
                    else
                    {
                        if (_includeHidden || (findData.dwFileAttributes & FileAttributes.Hidden) != FileAttributes.Hidden)
                        {
                            await fileChannel.WriteAsync(fullPath);
                        }
                    }
                }
                while (FindNextFile(findHandle, out findData));
            }
        }
        catch (Exception ex) when (
            ex is UnauthorizedAccessException or
                DirectoryNotFoundException or
                IOException)
        {
            return;
        }
    }
    
    private async Task ScanDirectoryAsync(string path, ChannelWriter<string> writer)
    {
        var directories = new List<string>();
        try
        {
            var findData = new WIN32_FIND_DATA();
            using var findHandle = FindFirstFile(CreateSearchPath(path), out findData);
            if (!findHandle.IsInvalid)
            {
                do
                {
                    if (findData.cFileName is "." or "..")
                        continue;

                    string fullPath = CombinePath(path, findData.cFileName);
                
                    if ((findData.dwFileAttributes & FileAttributes.Directory) != 0)
                    {
                        if (_includeHidden || (findData.dwFileAttributes & FileAttributes.Hidden) != FileAttributes.Hidden)
                        {
                            directories.Add(fullPath);
                        }
                    }
                    else
                    {
                        if (_includeHidden || (findData.dwFileAttributes & FileAttributes.Hidden) != FileAttributes.Hidden)
                        {
                            await writer.WriteAsync(fullPath);
                        }
                    }
                }
                while (FindNextFile(findHandle, out findData));
            }
        }
        catch (Exception ex) when (
            ex is UnauthorizedAccessException or
                DirectoryNotFoundException or
                IOException)
        {
            return;
        }

        await Parallel.ForEachAsync(
            directories,
            new ParallelOptions { MaxDegreeOfParallelism = Environment.ProcessorCount },
            async (directory, ct) =>
            {
                try
                {
                    await ScanDirectoryAsync(directory, writer);
                }
                catch (Exception ex) when (
                    ex is UnauthorizedAccessException or
                        DirectoryNotFoundException or
                        IOException)
                {
                    return;
                }
            }
        );
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

    public IEnumerable<string> Scan(string rootPath)
    {
        using var results = new BlockingCollection<string>();

        Task.Run(() => {
            try
            {
                ScanDirectory(rootPath, results);
            }
            finally
            {
                results.CompleteAdding();
            }
        });

        foreach (var result in results.GetConsumingEnumerable())
        {
            yield return result;
        }
    }

    private void ScanDirectory(string path, BlockingCollection<string> results)
    {
        var searchPath = Path.Combine(path, "*");
        var directories = new List<string>();

        try
        {
            var findData = new WIN32_FIND_DATA();
            using var findHandle = FindFirstFileEx(
                CreateSearchPath(path),
                FINDEX_INFO_LEVELS.FindExInfoBasic,
                out findData,
                FINDEX_SEARCH_OPS.FindExSearchNameMatch,
                IntPtr.Zero,
                FIND_FIRST_EX_FLAGS.FIND_FIRST_EX_LARGE_FETCH);

            if (!findHandle.IsInvalid)
            {
                do
                {
                    if (findData.cFileName is "." or "..")
                        continue;

                    var fullPath = Path.Combine(path, findData.cFileName);

                    if ((findData.dwFileAttributes & FileAttributes.Directory) != 0)
                    {
                        if (_includeHidden || (findData.dwFileAttributes & FileAttributes.Hidden) != FileAttributes.Hidden)
                        {
                            directories.Add(fullPath);
                        }
                    }
                    else
                    {
                        if (_includeHidden || (findData.dwFileAttributes & FileAttributes.Hidden) != FileAttributes.Hidden)
                        {
                            results.Add(fullPath);
                        }
                    }
                }
                while (FindNextFile(findHandle, out findData));
            }
        }
        catch (Exception ex) when (
            ex is UnauthorizedAccessException or
                DirectoryNotFoundException or
                IOException)
        {
            return;
        }

        Parallel.ForEach(
            directories,
            new ParallelOptions { MaxDegreeOfParallelism = Environment.ProcessorCount },
            directory =>
            {
                try
                {
                    ScanDirectory(directory, results);
                }
                catch (Exception ex) when (
                    ex is UnauthorizedAccessException or
                        DirectoryNotFoundException or
                        IOException)
                {
                    return;
                }
            }
        );
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
    private static extern SafeFindHandle FindFirstFileEx(
        string lpFileName,
        FINDEX_INFO_LEVELS fInfoLevelId,
        out WIN32_FIND_DATA lpFindFileData,
        FINDEX_SEARCH_OPS fSearchOp,
        IntPtr lpSearchFilter,
        FIND_FIRST_EX_FLAGS dwAdditionalFlags);

    [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern bool FindNextFile(SafeFindHandle hFindFile, out WIN32_FIND_DATA lpFindFileData);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool FindClose(IntPtr hFindFile);

    private class SafeFindHandle : SafeHandleZeroOrMinusOneIsInvalid
    {
        public SafeFindHandle() : base(true) { }

        protected override bool ReleaseHandle()
        {
            return FindClose(handle);
        }
    }

    private readonly bool _includeHidden;

    public StreamingWin32DriveScanner2(bool includeHidden = false)
    {
        _includeHidden = includeHidden;
    }

    public ChannelReader<string> ScanAsync(string rootPath)
    {
        var channel = Channel.CreateUnbounded<string>(new UnboundedChannelOptions
        {
            SingleReader = true,
            SingleWriter = false
        });

        Task.Run(async () =>
        {
            try
            {
                await ScanDirectoryAsync(rootPath, channel.Writer);
            }
            finally
            {
                channel.Writer.Complete();
            }
        });

        return channel.Reader;
    }

    private async Task ScanDirectoryAsync(string path, ChannelWriter<string> writer)
    {
        if (!Directory.Exists(path))
            return;

        try
        {
            var findData = new WIN32_FIND_DATA();
            using var findHandle = FindFirstFileEx(
                CreateSearchPath(path),
                FINDEX_INFO_LEVELS.FindExInfoBasic,
                out findData,
                FINDEX_SEARCH_OPS.FindExSearchNameMatch,
                IntPtr.Zero,
                FIND_FIRST_EX_FLAGS.FIND_FIRST_EX_LARGE_FETCH);

            if (!findHandle.IsInvalid)
            {
                do
                {
                    if (findData.cFileName is "." or "..")
                        continue;

                    string fullPath = CombinePath(path, findData.cFileName);

                    if ((findData.dwFileAttributes & FileAttributes.Directory) != 0)
                    {
                        if (_includeHidden || (findData.dwFileAttributes & FileAttributes.Hidden) == 0)
                        {
                            // Process directory immediately
                            await ScanDirectoryAsync(fullPath, writer);
                        }
                    }
                    else
                    {
                        if (_includeHidden || (findData.dwFileAttributes & FileAttributes.Hidden) == 0)
                        {
                            await writer.WriteAsync(fullPath);
                        }
                    }
                }
                while (FindNextFile(findHandle, out findData));
            }
        }
        catch (Exception ex) when (
            ex is UnauthorizedAccessException or
                DirectoryNotFoundException or
                IOException)
        {
            // Handle exceptions gracefully
            return;
        }
    }

    private static string CreateSearchPath(string path)
    {
        return Path.Combine(path.TrimEnd(Path.DirectorySeparatorChar), "*");
    }

    private static string CombinePath(string path, string filename)
    {
        return Path.Combine(path, filename);
    }

    public IEnumerable<string> Scan(string rootPath)
    {
        using var results = new BlockingCollection<string>();

        Task.Run(() =>
        {
            try
            {
                ScanDirectory(rootPath, results);
            }
            finally
            {
                results.CompleteAdding();
            }
        });

        foreach (var result in results.GetConsumingEnumerable())
        {
            yield return result;
        }
    }

    private void ScanDirectory(string path, BlockingCollection<string> results)
    {
        if (!Directory.Exists(path))
            return;

        try
        {
            var findData = new WIN32_FIND_DATA();
            using var findHandle = FindFirstFileEx(
                CreateSearchPath(path),
                FINDEX_INFO_LEVELS.FindExInfoBasic,
                out findData,
                FINDEX_SEARCH_OPS.FindExSearchNameMatch,
                IntPtr.Zero,
                FIND_FIRST_EX_FLAGS.FIND_FIRST_EX_LARGE_FETCH);

            if (!findHandle.IsInvalid)
            {
                do
                {
                    if (findData.cFileName is "." or "..")
                        continue;

                    string fullPath = CombinePath(path, findData.cFileName);

                    if ((findData.dwFileAttributes & FileAttributes.Directory) != 0)
                    {
                        if (_includeHidden || (findData.dwFileAttributes & FileAttributes.Hidden) == 0)
                        {
                            // Process directory immediately
                            ScanDirectory(fullPath, results);
                        }
                    }
                    else
                    {
                        if (_includeHidden || (findData.dwFileAttributes & FileAttributes.Hidden) == 0)
                        {
                            results.Add(fullPath);
                        }
                    }
                }
                while (FindNextFile(findHandle, out findData));
            }
        }
        catch (Exception ex) when (
            ex is UnauthorizedAccessException or
                DirectoryNotFoundException or
                IOException)
        {
            // Handle exceptions gracefully
            return;
        }
    }
}
