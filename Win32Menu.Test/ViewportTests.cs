using Core;
using NUnit.Framework;
namespace nfm.menu.Tests
{
    public static class TerminalEscapedLineExtensions
    {
        public static TerminalEscapedLine MultiLine(params string[] lines)
        {
            var result = new TerminalEscapedLine();
            foreach (var line in lines)
            {
                var segments = new List<TextSegment>();
                segments.Add(new TextSegment{State = new AnsiState(), Text = line});
                result.Lines.Add(new EscapedLine(segments));
            }

            return result;
        }
    }
    
    [TestFixture]
    public class ViewportTests_SetItems
    {
        [Test]
        public void SetItems_None()
        {
            int viewportRows = 3;
            var viewport = new Viewport(viewportRows);
            var items = new List<TerminalEscapedLine>();

            viewport.SetItems(items, wrap: false);
            
            Assert.That(viewport.SelectedIndex, Is.EqualTo(0));
            Assert.That(viewport.ViewportSelectedIndex, Is.EqualTo(0));

            Assert.That(viewport.EndRow, Is.EqualTo(0));
            Assert.That(viewport.StartRow, Is.EqualTo(0));
        }
        
        [Test]
        public void SetItems_WithMoreItem_AfterPageDown()
        {
            int viewportRows = 3;
            var viewport = new Viewport(viewportRows);
            var items = new List<TerminalEscapedLine>
            {
                TerminalEscapedLine.SimpleText("line1"),
                TerminalEscapedLine.SimpleText("line1"),
                TerminalEscapedLine.SimpleText("line1"),
                TerminalEscapedLine.SimpleText("line1"),
                TerminalEscapedLine.SimpleText("line1")
            };

            viewport.SetItems(items, wrap: false);
            
            Assert.That(viewport.StartRow, Is.EqualTo(0));
            Assert.That(viewport.EndRow, Is.EqualTo(2));
            Assert.That(viewport.SelectedIndex, Is.EqualTo(0));
            Assert.That(viewport.ViewportSelectedIndex, Is.EqualTo(0));
            
            viewport.PageDown();
            
            Assert.That(viewport.StartRow, Is.EqualTo(2));
            Assert.That(viewport.EndRow, Is.EqualTo(4));
            Assert.That(viewport.SelectedIndex, Is.EqualTo(2));
            Assert.That(viewport.ViewportSelectedIndex, Is.EqualTo(0));
            
            var newitems = new List<TerminalEscapedLine>
            {
                TerminalEscapedLine.SimpleText("line1"),
                TerminalEscapedLine.SimpleText("line1"),
                TerminalEscapedLine.SimpleText("line1"),
                TerminalEscapedLine.SimpleText("line1"),
                TerminalEscapedLine.SimpleText("line1"),
                TerminalEscapedLine.SimpleText("line1"),
                TerminalEscapedLine.SimpleText("line1"),
                TerminalEscapedLine.SimpleText("line1"),
                TerminalEscapedLine.SimpleText("line1"),
                TerminalEscapedLine.SimpleText("line1")
            };
            
            viewport.SetItems(newitems, wrap: false);
            
            Assert.That(viewport.StartRow, Is.EqualTo(2));
            Assert.That(viewport.EndRow, Is.EqualTo(4));
            Assert.That(viewport.SelectedIndex, Is.EqualTo(2));
            Assert.That(viewport.ViewportSelectedIndex, Is.EqualTo(0));
        }
        
        [Test]
        public void TestSetItemsWithoutWrap()
        {
            int viewportRows = 3;
            var viewport = new Viewport(viewportRows);
            var items = new List<TerminalEscapedLine>
            {
                TerminalEscapedLine.SimpleText("line1"),
                TerminalEscapedLine.SimpleText("line1"),
                TerminalEscapedLine.SimpleText("line1"),
                TerminalEscapedLine.SimpleText("line1"),
                TerminalEscapedLine.SimpleText("line1")
            };

            viewport.SetItems(items, wrap: false);
            
            Assert.That(viewport.SelectedIndex, Is.EqualTo(0));
            Assert.That(viewport.ViewportSelectedIndex, Is.EqualTo(0));

            Assert.That(viewport.StartRow, Is.EqualTo(0));
            Assert.That(viewport.EndRow, Is.EqualTo(2));
        }
        
        [Test]
        public void SetItemsWithoutWrap_SameNumberOfRowsAsViewport()
        {
            int viewportRows = 3;
            var viewport = new Viewport(viewportRows);
            var items = new List<TerminalEscapedLine>
            {
                TerminalEscapedLine.SimpleText("line1"),
                TerminalEscapedLine.SimpleText("line1"),
                TerminalEscapedLine.SimpleText("line1"),
            };

            viewport.SetItems(items, wrap: false);
            
            Assert.That(viewport.SelectedIndex, Is.EqualTo(0));
            Assert.That(viewport.ViewportSelectedIndex, Is.EqualTo(0));

            Assert.That(viewport.StartRow, Is.EqualTo(0));
            Assert.That(viewport.EndRow, Is.EqualTo(2));
        }
        
