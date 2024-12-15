using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using Avalonia.Input;
using nfzf;

namespace nfm.menu;

public class MenuDefinition
{
    public int MinScore { get; init; }
    public Func<IEnumerable<object>>? ItemsFunction { get; init; }
    public Func<ChannelWriter<object>, CancellationToken, Task>? AsyncFunction { get; init; }
    public IResultHandler ResultHandler { get; init; }
    public Dictionary<(KeyModifiers, Key), Func<object, Task>> KeyBindings { get; init; }
    public Func<object, Pattern, Slab, (int, int)> ScoreFunc { get; set; }
    public bool ShowHeader { get; set; }
    public string? Header { get; init; }
    public bool QuitOnEscape { get; init; }
    public bool HasPreview { get; init; }
    public IComparer<Entry>? Comparer { get; init; }
    public IComparer<Entry>? FinalComparer { get; init; }
    public Action? OnClosed { get; init; }
    public string? SearchString { get; init; }
    public IPreviewHandler? PreviewHandler { get; set; }
}
