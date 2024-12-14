﻿using System.Diagnostics.CodeAnalysis;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using CommandLine;
using nfm.Cli;
using nfzf.FileSystem;

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
                    BuildStdInApp().Start((app, _) => Run(app, false), args);
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
    
    private static AppBuilder BuildStdInApp() 
        => AppBuilder.Configure(() =>
        {
            var viewModel = new MainViewModel<string>();
            viewModel.GlobalKeyBindings.Add((KeyModifiers.Control, Key.C), ClipboardHelper.CopyStringToClipboard);
            var command = new StdInMenuDefinitionProvider();
            var app = new App<string>(viewModel, command);
            return app;
        }).UsePlatformDetect();
    
    private static AppBuilder BuildCommandApp(string command)
        => AppBuilder.Configure(() =>
        {
            var viewModel = new MainViewModel<string>();
            viewModel.GlobalKeyBindings.Add((KeyModifiers.Control, Key.C), ClipboardHelper.CopyStringToClipboard);
            var definitionProvider = new RunCommandMenuDefinitionProvider(command);
            var app = new App<string>(viewModel, definitionProvider);
            return app;
        }).UsePlatformDetect();
    
    private static AppBuilder BuildFileReaderApp(string path, string? searchString) 
        => AppBuilder.Configure(() =>
        {
            var viewModel = new MainViewModel<string>();
            viewModel.GlobalKeyBindings.Add((KeyModifiers.Control, Key.C), ClipboardHelper.CopyStringToClipboard);
            var definitionProvider = new ReadFileMenuDefinitionProvider2(path, Comparers.StringScoreOnly, searchString);
            var app = new App<string>(viewModel, definitionProvider);
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
            var viewModel = new MainViewModel<FileSystemNode>();
            viewModel.GlobalKeyBindings.Add((KeyModifiers.Control, Key.C), ClipboardHelper.CopyStringToClipboard);

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
            var app = new App<FileSystemNode>(viewModel, command);
            return app;
        }).UsePlatformDetect();

    private static void Run(Application app, bool keyHandler)
    {
        app.Run(CancellationToken.None);
    }
}