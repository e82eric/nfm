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
public record ActionToken(TextSpan TextSpan, ActionTokenKind? Action, bool IsCompleted);

public abstract record BaseToken(TextSpan TextSpan);

public sealed record OnActionToken( TextSpan TextSpan, ActionToken Action) : BaseToken(TextSpan);
public sealed record OnColumnToken(TextSpan TextSpan, ActionToken Action, ValueToken Column) : BaseToken(TextSpan);
public sealed record OnOperatorToken(TextSpan TextSpan, ActionToken Action, ValueToken Column, OperatorToken Operator): BaseToken(TextSpan);
public sealed record OnValueToken(TextSpan TextSpan,
    ActionToken Action,
    ValueToken Column,
    OperatorToken Operator,
    SeparatedValues Values) : BaseToken(TextSpan);

public record ParseResult(IReadOnlyList<BaseToken> Tokens, string SearchString);

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
    Filter,
    SelectDisplayColumns,
    Sort,
}

public enum TokenPart
{
    Column,
    Operator,
    Value
}

public static class TokenExtensions
{
    public static IEnumerable<OnValueToken> GetFilters(this ParseResult parseResult)
    {
        var completeTokens = parseResult.Tokens.OfType<OnValueToken>().Where(t =>
            t.Action.Action == ActionTokenKind.Filter && t.Values.IsComplete);
        return completeTokens;
    }
    
    public static IEnumerable<OnValueToken> GetSortFilters(this ParseResult parseResult)
    {
        var completeTokens = parseResult.Tokens.OfType<OnValueToken>().Where( r =>
            r.Action.Action == ActionTokenKind.Sort && r.Values.IsComplete);
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
        var token = parseResult.Tokens.OfType<OnValueToken>().FirstOrDefault(t => t.Values.IsComplete && t.Action.Action == ActionTokenKind.SelectDisplayColumns);
        if (token == null)
        {
            return null;
        }
        return token.Values.Values.Select(v => v.Token.Val);
    }

    public static (BaseToken Token, TokenPart Part)? GetFocusedToken(this ParseResult parseResult, int cursorPos)
    {
        (BaseToken, TokenPart)? result = null;
        foreach (var token in parseResult.Tokens)
        {
            if (TryGetAtEndOfIncompleteTokenPart(token, cursorPos, out var part))
            {
                result = (token, part);
            }
        }

        return result;
    }
    
