using System.Diagnostics.CodeAnalysis;
using System.Text;
using System.Text.RegularExpressions;

namespace nfm.Win32Ui;

public record TextSpan(int Start, int EndExclusive);
public record SeparatorToken(TextSpan TextSpan, SeparatorKind Kind);
public record SeparatedValue(ValueToken Token, SeparatorToken? Separator);
public record SeparatedValues(IReadOnlyList<SeparatedValue> Values, bool IsComplete);
public record ValueToken(TextSpan TextSpan, string Val, bool IsComplete);
public record OperatorToken(TextSpan TextSpan, OperatorTokenKind? TokenOperator, bool IsComplete, bool Success);
public record ActionToken(TextSpan TextSpan, ActionTokenKind Action);
public record Token(TextSpan TextSpan, ActionToken? Action, ValueToken? Column, OperatorToken? Operator, SeparatedValues? Values)
{
    [MemberNotNullWhen(true, nameof(Action))]
    public bool HasAction => Action is not null;

    [MemberNotNullWhen(true, nameof(Action))]
    [MemberNotNullWhen(true, nameof(Column))]
    public bool HasColumn => Action is not null && Column is not null;

    [MemberNotNullWhen(true, nameof(Operator))]
    public bool HasOperator => Operator is not null;

    [MemberNotNullWhen(true, nameof(Values))]
    public bool HasValues => Values is not null;

    // Optional convenience pattern:
    public bool TryGetValues([NotNullWhen(true)] out SeparatedValues? values)
        => (values = Values) is not null;
    
    
}
public record ParseResult(IReadOnlyList<Token> Tokens, string SearchString);

public enum OperatorTokenKind
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

public enum SeparatorKind
{
    Comma
}

public enum ActionTokenKind
{
    Expression
}

public enum TokenPart
{
    Column,
    Operator,
    Value
}

public static class TokenExtensions
{
    public static IEnumerable<Token> GetFilters(this ParseResult parseResult)
    {
        var completeTokens = parseResult.Tokens.Where( r =>
            r.Column != null && r.Column.Val != "DisplayColumns" && r.Column.Val != "SortDsc" && r.Column.Val != "SortDsc" &&
            r.Values != null && r.Values.IsComplete);
        return completeTokens;
    }
    
    public static IEnumerable<Token> GetSortFilters(this ParseResult parseResult)
    {
        var completeTokens = parseResult.Tokens.Where( r =>
            r.Column != null && (r.Column.Val == "SortDsc" || r.Column.Val == "SortDsc") &&
            r.Values != null && r.Values.IsComplete);
        return completeTokens;
    }

    public static string? GetFocusedValuePrefix(this SeparatedValues value)
    {
        var last = value.Values.LastOrDefault();
        if (last == null)
        {
            return null;
        }

        return last.Token.Val;
    }

    public static IEnumerable<string>? GetDisplayColumns(this ParseResult parseResult)
    {
        var token = parseResult.Tokens.FirstOrDefault(t => t.Values != null && t.Values.IsComplete && t.Column.Val == "DisplayColumns");
        if (token == null)
        {
            return null;
        }
        return token.Values.Values.Select(v => v.Token.Val);
    }

    public static (Token Token, TokenPart Part)? GetFocusedToken(this ParseResult parseResult, int cursorPos)
    {
        (Token, TokenPart)? result = null;
        foreach (var token in parseResult.Tokens)
        {
            if (TryGetAtEndOfIncompleteTokenPart(token, cursorPos, out var part))
            {
                result = (token, part);
            }
        }

        return result;
    }
    
    public static string GetSearchString(this IReadOnlyList<Token> tokens, string input)
    {
        if (string.IsNullOrEmpty(input) || tokens.Count == 0)
        {
            return input;
        }

        var sb = new StringBuilder(input.Length);
        var currentStart = 0;

        foreach (var token in tokens)
        {
            var tokenPos = token.TextSpan;

            if (currentStart < tokenPos.Start)
            {
                sb.Append(input, currentStart, tokenPos.Start - currentStart);
            }

            currentStart = tokenPos.EndExclusive + 1;
        }

        if (currentStart < input.Length)
        {
            sb.Append(input, currentStart, input.Length - currentStart);
        }

        return sb.ToString();
    }
    
