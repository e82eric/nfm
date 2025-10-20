using nfm.Win32Ui;
using NUnit.Framework;

namespace nfm.Ui.Core.Tests;

[TestFixture]
public class SlashColonTokenParserTests
{
    [Test]
    public void Slash()
    {
        var str = "/";
        var result = SlashColonTokenParser.Extract(str, str.Length - 1);
        Assert.That(result.Tokens.Count, Is.EqualTo(0));
        Assert.That(result.ParsedSearchString, Is.EqualTo(string.Empty));
    }
    
    [Test]
    public void SlashAndSpace()
    {
        var str = "/ ";
        var result = SlashColonTokenParser.Extract(str, str.Length - 1);
        Assert.That(result.Tokens.Count, Is.EqualTo(0));
        Assert.That(result.ParsedSearchString, Is.EqualTo(str));
    }
    
    [Test]
    public void SlashOnSecondToken()
    {
        var str = "/:foo==bar /";
        var result = SlashColonTokenParser.Extract(str, str.Length - 1);
        Assert.That(result.Tokens.Count, Is.EqualTo(1));
        Assert.That(result.ParsedSearchString, Is.EqualTo(string.Empty));
    }
    
    [Test]
    public void SlashAndSpaceOnSecondToken()
    {
        var str = "/:foo==bar / ";
        var result = SlashColonTokenParser.Extract(str, str.Length - 1);
        Assert.That(result.Tokens.Count, Is.EqualTo(1));
        Assert.That(result.ParsedSearchString, Is.EqualTo("/ "));
    }
    
    [Test]
    public void SlashAndSpaceOnMiddleToken()
    {
        var str = "/:foo==bar / /:col1==val1";
        var result = SlashColonTokenParser.Extract(str, str.Length - 1);
        Assert.That(result.Tokens.Count, Is.EqualTo(2));
        Assert.That(result.ParsedSearchString, Is.EqualTo("/ "));
    }
    
    [Test]
    public void SlashAndNonColon()
    {
        var str = "/a";
        var result = SlashColonTokenParser.Extract(str, str.Length - 1);
        Assert.That(result.Tokens.Count, Is.EqualTo(0));
        Assert.That(result.ParsedSearchString, Is.EqualTo("/a"));
    }
    
    [Test]
    public void SlashAndColon()
    {
        var str = "/:";
        var result = SlashColonTokenParser.Extract(str, str.Length - 1);
        Assert.That(result.Tokens.Count, Is.EqualTo(1));
        Assert.That(result.Tokens[0].Value, Is.EqualTo(string.Empty));
        Assert.That(result.ParsedSearchString, Is.EqualTo(string.Empty));
    }
    
    [Test]
    public void Simple()
    {
        var str = "/:foo=bar";
        var result = SlashColonTokenParser.Extract(str, str.Length - 1);
        Assert.That(result.Tokens.Count, Is.EqualTo(1));
        Assert.That(result.Tokens[0].Value, Is.EqualTo("foo=bar"));
        Assert.That(result.ParsedSearchString, Is.EqualTo(string.Empty));
    }
    
    [Test]
    public void SimpleDoubleQuotes()
    {
        var str = "/:foo=\"bar baz\"";
        var result = SlashColonTokenParser.Extract(str, str.Length - 1);
        Assert.That(result.Tokens.Count, Is.EqualTo(1));
        Assert.That(result.Tokens[0].Value, Is.EqualTo("foo=bar baz"));
        Assert.That(result.ParsedSearchString, Is.EqualTo(string.Empty));
    }
    
    [Test]
    public void SimpleNonClosedDoubleQuotes()
    {
        var str = "/:foo=\"bar baz";
        var result = SlashColonTokenParser.Extract(str, str.Length - 1);
        Assert.That(result.Tokens.Count, Is.EqualTo(1));
        Assert.That(result.Tokens[0].Value, Is.EqualTo("foo=bar baz"));
        Assert.That(result.ParsedSearchString, Is.EqualTo(string.Empty));
    }
    
    [Test]
    public void SimpleDoubleQuotesAndEscapedDoubleQuotes()
    {
        var str = "/:foo=\"\\\"bar\\\" baz\"";
        var result = SlashColonTokenParser.Extract(str, str.Length - 1);
        Assert.That(result.Tokens.Count, Is.EqualTo(1));
        Assert.That(result.Tokens[0].Value, Is.EqualTo("foo=\"bar\" baz"));
        Assert.That(result.ParsedSearchString, Is.EqualTo(string.Empty));
    }
    
