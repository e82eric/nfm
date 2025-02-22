using System.Drawing;
using Core;
using nfm.menu;
using NUnit.Framework;

namespace Ui.Core.Tests
{
    [TestFixture]
    public class TextWrapTests()
    {
        [Test]
        public void Test1()
        {
            var segment = new TextSegment()
            {
                Text = """C:\Users\eric\src\nfzf\PowershellHistoryReader\bin\debug\net9.0\PowershellHistoryReader.exe | bat --style=plain --paging=never --color=always --theme="Visual Studio Dark+" --language=ps1 | C:\Users\eric\src\nfzf\PInvokeTest\bin\debug\net9.0\nfm.exe --linecontinuation `` --gap""",
                State = new AnsiState()
            };
            
            var line = new EscapedLine([segment]);
            var wrapped = line.WrapLines();
        }
    }
    
    [TestFixture]
    public class ListBoxTerminalEscapeCodeConverterTests()
    {
        [Test]
        public void Convert_TextWith24BitColorEscapeSequence_ParsesCorrectRgbValues()
        {
            string inputLine = "Test \x1B[38;2;123;45;67mColor\r\nAnotherLine";
            var input = new List<string> { inputLine };

            //var result = TerminalEscapeCodeConverter.ConvertToColoredListBoxLine(inputLine, [1, 2, 9, 10, 11, 20, 21]);

            //Assert.That(result.Count, Is.EqualTo(1));
            //var segments = result[0];
            //Assert.That(segments.Count, Is.EqualTo(2));
            //Assert.That(segments[0].Text, Is.EqualTo("Test "));
            //Assert.That(segments[1].Text, Is.EqualTo("Color"));
            //Color expected = Color.FromArgb(123, 45, 67);
            //Assert.That(segments[1].State.Foreground, Is.EqualTo(expected));
        }
    }
    
    [TestFixture]
    public class TerminalEscapeCodeConverterTests
    {
        private readonly string _defaultForeground = "#a89984";

        [Test]
        public void Convert_EmptyLine_ReturnsBlankLineSegment()
        {
            var input = new List<string> { "" };

            var result = TerminalEscapeCodeConverter.Convert(input);

            Assert.That(result.Count, Is.EqualTo(1));
            var lineSegments = result[0];
            Assert.That(lineSegments.Count, Is.EqualTo(1));
            Assert.That(lineSegments[0].Text, Is.EqualTo(string.Empty));
        }

        [Test]
        public void BasicText_ReturnsCorrectSegments()
        {
            var result = TextSegment.BasicText("Test");

            Assert.That(result.Count, Is.EqualTo(1));
            var lineSegments = result[0];
            Assert.That(lineSegments.Count, Is.EqualTo(1));
            Assert.That(lineSegments[0].Text, Is.EqualTo("Test"));

            Color expected = ColorTranslator.FromHtml(_defaultForeground);
            Assert.That(lineSegments[0].State.Foreground, Is.EqualTo(expected));
        }

        [Test]
        public void Convert_PlainText_NoEscapeSequences()
        {
            var input = new List<string> { "Hello world" };

            var result = TerminalEscapeCodeConverter.Convert(input);

            Assert.That(result.Count, Is.EqualTo(1));
            var segments = result[0];
            Assert.That(segments.Count, Is.EqualTo(1));
            Assert.That(segments[0].Text, Is.EqualTo("Hello world"));

            Color expected = ColorTranslator.FromHtml(_defaultForeground);
            Assert.That(segments[0].State.Foreground, Is.EqualTo(expected));
            Assert.That(segments[0].State.Bold, Is.False);
        }

        [Test]
        public void Convert_TextWithBoldEscapeSequence_ChangesStateForFollowingText()
        {
            string inputLine = "Hello \x1B[1mWorld";
            var input = new List<string> { inputLine };

            var result = TerminalEscapeCodeConverter.Convert(input);

            Assert.That(result.Count, Is.EqualTo(1));
            var segments = result[0];
            Assert.That(segments.Count, Is.EqualTo(2));

            Assert.That(segments[0].Text, Is.EqualTo("Hello "));
            Assert.That(segments[0].State.Bold, Is.False);

            Assert.That(segments[1].Text, Is.EqualTo("World"));
            Assert.That(segments[1].State.Bold, Is.True);
        }

