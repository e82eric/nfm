using NUnit.Framework;

namespace nfzf.tests;

public class PrefixMatchTest
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
        var pattern = "aa";
        var inputSpan = input.AsSpan();
        var patternSpan = pattern.AsSpan();
        var pos = new List<int>();
        FuzzySearcher.FzfPrefixMatch(true, inputSpan, patternSpan, _slab, pos);
        Assert.That(pos.Count, Is.EqualTo(2));
        Assert.That(pos[0], Is.EqualTo(0));
        Assert.That(pos[1], Is.EqualTo(1));
    }
}