    [Test]
    public void KeyOnly()
    {
        var str = "/:foo";
        var result = SlashColonTokenParser.Extract(str, str.Length - 1);
        Assert.That(result.Tokens.Count, Is.EqualTo(1));
        Assert.That(result.Tokens[0].Value, Is.EqualTo("foo"));
        Assert.That(result.ParsedSearchString, Is.EqualTo(string.Empty));
    }
    
    [Test]
    public void KeyAndOperator()
    {
        var str = "/:foo=";
        var result = SlashColonTokenParser.Extract(str, str.Length - 1);
        Assert.That(result.Tokens.Count, Is.EqualTo(1));
        Assert.That(result.Tokens[0].Value, Is.EqualTo("foo="));
        Assert.That(result.ParsedSearchString, Is.EqualTo(string.Empty));
    }
    
    [Test]
    public void KeyValueAndOperatorAndSearchStringAtEnd()
    {
        var str = "/:foo=bar test string";
        var result = SlashColonTokenParser.Extract(str, str.Length - 1);
        Assert.That(result.Tokens.Count, Is.EqualTo(1));
        Assert.That(result.Tokens[0].Value, Is.EqualTo("foo=bar"));
        Assert.That(result.ParsedSearchString, Is.EqualTo("test string"));
    }
    
    [Test]
    public void KeyValueWithDoubleQuotesAndOperatorAndSearchStringAtEnd()
    {
        var str = "/:foo=\"bar baz\" test string";
        var result = SlashColonTokenParser.Extract(str, str.Length - 1);
        Assert.That(result.Tokens.Count, Is.EqualTo(1));
        Assert.That(result.Tokens[0].Value, Is.EqualTo("foo=bar baz"));
        Assert.That(result.ParsedSearchString, Is.EqualTo("test string"));
    }
    
    [Test]
    public void KeyValueAndOperatorAndSearchStringAtStart()
    {
        var str = "test string /:foo=bar";
        var result = SlashColonTokenParser.Extract(str, str.Length - 1);
        Assert.That(result.Tokens.Count, Is.EqualTo(1));
        Assert.That(result.Tokens[0].Value, Is.EqualTo("foo=bar"));
        Assert.That(result.ParsedSearchString, Is.EqualTo("test string"));
    }
    
    [Test]
    public void KeyValueAndOperatorAndSearchStringAtStartNoSpace()
    {
        var str = "test string/:foo=bar";
        var result = SlashColonTokenParser.Extract(str, str.Length - 1);
        Assert.That(result.Tokens.Count, Is.EqualTo(0));
        Assert.That(result.ParsedSearchString, Is.EqualTo("test string"));
    }
    
    [Test]
    public void MultipleKeyValues()
    {
        var str = "/:foo=bar /:col1=val1";
        var result = SlashColonTokenParser.Extract(str, str.Length - 1);
        Assert.That(result.Tokens.Count, Is.EqualTo(2));
        Assert.That(result.Tokens[0].Value, Is.EqualTo("foo=bar"));
        Assert.That(result.Tokens[1].Value, Is.EqualTo("col1=val1"));
        Assert.That(result.ParsedSearchString, Is.EqualTo(string.Empty));
    }
    
    [Test]
    public void MultipleKeyValuesWithDoubleQuotes()
    {
        var str = "/:foo=\"bar baz\" /:col1=\"val 1\"";
        var result = SlashColonTokenParser.Extract(str, str.Length - 1);
        Assert.That(result.Tokens.Count, Is.EqualTo(2));
        Assert.That(result.Tokens[0].Value, Is.EqualTo("foo=bar baz"));
        Assert.That(result.Tokens[1].Value, Is.EqualTo("col1=val 1"));
        Assert.That(result.ParsedSearchString, Is.EqualTo(string.Empty));
    }
    
    [Test]
    public void MultipleKeyValuesWithNonClosedDoubleQuotes()
    {
        var str = "/:foo=\"bar baz\" /:col1=\"val 1 test";
        var result = SlashColonTokenParser.Extract(str, str.Length - 1);
        Assert.That(result.Tokens.Count, Is.EqualTo(2));
        Assert.That(result.Tokens[0].Value, Is.EqualTo("foo=bar baz"));
        Assert.That(result.Tokens[1].Value, Is.EqualTo("col1=val 1 test"));
        Assert.That(result.ParsedSearchString, Is.EqualTo(string.Empty));
    }
    
