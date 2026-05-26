using System.Globalization;

namespace ErgStream.Converters;

/// <summary>
/// Converts a TimeSpan (or nullable TimeSpan) to a M:SS string, e.g. "1:58".
/// </summary>
public class TimeSpanToMSSConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        TimeSpan? ts = value switch
        {
            TimeSpan t => t,
            _ => null
        };

        if (ts == null)
            return "--:--";

        TimeSpan abs = ts.Value < TimeSpan.Zero ? TimeSpan.Zero : ts.Value;
        // We add 1 second to the total seconds to ensure that we round up to the next second if there are any fractional seconds.
        // Otherwise, if we have 0:00.1, it would display as "0:00" instead of "0:01".
        int totalSeconds = (int)abs.TotalSeconds + 1;
        int minutes = totalSeconds / 60;
        int seconds = totalSeconds % 60;
        return $"{minutes}:{seconds:D2}";
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotImplementedException();
}
