using NUnit.Framework;
using nfm.Ui.Core;

namespace nfm.Ui.Core.Tests;

public class MockResultHandler : IResultHandler
{
    public Task HandleAsync(object output) => Task.CompletedTask;
}

[TestFixture]
public class ColumnFilterParserTests
{
    [Test]
    public void ParseColumnFilters_EmptyString_ReturnsEmptyList()
    {
        var searchText = "";

        var result = ColumnFilterParser.ParseColumnFilters(searchText);

        Assert.That(result.Count, Is.EqualTo(0));
    }

    [Test]
    public void ParseColumnFilters_NullString_ReturnsEmptyList()
    {
        string? searchText = null;

        var result = ColumnFilterParser.ParseColumnFilters(searchText!);

        Assert.That(result.Count, Is.EqualTo(0));
    }

    [Test]
    public void ParseColumnFilters_SingleCompleteFilter_ReturnsSingleFilter()
    {
        var searchText = "/:status=stopped";

        var result = ColumnFilterParser.ParseColumnFilters(searchText);

        Assert.That(result.Count, Is.EqualTo(1));
        Assert.That(result[0].ColumnName, Is.EqualTo("status"));
        Assert.That(result[0].Operator, Is.EqualTo("="));
        Assert.That(result[0].Value, Is.EqualTo("stopped"));
    }

    [Test]
    public void ParseColumnFilters_MultipleCompleteFilters_ReturnsMultipleFilters()
    {
        var searchText = "/:status=stopped /:name=notepad /:pid=1234";

        var result = ColumnFilterParser.ParseColumnFilters(searchText);

        Assert.That(result.Count, Is.EqualTo(3));

        Assert.That(result[0].ColumnName, Is.EqualTo("status"));
        Assert.That(result[0].Operator, Is.EqualTo("="));
        Assert.That(result[0].Value, Is.EqualTo("stopped"));

        Assert.That(result[1].ColumnName, Is.EqualTo("name"));
        Assert.That(result[1].Operator, Is.EqualTo("="));
        Assert.That(result[1].Value, Is.EqualTo("notepad"));

        Assert.That(result[2].ColumnName, Is.EqualTo("pid"));
        Assert.That(result[2].Operator, Is.EqualTo("="));
        Assert.That(result[2].Value, Is.EqualTo("1234"));
    }

    [Test]
    public void ParseColumnFilters_FilterWithEmptyValue_ReturnsEmptyList()
    {
        var searchText = "/:status=";

        var result = ColumnFilterParser.ParseColumnFilters(searchText);

        Assert.That(result.Count, Is.EqualTo(0));
    }

    [Test]
    public void ParseColumnFilters_FilterWithoutSpaces_ReturnsCorrectValue()
    {
        var searchText = "/:name=VisualStudio";

        var result = ColumnFilterParser.ParseColumnFilters(searchText);

        Assert.That(result.Count, Is.EqualTo(1));
        Assert.That(result[0].ColumnName, Is.EqualTo("name"));
        Assert.That(result[0].Operator, Is.EqualTo("="));
        Assert.That(result[0].Value, Is.EqualTo("VisualStudio"));
    }

    [Test]
    public void ParseColumnFilters_IncompleteFilter_ReturnsEmptyList()
    {
        var searchText = "/:status=incomplet";

        var result = ColumnFilterParser.ParseColumnFilters(searchText);

        Assert.That(result.Count, Is.EqualTo(1));
        Assert.That(result[0].ColumnName, Is.EqualTo("status"));
        Assert.That(result[0].Operator, Is.EqualTo("="));
        Assert.That(result[0].Value, Is.EqualTo("incomplet"));
    }

    [Test]
    public void ParseColumnFilters_MixedCompleteAndIncompleteFilters_ReturnsAllCompleteFilters()
    {
        var searchText = "/:status=stopped /:name=incomplet";

        var result = ColumnFilterParser.ParseColumnFilters(searchText);

        Assert.That(result.Count, Is.EqualTo(2));
        Assert.That(result[0].ColumnName, Is.EqualTo("status"));
        Assert.That(result[0].Operator, Is.EqualTo("="));
        Assert.That(result[0].Value, Is.EqualTo("stopped"));
        Assert.That(result[1].ColumnName, Is.EqualTo("name"));
        Assert.That(result[1].Operator, Is.EqualTo("="));
        Assert.That(result[1].Value, Is.EqualTo("incomplet"));
    }