        [Test]
        public void Convert_TextWithMultipleEscapeSequences_FormatsCorrectly()
        {
            string inputLine = "Normal \x1B[1;3mBoldItalic \x1B[0mReset";
            var input = new List<string> { inputLine };

            var result = TerminalEscapeCodeConverter.Convert(input);

            Assert.That(result.Count, Is.EqualTo(1));
            var segments = result[0];
            Assert.That(segments.Count, Is.EqualTo(3));

            Assert.That(segments[0].Text, Is.EqualTo("Normal "));
            Assert.That(segments[0].State.Bold, Is.False);
            Assert.That(segments[0].State.Italic, Is.False);
            Color defaultForeground = ColorTranslator.FromHtml(_defaultForeground);
            Assert.That(segments[0].State.Foreground, Is.EqualTo(defaultForeground));

            Assert.That(segments[1].Text, Is.EqualTo("BoldItalic "));
            Assert.That(segments[1].State.Bold, Is.True);
            Assert.That(segments[1].State.Italic, Is.True);

            Assert.That(segments[2].Text, Is.EqualTo("Reset"));
            Color resetForeground = ColorTranslator.FromHtml(_defaultForeground);
            Assert.That(segments[2].State.Bold, Is.False);
            Assert.That(segments[2].State.Italic, Is.False);
            Assert.That(segments[2].State.Foreground, Is.EqualTo(resetForeground));
        }

        [Test]
        public void Convert_TextWithExtended256ColorEscapeSequence_ChangesForegroundColor()
        {
            string inputLine = "Color \x1B[38;5;196mRed";
            var input = new List<string> { inputLine };

            var result = TerminalEscapeCodeConverter.Convert(input);

            Assert.That(result.Count, Is.EqualTo(1));
            var segments = result[0];
            Assert.That(segments.Count, Is.EqualTo(2));

            Assert.That(segments[0].Text, Is.EqualTo("Color "));
            Color defaultForeground = ColorTranslator.FromHtml(_defaultForeground);
            Assert.That(segments[0].State.Foreground, Is.EqualTo(defaultForeground));

            Assert.That(segments[1].Text, Is.EqualTo("Red"));
            Color expectedRed = Color.FromArgb(255, 0, 0);
            Assert.That(segments[1].State.Foreground, Is.EqualTo(expectedRed));
        }

        [Test]
        public void Convert_TextWithIncompleteEscapeSequence_TreatsRemainingTextAsPlainText()
        {
            string inputLine = "Hello \x1B[";
            var input = new List<string> { inputLine };

            var result = TerminalEscapeCodeConverter.Convert(input);

            Assert.That(result.Count, Is.EqualTo(1));
            var segments = result[0];
            Assert.That(segments.Count, Is.EqualTo(2));
            Assert.That(segments[0].Text, Is.EqualTo("Hello "));
            Assert.That(segments[1].Text, Is.EqualTo("\x1B["));
        }

        [Test]
        public void Convert_TextWithNonSgrEscapeSequence_TreatsEscAsPlainText()
        {
            string inputLine = "A\x1BX";
            var input = new List<string> { inputLine };

            var result = TerminalEscapeCodeConverter.Convert(input);

            Assert.That(result.Count, Is.EqualTo(1));
            var segments = result[0];
            Assert.That(segments.Count, Is.EqualTo(3));
            Assert.That(segments[0].Text, Is.EqualTo("A"));
            Assert.That(segments[1].Text, Is.EqualTo("\x1B"));
            Assert.That(segments[2].Text, Is.EqualTo("X"));
        }

        [Test]
        public void Convert_TextWithEscapeSequenceAtStart_AppliesFormattingToAllText()
        {
            string inputLine = "\x1B[1mHello";
            var input = new List<string> { inputLine };

            var result = TerminalEscapeCodeConverter.Convert(input);

            Assert.That(result.Count, Is.EqualTo(1));
            var segments = result[0];
            Assert.That(segments.Count, Is.EqualTo(1));
            Assert.That(segments[0].Text, Is.EqualTo("Hello"));
            Assert.That(segments[0].State.Bold, Is.True);
        }

        [Test]
        public void Convert_TextWithContradictoryBoldCodes_ResultsInNormalBoldState()
        {
            string inputLine = "Hello \x1B[1;22mWorld";
            var input = new List<string> { inputLine };

            var result = TerminalEscapeCodeConverter.Convert(input);

            Assert.That(result.Count, Is.EqualTo(1));
            var segments = result[0];
            Assert.That(segments.Count, Is.EqualTo(2));
            Assert.That(segments[0].Text, Is.EqualTo("Hello "));
            Assert.That(segments[0].State.Bold, Is.False);
            Assert.That(segments[1].Text, Is.EqualTo("World"));
            Assert.That(segments[1].State.Bold, Is.False);
        }

        [Test]
        public void Convert_TextWith24BitColorEscapeSequence_ParsesCorrectRgbValues()
        {
            string inputLine = "Test \x1B[38;2;123;45;67mColor";
            var input = new List<string> { inputLine };

            var result = TerminalEscapeCodeConverter.Convert(input);

            Assert.That(result.Count, Is.EqualTo(1));
            var segments = result[0];
            Assert.That(segments.Count, Is.EqualTo(2));
            Assert.That(segments[0].Text, Is.EqualTo("Test "));
            Assert.That(segments[1].Text, Is.EqualTo("Color"));
            Color expected = Color.FromArgb(123, 45, 67);
            Assert.That(segments[1].State.Foreground, Is.EqualTo(expected));
        }
    }
}
