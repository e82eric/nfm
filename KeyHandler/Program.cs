using Avalonia;
using Avalonia.Controls;

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
            _keyHandlerApp = new KeyHandlerApp();
            return _keyHandlerApp;
        }).UsePlatformDetect();

    private const int VK_P = 0x50;
    private const int VK_O = 0x4F;
    private const int VK_I = 0x49;
    private const int VK_U = 0x55;
    private const int VK_L = 0x4c;
    private const int VK_R = 0x52;
    
    private static void Run(Application app)
    {
        var keyBindings = new Dictionary<(GlobalKeyHandler.Modifiers, int), Func<Task>>
        {
            {
                (GlobalKeyHandler.Modifiers.LAlt | GlobalKeyHandler.Modifiers.LShift, VK_P),
                async () => { await _keyHandlerApp.RunProgramsMenu(); }
            },
            {
                (GlobalKeyHandler.Modifiers.LAlt | GlobalKeyHandler.Modifiers.LShift, VK_I),
                async () => { await _keyHandlerApp.RunShowWindows(); }
            },
            { (GlobalKeyHandler.Modifiers.LAlt | GlobalKeyHandler.Modifiers.LShift, VK_U), async () =>
            {
                await _keyHandlerApp.RunProcesses();
            } },
            { (GlobalKeyHandler.Modifiers.LAlt | GlobalKeyHandler.Modifiers.LShift, VK_L), async () =>
            {
                await _keyHandlerApp.RunFileSystemMenu();
            } },
            { (GlobalKeyHandler.Modifiers.LAlt | GlobalKeyHandler.Modifiers.LShift, VK_R), async () =>
            {
                await _keyHandlerApp.RunLastDefinition();
            } },
        };
        GlobalKeyHandler.SetHook(keyBindings);
        app.Run(CancellationToken.None);
    }
}