    [Test]
    public void RemoveColumnFilters_EmptyString_ReturnsEmptyString()
    {
        var searchText = "";

        var result = ColumnFilterParser.RemoveColumnFilters(searchText);

        Assert.That(result, Is.EqualTo(""));
    }

    [Test]
    public void RemoveColumnFilters_NoFilters_ReturnsOriginalString()
    {
        var searchText = "notepad process";

        var result = ColumnFilterParser.RemoveColumnFilters(searchText);

        Assert.That(result, Is.EqualTo("notepad process"));
    }

    [Test]
    public void RemoveColumnFilters_SingleFilter_RemovesFilter()
    {
        var searchText = "/:status=stopped";

        var result = ColumnFilterParser.RemoveColumnFilters(searchText);

        Assert.That(result, Is.EqualTo(""));
    }

    [Test]
    public void RemoveColumnFilters_FilterWithAdditionalText_RemovesOnlyFilter()
    {
        var searchText = "/:status=stopped notepad";

        var result = ColumnFilterParser.RemoveColumnFilters(searchText);

        Assert.That(result, Is.EqualTo("notepad"));
    }

    [Test]
    public void RemoveColumnFilters_MultipleFilters_RemovesAllFilters()
    {
        var searchText = "/:status=stopped /:name=notepad search text";

        var result = ColumnFilterParser.RemoveColumnFilters(searchText);

        Assert.That(result, Is.EqualTo("search text"));
    }

    [Test]
    public void HasIncompleteColumnFilters_EmptyString_ReturnsFalse()
    {
        var searchText = "";

        var result = ColumnFilterParser.HasIncompleteColumnFilters(searchText);

        Assert.That(result, Is.False);
    }

    [Test]
    public void HasIncompleteColumnFilters_NoFilters_ReturnsFalse()
    {
        var searchText = "notepad process";

        var result = ColumnFilterParser.HasIncompleteColumnFilters(searchText);

        Assert.That(result, Is.False);
    }

    [Test]
    public void HasIncompleteColumnFilters_CompleteFilter_ReturnsFalse()
    {
        var searchText = "/:status=stopped";

        var result = ColumnFilterParser.HasIncompleteColumnFilters(searchText);

        Assert.That(result, Is.False);
    }

    [Test]
    public void HasIncompleteColumnFilters_MultipleCompleteFilters_ReturnsFalse()
    {
        var searchText = "/:status=stopped /:name=notepad";

        var result = ColumnFilterParser.HasIncompleteColumnFilters(searchText);

        Assert.That(result, Is.False);
    }

    [Test]
    public void HasIncompleteColumnFilters_IncompleteColumnName_ReturnsTrue()
    {
        var searchText = "/:status";

        var result = ColumnFilterParser.HasIncompleteColumnFilters(searchText);

        Assert.That(result, Is.True);
    }

    [Test]
    public void HasIncompleteColumnFilters_IncompleteWithEquals_ReturnsTrue()
    {
        var searchText = "/:status=";

        var result = ColumnFilterParser.HasIncompleteColumnFilters(searchText);

        Assert.That(result, Is.True);
    }

    [Test]
    public void HasIncompleteColumnFilters_FilterAtEndOfString_ReturnsTrue()
    {
        var searchText = "/:status";

        var result = ColumnFilterParser.HasIncompleteColumnFilters(searchText);

        Assert.That(result, Is.True);
    }

    [Test]
    public void HasIncompleteColumnFilters_FilterWithEqualsAtEnd_ReturnsTrue()
    {
        var searchText = "/:status=";

        var result = ColumnFilterParser.HasIncompleteColumnFilters(searchText);

        Assert.That(result, Is.True);
    }

    [Test]
    public void HasIncompleteColumnFilters_MixedCompleteAndIncomplete_ReturnsTrue()
    {
        var searchText = "/:status=stopped /:name";

        var result = ColumnFilterParser.HasIncompleteColumnFilters(searchText);

        Assert.That(result, Is.True);
    }

