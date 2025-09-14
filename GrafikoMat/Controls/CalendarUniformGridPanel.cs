using System;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Windows.Foundation;

namespace GrafikoMat.Controls
{
    public sealed class CalendarUniformGridPanel : Panel
    {
        public int Columns
        {
            get => (int)GetValue(ColumnsProperty);
            set => SetValue(ColumnsProperty, value);
        }
        public static readonly DependencyProperty ColumnsProperty =
            DependencyProperty.Register(nameof(Columns), typeof(int), typeof(CalendarUniformGridPanel),
                new PropertyMetadata(7, OnLayoutPropertyChanged));

        public double ColumnSpacing
        {
            get => (double)GetValue(ColumnSpacingProperty);
            set => SetValue(ColumnSpacingProperty, value);
        }
        public static readonly DependencyProperty ColumnSpacingProperty =
            DependencyProperty.Register(nameof(ColumnSpacing), typeof(double), typeof(CalendarUniformGridPanel),
                new PropertyMetadata(8.0, OnLayoutPropertyChanged));

        public double RowSpacing
        {
            get => (double)GetValue(RowSpacingProperty);
            set => SetValue(RowSpacingProperty, value);
        }
        public static readonly DependencyProperty RowSpacingProperty =
            DependencyProperty.Register(nameof(RowSpacing), typeof(double), typeof(CalendarUniformGridPanel),
                new PropertyMetadata(8.0, OnLayoutPropertyChanged));

        private static void OnLayoutPropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is CalendarUniformGridPanel p) { p.InvalidateMeasure(); p.InvalidateArrange(); }
        }

        protected override Size MeasureOverride(Size availableSize)
        {
            // W fazie pomiaru po prostu zgłaszamy chęć zajęcia całej dostępnej przestrzeni.
            // Nie mierzymy tutaj dzieci, aby uniknąć "zamrożenia" rozmiaru.
            // To pozwoli rodzicowi w pełni rozciągnąć nasz panel w fazie Arrange.
            double width = double.IsInfinity(availableSize.Width) ? 800 : availableSize.Width;
            double height = double.IsInfinity(availableSize.Height) ? 600 : availableSize.Height;
            return new Size(width, height);
        }

        protected override Size ArrangeOverride(Size finalSize)
        {
            int cols = Math.Max(1, Columns);
            int count = Children.Count;
            if (count == 0) return finalSize;

            int rows = Math.Max(1, (int)Math.Ceiling((double)count / cols));
            double cellW = Math.Max(0, (finalSize.Width - (cols - 1) * ColumnSpacing) / cols);
            double cellH = Math.Max(0, (finalSize.Height - (rows - 1) * RowSpacing) / rows);

            // Unikamy ujemnych wymiarów, jeśli spacing jest większy niż dostępny rozmiar
            if (cellW < 0) cellW = 0;
            if (cellH < 0) cellH = 0;

            for (int i = 0; i < count; i++)
            {
                int row = i / cols;
                int col = i % cols;

                double x = col * (cellW + ColumnSpacing);
                double y = row * (cellH + RowSpacing);

                var rect = new Rect(x, y, cellW, cellH);

                // W fazie Arrange wywołujemy TYLKO Arrange.
                // Dziecko samo dostosuje swój wewnętrzny rozmiar do przekazanego prostokąta.
                Children[i].Arrange(rect);
            }
            return finalSize;
        }
    }
}