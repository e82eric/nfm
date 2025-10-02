using System.Text.RegularExpressions;
using System.Text;

namespace nfm.Ui.Core;

public class ColumnFilter
{
    public string ColumnName { get; set; } = string.Empty;
    public string Operator { get; set; } = "=";
    public string Value { get; set; } = string.Empty;
    public List<string> Values { get; set; } = new List<string>();
    public bool IsMultiValue => Values.Count > 0;
}

public enum IncompleteFilterType
{
    None,
    Slash,              // "/"
    SlashWithColon,     // "/:"
    ColumnPrefix,       // "/:col" (partial column name)
    ValuePrefix         // "/:column=val" (partial value)
}

public enum IncompleteFilterContext
{
    None,
    Column,             // User is typing a column name
    Value               // User is typing a value after an operator
}

public class IncompleteFilterInfo
{
    public IncompleteFilterType Type { get; set; } = IncompleteFilterType.None;
    public IncompleteFilterContext Context { get; set; } = IncompleteFilterContext.None;
    public string ColumnPrefix { get; set; } = string.Empty;
    public string Operator { get; set; } = string.Empty;
    public string ValuePrefix { get; set; } = string.Empty;
    public bool HasIncompleteFilter => Type != IncompleteFilterType.None;
    public bool IsMultiValue { get; set; } = false;
    public List<string> CompletedValues { get; set; } = new List<string>();
}

