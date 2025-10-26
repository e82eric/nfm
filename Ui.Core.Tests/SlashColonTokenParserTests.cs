using nfm.Win32Ui;
using NUnit.Framework;

namespace nfm.Ui.Core.Tests;

[TestFixture]
public class TokenParserTests2()
{
    [Test]
    public void OnlySearchString()
    {
        var input = "Some Search String";
        var result = TokenParser.Parse(input);
        Assert.That(result.Tokens.Count, Is.EqualTo(0));
        Assert.That(result.SearchString, Is.EqualTo(input));
    }
    
    [Test]
    public void SlashWithCharBeforeIsNotAToken()
    {
        var result = TokenParser.Parse("a/col1==val1");
        Assert.That(result.Tokens.Count, Is.EqualTo(0));
    }
    
    [Test]
    public void SlashOnly()
    {
        var result = TokenParser.Parse("/");
        Assert.That(result.Tokens.Count, Is.EqualTo(1));
        Assert.That(result.Tokens[0], Is.EqualTo(new Token(new TextSpan(0, 1), null, null, null, null)));
        Assert.That(result.SearchString, Is.EqualTo(string.Empty));
    }
    
    [Test]
    public void SlashThenSearchString()
    {
        var expectedSearchString = "Some Search String";
        var result = TokenParser.Parse("/ " + expectedSearchString);
        Assert.That(result.Tokens.Count, Is.EqualTo(1));
        Assert.That(result.Tokens[0], Is.EqualTo(new Token(new TextSpan(0, 1), null, null, null, null)));
        Assert.That(result.SearchString, Is.EqualTo(expectedSearchString));
    }
    
    [Test]
    public void SearchStringThenSlash()
    {
        var expectedSearchString = "Some Search String";
        var result = TokenParser.Parse(expectedSearchString + " /");
        Assert.That(result.Tokens.Count, Is.EqualTo(1));
        Assert.That(result.Tokens[0], Is.EqualTo(new Token(new TextSpan(expectedSearchString.Length + 1, expectedSearchString.Length + 2), null, null, null, null)));
        Assert.That(result.SearchString, Is.EqualTo(expectedSearchString + " "));
    }
    
    [Test]
    public void SearchStringThenSlashThenMoreSearchString()
    {
        var searchString1 = "Some Search String";
        var searchString2 = "more search";
        var result = TokenParser.Parse(searchString1 + " / " + searchString2);
        Assert.That(result.Tokens.Count, Is.EqualTo(1));
        Assert.That(result.Tokens[0], Is.EqualTo(new Token(new TextSpan(searchString1.Length + 1, searchString1.Length + 2), null, null, null, null)));
        Assert.That(result.SearchString, Is.EqualTo(searchString1 + " " + searchString2));
    }
    
    [Test]
    public void SlashSpace()
    {
        var result = TokenParser.Parse("/ ");
        Assert.That(result.Tokens.Count, Is.EqualTo(1));
        Assert.That(result.Tokens[0], Is.EqualTo(new Token(new TextSpan(0, 1), null, null, null, null)));
        Assert.That(result.SearchString, Is.EqualTo(string.Empty));
    }
    
    [Test]
    public void SlashColon()
    {
        var result = TokenParser.Parse("/:");
        Assert.That(result.Tokens.Count, Is.EqualTo(1));
        Assert.That(result.Tokens[0], Is.EqualTo(new Token(new TextSpan(0, 2), new ActionToken(new TextSpan(1, 2), ActionTokenKind.Expression), null, null, null)));
        Assert.That(result.SearchString, Is.EqualTo(string.Empty));
    }
    
    [Test]
    public void SingleCharColumn()
    {
        var result = TokenParser.Parse("/:a");
        Assert.That(result.Tokens.Count, Is.EqualTo(1));
        Assert.That(result.Tokens[0], Is.EqualTo(new Token(new TextSpan(0, 3), new ActionToken(new TextSpan(1, 2), ActionTokenKind.Expression), new ValueToken(new TextSpan(2, 3), "a", false), null, null)));
        Assert.That(result.SearchString, Is.EqualTo(string.Empty));
    }
    
    [Test]
    public void SingleCharColumnAndSearchString()
    {
        var result = TokenParser.Parse("/:a search string");
        Assert.That(result.Tokens.Count, Is.EqualTo(1));
        Assert.That(result.Tokens[0], Is.EqualTo(new Token(new TextSpan(0, 3), new ActionToken(new TextSpan(1, 2), ActionTokenKind.Expression), new ValueToken(new TextSpan(2, 3), "a", false), null, null)));
        Assert.That(result.SearchString, Is.EqualTo("search string"));
    }
    
    [Test]
    public void TwoCharColumn()
    {
        var result = TokenParser.Parse("/:ab");
        Assert.That(result.Tokens.Count, Is.EqualTo(1));
        Assert.That(result.Tokens[0], Is.EqualTo(new Token(new TextSpan(0, 4), new ActionToken(new TextSpan(1, 2), ActionTokenKind.Expression), new ValueToken(new TextSpan(2, 4), "ab", false), null, null)));
        Assert.That(result.SearchString, Is.EqualTo(string.Empty));
    }
    
