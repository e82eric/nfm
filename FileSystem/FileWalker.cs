using System.Buffers;
using System.Collections.Concurrent;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.ComTypes;
using System.Threading.Channels;
using Microsoft.Win32.SafeHandles;

namespace nfzf.FileSystem;

public class FileSystemNode
{
    public string Text;
    public FileSystemNode? Previous;

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
    public static void UpdateTextSlow(this FileSystemNode node, FileInfo info)
    {
        node.Text = info.Name;
        var currentDirectory = info.Directory;
        var currentNode = node;

        while (currentDirectory != null)
        {
            var previousNode = new FileSystemNode(currentDirectory.Name, null);
            currentNode.Previous = previousNode;
            currentNode = previousNode;
            currentDirectory = currentDirectory.Parent;
        }
    }
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

    public FileWalker(bool includeHidden = true)
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