    [Test]
    public void PreParseSearchString_EmptyString_ReturnsEmptyString()
    {
        var searchText = "";

        var result = ColumnFilterParser.PreParseSearchString(searchText);

        Assert.That(result, Is.EqualTo(""));
    }

    [Test]
    public void PreParseSearchString_NoFilters_ReturnsOriginalString()
    {
        var searchText = "notepad process";

        var result = ColumnFilterParser.PreParseSearchString(searchText);

        Assert.That(result, Is.EqualTo("notepad process"));
    }

    [Test]
    public void PreParseSearchString_CompleteFilterOnly_ReturnsEmptyString()
    {
        var searchText = "/:status=stopped";

        var result = ColumnFilterParser.PreParseSearchString(searchText);

        Assert.That(result, Is.EqualTo(""));
    }

    [Test]
    public void PreParseSearchString_CompleteFilterWithText_ReturnsOnlyText()
    {
        var searchText = "/:status=stopped notepad";

        var result = ColumnFilterParser.PreParseSearchString(searchText);

        Assert.That(result, Is.EqualTo("notepad"));
    }

    [Test]
    public void PreParseSearchString_IncompleteFilter_ReturnsEmptyString()
    {
        var searchText = "/:status";

        var result = ColumnFilterParser.PreParseSearchString(searchText);

        Assert.That(result, Is.EqualTo(""));
    }

    [Test]
    public void PreParseSearchString_IncompleteFilterWithText_ReturnsOnlyText()
    {
        var searchText = "/:status= notepad";

        var result = ColumnFilterParser.PreParseSearchString(searchText);

        Assert.That(result, Is.EqualTo("notepad"));
    }
    
    [Test]
    public void PreParseSearchString_MixedCompleteAndIncompleteWithText_ReturnsOnlyText2()
    {
        var searchText = "/:status=stopped /:s";

        var result = ColumnFilterParser.PreParseSearchString(searchText);

        Assert.That(result, Is.EqualTo(""));
    }

    [Test]
    public void PreParseSearchString_MixedCompleteAndIncompleteWithText_ReturnsOnlyText()
    {
        var searchText = "/:status=stopped /:name= notepad";

        var result = ColumnFilterParser.PreParseSearchString(searchText);

        Assert.That(result, Is.EqualTo("notepad"));
    }

    [Test]
    public void PreParseSearchString_MultipleCompleteFiltersWithText_ReturnsOnlyText()
    {
        var searchText = "/:status=stopped /:name=notepad search text";

        var result = ColumnFilterParser.PreParseSearchString(searchText);

        Assert.That(result, Is.EqualTo("search text"));
    }

    // Additional tests for incomplete filter detection without quotes
    [Test]
    public void HasIncompleteColumnFilters_IncompleteAtEndOfLine_ReturnsTrue()
    {
        var searchText = "some text /:status";

        var result = ColumnFilterParser.HasIncompleteColumnFilters(searchText);

        Assert.That(result, Is.True);
    }

    [Test]
    public void HasIncompleteColumnFilters_IncompleteWithEqualsAtEndOfLine_ReturnsTrue()
    {
        var searchText = "some text /:status=";

        var result = ColumnFilterParser.HasIncompleteColumnFilters(searchText);

        Assert.That(result, Is.True);
    }

    [Test]
    public void HasIncompleteColumnFilters_IncompleteInMiddleOfText_ReturnsTrue()
    {
        var searchText = "/:status more text";

        var result = ColumnFilterParser.HasIncompleteColumnFilters(searchText);

        Assert.That(result, Is.True);
    }

    [Test]
    public void HasIncompleteColumnFilters_IncompleteWithEqualsInMiddle_ReturnsTrue()
    {
        var searchText = "/:status= more text";

        var result = ColumnFilterParser.HasIncompleteColumnFilters(searchText);

        Assert.That(result, Is.True);
    }

    [Test]
    public void HasIncompleteColumnFilters_CompleteFilterInMiddle_ReturnsFalse()
    {
        var searchText = "/:status=stopped more text";

        var result = ColumnFilterParser.HasIncompleteColumnFilters(searchText);

        Assert.That(result, Is.False);
    }

