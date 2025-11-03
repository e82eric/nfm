using nfm.Win32Ui;
using NUnit.Framework;

namespace nfm.Ui.Core.Tests;

[TestFixture]
public class TokenParserTests()
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
        Assert.That(result.Tokens[0], Is.EqualTo(new OnActionToken(new TextSpan(0, 1), new ActionToken(new TextSpan(1, 1), null, false))));
        Assert.That(result.SearchString, Is.EqualTo(string.Empty));
    }
    
    [Test]
    public void SlashThenSearchString()
    {
        var expectedSearchString = "Some Search String";
        var result = TokenParser.Parse("/ " + expectedSearchString);
        Assert.That(result.Tokens.Count, Is.EqualTo(1));
        Assert.That(result.Tokens[0], Is.EqualTo(new OnActionToken(new TextSpan(0, 1), new ActionToken(new TextSpan(1, 1), null, false))));
        Assert.That(result.SearchString, Is.EqualTo(expectedSearchString));
    }
    
    [Test]
    public void SearchStringThenSlash()
    {
        var expectedSearchString = "Some Search String";
        var result = TokenParser.Parse(expectedSearchString + " /");
        Assert.That(result.Tokens.Count, Is.EqualTo(1));
        Assert.That(result.Tokens[0], Is.EqualTo(new OnActionToken(new TextSpan(expectedSearchString.Length + 1, expectedSearchString.Length + 2), new ActionToken(new TextSpan(expectedSearchString.Length + 2, expectedSearchString.Length + 2), null, false))));
        Assert.That(result.SearchString, Is.EqualTo(expectedSearchString + " "));
    }
    
    [Test]
    public void SearchStringThenSlashThenMoreSearchString()
    {
        var searchString1 = "Some Search String";
        var searchString2 = "more search";
        var result = TokenParser.Parse(searchString1 + " / " + searchString2);
        Assert.That(result.Tokens.Count, Is.EqualTo(1));
        Assert.That(result.Tokens[0], Is.EqualTo(new OnActionToken(new TextSpan(searchString1.Length + 1, searchString1.Length + 2), new ActionToken(new TextSpan(searchString1.Length + 2, searchString1.Length + 2), null, false))));
        Assert.That(result.SearchString, Is.EqualTo(searchString1 + " " + searchString2));
    }
    
    [Test]
    public void SlashSpace()
    {
        var result = TokenParser.Parse("/ ");
        Assert.That(result.Tokens.Count, Is.EqualTo(1));
        Assert.That(result.Tokens[0], Is.EqualTo(new OnActionToken(new TextSpan(0, 1), new ActionToken(new TextSpan(1, 1), null, false))));
        Assert.That(result.SearchString, Is.EqualTo(string.Empty));
    }
    
    [Test]
    public void SlashColon()
    {
        var result = TokenParser.Parse("/:");
        Assert.That(result.Tokens.Count, Is.EqualTo(1));
        Assert.That(result.Tokens[0], Is.EqualTo(new OnColumnToken(new TextSpan(0, 2), new ActionToken(new TextSpan(1, 2), ActionTokenKind.Filter, true), new ValueToken(new TextSpan(2, 2), string.Empty, false))));
        Assert.That(result.SearchString, Is.EqualTo(string.Empty));
    }
    
    [Test]
    public void SingleCharColumn()
    {
        var result = TokenParser.Parse("/:a");
        Assert.That(result.Tokens.Count, Is.EqualTo(1));
        Assert.That(result.Tokens[0], Is.EqualTo(new OnColumnToken(new TextSpan(0, 3), new ActionToken(new TextSpan(1, 2), ActionTokenKind.Filter, true), new ValueToken(new TextSpan(2, 3), "a", false))));
        Assert.That(result.SearchString, Is.EqualTo(string.Empty));
    }
    
    [Test]
    public void SingleCharColumnAndSearchString()
    {
        var result = TokenParser.Parse("/:a search string");
        Assert.That(result.Tokens.Count, Is.EqualTo(1));
        Assert.That(result.Tokens[0], Is.EqualTo(new OnColumnToken(new TextSpan(0, 3), new ActionToken(new TextSpan(1, 2), ActionTokenKind.Filter, true), new ValueToken(new TextSpan(2, 3), "a", false))));
        Assert.That(result.SearchString, Is.EqualTo("search string"));
    }
    
    [Test]
    public void TwoCharColumn()
    {
        var result = TokenParser.Parse("/:ab");
        Assert.That(result.Tokens.Count, Is.EqualTo(1));
        Assert.That(result.Tokens[0], Is.EqualTo(new OnColumnToken(new TextSpan(0, 4), new ActionToken(new TextSpan(1, 2), ActionTokenKind.Filter, true), new ValueToken(new TextSpan(2, 4), "ab", false))));
        Assert.That(result.SearchString, Is.EqualTo(string.Empty));
    }
    
    [Test]
    public void ThreeCharColumn()
    {
        var result = TokenParser.Parse("/:abc");
        Assert.That(result.Tokens.Count, Is.EqualTo(1));
        Assert.That(result.Tokens[0], Is.EqualTo(new OnColumnToken(new TextSpan(0, 5), new ActionToken(new TextSpan(1, 2), ActionTokenKind.Filter, true), new ValueToken(new TextSpan(2, 5), "abc", false))));
        Assert.That(result.SearchString, Is.EqualTo(string.Empty));
    }
    
    [Test]
    public void DoubleQuoteOnlyColumn()
    {
        var result = TokenParser.Parse("/:\"");
        Assert.That(result.Tokens.Count, Is.EqualTo(1));
        Assert.That(result.Tokens[0], Is.EqualTo(new OnColumnToken(new TextSpan(0, 3), new ActionToken(new TextSpan(1, 2), ActionTokenKind.Filter, true), new ValueToken(new TextSpan(2, 3), string.Empty, false))));
        Assert.That(result.SearchString, Is.EqualTo(string.Empty));
    }
    
    [Test]
    public void DoubleQuoteAndSingleCharColumn()
    {
        var result = TokenParser.Parse("/:\"a");
        Assert.That(result.Tokens.Count, Is.EqualTo(1));
        Assert.That(result.Tokens[0], Is.EqualTo(new OnColumnToken(new TextSpan(0, 4), new ActionToken(new TextSpan(1, 2), ActionTokenKind.Filter, true), new ValueToken(new TextSpan(2, 4), "a", false))));
        Assert.That(result.SearchString, Is.EqualTo(string.Empty));
    }
    
    [Test]
    public void SingleDoubleQuoteAndMultipleWordColumn()
    {
        var result = TokenParser.Parse("/:\"a search string");
        Assert.That(result.Tokens.Count, Is.EqualTo(1));
        Assert.That(result.Tokens[0], Is.EqualTo(new OnColumnToken(new TextSpan(0, 18), new ActionToken(new TextSpan(1, 2), ActionTokenKind.Filter, true), new ValueToken(new TextSpan(2, 18), "a search string", false))));
        Assert.That(result.SearchString, Is.EqualTo(string.Empty));
    }
    
    [Test]
    public void SingleCharSurroundedByDoubleQuotesColumn()
    {
        var result = TokenParser.Parse("/:\"a\"");
        Assert.That(result.Tokens.Count, Is.EqualTo(1));
        Assert.That(result.Tokens[0], Is.EqualTo(new OnOperatorToken(new TextSpan(0, 5),
            new ActionToken(new TextSpan(1, 2), ActionTokenKind.Filter, true),
            new ValueToken(new TextSpan(2, 5), "a", true),
            new OperatorToken(new TextSpan(5, 5), null, false, false))));
        Assert.That(result.SearchString, Is.EqualTo(string.Empty));
    }
    
    [Test]
    public void SingleCharSurroundedByDoubleQuotesColumnAndSearchString()
    {
        var result = TokenParser.Parse("/:\"a\" search string");
        Assert.That(result.Tokens.Count, Is.EqualTo(1));
        Assert.That(result.Tokens[0], Is.EqualTo(new OnOperatorToken(new TextSpan(0, 5), new ActionToken(new TextSpan(1, 2), ActionTokenKind.Filter, true), new ValueToken(new TextSpan(2, 5), "a", true), new OperatorToken(new TextSpan(5, 5), null, false, false))));
        Assert.That(result.SearchString, Is.EqualTo("search string"));
    }
    
    [Test]
    public void SingleEqualSignOperator()
    {
        var result = TokenParser.Parse("/:col1=");
        Assert.That(result.Tokens.Count, Is.EqualTo(1));
        Assert.That(result.Tokens[0], Is.EqualTo(new OnOperatorToken(new TextSpan(0, 7), new ActionToken(new TextSpan(1, 2), ActionTokenKind.Filter, true), new ValueToken(new TextSpan(2, 6), "col1", true), new OperatorToken(new TextSpan(6, 7), null, false, true))));
        Assert.That(result.SearchString, Is.EqualTo(string.Empty));
    }
    
    [Test]
    public void DoubleEqualSignOperator()
    {
        var result = TokenParser.Parse("/:col1==");
        Assert.That(result.Tokens.Count, Is.EqualTo(1));
        Assert.That(result.Tokens[0], Is.EqualTo(new OnValueToken(
            new TextSpan(0, 8), new ActionToken(new TextSpan(1, 2), ActionTokenKind.Filter, true),
            new ValueToken(new TextSpan(2, 6), "col1", true),
            new OperatorToken(new TextSpan(6, 8), OperatorTokenKind.Equals, true, true),
            new SeparatedValues([], false))));
        Assert.That(result.SearchString, Is.EqualTo(string.Empty));
    }
    
    [Test]
    public void StartOfValue()
    {
        var input = "/:col1==v";
        var result = TokenParser.Parse(input);
        Assert.That(result.Tokens.Count, Is.EqualTo(1));
        AssertTokensSame((OnValueToken)result.Tokens[0], new OnValueToken(
            new TextSpan(0, 9),
            new ActionToken(new TextSpan(1, 2), ActionTokenKind.Filter, true),
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
        AssertTokensSame((OnValueToken)result.Tokens[0], new OnValueToken(
            new TextSpan(0, 9),
            new ActionToken(new TextSpan(1, 2), ActionTokenKind.Filter, true),
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
        AssertTokensSame((OnValueToken)result.Tokens[0], new OnValueToken(
            new TextSpan(0, 17),
            new ActionToken(new TextSpan(1, 2), ActionTokenKind.Filter, true),
            new ValueToken(new TextSpan(2, 6), "col1", true),
            new OperatorToken(new TextSpan(6, 8), OperatorTokenKind.Equals, true, true),
            new SeparatedValues([
                new SeparatedValue(new ValueToken(new TextSpan(8, 13), "val1", true), new SeparatorToken(new TextSpan(12, 13), SeparatorKind.Comma)),
                new SeparatedValue(new ValueToken(new TextSpan(13, 17), "val2", true), null),
            ], true)));
    }
    
    [Test]
    public void ValueAndCommaEndOfLine()
    {
        var input = "/:col1==val1,";
        var result = TokenParser.Parse(input);
        Assert.That(result.Tokens.Count, Is.EqualTo(1));
        AssertTokensSame((OnValueToken)result.Tokens[0], new OnValueToken(
            new TextSpan(0, 13),
            new ActionToken(new TextSpan(1, 2), ActionTokenKind.Filter, true),
            new ValueToken(new TextSpan(2, 6), "col1", true),
            new OperatorToken(new TextSpan(6, 8), OperatorTokenKind.Equals, true, true),
            new SeparatedValues([
                new SeparatedValue(new ValueToken(new TextSpan(8, 13), "val1", true), new SeparatorToken(new TextSpan(12, 13), SeparatorKind.Comma)),
                new SeparatedValue(new ValueToken(new TextSpan(13, 13), string.Empty, false), null),
            ], false)));
    }
    
    [Test]
    public void ValueAndCommaWithSpaceAfter()
    {
        var input = "/:col1==val1, ";
        var result = TokenParser.Parse(input);
        Assert.That(result.Tokens.Count, Is.EqualTo(1));
        AssertTokensSame((OnValueToken)result.Tokens[0], new OnValueToken(
            new TextSpan(0, 13),
            new ActionToken(new TextSpan(1, 2), ActionTokenKind.Filter, true),
            new ValueToken(new TextSpan(2, 6), "col1", true),
            new OperatorToken(new TextSpan(6, 8), OperatorTokenKind.Equals, true, true),
            new SeparatedValues([
                new SeparatedValue(new ValueToken(new TextSpan(8, 13), "val1", true), new SeparatorToken(new TextSpan(12, 13), SeparatorKind.Comma)),
                new SeparatedValue(new ValueToken(new TextSpan(13, 13), string.Empty, false), null),
            ], false)));
    }
    
    [Test]
    public void ValueAndCommaWithSpaceAfterAndSearchString()
    {
        var input = "/:col1==val1, some search string";
        var result = TokenParser.Parse(input);
        Assert.That(result.Tokens.Count, Is.EqualTo(1));
        AssertTokensSame((OnValueToken)result.Tokens[0], new OnValueToken(
            new TextSpan(0, 13),
            new ActionToken(new TextSpan(1, 2), ActionTokenKind.Filter, true),
            new ValueToken(new TextSpan(2, 6), "col1", true),
            new OperatorToken(new TextSpan(6, 8), OperatorTokenKind.Equals, true, true),
            new SeparatedValues([
                new SeparatedValue(new ValueToken(new TextSpan(8, 13), "val1", true), new SeparatorToken(new TextSpan(12, 13), SeparatorKind.Comma)),
                new SeparatedValue(new ValueToken(new TextSpan(13, 13), string.Empty, false), null),
            ], false)));
        Assert.That(result.SearchString, Is.EqualTo("some search string"));
    }
    
    [Test]
    public void TwoValues_LastNotComplete()
    {
        var input = "/:col1==val1,val2";
        var result = TokenParser.Parse(input);
        Assert.That(result.Tokens.Count, Is.EqualTo(1));
        AssertTokensSame((OnValueToken)result.Tokens[0], new OnValueToken(
            new TextSpan(0, 17),
            new ActionToken(new TextSpan(1, 2), ActionTokenKind.Filter, true),
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
        AssertTokensSame((OnValueToken)result.Tokens[0], new OnValueToken(
            new TextSpan(0, 19),
            new ActionToken(new TextSpan(1, 2), ActionTokenKind.Filter, true),
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
        AssertTokensSame((OnValueToken)result.Tokens[0], new OnValueToken(
            new TextSpan(0, 21),
            new ActionToken(new TextSpan(1, 2), ActionTokenKind.Filter, true),
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
        AssertTokensSame((OnValueToken)result.Tokens[0], new OnValueToken(
            new TextSpan(0, 11),
            new ActionToken(new TextSpan(1, 2), ActionTokenKind.Filter, true),
            new ValueToken(new TextSpan(2, 6), "col1", true),
            new OperatorToken(new TextSpan(6, 8), OperatorTokenKind.Equals, true, true),
            new SeparatedValues([new SeparatedValue(new ValueToken(new TextSpan(8, 11), "v ", false), null)], false)));
    }

    private void AssertTokensSame(OnValueToken expected, OnValueToken actual)
    {
        Assert.That(expected.TextSpan, Is.EqualTo(actual.TextSpan));
        Assert.That(expected.Action, Is.EqualTo(actual.Action));
        Assert.That(expected.Column, Is.EqualTo(actual.Column));
        Assert.That(expected.Operator, Is.EqualTo(actual.Operator));

        Assert.That(expected.Values.Values.Count, Is.EqualTo(actual.Values.Values.Count));
        Assert.That(expected.Values.IsComplete, Is.EqualTo(actual.Values.IsComplete));

        for (int i = 0; i < expected.Values.Values.Count; i++)
        {
            Assert.That(expected.Values.Values[i], Is.EqualTo(actual.Values.Values[i]));
        }
    }
    
    [Test]
    public void CompleteValueSingleSurroundedByDoubleQuotes()
    {
        var input = "/:col1==\"v\"";
        var result = TokenParser.Parse(input);
        Assert.That(result.Tokens.Count, Is.EqualTo(1));
        AssertTokensSame((OnValueToken)result.Tokens[0], new OnValueToken(
            new TextSpan(0, 11),
            new ActionToken(new TextSpan(1, 2), ActionTokenKind.Filter, true),
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
        Assert.That(result.Tokens[1], Is.EqualTo(new OnColumnToken(
            new TextSpan(13, 15),
            new ActionToken(new TextSpan(14, 15), ActionTokenKind.Filter, true),
            new ValueToken(new TextSpan(15, 15), string.Empty, false))));
    }
    
    [Test]
    public void SecondTokenIsStartOfColumn()
    {
        var input = "/:col1==val1 /:c";
        var result = TokenParser.Parse(input);
        Assert.That(result.Tokens.Count, Is.EqualTo(2));
        AssertTokensSame((OnValueToken)result.Tokens[0], new OnValueToken(
            new TextSpan(0, 12),
            new ActionToken(new TextSpan(1, 2), ActionTokenKind.Filter, true),
            new ValueToken(new TextSpan(2, 6), "col1", true),
            new OperatorToken(new TextSpan(6, 8), OperatorTokenKind.Equals, true, true),
            new SeparatedValues([
                new SeparatedValue(new ValueToken(new TextSpan(8, 12), "val1", true), null)
            ], true)));
        Assert.That(result.Tokens[1], Is.EqualTo(new OnColumnToken(
            new TextSpan(13, 16),
            new ActionToken(new TextSpan(14, 15), ActionTokenKind.Filter, true),
            new ValueToken(new TextSpan(15, 16), "c", false))));
    }
    
    [Test]
    public void SecondTokenIsStartOfColumnAndSearchString()
    {
        var input = "/:col1==val1 /:c some search string";
        var result = TokenParser.Parse(input);
        Assert.That(result.Tokens.Count, Is.EqualTo(2));
        AssertTokensSame((OnValueToken)result.Tokens[0], new OnValueToken(
            new TextSpan(0, 12),
            new ActionToken(new TextSpan(1, 2), ActionTokenKind.Filter, true),
            new ValueToken(new TextSpan(2, 6), "col1", true),
            new OperatorToken(new TextSpan(6, 8), OperatorTokenKind.Equals, true, true),
            new SeparatedValues([
                new SeparatedValue(new ValueToken(new TextSpan(8, 12), "val1", true), null)
            ], true)));
        Assert.That(result.Tokens[1], Is.EqualTo(new OnColumnToken(
            new TextSpan(13, 16),
            new ActionToken(new TextSpan(14, 15), ActionTokenKind.Filter, true),
            new ValueToken(new TextSpan(15, 16), "c", false))));
        Assert.That(result.SearchString, Is.EqualTo("some search string"));
    }
    
    [Test]
    public void SecondTokenIsStartOfOperatorAndSearchString()
    {
        var input = "/:col1==val1 /:col1= some search string";
        var result = TokenParser.Parse(input);
        Assert.That(result.Tokens.Count, Is.EqualTo(2));
        AssertTokensSame((OnValueToken)result.Tokens[0], new OnValueToken(
            new TextSpan(0, 12),
            new ActionToken(new TextSpan(1, 2), ActionTokenKind.Filter, true),
            new ValueToken(new TextSpan(2, 6), "col1", true),
            new OperatorToken(new TextSpan(6, 8), OperatorTokenKind.Equals, true, true),
            new SeparatedValues([
                new SeparatedValue(new ValueToken(new TextSpan(8, 12), "val1", true), null)
            ], true)));
        Assert.That(result.Tokens[1], Is.EqualTo(new OnOperatorToken(
            new TextSpan(13, 20),
            new ActionToken(new TextSpan(14, 15), ActionTokenKind.Filter, true),
            new ValueToken(new TextSpan(15, 19), "col1", true),
            new OperatorToken(new TextSpan(19, 20), null, false, true))));
        Assert.That(result.SearchString, Is.EqualTo("some search string"));
    }
    
    [Test]
    public void SecondTokenIsEndOfOperatorAndSearchString()
    {
        var input = "/:col1==val1 /:col1== some search string";
        var result = TokenParser.Parse(input);
        Assert.That(result.Tokens.Count, Is.EqualTo(2));
        AssertTokensSame((OnValueToken)result.Tokens[0], new OnValueToken(
            new TextSpan(0, 12),
            new ActionToken(new TextSpan(1, 2), ActionTokenKind.Filter, true),
            new ValueToken(new TextSpan(2, 6), "col1", true),
            new OperatorToken(new TextSpan(6, 8), OperatorTokenKind.Equals, true, true),
            new SeparatedValues([
                new SeparatedValue(new ValueToken(new TextSpan(8, 12), "val1", true), null)
            ], true)));
        AssertTokensSame((OnValueToken)result.Tokens[1], new OnValueToken(
            new TextSpan(13, 21),
            new ActionToken(new TextSpan(14, 15), ActionTokenKind.Filter, true),
            new ValueToken(new TextSpan(15, 19), "col1", true),
            new OperatorToken(new TextSpan(19, 21), OperatorTokenKind.Equals, true, true),
            new SeparatedValues([], false)));
        Assert.That(result.SearchString, Is.EqualTo("some search string"));
    }
    
    [Test]
    public void SecondTokenIsStartOfFirstValueAndSearchString()
    {
        var input = "/:col1==val1 /:col1==a some search string";
        var result = TokenParser.Parse(input);
        Assert.That(result.Tokens.Count, Is.EqualTo(2));
        AssertTokensSame((OnValueToken)result.Tokens[0], new OnValueToken(
            new TextSpan(0, 12),
            new ActionToken(new TextSpan(1, 2), ActionTokenKind.Filter, true),
            new ValueToken(new TextSpan(2, 6), "col1", true),
            new OperatorToken(new TextSpan(6, 8), OperatorTokenKind.Equals, true, true),
            new SeparatedValues([
                new SeparatedValue(new ValueToken(new TextSpan(8, 12), "val1", true), null)
            ], true)));
        AssertTokensSame((OnValueToken)result.Tokens[1], new OnValueToken(
            new TextSpan(13, 22),
            new ActionToken(new TextSpan(14, 15), ActionTokenKind.Filter, true),
            new ValueToken(new TextSpan(15, 19), "col1", true),
            new OperatorToken(new TextSpan(19, 21), OperatorTokenKind.Equals, true, true),
            new SeparatedValues([new SeparatedValue(new ValueToken(new TextSpan(21, 22), "a", true), null)], true)));
        Assert.That(result.SearchString, Is.EqualTo("some search string"));
    }
    
    [Test]
    public void SecondTokenMultipleValuesAndSearchString()
    {
        var input = "/:col1==val1 /:col1==val1,val2,val3 some search string";
        var result = TokenParser.Parse(input);
        Assert.That(result.Tokens.Count, Is.EqualTo(2));
        AssertTokensSame((OnValueToken)result.Tokens[0], new OnValueToken(
            new TextSpan(0, 12),
            new ActionToken(new TextSpan(1, 2), ActionTokenKind.Filter, true),
            new ValueToken(new TextSpan(2, 6), "col1", true),
            new OperatorToken(new TextSpan(6, 8), OperatorTokenKind.Equals, true, true),
            new SeparatedValues([
                new SeparatedValue(new ValueToken(new TextSpan(8, 12), "val1", true), null)
            ], true)));
        AssertTokensSame((OnValueToken)result.Tokens[1], new OnValueToken(
            new TextSpan(13, 22),
            new ActionToken(new TextSpan(14, 15), ActionTokenKind.Filter, true),
            new ValueToken(new TextSpan(15, 19), "col1", true),
            new OperatorToken(new TextSpan(19, 21), OperatorTokenKind.Equals, true, true),
            new SeparatedValues([new SeparatedValue(new ValueToken(new TextSpan(21, 22), "a", true), null)], true)));
        Assert.That(result.SearchString, Is.EqualTo("some search string"));
    }
}