        [Test]
        public void SetItemsWithoutWrap_LessThanViewport()
        {
            int viewportRows = 3;
            var viewport = new Viewport(viewportRows);
            var items = new List<TerminalEscapedLine>
            {
                TerminalEscapedLine.SimpleText("line1"),
                TerminalEscapedLine.SimpleText("line1"),
            };

            viewport.SetItems(items, wrap: false);
            
            Assert.That(viewport.SelectedIndex, Is.EqualTo(0));
            Assert.That(viewport.ViewportSelectedIndex, Is.EqualTo(0));

            Assert.That(viewport.StartRow, Is.EqualTo(0));
            Assert.That(viewport.EndRow, Is.EqualTo(1));
        }
        
        [Test]
        public void TestSetItemsWithWrap()
        {
            TerminalEscapedLine MultiLine(params string[] lines)
            {
                var result = new TerminalEscapedLine();
                foreach (var line in lines)
                {
                    var segments = new List<TextSegment>();
                    segments.Add(new TextSegment{State = new AnsiState(), Text = line});
                    result.Lines.Add(new EscapedLine(segments));
                }

                return result;
            }
            int viewportRows = 3;
            var viewport = new Viewport(viewportRows);
            var items = new List<TerminalEscapedLine>
            {
                MultiLine("line1", "line2"),
                MultiLine("line1", "line2"),
                MultiLine("line1", "line2")
            };

            viewport.SetItems(items, wrap: true);
            
            Assert.That(viewport.SelectedIndex, Is.EqualTo(0));
            Assert.That(viewport.ViewportSelectedIndex, Is.EqualTo(0));
            Assert.That(viewport.EndRow, Is.EqualTo(1));
        }
    }
    
    [TestFixture]
    public class ViewportTests
    {
        [Test]
        public void TestSelectNextWithinViewport()
        {
            int viewportRows = 3;
            var viewport = new Viewport(viewportRows);
            var items = new List<TerminalEscapedLine>
            {
                TerminalEscapedLine.SimpleText("line1"),
                TerminalEscapedLine.SimpleText("line1"),
                TerminalEscapedLine.SimpleText("line1"),
                TerminalEscapedLine.SimpleText("line1"),
                TerminalEscapedLine.SimpleText("line1")
            };

            viewport.SetItems(items, wrap: false);

            viewport.SelectNext();

            Assert.That(viewport.SelectedIndex, Is.EqualTo(1));
            Assert.That(viewport.ViewportSelectedIndex, Is.EqualTo(1));
            
            Assert.That(viewport.StartRow, Is.EqualTo(0));
            Assert.That(viewport.EndRow, Is.EqualTo(2));
        }

        [Test]
        public void TestSelectNextExpandingViewport()
        {
            int viewportRows = 3;
            var viewport = new Viewport(viewportRows);
            var items = new List<TerminalEscapedLine>
            {
                TerminalEscapedLine.SimpleText("line1"),
                TerminalEscapedLine.SimpleText("line1"),
                TerminalEscapedLine.SimpleText("line1"),
                TerminalEscapedLine.SimpleText("line1"),
                TerminalEscapedLine.SimpleText("line1")
            };

            viewport.SetItems(items, wrap: false);

            viewport.SelectNext();
            viewport.SelectNext();

            Assert.That(viewport.StartRow, Is.EqualTo(0));
            Assert.That(viewport.EndRow, Is.EqualTo(2));

            viewport.SelectNext();
            Assert.That(viewport.StartRow, Is.EqualTo(1));
            Assert.That(viewport.EndRow, Is.EqualTo(3));
            
            viewport.SelectNext();
            Assert.That(viewport.StartRow, Is.EqualTo(2));
            Assert.That(viewport.EndRow, Is.EqualTo(4));
        }

        [Test]
        public void TestSelectPreviousWithinViewport()
        {
            int viewportRows = 3;
            var viewport = new Viewport(viewportRows);
            var items = new List<TerminalEscapedLine>
            {
                TerminalEscapedLine.SimpleText("line1"),
                TerminalEscapedLine.SimpleText("line1"),
                TerminalEscapedLine.SimpleText("line1"),
                TerminalEscapedLine.SimpleText("line1"),
                TerminalEscapedLine.SimpleText("line1")
            };

            viewport.SetItems(items, wrap: false);
            viewport.SetTotalRows(items.Count);
            
            Assert.That(viewport.StartRow, Is.EqualTo(0));
            Assert.That(viewport.EndRow, Is.EqualTo(2));
            Assert.That(viewport.SelectedIndex, Is.EqualTo(0));
            Assert.That(viewport.ViewportSelectedIndex, Is.EqualTo(0));

            viewport.SelectNext();
            
            Assert.That(viewport.StartRow, Is.EqualTo(0));
            Assert.That(viewport.EndRow, Is.EqualTo(2));
            Assert.That(viewport.SelectedIndex, Is.EqualTo(1));
            Assert.That(viewport.ViewportSelectedIndex, Is.EqualTo(1));
            
            viewport.SelectNext();
            
            Assert.That(viewport.StartRow, Is.EqualTo(0));
            Assert.That(viewport.EndRow, Is.EqualTo(2));
            Assert.That(viewport.SelectedIndex, Is.EqualTo(2));
            Assert.That(viewport.ViewportSelectedIndex, Is.EqualTo(2));

            viewport.SelectPrevious();

            Assert.That(viewport.StartRow, Is.EqualTo(0));
            Assert.That(viewport.EndRow, Is.EqualTo(2));
            Assert.That(viewport.SelectedIndex, Is.EqualTo(1));
            Assert.That(viewport.ViewportSelectedIndex, Is.EqualTo(1));
        }

