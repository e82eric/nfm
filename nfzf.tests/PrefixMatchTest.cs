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
        var _input = "aaa";      // Input string of length 10,000
        var _pattern = "aa";     // Pattern string of length 1,000
        var inputSpan = _input.AsSpan();
        var patternSpan = _pattern.AsSpan();
        var pos = new List<int>();
        FuzzySearcher.FzfPrefixMatch(true, inputSpan, patternSpan, _slab, pos);
        Assert.That(pos.Count, Is.EqualTo(2));
        Assert.That(pos[0], Is.EqualTo(0));
        Assert.That(pos[1], Is.EqualTo(1));
    }
}