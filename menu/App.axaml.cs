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

public interface IMenuDefinitionProvider
{
    MenuDefinition Get();
}

public class ShowProcessesMenuDefinitionProvider : IMenuDefinitionProvider
{
    public MenuDefinition Get()
    {
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
        return definition;
    }
}

public class StdInMenuDefinitionProvider : IMenuDefinitionProvider
{
    public MenuDefinition Get()
    {
        var definition = new MenuDefinition
        {
            AsyncFunction = Run,
            Header = null,
            KeyBindings = new Dictionary<(KeyModifiers, Key), Action<string>>(),
            ResultHandler = new StdOutResultHandler(),
            MinScore = 0,
            ShowHeader = false,
            QuitOnEscape = true
        };
        return definition;
    }

    private static async Task Run(ChannelWriter<string> writer, CancellationToken cancellationToken)
    {
        await Task.Run(async () =>
        {
            try
            {
                using (var reader = Console.In)
                {
                    while (!cancellationToken.IsCancellationRequested)
                    {
                        var lineTask = reader.ReadLineAsync();

                        var completedTask =
                            await Task.WhenAny(lineTask, Task.Delay(Timeout.Infinite, cancellationToken));

                        if (completedTask == lineTask)
                        {
                            string line = await lineTask;
                            if (line == null)
                            {
                                break;
                            }

                            await writer.WriteAsync(line, cancellationToken);
                        }
                        else
                        {
                            break;
                        }
                    }
                }
            }
            catch (OperationCanceledException)
            {
            }
            finally
            {
                writer.Complete();
            }
        });
    }
}

public class ShowWindowsMenuDefinitionProvider : IMenuDefinitionProvider
{
    public MenuDefinition Get()
    {
        var definition = new MenuDefinition
        {
            AsyncFunction = ListWindows.Run,
            Header = null,
            KeyBindings = new Dictionary<(KeyModifiers, Key), Action<string>>(),
            ResultHandler = new StdOutResultHandler(),
            MinScore = 0,
            ShowHeader = false
        };
        return definition;
    }
}

public class FileSystemMenuDefinitionProvider(
    TestResultHandler resultHandler,
    int maxDepth,
    IEnumerable<string>? rootDirectory,
    bool quitOnEscape,
    bool hasPreview) : IMenuDefinitionProvider
{
    public MenuDefinition Get()
    {
        var definition = new MenuDefinition
        {
            AsyncFunction = rootDirectory == null || !rootDirectory.Any() ?
                (writer, ct) => ListDrives(maxDepth, writer, ct) :
                (writer, ct) => ListDrives(rootDirectory, maxDepth, writer, ct),
            Header = null,
            KeyBindings = new Dictionary<(KeyModifiers, Key), Action<string>>(),
            MinScore = 0,
            ResultHandler = resultHandler,
            ShowHeader = false,
            QuitOnEscape = quitOnEscape,
            HasPreview = hasPreview
        };
        return definition;
    }
    
    private async Task ListDrives(int maxDepth, ChannelWriter<string> writer, CancellationToken cancellationToken)
    {
        DriveInfo[] allDrives = DriveInfo.GetDrives();
        var drives = allDrives.Select(d => d.Name);
        await ListDrives(drives, maxDepth, writer, cancellationToken);
    }
    
    private async Task ListDrives(IEnumerable<string> rootDirectories, int maxDepth, ChannelWriter<string> writer, CancellationToken cancellationToken)
    {
        var fileScanner = new StreamingWin32DriveScanner2();
        await fileScanner.StartScanForDirectoriesAsync(rootDirectories, writer, maxDepth, cancellationToken);
    }
}

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
    public bool HasPreview { get; set; }
}

public class App : Application
{
    private readonly MainViewModel _viewModel;
    private readonly IMenuDefinitionProvider? _definitionProvider;

    public App(MainViewModel viewModel, IMenuDefinitionProvider definitionProvider)
    {
        _viewModel = viewModel;
        _definitionProvider = definitionProvider;
    }

    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
        if (_definitionProvider != null)
        {
            Dispatcher.UIThread.InvokeAsync(async () =>
            {
                var definition = _definitionProvider.Get();
                var window = new MainWindow(_viewModel);
                await _viewModel.RunDefinitionAsync(definition);
                window.Show();
            });
        }
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
}
