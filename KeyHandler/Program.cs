using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;

namespace nfm.menu;

class Program
{
    private static MainViewModel _viewModel;
    private static KeyHandlerApp _keyHandlerApp;

    [STAThread]
    public static void Main(string[] args)
    {
        BuildAvaloniaApp(args).Start((app, strings) => Run(app), args);
    }

    private static AppBuilder BuildAvaloniaApp(string[] args) 
        => AppBuilder.Configure(() =>
        {
            var globalKeyBindings = new Dictionary<(KeyModifiers, Key), Func<string, Task>>();
            globalKeyBindings.Add((KeyModifiers.Control, Key.C), async line => {
                await ClipboardHelper.CopyStringToClipboard(line);
                await _viewModel.ShowToast($"Copied \"{line}\" to clipboard", 1500);
            });
            _viewModel = new MainViewModel(globalKeyBindings);
            _keyHandlerApp = new KeyHandlerApp(_viewModel);
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

        var keyBindings = new Dictionary<(GlobalKeyHandler.Modifiers, int), IMenuDefinitionProvider>();
        var runFileResultHandler = new RunFileResultHandler();
        keyBindings.Add((GlobalKeyHandler.Modifiers.LAlt | GlobalKeyHandler.Modifiers.LShift, VK_P), new FileSystemMenuDefinitionProvider(
            new FileSystemResultHandler(
                runFileResultHandler,
                new ShowDirectoryResultHandler(runFileResultHandler, false, false, false, true, null, programLauncherTitle),
                false,
                true),
            Int32.MaxValue,
            appDirectories,
            false,
            false,
            false,
            true,
            _viewModel,
            ProgramComparer,
            null,
            programLauncherTitle));
        keyBindings.Add((GlobalKeyHandler.Modifiers.LAlt | GlobalKeyHandler.Modifiers.LShift, VK_I), new ShowWindowsMenuDefinitionProvider(new StdOutResultHandler(), null));
        keyBindings.Add((GlobalKeyHandler.Modifiers.LAlt | GlobalKeyHandler.Modifiers.LShift, VK_U), new ShowProcessesMenuDefinitionProvider(_viewModel, null));
        keyBindings.Add((GlobalKeyHandler.Modifiers.LAlt | GlobalKeyHandler.Modifiers.LShift, VK_L), new FileSystemMenuDefinitionProvider(
            new FileSystemResultHandler(
                runFileResultHandler,
                new ShowDirectoryResultHandler(runFileResultHandler, false, true, false, false, null, fileSystemTitle),
                false,
                true),
            5,
            null,
            false,
            true,
            false,
            false,
            _viewModel,
            null,
            null,
            fileSystemTitle));
        GlobalKeyHandler.SetHook(_keyHandlerApp, keyBindings);
        app.Run(CancellationToken.None);
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

        // Compare based on extension priority
        int extensionPriorityComparison = GetExtensionPriority(y.Line).CompareTo(GetExtensionPriority(x.Line));
        if (extensionPriorityComparison != 0) return extensionPriorityComparison;

        // Compare based on line length
        int lengthComparison = x.Line.Length.CompareTo(y.Line.Length);
        if (lengthComparison != 0) return lengthComparison;

        // Compare based on string content
        return string.Compare(x.Line, y.Line, StringComparison.Ordinal);
    });
}
