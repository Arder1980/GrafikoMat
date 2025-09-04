using Microsoft.UI;
using Microsoft.UI.Xaml; // ZMIANA: Dodajemy ten using
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Media;
using System;
using Windows.UI;

namespace GrafikoMat.Common
{
    public class BooleanToBrushConverter_DayOff : IValueConverter
    {
        public Brush DayOffBrush { get; set; } = new SolidColorBrush(Color.FromArgb(0x0A, 0x00, 0x00, 0x00));
        public Brush WorkDayBrush { get; set; } = new SolidColorBrush(Colors.Transparent);
        public object Convert(object value, Type targetType, object parameter, string language) => (value is bool b && b) ? DayOffBrush : WorkDayBrush;
        public object ConvertBack(object value, Type targetType, object parameter, string language) => throw new NotImplementedException();
    }

    // ZMIANA: Nowy konwerter do ukrywania dolnej ramki ostatniego elementu
    public class BooleanToThicknessConverter_LastItemBorder : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            // Jeśli 'value' to 'true' (czyli IsLast == true), zwróć grubość 0. W przeciwnym razie 1.
            return (value is bool isLast && isLast)
                ? new Thickness(0, 0, 0, 0)
                : new Thickness(0, 0, 0, 1);
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            throw new NotImplementedException();
        }
    }
}