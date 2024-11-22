using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Input;
using Avalonia.Markup.Xaml;
using Avalonia.Threading;
using nfzf.FileSystem;
using nfzf.ListProcesses;

namespace nfm.menu;

public class TestResultHandler : IResultHandler
{
    private readonly MainViewModel _viewModel;
    private readonly StreamingWin32DriveScanner2 _fileScanner;
    private bool _quitAfter;
    private bool _quitOnEscape;
    private bool _writeToStdOut;
    private bool _searchDirectories;

    public TestResultHandler(MainViewModel viewModel, bool quitAfter, bool quitOnEscape, bool writeToStdOut, bool searchDirectories)
    {
        _searchDirectories = searchDirectories;
        _writeToStdOut = writeToStdOut;
        _quitOnEscape = quitOnEscape;
        _quitAfter = quitAfter;
        _viewModel = viewModel;
        _fileScanner = new StreamingWin32DriveScanner2();
    }

    private static bool IsDirectory(string path)
    {
        FileAttributes attributes = File.GetAttributes(path);
        return attributes.HasFlag(FileAttributes.Directory);
    }
    
    public async Task HandleAsync(string output)
    {
        if (!IsDirectory(output) || !_searchDirectories)
        {
            if (_writeToStdOut)
            {
                Console.WriteLine(output);
            }
            else
            {
                ProcessStartInfo startInfo = new ProcessStartInfo
                {
                    FileName = output,
                    UseShellExecute = true
                };
                Process.Start(startInfo);
            }

            if (_quitAfter)
            {
                Environment.Exit(0);
            }
        }
        else
        {
            var window = new MainWindow(_viewModel);
            var definition = new MenuDefinition
            {
                AsyncFunction = (writer, ct) => _fileScanner.StartScanForDirectoriesAsync(
                    [output],
                    writer,
                    Int32.MaxValue,
                    ct),
                Header = null,
                KeyBindings = new Dictionary<(KeyModifiers, Key), Action<string>>(),
                MinScore = 0,
                ResultHandler = this,
                ShowHeader = false,
                QuitOnEscape = _quitOnEscape
            };

            window.Show();
            await _viewModel.RunDefinitionAsync(definition);
        }
    }
}

public class MenuDefinition
{
    public int MinScore { get; set; }
    public Func<ChannelWriter<string>, CancellationToken, Task>? AsyncFunction { get; set; }
    public IResultHandler ResultHandler { get; set; }
    public Dictionary<(KeyModifiers, Key), Action<string>> KeyBindings { get; set; }
    public bool ShowHeader { get; set; }
    public string? Header { get; set; }
    public bool QuitOnEscape { get; set; }
}

public class App : Application
{
    private readonly string _command;
    private readonly MainViewModel _viewModel;
    private readonly StreamingWin32DriveScanner2 _fileScanner = new();
    private bool _showFiles;
    private bool _searchDirectories;

    public App(string command, MainViewModel viewModel, bool showFiles, bool searchDirectories)
    {
        _searchDirectories = searchDirectories;
        _showFiles = showFiles;
        _command = command;
        _viewModel = viewModel;
    }
    
    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
        if (_showFiles)
        {
            Dispatcher.UIThread.InvokeAsync(async () =>
            {
                await ShowFiles(true, true, true, _searchDirectories);
            });
        }
    }

    private async Task ListDrives(ChannelWriter<string> writer, CancellationToken cancellationToken)
    {
        var fileScanner = new StreamingWin32DriveScanner2();
        DriveInfo[] allDrives = DriveInfo.GetDrives();
        var drives = allDrives.Select(d => d.Name);
        await fileScanner.StartScanForDirectoriesAsync(drives, writer, 5, cancellationToken);
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
            AsyncFunction = (writer, ct) => _fileScanner.StartScanForDirectoriesAsync(commands, writer, Int32.MaxValue, ct),
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

    public async Task ShowFiles(bool quitAfter = false, bool quitOnEscape = false, bool useStdOut = false, bool searchDirectories = true)
    {
        var window  = new MainWindow(_viewModel);
        var definition = new MenuDefinition
        {
            AsyncFunction = ListDrives,
            Header = null,
            KeyBindings = new Dictionary<(KeyModifiers, Key), Action<string>>(),
            MinScore = 0,
            ResultHandler = new TestResultHandler(_viewModel, quitAfter, quitOnEscape, useStdOut, searchDirectories),
            ShowHeader = false,
            QuitOnEscape = quitOnEscape
        };
        await _viewModel.RunDefinitionAsync(definition);
        window.Show();
    }
}
