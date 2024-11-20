using System;
using System.Collections.Generic;
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
    private StreamingWin32DriveScanner _fileScanner;

    public TestResultHandler(MainViewModel viewModel)
    {
        _viewModel = viewModel;
        _fileScanner = new StreamingWin32DriveScanner();
    }

    public void Handle(string output)
    {
        var window = new MainWindow(_viewModel);

        var definition = new MenuDefinition
        {
            Command = null,
            Function = null,
            AsyncFunction = () => _fileScanner.ScanAsync(output),
            Header = null,
            KeyBindings = new Dictionary<(KeyModifiers, Key), Action<string>>(),
            MinScore = 50,
            ResultHandler = new ProcessRunResultHandler(),
            ShowHeader = false
        };

        window.Show();
        _viewModel.RunDefinition(definition);
    }
    
    public async Task HandleAsync(string output)
    {
        var window = new MainWindow(_viewModel);

        var definition = new MenuDefinition
        {
            Command = null,
            Function = null,
            AsyncFunction = () => _fileScanner.ScanAsync(output),
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
    public string? Command { get; set; }
    public Func<IEnumerable<string>>? Function { get; set; }
    public Func<ChannelReader<string>>? AsyncFunction { get; set; }
    public IResultHandler ResultHandler { get; set; }
    public Dictionary<(KeyModifiers, Key), Action<string>> KeyBindings { get; set; }
    public bool ShowHeader { get; set; }
    public string? Header { get; set; }
}

public class App : Application
{
    private readonly string _command;
    private readonly MainViewModel _viewModel;

    public App(string command, MainViewModel viewModel)
    {
        _command = command;
        _viewModel = viewModel;
    }
    
    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    private static IEnumerable<string> ListDrives()
    {
        DriveInfo[] allDrives = DriveInfo.GetDrives();
        foreach (var driveInfo in allDrives)
        {
            yield return driveInfo.Name;
        }
    }

    public void Show()
    {
        var window  = new MainWindow(_viewModel);
        var definition = new MenuDefinition()
        {
            Command = _command,
            Function = null,
            Header = null,
            KeyBindings = new Dictionary<(KeyModifiers, Key), Action<string>>(),
            MinScore = 0,
            ResultHandler = new ProcessRunResultHandler(),
            ShowHeader = false
        };
        _viewModel.RunDefinition(definition);
        window.Show();
    }
    
    public void ShowListWindows()
    {
        var window  = new MainWindow(_viewModel);
        var definition = new MenuDefinition()
        {
            Command = null,
            Function = ListWindows.Run,
            Header = null,
            KeyBindings = new Dictionary<(KeyModifiers, Key), Action<string>>(),
            ResultHandler = new StdOutResultHandler(),
            MinScore = 0,
            ShowHeader = false
        };
        _viewModel.RunDefinition(definition);
        window.Show();
    }

    public void ShowProcesses()
    {
        var window  = new MainWindow(_viewModel);
        
        string header = string.Format("{0,-75} {1,8} {2,20} {3,20} {4,10}",
            "Name", "PID", "WorkingSet(kb)", "PrivateBytes(kb)", "CPU(s)");
        var keyBindings = new Dictionary<(KeyModifiers, Key), Action<string>>();
        keyBindings.Add((KeyModifiers.Control, Key.K), ProcessLister2.KillProcessById);
        keyBindings.Add((KeyModifiers.Control, Key.C), ClipboardHelper.CopyStringToClipboard);

        var definition = new MenuDefinition
        {
            Command = null,
            Function = ProcessLister2.RunNoSort,
            Header = header,
            KeyBindings = keyBindings,
            MinScore = 0,
            ResultHandler = new StdOutResultHandler(),
            ShowHeader = true
        };
        
        _viewModel.RunDefinition(definition);
        window.Show();
    }

    public void ShowFiles()
    {
        var window  = new MainWindow(_viewModel);
        var definition = new MenuDefinition()
        {
            //Command = "pwsh -Command \"(Get-PSDrive -PSProvider 'FileSystem').Name\"",
            //Command = "fd . c:\\ -H -t f",
            Function = ListDrives,
            Header = null,
            KeyBindings = new Dictionary<(KeyModifiers, Key), Action<string>>(),
            MinScore = 0,
            ResultHandler = new TestResultHandler(_viewModel),
            ShowHeader = false
        };
        _viewModel.RunDefinition(definition);
        window.Show();
    }
}
