using System;
using System.Globalization;
using Avalonia.Data.Converters;

namespace Sedgewright.Converters;

public sealed class DoubleToQuarterHeightConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is double height ? Math.Max(0d, height * 0.25d) : 0d;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
