using System.Runtime.InteropServices;
using System.Runtime.InteropServices.ComTypes;
using System.Threading.Channels;
using Microsoft.Win32.SafeHandles;

namespace nfzf.FileSystem;

public class FileWalker
{
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private unsafe struct WIN32_FIND_DATA
    {
        public FileAttributes dwFileAttributes;
        public FILETIME ftCreationTime;
        public FILETIME ftLastAccessTime;
        public FILETIME ftLastWriteTime;
        public uint nFileSizeHigh;
        public uint nFileSizeLow;
        public uint dwReserved0;
        public uint dwReserved1;
        public fixed char cFileName[260];
        public fixed char cAlternateFileName[14];
        public ReadOnlySpan<char> GetFileName()
        {
            fixed (char* ptr = cFileName)
            {
                return MemoryMarshal.CreateReadOnlySpanFromNullTerminated(ptr);
            }
        }
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
    private unsafe static extern SafeFindHandle FindFirstFile(char* lpFileName, out WIN32_FIND_DATA lpFindFileData);

    [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern bool FindNextFile(SafeFindHandle hFindFile, out WIN32_FIND_DATA lpFindFileData);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool FindClose(IntPtr hFindFile);
    
    [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private unsafe static extern SafeFindHandle FindFirstFileEx(
        char* lpFileName,
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
    
    public FileWalker(bool includeHidden = true)
    {
        _includeHidden = includeHidden;
    }

    private class ScanState(
        ChannelWriter<(int depth, string path)> directoryChannelWriter,
        ChannelWriter<string> fileChannelWriter)
    {
        public int PendingDirectoryCount = 0;
        public int ChannelsCompleted = 0;

        public ChannelWriter<(int depth, string path)> DirectoryChannelWriter { get; set; } = directoryChannelWriter;
        public ChannelWriter<string> FileChannelWriter { get; set; } = fileChannelWriter;
        public TaskCompletionSource<bool> CompletionSource { get; } = new();
    }

    public async Task StartScanForDirectoriesAsync(
        IEnumerable<string> initialDirectory,
        ChannelWriter<string> cw,
        int maxDepth,
        bool directoriesOnly,
        bool filesOnly,
        CancellationToken cancellationToken)
    {
        var directoryChannel = Channel.CreateUnbounded<(int, string)>(new UnboundedChannelOptions
        {
            SingleReader = false,
            SingleWriter = false
        });

        var scanState = new ScanState(directoryChannel.Writer, cw);

        for (int i = 0; i < initialDirectory.Count(); i++)
        {
            Interlocked.Increment(ref scanState.PendingDirectoryCount);
        }

        foreach (var directory in initialDirectory)
        {
            if (!filesOnly)
            {
                await cw.WriteAsync(directory, cancellationToken);
            }
            await directoryChannel.Writer.WriteAsync((0, directory), cancellationToken);
        }

        try
        {
            await Parallel.ForEachAsync(directoryChannel.Reader.ReadAllAsync(cancellationToken), new ParallelOptions
                {
                    MaxDegreeOfParallelism = Environment.ProcessorCount
                },
                async (item, ct) =>
                {
                    await ScanDirectoryForDirectoriesAsync(item.Item2,
                        item.Item1,
                        maxDepth,
                        scanState,
                        directoriesOnly,
                        filesOnly,
                        cancellationToken);
                });
        }
        catch (TaskCanceledException)
        {
        }

        await scanState.CompletionSource.Task;
    }
    
    private unsafe Task ScanDirectoryForDirectoriesAsync(
        string path,
        int currentDepth,
        int maxDepth,
        ScanState scanState,
        bool directoriesOnly,
        bool filesOnly,
        CancellationToken cancellationToken)
    {
        if (currentDepth >= maxDepth || cancellationToken.IsCancellationRequested)
        {
            if (Interlocked.Decrement(ref scanState.PendingDirectoryCount) == 0)
            {
                CompleteScan(scanState);
            }
            return Task.CompletedTask;
        }

        try
        {
            if (cancellationToken.IsCancellationRequested)
            {
                if (Interlocked.Decrement(ref scanState.PendingDirectoryCount) == 0)
                {
                    CompleteScan(scanState);
                }

                return Task.CompletedTask;
            }
            
            Span<char> fullPathBuffer = stackalloc char[2600];
            var findData = new WIN32_FIND_DATA();
            Span<char> searchPath = stackalloc char[path.Length + 3];
            CreateSearchPath(path, searchPath);
            fixed (char* searchPathPtr = searchPath)
            {
                using var findHandle = FindFirstFileEx(
                    searchPathPtr,
                    FINDEX_INFO_LEVELS.FindExInfoBasic,
                    out findData,
                    FINDEX_SEARCH_OPS.FindExSearchNameMatch,
                    IntPtr.Zero,
                    FIND_FIRST_EX_FLAGS.FIND_FIRST_EX_LARGE_FETCH);
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

                            return Task.CompletedTask;
                        }
                        
                        var fileName = findData.GetFileName();

                        if (fileName.Length == 1 && fileName[0] == '.' ||
                            fileName.Length == 2 && fileName[0] == '.' && fileName[1] == '.')
                        {
                            continue;
                        }

                        if (!TryCombinePath(path.AsSpan(), fileName, fullPathBuffer, out var fullPath))
                        {
                            continue;
                        }

                        if (_includeHidden || (findData.dwFileAttributes & FileAttributes.Hidden) == 0)
                        {
                            var toSend = fullPath[..^1].ToString();
                            if ((findData.dwFileAttributes & FileAttributes.Directory) != 0)
                            {
                                if (!filesOnly)
                                {
                                    scanState.FileChannelWriter.TryWrite(toSend);
                                }

                                Interlocked.Increment(ref scanState.PendingDirectoryCount);
                                scanState.DirectoryChannelWriter.TryWrite((currentDepth + 1, toSend));
                            }
                            else
                            {
                                if (!directoriesOnly)
                                {
                                    scanState.FileChannelWriter.TryWrite(toSend);
                                }
                            }
                        }
                    } while (FindNextFile(findHandle, out findData));
                }
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
        return Task.CompletedTask;
    }

    private static void CreateSearchPath(ReadOnlySpan<char> path, Span<char> searchPath)
    {
        path.CopyTo(searchPath);
        searchPath[path.Length] = '\\';
        searchPath[path.Length + 1] = '*';
        searchPath[path.Length + 2] = '\0';
    }
    private static bool TryCombinePath(ReadOnlySpan<char> path, ReadOnlySpan<char> fileName, Span<char> buffer, out ReadOnlySpan<char> result)
    {
        if (path.Length + 1 + fileName.Length + 1 > buffer.Length)
        {
            result = default;
            return false;
        }
    
        path.CopyTo(buffer);
        buffer[path.Length] = '\\';
        fileName.CopyTo(buffer[(path.Length + 1)..]);
        buffer[path.Length + 1 + fileName.Length] = '\0';
    
        result = buffer[..(path.Length + 1 + fileName.Length + 1)];
        return true;
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

public class FileWalker2
{
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private unsafe struct WIN32_FIND_DATA
    {
        public FileAttributes dwFileAttributes;
        public FILETIME ftCreationTime;
        public FILETIME ftLastAccessTime;
        public FILETIME ftLastWriteTime;
        public uint nFileSizeHigh;
        public uint nFileSizeLow;
        public uint dwReserved0;
        public uint dwReserved1;
        public fixed char cFileName[260];
        public fixed char cAlternateFileName[14];
        public ReadOnlySpan<char> GetFileName()
        {
            fixed (char* ptr = cFileName)
            {
                return MemoryMarshal.CreateReadOnlySpanFromNullTerminated(ptr);
            }
        }
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
    private unsafe static extern SafeFindHandle FindFirstFile(char* lpFileName, out WIN32_FIND_DATA lpFindFileData);

    [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern bool FindNextFile(SafeFindHandle hFindFile, out WIN32_FIND_DATA lpFindFileData);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool FindClose(IntPtr hFindFile);
    
    [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private unsafe static extern SafeFindHandle FindFirstFileEx(
        char* lpFileName,
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
    
    public FileWalker2(bool includeHidden = true)
    {
        _includeHidden = includeHidden;
    }

    private class ScanState(
        ChannelWriter<(int depth, string path)> directoryChannelWriter,
        ChannelWriter<(string, string)> fileChannelWriter)
    {
        public int PendingDirectoryCount = 0;
        public int ChannelsCompleted = 0;

        public ChannelWriter<(int depth, string path)> DirectoryChannelWriter { get; set; } = directoryChannelWriter;
        public ChannelWriter<(string, string)> FileChannelWriter { get; set; } = fileChannelWriter;
        public TaskCompletionSource<bool> CompletionSource { get; } = new();
    }

    public async Task StartScanForDirectoriesAsync(
        IEnumerable<string> initialDirectory,
        ChannelWriter<(string, string)> cw,
        int maxDepth,
        bool directoriesOnly,
        bool filesOnly,
        CancellationToken cancellationToken)
    {
        var directoryChannel = Channel.CreateUnbounded<(int, string)>(new UnboundedChannelOptions
        {
            SingleReader = false,
            SingleWriter = false
        });

        var scanState = new ScanState(directoryChannel.Writer, cw);

        for (int i = 0; i < initialDirectory.Count(); i++)
        {
            Interlocked.Increment(ref scanState.PendingDirectoryCount);
        }

        foreach (var directory in initialDirectory)
        {
            if (!filesOnly)
            {
                await cw.WriteAsync((directory, string.Empty), cancellationToken);
            }
            await directoryChannel.Writer.WriteAsync((0, directory), cancellationToken);
        }

        try
        {
            await Parallel.ForEachAsync(directoryChannel.Reader.ReadAllAsync(cancellationToken), new ParallelOptions
                {
                    MaxDegreeOfParallelism = Environment.ProcessorCount
                },
                async (item, ct) =>
                {
                    await ScanDirectoryForDirectoriesAsync(item.Item2,
                        item.Item1,
                        maxDepth,
                        scanState,
                        directoriesOnly,
                        filesOnly,
                        cancellationToken);
                });
        }
        catch (TaskCanceledException)
        {
        }

        await scanState.CompletionSource.Task;
    }
    
    private unsafe Task ScanDirectoryForDirectoriesAsync(
        string path,
        int currentDepth,
        int maxDepth,
        ScanState scanState,
        bool directoriesOnly,
        bool filesOnly,
        CancellationToken cancellationToken)
    {
        if (currentDepth >= maxDepth || cancellationToken.IsCancellationRequested)
        {
            if (Interlocked.Decrement(ref scanState.PendingDirectoryCount) == 0)
            {
                CompleteScan(scanState);
            }
            return Task.CompletedTask;
        }

        try
        {
            if (cancellationToken.IsCancellationRequested)
            {
                if (Interlocked.Decrement(ref scanState.PendingDirectoryCount) == 0)
                {
                    CompleteScan(scanState);
                }

                return Task.CompletedTask;
            }
            
            Span<char> fullPathBuffer = stackalloc char[2600];
            var findData = new WIN32_FIND_DATA();
            Span<char> searchPath = stackalloc char[path[^1] == '\\' ? path.Length + 2 : path.Length + 3];
            CreateSearchPath(path, searchPath);
            //Console.WriteLine(searchPath.ToString());
            fixed (char* searchPathPtr = searchPath)
            {
                using var findHandle = FindFirstFileEx(
                    searchPathPtr,
                    FINDEX_INFO_LEVELS.FindExInfoBasic,
                    out findData,
                    FINDEX_SEARCH_OPS.FindExSearchNameMatch,
                    IntPtr.Zero,
                    FIND_FIRST_EX_FLAGS.FIND_FIRST_EX_LARGE_FETCH);
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

                            return Task.CompletedTask;
                        }
                        
                        var fileName = findData.GetFileName();

                        if (fileName.Length == 1 && fileName[0] == '.' ||
                            fileName.Length == 2 && fileName[0] == '.' && fileName[1] == '.')
                        {
                            continue;
                        }

                        if (_includeHidden || (findData.dwFileAttributes & FileAttributes.Hidden) == 0)
                        {
                            if ((findData.dwFileAttributes & FileAttributes.Directory) != 0)
                            {
                                if (!TryCombinePath(path.AsSpan(), fileName, fullPathBuffer, out var fullPath))
                                {
                                    continue;
                                }
                                //Remove the null terminator
                                var toSend = fullPath[..^1].ToString();
                                //var toSend = fullPath.ToString();

                                if (!filesOnly)
                                {
                                    scanState.FileChannelWriter.TryWrite((toSend, string.Empty));
                                }

                                Interlocked.Increment(ref scanState.PendingDirectoryCount);
                                scanState.DirectoryChannelWriter.TryWrite((currentDepth + 1, toSend));
                            }
                            else
                            {
                                if (!directoriesOnly)
                                {
                                    scanState.FileChannelWriter.TryWrite((path, fileName.ToString()));
                                }
                            }
                        }
                    } while (FindNextFile(findHandle, out findData));
                }
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
        return Task.CompletedTask;
    }

    private static void CreateSearchPath(ReadOnlySpan<char> path, Span<char> searchPath)
    {
        path.CopyTo(searchPath);
        if (path[^1] == '\\')
        {
            searchPath[path.Length + 0] = '*';
            searchPath[path.Length + 1] = '\0';
            return;
        }
        searchPath[path.Length] = '\\';
        searchPath[path.Length + 1] = '*';
        searchPath[path.Length + 2] = '\0';
    }
    
    private static bool TryCombinePath(ReadOnlySpan<char> path, ReadOnlySpan<char> fileName, Span<char> buffer, out ReadOnlySpan<char> result)
    {
        if (path.Length + 1 + fileName.Length + 1 > buffer.Length)
        {
            result = default;
            return false;
        }

        path.CopyTo(buffer);
        if (path[^1] == '\\')
        {
            fileName.CopyTo(buffer[(path.Length)..]);
            buffer[path.Length + fileName.Length] = '\\';
            buffer[path.Length + 1 + fileName.Length] = '\0';
    
            result = buffer[..(path.Length + fileName.Length + 2)];
            return true;
        }
    
        buffer[path.Length] = '\\';
        fileName.CopyTo(buffer[(path.Length + 1)..]);
        buffer[path.Length + 1 + fileName.Length] = '\\';
        buffer[path.Length + 2 + fileName.Length] = '\0';
    
        result = buffer[..(path.Length + 1 + fileName.Length + 2)];
        return true;
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
