using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using Avalonia.Input;

namespace nfm.menu;

public class MenuDefinition
{
    public int MinScore { get; init; }
    public Func<IEnumerable<string>>? ItemsFunction { get; init; }
    public Func<ChannelWriter<string>, CancellationToken, Task>? AsyncFunction { get; init; }
    public IResultHandler ResultHandler { get; init; }
    public Dictionary<(KeyModifiers, Key), Func<string, Task>> KeyBindings { get; init; }
    public bool ShowHeader { get; set; }
    public string? Header { get; init; }
    public bool QuitOnEscape { get; init; }
    public bool HasPreview { get; init; }
    public IComparer<Entry>? Comparer { get; init; }
    public Action? OnClosed { get; init; }
    public string? Title { get; init; }
}