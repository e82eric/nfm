using NUnit.Framework;

namespace nfzf.tests;

public class PatternParserTests
{
    [Test]
    public void SimplePattern()
    {
        var result = FuzzySearcher.ParsePattern(CaseMode.CaseSmart, "aaa", true);
        Assert.That(result.TermSets.Count == 1);
        Assert.That(result.TermSets[0].Terms[0].Text == "aaa");
        Assert.That(result.TermSets[0].Terms[0].Inv == false);
        Assert.That(result.TermSets[0].Terms[0].CaseSensitive == false);
        Assert.That(result.TermSets[0].Terms[0].MatchFunction == FuzzySearcher.FzfFuzzyMatchV2);
    }
    
    [Test]
    public void NegativePattern()
    {
        var result =FuzzySearcher.ParsePattern(CaseMode.CaseSmart, "!aaa", true);
        Assert.That(result.TermSets.Count == 1);
        Assert.That(result.TermSets[0].Terms[0].Text == "aaa");
        Assert.That(result.TermSets[0].Terms[0].Inv == true);
        Assert.That(result.TermSets[0].Terms[0].CaseSensitive == false);
        Assert.That(result.TermSets[0].Terms[0].MatchFunction == FuzzySearcher.FzfExactMatchNaive);
    }
    
    [Test]
    public void EndsWithPattern()
    {
        var result =FuzzySearcher.ParsePattern(CaseMode.CaseSmart, "aaa$", true);
        Assert.That(result.TermSets.Count == 1);
        Assert.That(result.TermSets[0].Terms[0].Text == "aaa");
        Assert.That(result.TermSets[0].Terms[0].Inv == false);
        Assert.That(result.TermSets[0].Terms[0].CaseSensitive == false);
        Assert.That(result.TermSets[0].Terms[0].MatchFunction == FuzzySearcher.FzfSuffixMatch);
    }
    
    [Test]
    public void DollarSignInWord()
    {
        var result =FuzzySearcher.ParsePattern(CaseMode.CaseSmart, "aa$a", true);
        Assert.That(result.TermSets.Count == 1);
        Assert.That(result.TermSets[0].Terms[0].Text == "aa$a");
        Assert.That(result.TermSets[0].Terms[0].Inv == false);
        Assert.That(result.TermSets[0].Terms[0].CaseSensitive == false);
        Assert.That(result.TermSets[0].Terms[0].MatchFunction == FuzzySearcher.FzfFuzzyMatchV2);
    }
    
    [Test]
    public void StartsWithPattern()
    {
        var result =FuzzySearcher.ParsePattern(CaseMode.CaseSmart, "^aaa", true);
        Assert.That(result.TermSets.Count == 1);
        Assert.That(result.TermSets[0].Terms[0].Text == "aaa");
        Assert.That(result.TermSets[0].Terms[0].Inv == false);
        Assert.That(result.TermSets[0].Terms[0].CaseSensitive == false);
        Assert.That(result.TermSets[0].Terms[0].MatchFunction == FuzzySearcher.FzfPrefixMatch);
    }
    
    [Test]
    public void CarretInWord()
    {
        var result =FuzzySearcher.ParsePattern(CaseMode.CaseSmart, "a^aa", true);
        Assert.That(result.TermSets.Count == 1);
        Assert.That(result.TermSets[0].Terms[0].Text == "a^aa");
        Assert.That(result.TermSets[0].Terms[0].Inv == false);
        Assert.That(result.TermSets[0].Terms[0].CaseSensitive == false);
        Assert.That(result.TermSets[0].Terms[0].MatchFunction == FuzzySearcher.FzfFuzzyMatchV2);
    }
    
    [Test]
    public void EscapedSpace()
    {
        var result =FuzzySearcher.ParsePattern(CaseMode.CaseSmart, "a\\ aa", true);
        Assert.That(result.TermSets.Count == 1);
        Assert.That(result.TermSets[0].Terms[0].Text == "a aa");
        Assert.That(result.TermSets[0].Terms[0].Inv == false);
        Assert.That(result.TermSets[0].Terms[0].CaseSensitive == false);
        Assert.That(result.TermSets[0].Terms[0].MatchFunction == FuzzySearcher.FzfFuzzyMatchV2);
    }
    
    [Test]
    public void MultipleTerms()
    {
        var result =FuzzySearcher.ParsePattern(CaseMode.CaseSmart, "aaa | bbb", true);
        Assert.That(result.TermSets.Count == 1);
        Assert.That(result.TermSets[0].Terms.Count == 2);
        
        Assert.That(result.TermSets[0].Terms[0].Text == "aaa");
        Assert.That(result.TermSets[0].Terms[0].Inv == false);
        Assert.That(result.TermSets[0].Terms[0].CaseSensitive == false);
        Assert.That(result.TermSets[0].Terms[0].MatchFunction == FuzzySearcher.FzfFuzzyMatchV2);
        
        Assert.That(result.TermSets[0].Terms[1].Text == "bbb");
        Assert.That(result.TermSets[0].Terms[1].Inv == false);
        Assert.That(result.TermSets[0].Terms[1].CaseSensitive == false);
        Assert.That(result.TermSets[0].Terms[1].MatchFunction == FuzzySearcher.FzfFuzzyMatchV2);
    }
    
    [Test]
    public void TwoSimpleTerms()
    {
        var result =FuzzySearcher.ParsePattern(CaseMode.CaseSmart, "aaa bbb", true);
        Assert.That(result.TermSets.Count == 2);
        Assert.That(result.TermSets[0].Terms[0].Text == "aaa");
        Assert.That(result.TermSets[0].Terms[0].Inv == false);
        Assert.That(result.TermSets[0].Terms[0].CaseSensitive == false);
        Assert.That(result.TermSets[0].Terms[0].MatchFunction == FuzzySearcher.FzfFuzzyMatchV2);
        
        Assert.That(result.TermSets[1].Terms[0].Text == "bbb");
        Assert.That(result.TermSets[1].Terms[0].Inv == false);
        Assert.That(result.TermSets[1].Terms[0].CaseSensitive == false);
        Assert.That(result.TermSets[1].Terms[0].MatchFunction == FuzzySearcher.FzfFuzzyMatchV2);
    }
    
    [Test]
    public void SimplePatternAndNegativePattern()
    {
        var result =FuzzySearcher.ParsePattern(CaseMode.CaseSmart, "aaa !bbb", true);
        Assert.That(result.TermSets.Count == 2);
        Assert.That(result.TermSets[0].Terms[0].Text == "aaa");
        Assert.That(result.TermSets[0].Terms[0].Inv == false);
        Assert.That(result.TermSets[0].Terms[0].CaseSensitive == false);
        Assert.That(result.TermSets[0].Terms[0].MatchFunction ==FuzzySearcher.FzfFuzzyMatchV2);
        
        Assert.That(result.TermSets[1].Terms[0].Text == "bbb");
        Assert.That(result.TermSets[1].Terms[0].Inv == true);
        Assert.That(result.TermSets[1].Terms[0].CaseSensitive == false);
        Assert.That(result.TermSets[1].Terms[0].MatchFunction == FuzzySearcher.FzfExactMatchNaive);
    }
}