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
                            BuildStdInApp(o.HasPreview).Start((app, _) => Run(app), args);
                        }
                    }
                    catch (Exception e)
                    {
                    }
                }
            });
    }
    
    private static AppBuilder BuildStdInApp(bool hasPreview) 
        => AppBuilder.Configure(() =>
        {
            var viewModel = new MainViewModel();
            viewModel.GlobalKeyBindings.Add((KeyModifiers.Control, Key.P), (_, vm) => {
                vm.TogglePreview();
                return Task.CompletedTask;
            });
            viewModel.GlobalKeyBindings.Add((KeyModifiers.Control, Key.C), ClipboardHelper.CopyStringToClipboard);
            var command = new StdInMenuDefinitionProvider(viewModel, hasPreview);
            var app = new App(viewModel, command);
            return app;
        }).UsePlatformDetect().With(new X11PlatformOptions()
        {
            WmClass = "netfuzzymenu"
        });
    
    private static void Run(Application app)
    {
        app.Run(CancellationToken.None);
    }
}