        [Test]
        public void TestSelectPreviousExpandingViewport()
        {
            int viewportRows = 3;
            var viewport = new Viewport(viewportRows);
            var items = new List<TerminalEscapedLine>
            {
                TerminalEscapedLine.SimpleText("line1"),
                TerminalEscapedLine.SimpleText("line1"),
                TerminalEscapedLine.SimpleText("line1"),
                TerminalEscapedLine.SimpleText("line1"),
                TerminalEscapedLine.SimpleText("line1")
            };

            viewport.SetItems(items, wrap: false);
            viewport.SetTotalRows(items.Count);
            
            Assert.That(viewport.StartRow, Is.EqualTo(0));
            Assert.That(viewport.EndRow, Is.EqualTo(2));
            Assert.That(viewport.SelectedIndex, Is.EqualTo(0));
            Assert.That(viewport.ViewportSelectedIndex, Is.EqualTo(0));

            viewport.SelectNext();
            
            Assert.That(viewport.StartRow, Is.EqualTo(0));
            Assert.That(viewport.EndRow, Is.EqualTo(2));
            Assert.That(viewport.SelectedIndex, Is.EqualTo(1));
            Assert.That(viewport.ViewportSelectedIndex, Is.EqualTo(1));
            
            viewport.SelectNext();
            
            Assert.That(viewport.StartRow, Is.EqualTo(0));
            Assert.That(viewport.EndRow, Is.EqualTo(2));
            Assert.That(viewport.SelectedIndex, Is.EqualTo(2));
            Assert.That(viewport.ViewportSelectedIndex, Is.EqualTo(2));
            
            viewport.SelectNext();
            
            Assert.That(viewport.StartRow, Is.EqualTo(1));
            Assert.That(viewport.EndRow, Is.EqualTo(3));
            Assert.That(viewport.SelectedIndex, Is.EqualTo(3));
            Assert.That(viewport.ViewportSelectedIndex, Is.EqualTo(2));

            viewport.SelectPrevious();

            Assert.That(viewport.StartRow, Is.EqualTo(1));
            Assert.That(viewport.EndRow, Is.EqualTo(3));
            Assert.That(viewport.SelectedIndex, Is.EqualTo(2));
            Assert.That(viewport.ViewportSelectedIndex, Is.EqualTo(1));
            
            viewport.SelectPrevious();

            Assert.That(viewport.StartRow, Is.EqualTo(1));
            Assert.That(viewport.EndRow, Is.EqualTo(3));
            Assert.That(viewport.SelectedIndex, Is.EqualTo(1));
            Assert.That(viewport.ViewportSelectedIndex, Is.EqualTo(0));
            
            viewport.SelectPrevious();

            Assert.That(viewport.StartRow, Is.EqualTo(0));
            Assert.That(viewport.EndRow, Is.EqualTo(2));
            Assert.That(viewport.SelectedIndex, Is.EqualTo(0));
            Assert.That(viewport.ViewportSelectedIndex, Is.EqualTo(0));
        }

        [Test]
        public void TestSelectPageDown()
        {
            int viewportRows = 3;
            var viewport = new Viewport(viewportRows);
            var items = new List<TerminalEscapedLine>
            {
                TerminalEscapedLine.SimpleText("line1"),
                TerminalEscapedLine.SimpleText("line1"),
                TerminalEscapedLine.SimpleText("line1"),
                TerminalEscapedLine.SimpleText("line1"),
                TerminalEscapedLine.SimpleText("line1")
            };

            viewport.SetItems(items, wrap: false);

            viewport.PageDown();

            Assert.That(viewport.StartRow, Is.EqualTo(2));
            Assert.That(viewport.EndRow, Is.EqualTo(4));
            Assert.That(viewport.SelectedIndex, Is.EqualTo(2));
            Assert.That(viewport.ViewportSelectedIndex, Is.EqualTo(0));
        }
        
