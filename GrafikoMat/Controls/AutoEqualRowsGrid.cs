using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Windows.Foundation;

namespace GrafikoMat.Controls
{
    /// <summary>
    /// Specjalny panel Grid, który automatycznie dzieli swoją przestrzeń
    /// na tyle równych wierszy (*), ile zawiera elementów.
    /// </summary>
    public class AutoEqualRowsGrid : Grid
    {
        protected override Size MeasureOverride(Size availableSize)
        {
            // ZMIANA: Przeniesiono całą logikę tworzenia wierszy do fazy Measure.
            // To jest prawidłowe i bezpieczne miejsce na modyfikację struktury siatki.
            if (this.RowDefinitions.Count != this.Children.Count)
            {
                this.RowDefinitions.Clear();
                for (int i = 0; i < this.Children.Count; i++)
                {
                    this.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
                    if (this.Children[i] is FrameworkElement fe)
                    {
                        SetRow(fe, i);
                    }
                }
            }

            // Po zdefiniowaniu struktury, wywołujemy bazową metodę MeasureOverride, 
            // aby standardowy Grid mógł poprawnie zmierzyć siebie i swoje dzieci.
            return base.MeasureOverride(availableSize);
        }

        protected override Size ArrangeOverride(Size finalSize)
        {
            // Metoda ArrangeOverride teraz tylko przekazuje wywołanie do bazy,
            // ponieważ struktura wierszy została już w pełni przygotowana w fazie Measure.
            return base.ArrangeOverride(finalSize);
        }
    }
}