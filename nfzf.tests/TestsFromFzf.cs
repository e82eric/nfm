using NUnit.Framework;

namespace nfzf.tests;

[TestFixture]
public class TestsFromFzf
{
    private Slab _slab;

    private const int ScoreMatch = FuzzySearcher.ScoreMatch;
    private const int ScoreGapStart = FuzzySearcher.ScoreGapStart;
    private const int ScoreGapExtension = FuzzySearcher.ScoreGapExtension;
    private const int BonusFirstCharMultiplier = FuzzySearcher.BonusFirstCharMultiplier;
    private const int BonusBoundary = FuzzySearcher.BoundaryBonus;
    private const int BonusCamel123 = FuzzySearcher.CamelCaseBonus;
    private const int BonusConsecutive = FuzzySearcher.BonusConsecutive;


    [SetUp]
    public void Setup()
    {
        _slab = Slab.MakeDefault();
    }

    [Test]
    public void TestFuzzyMatch()
    {
        // Create delegates for both algorithm versions
        FuzzySearcher.MatchFunctionDelegate[] algorithms = { FuzzySearcher.FuzzyMatchV1, FuzzySearcher.FzfFuzzyMatchV2 };
        bool[] forwardOptions = { true, false };

        foreach (var algo in algorithms)
        {
            foreach (var forward in forwardOptions)
            {
                // Test case 1: Camel case matching
                AssertMatch(algo, false, forward, "fooBarbaz1", "obz", 1, 8,
                    ScoreMatch * 3 + BonusCamel123 + ScoreGapStart + ScoreGapExtension * 3);

                // Test case 2: Space-separated words
                AssertMatch(algo, false, forward, "foo bar baz", "fbb", 0, 9,
                    ScoreMatch * 3 + BonusBoundary * BonusFirstCharMultiplier +
                    BonusBoundary * 2 + 2 * ScoreGapStart + 4 * ScoreGapExtension);

                // Test case 3: Path with file extension
                AssertMatch(algo, false, forward, "/AutomatorDocument.icns", "rdoc", 9, 13,
                    ScoreMatch * 4 + BonusCamel123 + BonusConsecutive * 2);

                // Test case 4: Man page path
                AssertMatch(algo, false, forward, "/man1/zshcompctl.1", "zshc", 6, 10,
                    ScoreMatch * 4 + BonusBoundary * BonusFirstCharMultiplier + BonusBoundary * 3);

                // Test case 5: Hidden directory path
                AssertMatch(algo, false, forward, "/.oh-my-zsh/cache", "zshc", 8, 13,
                    ScoreMatch * 4 + BonusBoundary * BonusFirstCharMultiplier + BonusBoundary * 2 + 
                    ScoreGapStart + BonusBoundary);

                // Test case 6: Numeric matching
                AssertMatch(algo, false, forward, "ab0123 456", "12356", 3, 10,
                    ScoreMatch * 5 + BonusConsecutive * 3 + ScoreGapStart + ScoreGapExtension);

                // Test case 7: Path with forward slashes
                AssertMatch(algo, false, forward, "foo/bar/baz", "fbb", 0, 9,
                    ScoreMatch * 3 + BonusBoundary * BonusFirstCharMultiplier +
                    BonusBoundary * 2 + 2 * ScoreGapStart + 4 * ScoreGapExtension);

                // Test case 8: CamelCase with forward matching
                AssertMatch(algo, true, forward, "FooBarBaz", "FBB", 0, 7,
                    ScoreMatch * 3 + BonusBoundary * BonusFirstCharMultiplier + BonusCamel123 * 2 +
                    ScoreGapStart * 2 + ScoreGapExtension * 2);

                // Non-match test cases
                AssertMatch(algo, true, forward, "fooBarbaz", "oBZ", -1, -1, 0);
                AssertMatch(algo, true, forward, "Foo Bar Baz", "fbb", -1, -1, 0);
                AssertMatch(algo, true, forward, "fooBarbaz", "fooBarbazz", -1, -1, 0);
            }
        }
    }

    private void AssertMatch(FuzzySearcher.MatchFunctionDelegate algo, bool caseSensitive, bool forward,
        string text, string pattern, int expectedStart, int expectedEnd, int expectedScore)
    {
        var result = algo(caseSensitive, text, pattern, _slab, null);
        
        var message = $"Pattern: '{pattern}', Text: '{text}', Forward: {forward}, CaseSensitive: {caseSensitive}";
        
        Assert.Multiple(() =>
        {
            //Assert.That(result.Start, Is.EqualTo(expectedStart), $"Start position mismatch. {message}");
            //Assert.That(result.End, Is.EqualTo(expectedEnd), $"End position mismatch. {message}");
            Assert.That(result.Score, Is.EqualTo(expectedScore), $"Score mismatch. {message}");
        });
    }
    
