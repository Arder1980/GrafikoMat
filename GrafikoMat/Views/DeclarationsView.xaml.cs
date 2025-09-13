using GrafikoMat.Services;
using GrafikoMat.ViewModels;
using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using System;
using Windows.UI;

namespace GrafikoMat.Views
{
    public sealed partial class DeclarationsView : UserControl
    {
        public event Action? SaveAndCloseRequested;
        public event Action? CloseRequested;

        public void OnDeclCloseOnly() => CloseRequested?.Invoke();
        public void OnDeclSaveAndCloseOnly() => SaveAndCloseRequested?.Invoke();

        public DeclarationsViewModel ViewModel => this.DataContext as DeclarationsViewModel;
        private bool _isDragging = false;
        private int _dragStartIndex = -1;

        public DeclarationsView()
        {
            this.InitializeComponent();
            this.ActualThemeChanged += OnThemeChanged;
            this.Unloaded += OnDeclarationsViewUnloaded;
        }

        public void AttachViewModel(DeclarationsViewModel vm)
        {
            this.DataContext = vm;
            UpdateAllCellBrushes();
        }

        private void UpdateCellBrushes(DayCell cell)
        {
            var currentTheme = ThemeManagerService.Instance.CurrentTheme;
            var borderSelected = Color.FromArgb(0xFF, 0x33, 0x99, 0xFF);

            if (currentTheme == ElementTheme.Light)
            {
                // === PALETA KOLORÓW DLA MOTYWU JASNEGO (zgodnie z tabelą) ===
                var transparent = Colors.Transparent;
                var shadeActiveDay = Color.FromArgb(0x0D, 0, 0, 0);      // Tło Dnia Roboczego (Aktywny): 5% czerni
                var shadeDayOff = Color.FromArgb(0x26, 0, 0, 0);         // Tło Dnia Wolnego (Aktywny): 15% czerni
                var headerBgActive = Color.FromArgb(0x59, 0, 0, 0);      // Tło Nagłówka (Aktywny): 35% czerni
                var headerBgOtherMonth = Color.FromArgb(0x0D, 0, 0, 0);  // Tło Nagłówka (Nieaktywny): 5% czerni
                var borderLight = Color.FromArgb(0x4D, 0, 0, 0);         // Ramka (Aktywny): 30% czerni
                var borderOther = Color.FromArgb(0x0D, 0, 0, 0);         // Ramka (Nieaktywny): 5% czerni
                var textNormal = Color.FromArgb(0xBF, 0, 0, 0);          // Czcionka (Aktywny): 75% czerni
                var textMuted = Color.FromArgb(0x1A, 0, 0, 0);           // ZMIANA: Czcionka (Nieaktywny): 10% czerni

                Color bgColor, borderColor, numFgColor, headerBgColor;

                if (cell.InMonth)
                    bgColor = cell.IsDayOff ? shadeDayOff : shadeActiveDay;
                else
                    bgColor = transparent;

                headerBgColor = cell.InMonth ? headerBgActive : headerBgOtherMonth;
                borderColor = cell.InMonth ? borderLight : borderOther;
                numFgColor = cell.InMonth ? textNormal : textMuted;

                (cell.EffectiveBackground as SolidColorBrush).Color = bgColor;
                (cell.EffectiveBorderBrush as SolidColorBrush).Color = cell.IsSelected ? borderSelected : borderColor;
                (cell.DayNumberForeground as SolidColorBrush).Color = numFgColor;
                (cell.EffectiveHeaderBackground as SolidColorBrush).Color = headerBgColor;
            }
            else // Dark Theme
            {
                // === PALETA KOLORÓW DLA MOTYWU CIEMNEGO (zgodnie z tabelą) ===
                var transparent = Colors.Transparent;
                var shadeActiveDay = Color.FromArgb(0x0D, 255, 255, 255);   // Tło Dnia Roboczego (Aktywny): 5% bieli
                var shadeDayOff = Color.FromArgb(0x26, 255, 255, 255);      // Tło Dnia Wolnego (Aktywny): 15% bieli
                var headerBgActive = Color.FromArgb(0x59, 255, 255, 255);   // Tło Nagłówka (Aktywny): 35% bieli
                var headerBgOtherMonth = Color.FromArgb(0x0D, 255, 255, 255); // Tło Nagłówka (Nieaktywny): 5% bieli
                var borderLight = Color.FromArgb(0x4D, 255, 255, 255);      // Ramka (Aktywny): 30% bieli
                var borderOther = Color.FromArgb(0x0D, 255, 255, 255);      // Ramka (Nieaktywny): 5% bieli
                var textNormal = Color.FromArgb(0xE6, 255, 255, 255);       // Czcionka (Aktywny): 90% bieli
                var textMuted = Color.FromArgb(0x1A, 255, 255, 255);        // ZMIANA: Czcionka (Nieaktywny): 10% bieli

                Color bgColor, borderColor, numFgColor, headerBgColor;

                if (cell.InMonth)
                    bgColor = cell.IsDayOff ? shadeDayOff : shadeActiveDay;
                else
                    bgColor = transparent;

                headerBgColor = cell.InMonth ? headerBgActive : headerBgOtherMonth;
                borderColor = cell.InMonth ? borderLight : borderOther;
                numFgColor = cell.InMonth ? textNormal : textMuted;

                (cell.EffectiveBackground as SolidColorBrush).Color = bgColor;
                (cell.EffectiveBorderBrush as SolidColorBrush).Color = cell.IsSelected ? borderSelected : borderColor;
                (cell.DayNumberForeground as SolidColorBrush).Color = numFgColor;
                (cell.EffectiveHeaderBackground as SolidColorBrush).Color = headerBgColor;
            }

            cell.NotifyBrushUpdate();
        }
        private void OnThemeChanged(FrameworkElement sender, object args)
        {
            UpdateAllCellBrushes();
        }

