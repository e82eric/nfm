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
    private readonly IComparer<Entry>? _comparer;
    private readonly IComparer<Entry>? _finalComparer;
    public int MinScore { get; init; } = 0;
    public required Func<ChannelWriter<object>, CancellationToken, Task>? AsyncFunction { get; init; }
    public required IResultHandler ResultHandler { get; init; }
    public Dictionary<(KeyModifiers, Key), Func<object, Task>> KeyBindings { get; } = new();
    public required Func<object, Pattern, Slab, (int, int)> ScoreFunc { get; init; }
    public string? Header { get; init; } = null;
    public bool QuitOnEscape { get; init; } = false;
    public bool HasPreview { get; init; } = false;

    public IComparer<Entry>? Comparer
    {
        get
        {
            if (_comparer == null)
            {
                return Comparers.ScoreLengthAndValue;
            }
            return _comparer;
        }
        init => _comparer = value;
    }

    public IComparer<Entry>? FinalComparer
    {
        get
        {
            if (_finalComparer == null)
            {
                return Comparer;
            }

            return _finalComparer;
        }
        init => _finalComparer = value;
    }
    public Action? OnClosed { get; init; } = null;
    public string? SearchString { get; init; } = null;
    public IPreviewHandler? PreviewHandler { get; init; } = null;
    public Func<object, string, Task<Result>>? EditAction { get; init; } = null;
}
