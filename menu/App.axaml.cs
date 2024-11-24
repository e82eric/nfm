using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Input;
using Avalonia.Markup.Xaml;
using Avalonia.Threading;

namespace nfm.menu;

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
