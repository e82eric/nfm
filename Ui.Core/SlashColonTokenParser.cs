using System.Text;

namespace nfm.Win32Ui;
public sealed record ParsedToken(string Value, int? CursorPos, int tokenStartPos);
public sealed record ParseResult(string ParsedSearchString, List<ParsedToken> Tokens);
public static class SlashColonTokenParser
{
    public static ParseResult Extract(string input, int cursorPos)
    {
        var tokens = new List<ParsedToken>();
        if (input is null) input = string.Empty;

        int firstMarker = input.IndexOf("/:", StringComparison.Ordinal);
        if (firstMarker < 0)
        {
            if (input == "/")
                return new ParseResult(string.Empty, tokens);
            return new ParseResult(input, tokens);
        }

        if (cursorPos < 0) cursorPos = 0;
        if (cursorPos > input.Length) cursorPos = input.Length;

        int i = 0;
        var remainder = new StringBuilder(input.Length);
        bool needSpace = false;

        static bool IsSlashOnly(ReadOnlySpan<char> s)
        {
            int start = 0, end = s.Length - 1;
            while (start <= end && char.IsWhiteSpace(s[start])) start++;
            while (end >= start && char.IsWhiteSpace(s[end])) end--;
            if (start > end) return false;            // only whitespace
            return (end == start) && s[start] == '/'; // exactly "/"
        }

        void AppendClean(string s, int start, int length)
        {
            if (length <= 0) return;

            int end = start + length;
            while (length > 0 && char.IsWhiteSpace(s[end - 1]))
            {
                end--;
                length--;
            }

            if (length <= 0)
            {
                needSpace = needSpace || remainder.Length > 0;
                return;
            }

            if (needSpace && remainder.Length > 0) remainder.Append(' ');
            remainder.Append(s, start, length);
            needSpace = false;
        }

        while (i < input.Length)
        {
            int marker = input.IndexOf("/:", i, StringComparison.Ordinal);
if (marker < 0)
{
    // Trailing non-token segment: handle dangling "/" specially
    var tail = input.AsSpan(i, input.Length - i);
    if (IsSlashOnly(tail))
    {
        // does the original tail have at least one space AFTER the slash?
        bool hasTrailingSpaceAfterSlash = tail.Length > 0 && tail[tail.Length - 1] == ' ';

        if (hasTrailingSpaceAfterSlash)
        {
            // normalize to "/ "
            if (remainder.Length > 0 && remainder[^1] != ' ')
                remainder.Append(' ');
            remainder.Append("/ ");
        }
        // else: no trailing space after slash -> drop it entirely

        break; // we're done
    }

    // otherwise, append normally (with spacing normalization)
    AppendClean(input, i, input.Length - i);
    break;
}

            // ---- Normalize middle dangling "/" segment to "/ " ----
            if (marker > i)
            {
                var between = input.AsSpan(i, marker - i);
                if (IsSlashOnly(between))
                {
                    // replace whatever spacing existed with a single "/ "
                    if (remainder.Length > 0 && remainder[^1] != ' ')
                    {
                        // ensure we don't glue onto previous text (rare due to prior normalization)
                        remainder.Append(' ');
                    }
                    remainder.Append("/ ");
                    needSpace = false; // we've just appended normalized text
                }
                else
                {
                    AppendClean(input, i, marker - i);
                }
            }

            // token is eligible only at start or when preceded by whitespace
            bool tokenEligible = marker == 0 || char.IsWhiteSpace(input[marker - 1]);

            int j = marker + 2;                 // after "/:"
            while (j < input.Length && char.IsWhiteSpace(input[j])) j++; // skip gap after "/:"

            int? relativeCursor = null;

            // Cursor on colon of a subsequent token ⇒ start of token (0)
            if (tokenEligible && cursorPos == marker + 1)
            {
                if (marker > 0 && char.IsWhiteSpace(input[marker - 1]))
                    relativeCursor = 0;
            }

            int tokenTextStart = j; // first token character index
            string tokenValue = ReadTokenWithQuotes_NoLocals(input, ref j, cursorPos, ref relativeCursor);

            if (tokenEligible)
            {
                int nextMarker = input.IndexOf("/:", tokenTextStart, StringComparison.Ordinal);
                int tokenStartPos = (nextMarker >= 0) ? (marker + 1) : tokenTextStart;
                tokens.Add(new ParsedToken(tokenValue, relativeCursor, tokenStartPos));
            }

            // skip whitespace after token so remainder stays tidy
            while (j < input.Length && char.IsWhiteSpace(input[j])) j++;
            if (remainder.Length > 0 && j < input.Length) needSpace = true;

            i = j;
        }

        // ---- Trailing normalization for dangling "/" cases ----
        string parsed = remainder.ToString();
        string leadTrim = parsed.TrimStart();
        if (leadTrim == "/")
        {
            parsed = string.Empty;
        }
        else if (leadTrim.Length > 0 && leadTrim[0] == '/')
        {
            bool onlySlashAndSpaces = true;
            for (int k = 1; k < leadTrim.Length; k++)
            {
                if (!char.IsWhiteSpace(leadTrim[k])) { onlySlashAndSpaces = false; break; }
            }
            if (onlySlashAndSpaces) parsed = "/ ";
        }

        return new ParseResult(parsed, tokens);
    }

    // Maps cursor ONLY when it is on a token character.
    // No mapping on whitespace boundaries (space between tokens/end of token).
    private static string ReadTokenWithQuotes_NoLocals(string input, ref int j, int cursorPos, ref int? relativeCursor)
    {
        var sb = new StringBuilder();

        while (j < input.Length)
        {
            char c = input[j];

            if (char.IsWhiteSpace(c))
            {
                // boundary → do not map; whitespace is outside the token
                break;
            }

            if (c == '"')
            {
                // opening quote isn't part of token; map position if cursor sits here
                if (relativeCursor is null && cursorPos == j)
                    relativeCursor = sb.Length;

                j++; // skip opening quote

                while (j < input.Length)
                {
                    if (j >= input.Length) break;

                    char qc = input[j];

                    if (qc == '"')
                    {
                        bool isDoubled = (j + 1 < input.Length) && input[j + 1] == '"';

                        if (relativeCursor is null && cursorPos == j)
                        {
                            if (isDoubled)
                            {
                                // map before appending the literal '"'
                                relativeCursor = sb.Length;
                            }
                            else
                            {
                                // closing quote → last real char in token
                                relativeCursor = sb.Length == 0 ? 0 : sb.Length - 1;
                            }
                        }

                        if (isDoubled)
                        {
                            j += 2;        // consume both quotes
                            sb.Append('"'); // append one
                            continue;
                        }

                        j++; // closing quote (no append)
                        break;
                    }

                    if (qc == '\\' && j + 1 < input.Length)
                    {
                        if (relativeCursor is null && cursorPos == j)
                            relativeCursor = sb.Length;

                        char next = input[j + 1];
                        if (next == '"' || next == '\\')
                        {
                            j += 2;
                            sb.Append(next);
                            continue;
                        }

                        j++;              // consume '\'
                        sb.Append('\\');  // literal backslash
                        continue;
                    }

                    if (relativeCursor is null && cursorPos == j)
                        relativeCursor = sb.Length;

                    sb.Append(qc);
                    j++;
                }

                continue;
            }

            // unquoted char
            if (relativeCursor is null && cursorPos == j)
                relativeCursor = sb.Length;

            sb.Append(c);
            j++;
        }

        // No fallback mapping at token end (whitespace or EOI)
        return sb.ToString();
    }
}