        [Test]
        public void TestPageDownTilEnd()
        {
            int viewportRows = 3;
            var viewport = new Viewport(viewportRows);
            var items = new List<TerminalEscapedLine>
            {
                TerminalEscapedLine.SimpleText("line1"),
                TerminalEscapedLine.SimpleText("line2"),
                TerminalEscapedLine.SimpleText("line3"),
                TerminalEscapedLine.SimpleText("line4"),
                TerminalEscapedLine.SimpleText("line5")
            };

            viewport.SetItems(items, wrap: false);
            
            viewport.PageDown();
            Assert.That(viewport.StartRow, Is.EqualTo(2));
            Assert.That(viewport.EndRow, Is.EqualTo(4));
            Assert.That(viewport.SelectedIndex, Is.EqualTo(2));
            Assert.That(viewport.ViewportSelectedIndex, Is.EqualTo(0));

            viewport.PageDown();
            Assert.That(viewport.StartRow, Is.EqualTo(2));
            Assert.That(viewport.EndRow, Is.EqualTo(4));
            Assert.That(viewport.SelectedIndex, Is.EqualTo(4));
            Assert.That(viewport.ViewportSelectedIndex, Is.EqualTo(2));
        }
        
        [Test]
        public void TestPageDownMultiplePages()
        {
            int viewportRows = 3;
            var viewport = new Viewport(viewportRows);
            var items = new List<TerminalEscapedLine>
            {
                TerminalEscapedLine.SimpleText("line1"),
                TerminalEscapedLine.SimpleText("line2"),
                TerminalEscapedLine.SimpleText("line3"),
                TerminalEscapedLine.SimpleText("line4"),
                TerminalEscapedLine.SimpleText("line5"),
                TerminalEscapedLine.SimpleText("line6"),
                TerminalEscapedLine.SimpleText("line7"),
                TerminalEscapedLine.SimpleText("line8"),
                TerminalEscapedLine.SimpleText("line9"),
                TerminalEscapedLine.SimpleText("line10")
            };

            viewport.SetItems(items, wrap: false);
            Assert.That(viewport.StartRow, Is.EqualTo(0));
            Assert.That(viewport.EndRow, Is.EqualTo(2));
            Assert.That(viewport.SelectedIndex, Is.EqualTo(0));
            Assert.That(viewport.ViewportSelectedIndex, Is.EqualTo(0));
            
            viewport.PageDown();
            Assert.That(viewport.StartRow, Is.EqualTo(3));
            Assert.That(viewport.EndRow, Is.EqualTo(5));
            Assert.That(viewport.SelectedIndex, Is.EqualTo(3));
            Assert.That(viewport.ViewportSelectedIndex, Is.EqualTo(0));

            viewport.PageDown();
            Assert.That(viewport.StartRow, Is.EqualTo(6));
            Assert.That(viewport.EndRow, Is.EqualTo(8));
            Assert.That(viewport.SelectedIndex, Is.EqualTo(6));
            Assert.That(viewport.ViewportSelectedIndex, Is.EqualTo(0));
            
            viewport.PageDown();
            Assert.That(viewport.StartRow, Is.EqualTo(7));
            Assert.That(viewport.EndRow, Is.EqualTo(9));
            Assert.That(viewport.SelectedIndex, Is.EqualTo(7));
            Assert.That(viewport.ViewportSelectedIndex, Is.EqualTo(0));
        }

        [Test]
        public void TestSelectHalfPageUp()
        {
            int viewportRows = 3;
            var viewport = new Viewport(viewportRows);
            var items = new List<TerminalEscapedLine>
            {
                TerminalEscapedLine.SimpleText("line1"),
                TerminalEscapedLine.SimpleText("line1"),
                TerminalEscapedLine.SimpleText("line1"),
                TerminalEscapedLine.SimpleText("line1"),
                TerminalEscapedLine.SimpleText("line1")
            };

            viewport.SetItems(items, wrap: false);

            viewport.PageDown();

            Assert.That(viewport.StartRow, Is.EqualTo(2));
            Assert.That(viewport.EndRow, Is.EqualTo(4));
            Assert.That(viewport.SelectedIndex, Is.EqualTo(2));
            Assert.That(viewport.ViewportSelectedIndex, Is.EqualTo(0));

            viewport.PageUp();

            Assert.That(viewport.StartRow, Is.EqualTo(0));
            Assert.That(viewport.EndRow, Is.EqualTo(2));
            Assert.That(viewport.SelectedIndex, Is.EqualTo(0));
            Assert.That(viewport.ViewportSelectedIndex, Is.EqualTo(0));
        }
        
