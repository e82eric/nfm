using System.Threading.Channels;
using Avalonia.Input;
using nfzf;
using nfzf.FileSystem;

namespace nfm.menu;

public class FileSystemMenuDefinitionProvider : IMenuDefinitionProvider
{
    private int MaxItems = 15;
    public MenuDefinition Get()
    {
        return _definition;
    }
    
    private readonly IComparer<Entry> EntryComparer = Comparer<Entry>.Create((x, y) =>
    {
        int scoreComparison = y.Score.CompareTo(x.Score);
        if (scoreComparison != 0) return scoreComparison;

        int lengthComparison = x.Length.CompareTo(y.Length);
        return lengthComparison;
    });
    
    private readonly IComparer<Entry> FinalEntryComparer = Comparer<Entry>.Create((x, y) =>
    {
        int scoreComparison = y.Score.CompareTo(x.Score);
        if (scoreComparison != 0) return scoreComparison;

        int lengthComparison = x.Length.CompareTo(y.Length);
        if (lengthComparison != 0) return lengthComparison;

        return string.Compare(x.Item.ToString(), y.Item.ToString(), StringComparison.Ordinal);
    });

    private readonly IResultHandler _resultHandler;
    private readonly int _maxDepth;
    private readonly IEnumerable<string>? _rootDirectory;
    private readonly bool _quitOnEscape;
    private readonly bool _hasPreview;
    private readonly bool _directoriesOnly;
    private readonly bool _filesOnly;
    private readonly IMainViewModel _viewModel;
    private readonly IComparer<Entry>? _comparer;
    private readonly Action? _onClosed;
    private MenuDefinition _definition;
    private FileWalker _fileScanner;

    public FileSystemMenuDefinitionProvider(IResultHandler resultHandler,
        int maxDepth,
        IEnumerable<string>? rootDirectory,
        bool quitOnEscape,
        bool hasPreview,
        bool directoriesOnly,
        bool filesOnly,
        IMainViewModel viewModel,
        IComparer<Entry>? comparer,
        Action? onClosed)
    {
        _resultHandler = resultHandler;
        _maxDepth = maxDepth;
        _rootDirectory = rootDirectory;
        _quitOnEscape = quitOnEscape;
        _hasPreview = hasPreview;
        _directoriesOnly = directoriesOnly;
        _filesOnly = filesOnly;
        _viewModel = viewModel;
        _comparer = comparer;
        _onClosed = onClosed;
        _fileScanner = new FileWalker();
        
        _definition = new MenuDefinition
        {
            AsyncFunction = _rootDirectory == null || !_rootDirectory.Any() ?
                (writer, ct) => ListDrives(_maxDepth, writer, ct) :
                (writer, ct) => ListDrives(_rootDirectory, _maxDepth, writer, ct),
            Header = null,
            MinScore = 0,
            ResultHandler = _resultHandler,
            QuitOnEscape = _quitOnEscape,
            HasPreview = _hasPreview,
            Comparer = _comparer ?? EntryComparer,
            FinalComparer = _comparer ?? FinalEntryComparer,
            OnClosed = _onClosed,
            ScoreFunc = ScoreFunc,
            PreviewHandler = new FileSystemPreviewHandler(),
            EditAction = (itemObj, newText) =>
            {
                var itemStr = itemObj.ToString();
                if (itemStr != null)
                {
                    var info = new FileInfo(itemStr);
                    if (info.Exists)
                    {
                        try
                        {
                            if (!File.Exists(newText))
                            {
                                info.MoveTo(newText);
                                var itemNode = (FileSystemNode)itemObj;
                                itemNode.UpdateTextSlow(new FileInfo(newText));
                                return Task.FromResult(Result.Ok());
                            }

                            return Task.FromResult(Result.Error($"{newText} already exists"));
                        }
                        catch (Exception e)
                        {
                            return Task.FromResult(Result.Error(e.Message));
                        }
                    }
                }

                return Task.FromResult(Result.Error("Failed to update file path"));
            }
        };
        _definition.KeyBindings.Add((KeyModifiers.Control, Key.O), _ => ParentDir(_rootDirectory));

        (int, int) ScoreFunc(object nodeObj, Pattern pattern, Slab slab)
        {
            Span<char> buf = stackalloc char[2048];
            var node = (FileSystemNode)nodeObj;
            var toScore = node.ToString(buf);
            var score = FuzzySearcher.GetScore(toScore, pattern, slab);
            return (toScore.Length, score);
        }
    }

    private async Task ParentDir(IEnumerable<string>? dirs)
    {
        if (dirs != null && dirs.Any())
        {
            var first = dirs.First();
            var directoryInfo = new DirectoryInfo(first);
            var parent = directoryInfo.Parent;
            var definition = new FileSystemMenuDefinitionProvider(
                _resultHandler,
                _maxDepth, 
                [parent.FullName],
                _quitOnEscape,
                _hasPreview,
                _directoriesOnly,
                _filesOnly,
                _viewModel,
                null,
                _onClosed).Get();

            await _viewModel.Clear();
            await _viewModel.RunDefinitionAsync(definition);
        }
    }
    
    private async Task ListDrives(int maxDepth, ChannelWriter<object> writer, CancellationToken cancellationToken)
    {
        DriveInfo[] allDrives = DriveInfo.GetDrives();
        var drives = allDrives.Select(d => d.Name);
        await ListDrives(drives, maxDepth, writer, cancellationToken);
    }
    
    private async Task ListDrives(IEnumerable<string> rootDirectories, int maxDepth, ChannelWriter<object> writer, CancellationToken cancellationToken)
    {
        await _fileScanner.StartScanForDirectoriesAsync(rootDirectories, writer, maxDepth, _directoriesOnly, _filesOnly, cancellationToken);
    }
}