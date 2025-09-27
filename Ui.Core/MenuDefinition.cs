using System.Threading.Channels;
using nfzf;

namespace nfm.Ui.Core;

public class MenuDefinition
{
    private readonly IComparer<Entry>? _comparer;
    private readonly IComparer<Entry>? _finalComparer;
    public int MinScore { get; init; } = 0;
    public required Func<ChannelWriter<object>, CancellationToken, Task>? AsyncFunction { get; init; }
    public required IResultHandler ResultHandler { get; init; }
    public Dictionary<(ModifierKeys, int), Func<object, Task>> KeyBindings { get; } = new();
    public required Func<object, Pattern, Slab, (int, int)> ScoreFunc { get; init; }
    public Func<object, string, bool>? PreFilter { get; init; }
    public Action<string, List<Entry>>? PostProcess { get; set; }
    public string? Header { get; init; } = null;
    public bool QuitOnEscape { get; init; } = false;
    public bool HasPreview { get; init; } = false;
    public char? PreviewDelimiter { get; set; }
    public string? PreviewStartLineCommand { get; set; }
    public bool ShowGap { get; set; } = false;
    public bool Wrap { get; set; } = false;

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
    public string SearchString { get; init; } = string.Empty;
    public IPreviewHandler? PreviewHandler { get; init; } = null;
    public Func<object, string, Task<Result>>? EditAction { get; init; } = null;
    public Func<string, string>? PreParseFunc { get; init; } = null;
    public Func<IncompleteFilterInfo, List<string>>? AutoCompleteProvider { get; init; } = null;
}