        [Test]
        public void TestPageUpMultiplePages()
        {
            int viewportRows = 3;
            var viewport = new Viewport(viewportRows);
            var items = new List<TerminalEscapedLine>
            {
                TerminalEscapedLine.SimpleText("line1"),
                TerminalEscapedLine.SimpleText("line1"),
                TerminalEscapedLine.SimpleText("line1"),
                TerminalEscapedLine.SimpleText("line1"),
                TerminalEscapedLine.SimpleText("line1"),
                TerminalEscapedLine.SimpleText("line1"),
                TerminalEscapedLine.SimpleText("line1"),
                TerminalEscapedLine.SimpleText("line1"),
                TerminalEscapedLine.SimpleText("line1"),
                TerminalEscapedLine.SimpleText("line1")
            };

            viewport.SetItems(items, wrap: false);
            Assert.That(items.Count, Is.EqualTo(10));
            
            Assert.That(viewport.StartRow, Is.EqualTo(0));
            Assert.That(viewport.EndRow, Is.EqualTo(2));
            Assert.That(viewport.SelectedIndex, Is.EqualTo(0));
            Assert.That(viewport.ViewportSelectedIndex, Is.EqualTo(0));

            viewport.PageDown();

            Assert.That(viewport.StartRow, Is.EqualTo(3));
            Assert.That(viewport.EndRow, Is.EqualTo(5));
            Assert.That(viewport.SelectedIndex, Is.EqualTo(3));
            Assert.That(viewport.ViewportSelectedIndex, Is.EqualTo(0));

            viewport.PageDown();

            Assert.That(viewport.StartRow, Is.EqualTo(6));
            Assert.That(viewport.EndRow, Is.EqualTo(8));
            Assert.That(viewport.SelectedIndex, Is.EqualTo(6));
            Assert.That(viewport.ViewportSelectedIndex, Is.EqualTo(0));
            
            viewport.PageDown();

            Assert.That(viewport.StartRow, Is.EqualTo(7));
            Assert.That(viewport.EndRow, Is.EqualTo(9));
            Assert.That(viewport.SelectedIndex, Is.EqualTo(7));
            Assert.That(viewport.ViewportSelectedIndex, Is.EqualTo(0));
            
            viewport.PageDown();

            Assert.That(viewport.StartRow, Is.EqualTo(7));
            Assert.That(viewport.EndRow, Is.EqualTo(9));
            Assert.That(viewport.SelectedIndex, Is.EqualTo(9));
            Assert.That(viewport.ViewportSelectedIndex, Is.EqualTo(2));
            
            viewport.PageUp();

            Assert.That(viewport.StartRow, Is.EqualTo(4));
            Assert.That(viewport.EndRow, Is.EqualTo(6));
            Assert.That(viewport.SelectedIndex, Is.EqualTo(6));
            Assert.That(viewport.ViewportSelectedIndex, Is.EqualTo(2));
            
            viewport.PageUp();

            Assert.That(viewport.StartRow, Is.EqualTo(1));
            Assert.That(viewport.EndRow, Is.EqualTo(3));
            Assert.That(viewport.SelectedIndex, Is.EqualTo(3));
            Assert.That(viewport.ViewportSelectedIndex, Is.EqualTo(2));
            
            viewport.PageUp();
            
            Assert.That(viewport.StartRow, Is.EqualTo(0));
            Assert.That(viewport.EndRow, Is.EqualTo(2));
            Assert.That(viewport.SelectedIndex, Is.EqualTo(2));
            Assert.That(viewport.ViewportSelectedIndex, Is.EqualTo(2));
            
            viewport.PageUp();
            
            Assert.That(viewport.StartRow, Is.EqualTo(0));
            Assert.That(viewport.EndRow, Is.EqualTo(2));
            Assert.That(viewport.SelectedIndex, Is.EqualTo(0));
            Assert.That(viewport.ViewportSelectedIndex, Is.EqualTo(0));
            
            viewport.PageUp();
            
            Assert.That(viewport.StartRow, Is.EqualTo(0));
            Assert.That(viewport.EndRow, Is.EqualTo(2));
            Assert.That(viewport.SelectedIndex, Is.EqualTo(0));
            Assert.That(viewport.ViewportSelectedIndex, Is.EqualTo(0));
        }
        
