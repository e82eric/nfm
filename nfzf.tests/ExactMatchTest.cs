using NUnit.Framework;

namespace nfzf.tests;

public class ExactMatchTest
{
    private Slab _slab;

    [SetUp]
    public void Setup()
    {
        _slab = new Slab(100 * 1024 * 10000, 2048 * 100);
    }
    
    [Test]
    public void FzfExactMatchNaive()
    {
        var input = "thisaaatext";
        var pattern = "aaa";
        var inputSpan = input.AsSpan();
        var patternSpan = pattern.AsSpan();
        var pos = new List<int>();
        FuzzySearcher.FzfExactMatchNaive(true, inputSpan, patternSpan, _slab, pos);
        Assert.That(3, Is.EqualTo(pos.Count));
        Assert.That(6, Is.EqualTo(pos[2]));
        Assert.That(5, Is.EqualTo(pos[1]));
        Assert.That(4, Is.EqualTo(pos[0]));
    }
}