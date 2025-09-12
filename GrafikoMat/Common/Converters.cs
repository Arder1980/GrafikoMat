using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Media;
using System;
using System.Collections.Generic;
using System.Linq;
using Windows.UI;

namespace GrafikoMat.Common
{
    // ... (pozostałe konwertery bez zmian) ...
    public class BooleanToOpacityConverter : IValueConverter
    {
        public double TrueValue { get; set; } = 1.0;
        public double FalseValue { get; set; } = 0.5;
        public object Convert(object value, Type targetType, object parameter, string language) => (value is bool b && b) ? TrueValue : FalseValue;
        public object ConvertBack(object value, Type targetType, object parameter, string language) => throw new NotImplementedException();
    }
    public class BooleanToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            bool isVisible = value is bool b && b;
            if (parameter is string s && s.Equals("Inverse", StringComparison.OrdinalIgnoreCase)) { isVisible = !isVisible; }
            return isVisible ? Visibility.Visible : Visibility.Collapsed;
        }
        public object ConvertBack(object value, Type targetType, object parameter, string language) => throw new NotImplementedException();
    }
    public class BooleanToBrushConverter_DayOff : IValueConverter
    {
        public Brush DayOffBrush { get; set; } = new SolidColorBrush(Color.FromArgb(0x0A, 0x00, 0x00, 0x00));
        public Brush WorkDayBrush { get; set; } = new SolidColorBrush(Colors.Transparent);
        public object Convert(object value, Type targetType, object parameter, string language) => (value is bool b && b) ? DayOffBrush : WorkDayBrush;
        public object ConvertBack(object value, Type targetType, object parameter, string language) => throw new NotImplementedException();
    }
    public class BooleanToThicknessConverter_LastItemBorder : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string language) => (value is bool isLast && isLast) ? new Thickness(0) : new Thickness(0, 0, 0, 1);
        public object ConvertBack(object value, Type targetType, object parameter, string language) => throw new NotImplementedException();
    }
    public class NullToVisibilityConverter : IValueConverter
    {
        public bool Invert { get; set; } = false;
        public Visibility NullState { get; set; } = Visibility.Collapsed;
        public Visibility NotNullState { get; set; } = Visibility.Visible;
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            bool isNull = value is null;
            if (Invert) isNull = !isNull;
            return isNull ? NullState : NotNullState;
        }
        public object ConvertBack(object value, Type targetType, object parameter, string language) => throw new NotSupportedException();
    }
    public class IntegerToItemsConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            if (value is int count && count > 0)
            {
                return Enumerable.Range(0, count).Select(i => new object()).ToList();
            }
            return Enumerable.Empty<object>();
        }
        public object ConvertBack(object value, Type targetType, object parameter, string language) => throw new NotImplementedException();
    }

    // ZMIANA: Zastosowanie nowej, 5-pikselowej skali rozmiarów
    public class RankToSizeConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            if (value is int rank)
            {
                return rank switch
                {
                    1 => 36.0,
                    2 => 31.0,
                    3 => 26.0,
                    4 => 21.0,
                    _ => 16.0, // Ranga 5 i każda ewentualna kolejna
                };
            }
            return 16.0;
        }
        public object ConvertBack(object value, Type targetType, object parameter, string language) => throw new NotImplementedException();
    }

    public class RankToBrushConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            Color baseColor = Color.FromArgb(255, 255, 193, 7); // Bursztynowy/Amber

            double opacity = 0.4;
            if (value is int rank)
            {
                opacity = rank switch
                {
                    1 => 1.0,
                    2 => 0.85,
                    3 => 0.70,
                    4 => 0.55,
                    _ => 0.40,
                };
            }

            return new SolidColorBrush(Color.FromArgb((byte)(opacity * 255), baseColor.R, baseColor.G, baseColor.B));
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language) => throw new NotImplementedException();
    }
}