        [Test]
        public void TestClipStartLines_SelectNextPrevious_Simple()
        {
            int viewportRows = 3;
            var viewport = new Viewport(viewportRows);
            var items = new List<TerminalEscapedLine>
            {
                TerminalEscapedLineExtensions.MultiLine("SubLine1,", "SubLine2"),
                TerminalEscapedLine.SimpleText("line1"),
                TerminalEscapedLine.SimpleText("line1"),
                TerminalEscapedLine.SimpleText("line1"),
                TerminalEscapedLine.SimpleText("line1"),
            };

            viewport.SetItems(items, wrap: true);
            Assert.That(items.Count, Is.EqualTo(5));
            
            Assert.That(viewport.StartRow, Is.EqualTo(0));
            Assert.That(viewport.EndRow, Is.EqualTo(1));
            Assert.That(viewport.SelectedIndex, Is.EqualTo(0));
            Assert.That(viewport.ViewportSelectedIndex, Is.EqualTo(0));
            Assert.That(viewport.StartLinesToClip, Is.EqualTo(0));

            viewport.SelectNext();
            
            Assert.That(viewport.StartRow, Is.EqualTo(0));
            Assert.That(viewport.EndRow, Is.EqualTo(1));
            Assert.That(viewport.SelectedIndex, Is.EqualTo(1));
            Assert.That(viewport.ViewportSelectedIndex, Is.EqualTo(1));
            Assert.That(viewport.StartLinesToClip, Is.EqualTo(0));
            
            viewport.SelectNext();
            
            Assert.That(viewport.StartRow, Is.EqualTo(0));
            Assert.That(viewport.EndRow, Is.EqualTo(2));
            Assert.That(viewport.SelectedIndex, Is.EqualTo(2));
            Assert.That(viewport.ViewportSelectedIndex, Is.EqualTo(2));
            Assert.That(viewport.StartLinesToClip, Is.EqualTo(1));
            
            viewport.SelectNext();
            
            Assert.That(viewport.StartRow, Is.EqualTo(1));
            Assert.That(viewport.EndRow, Is.EqualTo(3));
            Assert.That(viewport.SelectedIndex, Is.EqualTo(3));
            Assert.That(viewport.ViewportSelectedIndex, Is.EqualTo(2));
            Assert.That(viewport.StartLinesToClip, Is.EqualTo(0));
            
            viewport.SelectNext();
            
            Assert.That(viewport.StartRow, Is.EqualTo(2));
            Assert.That(viewport.EndRow, Is.EqualTo(4));
            Assert.That(viewport.SelectedIndex, Is.EqualTo(4));
            Assert.That(viewport.ViewportSelectedIndex, Is.EqualTo(2));
            Assert.That(viewport.StartLinesToClip, Is.EqualTo(0));
            
            viewport.SelectPrevious();
            
            Assert.That(viewport.StartRow, Is.EqualTo(2));
            Assert.That(viewport.EndRow, Is.EqualTo(4));
            Assert.That(viewport.SelectedIndex, Is.EqualTo(3));
            Assert.That(viewport.ViewportSelectedIndex, Is.EqualTo(1));
            Assert.That(viewport.StartLinesToClip, Is.EqualTo(0));
            
            viewport.SelectPrevious();
            
            Assert.That(viewport.StartRow, Is.EqualTo(2));
            Assert.That(viewport.EndRow, Is.EqualTo(4));
            Assert.That(viewport.SelectedIndex, Is.EqualTo(2));
            Assert.That(viewport.ViewportSelectedIndex, Is.EqualTo(0));
            Assert.That(viewport.StartLinesToClip, Is.EqualTo(0));
            
            viewport.SelectPrevious();
            
            Assert.That(viewport.StartRow, Is.EqualTo(1));
            Assert.That(viewport.EndRow, Is.EqualTo(3));
            Assert.That(viewport.SelectedIndex, Is.EqualTo(1));
            Assert.That(viewport.ViewportSelectedIndex, Is.EqualTo(0));
            Assert.That(viewport.StartLinesToClip, Is.EqualTo(0));
            
            viewport.SelectPrevious();
            
            Assert.That(viewport.StartRow, Is.EqualTo(0));
            Assert.That(viewport.EndRow, Is.EqualTo(1));
            Assert.That(viewport.SelectedIndex, Is.EqualTo(0));
            Assert.That(viewport.ViewportSelectedIndex, Is.EqualTo(0));
            Assert.That(viewport.StartLinesToClip, Is.EqualTo(0));
        }
        