    private static bool TryGetAtEndOfIncompleteTokenPart(this Token token, int cursorPos,  out TokenPart result)
    {
        result = default;

        if (token.Column == null && token.Action != null && token.Action.TextSpan.EndExclusive == cursorPos)
        {
            result = TokenPart.Column;
            return true;
        }
        
        if (token.Column != null && !token.Column.IsComplete && token.Column.TextSpan.EndExclusive == cursorPos)
        {
            result = TokenPart.Column;
            return true;
        }

        if (token.Operator != null && !token.Operator.IsComplete && token.Operator.TextSpan.EndExclusive == cursorPos)
        {
            result = TokenPart.Operator;
            return true;
        }

        if (token.Values == null && token.Operator != null && token.Operator.IsComplete && token.Operator.TextSpan.EndExclusive == cursorPos)
        {
            result = TokenPart.Value;
            return true;
        }

        if (token.Values != null && !token.Values.IsComplete)
        {
            var last = token.Values.Values.LastOrDefault();
            if (last != null && last.Token.TextSpan.EndExclusive == cursorPos)
            {
                result = TokenPart.Value;
                return true;
            }
        }

        return false;
    }
}

public static partial class TokenParser
{
    public static readonly Dictionary<string, OperatorTokenKind> Operators = new()
    {
        {"==", OperatorTokenKind.Equals},
        {"!=", OperatorTokenKind.NotEquals},
        {"=~", OperatorTokenKind.Regex},
        {"!~", OperatorTokenKind.NotEqualsRegex},
        {">=", OperatorTokenKind.GreaterThanOrEqual},
        {"<=", OperatorTokenKind.LessThenOrEqual},
        {">",  OperatorTokenKind.GreaterThan},
        {"<",  OperatorTokenKind.LessThan},
    };
    
    [GeneratedRegex(@"(?<=^|\s)/", RegexOptions.CultureInvariant)]
    private static partial Regex UnescapedSlash();
    
    [GeneratedRegex(@"^""(?<val>(?:\\.|[^""])*)(?<!\\)""", RegexOptions.CultureInvariant)]
    private static partial Regex QuotedString();
    
    [GeneratedRegex(@"^""(?<val>(?:\\.|[^""])+)$", RegexOptions.CultureInvariant)]
    private static partial Regex IncompleteQuotedString();
    
    [GeneratedRegex(@"^(?<val>[^""|=|<|>|!|\s]+)(=|<|>|!)", RegexOptions.CultureInvariant)]
    private static partial Regex NonQuotedColumn();
    
    [GeneratedRegex(@"^(?<val>[^""|\s][^\s|$]*)\s", RegexOptions.CultureInvariant)]
    private static partial Regex UnquotedValue();
    
    [GeneratedRegex(@"^(?<val>[^\s]+)$", RegexOptions.CultureInvariant)]
    private static partial Regex IncompleteUnquotedValue();
    
    [GeneratedRegex(@"^(?<val>[^(=|!|<|>|\s)]+)(\s)", RegexOptions.CultureInvariant)]
    private static partial Regex IncompleteUnquotedColumnNotLineEnd();
    
    [GeneratedRegex(@"^(?<val>[^(=|!|<|>|\s)]+)$", RegexOptions.CultureInvariant)]
    private static partial Regex IncompleteUnquotedColumnLineEnd();
    
    [GeneratedRegex(@"^(?<val>(==|!=|<|>|=~|!~|>=|<=)+)", RegexOptions.CultureInvariant)]
    private static partial Regex CompleteOperator();
    
    [GeneratedRegex(@"^(?<val>(=|!))(\s|$)", RegexOptions.CultureInvariant)]
    private static partial Regex InCompleteOperator();
    
    [GeneratedRegex(@"^(?<val>(?:\\.|[^""|\s])(?:\\.|[^\s|$|,])*)", RegexOptions.CultureInvariant)]
    private static partial Regex InCompleteValue2();
    
    [GeneratedRegex(@"^(?<val>(?:\\.|[^,\\\s""])(?:\\.|[^,\\])*),", RegexOptions.CultureInvariant)]
    private static partial Regex TerminatedCompleteValue2();
    
    [GeneratedRegex(@"^""(?<val>(?:\\.|[^""])*)(?<!\\)"",", RegexOptions.CultureInvariant)]
    private static partial Regex TerminatedQuotedString();
    