    [Test]
    public void ThreeCharColumn()
    {
        var result = TokenParser.Parse("/:abc");
        Assert.That(result.Tokens.Count, Is.EqualTo(1));
        Assert.That(result.Tokens[0], Is.EqualTo(new Token(new TextSpan(0, 5), new ActionToken(new TextSpan(1, 2), ActionTokenKind.Expression), new ValueToken(new TextSpan(2, 5), "abc", false), null, null)));
        Assert.That(result.SearchString, Is.EqualTo(string.Empty));
    }
    
    [Test]
    public void DoubleQuoteOnlyColumn()
    {
        var result = TokenParser.Parse("/:\"");
        Assert.That(result.Tokens.Count, Is.EqualTo(1));
        Assert.That(result.Tokens[0], Is.EqualTo(new Token(new TextSpan(0, 3), new ActionToken(new TextSpan(1, 2), ActionTokenKind.Expression), new ValueToken(new TextSpan(2, 3), string.Empty, false), null, null)));
        Assert.That(result.SearchString, Is.EqualTo(string.Empty));
    }
    
    [Test]
    public void DoubleQuoteAndSingleCharColumn()
    {
        var result = TokenParser.Parse("/:\"a");
        Assert.That(result.Tokens.Count, Is.EqualTo(1));
        Assert.That(result.Tokens[0], Is.EqualTo(new Token(new TextSpan(0, 4), new ActionToken(new TextSpan(1, 2), ActionTokenKind.Expression), new ValueToken(new TextSpan(2, 4), "a", false), null, null)));
        Assert.That(result.SearchString, Is.EqualTo(string.Empty));
    }
    
    [Test]
    public void SingleDoubleQuoteAndMultipleWordColumn()
    {
        var result = TokenParser.Parse("/:\"a search string");
        Assert.That(result.Tokens.Count, Is.EqualTo(1));
        Assert.That(result.Tokens[0], Is.EqualTo(new Token(new TextSpan(0, 18), new ActionToken(new TextSpan(1, 2), ActionTokenKind.Expression), new ValueToken(new TextSpan(2, 18), "a search string", false), null, null)));
        Assert.That(result.SearchString, Is.EqualTo(string.Empty));
    }
    
    [Test]
    public void SingleCharSurroundedByDoubleQuotesColumn()
    {
        var result = TokenParser.Parse("/:\"a\"");
        Assert.That(result.Tokens.Count, Is.EqualTo(1));
        Assert.That(result.Tokens[0], Is.EqualTo(new Token(new TextSpan(0, 5), new ActionToken(new TextSpan(1, 2), ActionTokenKind.Expression), new ValueToken(new TextSpan(2, 5), "a", true), null, null)));
        Assert.That(result.SearchString, Is.EqualTo(string.Empty));
    }
    
    [Test]
    public void SingleCharSurroundedByDoubleQuotesColumnAndSearchString()
    {
        var result = TokenParser.Parse("/:\"a\" search string");
        Assert.That(result.Tokens.Count, Is.EqualTo(1));
        Assert.That(result.Tokens[0], Is.EqualTo(new Token(new TextSpan(0, 5), new ActionToken(new TextSpan(1, 2), ActionTokenKind.Expression), new ValueToken(new TextSpan(2, 5), "a", true), null, null)));
        Assert.That(result.SearchString, Is.EqualTo("search string"));
    }
    
    [Test]
    public void SingleEqualSignOperator()
    {
        var result = TokenParser.Parse("/:col1=");
        Assert.That(result.Tokens.Count, Is.EqualTo(1));
        Assert.That(result.Tokens[0], Is.EqualTo(new Token(new TextSpan(0, 7), new ActionToken(new TextSpan(1, 2), ActionTokenKind.Expression), new ValueToken(new TextSpan(2, 6), "col1", true), new OperatorToken(new TextSpan(6, 7), null, false, true), null)));
        Assert.That(result.SearchString, Is.EqualTo(string.Empty));
    }
    
    [Test]
    public void DoubleEqualSignOperator()
    {
        var result = TokenParser.Parse("/:col1==");
        Assert.That(result.Tokens.Count, Is.EqualTo(1));
        Assert.That(result.Tokens[0], Is.EqualTo(new Token(
            new TextSpan(0, 8), new ActionToken(new TextSpan(1, 2), ActionTokenKind.Expression),
            new ValueToken(new TextSpan(2, 6), "col1", true),
            new OperatorToken(new TextSpan(6, 8), OperatorTokenKind.Equals, true, true),
            null)));
        Assert.That(result.SearchString, Is.EqualTo(string.Empty));
    }
    
    [Test]
    public void SingleToken()
    {
        var input = "/:col1==val1";
        var result = TokenParser.Parse(input);
        Assert.That(result.Tokens.Count, Is.EqualTo(1));
        Assert.That(result.Tokens[0], Is.EqualTo(new Token(
            new TextSpan(0, input.Length - 1),
            new ActionToken(new TextSpan(1, 2), ActionTokenKind.Expression),
            new ValueToken(new TextSpan(2, 5), "col1", true),
            new OperatorToken(new TextSpan(6, 7), OperatorTokenKind.Equals, true, true), null)));
    }
    
