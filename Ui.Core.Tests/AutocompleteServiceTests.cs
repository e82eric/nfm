using NUnit.Framework;
using nfm.Ui.Core;

namespace nfm.Ui.Core.Tests;

[TestFixture]
public class AutocompleteServiceTests
{
    [Test]
    public void ApplySelection_ColumnNameCompletion_AddsEqualsAndSetsCursor()
    {
        // Arrange
        var currentText = "/:st";
        var currentCursorPosition = 4;

        // Act
        var result = AutocompleteService.ApplySelection(currentText, currentCursorPosition, "status");

        // Assert
        Assert.That(result.Success, Is.True);
        Assert.That(result.NewText, Is.EqualTo("/:status="));
        Assert.That(result.NewCursorPosition, Is.EqualTo(9));
        Assert.That(result.CompletedColumn, Is.True);
        Assert.That(result.CompletedValue, Is.False);
    }

    [Test]
    public void ApplySelection_ColumnNameCompletion_WithTextAfterCursor()
    {
        // Arrange
        var currentText = "/:st more text";
        var currentCursorPosition = 4;

        // Act
        var result = AutocompleteService.ApplySelection(currentText, currentCursorPosition, "status");

        // Assert
        Assert.That(result.Success, Is.True);
        Assert.That(result.NewText, Is.EqualTo("/:status= more text"));
        Assert.That(result.NewCursorPosition, Is.EqualTo(9));
        Assert.That(result.CompletedColumn, Is.True);
        Assert.That(result.CompletedValue, Is.False);
    }

    [Test]
    public void ApplySelection_ValueCompletion_SingleEquals_AddsSpaceAfterValue()
    {
        // Arrange
        var currentText = "/:status=runn";
        var currentCursorPosition = 13;

        // Act
        var result = AutocompleteService.ApplySelection(currentText, currentCursorPosition, "running");

        // Assert
        Assert.That(result.Success, Is.True);
        Assert.That(result.NewText, Is.EqualTo("/:status=running "));
        Assert.That(result.NewCursorPosition, Is.EqualTo(17));
        Assert.That(result.CompletedValue, Is.True);
        Assert.That(result.CompletedColumn, Is.False);
    }

    [Test]
    public void ApplySelection_ValueCompletion_DoubleEquals_AddsSpaceAfterValue()
    {
        // Arrange
        var currentText = "/:status==runn";
        var currentCursorPosition = 14;

        // Act
        var result = AutocompleteService.ApplySelection(currentText, currentCursorPosition, "running");

        // Assert
        Assert.That(result.Success, Is.True);
        Assert.That(result.NewText, Is.EqualTo("/:status==running "));
        Assert.That(result.NewCursorPosition, Is.EqualTo(18));
        Assert.That(result.CompletedValue, Is.True);
        Assert.That(result.CompletedColumn, Is.False);
    }

    [Test]
    public void ApplySelection_ValueCompletion_NotEquals_AddsSpaceAfterValue()
    {
        // Arrange
        var currentText = "/:status!=runn";
        var currentCursorPosition = 14;

        // Act
        var result = AutocompleteService.ApplySelection(currentText, currentCursorPosition, "running");

        // Assert
        Assert.That(result.Success, Is.True);
        Assert.That(result.NewText, Is.EqualTo("/:status!=running "));
        Assert.That(result.NewCursorPosition, Is.EqualTo(18));
        Assert.That(result.CompletedValue, Is.True);
        Assert.That(result.CompletedColumn, Is.False);
    }

    [Test]
    public void ApplySelection_ValueCompletion_RegexMatch_AddsSpaceAfterValue()
    {
        // Arrange
        var currentText = "/:name=~note";
        var currentCursorPosition = 12;

        // Act
        var result = AutocompleteService.ApplySelection(currentText, currentCursorPosition, "notepad");

        // Assert
        Assert.That(result.Success, Is.True);
        Assert.That(result.NewText, Is.EqualTo("/:name=~notepad "));
        Assert.That(result.NewCursorPosition, Is.EqualTo(16));
        Assert.That(result.CompletedValue, Is.True);
        Assert.That(result.CompletedColumn, Is.False);
    }

