﻿using NUnit.Framework;

namespace nfzf.tests;

public class SuffixMatchTest
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
        FuzzySearcher.FzfSuffixMatch(true, inputSpan, patternSpan, _slab, pos);
        Assert.That(2, Is.EqualTo(pos.Count));
        Assert.That(2, Is.EqualTo(pos[1]));
        Assert.That(1, Is.EqualTo(pos[0]));
    }
}