    [Test]
    public void StartOfValue()
    {
        var input = "/:col1==v";
        var result = TokenParser.Parse(input);
        Assert.That(result.Tokens.Count, Is.EqualTo(1));
        AssertTokensSame(result.Tokens[0], new Token(
            new TextSpan(0, 9),
            new ActionToken(new TextSpan(1, 2), ActionTokenKind.Expression),
            new ValueToken(new TextSpan(2, 6), "col1", true),
            new OperatorToken(new TextSpan(6, 8), OperatorTokenKind.Equals, true, true),
            new SeparatedValues([new SeparatedValue(new ValueToken(new TextSpan(8, 9), "v", false), null)], false)));
    }
    
    [Test]
    public void CompleteValueOneChar()
    {
        var input = "/:col1==v ";
        var result = TokenParser.Parse(input);
        Assert.That(result.Tokens.Count, Is.EqualTo(1));
        AssertTokensSame(result.Tokens[0], new Token(
            new TextSpan(0, 9),
            new ActionToken(new TextSpan(1, 2), ActionTokenKind.Expression),
            new ValueToken(new TextSpan(2, 6), "col1", true),
            new OperatorToken(new TextSpan(6, 8), OperatorTokenKind.Equals, true, true),
            new SeparatedValues([new SeparatedValue(new ValueToken(new TextSpan(8, 9), "v", true), null)], true)));
    }
    
    [Test]
    public void TwoValues()
    {
        var input = "/:col1==val1,val2 ";
        var result = TokenParser.Parse(input);
        Assert.That(result.Tokens.Count, Is.EqualTo(1));
        AssertTokensSame(result.Tokens[0], new Token(
            new TextSpan(0, 17),
            new ActionToken(new TextSpan(1, 2), ActionTokenKind.Expression),
            new ValueToken(new TextSpan(2, 6), "col1", true),
            new OperatorToken(new TextSpan(6, 8), OperatorTokenKind.Equals, true, true),
            new SeparatedValues([
                new SeparatedValue(new ValueToken(new TextSpan(8, 13), "val1", true), new SeparatorToken(new TextSpan(12, 13), SeparatorKind.Comma)),
                new SeparatedValue(new ValueToken(new TextSpan(13, 17), "val2", true), null),
            ], true)));
    }
    
    [Test]
    public void TwoValues_LastNotComplete()
    {
        var input = "/:col1==val1,val2";
        var result = TokenParser.Parse(input);
        Assert.That(result.Tokens.Count, Is.EqualTo(1));
        AssertTokensSame(result.Tokens[0], new Token(
            new TextSpan(0, 17),
            new ActionToken(new TextSpan(1, 2), ActionTokenKind.Expression),
            new ValueToken(new TextSpan(2, 6), "col1", true),
            new OperatorToken(new TextSpan(6, 8), OperatorTokenKind.Equals, true, true),
            new SeparatedValues([
                new SeparatedValue(new ValueToken(new TextSpan(8, 13), "val1", true), new SeparatorToken(new TextSpan(12, 13), SeparatorKind.Comma)),
                new SeparatedValue(new ValueToken(new TextSpan(13, 17), "val2", false), null),
            ], false)));
    }
    
    [Test]
    public void TwoValues_FirstQuoted()
    {
        var input = "/:col1==\"val1\",val2 ";
        var result = TokenParser.Parse(input);
        Assert.That(result.Tokens.Count, Is.EqualTo(1));
        AssertTokensSame(result.Tokens[0], new Token(
            new TextSpan(0, 19),
            new ActionToken(new TextSpan(1, 2), ActionTokenKind.Expression),
            new ValueToken(new TextSpan(2, 6), "col1", true),
            new OperatorToken(new TextSpan(6, 8), OperatorTokenKind.Equals, true, true),
            new SeparatedValues([
                new SeparatedValue(new ValueToken(new TextSpan(8, 15), "val1", true), new SeparatorToken(new TextSpan(14, 15), SeparatorKind.Comma)),
                new SeparatedValue(new ValueToken(new TextSpan(15, 19), "val2", true), null),
            ], true)));
    }
    
    [Test]
    public void TwoValues_BothQuoted()
    {
        var input = "/:col1==\"val1\",\"val2\" ";
        var result = TokenParser.Parse(input);
        Assert.That(result.Tokens.Count, Is.EqualTo(1));
        AssertTokensSame(result.Tokens[0], new Token(
            new TextSpan(0, 21),
            new ActionToken(new TextSpan(1, 2), ActionTokenKind.Expression),
            new ValueToken(new TextSpan(2, 6), "col1", true),
            new OperatorToken(new TextSpan(6, 8), OperatorTokenKind.Equals, true, true),
            new SeparatedValues([
                new SeparatedValue(new ValueToken(new TextSpan(8, 15), "val1", true), new SeparatorToken(new TextSpan(14, 15), SeparatorKind.Comma)),
                new SeparatedValue(new ValueToken(new TextSpan(15, 21), "val2", true), null),
            ], true)));
    }
    
