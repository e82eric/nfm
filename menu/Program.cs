using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;

namespace nfm.menu;

class Program
{
    private static MainViewModel _viewModel;
    private static App _app;
    private static string _command;

    [STAThread]
    public static void Main(string[] args)
    {
        BuildAvaloniaApp(args).Start(Run, args);
    }

    private static AppBuilder BuildAvaloniaApp(string[] args) 
        => AppBuilder.Configure(() =>
        {
            bool debug = args.Contains("--debug");
            if (debug)
            {
                Debugger.Launch();
            }

            _command = """fd -t f . "%USERPROFILE%\AppData\Roaming\Microsoft\Windows\Start Menu" "C:\ProgramData\Microsoft\Windows\Start Menu" "%USERPROFILE%\AppData\Local\Microsoft\WindowsApps" "%USERPROFILE%\utilities" "C:\Program Files\sysinternals\""";

            var globalKeyBindings = new Dictionary<(KeyModifiers, Key), Action<string>>();
            globalKeyBindings.Add((KeyModifiers.Control, Key.C), ClipboardHelper.CopyStringToClipboard);
            _viewModel = new MainViewModel(globalKeyBindings);
            _app = new App(_command, _viewModel);
            return _app;
        }).UsePlatformDetect();

    private static void Run(Application app, string[] args)
    {
        GlobalKeyHandler.SetHook(_app);
        app.Run(CancellationToken.None);
    }
}