public static class ColumnFilterParser
{
    private static readonly Regex CompleteFilterRegex = new(@"/:(\w+)(>=|<=|==|!=|!~|=~|>|<)(""[^""]*""|[^\s""]+)|/:(\w+)(=)(?![=~])(""[^""]*""|[^\s""]+)", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex IncompleteFilterRegex = new(@"/:(\w*)(?:(>=|<=|==|!=|!~|=~|>|<|=|!)\s*$|(>=|<=|==|!=|!~|=~|>|<|=|!)\s+|$|\s+)|/:$|/$|^\s*/\s*$", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    public static List<ColumnFilter> ParseColumnFilters(string searchText)
    {
        var filters = new List<ColumnFilter>();

        if (string.IsNullOrEmpty(searchText))
        {
            return filters;
        }

        var matches = CompleteFilterRegex.Matches(searchText);

        foreach (Match match in matches)
        {
            // Check if this filter is at the end of the string without trailing whitespace
            // Only single '=' operators are considered incomplete when at end without space
            bool isAtEndWithoutSpace = (match.Index + match.Length) == searchText.Length && !searchText.EndsWith(" ");
            
            if (isAtEndWithoutSpace)
            {
                // Determine the operator for this match
                string currentOperator = "";
                if (match.Groups[2].Success)
                {
                    currentOperator = match.Groups[2].Value; // First alternation operators
                }
                else if (match.Groups[5].Success)
                {
                    currentOperator = match.Groups[5].Value; // Second alternation operators
                }
                
                // Only skip single '=' operators at the end without trailing space
                if (currentOperator == "=")
                {
                    continue;
                }
            }
            
            if (match.Groups[1].Success && match.Groups[2].Success && match.Groups[3].Success)
            {
                // First alternation: compound operators (==, !=, =~)
                var columnName = match.Groups[1].Value;

                // Skip sort filters
                if (string.Equals(columnName, "SortAsc", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(columnName, "SortDsc", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                var value = match.Groups[3].Value;
                // Remove quotes if present
                if (value.StartsWith("\"") && value.EndsWith("\"") && value.Length >= 2)
                {
                    value = value.Substring(1, value.Length - 2);
                }

                var filter = new ColumnFilter
                {
                    ColumnName = columnName,
                    Operator = match.Groups[2].Value,
                    Value = value
                };

                // Handle DisplayColumns with comma-separated values
                // if (string.Equals(columnName, "DisplayColumns", StringComparison.OrdinalIgnoreCase))
                // {
                //     filter.Values = value.Split(',').Select(v => v.Trim()).Where(v => !string.IsNullOrEmpty(v)).ToList();
                // }

                filters.Add(filter);
            }
            else if (match.Groups[4].Success && match.Groups[5].Success && match.Groups[6].Success)
            {
                // Second alternation: single equals operator
                var columnName = match.Groups[4].Value;

                // Skip sort filters
                if (string.Equals(columnName, "SortAsc", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(columnName, "SortDsc", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(columnName, "DisplayColumns", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                var value = match.Groups[6].Value;
                // Remove quotes if present
                if (value.StartsWith("\"") && value.EndsWith("\"") && value.Length >= 2)
                {
                    value = value.Substring(1, value.Length - 2);
                }

                var filter = new ColumnFilter
                {
                    ColumnName = columnName,
                    Operator = match.Groups[5].Value,
                    Value = value
                };

                filters.Add(filter);
            }
        }

        return filters;
    }

    public static List<ColumnFilter> ParseSortColumnFilters(string searchText)
    {
        var filters = new List<ColumnFilter>();

        if (string.IsNullOrEmpty(searchText))
        {
            return filters;
        }

        var matches = CompleteFilterRegex.Matches(searchText);

        foreach (Match match in matches)
        {
            // Check if this filter is at the end of the string without trailing whitespace
            // Only single '=' operators are considered incomplete when at end without space
            bool isAtEndWithoutSpace = (match.Index + match.Length) == searchText.Length && !searchText.EndsWith(" ");

            if (isAtEndWithoutSpace)
            {
                // Determine the operator for this match
                string currentOperator = "";
                if (match.Groups[2].Success)
                {
                    currentOperator = match.Groups[2].Value; // First alternation operators
                }
                else if (match.Groups[5].Success)
                {
                    currentOperator = match.Groups[5].Value; // Second alternation operators
                }

                // Only skip single '=' operators at the end without trailing space
                if (currentOperator == "=")
                {
                    continue;
                }
            }

            if (match.Groups[1].Success && match.Groups[2].Success && match.Groups[3].Success)
            {
                // First alternation: compound operators (==, !=, =~)
                var columnName = match.Groups[1].Value;

                // Only include sort filters
                if (!string.Equals(columnName, "SortAsc", StringComparison.OrdinalIgnoreCase) &&
                    !string.Equals(columnName, "SortDsc", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                var value = match.Groups[3].Value;
                // Remove quotes if present
                if (value.StartsWith("\"") && value.EndsWith("\"") && value.Length >= 2)
                {
                    value = value.Substring(1, value.Length - 2);
                }

                filters.Add(new ColumnFilter
                {
                    ColumnName = columnName,
                    Operator = match.Groups[2].Value,
                    Value = value
                });
            }
            else if (match.Groups[4].Success && match.Groups[5].Success && match.Groups[6].Success)
            {
                // Second alternation: single equals operator
                var columnName = match.Groups[4].Value;

                // Only include sort filters
                if (!string.Equals(columnName, "SortAsc", StringComparison.OrdinalIgnoreCase) &&
                    !string.Equals(columnName, "SortDsc", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                var value = match.Groups[6].Value;
                // Remove quotes if present
                if (value.StartsWith("\"") && value.EndsWith("\"") && value.Length >= 2)
                {
                    value = value.Substring(1, value.Length - 2);
                }

                filters.Add(new ColumnFilter
                {
                    ColumnName = columnName,
                    Operator = match.Groups[5].Value,
                    Value = value
                });
            }
        }

        return filters;
    }

    public static List<string> ParseDisplayColumnsFilter(string searchText)
    {
        if (string.IsNullOrEmpty(searchText))
        {
            return new List<string>();
        }

        // First try to get from complete filters
        var filters = ParseColumnFilters(searchText);
        var displayColumnsFilter = filters.FirstOrDefault(f =>
            string.Equals(f.ColumnName, "DisplayColumns", StringComparison.OrdinalIgnoreCase));

        if (displayColumnsFilter != null && displayColumnsFilter.IsMultiValue)
        {
            return displayColumnsFilter.Values;
        }

        // If not found in complete filters, check incomplete filter info
        var incompleteInfo = GetIncompleteFilterInfo(searchText);
        if (incompleteInfo.HasIncompleteFilter &&
            incompleteInfo.IsMultiValue &&
            string.Equals(incompleteInfo.ColumnPrefix, "DisplayColumns", StringComparison.OrdinalIgnoreCase))
        {
            var result = new List<string>(incompleteInfo.CompletedValues);

            // Add the current value being typed if it's not empty
            if (!string.IsNullOrWhiteSpace(incompleteInfo.ValuePrefix))
            {
                result.Add(incompleteInfo.ValuePrefix.Trim());
            }

            return result;
        }

        return new List<string>();
    }

    public static IncompleteFilterInfo GetIncompleteFilterInfo(string searchText)
    {
        var result = new IncompleteFilterInfo();
        
        if (string.IsNullOrEmpty(searchText))
        {
            return result;
        }

        // Use the original complex logic but extract information
        // This maintains compatibility with existing behavior
        
        // Check for basic incomplete patterns using the original regex approach
        var textWithoutCompleteFilters = CompleteFilterRegex.Replace(searchText, "");
        var hasIncompletePatterns = IncompleteFilterRegex.IsMatch(textWithoutCompleteFilters);
        
        // Check for any operator filters without trailing space (original behavior + fix for compound operators)
        var hasOperatorWithoutSpace = false;
        if (!searchText.EndsWith(" "))
        {
            var operatorWithoutSpaceRegex = new Regex(@"/:(\w+)(>=|<=|==|!=|!~|=~|>|<|=)([^\s]+)$", RegexOptions.IgnoreCase);
            hasOperatorWithoutSpace = operatorWithoutSpaceRegex.IsMatch(searchText);
        }

        // Original logic for determining if incomplete
        var isIncomplete = false;
        if (hasIncompletePatterns)
        {
            // Special exception: column names and single = with trailing space should return false
            if (searchText.EndsWith(" "))
            {
                var trimmed = searchText.Trim();
                
                // Check if the last incomplete pattern is just a column name: "... /:name"
                var isColumnNameOnly = System.Text.RegularExpressions.Regex.IsMatch(trimmed, @"/:(\w+)$");
                
                // Check if the last incomplete pattern is single = : "... /:name="
                var isSingleEqualsOnly = System.Text.RegularExpressions.Regex.IsMatch(trimmed, @"/:(\w+)=$");
                
                // Check if it's just a slash with trailing space (but not surrounded by spaces): "/ "
                // This should exclude "/ " but allow "  /  " 
                var isSlashWithTrailingSpaceOnly = trimmed == "/" && searchText == "/ ";
                
                isIncomplete = !isColumnNameOnly && !isSingleEqualsOnly && !isSlashWithTrailingSpaceOnly;
            }
            else
            {
                isIncomplete = true;
            }
        }
        else if (hasOperatorWithoutSpace)
        {
            isIncomplete = true;
        }

        if (!isIncomplete)
        {
            return result; // Type = None
        }

        // Now extract detailed information for incomplete filters
        // Check for slash-only patterns first
        var trimmedText = searchText.Trim();
        if (trimmedText == "/")
        {
            result.Type = IncompleteFilterType.Slash;
            result.Context = IncompleteFilterContext.Column;
            return result;
        }
        
        if (searchText.Trim() == "/:")
        {
            result.Type = IncompleteFilterType.SlashWithColon;
            result.Context = IncompleteFilterContext.Column;
            return result;
        }

        // Find the last occurrence of /: to focus on the current filter being typed
        var lastColonSlashIndex = searchText.LastIndexOf("/:");
        if (lastColonSlashIndex == -1)
        {
            // Check for standalone / at the end
            if (searchText.EndsWith("/"))
            {
                result.Type = IncompleteFilterType.Slash;
                result.Context = IncompleteFilterContext.Column;
            }
            return result;
        }

        // Extract everything after the last /:
        var afterLastColonSlash = searchText.Substring(lastColonSlashIndex + 2);
        
        // Handle special cases first
        if (string.IsNullOrEmpty(afterLastColonSlash) || afterLastColonSlash.Trim() == "")
        {
            result.Type = IncompleteFilterType.SlashWithColon;
            result.Context = IncompleteFilterContext.Column;
            return result;
        }
        
        // Remove trailing spaces for analysis but preserve original for context
        var trimmedAfter = afterLastColonSlash.TrimEnd();
        
        // Check for different patterns
        var operatorMatch = System.Text.RegularExpressions.Regex.Match(trimmedAfter, @"^(\w+)(>=|<=|==|!=|!~|=~|>|<|=)(.*)$");
        
        if (operatorMatch.Success)
        {
            var columnName = operatorMatch.Groups[1].Value;
            var operatorStr = operatorMatch.Groups[2].Value;
            var valueStr = operatorMatch.Groups[3].Value;

            // Check if this is a DisplayColumns filter with comma-separated values
            bool isDisplayColumns = string.Equals(columnName, "DisplayColumns", StringComparison.OrdinalIgnoreCase);

            if (string.IsNullOrEmpty(valueStr))
            {
                // Has operator but no value yet: "/:column=" or "/:column=="
                result.Type = IncompleteFilterType.ValuePrefix;
                result.Context = IncompleteFilterContext.Value;
                result.ColumnPrefix = columnName;
                result.Operator = operatorStr;
                result.ValuePrefix = "";
                result.IsMultiValue = isDisplayColumns;
            }
            else if (isDisplayColumns)
            {
                // For DisplayColumns, parse comma-separated values
                // Keep returning incomplete until there's a trailing space
                var values = valueStr.Split(',');
                var completedValues = new List<string>();
                var currentValue = "";

                // All values except the last are considered complete
                for (int i = 0; i < values.Length - 1; i++)
                {
                    var trimmed = values[i].Trim();
                    if (!string.IsNullOrEmpty(trimmed))
                    {
                        completedValues.Add(trimmed);
                    }
                }

                // The last value is the one being typed
                currentValue = values[values.Length - 1];

                result.Type = IncompleteFilterType.ValuePrefix;
                result.Context = IncompleteFilterContext.Value;
                result.ColumnPrefix = columnName;
                result.Operator = operatorStr;
                result.ValuePrefix = currentValue;
                result.IsMultiValue = true;
                result.CompletedValues = completedValues;
            }
            else
            {
                // Has value, this is a complete filter being considered incomplete
                result.Type = IncompleteFilterType.ValuePrefix;
                result.Context = IncompleteFilterContext.Value;
                result.ColumnPrefix = columnName;
                result.Operator = operatorStr;
                result.ValuePrefix = valueStr;
            }
        }
        else if (!string.IsNullOrEmpty(trimmedAfter))
        {
            // Has column name but no operator yet: "/:column"
            result.Type = IncompleteFilterType.ColumnPrefix;
            result.Context = IncompleteFilterContext.Column;
            result.ColumnPrefix = trimmedAfter;
        }
        else
        {
            // Just "/:" with nothing after
            result.Type = IncompleteFilterType.SlashWithColon;
            result.Context = IncompleteFilterContext.Column;
        }

        return result;
    }

    public static string ParseSearchString(string searchText)
    {
        if (string.IsNullOrEmpty(searchText))
        {
            return searchText;
        }

        // Collect all filter matches (both complete and incomplete), excluding sort filters
        var allMatches = new List<(int Start, int End)>();

        foreach (Match match in CompleteFilterRegex.Matches(searchText))
        {
            allMatches.Add((match.Index, match.Index + match.Length));
        }

        foreach (Match match in IncompleteFilterRegex.Matches(searchText))
        {
            allMatches.Add((match.Index, match.Index + match.Length));
        }

        // Sort matches by start position and merge overlapping ranges
        allMatches.Sort((a, b) => a.Start.CompareTo(b.Start));
        var mergedMatches = new List<(int Start, int End)>();
        
        foreach (var match in allMatches)
        {
            if (mergedMatches.Count == 0 || mergedMatches[mergedMatches.Count - 1].End < match.Start)
            {
                mergedMatches.Add(match);
            }
            else
            {
                // Merge overlapping matches
                var last = mergedMatches[mergedMatches.Count - 1];
                mergedMatches[mergedMatches.Count - 1] = (last.Start, Math.Max(last.End, match.End));
            }
        }

        // Build result and track cursor position
        var result = new StringBuilder();
        var originalPos = 0;
        var newPos = 0;
        var cursorMapped = false;

        foreach (var match in mergedMatches)
        {
            // Add text before this filter
            if (originalPos < match.Start)
            {
                var beforeFilter = searchText.Substring(originalPos, match.Start - originalPos);
                if (0 >= originalPos && 0 <= match.Start && !cursorMapped)
                {
                    cursorMapped = true;
                }
                result.Append(beforeFilter);
                newPos += beforeFilter.Length;
            }

            // Skip the filter (don't add it to result)
            if (0 >= match.Start && 0 < match.End && !cursorMapped)
            {
                // Cursor was inside a filter, place it at the start of where the filter was
                cursorMapped = true;
            }

            originalPos = match.End;
        }

        // Add remaining text after last filter
        if (originalPos < searchText.Length)
        {
            var remaining = searchText.Substring(originalPos);
            if (0 >= originalPos && !cursorMapped)
            {
                cursorMapped = true;
            }
            result.Append(remaining);
        }

        // Normalize spaces and adjust cursor position
        var beforeNormalization = result.ToString();
        var normalized = System.Text.RegularExpressions.Regex.Replace(beforeNormalization, @"\s+", " ").Trim();

        return normalized;
    }
}