    [Test]
    public void CompleteValueSingleDoubleQuoteOneChar()
    {
        var input = "/:col1==\"v ";
        var result = TokenParser.Parse(input);
        Assert.That(result.Tokens.Count, Is.EqualTo(1));
        AssertTokensSame(result.Tokens[0], new Token(
            new TextSpan(0, 11),
            new ActionToken(new TextSpan(1, 2), ActionTokenKind.Expression),
            new ValueToken(new TextSpan(2, 6), "col1", true),
            new OperatorToken(new TextSpan(6, 8), OperatorTokenKind.Equals, true, true),
            new SeparatedValues([new SeparatedValue(new ValueToken(new TextSpan(8, 11), "v ", false), null)], false)));
    }

    private void AssertTokensSame(Token expected, Token actual)
    {
        Assert.That(expected.TextSpan, Is.EqualTo(actual.TextSpan));
        Assert.That(expected.Action, Is.EqualTo(actual.Action));
        Assert.That(expected.Column, Is.EqualTo(actual.Column));
        Assert.That(expected.Operator, Is.EqualTo(actual.Operator));

        if (expected.Values == null)
        {
            Assert.That(actual.Values, Is.Null);
        }
        else
        {
            Assert.That(expected.Values.Values.Count, Is.EqualTo(actual.Values.Values.Count));
            Assert.That(expected.Values.IsComplete, Is.EqualTo(actual.Values.IsComplete));

            for (int i = 0; i < expected.Values.Values.Count; i++)
            {
                Assert.That(expected.Values.Values[i], Is.EqualTo(actual.Values.Values[i]));
            }
        }
    }
    
    [Test]
    public void CompleteValueSingleSurroundedByDoubleQuotes()
    {
        var input = "/:col1==\"v\"";
        var result = TokenParser.Parse(input);
        Assert.That(result.Tokens.Count, Is.EqualTo(1));
        AssertTokensSame(result.Tokens[0], new Token(
            new TextSpan(0, 11),
            new ActionToken(new TextSpan(1, 2), ActionTokenKind.Expression),
            new ValueToken(new TextSpan(2, 6), "col1", true),
            new OperatorToken(new TextSpan(6, 8), OperatorTokenKind.Equals, true, true),
            new SeparatedValues([new SeparatedValue(new ValueToken(new TextSpan(8, 11), "v", true), null)], true)));
    }
    
    [Test]
    public void SecondTokenIsOnlySlashAndColon()
    {
        var input = "/:col1==val1 /:";
        var result = TokenParser.Parse(input);
        Assert.That(result.Tokens.Count, Is.EqualTo(2));
        AssertTokensSame(result.Tokens[1], new Token(
            new TextSpan(13, 15),
            new ActionToken(new TextSpan(14, 15), ActionTokenKind.Expression),
            null,
            null,
            null));
    }
    
    // [Test]
    // public void ThreeFullTokens()
    // {
    //     var result = TokenParser2.Parse("/col1==val1 /col2==val2 blah /col3==val3");
    //     Assert.That(result.Count, Is.EqualTo(3));
    //     Assert.That(result[0], Is.EqualTo(new Token(
    //         new Value(new Pos(1, 4), "col1", true),
    //         new Operator(new Pos(5, 6), TokenOperator.Equals, true, true),
    //         new Value(new Pos(7, 10), "val1", true))));
    //     Assert.That(result[1], Is.EqualTo(new Token(
    //         new Value(new Pos(13, 16), "col2", true),
    //         new Operator(new Pos(17, 18), TokenOperator.Equals, true, true),
    //         new Value(new Pos(19, 22), "val2", true))));
    //     Assert.That(result[2], Is.EqualTo(new Token(
    //         new Value(new Pos(30, 33), "col3", true),
    //         new Operator(new Pos(34, 35), TokenOperator.Equals, true, true),
    //         new Value(new Pos(36, 39), "val3", false))));
    // }
    
