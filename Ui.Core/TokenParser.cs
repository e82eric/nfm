namespace nfm.Win32Ui;

public enum TokenOperator
{
    Equals,
    NotEquals,
    Regex,
    NotEqualsRegex,
    GreaterThan,
    GreaterThanOrEqual,
    LessThan,
    LessThenOrEqual
}

public enum TokenCursorPosition
{
    Key,
    Operator,
    Value
}

public sealed record SplitToken(string Key, TokenOperator? TokenOperator, List<string> Values, TokenCursorPosition? CursorPosition, int? ValueCursorOn, ParsedToken BaseToken);
public static class TokenParser
{
    public static readonly (string op, TokenOperator kind)[] Operators =
    {
        ("==", TokenOperator.Equals),
        ("!=", TokenOperator.NotEquals),
        ("=~", TokenOperator.Regex),
        ("!~", TokenOperator.NotEqualsRegex),
        (">=", TokenOperator.GreaterThanOrEqual),
        ("<=", TokenOperator.LessThenOrEqual),
        (">",  TokenOperator.GreaterThan),
        ("<",  TokenOperator.LessThan),
    };

    public static SplitToken Parse(ParsedToken parsedToken)
    {
        if (parsedToken is null) throw new ArgumentNullException(nameof(parsedToken));
        var token = parsedToken.Value ?? throw new ArgumentNullException(nameof(parsedToken.Value));
        token = token.Trim();

        if (token.Length == 0)
        {
            return new SplitToken(
                Key: string.Empty,
                TokenOperator: null,
                Values: new List<string>(),
                CursorPosition: TokenCursorPosition.Key,
                ValueCursorOn: null,
                BaseToken: parsedToken
            );
        }

        // Find the LEFTMOST operator, preferring the LONGEST when ties occur
        int fullOpIdx = -1;
        TokenOperator fullOpKind = default;
        string? fullOpText = null;

        foreach (var (op, kind) in Operators)
        {
            int idx = token.IndexOf(op, StringComparison.Ordinal);
            if (idx >= 0)
            {
                if (fullOpIdx == -1 || idx < fullOpIdx ||
                    (idx == fullOpIdx && fullOpText != null && op.Length > fullOpText.Length))
                {
                    fullOpIdx = idx;
                    fullOpKind = kind;
                    fullOpText = op;
                }
                else if (idx == fullOpIdx && fullOpText == null)
                {
                    fullOpIdx = idx;
                    fullOpKind = kind;
                    fullOpText = op;
                }
            }
        }

        // Detect PARTIAL operator start for any operator-leading char (=, !, <, >)
        int partialOpStart = -1;
        for (int i = 0; i < token.Length; i++)
        {
            char ch = token[i];
            if (ch == '=' || ch == '!' || ch == '<' || ch == '>')
            {
                partialOpStart = i;
                break;
            }
        }

        // --------- No operator at all ---------
        if (partialOpStart < 0)
        {
            return new SplitToken(
                Key: token,
                TokenOperator: null,
                Values: new List<string>(),
                CursorPosition: parsedToken.CursorPos is null ? null : TokenCursorPosition.Key,
                ValueCursorOn: null,
                BaseToken: parsedToken
            );
        }

        bool useFull = (fullOpIdx >= 0 &&
                        fullOpIdx == partialOpStart &&
                        fullOpText is not null &&
                        (
                            // two-char ops like ==, !=, =~, !~, >=, <=
                            fullOpText.Length == 2
                            // single-char comparators are complete on their own
                            || fullOpText == ">" || fullOpText == "<"
                        ));

        string key = token.Substring(0, partialOpStart).Trim();

        if (!useFull)
        {
            // BEFORE: var cursorPos = ClassifyCursorForPartial(parsedToken.CursorPos, partialOpStart, token.Length);
            var cursorPos = ClassifyCursorForPartial(parsedToken.CursorPos, partialOpStart, token.Length, token);

            return new SplitToken(
                Key: key,
                TokenOperator: null,
                Values: new List<string>(),
                CursorPosition: cursorPos,
                ValueCursorOn: null,
                BaseToken: parsedToken
            );
        }
        else
        {
            // --------- Full operator (two chars like ==, !=, =~, !~, >=, <=) ---------
            string rhs = (partialOpStart + fullOpText!.Length <= token.Length)
                ? token.Substring(partialOpStart + fullOpText.Length)
                : string.Empty;

            // Preserve empty values (split with None + Trim each piece)
            var values = new List<string>();
            foreach (var piece in rhs.Split(new[] { ',' }, StringSplitOptions.None))
                values.Add(piece.Trim());

            var cursorPos = ClassifyCursorForFull(parsedToken.CursorPos, partialOpStart);

            int? valueCursorOn = null;
            if (cursorPos == TokenCursorPosition.Value && values.Count > 0)
            {
                int opEnd = partialOpStart + fullOpText.Length; // exclusive
                int rel = (parsedToken.CursorPos ?? opEnd) - opEnd;
                rel = rhs.Length > 0 ? Math.Clamp(rel, 0, rhs.Length - 1) : 0;
                valueCursorOn = MapRelToValueIndex(rhs, rel, values.Count);
            }

            return new SplitToken(
                Key: key,
                TokenOperator: fullOpKind,
                Values: values,
                CursorPosition: cursorPos,
                ValueCursorOn: valueCursorOn,
                BaseToken: parsedToken
            );
        }
    }

    private static TokenCursorPosition? ClassifyCursorForPartial(int? cpNullable, int opStart, int tokenLen, string token)
    {
        if (cpNullable is null) return null;

        // Allow caret to sit just after the last char
        int cp = Math.Clamp(cpNullable.Value, 0, tokenLen);

        // Safety: if opStart is out of range, default to Key
        if (opStart < 0 || opStart >= tokenLen) return TokenCursorPosition.Key;

        char opChar = token[opStart];

        // For single-char < or >, being ON the operator already counts as Value
        if (opChar == '<' || opChar == '>')
        {
            return (cp < opStart) ? TokenCursorPosition.Key : TokenCursorPosition.Value;
        }

        // For single-char '=' or '!' keep old rule:
        if (cp < opStart) return TokenCursorPosition.Key;
        if (cp == opStart) return TokenCursorPosition.Operator;
        return TokenCursorPosition.Value; // cp > opStart
    }

    private static TokenCursorPosition? ClassifyCursorForFull(int? cpNullable, int opStart)
    {
        if (cpNullable is null) return null;
        int cp = cpNullable.Value; // do NOT clamp to tokenLen-1

        if (cp < opStart) return TokenCursorPosition.Key;
        if (cp == opStart) return TokenCursorPosition.Operator;
        return TokenCursorPosition.Value; // cp >= opStart + 1
    }

    private static int MapRelToValueIndex(string rhs, int rel, int valuesCount)
    {
        int segStart = 0;
        int built = 0;

        while (segStart <= rhs.Length)
        {
            int comma = rhs.IndexOf(',', segStart);
            int segEnd = (comma >= 0) ? comma : rhs.Length; // exclusive

            if (rel <= segEnd) return Math.Min(built, valuesCount - 1);

            built++;
            if (comma < 0) break;
            segStart = comma + 1;
        }

        return Math.Max(0, valuesCount - 1);
    }
}
