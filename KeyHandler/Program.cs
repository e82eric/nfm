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
            //_fileSystemViewModel.GlobalKeyBindings.Add((KeyModifiers.Control, Key.C), ClipboardHelper.CopyStringToClipboard);
            //_stringViewModel.GlobalKeyBindings.Add((KeyModifiers.Control, Key.C), ClipboardHelper.CopyStringToClipboard);
            _keyHandlerApp = new KeyHandlerApp();
            return _keyHandlerApp;
        }).UsePlatformDetect();

    private const int VK_P = 0x50;
    private const int VK_O = 0x4F;
    private const int VK_I = 0x49;
    private const int VK_U = 0x55;
    private const int VK_L = 0x4c;
    
    private static void Run(Application app)
    {
        var keyBindings = new Dictionary<(GlobalKeyHandler.Modifiers, int), Func<Task>>();
        keyBindings.Add((GlobalKeyHandler.Modifiers.LAlt | GlobalKeyHandler.Modifiers.LShift, VK_P), async () =>
        {
            await _keyHandlerApp.RunProgramsMenu();
        });
        keyBindings.Add((GlobalKeyHandler.Modifiers.LAlt | GlobalKeyHandler.Modifiers.LShift, VK_I), async () =>
        {
            await _keyHandlerApp.RunShowWindows();
        });
        keyBindings.Add((GlobalKeyHandler.Modifiers.LAlt | GlobalKeyHandler.Modifiers.LShift, VK_U), async () =>
        {
            await _keyHandlerApp.RunProcesses();
        });
        keyBindings.Add((GlobalKeyHandler.Modifiers.LAlt | GlobalKeyHandler.Modifiers.LShift, VK_L), async () =>
        {
            await _keyHandlerApp.RunFileSystemMenu();
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

    //private static MainViewModel<FileSystemNode> _fileSystemViewModel;
    //private static MainViewModel<string> _stringViewModel;
}
