using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Avalonia.Controls.Documents;
using Avalonia.Data.Converters;
using Avalonia.Media;

namespace nfm.menu;

public class HighlightedTextConverter : IMultiValueConverter
{
    public static readonly HighlightedTextConverter Instance = new();

    public IBrush NormalTextBrush { get; set; } = new SolidColorBrush(Color.Parse("#ebdbb2"));
    public IBrush HighlightBrush { get; set; } = new SolidColorBrush(Color.Parse("#fe8019"));
    public IBrush SelectedItemTextBrush { get; set; } = new SolidColorBrush(Color.Parse("#ebdbb2"));

    public object Convert(IList<object?> values, Type targetType, object? parameter, CultureInfo culture)
    {
        if (values?.Count != 4 || values[0] is not string _text || values[1] is not IList<int> _highlights || values[2] is not bool isSelected || values[3] is not HighlightedText viewModel)
        {
            return new InlineCollection { new Run { Text = values?[0]?.ToString() ?? string.Empty } };
        }

        var inlines = new InlineCollection();
        var currentIndex = 0;

        var sortedHighlights = viewModel.HighlightIndexes.OrderBy(i => i).Distinct().ToList();

        var normalBrush = isSelected ? SelectedItemTextBrush : NormalTextBrush;

        foreach (var highlightIndex in sortedHighlights)
        {
            if (highlightIndex > currentIndex)
            {
                inlines.Add(new Run
                {
                    Text = viewModel.Text[currentIndex..highlightIndex],
                    Foreground = normalBrush
                });
            }

            if (highlightIndex < viewModel.Text.Length)
            {
                inlines.Add(new Run
                {
                    Text = viewModel.Text[highlightIndex].ToString(),
                    Foreground = HighlightBrush
                });
            }

            currentIndex = highlightIndex + 1;
        }

        if (currentIndex < viewModel.Text.Length)
        {
            inlines.Add(new Run
            {
                Text = viewModel.Text[currentIndex..],
                Foreground = normalBrush
            });
        }

        return inlines;
    }
}