    [Test]
    public void ApplySelection_ValueCompletion_RegexNotMatch_AddsSpaceAfterValue()
    {
        // Arrange
        var currentText = "/:name!~note";
        var currentCursorPosition = 12;

        // Act
        var result = AutocompleteService.ApplySelection(currentText, currentCursorPosition, "notepad");

        // Assert
        Assert.That(result.Success, Is.True);
        Assert.That(result.NewText, Is.EqualTo("/:name!~notepad "));
        Assert.That(result.NewCursorPosition, Is.EqualTo(16));
        Assert.That(result.CompletedValue, Is.True);
        Assert.That(result.CompletedColumn, Is.False);
    }

    [Test]
    public void ApplySelection_ValueCompletion_WithTextAfterCursor()
    {
        // Arrange
        var currentText = "/:status=runn more text";
        var currentCursorPosition = 13;

        // Act
        var result = AutocompleteService.ApplySelection(currentText, currentCursorPosition, "running");

        // Assert
        Assert.That(result.Success, Is.True);
        Assert.That(result.NewText, Is.EqualTo("/:status=running  more text"));
        Assert.That(result.NewCursorPosition, Is.EqualTo(17));
        Assert.That(result.CompletedValue, Is.True);
        Assert.That(result.CompletedColumn, Is.False);
    }

    [Test]
    public void ApplySelection_EmptyValueCompletion_ReplacesEmptyValue()
    {
        // Arrange
        var currentText = "/:status=";
        var currentCursorPosition = 9;

        // Act
        var result = AutocompleteService.ApplySelection(currentText, currentCursorPosition, "running");

        // Assert
        Assert.That(result.Success, Is.True);
        Assert.That(result.NewText, Is.EqualTo("/:status=running "));
        Assert.That(result.NewCursorPosition, Is.EqualTo(17));
        Assert.That(result.CompletedValue, Is.True);
        Assert.That(result.CompletedColumn, Is.False);
    }

    [Test]
    public void ApplySelection_ComplexScenario_MultipleFiltersColumnCompletion()
    {
        // Arrange
        var currentText = "/:status=running /:na";
        var currentCursorPosition = 21;

        // Act
        var result = AutocompleteService.ApplySelection(currentText, currentCursorPosition, "name");

        // Assert
        Assert.That(result.Success, Is.True);
        Assert.That(result.NewText, Is.EqualTo("/:status=running /:name="));
        Assert.That(result.NewCursorPosition, Is.EqualTo(24));
        Assert.That(result.CompletedColumn, Is.True);
        Assert.That(result.CompletedValue, Is.False);
    }

    [Test]
    public void ApplySelection_ComplexScenario_MultipleFiltersValueCompletion()
    {
        // Arrange
        var currentText = "/:status=running /:name=note";
        var currentCursorPosition = 28;

        // Act
        var result = AutocompleteService.ApplySelection(currentText, currentCursorPosition, "notepad");

        // Assert
        Assert.That(result.Success, Is.True);
        Assert.That(result.NewText, Is.EqualTo("/:status=running /:name=notepad "));
        Assert.That(result.NewCursorPosition, Is.EqualTo(32));
        Assert.That(result.CompletedValue, Is.True);
        Assert.That(result.CompletedColumn, Is.False);
    }

    [Test]
    public void ApplySelection_WithSearchTextBefore_ColumnCompletion()
    {
        // Arrange
        var currentText = "search text /:st";
        var currentCursorPosition = 16;

        // Act
        var result = AutocompleteService.ApplySelection(currentText, currentCursorPosition, "status");

        // Assert
        Assert.That(result.Success, Is.True);
        Assert.That(result.NewText, Is.EqualTo("search text /:status="));
        Assert.That(result.NewCursorPosition, Is.EqualTo(21));
        Assert.That(result.CompletedColumn, Is.True);
        Assert.That(result.CompletedValue, Is.False);
    }

    [Test]
    public void ApplySelection_WithSearchTextBefore_ValueCompletion()
    {
        // Arrange
        var currentText = "search text /:status=runn";
        var currentCursorPosition = 25;

        // Act
        var result = AutocompleteService.ApplySelection(currentText, currentCursorPosition, "running");

        // Assert
        Assert.That(result.Success, Is.True);
        Assert.That(result.NewText, Is.EqualTo("search text /:status=running "));
        Assert.That(result.NewCursorPosition, Is.EqualTo(29));
        Assert.That(result.CompletedValue, Is.True);
        Assert.That(result.CompletedColumn, Is.False);
    }