    // [Test]
    // public void IncompleteColumn()
    // {
    //     var result = TokenParser2.Parse("/col1==val1 /col2==val2 blah /col3");
    //     Assert.That(result.Count, Is.EqualTo(3));
    //     Assert.That(result[0], Is.EqualTo(new Token(
    //         new Value(new Pos(1, 4), "col1", true),
    //         new Operator(new Pos(5, 6), TokenOperator.Equals, true, true),
    //         new Value(new Pos(7, 10), "val1", true))));
    //     Assert.That(result[1], Is.EqualTo(new Token(
    //         new Value(new Pos(13, 16), "col2", true),
    //         new Operator(new Pos(17, 18), TokenOperator.Equals, true, true),
    //         new Value(new Pos(19, 22), "val2", true))));
    //     Assert.That(result[2], Is.EqualTo(new Token(
    //         new Value(new Pos(30, 33), "col3", false),
    //         null,
    //         null)));
    // }
    //
    // [Test]
    // public void DoubleQuotedColumn()
    // {
    //     var result = TokenParser2.Parse("/\"col 1\"==val1 /\"col2 \\\"\"==val2 blah /col3");
    //     Assert.That(result.Count, Is.EqualTo(3));
    //     Assert.That(result[0], Is.EqualTo(new Token(
    //         new Value(new Pos(1, 7), "col 1", true),
    //         new Operator(new Pos(8, 9), TokenOperator.Equals, true, true),
    //         new Value(new Pos(10, 13), "val1", true))));
    //     Assert.That(result[1], Is.EqualTo(new Token(
    //         new Value(new Pos(16, 24), "col2 \"", true),
    //         new Operator(new Pos(25, 26), TokenOperator.Equals, true, true),
    //         new Value(new Pos(27, 30), "val2", true))));
    //     Assert.That(result[2], Is.EqualTo(new Token(
    //         new Value(new Pos(38, 41), "col3", false),
    //         null,
    //         null)));
    // }
    //
    // [Test]
    // public void DoubleQuotedValue()
    // {
    //     var result = TokenParser2.Parse("/col1==\"val 1\" /col2==\"val \\\"2\" blah /col3==val3");
    //     Assert.That(result.Count, Is.EqualTo(3));
    //     Assert.That(result[0], Is.EqualTo(new Token(
    //         new Value(new Pos(1, 4), "col1", true),
    //         new Operator(new Pos(5, 6), TokenOperator.Equals, true, true),
    //         new Value(new Pos(7, 13), "val 1", true))));
    //     Assert.That(result[1], Is.EqualTo(new Token(
    //         new Value(new Pos(16, 19), "col2", true),
    //         new Operator(new Pos(20, 21), TokenOperator.Equals, true, true),
    //         new Value(new Pos(22, 30), "val \"2", true))));
    //     Assert.That(result[2], Is.EqualTo(new Token(
    //         new Value(new Pos(38, 41), "col3", true),
    //         new Operator(new Pos(42, 43), TokenOperator.Equals, true, true),
    //         new Value(new Pos(44, 47), "val3", false))));
    // }
    //
    // [Test]
    // public void InCompleteDoubleQuotedColumn()
    // {
    //     var result = TokenParser2.Parse("/\"col 1\"==val1 /\"col2 \\\" blah");
    //     Assert.That(result.Count, Is.EqualTo(2));
    //     Assert.That(result[0], Is.EqualTo(new Token(
    //         new Value(new Pos(1, 7), "col 1", true),
    //         new Operator(new Pos(8, 9), TokenOperator.Equals, true, true),
    //         new Value(new Pos(10, 13), "val1", true))));
    //     Assert.That(result[1], Is.EqualTo(new Token(
    //         new Value(new Pos(16, 28), "col2 \" blah", false),
    //         null,
    //         null)));
    // }
}

