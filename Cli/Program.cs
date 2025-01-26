using System.Diagnostics.CodeAnalysis;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using CommandLine;
using nfm.Cli;

namespace nfm.menu;

class StdInOptions
{
    [Option]
    public string? Header { get; set; }
    [Option]
    public string? EditCommand { get; set; }
    [Option]
    public string? PreviewCommand { get; set; }
}

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
    [Option]
    public bool DirectoriesOnly { get; set; }
    [Option]
    public bool FilesOnly { get; set; }
}

[Verb("command")]
class CommandOptions
{
    [Value(0, Required = true)]
    public IEnumerable<string>? Command { get; set; }
}

[Verb("filereader")]
class FileReaderOptions
{
    [Option(Required = true)]
    public string? Path { get; set; }
    [Option]
    public string? SearchString { get; set; }
}

class Program
{
    [DynamicDependency(DynamicallyAccessedMemberTypes.All, typeof(StdInOptions))]
    [DynamicDependency(DynamicallyAccessedMemberTypes.All, typeof(FileReaderOptions))]
    [DynamicDependency(DynamicallyAccessedMemberTypes.All, typeof(CommandOptions))]
    [DynamicDependency(DynamicallyAccessedMemberTypes.All, typeof(FileSystemOptions))]
    [STAThread]
    public static void Main(string[] args)
    {
        if (Console.IsInputRedirected)
        {
            try
            {
                int nextChar = Console.In.Peek();
                if (nextChar != -1)
                {
                    Parser.Default.ParseArguments<StdInOptions>(args)
                        .MapResult(o =>
                        {
                            BuildStdInApp(o.EditCommand, o.PreviewCommand, o.Header).Start((app, _) => Run(app, false), args);
                            return 0;
                        },
                        _ => 1);
                    return;
                }
            }
            catch (Exception)
            {
            }
        }
        
        Parser.Default.ParseArguments<FileSystemOptions, CommandOptions, FileReaderOptions>(args)
            .MapResult(
                (FileSystemOptions opts) =>
                {
                    BuildFileSystemApp(opts.RootDirectory, opts.MaxDepth, opts.HasPreview, opts.DirectoriesOnly, opts.FilesOnly)
                        .Start((application, strings) => Run(application, false), args);
                    return 0;
                },
                (CommandOptions opts) =>
                {
                    if (opts.Command != null)
                    {
                        BuildCommandApp(string.Join(" ", opts.Command))
                            .Start((application, strings) => Run(application, false), args);
                        return 0;
                    }

                    return -1;
                },
                (FileReaderOptions opts) =>
                {
                    var searchString = opts.SearchString == null ? opts.SearchString! : string.Empty;
                    BuildFileReaderApp(string.Join(" ", opts.Path), searchString)
                        .Start((application, strings) => Run(application, false), args);
                    return 0;
                },
                errors => 1);
    }
    
    private static AppBuilder BuildStdInApp(string? editCommand, string? previewCommand, string? header) 
        => AppBuilder.Configure(() =>
        {
            var viewModel = new MainViewModel();
            viewModel.GlobalKeyBindings.Add((KeyModifiers.Control, Key.P), (_, vm) => {
                vm.TogglePreview();
                return Task.CompletedTask;
            });
            viewModel.GlobalKeyBindings.Add((KeyModifiers.Control, Key.C), ClipboardHelper.CopyStringToClipboard);
            var app = new App(viewModel, () => new StdInMenuDefinitionProvider(
                viewModel,
                false,
                editCommand,
                previewCommand,
                header));
            return app;
        }).UseWin32().SetupWithClassicDesktopLifetime([]);
    
    private static AppBuilder BuildCommandApp(string command)
        => AppBuilder.Configure(() =>
        {
            var viewModel = new MainViewModel();
            viewModel.GlobalKeyBindings.Add((KeyModifiers.Control, Key.C), ClipboardHelper.CopyStringToClipboard);
            var app = new App(viewModel, () => new RunCommandMenuDefinitionProvider(command, viewModel));
            return app;
        }).UsePlatformDetect();
    
    private static AppBuilder BuildFileReaderApp(string path, string searchString) 
        => AppBuilder.Configure(() =>
        {
            var viewModel = new MainViewModel();
            viewModel.GlobalKeyBindings.Add((KeyModifiers.Control, Key.C), ClipboardHelper.CopyStringToClipboard);
            var app = new App(viewModel, () => new ReadFileMenuDefinitionProvider(path, Comparers.ScoreOnly, searchString, viewModel));
            return app;
        }).UsePlatformDetect();
    
    private static AppBuilder BuildFileSystemApp(
        string? rootDirectory,
        int maxDepth,
        bool hasPreview,
        bool directoriesOnly,
        bool filesOnly) 
        => AppBuilder.Configure(() =>
        {
            var viewModel = new MainViewModel();
            viewModel.GlobalKeyBindings.Add((KeyModifiers.Control, Key.C), ClipboardHelper.CopyStringToClipboard);
            viewModel.GlobalKeyBindings.Add((KeyModifiers.Control, Key.P), (_, vm) => {
                vm.TogglePreview();
                return Task.CompletedTask;
            });

            var app = new App(viewModel, () =>
            {
                var command = new FileSystemMenuDefinitionProvider(
                    new StdOutResultHandler(viewModel),
                    maxDepth,
                    rootDirectory == null ? [] : [rootDirectory],
                    true,
                    hasPreview,
                    directoriesOnly,
                    filesOnly,
                    viewModel,
                    null,
                    null);
                return command;
            });
            return app;
        }).UsePlatformDetect();

    private static void Run(Application app, bool keyHandler)
    {
        app.Run(CancellationToken.None);
    }
}
