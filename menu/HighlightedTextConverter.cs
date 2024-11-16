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

    public object Convert(IList<object?> values, Type targetType, object? parameter, CultureInfo culture)
    {
        if (values?.Count != 2 || values[0] is not string text || values[1] is not IList<int> highlights)
        {
            return new InlineCollection { new Run { Text = values?[0]?.ToString() ?? string.Empty } };
        }

        var inlines = new InlineCollection();
        var currentIndex = 0;

        var sortedHighlights = highlights.OrderBy(i => i).Distinct().ToList();

        foreach (var highlightIndex in sortedHighlights)
        {
            if (highlightIndex > currentIndex)
            {
                inlines.Add(new Run
                {
                    Text = text[currentIndex..highlightIndex],
                    Foreground = Brushes.Gray
                });
            }

            if (highlightIndex < text.Length)
            {
                inlines.Add(new Run
                {
                    Text = text[highlightIndex].ToString(),
                    Foreground = Brushes.Orange
                });
            }

            currentIndex = highlightIndex + 1;
        }

        if (currentIndex < text.Length)
        {
            inlines.Add(new Run
            {
                Text = text[currentIndex..],
                Foreground = Brushes.Gray
            });
        }

        return inlines;
    }
}