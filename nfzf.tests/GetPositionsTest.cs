using NUnit.Framework;

namespace nfzf.tests;

public class GetPositionsTest
{
    private Slab _slab;

    [SetUp]
    public void Setup()
    {
        _slab = new Slab(100 * 1024 * 10000, 2048 * 100);
    }
    
    [Test]
    public void Case1()
    {
        var input = "aaa";
        var patternText = "aa";
        var inputSpan = input.AsSpan();
        var pattern = FuzzySearcher.ParsePattern(CaseMode.CaseSmart, patternText, true);
        var pos = FuzzySearcher.GetPositions(inputSpan, pattern, _slab);
        Assert.That(pos.Count, Is.EqualTo(2));
        Assert.That(pos[1], Is.EqualTo(0));
        Assert.That(pos[0], Is.EqualTo(1));
    }
    
    [Test]
    public void WithInverse()
    {
        var input = "aaa";
        var patternText = "aa !bb";
        var inputSpan = input.AsSpan();
        var pattern = FuzzySearcher.ParsePattern(CaseMode.CaseSmart, patternText, true);
        var pos = FuzzySearcher.GetPositions(inputSpan, pattern, _slab);
        Assert.That(pos.Count, Is.EqualTo(2));
        Assert.That(pos[1], Is.EqualTo(0));
        Assert.That(pos[0], Is.EqualTo(1));
    }
    
    [Test]
    public void WithInverse2()
    {
        var input = "aaabb";
        var patternText = "aa !bb";
        var inputSpan = input.AsSpan();
        var pattern = FuzzySearcher.ParsePattern(CaseMode.CaseSmart, patternText, true);
        var pos = FuzzySearcher.GetPositions(inputSpan, pattern, _slab);
        Assert.That(pos.Count, Is.EqualTo(0));
    }
    
    [Test]
    public void Execption()
    {
        var input = "c:\\Program Files\\dotnet\\sdk\\NuGetFallbackFolder\\microsoft.azure.keyvault.webkey\\2.0.7\\lib\\net452\\Microsoft.Azure.KeyVault.WebKey.xml";
        var patternText = "dotfiles";
        var inputSpan = input.AsSpan();
        var pattern = FuzzySearcher.ParsePattern(CaseMode.CaseSmart, patternText, true);
        var pos = FuzzySearcher.GetPositions(inputSpan, pattern, _slab);
        Assert.That(pos.Count, Is.EqualTo(8));
        Assert.That(pos[0], Is.EqualTo(102));
        Assert.That(pos[1], Is.EqualTo(91));
        Assert.That(pos[2], Is.EqualTo(86));
        Assert.That(pos[3], Is.EqualTo(49));
        Assert.That(pos[4], Is.EqualTo(41));
        Assert.That(pos[5], Is.EqualTo(19));
        Assert.That(pos[6], Is.EqualTo(18));
        Assert.That(pos[7], Is.EqualTo(17));
    }
    
    [Test]
    public void Exception2()
    {
        var input = @"c:\Users\eric\src\dotfiles\.git\logs\HEAD";
        var patternText = "dotfiles";
        var inputSpan = input.AsSpan();
        var pattern = FuzzySearcher.ParsePattern(CaseMode.CaseSmart, patternText, true);
        var pos = FuzzySearcher.GetPositions(inputSpan, pattern, _slab);
        Assert.That(pos.Count, Is.EqualTo(8));
        Assert.That(pos[0], Is.EqualTo(25));
        Assert.That(pos[1], Is.EqualTo(24));
        Assert.That(pos[2], Is.EqualTo(23));
        Assert.That(pos[3], Is.EqualTo(22));
        Assert.That(pos[4], Is.EqualTo(21));
        Assert.That(pos[5], Is.EqualTo(20));
        Assert.That(pos[6], Is.EqualTo(19));
        Assert.That(pos[7], Is.EqualTo(18));
    }
    
    [Test]
    public void Execption3()
    {
        var input = @"c:\Users\eric\src\dotfiles\.git\logs\HEAD";
        var patternText = "la";
        var inputSpan = input.AsSpan();
        var pattern = FuzzySearcher.ParsePattern(CaseMode.CaseSmart, patternText, true);
        var pos = FuzzySearcher.GetPositions(inputSpan, pattern, _slab);
        Assert.That(pos.Count, Is.EqualTo(2));
        Assert.That(pos[0], Is.EqualTo(39));
        Assert.That(pos[1], Is.EqualTo(32));
    }
}