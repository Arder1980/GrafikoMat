using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Data;
using System;

namespace GrafikoMat.Converters
{
    /// <summary>
    /// Zwraca Visible, gdy wartość NIE jest null (domyślnie),
    /// oraz Collapsed, gdy jest null. Można odwrócić logikę przez Invert=true.
    /// </summary>
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

        public object ConvertBack(object value, Type targetType, object parameter, string language)
            => throw new NotSupportedException();
    }
}
