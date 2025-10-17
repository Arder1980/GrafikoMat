using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Data;
using System;

namespace GrafikoMat.Converters
{
    public class BooleanToPrimaryStyleConverter : IValueConverter
    {
        public Style? PrimaryStyle { get; set; }
        public Style? NormalStyle { get; set; }

        public object? Convert(object value, Type targetType, object parameter, string language)
        {
            if (value is bool isPrimary)
            {
                return isPrimary ? PrimaryStyle : NormalStyle;
            }
            return NormalStyle;
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            throw new NotImplementedException();
        }
    }
}