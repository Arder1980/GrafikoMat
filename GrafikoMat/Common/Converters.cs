using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Media;
using System;
using System.Linq;
using Windows.UI;

namespace GrafikoMat.Common
{
    public class BooleanToOpacityConverter : IValueConverter
    {
        public double TrueValue { get; set; } = 1.0;
        public double FalseValue { get; set; } = 0.5;

        public object Convert(object value, Type targetType, object parameter, string language)
        {
            bool boolValue = value is bool b && b;

            // Obsługa parametru w formacie "FalseValue;TrueValue"
            if (parameter is string paramStr && !string.IsNullOrEmpty(paramStr))
            {
                var parts = paramStr.Split(';');
                // ✅ DODANE - użyj InvariantCulture dla kropki dziesiętnej
                if (parts.Length == 2 &&
                    double.TryParse(parts[0], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out double falseVal) &&
                    double.TryParse(parts[1], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out double trueVal))
                {
                    return boolValue ? trueVal : falseVal;
                }
                // Jeśli tylko jedna wartość, użyj jej jako FalseValue
                if (parts.Length == 1 && double.TryParse(parts[0], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out double singleVal))
                {
                    return boolValue ? 1.0 : singleVal;
                }
            }

            return boolValue ? TrueValue : FalseValue;
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language)
            => throw new NotImplementedException();
    }
    public class BooleanToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            bool isVisible = value is bool b && b;
            if (parameter is string s && s.Equals("Inverse", StringComparison.OrdinalIgnoreCase))
            {
                isVisible = !isVisible;
            }
            return isVisible ? Visibility.Visible : Visibility.Collapsed;
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            bool isVisible = value is Visibility v && v == Visibility.Visible;
            if (parameter is string s && s.Equals("Inverse", StringComparison.OrdinalIgnoreCase))
            {
                isVisible = !isVisible;
            }
            return isVisible;
        }
    }

    // ================== USUNIĘTA KLASA ==================
    // public class BooleanToBrushConverter_DayOff : IValueConverter
    // { ... }
    // ====================================================

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

    public class StringNotEmptyToBoolConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            return value is string str && !string.IsNullOrEmpty(str);
        }
        public object ConvertBack(object value, Type targetType, object parameter, string language) => throw new NotImplementedException();
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
                    _ => 16.0,
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
            Color baseColor = Color.FromArgb(255, 255, 193, 7);
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

    public class BooleanToBrushConverter : IValueConverter
    {
        public Brush TrueBrush { get; set; } = null!;
        public Brush FalseBrush { get; set; } = null!;

        public object Convert(object value, Type targetType, object parameter, string language)
        {
            return (value is bool b && b) ? TrueBrush : FalseBrush;
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language) => throw new NotImplementedException();
    }

    public class EnumToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            if (value == null || parameter == null)
                return Visibility.Collapsed;

            string? enumValue = value.ToString();
            string? targetValue = parameter.ToString();

            return enumValue == targetValue ? Visibility.Visible : Visibility.Collapsed;
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            throw new NotImplementedException();
        }
    }

    public class EnumToBoolConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            if (value == null || parameter == null)
                return false;

            string? enumValue = value.ToString();
            string? targetValue = parameter.ToString();

            return enumValue == targetValue;
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            if (value is bool isChecked && isChecked && parameter is string enumString)
            {
                try
                {
                    // Dla x:Bind, targetType może być Object zamiast właściwego typu enum
                    // Musimy spróbować znaleźć właściwy typ

                    // Próbujemy najpierw użyć przekazanego targetType
                    if (targetType != null && targetType != typeof(object) && targetType.IsEnum)
                    {
                        return Enum.Parse(targetType, enumString);
                    }

                    // Jeśli targetType to object lub nie jest enumem, próbujemy znaleźć typ na podstawie nazwy
                    // Sprawdzamy popularne typy enum w aplikacji
                    var enumTypes = new[]
                    {
                        typeof(GrafikoMat.Services.AppTheme),
                        typeof(GrafikoMat.Core.Scheduling.Models.SolverType)
                    };

                    foreach (var enumType in enumTypes)
                    {
                        if (Enum.IsDefined(enumType, enumString))
                        {
                            return Enum.Parse(enumType, enumString);
                        }
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"EnumToBoolConverter.ConvertBack error: {ex.Message}");
                }
            }
            return DependencyProperty.UnsetValue;
        }
    }

    /// <summary>
    /// Konwertuje szerokość na MaxWidth z zadanym procentem.
    /// Parametr: procent jako string np. "0.9" dla 90%
    /// Domyślnie: 90%
    /// </summary>
    public class WidthToMaxWidthConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            double percentage = 0.9; // Domyślnie 90%

            // Parse parametru
            if (parameter is string paramStr && !string.IsNullOrEmpty(paramStr))
            {
                if (double.TryParse(paramStr, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out double parsedPercent))
                {
                    percentage = parsedPercent;
                }
            }

            // Jeśli nie ma wartości szerokości, zwróć bardzo dużą wartość (brak ograniczenia)
            if (value is not double width || width <= 0)
            {
                return double.PositiveInfinity;
            }

            return width * percentage;
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language)
            => throw new NotImplementedException();
    }

    /// <summary>
    /// Konwertuje wysokość slotu na rozmiar czcionki z osłabionym skalowaniem.
    /// Parametr: "BaseFontSize;ScaleFactor;Divisor" np. "18;0.1;1" lub "14;0.08;2"
    /// Divisor: dzielnik wysokości (dla slotów 12h użyj 2, dla 24h użyj 1)
    /// Formuła: FontSize = BaseFontSize + ((SlotHeight / Divisor) - 48) * ScaleFactor
    /// </summary>
    public class SlotHeightToFontSizeConverter : IValueConverter
    {
        private const double BaseSlotHeight = 48.0;

        public object Convert(object value, Type targetType, object parameter, string language)
        {
            // Domyślne wartości
            double baseFontSize = 16.0;
            double scaleFactor = 0.1;
            double divisor = 1.0;

            // Parse parametru "BaseFontSize;ScaleFactor;Divisor"
            if (parameter is string paramStr && !string.IsNullOrEmpty(paramStr))
            {
                var parts = paramStr.Split(';');
                if (parts.Length >= 1 && double.TryParse(parts[0], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out double parsedBase))
                {
                    baseFontSize = parsedBase;
                }
                if (parts.Length >= 2 && double.TryParse(parts[1], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out double parsedScale))
                {
                    scaleFactor = parsedScale;
                }
                if (parts.Length >= 3 && double.TryParse(parts[2], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out double parsedDivisor) && parsedDivisor > 0)
                {
                    divisor = parsedDivisor;
                }
            }

            // Jeśli nie ma wartości wysokości, zwróć bazowy rozmiar
            if (value is not double height || height <= 0)
            {
                return baseFontSize;
            }

            // Oblicz efektywną wysokość (podzieloną przez divisor dla slotów 12h)
            double effectiveHeight = height / divisor;

            // Oblicz rozmiar czcionki: BaseFontSize + (EffectiveHeight - BaseHeight) * ScaleFactor
            double fontSize = baseFontSize + Math.Max(0, effectiveHeight - BaseSlotHeight) * scaleFactor;

            // Minimum to baseFontSize
            return Math.Max(baseFontSize, fontSize);
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language)
            => throw new NotImplementedException();
    }
}