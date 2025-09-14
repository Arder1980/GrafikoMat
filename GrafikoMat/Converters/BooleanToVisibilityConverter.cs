using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Data;
using System;

namespace GrafikoMat.Converters
{
    public class BooleanToOpacityConverter : IValueConverter
    {
        // ZMIANA: Zamieniono wartości, aby poprawnie obsługiwać "wyszarzanie"
        public double TrueValue { get; set; } = 1.0; // Wartość krycia, gdy warunek jest PRAWDZIWY (np. IsArchived = true)
        public double FalseValue { get; set; } = 0.5; // Wartość krycia, gdy warunek jest FAŁSZYWY (np. IsArchived = false)

        public object Convert(object value, Type targetType, object parameter, string language) => (value is bool b && b) ? TrueValue : FalseValue;

        public object ConvertBack(object value, Type targetType, object parameter, string language) => throw new NotImplementedException();
    }
}
