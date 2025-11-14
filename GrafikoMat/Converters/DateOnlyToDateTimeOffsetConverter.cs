using Microsoft.UI.Xaml.Data;
using System;

namespace GrafikoMat.Common
{
    /// <summary>
    /// Konwerter między DateOnly (C# 10+) a DateTimeOffset (WinUI CalendarDatePicker)
    /// </summary>
    public class DateOnlyToDateTimeOffsetConverter : IValueConverter
    {
        public object? Convert(object value, Type targetType, object parameter, string language)
        {
            if (value is DateOnly dateOnly)
            {
                // DateOnly -> DateTimeOffset
                var dateTime = dateOnly.ToDateTime(TimeOnly.MinValue, DateTimeKind.Unspecified);
                return new DateTimeOffset(dateTime);
            }

            return null;
        }

        public object? ConvertBack(object value, Type targetType, object parameter, string language)
        {
            if (value is DateTimeOffset dateTimeOffset)
            {
                // DateTimeOffset -> DateOnly
                return DateOnly.FromDateTime(dateTimeOffset.DateTime);
            }

            return null;
        }
    }
}