    [Test]
    public void HasIncompleteColumnFilters_MultipleIncompleteFilters_ReturnsTrue()
    {
        var searchText = "/:status /:name=";

        var result = ColumnFilterParser.HasIncompleteColumnFilters(searchText);

        Assert.That(result, Is.True);
    }

    [Test]
    public void PreParseSearchString_IncompleteFilterInMiddle_RemovesIncompleteFilter()
    {
        var searchText = "/:status some search text";

        var result = ColumnFilterParser.PreParseSearchString(searchText);

        Assert.That(result, Is.EqualTo("some search text"));
    }

    [Test]
    public void PreParseSearchString_IncompleteFilterWithEqualsInMiddle_RemovesIncompleteFilter()
    {
        var searchText = "/:status= some search text";

        var result = ColumnFilterParser.PreParseSearchString(searchText);

        Assert.That(result, Is.EqualTo("some search text"));
    }

    [Test]
    public void IncompleteFilterDetection_ComprehensiveScenarios()
    {
        // Test various incomplete scenarios
        Assert.That(ColumnFilterParser.HasIncompleteColumnFilters("/:name"), Is.True, "Should detect incomplete filter without equals");
        Assert.That(ColumnFilterParser.HasIncompleteColumnFilters("/:name="), Is.True, "Should detect incomplete filter with equals but no value");
        Assert.That(ColumnFilterParser.HasIncompleteColumnFilters("/:name= "), Is.True, "Should detect incomplete filter with equals and space");
        Assert.That(ColumnFilterParser.HasIncompleteColumnFilters("text /:name"), Is.True, "Should detect incomplete filter after text");
        Assert.That(ColumnFilterParser.HasIncompleteColumnFilters("/:name text"), Is.True, "Should detect incomplete filter before text");
        Assert.That(ColumnFilterParser.HasIncompleteColumnFilters("/:name= text"), Is.True, "Should detect incomplete filter with equals before text");

        // Test complete scenarios that should NOT be detected as incomplete
        Assert.That(ColumnFilterParser.HasIncompleteColumnFilters("/:name=value"), Is.False, "Should not detect complete filter");
        Assert.That(ColumnFilterParser.HasIncompleteColumnFilters("/:name=value text"), Is.False, "Should not detect complete filter with text");
        Assert.That(ColumnFilterParser.HasIncompleteColumnFilters("text /:name=value"), Is.False, "Should not detect complete filter after text");
        Assert.That(ColumnFilterParser.HasIncompleteColumnFilters("/:name=value /:other=value2"), Is.False, "Should not detect multiple complete filters");
    }

    // Tests for new operators: ==, !=, =~
    [Test]
    public void ParseColumnFilters_EqualsOperator_ReturnsCorrectFilter()
    {
        var searchText = "/:status==stopped";

        var result = ColumnFilterParser.ParseColumnFilters(searchText);

        Assert.That(result.Count, Is.EqualTo(1));
        Assert.That(result[0].ColumnName, Is.EqualTo("status"));
        Assert.That(result[0].Operator, Is.EqualTo("=="));
        Assert.That(result[0].Value, Is.EqualTo("stopped"));
    }

    [Test]
    public void ParseColumnFilters_NotEqualsOperator_ReturnsCorrectFilter()
    {
        var searchText = "/:status!=stopped";

        var result = ColumnFilterParser.ParseColumnFilters(searchText);

        Assert.That(result.Count, Is.EqualTo(1));
        Assert.That(result[0].ColumnName, Is.EqualTo("status"));
        Assert.That(result[0].Operator, Is.EqualTo("!="));
        Assert.That(result[0].Value, Is.EqualTo("stopped"));
    }

    [Test]
    public void ParseColumnFilters_RegexMatchOperator_ReturnsCorrectFilter()
    {
        var searchText = "/:name=~note.*";

        var result = ColumnFilterParser.ParseColumnFilters(searchText);

        Assert.That(result.Count, Is.EqualTo(1));
        Assert.That(result[0].ColumnName, Is.EqualTo("name"));
        Assert.That(result[0].Operator, Is.EqualTo("=~"));
        Assert.That(result[0].Value, Is.EqualTo("note.*"));
    }

