using System.Diagnostics;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using nfm.Win32Ui;
using nfzf;

namespace nfm.Ui.Core;

public class DisplayColumnsProvider
{
    private Dictionary<int, int> _resolvedColumnsAndWidths;
    public string[]? AllColumnHeaders { get; }
    private readonly Dictionary<int, int> _maxWidths;

    public DisplayColumnsProvider(int[] columnIndices, string[]? allColumnHeaders, Dictionary<int, int> maxWidths)
    {
        _maxWidths = maxWidths;
        AllColumnHeaders = allColumnHeaders;
        SetResolvedColumnsAndWidths(columnIndices);
    }

    public void SetDisplayColumns(string[] displayColumns)
    {
        if (AllColumnHeaders == null)
        {
            return;
        }

        var indices = new List<int>();
            
        foreach (var displayColumn in displayColumns)
        {
            var index = Array.FindIndex(AllColumnHeaders, h => string.Equals(h, displayColumn, StringComparison.OrdinalIgnoreCase));
            if (index >= 0)
            {
                indices.Add(index);
            }
        }
            
        SetResolvedColumnsAndWidths(indices);
    }
        
    private void SetResolvedColumnsAndWidths(IEnumerable<int> displayIndices)
    {
        var result = new Dictionary<int, int>();

        foreach (var index in displayIndices)
        {
            if (_maxWidths.TryGetValue(index, out var width))
            {
                result.Add(index, width);
            }
            else
            {
                result.Add(index, 99);
            }
        }

        _resolvedColumnsAndWidths = result;
    }

    public Dictionary<int, int> GetDisplayColumnsAndWidths()
    {
        return _resolvedColumnsAndWidths;
    }

