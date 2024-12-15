using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace nfm.menu;

public class HighlightedText(string text, IList<int> highlightIndexes, object backing) : INotifyPropertyChanged
{
    private string _text = text;
    private IList<int> _highlightIndexes = highlightIndexes;

    public object? BackingObj = backing;
    public string Text => _text;

    public IList<int> HighlightIndexes => _highlightIndexes;

    public void Set(string text, IList<int> positions, object backingObj)
    {
        if (text == _text && Equals(positions, _highlightIndexes)) return;
        _text = text;
        _highlightIndexes = positions;
        BackingObj = backingObj;
        OnPropertyChanged(null);
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