    [Test]
    public void ParseColumnFilters_RegexNotMatchOperator_ReturnsCorrectFilter()
    {
        var searchText = "/:name!~note.*";

        var result = ColumnFilterParser.ParseColumnFilters(searchText);

        Assert.That(result.Count, Is.EqualTo(1));
        Assert.That(result[0].ColumnName, Is.EqualTo("name"));
        Assert.That(result[0].Operator, Is.EqualTo("!~"));
        Assert.That(result[0].Value, Is.EqualTo("note.*"));
    }

    [Test]
    public void ParseColumnFilters_MixedOperators_ReturnsAllFilters()
    {
        var searchText = "/:status=running /:name!=notepad /:pid==1234 /:command=~.*exe /:type!~temp.*";

        var result = ColumnFilterParser.ParseColumnFilters(searchText);

        Assert.That(result.Count, Is.EqualTo(5));

        Assert.That(result[0].ColumnName, Is.EqualTo("status"));
        Assert.That(result[0].Operator, Is.EqualTo("="));
        Assert.That(result[0].Value, Is.EqualTo("running"));

        Assert.That(result[1].ColumnName, Is.EqualTo("name"));
        Assert.That(result[1].Operator, Is.EqualTo("!="));
        Assert.That(result[1].Value, Is.EqualTo("notepad"));

        Assert.That(result[2].ColumnName, Is.EqualTo("pid"));
        Assert.That(result[2].Operator, Is.EqualTo("=="));
        Assert.That(result[2].Value, Is.EqualTo("1234"));

        Assert.That(result[3].ColumnName, Is.EqualTo("command"));
        Assert.That(result[3].Operator, Is.EqualTo("=~"));
        Assert.That(result[3].Value, Is.EqualTo(".*exe"));

        Assert.That(result[4].ColumnName, Is.EqualTo("type"));
        Assert.That(result[4].Operator, Is.EqualTo("!~"));
        Assert.That(result[4].Value, Is.EqualTo("temp.*"));
    }

    [Test]
    public void RemoveColumnFilters_NewOperators_RemovesAllFilters()
    {
        var searchText = "/:status==stopped /:name!=notepad /:command=~.*exe /:type!~temp.* search text";

        var result = ColumnFilterParser.RemoveColumnFilters(searchText);

        Assert.That(result, Is.EqualTo("search text"));
    }

    [Test]
    public void HasIncompleteColumnFilters_NewOperatorsIncomplete_ReturnsTrue()
    {
        Assert.That(ColumnFilterParser.HasIncompleteColumnFilters("/:status=="), Is.True, "Should detect incomplete == filter");
        Assert.That(ColumnFilterParser.HasIncompleteColumnFilters("/:status!="), Is.True, "Should detect incomplete != filter");
        Assert.That(ColumnFilterParser.HasIncompleteColumnFilters("/:status=~"), Is.True, "Should detect incomplete =~ filter");
        Assert.That(ColumnFilterParser.HasIncompleteColumnFilters("/:status!~"), Is.True, "Should detect incomplete !~ filter");
        Assert.That(ColumnFilterParser.HasIncompleteColumnFilters("/:status== "), Is.True, "Should detect incomplete == filter with space");
        Assert.That(ColumnFilterParser.HasIncompleteColumnFilters("/:status!= text"), Is.True, "Should detect incomplete != filter with text");
        Assert.That(ColumnFilterParser.HasIncompleteColumnFilters("/:status!~ text"), Is.True, "Should detect incomplete !~ filter with text");
    }

    [Test]
    public void HasIncompleteColumnFilters_NewOperatorsComplete_ReturnsFalse()
    {
        Assert.That(ColumnFilterParser.HasIncompleteColumnFilters("/:status==value"), Is.False, "Should not detect complete == filter");
        Assert.That(ColumnFilterParser.HasIncompleteColumnFilters("/:status!=value"), Is.False, "Should not detect complete != filter");
        Assert.That(ColumnFilterParser.HasIncompleteColumnFilters("/:status=~value"), Is.False, "Should not detect complete =~ filter");
        Assert.That(ColumnFilterParser.HasIncompleteColumnFilters("/:status!~value"), Is.False, "Should not detect complete !~ filter");
    }

