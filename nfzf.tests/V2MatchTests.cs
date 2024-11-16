using NUnit.Framework;

namespace nfzf.tests;

public class V2MatchTests
{
    private const int ScoreMatch = FuzzySearcher.ScoreMatch;
    private const int ScoreGapStart = FuzzySearcher.ScoreGapStart;
    private const int ScoreGapExtension = FuzzySearcher.ScoreGapExtension;
    private const int BonusFirstCharMultiplier = FuzzySearcher.BonusFirstCharMultiplier;
    private const int BonusBoundary = FuzzySearcher.BoundaryBonus;
    private const int BonusCamel123 = FuzzySearcher.CamelCaseBonus;
    private const int BonusConsecutive = FuzzySearcher.BonusConsecutive;
    
    [Test]
    public void Case6()
    {
        var slab = Slab.MakeDefault();
        var pos = new List<int>();
        var result = FuzzySearcher.FzfFuzzyMatchV2(
            false,
            "C:\\Users\\JohnDoe\\Desktop\\log_93.png",
            "doe", slab, pos);
        //76
        var expectedScore = ScoreMatch * 3 + BonusCamel123 * BonusFirstCharMultiplier + BonusCamel123 * 2;
        Assert.That(result.Score, Is.EqualTo(expectedScore));
        Assert.That(pos.Count, Is.EqualTo(3));
        Assert.That(15, Is.EqualTo(pos[0]));
        Assert.That(14, Is.EqualTo(pos[1]));
        Assert.That(13, Is.EqualTo(pos[2]));
    }
    [Test]
    public void Case5()
    {
        var pos = new List<int>();
        var slab = Slab.MakeDefault();
        var result = FuzzySearcher.FzfFuzzyMatchV2(false, "D:\\Games\\error_7371.exe", "doe", slab, pos);
        //50
        var expectedScore = ScoreMatch * 3 + BonusBoundary * BonusFirstCharMultiplier + ScoreGapStart + ScoreGapExtension * 10 + ScoreGapStart + ScoreGapExtension * 6 + BonusBoundary;
        Assert.That(result.Score, Is.EqualTo(expectedScore));
        Assert.That(pos.Count, Is.EqualTo(3));
        Assert.That(20, Is.EqualTo(pos[0]));
        Assert.That(12, Is.EqualTo(pos[1]));
        Assert.That(0, Is.EqualTo(pos[2]));
    }
    
    [Test]
    public void Case4()
    {
        var expectedScore = ScoreMatch * 3 + BonusConsecutive + ScoreGapStart + ScoreGapExtension * 6;
        var slab = Slab.MakeDefault();
        var pos = new List<int>();
        var result = FuzzySearcher.FzfFuzzyMatchV2(false, "C:\\Windows\\System32\\info_7190.xml", "doe", slab, pos);
        Assert.That(result.Score, Is.EqualTo(expectedScore));
        Assert.That(pos.Count, Is.EqualTo(3));
        Assert.That(15, Is.EqualTo(pos[0]));
        Assert.That(7, Is.EqualTo(pos[1]));
        Assert.That(6, Is.EqualTo(pos[2]));
    }
    
    [Test]
    public void Case3()
    {
        var expectedScore = ScoreMatch * 3 + BonusCamel123 * BonusFirstCharMultiplier + ScoreGapExtension + ScoreGapExtension * 3 + ScoreGapStart + ScoreGapExtension * 2;
        var slab = Slab.MakeDefault();
        var pos = new List<int>();
        var result = FuzzySearcher.FzfFuzzyMatchV2(false, "C:\\Users\\User1\\Desktop\\debug_825.zip", "doe", slab, pos);
        Assert.That(result.Score, Is.EqualTo(expectedScore));
        Assert.That(pos.Count, Is.EqualTo(3));
        Assert.That(24, Is.EqualTo(pos[0]));
        Assert.That(20, Is.EqualTo(pos[1]));
        Assert.That(15, Is.EqualTo(pos[2]));
    }
    
    [Test]
    public void Case2_insensitive()
    {
        var expected = ScoreMatch * 3 + BonusBoundary * BonusFirstCharMultiplier + BonusBoundary + ScoreGapStart + ScoreGapExtension * 7 + BonusBoundary;
        var slab = Slab.MakeDefault();
        var pos = new List<int>();
        var result = FuzzySearcher.FzfFuzzyMatchV2(false, "C:\\Users\\Admin\\Downloads\\error_2473.log", "doe", slab, pos);
        Assert.That(result.Score, Is.EqualTo(expected));
        Assert.That(pos.Count, Is.EqualTo(3));
        Assert.That(25, Is.EqualTo(pos[0]));
        Assert.That(16, Is.EqualTo(pos[1]));
        Assert.That(15, Is.EqualTo(pos[2]));
    }
    
    [Test]
    public void Case2()
    {
        var expected = ScoreMatch * 3 + ScoreGapStart + ScoreGapExtension * 4 + ScoreGapStart + ScoreGapExtension * 7 + BonusBoundary;
        var slab = Slab.MakeDefault();
        var pos = new List<int>();
        var result = FuzzySearcher.FzfFuzzyMatchV2(true, "C:\\Users\\Admin\\Downloads\\error_2473.log", "doe", slab, pos);
        Assert.That(result.Score, Is.EqualTo(expected));
        Assert.That(pos.Count, Is.EqualTo(3));
        Assert.That(25, Is.EqualTo(pos[0]));
        Assert.That(20, Is.EqualTo(pos[1]));
        Assert.That(10, Is.EqualTo(pos[2]));
    }
    
    [Test]
    public void Case1()
    {
        var expected = ScoreMatch * 3 + BonusConsecutive * 2;
        var slab = Slab.MakeDefault();
        var pos = new List<int>();
        var result = FuzzySearcher.FzfFuzzyMatchV2(false, "aabcd", "abc", slab, pos);
        Assert.That(result.Score, Is.EqualTo(expected));
        Assert.That(pos.Count, Is.EqualTo(3));
        Assert.That(3, Is.EqualTo(pos[0]));
        Assert.That(2, Is.EqualTo(pos[1]));
        Assert.That(0, Is.EqualTo(pos[2]));
    }
    
    [Test]
    public void Case7()
    {
        var expected = ScoreMatch * 4 + BonusConsecutive * 3;
        var slab = Slab.MakeDefault();
        var pos = new List<int>();
        var result = FuzzySearcher.FzfFuzzyMatchV2(false, "aabcd", "abcd", slab, pos);
        Assert.That(result.Score, Is.EqualTo(expected));
        Assert.That(pos.Count, Is.EqualTo(4));
        Assert.That(4, Is.EqualTo(pos[0]));
        Assert.That(3, Is.EqualTo(pos[1]));
        Assert.That(2, Is.EqualTo(pos[2]));
        Assert.That(0, Is.EqualTo(pos[3]));
    }
}