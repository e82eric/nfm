using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading.Channels;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Input;
using Avalonia.Markup.Xaml;
using nfzf.FileSystem;
using nfzf.ListProcesses;

namespace nfm.menu;

public class TestResultHandler : IResultHandler
{
    private readonly MainViewModel _viewModel;
    private readonly StreamingWin32DriveScanner _fileScanner;

    public TestResultHandler(MainViewModel viewModel)
    {
        _viewModel = viewModel;
        _fileScanner = new StreamingWin32DriveScanner();
    }
    
    public async Task HandleAsync(string output)
    {
        var window = new MainWindow(_viewModel);

        var definition = new MenuDefinition
        {
            AsyncFunction = writer => _fileScanner.StartScanAsync(output, writer),
            Header = null,
            KeyBindings = new Dictionary<(KeyModifiers, Key), Action<string>>(),
            MinScore = 50,
            ResultHandler = new ProcessRunResultHandler(),
            ShowHeader = false
        };

        window.Show();
        await _viewModel.RunDefinitionAsync(definition);
    }
}

public class MenuDefinition
{
    public int MinScore { get; set; }
    public Func<ChannelWriter<string>, Task>? AsyncFunction { get; set; }
    public IResultHandler ResultHandler { get; set; }
    public Dictionary<(KeyModifiers, Key), Action<string>> KeyBindings { get; set; }
    public bool ShowHeader { get; set; }
    public string? Header { get; set; }
}

public class App : Application
{
    private readonly string _command;
    private readonly MainViewModel _viewModel;
    private readonly StreamingWin32DriveScanner _fileScanner = new StreamingWin32DriveScanner();

    public App(string command, MainViewModel viewModel)
    {
        _command = command;
        _viewModel = viewModel;
    }
    
    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    private static async Task ListDrives(ChannelWriter<string> writer)
    {
        DriveInfo[] allDrives = DriveInfo.GetDrives();
        foreach (var driveInfo in allDrives)
        {
            await writer.WriteAsync(driveInfo.Name);
        }
        writer.Complete();
    }

    private async Task RunCommand(string command, ChannelWriter<string> writer)
    {
        using (var process = new Process())
        {
            process.StartInfo.FileName = "cmd.exe";
            process.StartInfo.Arguments = $"/C {command}";
            process.StartInfo.RedirectStandardOutput = true;
            process.StartInfo.UseShellExecute = false;
            process.StartInfo.CreateNoWindow = true;

            process.Start();

            using (var stream = process.StandardOutput.BaseStream)
            {
                using (var reader = new StreamReader(stream))
                {
                    string? line;
                    while ((line = reader.ReadLine()) != null)
                    {
                        await writer.WriteAsync(line);
                    }
                }
            }
            await process.WaitForExitAsync();
            writer.Complete();
        }
    }

    public async Task Show()
    {
        var commands = new []{ @"c:\users\eric\AppData\Roaming\Microsoft\Windows\Start Menu",
            @"C:\ProgramData\Microsoft\Windows\Start Menu",
            @"c:\users\eric\AppData\Local\Microsoft\WindowsApps",
            @"c:\users\eric\utilities",
            @"C:\Program Files\sysinternals\"};
        var window  = new MainWindow(_viewModel);
        var definition = new MenuDefinition
        {
            AsyncFunction = writer => _fileScanner.StartScanMultiAsync(commands, writer),
            Header = null,
            KeyBindings = new Dictionary<(KeyModifiers, Key), Action<string>>(),
            MinScore = 0,
            ResultHandler = new ProcessRunResultHandler(),
            ShowHeader = false
        };
        await _viewModel.RunDefinitionAsync(definition);
        window.Show();
    }
    
    public async Task ShowListWindows()
    {
        var window  = new MainWindow(_viewModel);
        var definition = new MenuDefinition
        {
            AsyncFunction = ListWindows.Run,
            Header = null,
            KeyBindings = new Dictionary<(KeyModifiers, Key), Action<string>>(),
            ResultHandler = new StdOutResultHandler(),
            MinScore = 0,
            ShowHeader = false
        };
        await _viewModel.RunDefinitionAsync(definition);
        window.Show();
    }

    public async Task ShowProcesses()
    {
        var window  = new MainWindow(_viewModel);
        
        var header = string.Format("{0,-75} {1,8} {2,20} {3,20} {4,10}",
            "Name", "PID", "WorkingSet(kb)", "PrivateBytes(kb)", "CPU(s)");
        var keyBindings = new Dictionary<(KeyModifiers, Key), Action<string>>();
        keyBindings.Add((KeyModifiers.Control, Key.K), ProcessLister.KillProcessById);
        keyBindings.Add((KeyModifiers.Control, Key.C), ClipboardHelper.CopyStringToClipboard);

        var definition = new MenuDefinition
        {
            AsyncFunction = ProcessLister.RunNoSort,
            Header = header,
            KeyBindings = keyBindings,
            MinScore = 0,
            ResultHandler = new StdOutResultHandler(),
            ShowHeader = true
        };
        
        await _viewModel.RunDefinitionAsync(definition);
        window.Show();
    }

    public async Task ShowFiles()
    {
        var window  = new MainWindow(_viewModel);
        var definition = new MenuDefinition
        {
            AsyncFunction = ListDrives,
            Header = null,
            KeyBindings = new Dictionary<(KeyModifiers, Key), Action<string>>(),
            MinScore = 0,
            ResultHandler = new TestResultHandler(_viewModel),
            ShowHeader = false
        };
        await _viewModel.RunDefinitionAsync(definition);
        window.Show();
    }
}