    [Test]
    public void PreParseSearchString_NewOperators_RemovesFiltersCorrectly()
    {
        var searchText = "/:status==stopped /:name!=notepad /:type!~temp.* search text";
        var result = ColumnFilterParser.PreParseSearchString(searchText);
        Assert.That(result, Is.EqualTo("search text"));

        var incompleteText = "/:status!~ search text";
        var incompleteResult = ColumnFilterParser.PreParseSearchString(incompleteText);
        Assert.That(incompleteResult, Is.EqualTo("search text"));
    }

    [Test]
    public void IntegrationTest_OperatorSupport_WithStringArrayProvider()
    {
        // Test data with different values in a column
        var testData = new string[][]
        {
            new[] { "process1", "running", "1234" },
            new[] { "process2", "stopped", "5678" },
            new[] { "notepad", "running", "9999" },
            new[] { "chrome", "error", "1111" }
        };

        // Create a simple mock result handler for testing
        var mockResultHandler = new MockResultHandler();

        var provider = new StringArrayColumnMenuDefinitionProvider(
            () => testData,
            new int[] { 0, 1, 2 },
            new[] { "Name", "Status", "PID" },
            mockResultHandler,
            false,
            null,
            new[] { "Name", "Status", "PID" }
        );

        var menuDef = provider.Get();
        var scoreFuncWithOriginalText = menuDef.ScoreFuncWithOriginalText;

        Assert.That(scoreFuncWithOriginalText, Is.Not.Null, "ScoreFuncWithOriginalText should be available");

        // Create test rows
        var row1 = new StringArrayRow(testData[0], new int[] { 0, 1, 2 }, new Dictionary<int, int>(), new[] { "Name", "Status", "PID" }, new[] { "Name", "Status", "PID" });
        var row2 = new StringArrayRow(testData[1], new int[] { 0, 1, 2 }, new Dictionary<int, int>(), new[] { "Name", "Status", "PID" }, new[] { "Name", "Status", "PID" });
        var row3 = new StringArrayRow(testData[2], new int[] { 0, 1, 2 }, new Dictionary<int, int>(), new[] { "Name", "Status", "PID" }, new[] { "Name", "Status", "PID" });

        var pattern = nfzf.FuzzySearcher.ParsePattern(nfzf.CaseMode.CaseSmart, "", true);
        var slab = nfzf.Slab.MakeDefault();

        // Test == operator (exact match)
        var result1 = scoreFuncWithOriginalText!(row1, pattern, slab, "/:Status==running");
        var result2 = scoreFuncWithOriginalText!(row2, pattern, slab, "/:Status==running");
        Assert.That(result1.Item2, Is.GreaterThan(0), "Row with 'running' status should match ==running filter");
        Assert.That(result2.Item2, Is.EqualTo(-1), "Row with 'stopped' status should not match ==running filter");

        // Test != operator (not equals)
        var result3 = scoreFuncWithOriginalText!(row1, pattern, slab, "/:Status!=stopped");
        var result4 = scoreFuncWithOriginalText!(row2, pattern, slab, "/:Status!=stopped");
        Assert.That(result3.Item2, Is.GreaterThan(0), "Row with 'running' status should match !=stopped filter");
        Assert.That(result4.Item2, Is.EqualTo(-1), "Row with 'stopped' status should not match !=stopped filter");

        // Test =~ operator (regex match)
        var result5 = scoreFuncWithOriginalText!(row3, pattern, slab, "/:Name=~note.*");
        var result6 = scoreFuncWithOriginalText!(row1, pattern, slab, "/:Name=~note.*");
        Assert.That(result5.Item2, Is.GreaterThan(0), "Row with 'notepad' name should match =~note.* filter");
        Assert.That(result6.Item2, Is.EqualTo(-1), "Row with 'process1' name should not match =~note.* filter");

        // Test !~ operator (regex not match)
        var result7 = scoreFuncWithOriginalText!(row1, pattern, slab, "/:Name!~note.*");
        var result8 = scoreFuncWithOriginalText!(row3, pattern, slab, "/:Name!~note.*");
        Assert.That(result7.Item2, Is.GreaterThan(0), "Row with 'process1' name should match !~note.* filter (not notepad)");
        Assert.That(result8.Item2, Is.EqualTo(-1), "Row with 'notepad' name should not match !~note.* filter (is notepad)");
    }

