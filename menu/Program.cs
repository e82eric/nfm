using System;
using System.Diagnostics;
using Avalonia;

namespace nfm.menu;

class Program
{
    [STAThread]
    public static void Main(string[] args)
    {
        if (args.Length > 0 && args[0] == "--debug")
        {
            Debugger.Launch();
        }
        BuildAvaloniaApp() .StartWithClassicDesktopLifetime(args);
    }

    private static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .WithInterFont()
            .LogToTrace();
}