    private static bool TryGetAtEndOfIncompleteTokenPart(this BaseToken token, int cursorPos,  out TokenPart result)
    {
        result = default;

        switch (token)
        {
            case OnActionToken { Action.TextSpan.EndExclusive: var end } when end == cursorPos:
                result = TokenPart.Column;
                return true;
            case OnColumnToken { Column.IsComplete: false, Column.TextSpan.EndExclusive: var end }
                when end == cursorPos:
                result = TokenPart.Column;
                return true;
            case OnOperatorToken { Operator.IsComplete: false, Operator.TextSpan.EndExclusive: var end }
                when end == cursorPos:
                result = TokenPart.Operator;
                return true;
            case OnOperatorToken { Operator.IsComplete: true, Operator.TextSpan.EndExclusive: var end }
                when end == cursorPos:
                result = TokenPart.Value;
                return true;
            case OnValueToken { Values.IsComplete: false }:
                result = TokenPart.Value;
                return true;
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

    public static readonly Dictionary<char, (ActionTokenKind Kind, string Description)> Actions = new()
    {
        { ':', (ActionTokenKind.Filter, "Filter") },
        { '#', (ActionTokenKind.SelectDisplayColumns, "Select Display Columns") },
        { '!', (ActionTokenKind.Sort, "Sort") },
    };
    
    [GeneratedRegex(@"(?<=^|\s)/", RegexOptions.CultureInvariant)]
    private static partial Regex UnescapedSlash();
    
    [GeneratedRegex(@"^""(?<val>(?:\\.|[^""])*)(?<!\\)""", RegexOptions.CultureInvariant)]
    private static partial Regex QuotedString();
    
    [GeneratedRegex(@"^""(?<val>(?:\\.|[^""])*)$", RegexOptions.CultureInvariant)]
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
    
    [GeneratedRegex(@"^(?<val>(?:\\.|[^,\\\s""])(?:\\.|[^,\\\s])*),", RegexOptions.CultureInvariant)]
    private static partial Regex TerminatedCompleteValue2();
    
    [GeneratedRegex(@"^""(?<val>(?:\\.|[^""])*)(?<!\\)"",", RegexOptions.CultureInvariant)]
    private static partial Regex TerminatedQuotedString();
    
public static ParseResult Parse(string input)
{
    var tokens = new List<BaseToken>();
    var pos = 0;

    while (true)
    {
        // Find next slash that is at start or preceded by whitespace (== (?<=^|\s)/ )
        var slashIdx = NextSlashStart(input, pos);
        if (slashIdx < 0) break;

        var tokenStart = slashIdx;
        var i = slashIdx + 1; // char after '/'

        // If '/' is last char or followed by space => incomplete action at cursor
        if (i >= input.Length || input[i] == ' ')
        {
            // OnAction with no concrete action yet
            var action = new ActionToken(new TextSpan(i, i), default, false);
            tokens.Add(new OnActionToken(new TextSpan(tokenStart, i), action));

            // Advance past '/' and an optional single space (to avoid refinding same slash)
            pos = Math.Min(input.Length, i + (i < input.Length && input[i] == ' ' ? 1 : 0));
            continue;
        }

        // Recognize action character (only valid immediately after '/')
        if (!Actions.TryGetValue(input[i], out var act))
        {
            // Not a known action => treat like incomplete action (cursor right after '/')
            var action = new ActionToken(new TextSpan(i, i), default, false);
            tokens.Add(new OnActionToken(new TextSpan(tokenStart, i), action));
            pos = i; // move forward one to avoid infinite loop
            continue;
        }

        // We have a concrete action "/<char>"
        var actionStart = i;
        var actionEnd   = i + 1; // covers the action char
        var actionTok   = new ActionToken(new TextSpan(actionStart, actionEnd), act.Kind, true);
        i = actionEnd;

        // If nothing after the action => we are now “on column”
        if (i >= input.Length)
        {
            tokens.Add(new OnColumnToken(new TextSpan(tokenStart, i), actionTok,
                        new ValueToken(new TextSpan(i, i), string.Empty, false)));
            pos = i;
            continue;
        }

        // --- Parse Column ---
        var (colVal, colComplete, colLen) = ParseColumn(input[i..]); // existing helper
        var colStart = i;
        var colEnd   = i + colLen;
        var column   = new ValueToken(new TextSpan(colStart, colEnd), UnescapeValue(colVal), colComplete);
        i = colEnd;

        if (!colComplete)
        {
            tokens.Add(new OnColumnToken(new TextSpan(tokenStart, i), actionTok, column));
            pos = i;
            continue;
        }

        // --- Parse Operator (optional/incomplete) ---
        var (opOk, opComplete, opKind, opLen) = ParseOperator(input[i..]); // existing helper
        if (!opOk)
        {
            var op = new OperatorToken(new TextSpan(i, i), null, false, false);
            tokens.Add(new OnOperatorToken(new TextSpan(tokenStart, i), actionTok, column, op));
            pos = i;
            continue;
        }

        var opStart = i;
        var opEnd   = i + opLen!.Value;
        var opTok   = new OperatorToken(new TextSpan(opStart, opEnd), opKind, opComplete, true);
        i = opEnd;
        
        if (!opComplete)
        {
            tokens.Add(new OnOperatorToken(new TextSpan(tokenStart, i), actionTok, column, opTok));
            pos = i;
            continue;
        }

        if (i >= input.Length)
        {
            tokens.Add(new OnValueToken(new TextSpan(tokenStart, i), actionTok, column, opTok, new SeparatedValues([], false)));
            pos = i;
            continue;
        }

        // --- Parse Values (zero or more, comma-separated) ---
        var vals = new List<SeparatedValue>();
        var valuesComplete = false;
        var lastTerminated = false;

        while (i < input.Length)
        {
            var (v, vComplete, terminated, vLen) = ParseValues(input[i..]); // your unified value parser
            if (vLen == 0) break; // nothing to consume

            var vStart = i;
            var vEnd   = i + vLen;
            var vTok   = new ValueToken(new TextSpan(vStart, vEnd), UnescapeValue(v), vComplete);

            SeparatorToken? sep = null;
            if (terminated)
                sep = new SeparatorToken(new TextSpan(vEnd - 1, vEnd), SeparatorKind.Comma);

            vals.Add(new SeparatedValue(vTok, sep));
            i = vEnd;
            lastTerminated = terminated;

            // Stop if last value is incomplete or not comma-terminated
            if (!vComplete || !terminated)
            {
                valuesComplete = vComplete && !terminated;
                break;
            }
        }
        
        if (lastTerminated)
        {
            var emptyTok = new ValueToken(new TextSpan(i, i), string.Empty, false);
            vals.Add(new SeparatedValue(emptyTok, null));
            valuesComplete = false; // still expecting input for this value
        }

        var values = new SeparatedValues(vals, valuesComplete);
        tokens.Add(new OnValueToken(new TextSpan(tokenStart, i), actionTok, column, opTok, values));
        pos = i;
    }

    var search = GetSearchString(tokens, input);
    return new ParseResult(tokens, search);
}

// Replaces (?<=^|\s)/  — finds '/' that is at start or preceded by whitespace.
private static int NextSlashStart(string s, int start)
{
    for (int k = start; k < s.Length; k++)
    {
        if (s[k] == '/' && (k == 0 || char.IsWhiteSpace(s[k - 1])))
            return k;
    }
    return -1;
}
    
    // public static ParseResult Parse(string input)
    // {
    //     var result = new List<BaseToken>();
    //     
    //     var current = input;
    //     var slashResult = UnescapedSlash().Match(current);
    //     var absoluteStart = 0;
    //     var absoluteEnd = 0;
    //     while(slashResult.Success)
    //     {
    //         var tokenStart = absoluteStart + slashResult.Index;
    //         absoluteStart = tokenStart;
    //         absoluteEnd += slashResult.Index + 1;
    //         current = current.Substring(slashResult.Index, current.Length - slashResult.Index);
    //
    //         if (current.Length == 1)
    //         {
    //             result.Add(new OnActionToken(new TextSpan(tokenStart, absoluteEnd), new ActionToken(new TextSpan(absoluteEnd, absoluteEnd), null, false)));
    //             break;
    //         }
    //
    //         current = current.Substring(1, current.Length - 1);
    //         absoluteStart++;
    //         var nextChar = current[0];
    //         if (nextChar == ' ')
    //         {
    //             result.Add(new OnActionToken(new TextSpan(tokenStart, absoluteEnd), new ActionToken(new TextSpan(absoluteEnd, absoluteEnd), null, false)));
    //             current = current.Substring(0, current.Length);
    //             slashResult = UnescapedSlash().Match(current);
    //             absoluteStart++;
    //         }
    //         else if (Actions.TryGetValue(nextChar, out var actionResult))
    //         {
    //             absoluteEnd++;
    //             var action = new ActionToken(new TextSpan(absoluteStart, absoluteEnd), actionResult.Kind, true);
    //             if (current.Length == 1)
    //             {
    //                 result.Add(new OnColumnToken(new TextSpan(tokenStart, absoluteEnd), action, new ValueToken(new TextSpan(absoluteEnd, absoluteEnd), string.Empty, false)));
    //                 break;
    //             }
    //             
    //             current = current.Substring(1, current.Length - 1);
    //             absoluteStart++;
    //             
    //             var parsedColumn = ParseColumn(current);
    //             absoluteEnd = absoluteStart + parsedColumn.len;
    //             var unescapeColumnValue = UnescapeValue(parsedColumn.val);
    //             var column1 = new ValueToken(new TextSpan(absoluteStart, absoluteEnd), unescapeColumnValue, parsedColumn.complete);
    //         
    //             current = current.Substring(parsedColumn.len, current.Length - parsedColumn.len);
    //             absoluteStart = absoluteEnd;
    //
    //             OperatorToken? op = null;
    //             SeparatedValues? values = null;
    //             bool isValueComplete = false;
    //             if (parsedColumn.complete)
    //             {
    //                 var operatorResult = ParseOperator(current);
    //                 if (operatorResult.success)
    //                 {
    //                     absoluteEnd += operatorResult.len!.Value;
    //                     op = new OperatorToken(
    //                         new TextSpan(absoluteStart, absoluteEnd),
    //                         operatorResult.op,
    //                         operatorResult.complete,
    //                         operatorResult.success);
    //
    //                     current = current.Substring(operatorResult.len.Value, current.Length - operatorResult.len.Value);
    //                     absoluteStart = absoluteEnd;
    //                     
    //                     if (current.Length == 0)
    //                     {
    //                         BaseToken toAdd = operatorResult.complete ?
    //                             new OnValueToken(new TextSpan(tokenStart, absoluteEnd), action, column1, op, new SeparatedValues([], false)) :
    //                             new OnOperatorToken(new TextSpan(tokenStart, absoluteEnd), action, column1, op);
    //                         result.Add(toAdd);
    //                         break;
    //                     }
    //                     
    //                     if (operatorResult.complete)
    //                     {
    //                         var innerValues = new List<SeparatedValue>();
    //                         while (true)
    //                         {
    //                             var valueResult = ParseValue2(current);
    //                             absoluteEnd += valueResult.len;
    //                             var unescapedValue = UnescapeValue(valueResult.val);
    //                             var textSpan = new TextSpan(absoluteStart, absoluteEnd);
    //                             var valueToken = new ValueToken(textSpan, unescapedValue, valueResult.complete);
    //                             SeparatorToken? separatorToken = null;
    //                             if (valueResult.terminated)
    //                             {
    //                                 separatorToken = new SeparatorToken(new TextSpan(absoluteEnd - 1, absoluteEnd), SeparatorKind.Comma);
    //                             }
    //                         
    //                             var splitValue = new SeparatedValue(valueToken, separatorToken);
    //                             innerValues.Add(splitValue);
    //                             current = current.Substring(valueResult.len, current.Length - valueResult.len);
    //                             absoluteStart = absoluteEnd;
    //
    //                             if (!valueResult.complete || !valueResult.terminated)
    //                             {
    //                                 if (valueResult.complete && !valueResult.terminated)
    //                                 {
    //                                     isValueComplete = true;
    //                                 }
    //                             
    //                                 values = new SeparatedValues(innerValues, isValueComplete);
    //                                 break;
    //                             }
    //                         }
    //                     }
    //                     var token = new OnValueToken(new TextSpan(tokenStart, absoluteEnd), action, column1, op, values);
    //                     result.Add(token);
    //                 }
    //                 else
    //                 {
    //                     op = new OperatorToken(new TextSpan(absoluteEnd, absoluteEnd), null, false, false);
    //                     var token = new OnOperatorToken(new TextSpan(tokenStart, absoluteEnd), action, column1, op);
    //                     result.Add(token);
    //                 }
    //             }
    //             else
    //             {
    //                 var token = new OnColumnToken(new TextSpan(tokenStart, absoluteEnd), action, column1);
    //                 result.Add(token);
    //             }
    //         
    //             slashResult = UnescapedSlash().Match(current);
    //         }
    //     }
    //
    //     var searchString = GetSearchString(result, input);
    //     return new ParseResult(result, searchString);
    // }
    
    private static string GetSearchString(IReadOnlyList<BaseToken> tokens, string input)
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
    
    private static (string val, bool complete, bool terminated, int len) ParseValues(string input)
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