using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Threading;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using CommandLine;

namespace nfm.menu;

[Verb("filesystem")]
class FileSystemOptions
{
    [Option(Default = false)]
    public bool SearchDirectoryOnSelect { get; set; }
    [Option(Default = null)]
    public string? RootDirectory { get; set; }
    [Option(Default = int.MaxValue)]
    public int MaxDepth { get; set; }
    [Option(Default = false)]
    public bool HasPreview { get; set; }
}

[Verb("keyhandler")]
class KeyHandlerOptions
{
}

class Program
{
    private static MainViewModel _viewModel;
    private static App _app;
    private static KeyHandlerApp _keyHandlerApp;

    [DynamicDependency(DynamicallyAccessedMemberTypes.All, typeof(FileSystemOptions))]
    [DynamicDependency(DynamicallyAccessedMemberTypes.All, typeof(KeyHandlerOptions))]
    [STAThread]
    public static void Main(string[] args)
    {
        if (Console.IsInputRedirected)
        {
            BuildStdInApp().Start((app, args) => Run(app, false), args);
            return;
        }
        
        Parser.Default.ParseArguments<FileSystemOptions, KeyHandlerOptions>(args)
            .MapResult(
                (FileSystemOptions opts) =>
                {
                    BuildFileSystemApp(opts.SearchDirectoryOnSelect, opts.RootDirectory, opts.MaxDepth, opts.HasPreview)
                        .Start((application, strings) => Run(application, false), args);
                    return 0;
                },
                (KeyHandlerOptions opts) =>
                {
                    BuildAvaloniaApp(args).Start((app, strings) => Run(app, true), args);
                    return 0;
                },
                errors => 1);
    }
    
    private static AppBuilder BuildStdInApp() 
        => AppBuilder.Configure(() =>
        {
            var globalKeyBindings = new Dictionary<(KeyModifiers, Key), Action<string>>();
            globalKeyBindings.Add((KeyModifiers.Control, Key.C), ClipboardHelper.CopyStringToClipboard);
            _viewModel = new MainViewModel(globalKeyBindings);
            var command = new StdInMenuDefinitionProvider();
            _app = new App(_viewModel, command);
            return _app;
        }).UsePlatformDetect();
    
    private static AppBuilder BuildFileSystemApp(bool searchDirectoriesOnSelect, string? rootDirectory, int maxDepth, bool hasPreview) 
        => AppBuilder.Configure(() =>
        {
            var globalKeyBindings = new Dictionary<(KeyModifiers, Key), Action<string>>();
            globalKeyBindings.Add((KeyModifiers.Control, Key.C), ClipboardHelper.CopyStringToClipboard);
            _viewModel = new MainViewModel(globalKeyBindings);

            var resultHandler = new TestResultHandler(_viewModel, true, true, true, searchDirectoriesOnSelect);
            var command = new FileSystemMenuDefinitionProvider(resultHandler, maxDepth, [rootDirectory], true, hasPreview);
            _app = new App(_viewModel, command);
            return _app;
        }).UsePlatformDetect();

    private static AppBuilder BuildAvaloniaApp(string[] args) 
        => AppBuilder.Configure(() =>
        {
            var globalKeyBindings = new Dictionary<(KeyModifiers, Key), Action<string>>();
            globalKeyBindings.Add((KeyModifiers.Control, Key.C), ClipboardHelper.CopyStringToClipboard);
            _viewModel = new MainViewModel(globalKeyBindings);
            _keyHandlerApp = new KeyHandlerApp(_viewModel);
            return _keyHandlerApp;
        }).UsePlatformDetect();

    private const int VK_O = 0x4F;
    private const int VK_I = 0x49;
    private const int VK_U = 0x55;
    private const int VK_L = 0x4c;
    
    private static void Run(Application app, bool keyHandler)
    {
        if (keyHandler)
        {
            var appDirectories = new []{ @"c:\users\eric\AppData\Roaming\Microsoft\Windows\Start Menu",
                @"C:\ProgramData\Microsoft\Windows\Start Menu",
                @"c:\users\eric\AppData\Local\Microsoft\WindowsApps",
                @"c:\users\eric\utilities",
                @"C:\Program Files\sysinternals\"};
            
            var fileSystemResultHandler = new TestResultHandler(_viewModel, false, false, false, true);
            
            var keyBindings = new Dictionary<(GlobalKeyHandler.Modifiers, int), IMenuDefinitionProvider>();
            keyBindings.Add((GlobalKeyHandler.Modifiers.LAlt, VK_O), new FileSystemMenuDefinitionProvider(
                fileSystemResultHandler,
                Int32.MaxValue,
                appDirectories,
                false,
                false));
            keyBindings.Add((GlobalKeyHandler.Modifiers.LAlt, VK_I), new ShowWindowsMenuDefinitionProvider());
            keyBindings.Add((GlobalKeyHandler.Modifiers.LAlt, VK_U), new ShowProcessesMenuDefinitionProvider());
            keyBindings.Add((GlobalKeyHandler.Modifiers.LAlt, VK_L), new FileSystemMenuDefinitionProvider(
                fileSystemResultHandler,
                5,
                null,
                false,
                false));
            GlobalKeyHandler.SetHook(_keyHandlerApp, keyBindings);
            app.Run(CancellationToken.None);
        }
        else
        {
            app.Run(CancellationToken.None);
        }
    }
}