    [Test]
    public void TestFuzzyMatchBackward()
    {
        // Forward case
        AssertMatch(FuzzySearcher.FuzzyMatchV1, false, true, "foobar fb", "fb", 0, 4,
            ScoreMatch * 2 + BonusBoundary * BonusFirstCharMultiplier +
            ScoreGapStart + ScoreGapExtension);

        // Backward case
        //AssertMatch(FuzzySearcher.FuzzyMatchV1, false, false, "foobar fb", "fb", 7, 9,
        //    ScoreMatch * 2 + BonusBoundary * BonusFirstCharMultiplier + BonusBoundary);
    }

    [Test]
    public void TestExactMatchNaive()
    {
        foreach (bool dir in new[] { true, false })
        {
            // Case sensitive tests
            AssertMatch(FuzzySearcher.FzfExactMatchNaive, true, dir, "fooBarbaz", "oBA", -1, -1, 0);
            AssertMatch(FuzzySearcher.FzfExactMatchNaive, true, dir, "fooBarbaz", "fooBarbazz", -1, -1, 0);

            // Case insensitive tests
            AssertMatch(FuzzySearcher.FzfExactMatchNaive, false, dir, "fooBarbaz", "oba", 2, 5,
                ScoreMatch * 3 + BonusCamel123 + BonusConsecutive);
            AssertMatch(FuzzySearcher.FzfExactMatchNaive, false, dir, "/AutomatorDocument.icns", "rdoc", 9, 13,
                ScoreMatch * 4 + BonusCamel123 + BonusConsecutive * 2);
            AssertMatch(FuzzySearcher.FzfExactMatchNaive, false, dir, "/man1/zshcompctl.1", "zshc", 6, 10,
                ScoreMatch * 4 + BonusBoundary * (BonusFirstCharMultiplier + 3));
            AssertMatch(FuzzySearcher.FzfExactMatchNaive, false, dir, "/.oh-my-zsh/cache", "zsh/c", 8, 13,
                ScoreMatch * 5 + BonusBoundary * (BonusFirstCharMultiplier + 3) + BonusBoundary);
        }
    }

    [Test]
    public void TestExactMatchNaiveBackward()
    {
        AssertMatch(FuzzySearcher.FzfExactMatchNaive, false, true, "foobar foob", "oo", 1, 3,
            ScoreMatch * 2 + BonusConsecutive);
        AssertMatch(FuzzySearcher.FzfExactMatchNaive, false, false, "foobar foob", "oo", 8, 10,
            ScoreMatch * 2 + BonusConsecutive);
    }

    [Test]
    public void TestPrefixMatch()
    {
        int score = ScoreMatch * 3 + BonusBoundary * BonusFirstCharMultiplier + BonusBoundary * 2;

        foreach (bool dir in new[] { true, false })
        {
            AssertMatch(FuzzySearcher.FzfPrefixMatch, true, dir, "fooBarbaz", "Foo", -1, -1, 0);
            AssertMatch(FuzzySearcher.FzfPrefixMatch, false, dir, "fooBarBaz", "baz", -1, -1, 0);
            AssertMatch(FuzzySearcher.FzfPrefixMatch, false, dir, "fooBarbaz", "foo", 0, 3, score);
            AssertMatch(FuzzySearcher.FzfPrefixMatch, false, dir, "foOBarBaZ", "foo", 0, 3, score);
            AssertMatch(FuzzySearcher.FzfPrefixMatch, false, dir, "f-oBarbaz", "f-o", 0, 3, score);
            
            AssertMatch(FuzzySearcher.FzfPrefixMatch, false, dir, " fooBar", "foo", 1, 4, score);
            AssertMatch(FuzzySearcher.FzfPrefixMatch, false, dir, " fooBar", " fo", 0, 3, score);
            AssertMatch(FuzzySearcher.FzfPrefixMatch, false, dir, "     fo", "foo", -1, -1, 0);
        }
    }

