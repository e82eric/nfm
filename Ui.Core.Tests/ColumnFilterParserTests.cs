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
    public void ParseColumnFilters_SingleCompleteFilterWithoutSpaceAtEnd_ReturnsNoFilter()
    {
        var searchText = "/:status=stopped";

        var result = ColumnFilterParser.ParseColumnFilters(searchText);

        Assert.That(result.Count, Is.EqualTo(0));
    }
    
    [Test]
    public void ParseColumnFilters_SingleCompleteFilterWithoutSpaceAtEnd_ReturnsFilter()
    {
        var searchText = "/:status=stopped ";

        var result = ColumnFilterParser.ParseColumnFilters(searchText);

        Assert.That(result.Count, Is.EqualTo(1));
        Assert.That(result[0].ColumnName, Is.EqualTo("status"));
        Assert.That(result[0].Operator, Is.EqualTo("="));
        Assert.That(result[0].Value, Is.EqualTo("stopped"));
    }

    [Test]
    public void ParseColumnFilters_MultipleCompleteFiltersWithoutSpaceAtEnd_ReturnsMultipleFiltersButNotLast()
    {
        var searchText = "/:status=stopped /:name=notepad /:pid=1234";

        var result = ColumnFilterParser.ParseColumnFilters(searchText);

        Assert.That(result.Count, Is.EqualTo(2));

        Assert.That(result[0].ColumnName, Is.EqualTo("status"));
        Assert.That(result[0].Operator, Is.EqualTo("="));
        Assert.That(result[0].Value, Is.EqualTo("stopped"));

        Assert.That(result[1].ColumnName, Is.EqualTo("name"));
        Assert.That(result[1].Operator, Is.EqualTo("="));
        Assert.That(result[1].Value, Is.EqualTo("notepad"));
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
        var searchText = "/:name=VisualStudio ";

        var result = ColumnFilterParser.ParseColumnFilters(searchText);

        Assert.That(result.Count, Is.EqualTo(1));
        Assert.That(result[0].ColumnName, Is.EqualTo("name"));
        Assert.That(result[0].Operator, Is.EqualTo("="));
        Assert.That(result[0].Value, Is.EqualTo("VisualStudio"));
    }

    [Test]
    public void ParseColumnFilters_IncompleteFilter_ReturnsEmptyList()
    {
        var searchText = "/:status=incomplet ";

        var result = ColumnFilterParser.ParseColumnFilters(searchText);

        Assert.That(result.Count, Is.EqualTo(1));
        Assert.That(result[0].ColumnName, Is.EqualTo("status"));
        Assert.That(result[0].Operator, Is.EqualTo("="));
        Assert.That(result[0].Value, Is.EqualTo("incomplet"));
    }

    [Test]
    public void ParseColumnFilters_MixedCompleteAndIncompleteFilters_ReturnsAllCompleteFilters()
    {
        var searchText = "/:status=stopped /:name=incomplet ";

        var result = ColumnFilterParser.ParseColumnFilters(searchText);

        Assert.That(result.Count, Is.EqualTo(2));
        Assert.That(result[0].ColumnName, Is.EqualTo("status"));
        Assert.That(result[0].Operator, Is.EqualTo("="));
        Assert.That(result[0].Value, Is.EqualTo("stopped"));
        Assert.That(result[1].ColumnName, Is.EqualTo("name"));
        Assert.That(result[1].Operator, Is.EqualTo("="));
        Assert.That(result[1].Value, Is.EqualTo("incomplet"));
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

    // [Test]
    // public void IntegrationTest_OperatorSupport_WithStringArrayProvider()
    // {
    //     // Test data with different values in a column
    //     var testData = new string[][]
    //     {
    //         new[] { "process1", "running", "1234" },
    //         new[] { "process2", "stopped", "5678" },
    //         new[] { "notepad", "running", "9999" },
    //         new[] { "chrome", "error", "1111" }
    //     };
    //
    //     // Create a simple mock result handler for testing
    //     var mockResultHandler = new MockResultHandler();
    //
    //     var provider = new StringArrayColumnMenuDefinitionProvider(
    //         () => testData,
    //         new int[] { 0, 1, 2 },
    //         new[] { "Name", "Status", "PID" },
    //         mockResultHandler,
    //         false,
    //         null,
    //         new[] { "Name", "Status", "PID" }
    //     );
    //
    //     var menuDef = provider.Get();
    //     var scoreFuncWithOriginalText = menuDef.ScoreFuncWithOriginalText;
    //
    //     Assert.That(scoreFuncWithOriginalText, Is.Not.Null, "ScoreFuncWithOriginalText should be available");
    //
    //     // Create test rows
    //     var row1 = new StringArrayRow(testData[0], new int[] { 0, 1, 2 }, new Dictionary<int, int>(), new[] { "Name", "Status", "PID" }, new[] { "Name", "Status", "PID" });
    //     var row2 = new StringArrayRow(testData[1], new int[] { 0, 1, 2 }, new Dictionary<int, int>(), new[] { "Name", "Status", "PID" }, new[] { "Name", "Status", "PID" });
    //     var row3 = new StringArrayRow(testData[2], new int[] { 0, 1, 2 }, new Dictionary<int, int>(), new[] { "Name", "Status", "PID" }, new[] { "Name", "Status", "PID" });
    //
    //     var pattern = nfzf.FuzzySearcher.ParsePattern(nfzf.CaseMode.CaseSmart, "", true);
    //     var slab = nfzf.Slab.MakeDefault();
    //
    //     // Test == operator (exact match)
    //     var result1 = scoreFuncWithOriginalText!(row1, pattern, slab, "/:Status==running");
    //     var result2 = scoreFuncWithOriginalText!(row2, pattern, slab, "/:Status==running");
    //     Assert.That(result1.Item2, Is.GreaterThan(0), "Row with 'running' status should match ==running filter");
    //     Assert.That(result2.Item2, Is.EqualTo(-1), "Row with 'stopped' status should not match ==running filter");
    //
    //     // Test != operator (not equals)
    //     var result3 = scoreFuncWithOriginalText!(row1, pattern, slab, "/:Status!=stopped");
    //     var result4 = scoreFuncWithOriginalText!(row2, pattern, slab, "/:Status!=stopped");
    //     Assert.That(result3.Item2, Is.GreaterThan(0), "Row with 'running' status should match !=stopped filter");
    //     Assert.That(result4.Item2, Is.EqualTo(-1), "Row with 'stopped' status should not match !=stopped filter");
    //
    //     // Test =~ operator (regex match)
    //     var result5 = scoreFuncWithOriginalText!(row3, pattern, slab, "/:Name=~note.*");
    //     var result6 = scoreFuncWithOriginalText!(row1, pattern, slab, "/:Name=~note.*");
    //     Assert.That(result5.Item2, Is.GreaterThan(0), "Row with 'notepad' name should match =~note.* filter");
    //     Assert.That(result6.Item2, Is.EqualTo(-1), "Row with 'process1' name should not match =~note.* filter");
    //
    //     // Test !~ operator (regex not match)
    //     var result7 = scoreFuncWithOriginalText!(row1, pattern, slab, "/:Name!~note.*");
    //     var result8 = scoreFuncWithOriginalText!(row3, pattern, slab, "/:Name!~note.*");
    //     Assert.That(result7.Item2, Is.GreaterThan(0), "Row with 'process1' name should match !~note.* filter (not notepad)");
    //     Assert.That(result8.Item2, Is.EqualTo(-1), "Row with 'notepad' name should not match !~note.* filter (is notepad)");
    // }

    // [Test]
    // public void IntegrationTest_IncompleteFilterHandling_NoLongerIgnored()
    // {
    //     // Test data
    //     var testData = new string[][]
    //     {
    //         new[] { "process1", "running", "1234" },
    //         new[] { "process2", "stopped", "5678" }
    //     };
    //
    //     var mockResultHandler = new MockResultHandler();
    //     var provider = new StringArrayColumnMenuDefinitionProvider(
    //         () => testData,
    //         new int[] { 0, 1, 2 },
    //         new[] { "Name", "Status", "PID" },
    //         mockResultHandler,
    //         false,
    //         null,
    //         new[] { "Name", "Status", "PID" }
    //     );
    //
    //     var menuDef = provider.Get();
    //     var scoreFuncWithOriginalText = menuDef.ScoreFuncWithOriginalText;
    //
    //     var row1 = new StringArrayRow(testData[0], new int[] { 0, 1, 2 }, new Dictionary<int, int>(), new[] { "Name", "Status", "PID" }, new[] { "Name", "Status", "PID" });
    //     var pattern = nfzf.FuzzySearcher.ParsePattern(nfzf.CaseMode.CaseSmart, "", true);
    //     var slab = nfzf.Slab.MakeDefault();
    //
    //     // Test with incomplete filter - should now fallback to normal fuzzy search instead of being ignored
    //     var result = scoreFuncWithOriginalText!(row1, pattern, slab, "/:Status=");
    //
    //     // Since the incomplete filter won't parse any valid filters, it should fallback to normal fuzzy search
    //     // The score should be the normal fuzzy search score, not boosted
    //     Assert.That(result.Item2, Is.GreaterThanOrEqualTo(0), "Incomplete filter should fallback to normal fuzzy search");
    //     Assert.That(result.Item2, Is.LessThan(500), "Incomplete filter should not get the column filter boost");
    // }

    // [Test]
    // public void IntegrationTest_RegexNegationOperator_ComplexPatterns()
    // {
    //     // Test data with various process names
    //     var testData = new string[][]
    //     {
    //         new[] { "notepad.exe", "running", "1234" },
    //         new[] { "chrome.exe", "running", "5678" },
    //         new[] { "firefox.exe", "stopped", "9999" },
    //         new[] { "winword.exe", "running", "1111" },
    //         new[] { "python", "running", "2222" }
    //     };
    //
    //     var mockResultHandler = new MockResultHandler();
    //     var provider = new StringArrayColumnMenuDefinitionProvider(
    //         () => testData,
    //         new int[] { 0, 1, 2 },
    //         new[] { "Name", "Status", "PID" },
    //         mockResultHandler,
    //         false,
    //         null,
    //         new[] { "Name", "Status", "PID" }
    //     );
    //
    //     var menuDef = provider.Get();
    //     var scoreFuncWithOriginalText = menuDef.ScoreFuncWithOriginalText;
    //
    //     var rows = testData.Select(data => new StringArrayRow(data, new int[] { 0, 1, 2 },
    //         new Dictionary<int, int>(), new[] { "Name", "Status", "PID" }, new[] { "Name", "Status", "PID" })).ToArray();
    //
    //     var pattern = nfzf.FuzzySearcher.ParsePattern(nfzf.CaseMode.CaseSmart, "", true);
    //     var slab = nfzf.Slab.MakeDefault();
    //
    //     // Test !~ operator - exclude processes that end with .exe
    //     var results = rows.Select(row => new { Row = row, Score = scoreFuncWithOriginalText!(row, pattern, slab, "/:Name!~.*\\.exe$") }).ToArray();
    //
    //     // Only python (which doesn't end with .exe) should match
    //     var matchingRows = results.Where(r => r.Score.Item2 > 0).ToArray();
    //     Assert.That(matchingRows.Length, Is.EqualTo(1), "Only one process should match !~.*\\.exe$ filter");
    //     Assert.That(matchingRows[0].Row.GetAllData()[0], Is.EqualTo("python"), "Python should be the only match (no .exe extension)");
    //
    //     // All .exe processes should be excluded
    //     var excludedRows = results.Where(r => r.Score.Item2 == -1).ToArray();
    //     Assert.That(excludedRows.Length, Is.EqualTo(4), "Four .exe processes should be excluded");
    // }

    [Test]
    public void AutoComplete_WithColonSlash_ReturnsMatchingColumns()
    {
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
        var autoCompleteProvider = menuDef.AutoCompleteProvider;

        Assert.That(autoCompleteProvider, Is.Not.Null, "AutoCompleteProvider should be available");

        // Test basic column suggestion
        var filterInfo1 = ColumnFilterParser.GetIncompleteFilterInfo("/:St");
        var suggestions1 = autoCompleteProvider!(filterInfo1);
        Assert.That(suggestions1.Count, Is.EqualTo(1), "Should find one column starting with 'St'");
        Assert.That(suggestions1[0], Is.EqualTo("Status"), "Should suggest 'Status' column");

        // Test no matches
        var filterInfo2 = ColumnFilterParser.GetIncompleteFilterInfo("/:Xyz");
        var suggestions2 = autoCompleteProvider(filterInfo2);
        Assert.That(suggestions2.Count, Is.EqualTo(0), "Should find no columns starting with 'Xyz'");

        // Test all columns when no partial name
        var filterInfo3 = ColumnFilterParser.GetIncompleteFilterInfo("/:");
        var suggestions3 = autoCompleteProvider(filterInfo3);
        Assert.That(suggestions3.Count, Is.EqualTo(5), "Should return all available columns including sort options");
        Assert.That(suggestions3, Contains.Item("SortDsc"));
        Assert.That(suggestions3, Contains.Item("SortAsc"));
        Assert.That(suggestions3, Contains.Item("Name"));
        Assert.That(suggestions3, Contains.Item("Status"));
        Assert.That(suggestions3, Contains.Item("PID"));

        // Test case insensitive matching
        var filterInfo4 = ColumnFilterParser.GetIncompleteFilterInfo("/:name");
        var suggestions4 = autoCompleteProvider(filterInfo4);
        Assert.That(suggestions4.Count, Is.EqualTo(1), "Should find 'Name' case-insensitively");
        Assert.That(suggestions4[0], Is.EqualTo("Name"));
    }

    [Test]
    public void AutoComplete_WithOperators_ReturnsColumnValues()
    {
        var testData = new string[][]
        {
            new[] { "process1", "running", "1234" },
            new[] { "process2", "stopped", "5678" },
            new[] { "notepad", "running", "9999" }
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
        var autoCompleteProvider = menuDef.AutoCompleteProvider!;

        // Test column value suggestions when operator is present
        var filterInfo1 = ColumnFilterParser.GetIncompleteFilterInfo("/:Status=");
        var suggestions1 = autoCompleteProvider(filterInfo1);
        Assert.That(suggestions1.Count, Is.EqualTo(2), "Should suggest column values when = operator is present");
        Assert.That(suggestions1, Contains.Item("running"));
        Assert.That(suggestions1, Contains.Item("stopped"));

        var filterInfo2 = ColumnFilterParser.GetIncompleteFilterInfo("/:Name!=");
        var suggestions2 = autoCompleteProvider(filterInfo2);
        Assert.That(suggestions2.Count, Is.EqualTo(3), "Should suggest column values when != operator is present");
        Assert.That(suggestions2, Contains.Item("process1"));
        Assert.That(suggestions2, Contains.Item("process2"));
        Assert.That(suggestions2, Contains.Item("notepad"));

        var filterInfo3 = ColumnFilterParser.GetIncompleteFilterInfo("/:PID=~");
        var suggestions3 = autoCompleteProvider(filterInfo3);
        Assert.That(suggestions3.Count, Is.EqualTo(3), "Should suggest column values when =~ operator is present");
        Assert.That(suggestions3, Contains.Item("1234"));
        Assert.That(suggestions3, Contains.Item("5678"));
        Assert.That(suggestions3, Contains.Item("9999"));

        var filterInfo4 = ColumnFilterParser.GetIncompleteFilterInfo("/:Status!~");
        var suggestions4 = autoCompleteProvider(filterInfo4);
        Assert.That(suggestions4.Count, Is.EqualTo(2), "Should suggest column values when !~ operator is present");
        Assert.That(suggestions4, Contains.Item("running"));
        Assert.That(suggestions4, Contains.Item("stopped"));

        // Test partial value matching
        var filterInfo5 = ColumnFilterParser.GetIncompleteFilterInfo("/:Status=run");
        var suggestions5 = autoCompleteProvider(filterInfo5);
        Assert.That(suggestions5.Count, Is.EqualTo(1), "Should suggest column values matching partial input");
        Assert.That(suggestions5[0], Is.EqualTo("running"));

        var filterInfo6 = ColumnFilterParser.GetIncompleteFilterInfo("/:Name=proc");
        var suggestions6 = autoCompleteProvider(filterInfo6);
        Assert.That(suggestions6.Count, Is.EqualTo(2), "Should suggest column values matching partial input");
        Assert.That(suggestions6, Contains.Item("process1"));
        Assert.That(suggestions6, Contains.Item("process2"));
    }

    // Tests for ParseSearchString method
    [Test]
    public void ParseSearchString_EmptyString_ReturnsEmptyString()
    {
        var searchText = "";

        var result = ColumnFilterParser.ParseSearchString(searchText);

        Assert.That(result, Is.EqualTo(""));
    }

    [Test]
    public void ParseSearchString_NullString_ReturnsNull()
    {
        string? searchText = null;

        var result = ColumnFilterParser.ParseSearchString(searchText!);

        Assert.That(result, Is.Null);
    }

    [Test]
    public void ParseSearchString_CompleteFilter_RemovesFilter()
    {
        var searchText = "/:status=running";

        var result = ColumnFilterParser.ParseSearchString(searchText);

        Assert.That(result, Is.EqualTo(""));
    }

    [Test]
    public void ParseSearchString_CompleteFilterWithText_RemovesFilterKeepsText()
    {
        var searchText = "/:status=running search text";

        var result = ColumnFilterParser.ParseSearchString(searchText);

        Assert.That(result, Is.EqualTo("search text"));
    }
    
    [Test]
    public void ParseSearchString_WithFirstPartOfNotEqualsOperatorAndNotOtherText_ReturnsEmptyString()
    {
        var searchText = "/:status!";

        var result = ColumnFilterParser.ParseSearchString(searchText);

        Assert.That(result, Is.EqualTo(""));
    }
    
    [Test]
    public void ParseSearchString_WithFirstPartOfNotEqualsOperator_RemovesFilterKeepsText()
    {
        var searchText = "/:status! search text";

        var result = ColumnFilterParser.ParseSearchString(searchText);

        Assert.That(result, Is.EqualTo("search text"));
    }
    
    [Test]
    public void ParseSearchString_WithNotEqualsOperator_RemovesFilterKeepsText()
    {
        var searchText = "/:status!= search text";

        var result = ColumnFilterParser.ParseSearchString(searchText);

        Assert.That(result, Is.EqualTo("search text"));
    }

    [Test]
    public void ParseSearchString_IncompleteFilter_RemovesFilter()
    {
        var searchText = "/:status";

        var result = ColumnFilterParser.ParseSearchString(searchText);

        Assert.That(result, Is.EqualTo(""));
    }

    [Test]
    public void ParseSearchString_IncompleteFilterWithEquals_RemovesFilter()
    {
        var searchText = "/:status=";

        var result = ColumnFilterParser.ParseSearchString(searchText);

        Assert.That(result, Is.EqualTo(""));
    }

    [Test]
    public void ParseSearchString_NoStartingSlash_ReturnsOriginalString()
    {
        var searchText = "normal search text";

        var result = ColumnFilterParser.ParseSearchString(searchText);

        Assert.That(result, Is.EqualTo("normal search text"));
    }

    [Test]
    public void ParseSearchString_MultipleCompleteFilters_RemovesAllFilters()
    {
        var searchText = "/:status=running /:name=test /:pid==1234";

        var result = ColumnFilterParser.ParseSearchString(searchText);

        Assert.That(result, Is.EqualTo(""));
    }

    [Test]
    public void ParseSearchString_MixedFiltersWithText_RemovesFiltersKeepsText()
    {
        var searchText = "/:status=running search text /:name=test";

        var result = ColumnFilterParser.ParseSearchString(searchText);

        Assert.That(result, Is.EqualTo("search text"));
    }

    [Test]
    public void ParseSearchString_IncompleteFilterWithText_RemovesFilterKeepsText()
    {
        var searchText = "/:status search text";

        var result = ColumnFilterParser.ParseSearchString(searchText);

        Assert.That(result, Is.EqualTo("search text"));
    }

    [Test]
    public void ParseSearchString_CompoundOperators_RemovesAllFilters()
    {
        var searchText = "/:status!=running /:name=~test.* /:pid!~123";

        var result = ColumnFilterParser.ParseSearchString(searchText);

        Assert.That(result, Is.EqualTo(""));
    }

    [Test]
    public void ParseSearchString_TextWithEmbeddedSlash_PreservesText()
    {
        var searchText = "search/text with/slashes";

        var result = ColumnFilterParser.ParseSearchString(searchText);

        Assert.That(result, Is.EqualTo("search/text with/slashes"));
    }

    [Test]
    public void ParseSearchString_MixedIncompleteAndCompleteFilters_RemovesAllFilters()
    {
        var searchText = "/:status=running /:name /:pid= text here";

        var result = ColumnFilterParser.ParseSearchString(searchText);

        Assert.That(result, Is.EqualTo("text here"));
    }

    [Test]
    public void ParseSearchString_MultipleSpaces_NormalizesSpaces()
    {
        var searchText = "/:status=running    multiple   spaces   here";

        var result = ColumnFilterParser.ParseSearchString(searchText);

        Assert.That(result, Is.EqualTo("multiple spaces here"));
    }

    // Tests for GetIncompleteFilterInfo method
    [Test]
    public void GetIncompleteFilterInfo_EmptyString_ReturnsNone()
    {
        var result = ColumnFilterParser.GetIncompleteFilterInfo("");

        Assert.That(result.Type, Is.EqualTo(IncompleteFilterType.None));
        Assert.That(result.Context, Is.EqualTo(IncompleteFilterContext.None));
        Assert.That(result.HasIncompleteFilter, Is.False);
    }

    [Test]
    public void GetIncompleteFilterInfo_SlashOnly_ReturnsSlash()
    {
        var result = ColumnFilterParser.GetIncompleteFilterInfo("/");

        Assert.That(result.Type, Is.EqualTo(IncompleteFilterType.Slash));
        Assert.That(result.Context, Is.EqualTo(IncompleteFilterContext.Column));
        Assert.That(result.HasIncompleteFilter, Is.True);
    }

    [Test]
    public void GetIncompleteFilterInfo_ColonSlashOnly_ReturnsSlashWithColon()
    {
        var result = ColumnFilterParser.GetIncompleteFilterInfo("/:");

        Assert.That(result.Type, Is.EqualTo(IncompleteFilterType.SlashWithColon));
        Assert.That(result.Context, Is.EqualTo(IncompleteFilterContext.Column));
        Assert.That(result.HasIncompleteFilter, Is.True);
    }

    [Test]
    public void GetIncompleteFilterInfo_PartialColumnName_ReturnsColumnPrefix()
    {
        var result = ColumnFilterParser.GetIncompleteFilterInfo("/:stat");

        Assert.That(result.Type, Is.EqualTo(IncompleteFilterType.ColumnPrefix));
        Assert.That(result.Context, Is.EqualTo(IncompleteFilterContext.Column));
        Assert.That(result.ColumnPrefix, Is.EqualTo("stat"));
        Assert.That(result.HasIncompleteFilter, Is.True);
    }

    [Test]
    public void GetIncompleteFilterInfo_ColumnNameWithSpace_ReturnsNone()
    {
        var result = ColumnFilterParser.GetIncompleteFilterInfo("/:status ");

        Assert.That(result.Type, Is.EqualTo(IncompleteFilterType.None));
        Assert.That(result.HasIncompleteFilter, Is.False);
    }

    [Test]
    public void GetIncompleteFilterInfo_ColumnWithEqualsNoValue_ReturnsValuePrefix()
    {
        var result = ColumnFilterParser.GetIncompleteFilterInfo("/:status=");

        Assert.That(result.Type, Is.EqualTo(IncompleteFilterType.ValuePrefix));
        Assert.That(result.Context, Is.EqualTo(IncompleteFilterContext.Value));
        Assert.That(result.ColumnPrefix, Is.EqualTo("status"));
        Assert.That(result.Operator, Is.EqualTo("="));
        Assert.That(result.ValuePrefix, Is.EqualTo(""));
        Assert.That(result.HasIncompleteFilter, Is.True);
    }

    [Test]
    public void GetIncompleteFilterInfo_ColumnWithPartialValue_ReturnsValuePrefix()
    {
        var result = ColumnFilterParser.GetIncompleteFilterInfo("/:status=runn");

        Assert.That(result.Type, Is.EqualTo(IncompleteFilterType.ValuePrefix));
        Assert.That(result.Context, Is.EqualTo(IncompleteFilterContext.Value));
        Assert.That(result.ColumnPrefix, Is.EqualTo("status"));
        Assert.That(result.Operator, Is.EqualTo("="));
        Assert.That(result.ValuePrefix, Is.EqualTo("runn"));
        Assert.That(result.HasIncompleteFilter, Is.True);
    }

    [Test]
    public void GetIncompleteFilterInfo_CompleteFilterWithSpace_ReturnsNone()
    {
        var result = ColumnFilterParser.GetIncompleteFilterInfo("/:status=running ");

        Assert.That(result.Type, Is.EqualTo(IncompleteFilterType.None));
        Assert.That(result.HasIncompleteFilter, Is.False);
    }

    [Test]
    public void GetIncompleteFilterInfo_CompoundOperatorNoValue_ReturnsValuePrefix()
    {
        var result = ColumnFilterParser.GetIncompleteFilterInfo("/:status==");

        Assert.That(result.Type, Is.EqualTo(IncompleteFilterType.ValuePrefix));
        Assert.That(result.Context, Is.EqualTo(IncompleteFilterContext.Value));
        Assert.That(result.ColumnPrefix, Is.EqualTo("status"));
        Assert.That(result.Operator, Is.EqualTo("=="));
        Assert.That(result.ValuePrefix, Is.EqualTo(""));
        Assert.That(result.HasIncompleteFilter, Is.True);
    }

    [Test]
    public void GetIncompleteFilterInfo_CompoundOperatorWithValue_ReturnsNone()
    {
        var result = ColumnFilterParser.GetIncompleteFilterInfo("/:status==running");

        Assert.That(result.Type, Is.EqualTo(IncompleteFilterType.None));
        Assert.That(result.HasIncompleteFilter, Is.False);
    }

    [Test]
    public void GetIncompleteFilterInfo_MultipleFiltersLastIncomplete_ReturnsLastFilter()
    {
        var result = ColumnFilterParser.GetIncompleteFilterInfo("/:status=running /:name=test /:pid");

        Assert.That(result.Type, Is.EqualTo(IncompleteFilterType.ColumnPrefix));
        Assert.That(result.Context, Is.EqualTo(IncompleteFilterContext.Column));
        Assert.That(result.ColumnPrefix, Is.EqualTo("pid"));
        Assert.That(result.HasIncompleteFilter, Is.True);
    }

    [Test]
    public void GetIncompleteFilterInfo_ComplexScenarioWithText_ReturnsCorrectInfo()
    {
        var result = ColumnFilterParser.GetIncompleteFilterInfo("search text /:status=runn");

        Assert.That(result.Type, Is.EqualTo(IncompleteFilterType.ValuePrefix));
        Assert.That(result.Context, Is.EqualTo(IncompleteFilterContext.Value));
        Assert.That(result.ColumnPrefix, Is.EqualTo("status"));
        Assert.That(result.Operator, Is.EqualTo("="));
        Assert.That(result.ValuePrefix, Is.EqualTo("runn"));
        Assert.That(result.HasIncompleteFilter, Is.True);
    }

    [Test]
    public void GetIncompleteFilterInfo_SlashWithSpaces_ReturnsSlash()
    {
        var result = ColumnFilterParser.GetIncompleteFilterInfo("  /  ");

        Assert.That(result.Type, Is.EqualTo(IncompleteFilterType.Slash));
        Assert.That(result.Context, Is.EqualTo(IncompleteFilterContext.Column));
        Assert.That(result.HasIncompleteFilter, Is.True);
    }

    [Test]
    public void GetIncompleteFilterInfo_SlashWithColonAndSpaces_ReturnsSlashWithColon()
    {
        var result = ColumnFilterParser.GetIncompleteFilterInfo("  /:  ");

        Assert.That(result.Type, Is.EqualTo(IncompleteFilterType.SlashWithColon));
        Assert.That(result.Context, Is.EqualTo(IncompleteFilterContext.Column));
        Assert.That(result.HasIncompleteFilter, Is.True);
    }

    [Test]
    public void GetIncompleteFilterInfo_TextEndingWithSlash_ReturnsSlash()
    {
        var result = ColumnFilterParser.GetIncompleteFilterInfo("search text /");

        Assert.That(result.Type, Is.EqualTo(IncompleteFilterType.Slash));
        Assert.That(result.Context, Is.EqualTo(IncompleteFilterContext.Column));
        Assert.That(result.HasIncompleteFilter, Is.True);
    }


    [Test]
    public void AutoComplete_ComplexSearch_ReturnsCorrectSuggestions()
    {
        var testData = new string[][]
        {
            new[] { "process1", "running", "1234" }
        };

        var mockResultHandler = new MockResultHandler();
        var provider = new StringArrayColumnMenuDefinitionProvider(
            () => testData,
            new int[] { 0, 1, 2 },
            new[] { "Name", "Status", "ProcessID", "Priority" },
            mockResultHandler,
            false,
            null,
            new[] { "Name", "Status", "ProcessID", "Priority" }
        );

        var menuDef = provider.Get();
        var autoCompleteProvider = menuDef.AutoCompleteProvider!;

        // Test with existing complete filter and new partial filter
        var filterInfo1 = ColumnFilterParser.GetIncompleteFilterInfo("/:Status=running /:P");
        var suggestions1 = autoCompleteProvider(filterInfo1);
        Assert.That(suggestions1.Count, Is.EqualTo(2), "Should find columns starting with 'P'");
        Assert.That(suggestions1, Contains.Item("ProcessID"));
        Assert.That(suggestions1, Contains.Item("Priority"));

        // Test with partial column name
        var filterInfo2 = ColumnFilterParser.GetIncompleteFilterInfo("/:St");
        var suggestions2 = autoCompleteProvider(filterInfo2);
        Assert.That(suggestions2.Count, Is.EqualTo(1), "Should find 'Status' when filter has /:St");
        Assert.That(suggestions2[0], Is.EqualTo("Status"));

        // Test no suggestions when not in column context
        var filterInfo3 = ColumnFilterParser.GetIncompleteFilterInfo("some search text");
        var suggestions3 = autoCompleteProvider(filterInfo3);
        Assert.That(suggestions3.Count, Is.EqualTo(0), "Should not suggest columns when not in /: context");
    }

    [Test]
    public void ParseColumnFilters_QuotedValue_RemovesQuotes()
    {
        var searchText = "/:name=\"Visual Studio Code\" ";

        var result = ColumnFilterParser.ParseColumnFilters(searchText);

        Assert.That(result.Count, Is.EqualTo(1));
        Assert.That(result[0].ColumnName, Is.EqualTo("name"));
        Assert.That(result[0].Operator, Is.EqualTo("="));
        Assert.That(result[0].Value, Is.EqualTo("Visual Studio Code"));
    }

    [Test]
    public void ParseColumnFilters_QuotedValueWithCompoundOperator_RemovesQuotes()
    {
        var searchText = "/:status==\"My Application\" ";

        var result = ColumnFilterParser.ParseColumnFilters(searchText);

        Assert.That(result.Count, Is.EqualTo(1));
        Assert.That(result[0].ColumnName, Is.EqualTo("status"));
        Assert.That(result[0].Operator, Is.EqualTo("=="));
        Assert.That(result[0].Value, Is.EqualTo("My Application"));
    }

    [Test]
    public void ParseColumnFilters_MultipleQuotedValues_RemovesQuotes()
    {
        var searchText = "/:name=\"First App\" /:status!=\"Not Running\" ";

        var result = ColumnFilterParser.ParseColumnFilters(searchText);

        Assert.That(result.Count, Is.EqualTo(2));
        
        Assert.That(result[0].ColumnName, Is.EqualTo("name"));
        Assert.That(result[0].Operator, Is.EqualTo("="));
        Assert.That(result[0].Value, Is.EqualTo("First App"));
        
        Assert.That(result[1].ColumnName, Is.EqualTo("status"));
        Assert.That(result[1].Operator, Is.EqualTo("!="));
        Assert.That(result[1].Value, Is.EqualTo("Not Running"));
    }

    [Test]
    public void ParseColumnFilters_MixedQuotedAndUnquoted_HandlesCorrectly()
    {
        var searchText = "/:name=\"Visual Studio\" /:status=running ";

        var result = ColumnFilterParser.ParseColumnFilters(searchText);

        Assert.That(result.Count, Is.EqualTo(2));
        
        Assert.That(result[0].ColumnName, Is.EqualTo("name"));
        Assert.That(result[0].Operator, Is.EqualTo("="));
        Assert.That(result[0].Value, Is.EqualTo("Visual Studio"));
        
        Assert.That(result[1].ColumnName, Is.EqualTo("status"));
        Assert.That(result[1].Operator, Is.EqualTo("="));
        Assert.That(result[1].Value, Is.EqualTo("running"));
    }

    [Test]
    public void ParseColumnFilters_QuotedValueWithRegexOperators_RemovesQuotes()
    {
        var searchText = "/:name=~\".*Studio.*\" /:path!~\"temp\" ";

        var result = ColumnFilterParser.ParseColumnFilters(searchText);

        Assert.That(result.Count, Is.EqualTo(2));
        
        Assert.That(result[0].ColumnName, Is.EqualTo("name"));
        Assert.That(result[0].Operator, Is.EqualTo("=~"));
        Assert.That(result[0].Value, Is.EqualTo(".*Studio.*"));
        
        Assert.That(result[1].ColumnName, Is.EqualTo("path"));
        Assert.That(result[1].Operator, Is.EqualTo("!~"));
        Assert.That(result[1].Value, Is.EqualTo("temp"));
    }

    [Test]
    public void ParseColumnFilters_EmptyQuotedValue_HandlesCorrectly()
    {
        var searchText = "/:name=\"\" ";

        var result = ColumnFilterParser.ParseColumnFilters(searchText);

        Assert.That(result.Count, Is.EqualTo(1));
        Assert.That(result[0].ColumnName, Is.EqualTo("name"));
        Assert.That(result[0].Operator, Is.EqualTo("="));
        Assert.That(result[0].Value, Is.EqualTo(""));
    }

    [Test]
    public void ParseColumnFilters_QuotedValueWithoutEndQuote_DoesNotMatch()
    {
        var searchText = "/:name=\"Visual Studio ";

        var result = ColumnFilterParser.ParseColumnFilters(searchText);

        Assert.That(result.Count, Is.EqualTo(0), "Incomplete quoted values should not be parsed as complete filters");
    }

    // Tests for excluding Sort filters from ParseSearchString
    [Test]
    public void ParseSearchString_SortAscFilter_RemoveFilter()
    {
        var searchText = "/:SortAsc=Name";

        var result = ColumnFilterParser.ParseSearchString(searchText);

        Assert.That(result, Is.EqualTo(""));
    }

    [Test]
    public void ParseSearchString_SortDscFilter_RemoveFilter()
    {
        var searchText = "/:SortDsc=PID";

        var result = ColumnFilterParser.ParseSearchString(searchText);

        Assert.That(result, Is.EqualTo(""));
    }

    [Test]
    public void ParseSearchString_SortAscWithText_RemoveFilterAndText()
    {
        var searchText = "search text /:SortAsc=Name more text";

        var result = ColumnFilterParser.ParseSearchString(searchText);

        Assert.That(result, Is.EqualTo("search text more text"));
    }

    [Test]
    public void ParseSearchString_SortDscWithText_RemoveFilterAndText()
    {
        var searchText = "search text /:SortDsc=CPU more text";

        var result = ColumnFilterParser.ParseSearchString(searchText);

        Assert.That(result, Is.EqualTo("search text more text"));
    }

    [Test]
    public void ParseSearchString_MixedSortAndColumnFilters_RemovesFilters()
    {
        var searchText = "search /:SortAsc=Name /:status=running /:SortDsc=PID /:name=test";

        var result = ColumnFilterParser.ParseSearchString(searchText);

        Assert.That(result, Is.EqualTo("search"));
    }

    [Test]
    public void ParseSearchString_MultipleSortFilters_RemoveAllSortFilters()
    {
        var searchText = "/:SortAsc=Name /:SortDsc=CPU /:SortAsc=PID";

        var result = ColumnFilterParser.ParseSearchString(searchText);

        Assert.That(result, Is.EqualTo(""));
    }

    [Test]
    public void ParseSearchString_SortFiltersWithDifferentOperators_RemoveCorrectly()
    {
        var searchText = "/:SortAsc==Name /:SortDsc!=PID";

        var result = ColumnFilterParser.ParseSearchString(searchText);

        Assert.That(result, Is.EqualTo(""));
    }

    [Test]
    public void ParseSearchString_IncompleteSortFilters_RemoveIncompleteFilters()
    {
        var searchText = "/:SortAsc= /:SortDsc search text";

        var result = ColumnFilterParser.ParseSearchString(searchText);

        Assert.That(result, Is.EqualTo("search text"));
    }

    [Test]
    public void ParseSearchString_CaseInsensitiveSortFilters_RemoveFilters()
    {
        var searchText = "/:sortasc=Name /:SORTDSC=PID";

        var result = ColumnFilterParser.ParseSearchString(searchText);

        Assert.That(result, Is.EqualTo(""));
    }

    [Test]
    public void ParseSearchString_OnlySortFilters_RemoveEverything()
    {
        var searchText = "/:SortAsc=Name /:SortDsc=CPU";

        var result = ColumnFilterParser.ParseSearchString(searchText);

        Assert.That(result, Is.EqualTo(""));
    }

    [Test]
    public void ParseSearchString_SortFiltersWithSpaceNormalization_RemoveAndNormalizes()
    {
        var searchText = "search   /:SortAsc=Name    /:status=running    more   text";

        var result = ColumnFilterParser.ParseSearchString(searchText);

        Assert.That(result, Is.EqualTo("search more text"));
    }
}