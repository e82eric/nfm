using System.Collections;
using System.Threading.Channels;
using nfzf;

namespace nfm.Ui.Core;

public class StringArrayColumnMenuDefinitionProvider : IMenuDefinitionProvider
{
    private readonly MenuDefinition _menuDefinition;

    public StringArrayColumnMenuDefinitionProvider(
        Func<string[][]> dataProvider,
        int[] columnIndices,
        string[]? columnHeaders = null,
        IResultHandler? resultHandler = null,
        bool enablePreview = true,
        IMainViewModel? viewModel = null,
        string[]? allColumnHeaders = null,
        string[]? displayColumns = null)
    {
        var data = dataProvider();

        // If display columns are specified by name, resolve them to indices
        var resolvedColumnIndices = columnIndices;
        var resolvedColumnHeaders = columnHeaders;

        if (displayColumns != null && displayColumns.Length > 0 && allColumnHeaders != null)
        {
            var indices = new List<int>();
            var headers = new List<string>();

            foreach (var displayColumn in displayColumns)
            {
                var index = Array.FindIndex(allColumnHeaders, h =>
                    string.Equals(h, displayColumn, StringComparison.OrdinalIgnoreCase));
                if (index >= 0)
                {
                    indices.Add(index);
                    headers.Add(allColumnHeaders[index]);
                }
            }

            if (indices.Count > 0)
            {
                resolvedColumnIndices = indices.ToArray();
                resolvedColumnHeaders = headers.ToArray();
            }
        }

        var maxWidths = CalculateColumnWidths(data, resolvedColumnIndices, resolvedColumnHeaders);

        // Create header string from resolved column headers if provided
        var headerString = resolvedColumnHeaders != null && resolvedColumnHeaders.Length > 0
            ? CreateHeaderString(resolvedColumnHeaders, maxWidths)
            : null;

        _menuDefinition = new MenuDefinition
        {
            AsyncFunction = async (writer, cancellationToken) =>
            {
                await Task.Run(() =>
                {
                    try
                    {
                        foreach (var row in data)
                        {
                            if (cancellationToken.IsCancellationRequested) break;

                            var arrayRow = new StringArrayRow(row, resolvedColumnIndices, maxWidths, resolvedColumnHeaders, allColumnHeaders);
                            writer.WriteAsync(arrayRow, cancellationToken);
                        }
                    }
                    catch (OperationCanceledException)
                    {
                        // Expected when cancellation is requested
                    }
                    finally
                    {
                        writer.Complete();
                    }
                });
            },
            Header = headerString,
            HasPreview = enablePreview,
            PreviewHandler = enablePreview ? new StringArrayPreviewHandler() : null,
            ResultHandler = resultHandler ?? new StdOutResultHandler(viewModel ?? throw new ArgumentNullException(nameof(viewModel))),
            MinScore = 0,
            QuitOnEscape = true,
            ScoreFunc = (sObj, pattern, slab) =>
            {
                if (sObj is StringArrayRow row)
                {
                    var searchText = row.GetSearchableText();
                    var score = FuzzySearcher.GetScore(searchText, pattern, slab);
                    return (searchText.Length, score);
                }
                return (0, 0);
            },
            ShowGap = false,
            Wrap = false,
            SearchString = string.Empty,
        };
    }

    public MenuDefinition Get() => _menuDefinition;

    private static Dictionary<int, int> CalculateColumnWidths(string[][] data, int[] columnIndices, string[]? columnHeaders)
    {
        var maxWidths = new Dictionary<int, int>();

        // Initialize with header widths if provided
        if (columnHeaders != null)
        {
            for (int i = 0; i < Math.Min(columnIndices.Length, columnHeaders.Length); i++)
            {
                maxWidths[i] = columnHeaders[i].Length;
            }
        }

        // Calculate max width for each column based on data
        foreach (var row in data)
        {
            for (int i = 0; i < columnIndices.Length; i++)
            {
                var columnIndex = columnIndices[i];
                if (columnIndex < row.Length)
                {
                    var value = row[columnIndex] ?? string.Empty;
                    var currentMax = maxWidths.GetValueOrDefault(i, 0);
                    maxWidths[i] = Math.Max(currentMax, value.Length);
                }
            }
        }

        return maxWidths;
    }

    private static string CreateHeaderString(string[] columnHeaders, Dictionary<int, int> maxWidths)
    {
        var parts = new List<string>();

        for (int i = 0; i < columnHeaders.Length; i++)
        {
            var header = columnHeaders[i] ?? string.Empty;
            var width = maxWidths.GetValueOrDefault(i, 0);

            if (i == columnHeaders.Length - 1)
            {
                // Last column - don't pad
                parts.Add(header);
            }
            else
            {
                // Pad other columns
                parts.Add(header.PadRight(width));
            }
        }

        return string.Join("  ", parts);
    }
}

public class StringArrayRow
{
    private readonly string[] _data;
    private readonly int[] _columnIndices;
    private readonly Dictionary<int, int> _columnWidths;
    private readonly string[]? _columnHeaders;
    private readonly string[]? _allColumnHeaders;

    public StringArrayRow(string[] data, int[] columnIndices, Dictionary<int, int> columnWidths, string[]? columnHeaders, string[]? allColumnHeaders = null)
    {
        _data = data;
        _columnIndices = columnIndices;
        _columnWidths = columnWidths;
        _columnHeaders = columnHeaders;
        _allColumnHeaders = allColumnHeaders;
    }

    public string GetSearchableText()
    {
        var searchableText = new List<string>();
        for (int i = 0; i < _columnIndices.Length; i++)
        {
            var columnIndex = _columnIndices[i];
            if (columnIndex < _data.Length)
            {
                searchableText.Add(_data[columnIndex] ?? string.Empty);
            }
        }
        return string.Join(" ", searchableText);
    }

    public override string ToString()
    {
        var parts = new List<string>();

        for (int i = 0; i < _columnIndices.Length; i++)
        {
            var columnIndex = _columnIndices[i];
            var value = columnIndex < _data.Length ? (_data[columnIndex] ?? string.Empty) : string.Empty;
            var width = _columnWidths.GetValueOrDefault(i, 0);

            if (i == _columnIndices.Length - 1)
            {
                // Last column - don't pad
                parts.Add(value);
            }
            else
            {
                // Pad other columns
                parts.Add(value.PadRight(width));
            }
        }

        return string.Join("  ", parts);
    }

    public string[] GetAllData() => _data;

    public string[]? GetColumnHeaders() => _columnHeaders;

    public string[]? GetAllColumnHeaders() => _allColumnHeaders;
}

public class StringArrayPreviewHandler : IPreviewHandler
{
    public Task Handle(IPreviewRenderer renderer, object item, int height, CancellationToken ct)
    {
        if (item is StringArrayRow row)
        {
            var data = row.GetAllData();
            var allHeaders = row.GetAllColumnHeaders();
            var lines = new List<List<TextSegment>>();

            for (int i = 0; i < data.Length; i++)
            {
                // Use actual header name if available, otherwise use generic "Column N"
                var label = (allHeaders != null && i < allHeaders.Length && !string.IsNullOrEmpty(allHeaders[i]))
                    ? allHeaders[i]
                    : $"Column {i + 1}";

                var value = data[i] ?? string.Empty;
                var lineText = $"{label}: {value}";

                var textSegments = TerminalEscapeCodeConverter.Convert(lineText);
                lines.Add(textSegments);
            }

            renderer.RenderText(lines, 0);
        }
        else
        {
            renderer.RenderError("No preview available");
        }

        return Task.CompletedTask;
    }
}