    [Test]
    public void ApplySelection_CursorInMiddleOfText_ColumnCompletion()
    {
        // Arrange
        var currentText = "/:st more text";
        var currentCursorPosition = 4;

        // Act
        var result = AutocompleteService.ApplySelection(currentText, currentCursorPosition, "status");

        // Assert
        Assert.That(result.Success, Is.True);
        Assert.That(result.NewText, Is.EqualTo("/:status= more text"));
        Assert.That(result.NewCursorPosition, Is.EqualTo(9));
        Assert.That(result.CompletedColumn, Is.True);
        Assert.That(result.CompletedValue, Is.False);
    }

    [Test]
    public void ApplySelection_CursorInMiddleOfText_ValueCompletion()
    {
        // Arrange
        var currentText = "/:status=runn more text";
        var currentCursorPosition = 13;

        // Act
        var result = AutocompleteService.ApplySelection(currentText, currentCursorPosition, "running");

        // Assert
        Assert.That(result.Success, Is.True);
        Assert.That(result.NewText, Is.EqualTo("/:status=running  more text"));
        Assert.That(result.NewCursorPosition, Is.EqualTo(17));
        Assert.That(result.CompletedValue, Is.True);
        Assert.That(result.CompletedColumn, Is.False);
    }

    [Test]
    public void ApplySelection_NoColonSlashFound_ReturnsFalse()
    {
        // Arrange
        var currentText = "normal search text";
        var currentCursorPosition = 10;

        // Act
        var result = AutocompleteService.ApplySelection(currentText, currentCursorPosition, "something");

        // Assert
        Assert.That(result.Success, Is.False);
        Assert.That(result.ErrorMessage, Is.EqualTo("No column filter pattern found"));
    }

    [Test]
    public void ApplySelection_CursorAtBeginning_NoColonSlash_ReturnsFalse()
    {
        // Arrange
        var currentText = "/:status=running";
        var currentCursorPosition = 0;

        // Act
        var result = AutocompleteService.ApplySelection(currentText, currentCursorPosition, "something");

        // Assert
        Assert.That(result.Success, Is.False);
        Assert.That(result.ErrorMessage, Is.EqualTo("No column filter pattern found"));
    }

    [Test]
    public void ApplySelection_CursorBeyondTextLength_HandlesGracefully()
    {
        // Arrange
        var currentText = "/:st";
        var currentCursorPosition = 100; // Cursor beyond text length

        // Act
        var result = AutocompleteService.ApplySelection(currentText, currentCursorPosition, "status");

        // Assert
        Assert.That(result.Success, Is.True);
        Assert.That(result.NewText, Is.EqualTo("/:status="));
        Assert.That(result.NewCursorPosition, Is.EqualTo(9));
        Assert.That(result.CompletedColumn, Is.True);
        Assert.That(result.CompletedValue, Is.False);
    }

    [Test]
    public void ApplySelection_PartialColumnName_CompletesCorrectly()
    {
        // Arrange
        var currentText = "/:stat";
        var currentCursorPosition = 6;

        // Act
        var result = AutocompleteService.ApplySelection(currentText, currentCursorPosition, "status");

        // Assert
        Assert.That(result.Success, Is.True);
        Assert.That(result.NewText, Is.EqualTo("/:status="));
        Assert.That(result.NewCursorPosition, Is.EqualTo(9));
        Assert.That(result.CompletedColumn, Is.True);
        Assert.That(result.CompletedValue, Is.False);
    }

    [Test]
    public void ApplySelection_PartialValue_CompletesCorrectly()
    {
        // Arrange  
        var currentText = "/:status=run";
        var currentCursorPosition = 12;

        // Act
        var result = AutocompleteService.ApplySelection(currentText, currentCursorPosition, "running");

        // Assert
        Assert.That(result.Success, Is.True);
        Assert.That(result.NewText, Is.EqualTo("/:status=running "));
        Assert.That(result.NewCursorPosition, Is.EqualTo(17));
        Assert.That(result.CompletedValue, Is.True);
        Assert.That(result.CompletedColumn, Is.False);
    }

