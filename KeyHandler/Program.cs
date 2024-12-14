using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using nfzf.FileSystem;

namespace nfm.menu;

class Program
{
    private static KeyHandlerApp _keyHandlerApp;

    [STAThread]
    public static void Main(string[] args)
    {
        BuildAvaloniaApp(args).Start((app, strings) => Run(app), args);
    }

    private static AppBuilder BuildAvaloniaApp(string[] args) 
        => AppBuilder.Configure(() =>
        {
            _fileSystemViewModel = new MainViewModel<FileSystemNode>();
            _stringViewModel = new MainViewModel<string>();
            _fileSystemViewModel.GlobalKeyBindings.Add((KeyModifiers.Control, Key.C), ClipboardHelper.CopyStringToClipboard);
            _stringViewModel.GlobalKeyBindings.Add((KeyModifiers.Control, Key.C), ClipboardHelper.CopyStringToClipboard);
            _keyHandlerApp = new KeyHandlerApp(_stringViewModel);
            return _keyHandlerApp;
        }).UsePlatformDetect();

    private const int VK_P = 0x50;
    private const int VK_O = 0x4F;
    private const int VK_I = 0x49;
    private const int VK_U = 0x55;
    private const int VK_L = 0x4c;
    
    private static void Run(Application app)
    {
        var fileSystemTitle = "File System";
        var programLauncherTitle = "Program Launcher";
        var appDirectories = new []{ @"c:\users\eric\AppData\Roaming\Microsoft\Windows\Start Menu",
            @"C:\ProgramData\Microsoft\Windows\Start Menu",
            @"c:\users\eric\AppData\Local\Microsoft\WindowsApps",
            @"c:\users\eric\utilities",
            @"C:\Program Files\sysinternals\"};

        var keyBindings = new Dictionary<(GlobalKeyHandler.Modifiers, int), Func<Task>>();
        var runFileResultHandler = new RunFileResultHandler();
        var systemMenuDefinitionProvider2 = new FileSystemMenuDefinitionProvider(
            new FileSystemResultHandler(
                runFileResultHandler,
                new ShowDirectoryResultHandler(runFileResultHandler, false, false, false, true, null),
                false,
                true),
            Int32.MaxValue,
            appDirectories,
            false,
            false,
            false,
            true,
            _fileSystemViewModel,
            ProgramComparer,
            null);
        keyBindings.Add((GlobalKeyHandler.Modifiers.LAlt | GlobalKeyHandler.Modifiers.LShift, VK_P), async () =>
        {
            var def =  systemMenuDefinitionProvider2.Get();
            await _keyHandlerApp.RunDefinition(def);
        });
        var showWindowsMenuDefinitionProvider = new ShowWindowsMenuDefinitionProvider(new StdOutResultHandler(), null);
        keyBindings.Add((GlobalKeyHandler.Modifiers.LAlt | GlobalKeyHandler.Modifiers.LShift, VK_I), async () =>
        {
            var def = showWindowsMenuDefinitionProvider.Get();
            await _keyHandlerApp.RunDefinition(def);
        });
        var showProcessesMenuDefinitionProvider2 = new ShowProcessesMenuDefinitionProvider2(_stringViewModel, null);
        keyBindings.Add((GlobalKeyHandler.Modifiers.LAlt | GlobalKeyHandler.Modifiers.LShift, VK_U), async () =>
        {
            var def = showProcessesMenuDefinitionProvider2.Get();
            await _keyHandlerApp.RunDefinition(def);
        });
        var fileSystemMenuDefinitionProvider2 = new FileSystemMenuDefinitionProvider(
            new FileSystemResultHandler(
                runFileResultHandler,
                new ShowDirectoryResultHandler(runFileResultHandler, false, true, false, false, null),
                false,
                true),
            5,
            null,
            false,
            true,
            false,
            false,
            _fileSystemViewModel,
            null,
            null);
        keyBindings.Add((GlobalKeyHandler.Modifiers.LAlt | GlobalKeyHandler.Modifiers.LShift, VK_L), async () =>
        {
            var def = fileSystemMenuDefinitionProvider2.Get();
            await _keyHandlerApp.RunDefinition(def);
        });
        GlobalKeyHandler.SetHook(keyBindings);
        app.Run(CancellationToken.None);
    }
    
    private static readonly IComparer<Entry<FileSystemNode>> ProgramComparer = Comparer<Entry<FileSystemNode>>.Create((x, y) =>
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

    private static MainViewModel<FileSystemNode> _fileSystemViewModel;
    private static MainViewModel<string> _stringViewModel;
}
