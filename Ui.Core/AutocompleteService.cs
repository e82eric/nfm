using System.Text.RegularExpressions;

namespace nfm.Ui.Core;

public static class AutocompleteService
{
    public static AutocompleteResult ApplySelection(string currentText, int currentCursorPosition, string selectedSuggestion)
    {
        var beforeCursor = currentText.Substring(0, Math.Min(currentCursorPosition, currentText.Length));
        var lastColonSlashIndex = beforeCursor.LastIndexOf("/:");

        if (lastColonSlashIndex < 0)
        {
            return new AutocompleteResult
            {
                Success = false,
                ErrorMessage = "No column filter pattern found"
            };
        }

        // Extract the content after /:
        var afterColonSlash = beforeCursor.Substring(lastColonSlashIndex + 2);

        // Check if we're completing a value (after an operator) or a column name
        var operatorMatch = Regex.Match(afterColonSlash, @"^(\w+)(==|!=|!~|=~|=)(.*)$");

        string newText;
        int newCursorPos;

        if (operatorMatch.Success)
        {
            // We're completing a value after an operator
            var columnName = operatorMatch.Groups[1].Value;
            var operatorStr = operatorMatch.Groups[2].Value;

            // Replace only the value part, preserving the column name and operator
            var replaceStart = lastColonSlashIndex + 2 + columnName.Length + operatorStr.Length;
            var beforeReplacement = currentText.Substring(0, replaceStart);
            var afterReplacement = currentCursorPosition < currentText.Length ? currentText.Substring(currentCursorPosition) : string.Empty;
            
            // Add quotes around value if it contains spaces
            var valueToInsert = selectedSuggestion.Contains(' ') ? $"\"{selectedSuggestion}\"" : selectedSuggestion;
            
            // Build new text with space after value
            newText = beforeReplacement + valueToInsert + " " + afterReplacement;
            newCursorPos = replaceStart + valueToInsert.Length + 1;
        }
        else
        {
            // We're completing a column name - add = after it
            var afterCursor = currentCursorPosition < currentText.Length ? currentText.Substring(currentCursorPosition) : string.Empty;
            newText = currentText.Substring(0, lastColonSlashIndex + 2) + selectedSuggestion + "=" + afterCursor;
            newCursorPos = lastColonSlashIndex + 2 + selectedSuggestion.Length + 1;
        }

        return new AutocompleteResult
        {
            Success = true,
            NewText = newText,
            NewCursorPosition = newCursorPos,
            CompletedValue = operatorMatch.Success,
            CompletedColumn = !operatorMatch.Success
        };
    }
}

public class AutocompleteResult
{
    public bool Success { get; set; }
    public string? ErrorMessage { get; set; }
    public string NewText { get; set; } = string.Empty;
    public int NewCursorPosition { get; set; }
    public bool CompletedValue { get; set; }
    public bool CompletedColumn { get; set; }
}