// [TestFixture]
// public class SlashColonTokenParserTests
// {
//     [Test]
//     public void Slash()
//     {
//         var str = "/";
//         var result = SlashColonTokenParser.Extract(str, str.Length - 1);
//         Assert.That(result.Tokens.Count, Is.EqualTo(0));
//         Assert.That(result.ParsedSearchString, Is.EqualTo(string.Empty));
//     }
//     
//     [Test]
//     public void SlashAndSpace()
//     {
//         var str = "/ ";
//         var result = SlashColonTokenParser.Extract(str, str.Length - 1);
//         Assert.That(result.Tokens.Count, Is.EqualTo(0));
//         Assert.That(result.ParsedSearchString, Is.EqualTo(str));
//     }
//     
//     [Test]
//     public void SlashOnSecondToken()
//     {
//         var str = "/:foo==bar /";
//         var result = SlashColonTokenParser.Extract(str, str.Length - 1);
//         Assert.That(result.Tokens.Count, Is.EqualTo(1));
//         Assert.That(result.ParsedSearchString, Is.EqualTo(string.Empty));
//     }
//     
//     [Test]
//     public void SlashAndSpaceOnSecondToken()
//     {
//         var str = "/:foo==bar / ";
//         var result = SlashColonTokenParser.Extract(str, str.Length - 1);
//         Assert.That(result.Tokens.Count, Is.EqualTo(1));
//         Assert.That(result.ParsedSearchString, Is.EqualTo("/ "));
//     }
//     
//     [Test]
//     public void SlashAndSpaceOnMiddleToken()
//     {
//         var str = "/:foo==bar / /:col1==val1";
//         var result = SlashColonTokenParser.Extract(str, str.Length - 1);
//         Assert.That(result.Tokens.Count, Is.EqualTo(2));
//         Assert.That(result.ParsedSearchString, Is.EqualTo("/ "));
//     }
//     
//     [Test]
//     public void SlashAndNonColon()
//     {
//         var str = "/a";
//         var result = SlashColonTokenParser.Extract(str, str.Length - 1);
//         Assert.That(result.Tokens.Count, Is.EqualTo(0));
//         Assert.That(result.ParsedSearchString, Is.EqualTo("/a"));
//     }
//     
//     [Test]
//     public void SlashAndColon()
//     {
//         var str = "/:";
//         var result = SlashColonTokenParser.Extract(str, str.Length - 1);
//         Assert.That(result.Tokens.Count, Is.EqualTo(1));
//         Assert.That(result.Tokens[0].Value, Is.EqualTo(string.Empty));
//         Assert.That(result.ParsedSearchString, Is.EqualTo(string.Empty));
//     }
//     
//     [Test]
//     public void Simple()
//     {
//         var str = "/:foo=bar";
//         var result = SlashColonTokenParser.Extract(str, str.Length - 1);
//         Assert.That(result.Tokens.Count, Is.EqualTo(1));
//         Assert.That(result.Tokens[0].Value, Is.EqualTo("foo=bar"));
//         Assert.That(result.ParsedSearchString, Is.EqualTo(string.Empty));
//     }
//     
//     [Test]
//     public void SimpleDoubleQuotes()
//     {
//         var str = "/:foo=\"bar baz\"";
//         var result = SlashColonTokenParser.Extract(str, str.Length - 1);
//         Assert.That(result.Tokens.Count, Is.EqualTo(1));
//         Assert.That(result.Tokens[0].Value, Is.EqualTo("foo=bar baz"));
//         Assert.That(result.ParsedSearchString, Is.EqualTo(string.Empty));
//     }
//     
//     [Test]
//     public void SimpleNonClosedDoubleQuotes()
//     {
//         var str = "/:foo=\"bar baz";
//         var result = SlashColonTokenParser.Extract(str, str.Length - 1);
//         Assert.That(result.Tokens.Count, Is.EqualTo(1));
//         Assert.That(result.Tokens[0].Value, Is.EqualTo("foo=bar baz"));
//         Assert.That(result.ParsedSearchString, Is.EqualTo(string.Empty));
//     }
//     
//     [Test]
//     public void SimpleDoubleQuotesAndEscapedDoubleQuotes()
//     {
//         var str = "/:foo=\"\\\"bar\\\" baz\"";
//         var result = SlashColonTokenParser.Extract(str, str.Length - 1);
//         Assert.That(result.Tokens.Count, Is.EqualTo(1));
//         Assert.That(result.Tokens[0].Value, Is.EqualTo("foo=\"bar\" baz"));
//         Assert.That(result.ParsedSearchString, Is.EqualTo(string.Empty));
//     }
//     
//     [Test]
//     public void KeyOnly()
//     {
//         var str = "/:foo";
//         var result = SlashColonTokenParser.Extract(str, str.Length - 1);
//         Assert.That(result.Tokens.Count, Is.EqualTo(1));
//         Assert.That(result.Tokens[0].Value, Is.EqualTo("foo"));
//         Assert.That(result.ParsedSearchString, Is.EqualTo(string.Empty));
//     }
//     
//     [Test]
//     public void KeyAndOperator()
//     {
//         var str = "/:foo=";
//         var result = SlashColonTokenParser.Extract(str, str.Length - 1);
//         Assert.That(result.Tokens.Count, Is.EqualTo(1));
//         Assert.That(result.Tokens[0].Value, Is.EqualTo("foo="));
//         Assert.That(result.ParsedSearchString, Is.EqualTo(string.Empty));
//     }
//     
//     [Test]
//     public void KeyValueAndOperatorAndSearchStringAtEnd()
//     {
//         var str = "/:foo=bar test string";
//         var result = SlashColonTokenParser.Extract(str, str.Length - 1);
//         Assert.That(result.Tokens.Count, Is.EqualTo(1));
//         Assert.That(result.Tokens[0].Value, Is.EqualTo("foo=bar"));
//         Assert.That(result.ParsedSearchString, Is.EqualTo("test string"));
//     }
//     
//     [Test]
//     public void KeyValueWithDoubleQuotesAndOperatorAndSearchStringAtEnd()
//     {
//         var str = "/:foo=\"bar baz\" test string";
//         var result = SlashColonTokenParser.Extract(str, str.Length - 1);
//         Assert.That(result.Tokens.Count, Is.EqualTo(1));
//         Assert.That(result.Tokens[0].Value, Is.EqualTo("foo=bar baz"));
//         Assert.That(result.ParsedSearchString, Is.EqualTo("test string"));
//     }
//     
//     [Test]
//     public void KeyValueAndOperatorAndSearchStringAtStart()
//     {
//         var str = "test string /:foo=bar";
//         var result = SlashColonTokenParser.Extract(str, str.Length - 1);
//         Assert.That(result.Tokens.Count, Is.EqualTo(1));
//         Assert.That(result.Tokens[0].Value, Is.EqualTo("foo=bar"));
//         Assert.That(result.ParsedSearchString, Is.EqualTo("test string"));
//     }
//     
//     [Test]
//     public void KeyValueAndOperatorAndSearchStringAtStartNoSpace()
//     {
//         var str = "test string/:foo=bar";
//         var result = SlashColonTokenParser.Extract(str, str.Length - 1);
//         Assert.That(result.Tokens.Count, Is.EqualTo(0));
//         Assert.That(result.ParsedSearchString, Is.EqualTo("test string"));
//     }
//     
//     [Test]
//     public void MultipleKeyValues()
//     {
//         var str = "/:foo=bar /:col1=val1";
//         var result = SlashColonTokenParser.Extract(str, str.Length - 1);
//         Assert.That(result.Tokens.Count, Is.EqualTo(2));
//         Assert.That(result.Tokens[0].Value, Is.EqualTo("foo=bar"));
//         Assert.That(result.Tokens[1].Value, Is.EqualTo("col1=val1"));
//         Assert.That(result.ParsedSearchString, Is.EqualTo(string.Empty));
//     }
//     
//     [Test]
//     public void MultipleKeyValuesWithDoubleQuotes()
//     {
//         var str = "/:foo=\"bar baz\" /:col1=\"val 1\"";
//         var result = SlashColonTokenParser.Extract(str, str.Length - 1);
//         Assert.That(result.Tokens.Count, Is.EqualTo(2));
//         Assert.That(result.Tokens[0].Value, Is.EqualTo("foo=bar baz"));
//         Assert.That(result.Tokens[1].Value, Is.EqualTo("col1=val 1"));
//         Assert.That(result.ParsedSearchString, Is.EqualTo(string.Empty));
//     }
//     
//     [Test]
//     public void MultipleKeyValuesWithNonClosedDoubleQuotes()
//     {
//         var str = "/:foo=\"bar baz\" /:col1=\"val 1 test";
//         var result = SlashColonTokenParser.Extract(str, str.Length - 1);
//         Assert.That(result.Tokens.Count, Is.EqualTo(2));
//         Assert.That(result.Tokens[0].Value, Is.EqualTo("foo=bar baz"));
//         Assert.That(result.Tokens[1].Value, Is.EqualTo("col1=val 1 test"));
//         Assert.That(result.ParsedSearchString, Is.EqualTo(string.Empty));
//     }
//     
//     [Test]
//     public void MultipleKeyValuesAtEnd()
//     {
//         var str = "/:foo=bar /:col1=val1 test string";
//         var result = SlashColonTokenParser.Extract(str, str.Length - 1);
//         Assert.That(result.Tokens.Count, Is.EqualTo(2));
//         Assert.That(result.Tokens[0].Value, Is.EqualTo("foo=bar"));
//         Assert.That(result.Tokens[1].Value, Is.EqualTo("col1=val1"));
//         Assert.That(result.ParsedSearchString, Is.EqualTo("test string"));
//     }
//     
//     [Test]
//     public void MultipleKeyValuesAtMiddle()
//     {
//         var str = "/:foo=bar test string /:col1=val1";
//         var result = SlashColonTokenParser.Extract(str, str.Length - 1);
//         Assert.That(result.Tokens.Count, Is.EqualTo(2));
//         Assert.That(result.Tokens[0].Value, Is.EqualTo("foo=bar"));
//         Assert.That(result.Tokens[1].Value, Is.EqualTo("col1=val1"));
//         Assert.That(result.ParsedSearchString, Is.EqualTo("test string"));
//     }
//     
//     [Test]
//     public void MultipleKeyValuesAtMiddleAndEnd()
//     {
//         var str = "/:foo=bar test string /:col1=val1 more search";
//         var result = SlashColonTokenParser.Extract(str, str.Length - 1);
//         Assert.That(result.Tokens.Count, Is.EqualTo(2));
//         Assert.That(result.Tokens[0].Value, Is.EqualTo("foo=bar"));
//         Assert.That(result.Tokens[1].Value, Is.EqualTo("col1=val1"));
//         Assert.That(result.ParsedSearchString, Is.EqualTo("test string more search"));
//     }
//     
//     [Test]
//     public void MultipleKeyValuesAtMiddleStartAndEnd()
//     {
//         var str = "/:foo=bar test string /:col1=val1 more search";
//         var result = SlashColonTokenParser.Extract(str, str.Length - 1);
//         Assert.That(result.Tokens.Count, Is.EqualTo(2));
//         Assert.That(result.Tokens[0].Value, Is.EqualTo("foo=bar"));
//         Assert.That(result.Tokens[1].Value, Is.EqualTo("col1=val1"));
//         Assert.That(result.ParsedSearchString, Is.EqualTo("test string more search"));
//     }
//     
//     [Test]
//     public void ItSetsTheCorrectCursorPosWhenThereIsOneTokenAndItIsAtTheEnd()
//     {
//         var str = "/:foo=bar";
//         var result = SlashColonTokenParser.Extract(str, str.Length - 1);
//         Assert.That(result.Tokens[0].CursorPos, Is.EqualTo("foo=bar".Length - 1));
//     }
//     
//     [Test]
//     public void ItSetsTheCorrectCursorPosWhenThereIsOneTokenAndItIsAtTheMiddle()
//     {
//         var str = "/:foo=bar";
//         var result = SlashColonTokenParser.Extract(str, str.Length - 4);
//         Assert.That(result.Tokens[0].CursorPos, Is.EqualTo("foo=bar".Length - 4));
//     }
//     
//     [Test]
//     public void ItSetsTheCorrectCursorPosWhenThereIsOneTokenAndItIsAtTheSlash()
//     {
//         var str = "/:foo=bar";
//         var result = SlashColonTokenParser.Extract(str, 0);
//         Assert.That(result.Tokens[0].CursorPos, Is.EqualTo(null));
//     }
//     
//     [Test]
//     public void ItSetsTheCorrectCursorPosWhenThereIsOneTokenAndItIsAtTheSemiColor()
//     {
//         var str = "/:foo=bar";
//         var result = SlashColonTokenParser.Extract(str, 1);
//         Assert.That(result.Tokens[0].CursorPos, Is.EqualTo(null));
//     }
//     
//     [Test]
//     public void ItSetsTheCorrectCursorPosWhenThereIsOneTokenAndItIsAtTheStartOfTheToken()
//     {
//         var str = "/:foo=bar";
//         var result = SlashColonTokenParser.Extract(str, 2);
//         Assert.That(result.Tokens[0].CursorPos, Is.EqualTo(0));
//     }
//     
//     [Test]
//     public void ItSetsTheCorrectCursorPosWhenThereIsOneTokenAndItIsAfterTheFirstSpace()
//     {
//         var str = "/:foo=bar ";
//         var result = SlashColonTokenParser.Extract(str, str.Length - 1);
//         Assert.That(result.Tokens[0].CursorPos, Is.EqualTo(null));
//     }
//     
//     [Test]
//     public void ItSetsTheCorrectCursorPosWhenThereIsOneTokenItHasDoubleQuotesAtEnd()
//     {
//         var str = "/:foo=\"bar baz\"";
//         var result = SlashColonTokenParser.Extract(str, str.Length - 1);
//         Assert.That(result.Tokens[0].CursorPos, Is.EqualTo("foo=bar baz".Length - 1));
//     }
//     
//     [Test]
//     public void ItSetsTheCorrectCursorPosWhenThereIsOneTokenItHasDoubleQuotesAtCenter()
//     {
//         var str = "/:foo=\"bar baz\"";
//         var result = SlashColonTokenParser.Extract(str, str.Length - 3);
//         Assert.That(result.Tokens[0].CursorPos, Is.EqualTo("foo=bar baz".Length - 2));
//     }
//     
//     [Test]
//     public void ItSetsTheCorrectCursorPosWhenThereIsOneTokenItHasDoubleQuotesAtStart()
//     {
//         var str = "/:foo=\"bar baz\"";
//         var result = SlashColonTokenParser.Extract(str, 2);
//         Assert.That(result.Tokens[0].CursorPos, Is.EqualTo(0));
//     }
//     
//     [Test]
//     public void ItSetsTheCorrectCursorPosWhenThereAreTwoTokensEndOfFirstToken()
//     {
//         var str = "/:foo=bar /:col1=va1";
//         var result = SlashColonTokenParser.Extract(str, "/:foo=bar".Length - 1);
//         Assert.That(result.Tokens[0].CursorPos, Is.EqualTo("foo=bar".Length - 1));
//         Assert.That(result.Tokens[1].CursorPos, Is.EqualTo(null));
//     }
//     
//     [Test]
//     public void ItSetsTheCorrectCursorPosWhenThereAreTwoTokensSpaceAfterFirstToken()
//     {
//         var str = "/:foo=bar /:col1=va1";
//         var result = SlashColonTokenParser.Extract(str, "/:foo=bar".Length - 1 + 1);
//         Assert.That(result.Tokens[0].CursorPos, Is.EqualTo(null));
//         Assert.That(result.Tokens[1].CursorPos, Is.EqualTo(null));
//     }
//     
//     [Test]
//     public void ItSetsTheCorrectCursorPosWhenThereAreTwoTokensColonOfSecondToken()
//     {
//         var str = "/:foo=bar /:col1=va1";
//         var result = SlashColonTokenParser.Extract(str, "/:foo=bar".Length - 1 + 2);
//         Assert.That(result.Tokens[0].CursorPos, Is.EqualTo(null));
//         Assert.That(result.Tokens[1].CursorPos, Is.EqualTo(null));
//     }
//     
//     [Test]
//     public void ItSetsTheCorrectCursorPosWhenThereAreTwoTokensStartOfSecondToken()
//     {
//         var str = "/:foo=bar /:col1=va1";
//         var result = SlashColonTokenParser.Extract(str, "/:foo=bar".Length - 1 + 3);
//         Assert.That(result.Tokens[0].CursorPos, Is.EqualTo(null));
//         Assert.That(result.Tokens[1].CursorPos, Is.EqualTo(0));
//     }
//     
//     [Test]
//     public void ItSetsTheCorrectCursorPosWhenThereAreTwoTokensMiddleOfSecondToken()
//     {
//         var str = "/:foo=bar /:col1=va1";
//         var result = SlashColonTokenParser.Extract(str, "/:foo=bar".Length - 1 + 6);
//         Assert.That(result.Tokens[0].CursorPos, Is.EqualTo(null));
//         Assert.That(result.Tokens[1].CursorPos, Is.EqualTo(2));
//     }
//     
//     [Test]
//     public void ItSetsTheCorrectCursorPosWhenThereAreTwoTokensEndOfSecondToken()
//     {
//         var str = "/:foo=bar /:col1=va1";
//         var result = SlashColonTokenParser.Extract(str, str.Length - 1);
//         Assert.That(result.Tokens[0].CursorPos, Is.EqualTo(null));
//         Assert.That(result.Tokens[1].CursorPos, Is.EqualTo("col1=val".Length - 1));
//     }
//     
//     [Test]
//     public void ItSetsTheCorrectTokenStartPos_OneToken()
//     {
//         var str = "/:foo=bar";
//         var result = SlashColonTokenParser.Extract(str, str.Length - 1);
//         Assert.That(result.Tokens[0].tokenStartPos, Is.EqualTo(2));
//     }
//     
//     [Test]
//     public void ItSetsTheCorrectTokenStartPos_TwoTokens()
//     {
//         var str = "/:foo=bar /:col1==val2";
//         var result = SlashColonTokenParser.Extract(str, str.Length - 1);
//         Assert.That(result.Tokens[0].tokenStartPos, Is.EqualTo(1));
//         Assert.That(result.Tokens[1].tokenStartPos, Is.EqualTo(12));
//     }
// }