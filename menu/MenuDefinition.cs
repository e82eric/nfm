using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using Avalonia.Input;
using nfzf;

namespace nfm.menu;

public class MenuDefinition<T>
{
    public int MinScore { get; init; }
    public Func<IEnumerable<T>>? ItemsFunction { get; init; }
    public Func<ChannelWriter<T>, CancellationToken, Task>? AsyncFunction { get; init; }
    public IResultHandler<T> ResultHandler { get; init; }
    public Dictionary<(KeyModifiers, Key), Func<T, Task>> KeyBindings { get; init; }
    public Func<T, Pattern, Slab, (int, int)> ScoreFunc { get; set; }
    public ITtoStrConverter<T> StrConverter { get; set; }
    public bool ShowHeader { get; set; }
    public string? Header { get; init; }
    public bool QuitOnEscape { get; init; }
    public bool HasPreview { get; init; }
    public IComparer<Entry<T>>? Comparer { get; init; }
    public IComparer<Entry<T>>? FinalComparer { get; init; }
    public Action? OnClosed { get; init; }
    public string? SearchString { get; init; }
    public IPreviewHandler<T>? PreviewHandler { get; set; }
}