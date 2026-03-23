using System;
using System.Globalization;
using Avalonia.Data.Converters;
using Avalonia.Media;

namespace BitFab.KW1281Test.Ui.Converters;

public class HexValueConverter : IValueConverter
{
    public static readonly HexValueConverter Instance = new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is byte b)
            return $"0x{b:X2}";
        if (value is ushort u)
            return $"0x{u:X4}";
        if (value is uint ui)
            return $"0x{ui:X4}";
        if (value is int i)
            return $"0x{i:X}";
        return value?.ToString();
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is string s && s.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
        {
            s = s[2..];
            if (targetType == typeof(byte) && byte.TryParse(s, NumberStyles.HexNumber, culture, out var b))
                return b;
            if (targetType == typeof(ushort) && ushort.TryParse(s, NumberStyles.HexNumber, culture, out var u))
                return u;
            if (targetType == typeof(uint) && uint.TryParse(s, NumberStyles.HexNumber, culture, out var ui))
                return ui;
            if (targetType == typeof(int) && int.TryParse(s, NumberStyles.HexNumber, culture, out var i))
                return i;
        }
        return value;
    }
}

public class LogLevelToBrushConverter : IValueConverter
{
    public static readonly LogLevelToBrushConverter Instance = new();

    private static readonly IBrush TxBrush = new SolidColorBrush(Color.FromRgb(0x33, 0x99, 0xFF));   // blue
    private static readonly IBrush RxBrush = new SolidColorBrush(Color.FromRgb(0x33, 0xCC, 0x66));   // green
    private static readonly IBrush ErrorBrush = new SolidColorBrush(Color.FromRgb(0xFF, 0x44, 0x44)); // red
    private static readonly IBrush DefaultBrush = Brushes.White;

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is string msg)
        {
            if (msg.StartsWith("TX", StringComparison.OrdinalIgnoreCase) ||
                msg.StartsWith("> TX", StringComparison.OrdinalIgnoreCase) ||
                msg.StartsWith("Sending", StringComparison.OrdinalIgnoreCase))
                return TxBrush;
            if (msg.StartsWith("RX", StringComparison.OrdinalIgnoreCase) ||
                msg.StartsWith("< RX", StringComparison.OrdinalIgnoreCase) ||
                msg.StartsWith("Received", StringComparison.OrdinalIgnoreCase))
                return RxBrush;
            if (msg.StartsWith("Error", StringComparison.OrdinalIgnoreCase) ||
                msg.StartsWith("Failed", StringComparison.OrdinalIgnoreCase) ||
                msg.Contains("failed", StringComparison.OrdinalIgnoreCase) ||
                msg.Contains("error", StringComparison.OrdinalIgnoreCase))
                return ErrorBrush;
        }
        return DefaultBrush;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

public class BoolToColorConverter : IValueConverter
{
    public static readonly BoolToColorConverter Instance = new();

    public IBrush TrueBrush { get; set; } = new SolidColorBrush(Color.FromRgb(0x33, 0xCC, 0x66));
    public IBrush FalseBrush { get; set; } = new SolidColorBrush(Color.FromRgb(0xFF, 0x44, 0x44));

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return value is true ? TrueBrush : FalseBrush;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