        [Test]
        public void TestClipStartLines_SelectNextPrevious_Complex()
        {
            int viewportRows = 3;
            var viewport = new Viewport(viewportRows);
            var items = new List<TerminalEscapedLine>
            {
                TerminalEscapedLine.SimpleText("line1"),
                TerminalEscapedLineExtensions.MultiLine("SubLine1,", "SubLine2", "SubLine3"),
                TerminalEscapedLine.SimpleText("line1"),
                TerminalEscapedLineExtensions.MultiLine("SubLine1,", "SubLine2", "SubLine3", "SubLine4"),
                TerminalEscapedLine.SimpleText("line1"),
                TerminalEscapedLineExtensions.MultiLine("SubLine1,", "SubLine2"),
                TerminalEscapedLine.SimpleText("line1"),
            };

            viewport.SetItems(items, wrap: true);
            Assert.That(items.Count, Is.EqualTo(7));
            
            Assert.That(viewport.StartRow, Is.EqualTo(0));
            Assert.That(viewport.EndRow, Is.EqualTo(1));
            Assert.That(viewport.SelectedIndex, Is.EqualTo(0));
            Assert.That(viewport.ViewportSelectedIndex, Is.EqualTo(0));
            Assert.That(viewport.StartLinesToClip, Is.EqualTo(0));

            viewport.SelectNext();
            
            Assert.That(viewport.StartRow, Is.EqualTo(0));
            Assert.That(viewport.EndRow, Is.EqualTo(1));
            Assert.That(viewport.SelectedIndex, Is.EqualTo(1));
            Assert.That(viewport.ViewportSelectedIndex, Is.EqualTo(1));
            Assert.That(viewport.StartLinesToClip, Is.EqualTo(0));
            
            viewport.SelectNext();
            
            Assert.That(viewport.StartRow, Is.EqualTo(1));
            Assert.That(viewport.EndRow, Is.EqualTo(2));
            Assert.That(viewport.SelectedIndex, Is.EqualTo(2));
            Assert.That(viewport.ViewportSelectedIndex, Is.EqualTo(1));
            Assert.That(viewport.StartLinesToClip, Is.EqualTo(1));
            
            viewport.SelectNext();
            
            Assert.That(viewport.StartRow, Is.EqualTo(3));
            Assert.That(viewport.EndRow, Is.EqualTo(3));
            Assert.That(viewport.SelectedIndex, Is.EqualTo(3));
            Assert.That(viewport.ViewportSelectedIndex, Is.EqualTo(0));
            Assert.That(viewport.StartLinesToClip, Is.EqualTo(1));
            
            viewport.SelectNext();
            
            Assert.That(viewport.StartRow, Is.EqualTo(3));
            Assert.That(viewport.EndRow, Is.EqualTo(4));
            Assert.That(viewport.SelectedIndex, Is.EqualTo(4));
            Assert.That(viewport.ViewportSelectedIndex, Is.EqualTo(1));
            Assert.That(viewport.StartLinesToClip, Is.EqualTo(2));
            
            viewport.SelectNext();
            
            Assert.That(viewport.StartRow, Is.EqualTo(4));
            Assert.That(viewport.EndRow, Is.EqualTo(5));
            Assert.That(viewport.SelectedIndex, Is.EqualTo(5));
            Assert.That(viewport.ViewportSelectedIndex, Is.EqualTo(1));
            Assert.That(viewport.StartLinesToClip, Is.EqualTo(0));
            
            viewport.SelectNext();
            
            Assert.That(viewport.StartRow, Is.EqualTo(5));
            Assert.That(viewport.EndRow, Is.EqualTo(6));
            Assert.That(viewport.SelectedIndex, Is.EqualTo(6));
            Assert.That(viewport.ViewportSelectedIndex, Is.EqualTo(1));
            Assert.That(viewport.StartLinesToClip, Is.EqualTo(0));
            
            viewport.SelectNext();
            
            Assert.That(viewport.StartRow, Is.EqualTo(5));
            Assert.That(viewport.EndRow, Is.EqualTo(6));
            Assert.That(viewport.SelectedIndex, Is.EqualTo(6));
            Assert.That(viewport.ViewportSelectedIndex, Is.EqualTo(1));
            Assert.That(viewport.StartLinesToClip, Is.EqualTo(0));
            
            viewport.SelectPrevious();
            
            Assert.That(viewport.StartRow, Is.EqualTo(5));
            Assert.That(viewport.EndRow, Is.EqualTo(6));
            Assert.That(viewport.SelectedIndex, Is.EqualTo(5));
            Assert.That(viewport.ViewportSelectedIndex, Is.EqualTo(0));
            Assert.That(viewport.StartLinesToClip, Is.EqualTo(0));
            
            viewport.SelectPrevious();
            
            Assert.That(viewport.StartRow, Is.EqualTo(4));
            Assert.That(viewport.EndRow, Is.EqualTo(5));
            Assert.That(viewport.SelectedIndex, Is.EqualTo(4));
            Assert.That(viewport.ViewportSelectedIndex, Is.EqualTo(0));
            Assert.That(viewport.StartLinesToClip, Is.EqualTo(0));
            
            viewport.SelectPrevious();
            
            Assert.That(viewport.StartRow, Is.EqualTo(3));
            Assert.That(viewport.EndRow, Is.EqualTo(3));
            Assert.That(viewport.SelectedIndex, Is.EqualTo(3));
            Assert.That(viewport.ViewportSelectedIndex, Is.EqualTo(0));
            Assert.That(viewport.StartLinesToClip, Is.EqualTo(0));
            
            viewport.SelectPrevious();
            
            Assert.That(viewport.StartRow, Is.EqualTo(2));
            Assert.That(viewport.EndRow, Is.EqualTo(3));
            Assert.That(viewport.SelectedIndex, Is.EqualTo(2));
            Assert.That(viewport.ViewportSelectedIndex, Is.EqualTo(0));
            Assert.That(viewport.StartLinesToClip, Is.EqualTo(0));
            
            viewport.SelectPrevious();
            
            Assert.That(viewport.StartRow, Is.EqualTo(1));
            Assert.That(viewport.EndRow, Is.EqualTo(1));
            Assert.That(viewport.SelectedIndex, Is.EqualTo(1));
            Assert.That(viewport.ViewportSelectedIndex, Is.EqualTo(0));
            Assert.That(viewport.StartLinesToClip, Is.EqualTo(0));
            
            viewport.SelectPrevious();
            
            Assert.That(viewport.StartRow, Is.EqualTo(0));
            Assert.That(viewport.EndRow, Is.EqualTo(1));
            Assert.That(viewport.SelectedIndex, Is.EqualTo(0));
            Assert.That(viewport.ViewportSelectedIndex, Is.EqualTo(0));
            Assert.That(viewport.StartLinesToClip, Is.EqualTo(0));
            
            viewport.SelectPrevious();
            
            Assert.That(viewport.StartRow, Is.EqualTo(0));
            Assert.That(viewport.EndRow, Is.EqualTo(1));
            Assert.That(viewport.SelectedIndex, Is.EqualTo(0));
            Assert.That(viewport.ViewportSelectedIndex, Is.EqualTo(0));
            Assert.That(viewport.StartLinesToClip, Is.EqualTo(0));
        }
        
