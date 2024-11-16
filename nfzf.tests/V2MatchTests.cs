using NUnit.Framework;

namespace nfzf.tests;

public class V2MatchTests
{
    
    [Test]
    public void Case6()
    {
        var slab = Slab.MakeDefault();
        var pos = new List<int>();
        var result = FuzzySearcher.FzfFuzzyMatchV2(
            false,
            "C:\\Users\\JohnDoe\\Desktop\\log_93.png",
            "doe", slab, pos);
        Assert.That(result.Score, Is.EqualTo(76));
        Assert.That(pos.Count, Is.EqualTo(3));
        Assert.That(15, Is.EqualTo(pos[0]));
        Assert.That(14, Is.EqualTo(pos[1]));
        Assert.That(13, Is.EqualTo(pos[2]));
    }
    [Test]
    public void Case5()
    {
        var slab = Slab.MakeDefault();
        FuzzySearcher.FzfFuzzyMatchV2(false, "D:\\Games\\error_7371.exe", "doe", slab, null);
    }
    [Test]
    public void Case4()
    {
        var slab = Slab.MakeDefault();
        FuzzySearcher.FzfFuzzyMatchV2(false, "C:\\Windows\\System32\\info_7190.xml", "doe", slab, null);
    }
    [Test]
    public void Case3()
    {
        var slab = Slab.MakeDefault();
        FuzzySearcher.FzfFuzzyMatchV2(false, "C:\\Users\\User1\\Desktop\\debug_825.zip", "doe", slab, null);
    }
    [Test]
    public void Case2()
    {
        var slab = Slab.MakeDefault();
        FuzzySearcher.FzfFuzzyMatchV2(true, "C:\\Users\\Admin\\Downloads\\error_2473.log", "doe", slab, null);
    }
    
    [Test]
    public void Case1()
    {
        var slab = Slab.MakeDefault();
        FuzzySearcher.FzfFuzzyMatchV2(false, "aabcd", "abc", slab, null);
    }
}