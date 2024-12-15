using Avalonia;
using System;
using nfm.menu;
using nfzf.FileSystem;

namespace tempA;

class Program
{
    // Initialization code. Don't use any Avalonia, third-party APIs or any
    // SynchronizationContext-reliant code before AppMain is called: things aren't initialized
    // yet and stuff might break.
    [STAThread]
    public static void Main(string[] args) => BuildFileSystemApp("C:", int.MaxValue, false, false, false)
        .StartWithClassicDesktopLifetime(args);

    // Avalonia configuration, don't remove; also used by visual designer.
    //public static AppBuilder BuildAvaloniaApp()
    //    => AppBuilder.Configure<App>()
    //        .UsePlatformDetect()
    //        .WithInterFont()
    //        .LogToTrace();
    
    private static AppBuilder BuildFileSystemApp(
        string? rootDirectory,
        int maxDepth,
        bool hasPreview,
        bool directoriesOnly,
        bool filesOnly) 
        => AppBuilder.Configure(() =>
        {
            var title = "File System";
            var viewModel = new MainViewModel();
            //viewModel.GlobalKeyBindings.Add((KeyModifiers.Control, Key.C), ClipboardHelper.CopyStringToClipboard);

            var command = new FileSystemMenuDefinitionProvider(
                new StdOutResultHandler(),
                maxDepth,
                [rootDirectory],
                true,
                hasPreview,
                directoriesOnly,
                filesOnly,
                viewModel,
                null,
                null);
            var app = new App(viewModel, command);
            return app;
        }).UsePlatformDetect();
}