using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace nfm.menu;

public class HighlightedText(string text, IList<int> highlightIndexes, object backing) : INotifyPropertyChanged
{
    private IList<int> _highlightIndexes = highlightIndexes;

    public object? BackingObj = backing;
    public string Text { get; set; } = text;

    public IList<int> HighlightIndexes => _highlightIndexes;

    public void Set(string text, IList<int> positions, object backingObj)
    {
        //if (text == Text && Equals(positions, _highlightIndexes)) return;
        Text = text;
        _highlightIndexes = positions;
        BackingObj = backingObj;
        //OnPropertyChanged(null);
        OnPropertyChanged(nameof(Text));
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    public HighlightedText(string text, IList<int> highlightIndexes) : this(text, highlightIndexes, null)
    {
    }
}