    [Test]
    public void IntegrationTest_IncompleteFilterHandling_NoLongerIgnored()
    {
        // Test data
        var testData = new string[][]
        {
            new[] { "process1", "running", "1234" },
            new[] { "process2", "stopped", "5678" }
        };

        var mockResultHandler = new MockResultHandler();
        var provider = new StringArrayColumnMenuDefinitionProvider(
            () => testData,
            new int[] { 0, 1, 2 },
            new[] { "Name", "Status", "PID" },
            mockResultHandler,
            false,
            null,
            new[] { "Name", "Status", "PID" }
        );

        var menuDef = provider.Get();
        var scoreFuncWithOriginalText = menuDef.ScoreFuncWithOriginalText;

        var row1 = new StringArrayRow(testData[0], new int[] { 0, 1, 2 }, new Dictionary<int, int>(), new[] { "Name", "Status", "PID" }, new[] { "Name", "Status", "PID" });
        var pattern = nfzf.FuzzySearcher.ParsePattern(nfzf.CaseMode.CaseSmart, "", true);
        var slab = nfzf.Slab.MakeDefault();

        // Test with incomplete filter - should now fallback to normal fuzzy search instead of being ignored
        var result = scoreFuncWithOriginalText!(row1, pattern, slab, "/:Status=");

        // Since the incomplete filter won't parse any valid filters, it should fallback to normal fuzzy search
        // The score should be the normal fuzzy search score, not boosted
        Assert.That(result.Item2, Is.GreaterThanOrEqualTo(0), "Incomplete filter should fallback to normal fuzzy search");
        Assert.That(result.Item2, Is.LessThan(500), "Incomplete filter should not get the column filter boost");
    }

    [Test]
    public void IntegrationTest_RegexNegationOperator_ComplexPatterns()
    {
        // Test data with various process names
        var testData = new string[][]
        {
            new[] { "notepad.exe", "running", "1234" },
            new[] { "chrome.exe", "running", "5678" },
            new[] { "firefox.exe", "stopped", "9999" },
            new[] { "winword.exe", "running", "1111" },
            new[] { "python", "running", "2222" }
        };

        var mockResultHandler = new MockResultHandler();
        var provider = new StringArrayColumnMenuDefinitionProvider(
            () => testData,
            new int[] { 0, 1, 2 },
            new[] { "Name", "Status", "PID" },
            mockResultHandler,
            false,
            null,
            new[] { "Name", "Status", "PID" }
        );

        var menuDef = provider.Get();
        var scoreFuncWithOriginalText = menuDef.ScoreFuncWithOriginalText;

        var rows = testData.Select(data => new StringArrayRow(data, new int[] { 0, 1, 2 },
            new Dictionary<int, int>(), new[] { "Name", "Status", "PID" }, new[] { "Name", "Status", "PID" })).ToArray();

        var pattern = nfzf.FuzzySearcher.ParsePattern(nfzf.CaseMode.CaseSmart, "", true);
        var slab = nfzf.Slab.MakeDefault();

        // Test !~ operator - exclude processes that end with .exe
        var results = rows.Select(row => new { Row = row, Score = scoreFuncWithOriginalText!(row, pattern, slab, "/:Name!~.*\\.exe$") }).ToArray();

        // Only python (which doesn't end with .exe) should match
        var matchingRows = results.Where(r => r.Score.Item2 > 0).ToArray();
        Assert.That(matchingRows.Length, Is.EqualTo(1), "Only one process should match !~.*\\.exe$ filter");
        Assert.That(matchingRows[0].Row.GetAllData()[0], Is.EqualTo("python"), "Python should be the only match (no .exe extension)");

        // All .exe processes should be excluded
        var excludedRows = results.Where(r => r.Score.Item2 == -1).ToArray();
        Assert.That(excludedRows.Length, Is.EqualTo(4), "Four .exe processes should be excluded");
    }
}