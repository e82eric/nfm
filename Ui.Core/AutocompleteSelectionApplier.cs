using nfm.Win32Ui;

namespace nfm.Ui.Core;
public static class AutocompleteSelectionApplier
{
    private static readonly string[] FullOps = { "==", "!=", "=~", "!~" };

    public static string Apply(
        string tokenText,
        TokenCursorPosition cursorPositionType,
        int existingTextLength,
        string selectedSuggestion,
        bool shiftApplied)
    {
        tokenText ??= string.Empty;
        selectedSuggestion ??= string.Empty;
        if (existingTextLength < 0) existingTextLength = 0;
        if (existingTextLength > tokenText.Length) existingTextLength = tokenText.Length;

        static string ReplaceTrailingPrefix(string text, int prefixLen, string replacement)
        {
            int cut = Math.Max(0, text.Length - prefixLen);
            return text.Substring(0, cut) + replacement;
        }

        static string PrepareValueForExplicitInsertion(string suggestion)
        {
            // Escape all embedded quotes
            var escaped = suggestion.Replace("\"", "\\\"");
            // Wrap if whitespace, quotes (already escaped), or comma present
            bool needsWrapping = false;
            for (int i = 0; i < suggestion.Length && !needsWrapping; i++)
            {
                char ch = suggestion[i];
                if (char.IsWhiteSpace(ch) || ch == '"' || ch == ',')
                    needsWrapping = true;
            }
            return needsWrapping ? $"\"{escaped}\"" : escaped;
        }

        // detect existing full operator
        int fullOpIndex = -1;
        foreach (var op in FullOps)
        {
            int idx = tokenText.IndexOf(op, StringComparison.Ordinal);
            if (idx >= 0 && (fullOpIndex == -1 || idx < fullOpIndex))
                fullOpIndex = idx;
        }

        switch (cursorPositionType)
        {
            case TokenCursorPosition.Key:
                return ReplaceTrailingPrefix(tokenText, existingTextLength, selectedSuggestion + "=");

            case TokenCursorPosition.Operator:
                if (fullOpIndex < 0)
                    return ReplaceTrailingPrefix(tokenText, existingTextLength, selectedSuggestion);
                else
                {
                    string suffix = shiftApplied ? "," : " ";
                    return ReplaceTrailingPrefix(tokenText, existingTextLength, selectedSuggestion + suffix);
                }

            case TokenCursorPosition.Value:
            {
                // NEW: append delimiter here too
                var processed = PrepareValueForExplicitInsertion(selectedSuggestion);
                string suffix = shiftApplied ? "," : " ";
                return ReplaceTrailingPrefix(tokenText, existingTextLength, processed + suffix);
            }

            default:
                return tokenText;
        }
    }
}
