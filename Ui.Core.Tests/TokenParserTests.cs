using nfm.Win32Ui;
using NUnit.Framework;

namespace nfm.Ui.Core.Tests;

[TestFixture]
public class TokenParserTests
{
    [Test]
    public void SimpleKeyValuePair()
    {
        var str = new ParsedToken("foo==bar", null, 0);
        var result = TokenParser.Parse(str);
        Assert.That(result.Key, Is.EqualTo("foo"));
        Assert.That(result.TokenOperator, Is.EqualTo(TokenOperator.Equals));
        Assert.That(result.Values.Count, Is.EqualTo(1));
        Assert.That(result.Values[0], Is.EqualTo("bar"));
    }
    
    [Test]
    public void SimpleKeyValuePairWithSpace()
    {
        var str = new ParsedToken("foo==bar baz", null, 0);
        var result = TokenParser.Parse(str);
        Assert.That(result.Key, Is.EqualTo("foo"));
        Assert.That(result.TokenOperator, Is.EqualTo(TokenOperator.Equals));
        Assert.That(result.Values.Count, Is.EqualTo(1));
        Assert.That(result.Values[0], Is.EqualTo("bar baz"));
    }
    
    [Test]
    public void SimpleKeyValuePairWithSpaceAndDoubleQuotes()
    {
        var str = new ParsedToken("foo==bar \"baz\"", null, 0);
        var result = TokenParser.Parse(str);
        Assert.That(result.Key, Is.EqualTo("foo"));
        Assert.That(result.TokenOperator, Is.EqualTo(TokenOperator.Equals));
        Assert.That(result.Values.Count, Is.EqualTo(1));
        Assert.That(result.Values[0], Is.EqualTo("bar \"baz\""));
    }
    
    [Test]
    public void MultipleValues()
    {
        var str = new ParsedToken("foo==bar,baz", null, 0);
        var result = TokenParser.Parse(str);
        Assert.That(result.Key, Is.EqualTo("foo"));
        Assert.That(result.TokenOperator, Is.EqualTo(TokenOperator.Equals));
        Assert.That(result.Values.Count, Is.EqualTo(2));
        Assert.That(result.Values[0], Is.EqualTo("bar"));
        Assert.That(result.Values[1], Is.EqualTo("baz"));
    }
    
    [Test]
    public void MultipleValues_2()
    {
        var str = new ParsedToken("foo==bar,baz,bat", null, 0);
        var result = TokenParser.Parse(str);
        Assert.That(result.Key, Is.EqualTo("foo"));
        Assert.That(result.TokenOperator, Is.EqualTo(TokenOperator.Equals));
        Assert.That(result.Values.Count, Is.EqualTo(3));
        Assert.That(result.Values[0], Is.EqualTo("bar"));
        Assert.That(result.Values[1], Is.EqualTo("baz"));
        Assert.That(result.Values[2], Is.EqualTo("bat"));
    }
    
    [Test]
    public void MultipleValues_WithSpaces()
    {
        var str = new ParsedToken("foo==bar rab,baz zab,bat tab", null, 0);
        var result = TokenParser.Parse(str);
        Assert.That(result.Key, Is.EqualTo("foo"));
        Assert.That(result.TokenOperator, Is.EqualTo(TokenOperator.Equals));
        Assert.That(result.Values.Count, Is.EqualTo(3));
        Assert.That(result.Values[0], Is.EqualTo("bar rab"));
        Assert.That(result.Values[1], Is.EqualTo("baz zab"));
        Assert.That(result.Values[2], Is.EqualTo("bat tab"));
    }
    
    [Test]
    public void MultipleValues_WithSpacesAndDoubleQuotes()
    {
        var str = new ParsedToken("foo==bar rab,\"baz\" zab,bat tab", null, 0);
        var result = TokenParser.Parse(str);
        Assert.That(result.Key, Is.EqualTo("foo"));
        Assert.That(result.TokenOperator, Is.EqualTo(TokenOperator.Equals));
        Assert.That(result.Values.Count, Is.EqualTo(3));
        Assert.That(result.Values[0], Is.EqualTo("bar rab"));
        Assert.That(result.Values[1], Is.EqualTo("\"baz\" zab"));
        Assert.That(result.Values[2], Is.EqualTo("bat tab"));
    }
    
    [Test]
    public void MultipleValuesWithSpaces()
    {
        var str = new ParsedToken("foo==bar,baz", null, 0);
        var result = TokenParser.Parse(str);
        Assert.That(result.Key, Is.EqualTo("foo"));
        Assert.That(result.TokenOperator, Is.EqualTo(TokenOperator.Equals));
        Assert.That(result.Values.Count, Is.EqualTo(2));
        Assert.That(result.Values[0], Is.EqualTo("bar"));
        Assert.That(result.Values[1], Is.EqualTo("baz"));
    }
    
    [Test]
    public void NoOperator()
    {
        var str = new ParsedToken("foo", null, 0);
        var result = TokenParser.Parse(str);
        Assert.That(result.Key, Is.EqualTo("foo"));
        Assert.That(result.TokenOperator, Is.EqualTo(null));
        Assert.That(result.Values.Count, Is.EqualTo(0));
    }
    