    public static ParseResult Parse(string input)
    {
        var result = new List<Token>();
        
        var current = input;
        var slashResult = UnescapedSlash().Match(current);
        var absoluteStart = 0;
        var absoluteEnd = 0;
        while(slashResult.Success)
        {
            var tokenStart = absoluteStart + slashResult.Index;
            absoluteStart = tokenStart;
            absoluteEnd += slashResult.Index + 1;
            current = current.Substring(slashResult.Index, current.Length - slashResult.Index);

            if (current.Length == 1)
            {
                result.Add(new Token(new TextSpan(tokenStart, absoluteEnd), null, null, null, null));
                break;
            }

            current = current.Substring(1, current.Length - 1);
            absoluteStart++;
            var nextChar = current[0];
            if (nextChar == ' ')
            {
                result.Add(new Token(new TextSpan(tokenStart, absoluteEnd), null, null, null, null));
                current = current.Substring(0, current.Length);
                slashResult = UnescapedSlash().Match(current);
                absoluteStart++;
            }
            else if (nextChar == ':')
            {
                absoluteEnd++;
                var action = new ActionToken(new TextSpan(absoluteStart, absoluteEnd), ActionTokenKind.Expression);
                if (current.Length == 1)
                {
                    result.Add(new Token(new TextSpan(tokenStart, absoluteEnd), action, null, null, null));
                    break;
                }
                
                current = current.Substring(1, current.Length - 1);
                absoluteStart++;
                
                var parsedColumn = ParseColumn(current);
                absoluteEnd = absoluteStart + parsedColumn.len;
                var unescapeColumnValue = UnescapeValue(parsedColumn.val);
                var column1 = new ValueToken(new TextSpan(absoluteStart, absoluteEnd), unescapeColumnValue, parsedColumn.complete);
            
                current = current.Substring(parsedColumn.len, current.Length - parsedColumn.len);
                absoluteStart = absoluteEnd;

                OperatorToken? op = null;
                SplitValueToken? value = null;
                SeparatedValues values = null;
                bool isValueComplete = false;
                if (parsedColumn.complete)
                {
                    var operatorResult = ParseOperator(current);
                    if (operatorResult.success)
                    {
                        absoluteEnd += operatorResult.len!.Value;
                        op = new OperatorToken(
                            new TextSpan(absoluteStart, absoluteEnd),
                            operatorResult.op,
                            operatorResult.complete,
                            operatorResult.success);

                        current = current.Substring(operatorResult.len.Value, current.Length - operatorResult.len.Value);
                        absoluteStart = absoluteEnd;
                    }

                    if (current.Length == 0)
                    {
                        result.Add(new Token(new TextSpan(tokenStart, absoluteEnd), action, column1, op, null));
                        break;
                    }

                    if (operatorResult.success && operatorResult.complete)
                    {
                        var innerValues = new List<SeparatedValue>();
                        while (true)
                        {
                            var valueResult = ParseValue2(current);
                            absoluteEnd += valueResult.len;
                            var unescapedValue = UnescapeValue(valueResult.val);
                            var textSpan = new TextSpan(absoluteStart, absoluteEnd);
                            var valueToken = new ValueToken(textSpan, unescapedValue, valueResult.complete);
                            SeparatorToken? separatorToken = null;
                            if (valueResult.terminated)
                            {
                                separatorToken = new SeparatorToken(new TextSpan(absoluteEnd - 1, absoluteEnd), SeparatorKind.Comma);
                            }
                            
                            var splitValue = new SeparatedValue(valueToken, separatorToken);
                            innerValues.Add(splitValue);
                            current = current.Substring(valueResult.len, current.Length - valueResult.len);
                            absoluteStart = absoluteEnd;

                            if (!valueResult.complete || !valueResult.terminated)
                            {
                                if (valueResult.complete && !valueResult.terminated)
                                {
                                    isValueComplete = true;
                                }
                                
                                values = new SeparatedValues(innerValues, isValueComplete);
                                break;
                            }
                        }
                    }
                }
            
                var token = new Token(new TextSpan(tokenStart, absoluteEnd), action, column1, op, values);
                result.Add(token);
            
                slashResult = UnescapedSlash().Match(current);
            }
        }

        var searchString = result.GetSearchString(input);
        return new ParseResult(result, searchString);
    }

