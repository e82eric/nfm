using System.Collections.Generic;

namespace nfm.menu;

public class HighlightedText(string text, IList<int> highlightIndexes)
{
    public string Text { get; set; } = text;
    public IList<int> HighlightIndexes { get; set; } = highlightIndexes;
}