        private void UpdateAllCellBrushes()
        {
            if (ViewModel?.DayCells == null) return;
            foreach (var cell in ViewModel.DayCells)
            {
                UpdateCellBrushes(cell);
            }
        }

        private void OnDeclarationsViewUnloaded(object sender, RoutedEventArgs e)
        {
            this.ActualThemeChanged -= OnThemeChanged;
            this.Unloaded -= OnDeclarationsViewUnloaded;
        }

        private void Cell_PointerPressed(object sender, PointerRoutedEventArgs e)
        {
            if (ViewModel != null && e.GetCurrentPoint(this).Properties.IsLeftButtonPressed && sender is FrameworkElement element && element.DataContext is DayCell cell)
            {
                if (!cell.IsInteractive) return;
                _isDragging = true;
                _dragStartIndex = cell.Index;
                element.CapturePointer(e.Pointer);
                ViewModel.SelectSingle(cell.Index);
                UpdateCellBrushes(cell);
                e.Handled = true;
            }
        }

        private void Cell_PointerMoved(object sender, PointerRoutedEventArgs e)
        {
            if (ViewModel != null && _isDragging && sender is FrameworkElement element && element.DataContext is DayCell cell)
            {
                if (!cell.IsInteractive) return;
                ViewModel.SelectRange(_dragStartIndex, cell.Index);
                UpdateAllCellBrushes();
                e.Handled = true;
            }
        }

        private void Cell_PointerReleased(object sender, PointerRoutedEventArgs e)
        {
            if (_isDragging && sender is FrameworkElement element)
            {
                _isDragging = false;
                element.ReleasePointerCapture(e.Pointer);
                e.Handled = true;
            }
        }

        private void OnCellRightTapped(object sender, RightTappedRoutedEventArgs e)
        {
            if (sender is FrameworkElement element && element.DataContext is DayCell cell)
            {
                if (!cell.IsInteractive) return;
                e.Handled = true;
            }
        }
    }
}