    private static (string val, bool complete, int len) ParseColumn(string input)
    {
        var complete = false;
        Match? match = null;
        int quoteLen = 0;
        var quotedResult = QuotedString().Match(input);
        if (quotedResult.Success)
        {
            quoteLen = 2;
            complete = true;
            match = quotedResult;
        }

        var unquotedResult = NonQuotedColumn().Match(input);
        if (match == null && unquotedResult.Success)
        {
            complete = true;
            match = unquotedResult;
        }

        var incompleteQuotedResult = IncompleteQuotedString().Match(input);
        if (match == null && incompleteQuotedResult.Success)
        {
            quoteLen = 1;
            match = incompleteQuotedResult;
        }

        var incompleteUnquotedNonLineEndResult = IncompleteUnquotedColumnNotLineEnd().Match(input);
        if (match == null && incompleteUnquotedNonLineEndResult.Success)
        {
            match = incompleteUnquotedNonLineEndResult;
        }
        
        var incompleteUnquotedLineEndResult = IncompleteUnquotedColumnLineEnd().Match(input);
        if (match == null && incompleteUnquotedLineEndResult.Success)
        {
            match = incompleteUnquotedLineEndResult;
        }

        var value = string.Empty;
        if (match != null)
        {
            value = match.Groups["val"].Value;
        }

        return (value, complete, value.Length + quoteLen);
    }
    
    private static (string val, bool complete, bool terminated, int len) ParseValue2(string input)
    {
        var complete = false;
        var terminated = false;
        Match? match = null;
        int quoteLen = 0;
        var terminatedQuotedResult = TerminatedQuotedString().Match(input);
        if (terminatedQuotedResult.Success)
        {
            quoteLen = 3;
            complete = true;
            terminated = true;
            match = terminatedQuotedResult;
        }
        
        var quotedResult = QuotedString().Match(input);
        if (match == null && quotedResult.Success)
        {
            quoteLen = 2;
            complete = true;
            match = quotedResult;
        }
        
        var unquotedTerminatedResult = TerminatedCompleteValue2().Match(input);
        if (match == null && unquotedTerminatedResult.Success)
        {
            complete = true;
            terminated = true;
            quoteLen = 1;
            match = unquotedTerminatedResult;
        }

        var unquotedResult = UnquotedValue().Match(input);
        if (match == null && unquotedResult.Success)
        {
            complete = true;
            match = unquotedResult;
        }

        var incompleteQuotedResult = IncompleteQuotedString().Match(input);
        if (match == null && incompleteQuotedResult.Success)
        {
            quoteLen = 1;
            match = incompleteQuotedResult;
        }
        
        var incompleteUnquotedNonLineEndResult = IncompleteUnquotedValue().Match(input);
        if (match == null && incompleteUnquotedNonLineEndResult.Success)
        {
            match = incompleteUnquotedNonLineEndResult;
        }

        var value = string.Empty;
        if (match != null)
        {
            value = match.Groups["val"].Value;
        }

        return (value, complete, terminated, value.Length + quoteLen);
    }

    private static (bool success, bool complete, OperatorTokenKind? op, int? len) ParseOperator(string input)
    {
        var completeResult = CompleteOperator().Match(input);
        if (completeResult.Success)
        {
            var value = completeResult.Groups["val"].Value;
            if (Operators.TryGetValue(value, out var op))
            {
                return (true, true, op, value.Length);
            }
        }

        var incompleteResult = InCompleteOperator().Match(input);
        if (incompleteResult.Success)
        {
            return (true, false, null, 1);
        }

        return (false, false, null, null);
    }
    
    private static string UnescapeValue(string raw)
    {
        if (string.IsNullOrEmpty(raw))
        {
            return raw;
        }

        if (raw.IndexOf('\\') < 0)
        {
            return raw;
        }

        var sb = new StringBuilder(raw.Length);

        for (int i = 0; i < raw.Length; i++)
        {
            var c = raw[i];

            if (c == '\\')
            {
                if (i + 1 < raw.Length)
                {
                    var next = raw[i + 1];
                    switch (next)
                    {
                        case '\\':
                        case '"':
                        case '/':
                            sb.Append(next);
                            i++; 
                            break;
                        default:
                            sb.Append('\\');
                            sb.Append(next);
                            i++;
                            break;
                    }
                }
                else
                {
                    sb.Append('\\');
                }
            }
            else
            {
                sb.Append(c);
            }
        }

        return sb.ToString();
    }
}