    [Test]
    public void ApplySelection_EmptyText_ReturnsFalse()
    {
        // Arrange
        var currentText = "";
        var currentCursorPosition = 0;

        // Act
        var result = AutocompleteService.ApplySelection(currentText, currentCursorPosition, "status");

        // Assert
        Assert.That(result.Success, Is.False);
        Assert.That(result.ErrorMessage, Is.EqualTo("No column filter pattern found"));
    }

    [Test]
    public void ApplySelection_OnlySlash_ReturnsFalse()
    {
        // Arrange
        var currentText = "/";
        var currentCursorPosition = 1;

        // Act
        var result = AutocompleteService.ApplySelection(currentText, currentCursorPosition, "status");

        // Assert
        Assert.That(result.Success, Is.False);
        Assert.That(result.ErrorMessage, Is.EqualTo("No column filter pattern found"));
    }

    [Test]
    public void ApplySelection_ColumnNameAtVeryEnd_CompletesCorrectly()
    {
        // Arrange
        var currentText = "some text /:name";
        var currentCursorPosition = 16;

        // Act
        var result = AutocompleteService.ApplySelection(currentText, currentCursorPosition, "name");

        // Assert
        Assert.That(result.Success, Is.True);
        Assert.That(result.NewText, Is.EqualTo("some text /:name="));
        Assert.That(result.NewCursorPosition, Is.EqualTo(17));
        Assert.That(result.CompletedColumn, Is.True);
        Assert.That(result.CompletedValue, Is.False);
    }

    [Test]
    public void ApplySelection_ValueWithSpaces_AddsQuotes()
    {
        // Arrange
        var currentText = "/:name=note";
        var currentCursorPosition = 11;

        // Act
        var result = AutocompleteService.ApplySelection(currentText, currentCursorPosition, "notepad with spaces");

        // Assert
        Assert.That(result.Success, Is.True);
        Assert.That(result.NewText, Is.EqualTo("/:name=\"notepad with spaces\" "));
        Assert.That(result.NewCursorPosition, Is.EqualTo(29)); // Fixed based on actual calculation
        Assert.That(result.CompletedValue, Is.True);
        Assert.That(result.CompletedColumn, Is.False);
    }

    [Test]
    public void ApplySelection_ValueWithoutSpaces_NoQuotes()
    {
        // Arrange
        var currentText = "/:name=note";
        var currentCursorPosition = 11;

        // Act
        var result = AutocompleteService.ApplySelection(currentText, currentCursorPosition, "notepad");

        // Assert
        Assert.That(result.Success, Is.True);
        Assert.That(result.NewText, Is.EqualTo("/:name=notepad "));
        Assert.That(result.NewCursorPosition, Is.EqualTo(15));
        Assert.That(result.CompletedValue, Is.True);
        Assert.That(result.CompletedColumn, Is.False);
    }

    [Test]
    public void ApplySelection_ValueWithSpaces_ComplexScenario()
    {
        // Arrange
        var currentText = "/:status=running /:name=note";
        var currentCursorPosition = 28;

        // Act
        var result = AutocompleteService.ApplySelection(currentText, currentCursorPosition, "Visual Studio Code");

        // Assert
        Assert.That(result.Success, Is.True);
        Assert.That(result.NewText, Is.EqualTo("/:status=running /:name=\"Visual Studio Code\" "));
        Assert.That(result.NewCursorPosition, Is.EqualTo(45)); // Fixed based on actual calculation
        Assert.That(result.CompletedValue, Is.True);
        Assert.That(result.CompletedColumn, Is.False);
    }

    [Test]
    public void ApplySelection_ValueWithSpaces_WithTextAfter()
    {
        // Arrange
        var currentText = "/:name=note more text";
        var currentCursorPosition = 11;

        // Act
        var result = AutocompleteService.ApplySelection(currentText, currentCursorPosition, "My Application");

        // Assert
        Assert.That(result.Success, Is.True);
        Assert.That(result.NewText, Is.EqualTo("/:name=\"My Application\"  more text"));
        Assert.That(result.NewCursorPosition, Is.EqualTo(24));
        Assert.That(result.CompletedValue, Is.True);
        Assert.That(result.CompletedColumn, Is.False);
    }
}