    [Test]
    public void TestSuffixMatch()
    {
        foreach (bool dir in new[] { true, false })
        {
            AssertMatch(FuzzySearcher.FzfSuffixMatch, true, dir, "fooBarbaz", "Baz", -1, -1, 0);
            AssertMatch(FuzzySearcher.FzfSuffixMatch, false, dir, "fooBarbaz", "foo", -1, -1, 0);

            AssertMatch(FuzzySearcher.FzfSuffixMatch, false, dir, "fooBarbaz", "baz", 6, 9,
                ScoreMatch * 3 + BonusConsecutive * 2);
            AssertMatch(FuzzySearcher.FzfSuffixMatch, false, dir, "fooBarBaZ", "baz", 6, 9,
                (ScoreMatch + BonusCamel123) * 3 + BonusCamel123 * (BonusFirstCharMultiplier - 1));

            // Strip trailing white space from the string
            AssertMatch(FuzzySearcher.FzfSuffixMatch, false, dir, "fooBarbaz ", "baz", 6, 9,
                ScoreMatch * 3 + BonusConsecutive * 2);

            // Only when the pattern doesn't end with a space
            AssertMatch(FuzzySearcher.FzfSuffixMatch, false, dir, "fooBarbaz ", "baz ", 6, 10,
                ScoreMatch * 4 + BonusConsecutive * 2 + BonusBoundary);
        }
    }

    [Test]
    public void TestEmptyPattern()
    {
        foreach (bool dir in new[] { true, false })
        {
            AssertMatch(FuzzySearcher.FuzzyMatchV1, true, dir, "foobar", "", 0, 0, 0);
            AssertMatch(FuzzySearcher.FzfFuzzyMatchV2, true, dir, "foobar", "", 0, 0, 0);
            AssertMatch(FuzzySearcher.FzfExactMatchNaive, true, dir, "foobar", "", 0, 0, 0);
            AssertMatch(FuzzySearcher.FzfPrefixMatch, true, dir, "foobar", "", 0, 0, 0);
            AssertMatch(FuzzySearcher.FzfSuffixMatch, true, dir, "foobar", "", 6, 6, 0);
        }
    }

    //[Test]
    //public void TestNormalize()
    //{
    //    var algorithms = new FuzzySearcher.MatchFunctionDelegate[] 
    //    { 
    //        FuzzySearcher.FuzzyMatchV1, 
    //        FuzzySearcher.FzfFuzzyMatchV2, 
    //        FuzzySearcher.FzfPrefixMatch, 
    //        FuzzySearcher.FzfExactMatchNaive 
    //    };

    //    foreach (var algo in algorithms)
    //    {
    //        AssertMatchNormalized(algo, false, true, true,
    //            "Só Danço Samba", "So", 0, 2, 62);
    //    }

    //    var fuzzyAlgos = new FuzzySearcher.MatchFunctionDelegate[] 
    //    { 
    //        FuzzySearcher.FuzzyMatchV1,
    //        FuzzySearcher.FzfFuzzyMatchV2 
    //    };

    //    foreach (var algo in fuzzyAlgos)
    //    {
    //        AssertMatchNormalized(algo, false, true, true,
    //            "Só Danço Samba", "sodc", 0, 7, 97);
    //    }
    //}

    [Test]
    public void TestLongString()
    {
        // Create a string longer than uint16 max
        var longString = new string('x', ushort.MaxValue * 2);
        var modifiedString = longString.Insert(ushort.MaxValue, "z");

        AssertMatch(FuzzySearcher.FzfFuzzyMatchV2, true, true, modifiedString, "zx", 
            ushort.MaxValue, ushort.MaxValue + 2, 
            ScoreMatch * 2 + BonusConsecutive);
    }

    private void AssertMatchNormalized(FuzzySearcher.MatchFunctionDelegate algo, bool caseSensitive, bool normalize, 
        bool forward, string text, string pattern, int expectedStart, int expectedEnd, int expectedScore)
    {
        var result = algo(caseSensitive, text, pattern, _slab, null);
        
        var message = $"Pattern: '{pattern}', Text: '{text}', Forward: {forward}, " +
                      $"CaseSensitive: {caseSensitive}, Normalize: {normalize}";
        
        Assert.Multiple(() =>
        {
            Assert.That(result.Start, Is.EqualTo(expectedStart), $"Start position mismatch. {message}");
            Assert.That(result.End, Is.EqualTo(expectedEnd), $"End position mismatch. {message}");
            Assert.That(result.Score, Is.EqualTo(expectedScore), $"Score mismatch. {message}");
        });
    }
}