    [Test]
    public void MultipleKeyValuesAtEnd()
    {
        var str = "/:foo=bar /:col1=val1 test string";
        var result = SlashColonTokenParser.Extract(str, str.Length - 1);
        Assert.That(result.Tokens.Count, Is.EqualTo(2));
        Assert.That(result.Tokens[0].Value, Is.EqualTo("foo=bar"));
        Assert.That(result.Tokens[1].Value, Is.EqualTo("col1=val1"));
        Assert.That(result.ParsedSearchString, Is.EqualTo("test string"));
    }
    
    [Test]
    public void MultipleKeyValuesAtMiddle()
    {
        var str = "/:foo=bar test string /:col1=val1";
        var result = SlashColonTokenParser.Extract(str, str.Length - 1);
        Assert.That(result.Tokens.Count, Is.EqualTo(2));
        Assert.That(result.Tokens[0].Value, Is.EqualTo("foo=bar"));
        Assert.That(result.Tokens[1].Value, Is.EqualTo("col1=val1"));
        Assert.That(result.ParsedSearchString, Is.EqualTo("test string"));
    }
    
    [Test]
    public void MultipleKeyValuesAtMiddleAndEnd()
    {
        var str = "/:foo=bar test string /:col1=val1 more search";
        var result = SlashColonTokenParser.Extract(str, str.Length - 1);
        Assert.That(result.Tokens.Count, Is.EqualTo(2));
        Assert.That(result.Tokens[0].Value, Is.EqualTo("foo=bar"));
        Assert.That(result.Tokens[1].Value, Is.EqualTo("col1=val1"));
        Assert.That(result.ParsedSearchString, Is.EqualTo("test string more search"));
    }
    
    [Test]
    public void MultipleKeyValuesAtMiddleStartAndEnd()
    {
        var str = "/:foo=bar test string /:col1=val1 more search";
        var result = SlashColonTokenParser.Extract(str, str.Length - 1);
        Assert.That(result.Tokens.Count, Is.EqualTo(2));
        Assert.That(result.Tokens[0].Value, Is.EqualTo("foo=bar"));
        Assert.That(result.Tokens[1].Value, Is.EqualTo("col1=val1"));
        Assert.That(result.ParsedSearchString, Is.EqualTo("test string more search"));
    }
    
    [Test]
    public void ItSetsTheCorrectCursorPosWhenThereIsOneTokenAndItIsAtTheEnd()
    {
        var str = "/:foo=bar";
        var result = SlashColonTokenParser.Extract(str, str.Length - 1);
        Assert.That(result.Tokens[0].CursorPos, Is.EqualTo("foo=bar".Length - 1));
    }
    
    [Test]
    public void ItSetsTheCorrectCursorPosWhenThereIsOneTokenAndItIsAtTheMiddle()
    {
        var str = "/:foo=bar";
        var result = SlashColonTokenParser.Extract(str, str.Length - 4);
        Assert.That(result.Tokens[0].CursorPos, Is.EqualTo("foo=bar".Length - 4));
    }
    
    [Test]
    public void ItSetsTheCorrectCursorPosWhenThereIsOneTokenAndItIsAtTheSlash()
    {
        var str = "/:foo=bar";
        var result = SlashColonTokenParser.Extract(str, 0);
        Assert.That(result.Tokens[0].CursorPos, Is.EqualTo(null));
    }
    
    [Test]
    public void ItSetsTheCorrectCursorPosWhenThereIsOneTokenAndItIsAtTheSemiColor()
    {
        var str = "/:foo=bar";
        var result = SlashColonTokenParser.Extract(str, 1);
        Assert.That(result.Tokens[0].CursorPos, Is.EqualTo(null));
    }
    
    [Test]
    public void ItSetsTheCorrectCursorPosWhenThereIsOneTokenAndItIsAtTheStartOfTheToken()
    {
        var str = "/:foo=bar";
        var result = SlashColonTokenParser.Extract(str, 2);
        Assert.That(result.Tokens[0].CursorPos, Is.EqualTo(0));
    }
    
    [Test]
    public void ItSetsTheCorrectCursorPosWhenThereIsOneTokenAndItIsAfterTheFirstSpace()
    {
        var str = "/:foo=bar ";
        var result = SlashColonTokenParser.Extract(str, str.Length - 1);
        Assert.That(result.Tokens[0].CursorPos, Is.EqualTo(null));
    }
    
    [Test]
    public void ItSetsTheCorrectCursorPosWhenThereIsOneTokenItHasDoubleQuotesAtEnd()
    {
        var str = "/:foo=\"bar baz\"";
        var result = SlashColonTokenParser.Extract(str, str.Length - 1);
        Assert.That(result.Tokens[0].CursorPos, Is.EqualTo("foo=bar baz".Length - 1));
    }
    
