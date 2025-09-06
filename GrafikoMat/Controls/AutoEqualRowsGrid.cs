using Microsoft.UI.Xaml.Controls;
using Windows.Foundation;

namespace GrafikoMat.Controls
{
    /// <summary>
    /// Panel, który układa elementy w jednej kolumnie,
    /// dając każdemu z nich równą, proporcjonalną wysokość.
    /// To jest poprawna implementacja niestandardowego panelu layoutu.
    /// </summary>
    public class AutoEqualRowsGrid : Panel
    {
        /// <summary>
        /// Faza pomiaru. W tym przypadku nie musimy mierzyć każdego dziecka z osobna,
        /// ponieważ i tak rozciągniemy je na całą dostępną szerokość.
        /// Zgłaszamy chęć zajęcia całej dostępnej przestrzeni.
        /// </summary>
        protected override Size MeasureOverride(Size availableSize)
        {
            return availableSize;
        }

        /// <summary>
        /// Faza aranżacji (układania). To tutaj dzieje się cała logika.
        /// Panel otrzymuje ostateczny rozmiar i musi rozłożyć w nim swoje dzieci.
        /// </summary>
        protected override Size ArrangeOverride(Size finalSize)
        {
            int count = Children.Count;
            if (count == 0)
            {
                return finalSize; // Jeśli nie ma dzieci, kończymy pracę.
            }

            // Obliczamy wysokość pojedynczego wiersza.
            double rowHeight = finalSize.Height / count;
            // Szerokość jest stała - równa szerokości całego panelu.
            double rowWidth = finalSize.Width;

            double currentY = 0;

            // Przechodzimy pętlą po wszystkich elementach (dzieciach) w panelu.
            foreach (var child in Children)
            {
                // Definiujemy prostokąt (pozycję i rozmiar) dla bieżącego dziecka.
                // X = 0 (zaczynamy od lewej krawędzi)
                // Y = currentY (aktualna pozycja pionowa)
                // Width = rowWidth (pełna szerokość panelu)
                // Height = rowHeight (obliczona, równa wysokość dla każdego)
                var rect = new Rect(0, currentY, rowWidth, rowHeight);

                // Mówimy dziecku, aby ułożyło się w tym prostokącie.
                child.Arrange(rect);

                // Przesuwamy pozycję startową dla następnego elementu w dół.
                currentY += rowHeight;
            }

            return finalSize; // Zwracamy ostateczny rozmiar, który zajęliśmy.
        }
    }
}