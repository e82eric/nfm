using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using Avalonia.Input;

namespace nfm.menu;

public class MenuDefinition
{
    public int MinScore { get; set; }
    public Func<ChannelWriter<string>, CancellationToken, Task>? AsyncFunction { get; set; }
    public IResultHandler ResultHandler { get; set; }
    public Dictionary<(KeyModifiers, Key), Func<string, Task>> KeyBindings { get; set; }
    public bool ShowHeader { get; set; }
    public string? Header { get; set; }
    public bool QuitOnEscape { get; set; }
    public bool HasPreview { get; set; }
    public IComparer<Entry>? Comparer { get; set; }
}