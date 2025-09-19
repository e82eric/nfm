using System.Collections;
using System.Threading.Channels;
using System.Text.RegularExpressions;
using nfzf;

namespace nfm.Ui.Core;

public class StringArrayColumnMenuDefinitionProvider : IMenuDefinitionProvider
{
    private readonly MenuDefinition _menuDefinition;
    private readonly Func<string[][]> _dataProvider;

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
        _dataProvider = dataProvider;
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
                // Fallback for when ScoreFuncWithOriginalText is not used
                if (sObj is StringArrayRow row)
                {
                    var normalSearchText = row.GetSearchableText();
                    var normalScore = FuzzySearcher.GetScore(normalSearchText, pattern, slab);
                    return (normalSearchText.Length, normalScore);
                }
                return (0, 0);
            },
            ScoreFuncWithOriginalText = (sObj, pattern, slab, originalSearchText) =>
            {
                if (sObj is StringArrayRow row)
                {
                    var columnFilters = ColumnFilterParser.ParseColumnFilters(originalSearchText);

                    if (columnFilters.Count > 0)
                    {
                        var allHeaders = row.GetAllColumnHeaders();
                        if (allHeaders != null)
                        {
                            var allData = row.GetAllData();
                            bool allFiltersMatch = true;

                            foreach (var filter in columnFilters)
                            {
                                var columnIndex = Array.FindIndex(allHeaders, h =>
                                    string.Equals(h, filter.ColumnName, StringComparison.OrdinalIgnoreCase));

                                if (columnIndex >= 0 && columnIndex < allData.Length)
                                {
                                    var cellValue = allData[columnIndex] ?? string.Empty;
                                    bool filterMatches = filter.Operator switch
                                    {
                                        "=" => string.Equals(cellValue, filter.Value, StringComparison.OrdinalIgnoreCase),
                                        "==" => string.Equals(cellValue, filter.Value, StringComparison.OrdinalIgnoreCase),
                                        "!=" => !string.Equals(cellValue, filter.Value, StringComparison.OrdinalIgnoreCase),
                                        "=~" => IsRegexMatch(cellValue, filter.Value),
                                        "!~" => !IsRegexMatch(cellValue, filter.Value),
                                        _ => string.Equals(cellValue, filter.Value, StringComparison.OrdinalIgnoreCase) // fallback to default
                                    };

                                    if (!filterMatches)
                                    {
                                        allFiltersMatch = false;
                                        break;
                                    }
                                }
                                else
                                {
                                    allFiltersMatch = false;
                                    break;
                                }
                            }

                            if (!allFiltersMatch)
                            {
                                // Column filters don't match, exclude this row
                                return (0, -1);
                            }

                            // Column filters match, now do fuzzy search with processed pattern
                            var fuzzySearchText = row.GetSearchableText();
                            var fuzzyScore = FuzzySearcher.GetScore(fuzzySearchText, pattern, slab);

                            // Boost score since column filters matched
                            return (fuzzySearchText.Length, fuzzyScore > 0 ? fuzzyScore + 500 : 500);
                        }
                    }

                    // Fall back to normal fuzzy search (no column filters)
                    var normalSearchText = row.GetSearchableText();
                    var normalScore = FuzzySearcher.GetScore(normalSearchText, pattern, slab);
                    return (normalSearchText.Length, normalScore);
                }
                return (0, 0);
            },
            ShowGap = false,
            Wrap = false,
            SearchString = string.Empty,
            PreParseFunc = ColumnFilterParser.PreParseSearchString,
            AutoCompleteProvider = GetColumnAutoComplete(allColumnHeaders),
        };
    }

    public MenuDefinition Get() => _menuDefinition;

    private static bool IsRegexMatch(string text, string pattern)
    {
        try
        {
            return Regex.IsMatch(text, pattern, RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        }
        catch (ArgumentException)
        {
            // Invalid regex pattern, fall back to literal string comparison
            return string.Equals(text, pattern, StringComparison.OrdinalIgnoreCase);
        }
    }

    private Func<string, int, List<string>> GetColumnAutoComplete(string[]? columnHeaders)
    {
        return (searchText, cursorPosition) =>
        {
            if (columnHeaders == null || columnHeaders.Length == 0)
                return new List<string>();

            // Find if we're in a column filter context at the cursor position
            var beforeCursor = searchText.Substring(0, Math.Min(cursorPosition, searchText.Length));

            // Look for the last occurrence of /: before the cursor
            var lastColonSlashIndex = beforeCursor.LastIndexOf("/:");
            if (lastColonSlashIndex == -1)
                return new List<string>();

            // Extract the partial column name after /:
            var afterColonSlash = beforeCursor.Substring(lastColonSlashIndex + 2);

            // Check if there's an operator in the current filter
            var operatorMatch = Regex.Match(afterColonSlash, @"^(\w+)(==|!=|!~|=~|=)(.*)$");

            if (operatorMatch.Success)
            {
                // We're after an operator, suggest column values
                var columnName = operatorMatch.Groups[1].Value;
                var @operator = operatorMatch.Groups[2].Value;
                var partialValue = operatorMatch.Groups[3].Value;

                // Find the column index
                var columnIndex = Array.FindIndex(columnHeaders, h =>
                    string.Equals(h, columnName, StringComparison.OrdinalIgnoreCase));

                if (columnIndex >= 0)
                {
                    // Get all unique values from this column in the dataset
                    var data = _dataProvider();
                    var uniqueValues = data
                        .Where(row => columnIndex < row.Length && !string.IsNullOrEmpty(row[columnIndex]))
                        .Select(row => row[columnIndex])
                        .Distinct(StringComparer.OrdinalIgnoreCase)
                        .Where(value => value.StartsWith(partialValue, StringComparison.OrdinalIgnoreCase))
                        .OrderBy(value => value)
                        .ToList();

                    return uniqueValues;
                }

                return new List<string>();
            }
            else
            {
                // We're still typing the column name, suggest column headers
                var partialName = afterColonSlash.Trim();
                var matchingColumns = columnHeaders
                    .Where(header => !string.IsNullOrEmpty(header) &&
                                   header.StartsWith(partialName, StringComparison.OrdinalIgnoreCase))
                    .ToList();

                return matchingColumns;
            }
        };
    }

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