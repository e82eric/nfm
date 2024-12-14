using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace nfm.menu;

public class HighlightedText(string text, IList<int> highlightIndexes) : INotifyPropertyChanged
{
    private string _text = text;
    private IList<int> _highlightIndexes = highlightIndexes;

    public string Text => _text;

    public IList<int> HighlightIndexes => _highlightIndexes;

    protected void Set(string text, IList<int> positions)
    {
        if (text == _text && Equals(positions, _highlightIndexes)) return;
        _text = text;
        _highlightIndexes = positions;
        OnPropertyChanged(null);
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
public class HighlightedText<T>(string text, IList<int> highlightIndexes, T backing) : HighlightedText(text, highlightIndexes)
{
    public T Backing { get; set; } = backing;
    public void Set(string text, IList<int> positions, T backing)
    {
        Backing = backing;
        Set(text, positions);
    }
}
