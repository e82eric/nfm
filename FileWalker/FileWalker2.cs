using System.Collections.Concurrent;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.ComTypes;
using System.Text;
using System.Threading.Channels;
using Microsoft.Win32.SafeHandles;

namespace nfzf.FileSystem2
{
    public class FileSystemNode
    {
        public byte[] Text;
        public FileSystemNode? Previous;

        public FileSystemNode(byte[] text, FileSystemNode? previous)
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
            node.Text = Encoding.UTF8.GetBytes(info.Name);
            var currentDirectory = info.Directory;
            var currentNode = node;

            while (currentDirectory != null)
            {
                var previousNode = new FileSystemNode(Encoding.UTF8.GetBytes(currentDirectory.Name), null);
                currentNode.Previous = previousNode;
                currentNode = previousNode;
                currentDirectory = currentDirectory.Parent;
            }
        }

        public static ReadOnlySpan<char> ToString(this FileSystemNode t, Span<char> buf)
        {
            int totalCharCount = 0;
            var current = t;
            // First pass: compute the total character count after decoding.
            while (current != null)
            {
                int charCount = Encoding.UTF8.GetCharCount(current.Text);
                totalCharCount += charCount;
                // Decode to check whether a trailing '\' is present.
                Span<char> temp = charCount <= 256 ? stackalloc char[charCount] : new char[charCount];
                Encoding.UTF8.GetChars(current.Text, temp);
                if (charCount > 0 && temp[charCount - 1] != '\\')
                {
                    totalCharCount += 1;
                }
                current = current.Previous;
            }

            if (totalCharCount > buf.Length)
                throw new ArgumentException("The searchPath buffer isn't large enough.");

            int position = totalCharCount - 1;
            current = t;
            // Second pass: decode and copy each node’s text into the provided buffer.
            while (current != null)
            {
                int charCount = Encoding.UTF8.GetCharCount(current.Text);
                Span<char> nodeChars = charCount <= 256 ? stackalloc char[charCount] : new char[charCount];
                Encoding.UTF8.GetChars(current.Text, nodeChars);
                bool addBackslash = charCount > 0 && nodeChars[charCount - 1] != '\\';
                if (addBackslash)
                {
                    buf[position] = '\\';
                    position--;
                }
                position -= charCount;
                nodeChars.CopyTo(buf.Slice(position + 1, charCount));
                current = current.Previous;
            }
            return buf.Slice(0, totalCharCount);
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

        public static int GetHashCode(ReadOnlySpan<byte> span)
        {
            const int fnvPrime = 16777619;
            const int fnvOffset = unchecked((int)2166136261);

            int hash = fnvOffset;
            foreach (byte b in span)
            {
                hash ^= b;
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
        private readonly ConcurrentDictionary<int, byte[]> _localInternPool = new();

        private byte[] InternLocally(ReadOnlySpan<char> input)
        {
            // Determine required byte count
            int byteCount = Encoding.UTF8.GetByteCount(input);

            // Use stackalloc for small sizes, heap allocation for large ones
            Span<byte> utf8Span = byteCount <= 256 ? stackalloc byte[byteCount] : new byte[byteCount];

            // Encode UTF-8 bytes into the span
            int bytesWritten = Encoding.UTF8.GetBytes(input, utf8Span);

            // Compute hash for the UTF-8 encoded bytes
            int hash = HashCodeHelper.GetHashCode(utf8Span);

            // Try to find an existing entry
            if (_localInternPool.TryGetValue(hash, out var existing))
            {
                if (existing.AsSpan().SequenceEqual(utf8Span))
                    return existing; // Return the interned instance
            }

            // If not found, allocate a new byte array and store it in the dictionary
            byte[] utf8 = utf8Span.ToArray();
            _localInternPool[hash] = utf8;

            return utf8;
        }

        public FileWalker(bool includeHidden = true)
        {
            _includeHidden = includeHidden;
        }

        private class ScanState
        {
            public int PendingDirectoryCount = 0;
            public int ChannelsCompleted = 0;
            public ChannelWriter<(int depth, FileSystemNode path)> DirectoryChannelWriter { get; }
            public ChannelWriter<object> FileChannelWriter { get; }
            public TaskCompletionSource<bool> CompletionSource { get; } = new();

            public ScanState(ChannelWriter<(int depth, FileSystemNode path)> directoryChannelWriter,
                             ChannelWriter<object> fileChannelWriter)
            {
                DirectoryChannelWriter = directoryChannelWriter;
                FileChannelWriter = fileChannelWriter;
            }
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

            foreach (var directory in initialDirectory)
            {
                Interlocked.Increment(ref scanState.PendingDirectoryCount);
            }

            foreach (var directory in initialDirectory)
            {
                // Store the initial directory name as UTF8.
                var fileSystemNode = new FileSystemNode(Encoding.UTF8.GetBytes(directory), null);
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
                            if ((fileName.Length == 1 && fileName[0] == '.') ||
                                (fileName.Length == 2 && fileName[0] == '.' && fileName[1] == '.'))
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
            catch (Exception)
            {
            }
            finally
            {
                if (Interlocked.Decrement(ref scanState.PendingDirectoryCount) == 0)
                {
                    CompleteScan(scanState);
                }
            }
        }

        private static ReadOnlySpan<char> CreateSearchPath(FileSystemNode path, Span<char> searchPath)
        {
            int totalCharCount = 0;
            var current = path;
            while (current != null)
            {
                int charCount = Encoding.UTF8.GetCharCount(current.Text);
                totalCharCount += charCount;
                Span<char> temp = charCount <= 256 ? stackalloc char[charCount] : new char[charCount];
                Encoding.UTF8.GetChars(current.Text, temp);
                if (charCount > 0 && temp[charCount - 1] != '\\')
                {
                    totalCharCount += 1;
                }
                current = current.Previous;
            }

            totalCharCount += 2;
            if (totalCharCount > searchPath.Length)
                throw new ArgumentException("The searchPath buffer isn't large enough.");

            int position = totalCharCount - 1;
            searchPath[position] = '\0';
            position--;
            searchPath[position] = '*';
            position--;

            current = path;
            while (current != null)
            {
                int charCount = Encoding.UTF8.GetCharCount(current.Text);
                Span<char> nodeChars = charCount <= 256 ? stackalloc char[charCount] : new char[charCount];
                Encoding.UTF8.GetChars(current.Text, nodeChars);
                bool addBackslash = charCount > 0 && nodeChars[charCount - 1] != '\\';
                if (addBackslash)
                {
                    searchPath[position] = '\\';
                    position--;
                }
                position -= charCount;
                nodeChars.CopyTo(searchPath.Slice(position + 1, charCount));
                current = current.Previous;
            }

            return searchPath.Slice(0, totalCharCount - 1);
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
}
