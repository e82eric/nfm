using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using nfm.Cli;
using nfm.menu;

namespace Cli_Linux;

class Program
{
    static void Main(string[] args)
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
    }
    
    private static AppBuilder BuildStdInApp() 
        => AppBuilder.Configure(() =>
        {
            var viewModel = new MainViewModel();
            viewModel.GlobalKeyBindings.Add((KeyModifiers.Control, Key.C), ClipboardHelper.CopyStringToClipboard);
            var command = new StdInMenuDefinitionProvider(viewModel);
            var app = new App(viewModel, command);
            return app;
        }).UsePlatformDetect();
    
    private static void Run(Application app, bool keyHandler)
    {
        app.Run(CancellationToken.None);
    }
}