    [Test]
    public void ItSetsTheCorrectCursorPosWhenThereIsOneTokenItHasDoubleQuotesAtCenter()
    {
        var str = "/:foo=\"bar baz\"";
        var result = SlashColonTokenParser.Extract(str, str.Length - 3);
        Assert.That(result.Tokens[0].CursorPos, Is.EqualTo("foo=bar baz".Length - 2));
    }
    
    [Test]
    public void ItSetsTheCorrectCursorPosWhenThereIsOneTokenItHasDoubleQuotesAtStart()
    {
        var str = "/:foo=\"bar baz\"";
        var result = SlashColonTokenParser.Extract(str, 2);
        Assert.That(result.Tokens[0].CursorPos, Is.EqualTo(0));
    }
    
    [Test]
    public void ItSetsTheCorrectCursorPosWhenThereAreTwoTokensEndOfFirstToken()
    {
        var str = "/:foo=bar /:col1=va1";
        var result = SlashColonTokenParser.Extract(str, "/:foo=bar".Length - 1);
        Assert.That(result.Tokens[0].CursorPos, Is.EqualTo("foo=bar".Length - 1));
        Assert.That(result.Tokens[1].CursorPos, Is.EqualTo(null));
    }
    
    [Test]
    public void ItSetsTheCorrectCursorPosWhenThereAreTwoTokensSpaceAfterFirstToken()
    {
        var str = "/:foo=bar /:col1=va1";
        var result = SlashColonTokenParser.Extract(str, "/:foo=bar".Length - 1 + 1);
        Assert.That(result.Tokens[0].CursorPos, Is.EqualTo(null));
        Assert.That(result.Tokens[1].CursorPos, Is.EqualTo(null));
    }
    
    [Test]
    public void ItSetsTheCorrectCursorPosWhenThereAreTwoTokensColonOfSecondToken()
    {
        var str = "/:foo=bar /:col1=va1";
        var result = SlashColonTokenParser.Extract(str, "/:foo=bar".Length - 1 + 2);
        Assert.That(result.Tokens[0].CursorPos, Is.EqualTo(null));
        Assert.That(result.Tokens[1].CursorPos, Is.EqualTo(null));
    }
    
    [Test]
    public void ItSetsTheCorrectCursorPosWhenThereAreTwoTokensStartOfSecondToken()
    {
        var str = "/:foo=bar /:col1=va1";
        var result = SlashColonTokenParser.Extract(str, "/:foo=bar".Length - 1 + 3);
        Assert.That(result.Tokens[0].CursorPos, Is.EqualTo(null));
        Assert.That(result.Tokens[1].CursorPos, Is.EqualTo(0));
    }
    
    [Test]
    public void ItSetsTheCorrectCursorPosWhenThereAreTwoTokensMiddleOfSecondToken()
    {
        var str = "/:foo=bar /:col1=va1";
        var result = SlashColonTokenParser.Extract(str, "/:foo=bar".Length - 1 + 6);
        Assert.That(result.Tokens[0].CursorPos, Is.EqualTo(null));
        Assert.That(result.Tokens[1].CursorPos, Is.EqualTo(2));
    }
    
    [Test]
    public void ItSetsTheCorrectCursorPosWhenThereAreTwoTokensEndOfSecondToken()
    {
        var str = "/:foo=bar /:col1=va1";
        var result = SlashColonTokenParser.Extract(str, str.Length - 1);
        Assert.That(result.Tokens[0].CursorPos, Is.EqualTo(null));
        Assert.That(result.Tokens[1].CursorPos, Is.EqualTo("col1=val".Length - 1));
    }
    
    [Test]
    public void ItSetsTheCorrectTokenStartPos_OneToken()
    {
        var str = "/:foo=bar";
        var result = SlashColonTokenParser.Extract(str, str.Length - 1);
        Assert.That(result.Tokens[0].tokenStartPos, Is.EqualTo(2));
    }
    
    [Test]
    public void ItSetsTheCorrectTokenStartPos_TwoTokens()
    {
        var str = "/:foo=bar /:col1==val2";
        var result = SlashColonTokenParser.Extract(str, str.Length - 1);
        Assert.That(result.Tokens[0].tokenStartPos, Is.EqualTo(1));
        Assert.That(result.Tokens[1].tokenStartPos, Is.EqualTo(12));
    }
}