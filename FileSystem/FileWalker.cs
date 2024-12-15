using System.Buffers;
using System.Collections.Concurrent;
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
            
            Span<char> fullPathBuffer = stackalloc char[2048];
            var findData = new WIN32_FIND_DATA();
            var searchPath = fullPathBuffer.Slice(0, path[^1] == '\\' ? path.Length + 2 : path.Length + 3);
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

public class FileWalker5
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
    
    public FileWalker5(bool includeHidden = true)
    {
        _includeHidden = includeHidden;
    }

    private class ScanState(
        ChannelWriter<(int depth, string[])> directoryChannelWriter,
        ChannelWriter<(string[], string)> fileChannelWriter)
    {
        public int PendingDirectoryCount = 0;
        public int ChannelsCompleted = 0;

        public ChannelWriter<(int depth, string[] path)> DirectoryChannelWriter { get; set; } = directoryChannelWriter;
        public ChannelWriter<(string[], string)> FileChannelWriter { get; set; } = fileChannelWriter;
        public TaskCompletionSource<bool> CompletionSource { get; } = new();
    }

    public async Task StartScanForDirectoriesAsync(
        IEnumerable<string> initialDirectory,
        ChannelWriter<(string[], string)> cw,
        int maxDepth,
        bool directoriesOnly,
        bool filesOnly,
        CancellationToken cancellationToken)
    {
        var directoryChannel = Channel.CreateUnbounded<(int, string[])>(new UnboundedChannelOptions
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
                await cw.WriteAsync(([directory], string.Empty), cancellationToken);
            }
            await directoryChannel.Writer.WriteAsync((0, [directory]), cancellationToken);
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
        string[] path,
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
            
            Span<char> fullPathBuffer = stackalloc char[2048];
            var findData = new WIN32_FIND_DATA();
            //var searchPath = fullPathBuffer.Slice(0, path[^1] == '\\' ? path.Length + 2 : path.Length + 3);
            var searchPath = CreateSearchPath(path, fullPathBuffer);
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
                                var toSend = new string[path.Length + 1];
                                for (var i = 0; i < path.Length; i++)
                                {
                                    toSend[i] = path[i];
                                }
                                toSend[path.Length] = fileName.ToString();

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

    private static ReadOnlySpan<char> CreateSearchPath(string[] path, Span<char> searchPath)
    {
        int position = 0;
        foreach (var p in path)
        {
            if (position + p.Length + 1 > searchPath.Length) 
                throw new ArgumentException("The searchPath buffer isn't large enough.");

            var pSpan = p.AsSpan();
            pSpan.CopyTo(searchPath.Slice(position));
            position += pSpan.Length;

            if (p.Length > 0 && p[^1] != '\\')
            {
                searchPath[position] = '\\';
                position++;
            }
        }

        if (position + 2 > searchPath.Length)
            throw new ArgumentException("Not enough space for final characters.");

        searchPath[position] = '*';
        position++;
        searchPath[position] = '\0';
        position++;

        return searchPath.Slice(0, position);
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

public class FileWalker6
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
    
    public FileWalker6(bool includeHidden = true)
    {
        _includeHidden = includeHidden;
    }

    private class ScanState(
        ChannelWriter<(int depth, string[])> directoryChannelWriter,
        ChannelWriter<string[]> fileChannelWriter)
    {
        public int PendingDirectoryCount = 0;
        public int ChannelsCompleted = 0;

        public ChannelWriter<(int depth, string[] path)> DirectoryChannelWriter { get; set; } = directoryChannelWriter;
        public ChannelWriter<string[]> FileChannelWriter { get; set; } = fileChannelWriter;
        public TaskCompletionSource<bool> CompletionSource { get; } = new();
    }

    public async Task StartScanForDirectoriesAsync(
        IEnumerable<string> initialDirectory,
        ChannelWriter<string[]> cw,
        int maxDepth,
        bool directoriesOnly,
        bool filesOnly,
        CancellationToken cancellationToken)
    {
        var directoryChannel = Channel.CreateUnbounded<(int, string[])>(new UnboundedChannelOptions
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
                await cw.WriteAsync([directory], cancellationToken);
            }
            await directoryChannel.Writer.WriteAsync((0, [directory]), cancellationToken);
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
        string[] path,
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
            
            Span<char> fullPathBuffer = stackalloc char[2048];
            var findData = new WIN32_FIND_DATA();
            //var searchPath = fullPathBuffer.Slice(0, path[^1] == '\\' ? path.Length + 2 : path.Length + 3);
            var searchPath = CreateSearchPath(path, fullPathBuffer);
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
                                var toSend = new string[path.Length + 1];
                                for (var i = 0; i < path.Length; i++)
                                {
                                    toSend[i] = path[i];
                                }
                                toSend[path.Length] = fileName.ToString();

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
                                    var toSend = new string[path.Length + 1];
                                    for (var i = 0; i < path.Length; i++)
                                    {
                                        toSend[i] = path[i];
                                    }
                                    toSend[path.Length] = fileName.ToString();
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

    private static ReadOnlySpan<char> CreateSearchPath(string[] path, Span<char> searchPath)
    {
        int position = 0;
        foreach (var p in path)
        {
            if (position + p.Length + 1 > searchPath.Length) 
                throw new ArgumentException("The searchPath buffer isn't large enough.");

            var pSpan = p.AsSpan();
            pSpan.CopyTo(searchPath.Slice(position));
            position += pSpan.Length;

            if (p.Length > 0 && p[^1] != '\\')
            {
                searchPath[position] = '\\';
                position++;
            }
        }

        if (position + 2 > searchPath.Length)
            throw new ArgumentException("Not enough space for final characters.");

        searchPath[position] = '*';
        position++;
        searchPath[position] = '\0';
        position++;

        return searchPath.Slice(0, position);
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

public class FileWalker7
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
    private ArrayPool<string> _stringArrayPool;

    public FileWalker7(bool includeHidden = true)
    {
        _includeHidden = includeHidden;
    }

    private class ScanState(
        ChannelWriter<(int depth, Memory<string>)> directoryChannelWriter,
        ChannelWriter<Memory<string>> fileChannelWriter)
    {
        public int PendingDirectoryCount = 0;
        public int ChannelsCompleted = 0;

        public ChannelWriter<(int depth, Memory<string> path)> DirectoryChannelWriter { get; } = directoryChannelWriter;
        public ChannelWriter<Memory<string>> FileChannelWriter { get; } = fileChannelWriter;
        public TaskCompletionSource<bool> CompletionSource { get; } = new();
    }

    public async Task StartScanForDirectoriesAsync(
        IEnumerable<string> initialDirectory,
        ChannelWriter<Memory<string>> cw,
        int maxDepth,
        bool directoriesOnly,
        bool filesOnly,
        ArrayPool<string> stringArrayPool,
        CancellationToken cancellationToken)
    {
        _stringArrayPool = stringArrayPool;
        var directoryChannel = Channel.CreateUnbounded<(int, Memory<string>)>(new UnboundedChannelOptions
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
            string[] arr = _stringArrayPool.Rent(1);
            arr[0] = directory;
            var toSend = arr.AsMemory()[..1];
            if (!filesOnly)
            {
                await cw.WriteAsync(arr.AsMemory()[..1], cancellationToken);
            }
            await directoryChannel.Writer.WriteAsync((0, toSend), cancellationToken);
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
        Memory<string> path,
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
            
            Span<char> fullPathBuffer = stackalloc char[2048];
            var findData = new WIN32_FIND_DATA();
            //var searchPath = fullPathBuffer.Slice(0, path[^1] == '\\' ? path.Length + 2 : path.Length + 3);
            var searchPath = CreateSearchPath(path, fullPathBuffer);
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
                                var rented = _stringArrayPool.Rent(path.Length + 1);
                                var toSend = rented.AsMemory()[..(path.Length + 1)];
                                //var toSend = new string[path.Length + 1];
                                for (var i = 0; i < path.Length; i++)
                                {
                                    toSend.Span[i] = path.Span[i];
                                }
                                toSend.Span[path.Length] = fileName.ToString();

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
                                    var rented = _stringArrayPool.Rent(path.Length + 1);
                                    var toSend = rented.AsMemory()[..(path.Length + 1)];
                                    //var toSend = new string[path.Length + 1];
                                    for (var i = 0; i < path.Length; i++)
                                    {
                                        toSend.Span[i] = path.Span[i];
                                    }
                                    toSend.Span[path.Length] = fileName.ToString();
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

    private static ReadOnlySpan<char> CreateSearchPath(Memory<string> path, Span<char> searchPath)
    {
        int position = 0;
        //foreach (var p in path)
        for(var i = 0; i < path.Length; i++)
        {
            string p = path.Span[i];
            if (position + p.Length + 1 > searchPath.Length) 
                throw new ArgumentException("The searchPath buffer isn't large enough.");

            ReadOnlySpan<char> pSpan = p.AsSpan();
            pSpan.CopyTo(searchPath.Slice(position));
            position += pSpan.Length;

            if (p.Length > 0 && p[^1] != '\\')
            {
                searchPath[position] = '\\';
                position++;
            }
        }

        if (position + 2 > searchPath.Length)
            throw new ArgumentException("Not enough space for final characters.");

        searchPath[position] = '*';
        position++;
        searchPath[position] = '\0';
        position++;

        return searchPath.Slice(0, position);
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

public class FileSystemNode
{
    public readonly string Text;
    public readonly FileSystemNode? Previous;

    public FileSystemNode(string text, FileSystemNode? previous)
    {
        Text = text;
        Previous = previous;
    }

    public override string ToString()
    {
        Span<char> buf = stackalloc char[2048];
        return this.ToString(buf).ToString();
    }
}

public static class FileSystemNodeExtensions
{
    public static ReadOnlySpan<char> ToString(this FileSystemNode t, Span<char> buf)
    {
        var length = 0;
        var current = t;

        while (current != null)
        {
            length += current.Text.Length;
            if (current.Text.Length > 0 && current.Text[^1] != '\\')
            {
                length += 1;
            }
            current = current.Previous;
        }

        if (length > buf.Length)
            throw new ArgumentException("The searchPath buffer isn't large enough.");

        var position = length - 1;

        current = t;
        while (current != null)
        {
            string text = current.Text;

            if (text.Length > 0 && text[^1] != '\\')
            {
                buf[position] = '\\';
                position--;
            }

            position -= text.Length;
            text.AsSpan().CopyTo(buf.Slice(position + 1, text.Length));

            current = current.Previous;
        }

        return buf.Slice(0, length - 1);
    }
}

public static class HashCodeHelper
{
    public static int GetHashCode(ReadOnlySpan<char> span)
    {
        const int fnvPrime = 16777619;
        const int fnvOffset = unchecked((int)2166136261);

        int hash = fnvOffset;
        foreach (char c in span)
        {
            hash ^= c;
            hash *= fnvPrime;
        }
        return hash;
    }
}

public class SpanAwareStringComparer : IEqualityComparer<string>
{
    public static readonly SpanAwareStringComparer Instance = new();

    public bool Equals(string? x, string? y)
    {
        return string.Equals(x, y, StringComparison.Ordinal);
    }

    public int GetHashCode(string obj)
    {
        return obj.GetHashCode();
    }

    public int GetHashCode(ReadOnlySpan<char> span)
    {
        return HashCodeHelper.GetHashCode(span);
    }

    public bool Equals(string? x, ReadOnlySpan<char> span)
    {
        if (x is null)
            return false;

        return x.AsSpan().SequenceEqual(span);
    }
}

public class FileWalker8
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
    
    
    public class SpanAwareStringComparer : IEqualityComparer<string>
    {
        public static readonly SpanAwareStringComparer Instance = new();

        public bool Equals(string? x, string? y)
        {
            return string.Equals(x, y, StringComparison.Ordinal);
        }

        public int GetHashCode(string obj)
        {
            return obj.GetHashCode();
        }

        public bool Equals(string? x, ReadOnlySpan<char> y)
        {
            if (x is null)
                return false;

            return x.AsSpan().SequenceEqual(y);
        }

        public int GetHashCode(ReadOnlySpan<char> span)
        {
            // Compute hash code for the span (simple implementation)
            unchecked
            {
                int hash = 17;
                foreach (var ch in span)
                {
                    hash = hash * 31 + ch;
                }
                return hash;
            }
        }
    }

    private class SafeFindHandle : SafeHandleZeroOrMinusOneIsInvalid
    {
        public SafeFindHandle() : base(true) { }

        protected override bool ReleaseHandle()
        {
            return FindClose(handle);
        }
    }

    private readonly bool _includeHidden;
    private readonly ConcurrentDictionary<int, string> _localInternPool = new();

    public string InternLocally(ReadOnlySpan<char> input)
    {
    // Compute the hash code for the span
    int hashCode = HashCodeHelper.GetHashCode(input);

    // Lookup using the hash code
    if (_localInternPool.TryGetValue(hashCode, out var existing))
    {
        // Verify equality to handle hash collisions
        if (existing.AsSpan().SequenceEqual(input))
            return existing;
    }

    // Allocate and add to the dictionary
    string inputString = input.ToString();
    _localInternPool[hashCode] = inputString;
    return inputString;
    }

    public FileWalker8(bool includeHidden = true)
    {
        _includeHidden = includeHidden;
    }

    private class ScanState(
        ChannelWriter<(int depth, FileSystemNode)> directoryChannelWriter,
        ChannelWriter<FileSystemNode> fileChannelWriter)
    {
        public int PendingDirectoryCount = 0;
        public int ChannelsCompleted = 0;

        public ChannelWriter<(int depth, FileSystemNode path)> DirectoryChannelWriter { get; } = directoryChannelWriter;
        public ChannelWriter<FileSystemNode> FileChannelWriter { get; } = fileChannelWriter;
        public TaskCompletionSource<bool> CompletionSource { get; } = new();
    }

    public async Task StartScanForDirectoriesAsync(
        IEnumerable<string> initialDirectory,
        ChannelWriter<FileSystemNode> cw,
        int maxDepth,
        bool directoriesOnly,
        bool filesOnly,
        CancellationToken cancellationToken)
    {
        var directoryChannel = Channel.CreateUnbounded<(int, FileSystemNode)>(new UnboundedChannelOptions
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
            var fileSystemNode = new FileSystemNode(directory,null);
            if (!filesOnly)
            {
                await cw.WriteAsync(fileSystemNode, cancellationToken);
            }
            await directoryChannel.Writer.WriteAsync((0, fileSystemNode), cancellationToken);
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
        FileSystemNode path,
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
            
            Span<char> fullPathBuffer = stackalloc char[2048];
            var findData = new WIN32_FIND_DATA();
            //var searchPath = fullPathBuffer.Slice(0, path[^1] == '\\' ? path.Length + 2 : path.Length + 3);
            var searchPath = CreateSearchPath(path, fullPathBuffer);
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
                                var fName = InternLocally(fileName);
                                var newNode = new FileSystemNode(fName, path);

                                if (!filesOnly)
                                {
                                    scanState.FileChannelWriter.TryWrite(newNode);
                                }

                                Interlocked.Increment(ref scanState.PendingDirectoryCount);
                                scanState.DirectoryChannelWriter.TryWrite((currentDepth + 1, newNode));
                            }
                            else
                            {
                                if (!directoriesOnly)
                                {
                                    var fName = InternLocally(fileName);
                                    var newNode = new FileSystemNode(fName, path);
                                    scanState.FileChannelWriter.TryWrite(newNode);
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

    private static ReadOnlySpan<char> CreateSearchPath(FileSystemNode path, Span<char> searchPath)
    {
        var length = 0;
        var current = path;

        while (current != null)
        {
            length += current.Text.Length;
            if (current.Text.Length > 0 && current.Text[^1] != '\\')
            {
                length += 1;
            }
            current = current.Previous;
        }

        length += 2;

        if (length > searchPath.Length)
            throw new ArgumentException("The searchPath buffer isn't large enough.");

        var position = length - 1;
        searchPath[position] = '\0';
        position--;
        searchPath[position] = '*';
        position--;

        current = path;
        while (current != null)
        {
            string text = current.Text;

            if (text.Length > 0 && text[^1] != '\\')
            {
                searchPath[position] = '\\';
                position--;
            }

            position -= text.Length;
            text.AsSpan().CopyTo(searchPath.Slice(position + 1, text.Length));

            current = current.Previous;
        }

        return searchPath.Slice(0, length - 1);
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

public class FileWalker9
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
    
    
    public class SpanAwareStringComparer : IEqualityComparer<string>
    {
        public static readonly SpanAwareStringComparer Instance = new();

        public bool Equals(string? x, string? y)
        {
            return string.Equals(x, y, StringComparison.Ordinal);
        }

        public int GetHashCode(string obj)
        {
            return obj.GetHashCode();
        }

        public bool Equals(string? x, ReadOnlySpan<char> y)
        {
            if (x is null)
                return false;

            return x.AsSpan().SequenceEqual(y);
        }

        public int GetHashCode(ReadOnlySpan<char> span)
        {
            // Compute hash code for the span (simple implementation)
            unchecked
            {
                int hash = 17;
                foreach (var ch in span)
                {
                    hash = hash * 31 + ch;
                }
                return hash;
            }
        }
    }

    private class SafeFindHandle : SafeHandleZeroOrMinusOneIsInvalid
    {
        public SafeFindHandle() : base(true) { }

        protected override bool ReleaseHandle()
        {
            return FindClose(handle);
        }
    }

    private readonly bool _includeHidden;
    private readonly ConcurrentDictionary<int, string> _localInternPool = new();

    public string InternLocally(ReadOnlySpan<char> input)
    {
    // Compute the hash code for the span
    int hashCode = HashCodeHelper.GetHashCode(input);

    // Lookup using the hash code
    if (_localInternPool.TryGetValue(hashCode, out var existing))
    {
        // Verify equality to handle hash collisions
        if (existing.AsSpan().SequenceEqual(input))
            return existing;
    }

    // Allocate and add to the dictionary
    string inputString = input.ToString();
    _localInternPool[hashCode] = inputString;
    return inputString;
    }

    public FileWalker9(bool includeHidden = true)
    {
        _includeHidden = includeHidden;
    }

    private class ScanState(
        ChannelWriter<(int depth, FileSystemNode)> directoryChannelWriter,
        ChannelWriter<FileSystemNode> fileChannelWriter)
    {
        public int PendingDirectoryCount = 0;
        public int ChannelsCompleted = 0;

        public ChannelWriter<(int depth, FileSystemNode path)> DirectoryChannelWriter { get; } = directoryChannelWriter;
        public ChannelWriter<FileSystemNode> FileChannelWriter { get; } = fileChannelWriter;
        public TaskCompletionSource<bool> CompletionSource { get; } = new();
    }

    public async Task StartScanForDirectoriesAsync(
        IEnumerable<string> initialDirectory,
        ChannelWriter<FileSystemNode> cw,
        int maxDepth,
        bool directoriesOnly,
        bool filesOnly,
        CancellationToken cancellationToken)
    {
        var directoryChannel = Channel.CreateUnbounded<(int, FileSystemNode)>(new UnboundedChannelOptions
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
            var fileSystemNode = new FileSystemNode(directory,null);
            if (!filesOnly)
            {
                await cw.WriteAsync(fileSystemNode, cancellationToken);
            }
            await directoryChannel.Writer.WriteAsync((0, fileSystemNode), cancellationToken);
        }

        try
        {
            await Parallel.ForEachAsync(directoryChannel.Reader.ReadAllAsync(cancellationToken), new ParallelOptions
                {
                    MaxDegreeOfParallelism = Environment.ProcessorCount
                }, (item, ct) =>
                {
                    ScanDirectoryForDirectoriesAsync(item.Item2,
                        item.Item1,
                        maxDepth,
                        scanState,
                        directoriesOnly,
                        filesOnly,
                        cancellationToken);
                    return ValueTask.CompletedTask;
                });
        }
        catch (TaskCanceledException)
        {
        }

        await scanState.CompletionSource.Task;
    }
    
    private unsafe void ScanDirectoryForDirectoriesAsync(
        FileSystemNode path,
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
            return;
        }

        try
        {
            if (cancellationToken.IsCancellationRequested)
            {
                if (Interlocked.Decrement(ref scanState.PendingDirectoryCount) == 0)
                {
                    CompleteScan(scanState);
                }

                return;
            }
            
            Span<char> fullPathBuffer = stackalloc char[2048];
            var findData = new WIN32_FIND_DATA();
            //var searchPath = fullPathBuffer.Slice(0, path[^1] == '\\' ? path.Length + 2 : path.Length + 3);
            var searchPath = CreateSearchPath(path, fullPathBuffer);
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

                            return;
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
                                var fName = InternLocally(fileName);
                                var newNode = new FileSystemNode(fName, path);

                                if (!filesOnly)
                                {
                                    scanState.FileChannelWriter.TryWrite(newNode);
                                }

                                Interlocked.Increment(ref scanState.PendingDirectoryCount);
                                scanState.DirectoryChannelWriter.TryWrite((currentDepth + 1, newNode));
                            }
                            else
                            {
                                if (!directoriesOnly)
                                {
                                    var fName = InternLocally(fileName);
                                    var newNode = new FileSystemNode(fName, path);
                                    scanState.FileChannelWriter.TryWrite(newNode);
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
        return;
    }

    private static ReadOnlySpan<char> CreateSearchPath(FileSystemNode path, Span<char> searchPath)
    {
        var length = 0;
        var current = path;

        while (current != null)
        {
            length += current.Text.Length;
            if (current.Text.Length > 0 && current.Text[^1] != '\\')
            {
                length += 1;
            }
            current = current.Previous;
        }

        length += 2;

        if (length > searchPath.Length)
            throw new ArgumentException("The searchPath buffer isn't large enough.");

        var position = length - 1;
        searchPath[position] = '\0';
        position--;
        searchPath[position] = '*';
        position--;

        current = path;
        while (current != null)
        {
            string text = current.Text;

            if (text.Length > 0 && text[^1] != '\\')
            {
                searchPath[position] = '\\';
                position--;
            }

            position -= text.Length;
            text.AsSpan().CopyTo(searchPath.Slice(position + 1, text.Length));

            current = current.Previous;
        }

        return searchPath.Slice(0, length - 1);
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
public class FileWalker10
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
    
    
    public class SpanAwareStringComparer : IEqualityComparer<string>
    {
        public static readonly SpanAwareStringComparer Instance = new();

        public bool Equals(string? x, string? y)
        {
            return string.Equals(x, y, StringComparison.Ordinal);
        }

        public int GetHashCode(string obj)
        {
            return obj.GetHashCode();
        }

        public bool Equals(string? x, ReadOnlySpan<char> y)
        {
            if (x is null)
                return false;

            return x.AsSpan().SequenceEqual(y);
        }

        public int GetHashCode(ReadOnlySpan<char> span)
        {
            // Compute hash code for the span (simple implementation)
            unchecked
            {
                int hash = 17;
                foreach (var ch in span)
                {
                    hash = hash * 31 + ch;
                }
                return hash;
            }
        }
    }

    private class SafeFindHandle : SafeHandleZeroOrMinusOneIsInvalid
    {
        public SafeFindHandle() : base(true) { }

        protected override bool ReleaseHandle()
        {
            return FindClose(handle);
        }
    }

    private readonly bool _includeHidden;
    private readonly ConcurrentDictionary<int, string> _localInternPool = new();

    public string InternLocally(ReadOnlySpan<char> input)
    {
    // Compute the hash code for the span
    int hashCode = HashCodeHelper.GetHashCode(input);

    // Lookup using the hash code
    if (_localInternPool.TryGetValue(hashCode, out var existing))
    {
        // Verify equality to handle hash collisions
        if (existing.AsSpan().SequenceEqual(input))
            return existing;
    }

    // Allocate and add to the dictionary
    string inputString = input.ToString();
    _localInternPool[hashCode] = inputString;
    return inputString;
    }

    public FileWalker10(bool includeHidden = true)
    {
        _includeHidden = includeHidden;
    }

    private class ScanState(
        ChannelWriter<(int depth, FileSystemNode)> directoryChannelWriter,
        ChannelWriter<object> fileChannelWriter)
    {
        public int PendingDirectoryCount = 0;
        public int ChannelsCompleted = 0;

        public ChannelWriter<(int depth, FileSystemNode path)> DirectoryChannelWriter { get; } = directoryChannelWriter;
        public ChannelWriter<object> FileChannelWriter { get; } = fileChannelWriter;
        public TaskCompletionSource<bool> CompletionSource { get; } = new();
    }

    public async Task StartScanForDirectoriesAsync(
        IEnumerable<string> initialDirectory,
        ChannelWriter<object> cw,
        int maxDepth,
        bool directoriesOnly,
        bool filesOnly,
        CancellationToken cancellationToken)
    {
        var directoryChannel = Channel.CreateUnbounded<(int, FileSystemNode)>(new UnboundedChannelOptions
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
            var fileSystemNode = new FileSystemNode(directory,null);
            if (!filesOnly)
            {
                await cw.WriteAsync(fileSystemNode, cancellationToken);
            }
            await directoryChannel.Writer.WriteAsync((0, fileSystemNode), cancellationToken);
        }

        try
        {
            await Parallel.ForEachAsync(directoryChannel.Reader.ReadAllAsync(cancellationToken), new ParallelOptions
                {
                    MaxDegreeOfParallelism = Environment.ProcessorCount
                }, (item, ct) =>
                {
                    ScanDirectoryForDirectoriesAsync(item.Item2,
                        item.Item1,
                        maxDepth,
                        scanState,
                        directoriesOnly,
                        filesOnly,
                        cancellationToken);
                    return ValueTask.CompletedTask;
                });
        }
        catch (TaskCanceledException)
        {
        }

        await scanState.CompletionSource.Task;
    }
    
    private unsafe void ScanDirectoryForDirectoriesAsync(
        FileSystemNode path,
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
            return;
        }

        try
        {
            if (cancellationToken.IsCancellationRequested)
            {
                if (Interlocked.Decrement(ref scanState.PendingDirectoryCount) == 0)
                {
                    CompleteScan(scanState);
                }

                return;
            }
            
            Span<char> fullPathBuffer = stackalloc char[2048];
            var findData = new WIN32_FIND_DATA();
            //var searchPath = fullPathBuffer.Slice(0, path[^1] == '\\' ? path.Length + 2 : path.Length + 3);
            var searchPath = CreateSearchPath(path, fullPathBuffer);
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

                            return;
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
                                var fName = InternLocally(fileName);
                                var newNode = new FileSystemNode(fName, path);

                                if (!filesOnly)
                                {
                                    scanState.FileChannelWriter.TryWrite(newNode);
                                }

                                Interlocked.Increment(ref scanState.PendingDirectoryCount);
                                scanState.DirectoryChannelWriter.TryWrite((currentDepth + 1, newNode));
                            }
                            else
                            {
                                if (!directoriesOnly)
                                {
                                    var fName = InternLocally(fileName);
                                    var newNode = new FileSystemNode(fName, path);
                                    scanState.FileChannelWriter.TryWrite(newNode);
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
        return;
    }

    private static ReadOnlySpan<char> CreateSearchPath(FileSystemNode path, Span<char> searchPath)
    {
        var length = 0;
        var current = path;

        while (current != null)
        {
            length += current.Text.Length;
            if (current.Text.Length > 0 && current.Text[^1] != '\\')
            {
                length += 1;
            }
            current = current.Previous;
        }

        length += 2;

        if (length > searchPath.Length)
            throw new ArgumentException("The searchPath buffer isn't large enough.");

        var position = length - 1;
        searchPath[position] = '\0';
        position--;
        searchPath[position] = '*';
        position--;

        current = path;
        while (current != null)
        {
            string text = current.Text;

            if (text.Length > 0 && text[^1] != '\\')
            {
                searchPath[position] = '\\';
                position--;
            }

            position -= text.Length;
            text.AsSpan().CopyTo(searchPath.Slice(position + 1, text.Length));

            current = current.Previous;
        }

        return searchPath.Slice(0, length - 1);
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

public class FileWalker4
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
    
    public FileWalker4(bool includeHidden = true)
    {
        _includeHidden = includeHidden;
    }

    private class ScanState(
        ChannelWriter<(int depth, string path, string path2, string path3)> directoryChannelWriter,
        ChannelWriter<(string, string, string)> fileChannelWriter)
    {
        public int PendingDirectoryCount = 0;
        public int ChannelsCompleted = 0;

        public ChannelWriter<(int depth, string path, string path2, string path3)> DirectoryChannelWriter { get; set; } = directoryChannelWriter;
        public ChannelWriter<(string, string, string)> FileChannelWriter { get; set; } = fileChannelWriter;
        public TaskCompletionSource<bool> CompletionSource { get; } = new();
    }

    public async Task StartScanForDirectoriesAsync(
        IEnumerable<string> initialDirectory,
        ChannelWriter<(string, string, string)> cw,
        int maxDepth,
        bool directoriesOnly,
        bool filesOnly,
        CancellationToken cancellationToken)
    {
        var directoryChannel = Channel.CreateUnbounded<(int, string, string, string)>(new UnboundedChannelOptions
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
                await cw.WriteAsync((directory, string.Empty, string.Empty), cancellationToken);
            }
            await directoryChannel.Writer.WriteAsync((0, directory, string.Empty, string.Empty), cancellationToken);
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
                        item.Item3,
                        item.Item4,
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
        string path2,
        string path3,
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
            
            Span<char> fullPathBuffer = stackalloc char[2048];
            var findData = new WIN32_FIND_DATA();
            Span<char> searchPath = null;
            if (currentDepth == 0)
            {
                var p1 = path[^1] == '\\' ? 2 : 3;
                searchPath = fullPathBuffer.Slice(0, path.Length + p1);
            }
            else if (currentDepth == 1)
            {
                var p1 = path[^1] == '\\' ? 0 : 1;
                var p2 = path2[^1] == '\\' ? 2 : 3;
                searchPath = fullPathBuffer.Slice(0, path.Length + path2.Length + p1 + p2);
            }
            else
            {
                var p1 = path[^1] == '\\' ? 0 : 1;
                var p2 = path2[^1] == '\\' ? 0 : 1;
                var p3 = path3[^1] == '\\' ? 2 : 3;
                searchPath = fullPathBuffer.Slice(0, path.Length + path2.Length + path3.Length + p1 + p2 + p3);
            }
            CreateSearchPath(path, path2, path3, searchPath);
            //Console.WriteLine("***search string: {0}", searchPath.ToString());
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
                            var allocatedFileName = fileName.ToString();
                            if ((findData.dwFileAttributes & FileAttributes.Directory) != 0)
                            {
                                //Remove the null terminator
                                //var toSend = fullPath.ToString();

                                //if (!filesOnly)
                                //{
                                //    switch (currentDepth)
                                //    {
                                //        case 0:
                                //            scanState.FileChannelWriter.TryWrite((path, allocatedFileName, string.Empty));
                                //            break;
                                //        case 1:
                                //            scanState.FileChannelWriter.TryWrite((path, path2, allocatedFileName));
                                //            break;
                                //        default:
                                //            scanState.FileChannelWriter.TryWrite((path2, path2, allocatedFileName));
                                //            break;
                                //    }
                                //}
                                Interlocked.Increment(ref scanState.PendingDirectoryCount);
                                switch (currentDepth)
                                {
                                    case 0:
                                        scanState.DirectoryChannelWriter.TryWrite((currentDepth + 1, path, allocatedFileName, string.Empty));
                                        scanState.FileChannelWriter.TryWrite((path, allocatedFileName, string.Empty));
                                        break;
                                    case 1:
                                        //if (!TryCombinePath(path.AsSpan(), path2, fullPathBuffer, out var fullPath2))
                                        //{
                                        //    continue;
                                        //}
                                        //var toSend2 = fullPath2[..^1].ToString();
                                        scanState.DirectoryChannelWriter.TryWrite((currentDepth + 1, path, path2, allocatedFileName));
                                        scanState.FileChannelWriter.TryWrite((path, path2, allocatedFileName));
                                        break;
                                    default:
                                        if (!TryCombinePath(path.AsSpan(), path2, fullPathBuffer, out var fullPath2))
                                        {
                                            continue;
                                        }
                                        var toSend2 = fullPath2[..^1].ToString();
                                        scanState.DirectoryChannelWriter.TryWrite((currentDepth + 1, toSend2, path3, allocatedFileName));
                                        scanState.FileChannelWriter.TryWrite((toSend2, path3, allocatedFileName));
                                        break;
                                }

                                //scanState.DirectoryChannelWriter.TryWrite((currentDepth + 1, toSend));
                            }
                            else
                            {
                                if (!directoriesOnly)
                                {
                                    if (!TryCombinePath(path.AsSpan(), path2, fullPathBuffer, out var fullPath2))
                                    {
                                        continue;
                                    }
                                    var toSend2 = fullPath2[..^1].ToString();
                                    scanState.FileChannelWriter.TryWrite((toSend2, path3, allocatedFileName));
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

    private static void CreateSearchPath(ReadOnlySpan<char> path, ReadOnlySpan<char> path2, ReadOnlySpan<char> path3, Span<char> searchPath)
    {
        path.CopyTo(searchPath);
        var numberOfSlashesAdded = 0;
        if (path[^1] != '\\')
        {
            searchPath[path.Length] = '\\';
            numberOfSlashesAdded++;
        }
        path2.CopyTo(searchPath[(path.Length + numberOfSlashesAdded)..]);
        if (path2.Length > 0 && path2[^1] != '\\')
        {
            searchPath[path.Length + numberOfSlashesAdded + path2.Length] = '\\';
            numberOfSlashesAdded++;
        }
        path3.CopyTo(searchPath[(path.Length + path2.Length + numberOfSlashesAdded)..]);
        if (path3.Length > 0 && path3[^1] != '\\')
        {
            searchPath[path.Length + path2.Length + + path3.Length + numberOfSlashesAdded] = '\\';
            numberOfSlashesAdded++;
        }
        searchPath[path.Length + path2.Length + path3.Length + numberOfSlashesAdded] = '*';
        searchPath[path.Length + path2.Length + path3.Length + numberOfSlashesAdded + 1] = '\0';
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

public class FileWalker3
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
    
    public FileWalker3(bool includeHidden = true)
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
            
            Span<char> fullPathBuffer = stackalloc char[2048];
            Span<char> searchPathBuffer = stackalloc char[2048];
            Span<char> tokenizedDirBuf = stackalloc char[2048];
            var findData = new WIN32_FIND_DATA();
            var unTokenizedSearchPath = SpecialDirectoryTokenizer.Untokenize(path, searchPathBuffer);
            var searchPath = fullPathBuffer.Slice(0, unTokenizedSearchPath[^1] == '\\' ? unTokenizedSearchPath.Length + 2 : unTokenizedSearchPath.Length + 3);
            CreateSearchPath(unTokenizedSearchPath, searchPath);
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
                                var toSend = SpecialDirectoryTokenizer.Tokenize(fullPath[..^1], tokenizedDirBuf).ToString();
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
public static class SpecialDirectoryTokenizer
{
    // Define known tokens and their replacements, sorted by prefix length.
    private static readonly (string Prefix, string Token)[] KnownDirectories;

    static SpecialDirectoryTokenizer()
    {
        KnownDirectories = new[]
        {
            // Program Files
            ("C:\\Program Files\\Common Files\\", "$CMMF$"),
            ("C:\\Program Files (x86)\\Common Files\\", "$CMMFx$"),
            ("C:\\Program Files\\", "$PF$"),
            ("C:\\Program Files (x86)\\", "$PFx$"),

            // User Folders
            (Environment.GetFolderPath(Environment.SpecialFolder.UserProfile) + "\\", "$UP$"),
            (Environment.GetFolderPath(Environment.SpecialFolder.Desktop) + "\\", "$DT$"),
            (Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments) + "\\", "$DOC$"),
            (Environment.GetFolderPath(Environment.SpecialFolder.MyMusic) + "\\", "$MUS$"),
            (Environment.GetFolderPath(Environment.SpecialFolder.MyPictures) + "\\", "$PIC$"),
            (Environment.GetFolderPath(Environment.SpecialFolder.MyVideos) + "\\", "$VID$"),
            (Environment.GetFolderPath(Environment.SpecialFolder.Favorites) + "\\", "$FAV$"),
            (Environment.GetFolderPath(Environment.SpecialFolder.Templates) + "\\", "$TMP$"),
            (Environment.GetFolderPath(Environment.SpecialFolder.StartMenu) + "\\", "$SM$"),
            (Environment.GetFolderPath(Environment.SpecialFolder.Programs) + "\\", "$PRG$"),
            (Environment.GetFolderPath(Environment.SpecialFolder.Startup) + "\\", "$STP$"),

            // Application Data
            (Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData) + "\\", "$AD$"),
            (Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData) + "\\", "$LAD$"),
            (Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData) + "\\", "$CAD$"),

            // Windows System Folders
            (Environment.GetFolderPath(Environment.SpecialFolder.System) + "\\", "$SYS$"),
            (Environment.GetFolderPath(Environment.SpecialFolder.SystemX86) + "\\", "$SYSx$"),
            (Environment.GetFolderPath(Environment.SpecialFolder.Windows) + "\\", "$WIN$"),

            // Public Folders
            (Environment.GetFolderPath(Environment.SpecialFolder.CommonDesktopDirectory) + "\\", "$CDT$"),
            (Environment.GetFolderPath(Environment.SpecialFolder.CommonDocuments) + "\\", "$CDOC$"),
            (Environment.GetFolderPath(Environment.SpecialFolder.CommonMusic) + "\\", "$CMUS$"),
            (Environment.GetFolderPath(Environment.SpecialFolder.CommonPictures) + "\\", "$CPIC$"),
            (Environment.GetFolderPath(Environment.SpecialFolder.CommonVideos) + "\\", "$CVID$"),
            (Environment.GetFolderPath(Environment.SpecialFolder.CommonStartMenu) + "\\", "$CSM$"),
            (Environment.GetFolderPath(Environment.SpecialFolder.CommonPrograms) + "\\", "$CPRG$"),
            (Environment.GetFolderPath(Environment.SpecialFolder.CommonStartup) + "\\", "$CSTP$"),

            // Internet Related
            (Environment.GetFolderPath(Environment.SpecialFolder.Cookies) + "\\", "$CKS$"),
            (Environment.GetFolderPath(Environment.SpecialFolder.History) + "\\", "$HST$"),
            (Environment.GetFolderPath(Environment.SpecialFolder.InternetCache) + "\\", "$IC$"),

            // Fonts
            (Environment.GetFolderPath(Environment.SpecialFolder.Fonts) + "\\", "$FNT$")
        }
        .OrderByDescending(d => d.Item1.Length)
        .ToArray(); // Sort by prefix length (longest first).
    }

    // Tokenize a path
    public static ReadOnlySpan<char> Tokenize(ReadOnlySpan<char> input, Span<char> output)
    {
        foreach (var (prefix, token) in KnownDirectories)
        {
            if (input.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            {
                // Write the token
                token.AsSpan().CopyTo(output);
                var tokenLength = token.Length;

                // Copy the remainder of the path after the prefix
                var remainder = input.Slice(prefix.Length);
                remainder.CopyTo(output.Slice(tokenLength));
                return output[..(tokenLength + remainder.Length)];
            }
        }

        // No special directory matched, copy as-is
        input.CopyTo(output);
        return output[..input.Length];
    }

    // Untokenize a path
    public static ReadOnlySpan<char> Untokenize(ReadOnlySpan<char> input, Span<char> output)
    {
        foreach (var (prefix, token) in KnownDirectories)
        {
            if (input.StartsWith(token, StringComparison.OrdinalIgnoreCase))
            {
                // Write the prefix
                prefix.AsSpan().CopyTo(output);
                var prefixLength = prefix.Length;

                // Copy the remainder of the path after the token
                var remainder = input.Slice(token.Length);
                remainder.CopyTo(output.Slice(prefixLength));
                return output[0..(prefixLength + remainder.Length)];
            }
        }

        // No token matched, copy as-is
        input.CopyTo(output);
        return output[0..(input.Length)];
    }
}