    public string GetHeader()
    {
        var displayColumnsAndWidths = GetDisplayColumnsAndWidths();
        var resolvedColumnHeaders = new List<string>();
        var maxWidths = new Dictionary<int, int>();
        var i = 0;
        foreach (var displayColumnsAndWidth in displayColumnsAndWidths)
        {
            resolvedColumnHeaders.Add(AllColumnHeaders[displayColumnsAndWidth.Key]);
            maxWidths.Add(i, displayColumnsAndWidth.Value);
            i++;
        }
        var headerString = resolvedColumnHeaders != null && resolvedColumnHeaders.Count > 0
            ? CreateHeaderString(resolvedColumnHeaders.ToArray(), maxWidths)
            : null;
        return headerString;
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

public class StringArrayColumnMenuDefinitionProvider : IMenuDefinitionProvider
{
    private readonly MenuDefinition _menuDefinition;
    private readonly Func<string[][]> _dataProvider;
    private DisplayColumnsProvider _displayColumnProvider;

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

        var maxWidths = CalculateColumnWidths(data, allColumnHeaders);
        _displayColumnProvider = new DisplayColumnsProvider(resolvedColumnIndices, allColumnHeaders, maxWidths);

        _menuDefinition = new MenuDefinition
        {
            OnClosed = onClosed,
            AsyncFunction = async (writer, cancellationToken) =>
            {
                await Task.Run(() =>
                {
                    try
                    {
                        var rows = data.Select(row => new StringArrayRow(row, _displayColumnProvider)).ToList();
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
            Header = _displayColumnProvider.GetHeader(),
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
                if (sObj is StringArrayRow row && stateObj is (ParseResult parseResult, int))
                {
                    var columnFilters = parseResult.GetFilters();

                    if (columnFilters.Any())
                    {
                        var allHeaders = row.GetAllColumnHeaders();
                        if (allHeaders != null)
                        {
                            var allData = row.GetAllData();
                            bool allFiltersMatch = true;

                            foreach (var filter in columnFilters)
                            {
                                var columnIndex = Array.FindIndex(allHeaders, h =>
                                    string.Equals(h, filter.Column.Val, StringComparison.OrdinalIgnoreCase));

                                if (columnIndex >= 0 && columnIndex < allData.Length)
                                {
                                    var filterValues = filter.Values.Values.Select(v => v.Token.Val);
                                    if (!filterValues.Where(t =>
                                        {
                                            var cellValue = allData[columnIndex] ?? string.Empty;
                                            bool filterMatches = filter.Operator.TokenOperator switch
                                            {
                                                OperatorTokenKind.Equals =>
                                                    string.Equals(cellValue, t, StringComparison.OrdinalIgnoreCase),
                                                OperatorTokenKind.NotEquals =>
                                                    !string.Equals(cellValue, t, StringComparison.OrdinalIgnoreCase),
                                                OperatorTokenKind.Regex =>
                                                    IsRegexMatch(cellValue, t),
                                                OperatorTokenKind.NotEqualsRegex =>
                                                    !IsRegexMatch(cellValue, t),
                                                OperatorTokenKind.GreaterThan =>
                                                    CompareForFilter(cellValue, t) > 0,
                                                OperatorTokenKind.GreaterThanOrEqual =>
                                                    CompareForFilter(cellValue, t) >= 0,
                                                OperatorTokenKind.LessThan =>
                                                    CompareForFilter(cellValue, t) < 0,
                                                OperatorTokenKind.LessThenOrEqual =>
                                                    CompareForFilter(cellValue, t) <= 0,
                                                _ => false
                                            };

                                            return filterMatches;
                                        }).Any())
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
                var parseResult = stateObject as (ParseResult parseResult, int cursorPos)?;
                if (parseResult is null || allColumnHeaders is null)
                {
                    return;
                }

                var sortFilters = parseResult.Value.parseResult.GetSortFilters().ToList();
                if (!sortFilters.Any())
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
                    for (int i = sortFilters.Count() - 1; i >= 0; i--)
                    {
                        var sortFilter = sortFilters[i];
                        var sortFilterValues = sortFilter.Values.Values.Select(t => t.Token.Val);
                        if (sortFilterValues.Any())
                        {
                            var columnIndex = Array.FindIndex(allColumnHeaders, h =>
                                //TODO:: Do something better with Values
                                string.Equals(h, sortFilterValues.First(), StringComparison.OrdinalIgnoreCase));

                            if (columnIndex < 0)
                            {
                                continue;
                            }
                
                            var value1 = arrayRow1.GetAllData()[columnIndex] ?? string.Empty;
                            var value2 = arrayRow2.GetAllData()[columnIndex] ?? string.Empty;

                            var comparison = CompareValues(value1, value2);
                            if (comparison != 0)
                            {
                                var isAscending = sortFilter.Column.Val == "Ascending";
                                return isAscending ? comparison : -comparison;
                            }
                        }
                    }
                
                    return 0;
                }));
            },
            ParseFunc = (searchString, cursorPos) =>
            {
                var parseResult = TokenParser.Parse(searchString);
                var newDisplayColumns = parseResult.GetDisplayColumns();
                
                if (newDisplayColumns != null)
                {
                    _displayColumnProvider.SetDisplayColumns(newDisplayColumns.ToArray());
                    _menuDefinition.Header = _displayColumnProvider.GetHeader();
                    viewModel.UpdateHeader();
                }

                return (parseResult.SearchString, (parseResult, cursorPos));
            },
            AutoCompleteSuggestionsFunc = (state, _) =>
            {
                if (state is (ParseResult parseResult, int cursorPos))
                {
                    var focused = parseResult.GetFocusedToken(cursorPos);
                    if (focused != null)
                    {
                        var suggestions = AutoCompleteProvider(focused.Value.Token, focused.Value.Part, allColumnHeaders);

                        if (suggestions.Count > 0)
                        {
                            return suggestions;
                        }
                    }
                }

                return [];
            },
            ApplySelectedSuggestion = request =>
            {
                if (request.state is (ParseResult parseResult, int cursorPos))
                {
                    var focusedResult = parseResult.GetFocusedToken(cursorPos);
                    if (focusedResult != null)
                    {
                        TextSpan? replacePos = null;
                        int? insertPos = null;
                        var postfix = string.Empty;
                        var value = request.selectedSuggestion;
                        switch (focusedResult.Value.Token)
                        {
                            case OnActionToken:
                                value = request.selectedSuggestion[0].ToString();
                                break;
                            case OnColumnToken columnToken:
                                replacePos = columnToken.Column.TextSpan;
                                insertPos = replacePos.Start;
                                postfix = columnToken.Action.Action == ActionTokenKind.Sort ? "==" : "=";
                                break;
                            case OnOperatorToken operatorToken:
                                replacePos = operatorToken.Operator.TextSpan;
                                insertPos = replacePos.Start;
                                break;
                            case OnValueToken valueToken:
                                postfix = " ";
                                if (valueToken.Action.Action == ActionTokenKind.SelectDisplayColumns)
                                {
                                    postfix = ",";
                                }
                                if (valueToken.Values.Values.Any())
                                {
                                    var last = valueToken.Values.Values.Last();
                                    replacePos = last.Token.TextSpan;
                                }
                                else
                                {
                                    replacePos = null;
                                    insertPos = null;
                                }
                                break;
                        }
                        var sb = new StringBuilder(request.fullSearchString);
                        if (replacePos != null)
                        {
                            sb.Remove(replacePos.Start, replacePos.EndExclusive - replacePos.Start);
                        }

                        if (insertPos == null)
                        {
                            sb.Append(value + postfix);
                        }
                        else
                        {
                            sb.Insert(insertPos.Value, value + postfix);
                        }
                        var newText = sb.ToString();
                        return newText;
                    }
                }
                return request.fullSearchString;
            }
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
    
    List<TerminalEscapedLine> AutoCompleteProvider(BaseToken token, TokenPart part, string[]? allColumnHeaders)
    {
        if (allColumnHeaders == null || allColumnHeaders.Length == 0)
        {
            return new List<TerminalEscapedLine>();
        }

        var suggestions = new List<string>();
        var searchString = string.Empty;

        switch (token)
        {
            case OnActionToken:
                var actions = TokenParser.Actions.Select(a => $"{a.Key}   {a.Value.Description}");
                suggestions.AddRange(actions);
                break;
            case OnColumnToken columnToken:
                if (columnToken.Action.Action == ActionTokenKind.Sort)
                {
                    suggestions.Add("Ascending");
                    suggestions.Add("Descending");
                }
                else
                {
                    searchString = columnToken.Column.Val;
                    suggestions.AddRange(allColumnHeaders);
                }
                break;
            case OnOperatorToken:
                foreach (var op in TokenParser.Operators.Select(o => o.Key))
                {
                    suggestions.Add(op);
                }
                break;
            case OnValueToken valuesToken:
                searchString = valuesToken.Values.GetFocusedValuePrefix();
                switch (valuesToken.Action.Action)
                {
                    case ActionTokenKind.Sort:
                        suggestions.AddRange(allColumnHeaders);
                        break;
                    case ActionTokenKind.SelectDisplayColumns:
                        var values = valuesToken.Values.Values.Select(v => v.Token.Val);
                        var notAddedColumns = allColumnHeaders.Where(h => !values.Contains(h));
                        suggestions.AddRange(notAddedColumns);
                        break;
                }

                var data = _dataProvider();
                var column = Array.FindIndex(allColumnHeaders, h => string.Equals(h, valuesToken.Column.Val, StringComparison.OrdinalIgnoreCase));

                if (column >= 0)
                {
                    var uniqueValues = data.Where(row => column < row.Length && !string.IsNullOrEmpty(row[column]))
                        .Select(row => row[column])
                        .Distinct(StringComparer.OrdinalIgnoreCase)
                        .OrderBy(value => value)
                        .ToList();
                    suggestions.AddRange(uniqueValues);
                }

                break;
        }

        var result = new List<(int score, string text, IList<int> pos)>();
        var pattern = FuzzySearcher.ParsePattern(CaseMode.CaseSmart, searchString, true);
        var slab = Slab.MakeDefault();
        foreach (var suggestion in suggestions)
        {
            var normalScore = FuzzySearcher.GetScore(suggestion, pattern, slab);
            slab.Reset();
            var pos = FuzzySearcher.GetPositions(suggestion, pattern, slab);
            slab.Reset();
            if (normalScore > 0)
            {
                result.Add((normalScore, suggestion, pos));
            }
        }
        
        result.Sort((a, b) =>
        {
            int byScore = b.score.CompareTo(a.score);
            if (byScore != 0)
            {
                return byScore;
            }

            return StringComparer.OrdinalIgnoreCase.Compare(a.text, b.text);
        });

        return result.Select(r =>
        {
            var line = TerminalEscapedLine.SimpleText(r.text);
            line.SetPos(r.pos);
            return line;
        }).ToList();
    }
    
    static int CompareForFilter(string? left, string? right)
    {
        // Handle nulls consistently with string.Compare behavior
        if (left is null || right is null)
            return string.Compare(left, right, StringComparison.OrdinalIgnoreCase);

        var l = left.Trim();
        var r = right.Trim();

        // 1) Numeric (decimal handles ints/floats/scientific)
        if (decimal.TryParse(l, NumberStyles.Float, CultureInfo.InvariantCulture, out var dnL) &&
            decimal.TryParse(r, NumberStyles.Float, CultureInfo.InvariantCulture, out var dnR))
            return dnL.CompareTo(dnR);

        // 2) DateTime
        if (DateTime.TryParse(l, CultureInfo.InvariantCulture, DateTimeStyles.AssumeLocal, out var dtL) &&
            DateTime.TryParse(r, CultureInfo.InvariantCulture, DateTimeStyles.AssumeLocal, out var dtR))
            return dtL.CompareTo(dtR);

        // 3) TimeSpan
        if (TimeSpan.TryParse(l, CultureInfo.InvariantCulture, out var tsL) &&
            TimeSpan.TryParse(r, CultureInfo.InvariantCulture, out var tsR))
            return tsL.CompareTo(tsR);

        // 4) Fallback: case-insensitive lexicographic
        return string.Compare(l, r, StringComparison.OrdinalIgnoreCase);
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
    
    private static Dictionary<int, int> CalculateColumnWidths(string[][] data, string[]? columnHeaders)
    {
        var maxWidths = new Dictionary<int, int>();

        if (columnHeaders != null)
        {
            for (int i = 0; i < columnHeaders.Length; i++)
            {
                maxWidths[i] = columnHeaders[i].Length;
            }
        }
        
        foreach (var row in data)
        {
            for (int i = 0; i < row.Length; i++)
            {
                var cellValue = row[i];
                var currentMax = maxWidths.GetValueOrDefault(i, 0);
                maxWidths[i] = Math.Max(currentMax, cellValue.Length);
            }
        }

        return maxWidths;
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
    private readonly DisplayColumnsProvider _displayColumnsProvider;

    public StringArrayRow(string[] data, DisplayColumnsProvider displayColumnsProvider)
    {
        _data = data;
        _displayColumnsProvider = displayColumnsProvider;
    }

    public string GetSearchableText()
    {
        var searchableText = new List<string>();
        var columnIndices = _displayColumnsProvider.GetDisplayColumnsAndWidths().Keys.ToList();
        for (int i = 0; i < columnIndices.Count; i++)
        {
            var columnIndex = columnIndices[i];
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

        var displayColumnsAndWidths = _displayColumnsProvider.GetDisplayColumnsAndWidths();

        var i = 0;
        foreach (var displayColumnsAndWidth in displayColumnsAndWidths)
        {
            var columnIndex = displayColumnsAndWidth.Key;
            var value = columnIndex < _data.Length ? (_data[columnIndex] ?? string.Empty) : string.Empty;
            var width = displayColumnsAndWidth.Value;

            if (i == displayColumnsAndWidths.Count - 1)
            {
                parts.Add(value);
            }
            else
            {
                parts.Add(value.PadRight(width));
            }

            i++;
        }

        return string.Join("  ", parts);
    }

    public string[] GetAllData() => _data;

    public string[]? GetAllColumnHeaders() => _displayColumnsProvider.AllColumnHeaders;
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