        [Test]
        public void TestClipStartLines_PageUpPageDown_Complex()
        {
            int viewportRows = 3;
            var viewport = new Viewport(viewportRows);
            var items = new List<TerminalEscapedLine>
            {
                TerminalEscapedLine.SimpleText("line1"),
                TerminalEscapedLineExtensions.MultiLine("SubLine1,", "SubLine2", "SubLine3"),
                TerminalEscapedLine.SimpleText("line1"),
                TerminalEscapedLineExtensions.MultiLine("SubLine1,", "SubLine2", "SubLine3", "SubLine4"),
                TerminalEscapedLine.SimpleText("line1"),
                TerminalEscapedLineExtensions.MultiLine("SubLine1,", "SubLine2"),
                TerminalEscapedLine.SimpleText("line1"),
            };

            viewport.SetItems(items, wrap: true);
            Assert.That(items.Count, Is.EqualTo(7));
            
            Assert.That(viewport.StartRow, Is.EqualTo(0));
            Assert.That(viewport.EndRow, Is.EqualTo(1));
            Assert.That(viewport.SelectedIndex, Is.EqualTo(0));
            Assert.That(viewport.ViewportSelectedIndex, Is.EqualTo(0));
            Assert.That(viewport.StartLinesToClip, Is.EqualTo(0));

            viewport.PageDown();
            
            Assert.That(viewport.StartRow, Is.EqualTo(2));
            Assert.That(viewport.EndRow, Is.EqualTo(3));
            Assert.That(viewport.SelectedIndex, Is.EqualTo(2));
            Assert.That(viewport.ViewportSelectedIndex, Is.EqualTo(0));
            Assert.That(viewport.StartLinesToClip, Is.EqualTo(0));
            
            viewport.PageDown();
            
            Assert.That(viewport.StartRow, Is.EqualTo(4));
            Assert.That(viewport.EndRow, Is.EqualTo(5));
            Assert.That(viewport.SelectedIndex, Is.EqualTo(4));
            Assert.That(viewport.ViewportSelectedIndex, Is.EqualTo(0));
            Assert.That(viewport.StartLinesToClip, Is.EqualTo(0));
            
            viewport.PageDown();
            
            Assert.That(viewport.StartRow, Is.EqualTo(5));
            Assert.That(viewport.EndRow, Is.EqualTo(6));
            Assert.That(viewport.SelectedIndex, Is.EqualTo(5));
            Assert.That(viewport.ViewportSelectedIndex, Is.EqualTo(0));
            Assert.That(viewport.StartLinesToClip, Is.EqualTo(0));
            
            viewport.PageDown();
            
            Assert.That(viewport.StartRow, Is.EqualTo(5));
            Assert.That(viewport.EndRow, Is.EqualTo(6));
            Assert.That(viewport.SelectedIndex, Is.EqualTo(6));
            Assert.That(viewport.ViewportSelectedIndex, Is.EqualTo(2));
            Assert.That(viewport.StartLinesToClip, Is.EqualTo(0));
            
            viewport.PageUp();
            
            Assert.That(viewport.StartRow, Is.EqualTo(3));
            Assert.That(viewport.EndRow, Is.EqualTo(4));
            Assert.That(viewport.SelectedIndex, Is.EqualTo(4));
            Assert.That(viewport.ViewportSelectedIndex, Is.EqualTo(1));
            Assert.That(viewport.StartLinesToClip, Is.EqualTo(2));
            
            viewport.PageUp();
            
            Assert.That(viewport.StartRow, Is.EqualTo(1));
            Assert.That(viewport.EndRow, Is.EqualTo(2));
            Assert.That(viewport.SelectedIndex, Is.EqualTo(2));
            Assert.That(viewport.ViewportSelectedIndex, Is.EqualTo(1));
            Assert.That(viewport.StartLinesToClip, Is.EqualTo(1));
            
            viewport.PageUp();
            
            Assert.That(viewport.StartRow, Is.EqualTo(0));
            Assert.That(viewport.EndRow, Is.EqualTo(1));
            Assert.That(viewport.SelectedIndex, Is.EqualTo(1));
            Assert.That(viewport.ViewportSelectedIndex, Is.EqualTo(1));
            Assert.That(viewport.StartLinesToClip, Is.EqualTo(0));
            
            viewport.PageUp();
            
            Assert.That(viewport.StartRow, Is.EqualTo(0));
            Assert.That(viewport.EndRow, Is.EqualTo(1));
            Assert.That(viewport.SelectedIndex, Is.EqualTo(0));
            Assert.That(viewport.ViewportSelectedIndex, Is.EqualTo(0));
            Assert.That(viewport.StartLinesToClip, Is.EqualTo(0));
        }
    }
}
