using System.Text.RegularExpressions;

namespace nfm.Ui.Core;

public class ColumnFilter
{
    public string ColumnName { get; set; } = string.Empty;
    public string Operator { get; set; } = "=";
    public string Value { get; set; } = string.Empty;
}

public static class ColumnFilterParser
{
    private static readonly Regex CompleteFilterRegex = new(@"/:(\w+)(==|!=|!~|=~)([^\s]+)|/:(\w+)(=)(?![=~])([^\s]+)", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex IncompleteFilterRegex = new(@"/:(\w+)(?:(==|!=|!~|=~|=)\s*$|(==|!=|!~|=~|=)\s+|$|\s+)", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    /// <summary>
    /// Parses complete column filters from the search text.
    /// Patterns: /:columnName=value, /:columnName==value, /:columnName!=value, /:columnName=~value, /:columnName!~value
    /// </summary>
    public static List<ColumnFilter> ParseColumnFilters(string searchText)
    {
        var filters = new List<ColumnFilter>();

        if (string.IsNullOrEmpty(searchText))
            return filters;

        var matches = CompleteFilterRegex.Matches(searchText);

        foreach (Match match in matches)
        {
            if (match.Groups[1].Success && match.Groups[2].Success && match.Groups[3].Success)
            {
                // First alternation: compound operators (==, !=, =~)
                filters.Add(new ColumnFilter
                {
                    ColumnName = match.Groups[1].Value,
                    Operator = match.Groups[2].Value,
                    Value = match.Groups[3].Value
                });
            }
            else if (match.Groups[4].Success && match.Groups[5].Success && match.Groups[6].Success)
            {
                // Second alternation: single equals operator
                filters.Add(new ColumnFilter
                {
                    ColumnName = match.Groups[4].Value,
                    Operator = match.Groups[5].Value,
                    Value = match.Groups[6].Value
                });
            }
        }

        return filters;
    }

    /// <summary>
    /// Removes complete column filter patterns from the search text.
    /// </summary>
    public static string RemoveColumnFilters(string searchText)
    {
        if (string.IsNullOrEmpty(searchText))
            return searchText;

        return CompleteFilterRegex.Replace(searchText, "").Trim();
    }

    /// <summary>
    /// Checks if the search text contains any incomplete column filter patterns.
    /// Incomplete patterns: /:columnName, /:columnName=, /:columnName==, /:columnName!=, /:columnName=~, /:columnName!~
    /// </summary>
    public static bool HasIncompleteColumnFilters(string searchText)
    {
        if (string.IsNullOrEmpty(searchText))
            return false;

        // First, remove all complete column filters
        var textWithoutCompleteFilters = CompleteFilterRegex.Replace(searchText, "");

        // Then check if there are any remaining incomplete column filter patterns
        return IncompleteFilterRegex.IsMatch(textWithoutCompleteFilters);
    }

    /// <summary>
    /// Pre-processes the search string for fuzzy search by handling column filters.
    /// Removes incomplete column filters and complete column filters to prevent interference with fuzzy search.
    /// </summary>
    public static string PreParseSearchString(string searchText)
    {
        if (string.IsNullOrEmpty(searchText))
            return searchText;

        var hasIncompleteFilters = HasIncompleteColumnFilters(searchText);

        if (hasIncompleteFilters)
        {
            // Remove incomplete column filters completely
            var cleanedText = IncompleteFilterRegex.Replace(searchText, "").Trim();
            // Also remove complete column filters for consistent behavior
            cleanedText = RemoveColumnFilters(cleanedText);
            // Normalize multiple spaces to single spaces
            cleanedText = System.Text.RegularExpressions.Regex.Replace(cleanedText, @"\s+", " ");
            return cleanedText;
        }

        // If no incomplete filters, proceed normally with complete filters removed
        return RemoveColumnFilters(searchText);
    }
}