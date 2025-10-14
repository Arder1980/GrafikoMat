using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Media;
using GrafikoMat.ViewModels;

namespace GrafikoMat.Common
{
    internal static class CalendarBrushKeys
    {
        public const string CellBgActive = "Cal_CellBgActive";
        public const string CellBgDayOff = "Cal_CellBgDayOff";
        public const string CellBgOther = "Cal_CellBgOther";
        public const string HeaderBgActive = "Cal_HeaderBgActive";
        public const string HeaderBgOther = "Cal_HeaderBgOther";
        public const string BorderActive = "Cal_BorderActive";
        public const string BorderOther = "Cal_BorderOther";
        public const string TextNormal = "Cal_TextNormal";
        public const string TextMuted = "Cal_TextMuted";
    }

    public sealed class CellBackgroundConverter : IValueConverter
    {
        public object Convert(object value, System.Type targetType, object parameter, string language)
        {
            if (value is DayCell c)
            {
                var key = c.InMonth
                    ? (c.IsDayOff ? CalendarBrushKeys.CellBgDayOff : CalendarBrushKeys.CellBgActive)
                    : CalendarBrushKeys.CellBgOther;
                return (Brush)Application.Current.Resources[key];
            }
            return null!;
        }

        public object ConvertBack(object value, System.Type targetType, object parameter, string language) => null!;
    }

    public sealed class CellHeaderBackgroundConverter : IValueConverter
    {
        public object Convert(object value, System.Type targetType, object parameter, string language)
        {
            if (value is DayCell c)
            {
                var key = c.InMonth ? CalendarBrushKeys.HeaderBgActive : CalendarBrushKeys.HeaderBgOther;
                return (Brush)Application.Current.Resources[key];
            }
            return null!;
        }

        public object ConvertBack(object value, System.Type targetType, object parameter, string language) => null!;
    }

    public sealed class CellBorderConverter : IValueConverter
    {
        public object Convert(object value, System.Type targetType, object parameter, string language)
        {
            if (value is DayCell c)
            {
                var key = c.InMonth ? CalendarBrushKeys.BorderActive : CalendarBrushKeys.BorderOther;
                return (Brush)Application.Current.Resources[key];
            }
            return null!;
        }

        public object ConvertBack(object value, System.Type targetType, object parameter, string language) => null!;
    }

    public sealed class DayNumberForegroundConverter : IValueConverter
    {
        public object Convert(object value, System.Type targetType, object parameter, string language)
        {
            if (value is DayCell c)
            {
                var key = c.InMonth ? CalendarBrushKeys.TextNormal : CalendarBrushKeys.TextMuted;
                return (Brush)Application.Current.Resources[key];
            }
            return null!;
        }

        public object ConvertBack(object value, System.Type targetType, object parameter, string language) => null!;
    }
}
