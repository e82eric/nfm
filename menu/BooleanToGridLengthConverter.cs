using System;
using System.Globalization;
using Avalonia.Controls;
using Avalonia.Data.Converters;

namespace nfm.menu;

public class BooleanToGridLengthConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return value is bool hasPreview && hasPreview ? GridLength.Star : new GridLength(0);
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}