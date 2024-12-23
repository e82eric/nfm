using Avalonia;
using Avalonia.Input;
using Avalonia.Themes.Fluent;

namespace nfm.menu;

public class KeyHandlerApp : Application
{
    private readonly MainViewModel _mainViewModel = new();
    private MainWindow _mainWindow;

    public KeyHandlerApp()
    {
        _mainViewModel.GlobalKeyBindings.Add((KeyModifiers.Control, Key.C), ClipboardHelper.CopyStringToClipboard);
        _mainViewModel.GlobalKeyBindings.Add((KeyModifiers.Control, Key.P), (_, vm) => {
            vm.TogglePreview();
            return Task.CompletedTask;
        });
    }

    public override void Initialize()
    {
        var fluentTheme = new FluentTheme { };
        Styles.Add(fluentTheme);
        _mainWindow = new MainWindow(_mainViewModel);
    }

    public async Task RunFileSystemMenu()
    {
        var definitionProvider = CreateDefinitionProvider(
            _mainViewModel,
            null,
            5,
            true,
            false,
            null);
        var definition = definitionProvider.Get();
        await _mainViewModel.Clear();
        await _mainViewModel.RunDefinitionAsync(definition);
    }
    
    public async Task RunLastDefinition()
    {
        await _mainViewModel.RunLastDefinition();
    }

    public async Task RunProgramsMenu()
    {
        var definitionProvider = CreateDefinitionProvider(
            _mainViewModel,
            new []{ @"c:\users\eric\AppData\Roaming\Microsoft\Windows\Start Menu",
            @"C:\ProgramData\Microsoft\Windows\Start Menu",
            @"c:\users\eric\AppData\Local\Microsoft\WindowsApps",
            @"c:\users\eric\utilities",
            @"C:\Program Files\sysinternals\"},
            Int32.MaxValue,
            false,
            true,
            ProgramComparer);
        var definition = definitionProvider.Get();
        await _mainViewModel.Clear();
        await _mainViewModel.RunDefinitionAsync(definition);
    }
    public async Task RunProcesses()
    {
        var definitionProvider = new ShowProcessesMenuDefinitionProvider(_mainViewModel, null);
        var definition = definitionProvider.Get();
        await _mainViewModel.Clear();
        await _mainViewModel.RunDefinitionAsync(definition);
    }
    public async Task RunShowWindows()
    {
        var definitionProvider = new ShowWindowsMenuDefinitionProvider2(new StdOutResultHandler(_mainViewModel), null);
        var definition = definitionProvider.Get();
        await _mainViewModel.Clear();
        await _mainViewModel.RunDefinitionAsync(definition);
    }
    
    private static FileSystemMenuDefinitionProvider CreateDefinitionProvider(
        MainViewModel _fileSystemViewModel,
        string[] directories,
        int maxDepth,
        bool hasPreview,
        bool filesOnly,
        IComparer<Entry> programComparer)
    {
        var runFileResultHandler = new RunFileResultHandler();
        return new FileSystemMenuDefinitionProvider(
            new FileSystemResultHandler(
                _fileSystemViewModel,
                runFileResultHandler,
                new ShowDirectoryResultHandler(
                    _fileSystemViewModel,
                    runFileResultHandler,
                    false,
                    hasPreview,
                    false,
                    filesOnly,
                    null),
                false,
                true),
            maxDepth,
            directories,
            false,
            hasPreview,
            false,
            filesOnly,
            _fileSystemViewModel,
            programComparer,
            null);
    }
    
    private static readonly IComparer<Entry> ProgramComparer = Comparer<Entry>.Create((x, y) =>
    {
        static int GetExtensionPriority(string line)
        {
            // Define extensions to prioritize
            string[] prioritizedExtensions = { ".exe", ".lnk", ".com", ".bat", ".cmd" };

            // Check if the line ends with one of the prioritized extensions
            foreach (var ext in prioritizedExtensions)
            {
                if (line.EndsWith(ext, StringComparison.OrdinalIgnoreCase))
                    return 1; // Higher priority
            }
            return 0; // Default priority
        }

        // Compare based on score
        int scoreComparison = y.Score.CompareTo(x.Score);
        if (scoreComparison != 0) return scoreComparison;
        return scoreComparison;

        // Compare based on extension priority
        //int extensionPriorityComparison = GetExtensionPriority(y.Line).CompareTo(GetExtensionPriority(x.Line));
        // return extensionPriorityComparison;
        //if (extensionPriorityComparison != 0) return extensionPriorityComparison;

        //// Compare based on line length
        //int lengthComparison = x.Line.Length.CompareTo(y.Line.Length);
        //if (lengthComparison != 0) return lengthComparison;

        //// Compare based on string content
        //return string.Compare(x.Line, y.Line, StringComparison.Ordinal);
    });
}