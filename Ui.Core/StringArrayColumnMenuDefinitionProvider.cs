using System.Text.RegularExpressions;
using nfzf;

namespace nfm.Ui.Core;


public class StringArrayColumnMenuDefinitionProvider : IMenuDefinitionProvider
{
    class ParseResult(
        List<ColumnFilter> sortFilters,
        IncompleteFilterInfo incompleteFilters,
        List<ColumnFilter> columnFilters)
    {
        public List<ColumnFilter> SortFilters { get; private set; } = sortFilters;
        public IncompleteFilterInfo IncompleteFilters { get; private set; } = incompleteFilters;
        public List<ColumnFilter> ColumnFilters { get; private set; } = columnFilters;
    }
    
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
        string[]? displayColumns = null,
        bool quitOnEscape = true,
        Action? onClosed = null)
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
            OnClosed = onClosed,
            AsyncFunction = async (writer, cancellationToken) =>
            {
                await Task.Run(() =>
                {
                    try
                    {
                        var rows = data.Select(row => new StringArrayRow(row, resolvedColumnIndices, maxWidths, resolvedColumnHeaders, allColumnHeaders)).ToList();
                        foreach (var arrayRow in rows)
                        {
                            if (cancellationToken.IsCancellationRequested)
                            {
                                break;
                            }

                            writer.WriteAsync(arrayRow, cancellationToken);
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
            },
            Header = headerString,
            HasPreview = enablePreview,
            PreviewHandler = enablePreview ? new StringArrayPreviewHandler() : null,
            ResultHandler = resultHandler ?? new StdOutResultHandler(viewModel ?? throw new ArgumentNullException(nameof(viewModel))),
            MinScore = 0,
            QuitOnEscape = quitOnEscape,
            ScoreFunc = (sObj, pattern, slab) =>
            {
                if (sObj is StringArrayRow row)
                {
                    var normalSearchText = row.GetSearchableText();
                    var normalScore = FuzzySearcher.GetScore(normalSearchText, pattern, slab);
                    return (normalSearchText.Length, normalScore);
                }
                return (0, 0);
            },
            PreFilter = (stateObj, sObj, searchString) =>
            {
                if (sObj is StringArrayRow row && stateObj is ParseResult parseResult)
                {
                    var columnFilters = parseResult.ColumnFilters;

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
                                        ">" => CompareValues(cellValue, filter.Value) > 0,
                                        "<" => CompareValues(cellValue, filter.Value) < 0,
                                        ">=" => CompareValues(cellValue, filter.Value) >= 0,
                                        "<=" => CompareValues(cellValue, filter.Value) <= 0,
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
                                return false;
                            }
                        }
                    }
                }
                return true;
            },
            ShowGap = false,
            Wrap = false,
            SearchString = string.Empty,
            PostProcess = (stateObject, items) =>
            {
                var parseResult = stateObject as ParseResult;
                if (parseResult is null || allColumnHeaders is null)
                {
                    return;
                }

                var sortFilters = parseResult.SortFilters;
                if (sortFilters.Count == 0)
                {
                    return;
                }
                
                items.Sort(Comparer<Entry>.Create((entry, entry1) =>
                {
                    var arrayRow1 = entry.Item as StringArrayRow;
                    var arrayRow2 = entry1.Item as StringArrayRow;

                    if (arrayRow1 is null || arrayRow2 is null)
                    {
                        return 0;
                    }

                    // Apply sorts in reverse order so the first sort filter has highest priority
                    for (int i = sortFilters.Count - 1; i >= 0; i--)
                    {
                        var sortFilter = sortFilters[i];
                        var columnIndex = Array.FindIndex(allColumnHeaders, h =>
                            string.Equals(h, sortFilter.Value, StringComparison.OrdinalIgnoreCase));

                        if (columnIndex < 0)
                        {
                            continue;
                        }
                
                        var value1 = arrayRow1.GetAllData()[columnIndex] ?? string.Empty;
                        var value2 = arrayRow2.GetAllData()[columnIndex] ?? string.Empty;

                        var comparison = CompareValues(value1, value2);
                        if (comparison != 0)
                        {
                            var isAscending = string.Equals(sortFilter.ColumnName, "SortAsc", StringComparison.OrdinalIgnoreCase);
                            return isAscending ? comparison : -comparison;
                        }
                    }
                
                    return 0;
                }));
            },
            ParseFunc = searchString =>
            {
                string parsedSearchString = ColumnFilterParser.ParseSearchString(searchString);
                List<ColumnFilter> sortFilters = ColumnFilterParser.ParseSortColumnFilters(searchString);
                IncompleteFilterInfo incompleteFilters = ColumnFilterParser.GetIncompleteFilterInfo(searchString);
                List<ColumnFilter> columnFilters = ColumnFilterParser.ParseColumnFilters(searchString);
                return (parsedSearchString, new ParseResult(sortFilters, incompleteFilters, columnFilters));
            },
            AutoCompleteSuggestionsFunc = state =>
            {
                if (state is ParseResult parseResult)
                {
                    var incompleteFilterInfo = parseResult.IncompleteFilters;
                    if (incompleteFilterInfo.HasIncompleteFilter && incompleteFilterInfo.Type != IncompleteFilterType.Slash)
                    {
                        var menuDefinition = _menuDefinition;
                        if (menuDefinition?.AutoCompleteProvider != null)
                        {
                            var suggestions = menuDefinition.AutoCompleteProvider(incompleteFilterInfo);

                            if (suggestions.Count > 0)
                            {
                                return suggestions;
                            }
                        }
                    }
                }

                return [];
            },
            AutoCompleteProvider = incompleteFilterInfo =>
            {
                if (allColumnHeaders == null || allColumnHeaders.Length == 0)
                {
                    return new List<string>();
                }

                switch (incompleteFilterInfo.Context)
                {
                    case IncompleteFilterContext.Column:
                        var suggestions = new List<string>();

                        suggestions.Add("SortDsc");
                        suggestions.Add("SortAsc");

                        suggestions.AddRange(allColumnHeaders);

                        if (!string.IsNullOrEmpty(incompleteFilterInfo.ColumnPrefix))
                        {
                            return suggestions
                                .Where(suggestion => suggestion.StartsWith(incompleteFilterInfo.ColumnPrefix, StringComparison.OrdinalIgnoreCase))
                                .ToList();
                        }
                        return suggestions;
                    case IncompleteFilterContext.Value:
                        if (string.Equals(incompleteFilterInfo.ColumnPrefix, "SortAsc", StringComparison.OrdinalIgnoreCase) ||
                            string.Equals(incompleteFilterInfo.ColumnPrefix, "SortDsc", StringComparison.OrdinalIgnoreCase))
                        {
                            if (!string.IsNullOrEmpty(incompleteFilterInfo.ValuePrefix))
                            {
                                return allColumnHeaders
                                    .Where(header => header.StartsWith(incompleteFilterInfo.ValuePrefix, StringComparison.OrdinalIgnoreCase))
                                    .ToList();
                            }
                            return allColumnHeaders.ToList();
                        }

                        var data1 = _dataProvider();

                        var columnIndex1 = Array.FindIndex(allColumnHeaders, h =>
                            string.Equals(h, incompleteFilterInfo.ColumnPrefix, StringComparison.OrdinalIgnoreCase));

                        if (columnIndex1 >= 0)
                        {
                            var uniqueValues = data1
                                .Where(row => columnIndex1 < row.Length && !string.IsNullOrEmpty(row[columnIndex1]))
                                .Select(row => row[columnIndex1])
                                .Distinct(StringComparer.OrdinalIgnoreCase)
                                .Where(value => value.StartsWith(incompleteFilterInfo.ValuePrefix,
                                    StringComparison.OrdinalIgnoreCase))
                                .OrderBy(value => value)
                                .ToList();
                            return uniqueValues;
                        }
                        break;
                }

                return new List<string>();
            },
        };

        _menuDefinition.KeyBindings.Add((ModifierKeys.LCtl ,VirtualKeyCodes.VK_RETURN), async _ =>
        {
            if (viewModel != null)
            {
                var currentSearchResults = viewModel.GetAllCurrentSearchResults();

                if (currentSearchResults.Count > 0)
                {
                    var filteredData = new List<string[]>();
                    int maxColumns = 0;

                    foreach (var item in currentSearchResults)
                    {
                        if (item is StringArrayRow arrayRow)
                        {
                            var rowData = arrayRow.GetAllData();
                            filteredData.Add(rowData);
                            maxColumns = Math.Max(maxColumns, rowData.Length);
                        }
                    }

                    if (filteredData.Count > 0)
                    {
                        var csvHeaders = new List<string>();

                        // Use all column headers if available
                        if (allColumnHeaders != null && allColumnHeaders.Length > 0)
                        {
                            csvHeaders.AddRange(allColumnHeaders.Take(maxColumns).Select(EscapeCsvField));
                            // Add generic headers for any extra columns
                            for (int i = allColumnHeaders.Length; i < maxColumns; i++)
                            {
                                csvHeaders.Add(EscapeCsvField($"Column{i + 1}"));
                            }
                        }
                        else
                        {
                            // Generate generic column headers
                            for (int i = 0; i < maxColumns; i++)
                            {
                                csvHeaders.Add(EscapeCsvField($"Column{i + 1}"));
                            }
                        }

                        // Output CSV header
                        Console.WriteLine(string.Join(",", csvHeaders));

                        // Output filtered data rows as CSV with all columns
                        foreach (var row in filteredData)
                        {
                            var csvRow = new List<string>();
                            for (int i = 0; i < maxColumns; i++)
                            {
                                var value = i < row.Length ? (row[i] ?? string.Empty) : string.Empty;
                                csvRow.Add(EscapeCsvField(value));
                            }
                            Console.WriteLine(string.Join(",", csvRow));
                        }
                    }
                }

                await viewModel.Close(true);
            }
        });
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

    private static int CompareValues(string value1, string value2)
    {
        // Try numeric comparison first
        if (double.TryParse(value1, out var num1) && double.TryParse(value2, out var num2))
        {
            return num1.CompareTo(num2);
        }

        // Try DateTime comparison
        if (DateTime.TryParse(value1, out var date1) && DateTime.TryParse(value2, out var date2))
        {
            return date1.CompareTo(date2);
        }

        // Fall back to string comparison
        return string.Compare(value1, value2, StringComparison.OrdinalIgnoreCase);
    }

    private static string EscapeCsvField(string field)
    {
        if (string.IsNullOrEmpty(field))
        {
            return string.Empty;
        }

        // If the field contains comma, quote, or newline, wrap it in quotes and escape internal quotes
        if (field.Contains(',') || field.Contains('"') || field.Contains('\n') || field.Contains('\r'))
        {
            return $"\"{field.Replace("\"", "\"\"")}\"";
        }

        return field;
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