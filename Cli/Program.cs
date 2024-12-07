using System.Diagnostics.CodeAnalysis;
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
    [Option]
    public bool DirectoriesOnly { get; set; }
    [Option]
    public bool FilesOnly { get; set; }
    public string? SearchString { get; set; }
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
    private static MainViewModel? _viewModel;
    private static App? _app;

    [DynamicDependency(DynamicallyAccessedMemberTypes.All, typeof(FileReaderOptions))]
    [DynamicDependency(DynamicallyAccessedMemberTypes.All, typeof(CommandOptions))]
    [DynamicDependency(DynamicallyAccessedMemberTypes.All, typeof(FileSystemOptions))]
    [STAThread]
    public static void Main(string[] args)
    {
        //if (Console.IsInputRedirected)
        //{
        //    try
        //    {
        //        int nextChar = Console.In.Peek();
        //        if (nextChar != -1)
        //        {
        //            BuildStdInApp().Start((app, _) => Run(app, false), args);
        //            return;
        //        }
        //    }
        //    catch (Exception e)
        //    {
        //    }
        //}
        
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
    
    private static AppBuilder BuildStdInApp() 
        => AppBuilder.Configure(() =>
        {
            var globalKeyBindings = new Dictionary<(KeyModifiers, Key), Func<string, Task>>();
            _viewModel = new MainViewModel();
            _viewModel.GlobalKeyBindings.Add((KeyModifiers.Control, Key.C), ClipboardHelper.CopyStringToClipboard);
            var command = new StdInMenuDefinitionProvider();
            _app = new App(_viewModel, command);
            return _app;
        }).UsePlatformDetect();
    
    private static AppBuilder BuildCommandApp(string command)
        => AppBuilder.Configure(() =>
        {
            _viewModel = new MainViewModel();
            _viewModel.GlobalKeyBindings.Add((KeyModifiers.Control, Key.C), ClipboardHelper.CopyStringToClipboard);
            var definitionProvider = new RunCommandMenuDefinitionProvider(command);
            _app = new App(_viewModel, definitionProvider);
            return _app;
        }).UsePlatformDetect();
    
    private static AppBuilder BuildFileReaderApp(string path, string? searchString) 
        => AppBuilder.Configure(() =>
        {
            _viewModel = new MainViewModel();
            _viewModel.GlobalKeyBindings.Add((KeyModifiers.Control, Key.C), ClipboardHelper.CopyStringToClipboard);
            var definitionProvider = new ReadFileMenuDefinitionProvider(path, HistoryComparer, searchString);
            _app = new App(_viewModel, definitionProvider);
            return _app;
        }).UsePlatformDetect();
    
    private static AppBuilder BuildFileSystemApp(
        string? rootDirectory,
        int maxDepth,
        bool hasPreview,
        bool directoriesOnly,
        bool filesOnly) 
        => AppBuilder.Configure(() =>
        {
            var title = "File System";
            _viewModel = new MainViewModel();
            _viewModel.GlobalKeyBindings.Add((KeyModifiers.Control, Key.C), ClipboardHelper.CopyStringToClipboard);

            var command = new FileSystemMenuDefinitionProvider(
                new StdOutResultHandler(),
                maxDepth,
                [rootDirectory],
                true,
                hasPreview,
                directoriesOnly,
                filesOnly,
                _viewModel,
                null,
                null);
            _app = new App(_viewModel, command);
            return _app;
        }).UsePlatformDetect();

    private static void Run(Application app, bool keyHandler)
    {
        app.Run(CancellationToken.None);
    }
    
    private static readonly IComparer<Entry> HistoryComparer = Comparer<Entry>.Create((x, y) =>
    {
        int scoreComparison = y.Score.CompareTo(x.Score);
        if (scoreComparison != 0) return scoreComparison;

        return x.Index.CompareTo(y.Index);
    });
}
