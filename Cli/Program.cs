using System.Diagnostics;
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
    public string? EditCommand { get; set; }
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
    [Value(0)]
    public IEnumerable<string> Command { get; set; }
}

[Verb("filereader")]
class FileReaderOptions
{
    [Option]
    public string Path { get; set; }
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
                            BuildStdInApp(o.EditCommand).Start((app, _) => Run(app, false), args);
                            return 0;
                        },
                        _ => 1);
                    return;
                }
            }
            catch (Exception e)
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
                    BuildCommandApp(string.Join(" ", opts.Command))
                        .Start((application, strings) => Run(application, false), args);
                    return 0;
                },
                (FileReaderOptions opts) =>
                {
                    BuildFileReaderApp(string.Join(" ", opts.Path), opts.SearchString)
                        .Start((application, strings) => Run(application, false), args);
                    return 0;
                },
                errors => 1);
    }
    
    private static AppBuilder BuildStdInApp(string? editCommand) 
        => AppBuilder.Configure(() =>
        {
            var viewModel = new MainViewModel();
            viewModel.GlobalKeyBindings.Add((KeyModifiers.Control, Key.C), ClipboardHelper.CopyStringToClipboard);
            var command = new StdInMenuDefinitionProvider(viewModel, false, editCommand);
            var app = new App(viewModel, command);
            return app;
        }).UsePlatformDetect();
    
    private static AppBuilder BuildCommandApp(string command)
        => AppBuilder.Configure(() =>
        {
            var viewModel = new MainViewModel();
            viewModel.GlobalKeyBindings.Add((KeyModifiers.Control, Key.C), ClipboardHelper.CopyStringToClipboard);
            var definitionProvider = new RunCommandMenuDefinitionProvider(command, viewModel);
            var app = new App(viewModel, definitionProvider);
            return app;
        }).UsePlatformDetect();
    
    private static AppBuilder BuildFileReaderApp(string path, string? searchString) 
        => AppBuilder.Configure(() =>
        {
            var viewModel = new MainViewModel();
            viewModel.GlobalKeyBindings.Add((KeyModifiers.Control, Key.C), ClipboardHelper.CopyStringToClipboard);
            var definitionProvider = new ReadFileMenuDefinitionProvider(path, Comparers.ScoreOnly, searchString, viewModel);
            var app = new App(viewModel, definitionProvider);
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

            var command = new FileSystemMenuDefinitionProvider(
                new StdOutResultHandler(viewModel),
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

    private static void Run(Application app, bool keyHandler)
    {
        app.Run(CancellationToken.None);
    }
}
