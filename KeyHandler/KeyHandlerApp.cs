using Avalonia;
using Avalonia.Input;
using Avalonia.Themes.Fluent;
using nfzf.FileSystem;

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
        _mainWindow.Show();
        await _mainViewModel.RunDefinitionAsync(definition);
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
        _mainViewModel.GlobalKeyBindings.Add((KeyModifiers.Control, Key.C), ClipboardHelper.CopyStringToClipboard);
        await _mainViewModel.Clear();
        _mainWindow.Show();
        await _mainViewModel.RunDefinitionAsync(definition);
    }
    public async Task RunProcesses()
    {
        var definitionProvider = new ShowProcessesMenuDefinitionProvider(_mainViewModel, null);
        var definition = definitionProvider.Get();
        //var window = new MainWindow(_mainViewModel);
        await _mainViewModel.Clear();
        await _mainViewModel.RunDefinitionAsync(definition);
    }
    public async Task RunShowWindows()
    {
        var definitionProvider = new ShowWindowsMenuDefinitionProvider2(new StdOutResultHandler(), null);
        var definition = definitionProvider.Get();
        await _mainViewModel.Clear();
        await _mainViewModel.RunDefinitionAsync(definition);
        _mainWindow.Show();
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
                runFileResultHandler,
                new ShowDirectoryResultHandler2(
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
        //var fileSystemMenuDefinitionProvider2 = new FileSystemMenuDefinitionProvider(
        //    new FileSystemResultHandler(
        //        runFileResultHandler,
        //        new ShowDirectoryResultHandler(
        //            runFileResultHandler,
        //            false,
        //            true,
        //            false,
        //            false,
        //            null),
        //        false,
        //        true),
        //    5,
        //    null,
        //    false,
        //    true,
        //    false,
        //    false,
        //    _fileSystemViewModel,
        //    null,
        //    null);
    }
    // private static void Run(MainViewModel<FileSystemNode> _fileSystemViewModel)
    // {
    //     var appDirectories = new []{ @"c:\users\eric\AppData\Roaming\Microsoft\Windows\Start Menu",
    //         @"C:\ProgramData\Microsoft\Windows\Start Menu",
    //         @"c:\users\eric\AppData\Local\Microsoft\WindowsApps",
    //         @"c:\users\eric\utilities",
    //         @"C:\Program Files\sysinternals\"};
    //
    //     var runFileResultHandler = new RunFileResultHandler();
    //     var systemMenuDefinitionProvider2 = new FileSystemMenuDefinitionProvider(
    //         new FileSystemResultHandler(
    //             runFileResultHandler,
    //             new ShowDirectoryResultHandler(runFileResultHandler, false, false, false, true, null),
    //             false,
    //             true),
    //         Int32.MaxValue,
    //         appDirectories,
    //         false,
    //         false,
    //         false,
    //         true,
    //         _fileSystemViewModel,
    //         ProgramComparer,
    //         null);
    //     var fileSystemMenuDefinitionProvider2 = new FileSystemMenuDefinitionProvider(
    //         new FileSystemResultHandler(
    //             runFileResultHandler,
    //             new ShowDirectoryResultHandler(runFileResultHandler, false, true, false, false, null),
    //             false,
    //             true),
    //         5,
    //         null,
    //         false,
    //         true,
    //         false,
    //         false,
    //         _fileSystemViewModel,
    //         null,
    //         null);
    // }
    
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