    [Test]
    public void NoValue()
    {
        var str = new ParsedToken("foo==", null, 0);
        var result = TokenParser.Parse(str);
        Assert.That(result.Key, Is.EqualTo("foo"));
        Assert.That(result.TokenOperator, Is.EqualTo(TokenOperator.Equals));
        Assert.That(result.Values.Count, Is.EqualTo(1));
        Assert.That(result.Values[0], Is.EqualTo(String.Empty));
    }
    
    [Test]
    public void ValueAndComma()
    {
        var str = new ParsedToken("foo==bar,", null, 0);
        var result = TokenParser.Parse(str);
        Assert.That(result.Key, Is.EqualTo("foo"));
        Assert.That(result.TokenOperator, Is.EqualTo(TokenOperator.Equals));
        Assert.That(result.Values.Count, Is.EqualTo(2));
        Assert.That(result.Values[0], Is.EqualTo("bar"));
        Assert.That(result.Values[1], Is.EqualTo(String.Empty));
    }
    
    [Test]
    public void CursorPositionWhenEmpty()
    {
        var str = new ParsedToken(string.Empty, null, 0);
        var result = TokenParser.Parse(str);
        Assert.That(result.CursorPosition, Is.EqualTo(TokenCursorPosition.Key));
    }
    
    [Test]
    public void CursorPositionHalfOperator()
    {
        var str = new ParsedToken("foo=", 3, 0);
        var result = TokenParser.Parse(str);
        Assert.That(result.CursorPosition, Is.EqualTo(TokenCursorPosition.Operator));
    }
    
    [Test]
    public void CursorPositionFullOperator()
    {
        var str = new ParsedToken("foo==", 4, 0);
        var result = TokenParser.Parse(str);
        Assert.That(result.CursorPosition, Is.EqualTo(TokenCursorPosition.Value));
    }
    
    [Test]
    public void CursorPositionLessThan()
    {
        var str = new ParsedToken("foo<", 3, 0);
        var result = TokenParser.Parse(str);
        Assert.That(result.CursorPosition, Is.EqualTo(TokenCursorPosition.Value));
    }
    
    [Test]
    public void CursorPositionLessThanEqual()
    {
        var str = new ParsedToken("foo<=", 4, 0);
        var result = TokenParser.Parse(str);
        Assert.That(result.CursorPosition, Is.EqualTo(TokenCursorPosition.Value));
    }
    
    [Test]
    public void CursorPositionGreaterThan()
    {
        var str = new ParsedToken("foo>", 3, 0);
        var result = TokenParser.Parse(str);
        Assert.That(result.CursorPosition, Is.EqualTo(TokenCursorPosition.Value));
    }
    
    [Test]
    public void CursorPositionGreaterThanEqual()
    {
        var str = new ParsedToken("foo>=", 4, 0);
        var result = TokenParser.Parse(str);
        Assert.That(result.CursorPosition, Is.EqualTo(TokenCursorPosition.Value));
    }
    
    [Test]
    public void NullCursorPos()
    {
        var str = new ParsedToken("foo==bar", null, 0);
        var result = TokenParser.Parse(str);
        Assert.That(result.CursorPosition, Is.EqualTo(null));
    }
    
    [Test]
    public void CursorPosOnKey()
    {
        var str = new ParsedToken("foo==bar", 0, 0);
        var result = TokenParser.Parse(str);
        Assert.That(result.CursorPosition, Is.EqualTo(TokenCursorPosition.Key));
    }
    
    [Test]
    public void CursorPosOnOperator()
    {
        var str = new ParsedToken("foo==bar", 3, 0);
        var result = TokenParser.Parse(str);
        Assert.That(result.CursorPosition, Is.EqualTo(TokenCursorPosition.Operator));
    }
    
    [Test]
    public void CursorPosOnValue()
    {
        var str = new ParsedToken("foo==bar", 5, 0);
        var result = TokenParser.Parse(str);
        Assert.That(result.CursorPosition, Is.EqualTo(TokenCursorPosition.Value));
    }
    
    [Test]
    public void GreaterThan()
    {
        var str = new ParsedToken("foo>10 ", 6, 0);
        var result = TokenParser.Parse(str);
        Assert.That(result.TokenOperator, Is.EqualTo(TokenOperator.GreaterThan));
    }
    
    [Test]
    public void ValueCursorOnSetOneValue()
    {
        var str = new ParsedToken("foo==bar", 5, 0);
        var result = TokenParser.Parse(str);
        Assert.That(result.ValueCursorOn, Is.EqualTo(0));
    }
    
    [Test]
    public void ValueCursorOnSetTwoValues()
    {
        var str = new ParsedToken("foo==bar,baz", 5, 0);
        var result = TokenParser.Parse(str);
        Assert.That(result.ValueCursorOn, Is.EqualTo(0));
    }
    
    [Test]
    public void ValueCursorOnSetTwoValues_2()
    {
        var str = new ParsedToken("foo==bar,baz", 9, 0);
        var result = TokenParser.Parse(str);
        Assert.That(result.ValueCursorOn, Is.EqualTo(1));
    }
    
    [Test]
    public void ValueCursorNullWhenOperator()
    {
        var str = new ParsedToken("foo==bar,baz", 3, 0);
        var result = TokenParser.Parse(str);
        Assert.That(result.ValueCursorOn, Is.EqualTo(null));
    }
    
    [Test]
    public void KeyCursorNullWhenOperator()
    {
        var str = new ParsedToken("foo==bar,baz", 3, 0);
        var result = TokenParser.Parse(str);
        Assert.That(result.ValueCursorOn, Is.EqualTo(null));
    }
}