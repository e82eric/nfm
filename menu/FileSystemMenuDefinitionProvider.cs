using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using Avalonia.Input;
using nfzf;
using nfzf.FileSystem;

namespace nfm.menu;

public class FileSystemMenuDefinitionProvider(
    IResultHandler<FileSystemNode> resultHandler,
    int maxDepth,
    IEnumerable<string>? rootDirectory,
    bool quitOnEscape,
    bool hasPreview,
    bool directoriesOnly,
    bool filesOnly,
    MainViewModel<FileSystemNode> viewModel,
    IComparer<Entry<FileSystemNode>>? comparer,
    Action? onClosed) : IMenuDefinitionProvider<FileSystemNode>
{
    ITtoStrConverter<FileSystemNode> fileSystemNodeConverter = new FileSystemNodeConverter();
    private int MaxItems = 15;
    public MenuDefinition<FileSystemNode> Get()
    {
        (int, int) ScoreFunc(FileSystemNode node, Pattern pattern, Slab slab)
        {
            Span<char> buf = stackalloc char[2048];
            var toScore = fileSystemNodeConverter.Convert(node, buf);
            var score = FuzzySearcher.GetScore(toScore, pattern, slab);
            return (toScore.Length, score);
        }

        var definition = new MenuDefinition<FileSystemNode>
        {
            AsyncFunction = rootDirectory == null || !rootDirectory.Any() ?
                (writer, ct) => ListDrives(maxDepth, writer, ct) :
                (writer, ct) => ListDrives(rootDirectory, maxDepth, writer, ct),
            Header = null,
            KeyBindings = new Dictionary<(KeyModifiers, Key), Func<FileSystemNode, Task>>(),
            MinScore = 0,
            ResultHandler = resultHandler,
            ShowHeader = false,
            QuitOnEscape = quitOnEscape,
            HasPreview = hasPreview,
            Comparer = comparer ?? EntryComparer,
            FinalComparer =FinalEntryComparer,
            OnClosed = onClosed,
            StrConverter = fileSystemNodeConverter,
            ScoreFunc = ScoreFunc,
            PreviewHandler = new FileSystemPreviewHandler()
        };
        definition.KeyBindings.Add((KeyModifiers.Control, Key.O), _ => ParentDir(rootDirectory));
        return definition;
    }
    
    private readonly IComparer<Entry<FileSystemNode>> EntryComparer = Comparer<Entry<FileSystemNode>>.Create((x, y) =>
    {
        int scoreComparison = y.Score.CompareTo(x.Score);
        if (scoreComparison != 0) return scoreComparison;

        int lengthComparison = x.Length.CompareTo(y.Length);
        return lengthComparison;

        //return string.Compare(x.Line, y.Line, StringComparison.Ordinal);
    });
    
    private readonly IComparer<Entry<FileSystemNode>> FinalEntryComparer = Comparer<Entry<FileSystemNode>>.Create((x, y) =>
    {
        int scoreComparison = y.Score.CompareTo(x.Score);
        if (scoreComparison != 0) return scoreComparison;

        int lengthComparison = x.Length.CompareTo(y.Length);
        if (lengthComparison != 0) return lengthComparison;

        return string.Compare(x.Line.ToString(), y.Line.ToString(), StringComparison.Ordinal);
    });

    private async Task ParentDir(IEnumerable<string>? dirs)
    {
        if (dirs != null && dirs.Any())
        {
            var first = dirs.First();
            var directoryInfo = new DirectoryInfo(first);
            var parent = directoryInfo.Parent;
            var definition = new FileSystemMenuDefinitionProvider(
                resultHandler,
                maxDepth, [parent.FullName],
                quitOnEscape,
                hasPreview,
                directoriesOnly,
                filesOnly,
                viewModel,
                null,
                onClosed).Get();

            await viewModel.Clear();
            await viewModel.RunDefinitionAsync(definition);
        }
    }
    
    private async Task ListDrives(int maxDepth, ChannelWriter<FileSystemNode> writer, CancellationToken cancellationToken)
    {
        DriveInfo[] allDrives = DriveInfo.GetDrives();
        var drives = allDrives.Select(d => d.Name);
        await ListDrives(drives, maxDepth, writer, cancellationToken);
    }
    
    private async Task ListDrives(IEnumerable<string> rootDirectories, int maxDepth, ChannelWriter<FileSystemNode> writer, CancellationToken cancellationToken)
    {
        var fileScanner = new FileWalker9();
        await fileScanner.StartScanForDirectoriesAsync(rootDirectories, writer, maxDepth, directoriesOnly, filesOnly, cancellationToken);
    }
}

public class FileSystemNodeConverter : ITtoStrConverter<FileSystemNode>
{
    public ReadOnlySpan<char> Convert(FileSystemNode t, Span<char> buf)
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

        return buf.Slice(0, Math.Max(0, length - 1));
    }
}