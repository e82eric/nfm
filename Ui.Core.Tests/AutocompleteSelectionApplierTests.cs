using nfm.Win32Ui;
using NUnit.Framework;

namespace nfm.Ui.Core.Tests;

// [TestFixture]
// public class AutocompleteSelectionApplierTests
// {
//     [Test]
//     public void SetKey_NoShift()
//     {
//         var result = AutocompleteSelectionApplier.Apply("", TokenCursorPosition.Key, 0, "zap", false);
//         Assert.That(result, Is.EqualTo("zap="));
//     }
//     
//     [Test]
//     public void SetKey_WithShift()
//     {
//         var result = AutocompleteSelectionApplier.Apply("", TokenCursorPosition.Key, 0, "zap", true);
//         Assert.That(result, Is.EqualTo("zap="));
//     }
//     
//     [Test]
//     public void SetKeyWithPrefix_NoShift()
//     {
//         var result = AutocompleteSelectionApplier.Apply("fo", TokenCursorPosition.Key, 2, "foo", false);
//         Assert.That(result, Is.EqualTo("foo="));
//     }
//     
//     [Test]
//     public void SetOperatorWithPrefix()
//     {
//         var result = AutocompleteSelectionApplier.Apply("foo=", TokenCursorPosition.Operator, 1, "!=", false);
//         Assert.That(result, Is.EqualTo("foo!="));
//     }
//     
//     [Test]
//     public void SetValueNoPrefix_NoShift()
//     {
//         var result = AutocompleteSelectionApplier.Apply("foo==", TokenCursorPosition.Operator, 0, "bar", false);
//         Assert.That(result, Is.EqualTo("foo==bar "));
//     }
//     
//     [Test]
//     public void SetValueWithPrefix_NoShift()
//     {
//         var result = AutocompleteSelectionApplier.Apply("foo==b", TokenCursorPosition.Operator, 1, "bar", false);
//         Assert.That(result, Is.EqualTo("foo==bar "));
//     }
//     
//     [Test]
//     public void SetValueSecondValueNoPrefix_NoShift()
//     {
//         var result = AutocompleteSelectionApplier.Apply("foo==bar,", TokenCursorPosition.Operator, 0, "baz", false);
//         Assert.That(result, Is.EqualTo("foo==bar,baz "));
//     }
//     
//     [Test]
//     public void SetValueSecondValueWithPrefix_NoShift()
//     {
//         var result = AutocompleteSelectionApplier.Apply("foo==bar,ba", TokenCursorPosition.Operator, 2, "baz", false);
//         Assert.That(result, Is.EqualTo("foo==bar,baz "));
//     }
//     
//     [Test]
//     public void SetValueNoPrefix_WithShift()
//     {
//         var result = AutocompleteSelectionApplier.Apply("foo==", TokenCursorPosition.Operator, 0, "bar", true);
//         Assert.That(result, Is.EqualTo("foo==bar,"));
//     }
//     
//     [Test]
//     public void SetValueWithPrefix_WithShift()
//     {
//         var result = AutocompleteSelectionApplier.Apply("foo==b", TokenCursorPosition.Operator, 1, "bar", true);
//         Assert.That(result, Is.EqualTo("foo==bar,"));
//     }
//     
//     [Test]
//     public void SetValueSecondValueNoPrefix_WithShift()
//     {
//         var result = AutocompleteSelectionApplier.Apply("foo==bar,", TokenCursorPosition.Operator, 0, "baz", true);
//         Assert.That(result, Is.EqualTo("foo==bar,baz,"));
//     }
//     
//     [Test]
//     public void SetValueSecondValueWithPrefix_WithShift()
//     {
//         var result = AutocompleteSelectionApplier.Apply("foo==bar,ba", TokenCursorPosition.Operator, 2, "baz", true);
//         Assert.That(result, Is.EqualTo("foo==bar,baz,"));
//     }
//     
//     [Test]
//     public void SetValueSecondValueWithSpace_NoShift()
//     {
//         var result = AutocompleteSelectionApplier.Apply("foo==", TokenCursorPosition.Value, 0, "bar baz", false);
//         Assert.That(result, Is.EqualTo("foo==\"bar baz\" "));
//     }
//     
//     [Test]
//     public void SetValueSecondValueWithSpaceAndDoubleQuotes_NoShift()
//     {
//         var result = AutocompleteSelectionApplier.Apply("foo==", TokenCursorPosition.Value, 0, "\"bar baz\"", false);
//         Assert.That(result, Is.EqualTo("foo==\"\\\"bar baz\\\"\" "));
//     }
// }