using System.Diagnostics.CodeAnalysis;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using CommandLine;
using nfm.Cli;
using nfm.menu;

namespace Cli_Linux;

class CliOptions
{
    [Option]
    public bool HasPreview { get; set; }
    [Option]
    public string? EditCommand { get; set; }
    [Option]
    public string? PreviewCommand { get; set; }
}

class Program
{
    [DynamicDependency(DynamicallyAccessedMemberTypes.All, typeof(CliOptions))]
    static void Main(string[] args)
    {
        Parser.Default.ParseArguments<CliOptions>(args)
            .WithParsed(o =>
            {
                if (Console.IsInputRedirected)
                {
                    try
                    {
                        int nextChar = Console.In.Peek();
                        if (nextChar != -1)
                        {
                            BuildStdInApp(o.HasPreview, o.EditCommand, o.PreviewCommand).Start((app, _) => Run(app), args);
                        }
                    }
                    catch (Exception e)
                    {
                    }
                }
                else
                {
                    BuildCommandApp("fd . /media").Start((app, _) => Run(app), args);
                }
            });
    }
    
    private static AppBuilder BuildStdInApp(bool hasPreview, string? editCommand, string? previewCommand) 
        => AppBuilder.Configure(() =>
        {
            var viewModel = new MainViewModel();
            viewModel.GlobalKeyBindings.Add((KeyModifiers.Control, Key.P), (_, vm) => {
                vm.TogglePreview();
                return Task.CompletedTask;
            });
            viewModel.GlobalKeyBindings.Add((KeyModifiers.Control, Key.C), ClipboardHelper.CopyStringToClipboard);
            var command = new StdInMenuDefinitionProvider(viewModel, hasPreview, editCommand, previewCommand);
            var app = new App(viewModel, command);
            return app;
        }).UsePlatformDetect().With(new X11PlatformOptions
        {
            WmClass = "netfuzzymenu"
        });
    
    private static AppBuilder BuildCommandApp(string command)
        => AppBuilder.Configure(() =>
        {
            var viewModel = new MainViewModel();
            viewModel.GlobalKeyBindings.Add((KeyModifiers.Control, Key.P), (_, vm) => {
                vm.TogglePreview();
                return Task.CompletedTask;
            });
            viewModel.GlobalKeyBindings.Add((KeyModifiers.Control, Key.C), ClipboardHelper.CopyStringToClipboard);
            var definitionProvider = new RunCommandMenuDefinitionProvider(command, viewModel);
            var app = new App(viewModel, definitionProvider);
            return app;
        }).UsePlatformDetect().With(new X11PlatformOptions
        {
            WmClass = "netfuzzymenu"
        });
    
    private static void Run(Application app)
    {
        app.Run(CancellationToken.None);
    }
}