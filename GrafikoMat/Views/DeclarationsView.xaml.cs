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
                // === PALETA KOLORÓW WYŁĄCZNIE DLA MOTYWU JASNEGO (BEZ ZMIAN) ===
                var transparent = Colors.Transparent;
                var shadeDayOff = Color.FromArgb(0x1A, 0, 0, 0);
                var shadeOtherMonth = Color.FromArgb(0x0D, 0, 0, 0);
                var headerBgStandard = Color.FromArgb(0x59, 0, 0, 0);
                var headerBgDayOff = Color.FromArgb(0x47, 0, 0, 0);
                var headerBgOtherMonth = Color.FromArgb(0x1A, 0, 0, 0);
                var borderLight = Color.FromArgb(0x33, 0, 0, 0);
                var borderOther = Color.FromArgb(0x4D, 0, 0, 0);
                var textNormal = Color.FromArgb(0xE6, 0, 0, 0);
                var textMutedDayOff = Color.FromArgb(0x4D, 0, 0, 0);
                var textMuted = Color.FromArgb(0x66, 0, 0, 0);

                Color bgColor, borderColor, numFgColor, headerBgColor;

                if (cell.InMonth == false) bgColor = shadeOtherMonth;
                else if (cell.IsDayOff) bgColor = shadeDayOff;
                else bgColor = transparent;

                if (cell.InMonth == false) headerBgColor = headerBgOtherMonth;
                else if (cell.IsDayOff) headerBgColor = headerBgDayOff;
                else headerBgColor = headerBgStandard;

                borderColor = cell.InMonth == false ? borderOther : borderLight;

                if (cell.InMonth == false)
                    numFgColor = cell.IsDayOff ? textMutedDayOff : textMuted;
                else
                    numFgColor = textNormal;

                (cell.EffectiveBackground as SolidColorBrush).Color = bgColor;
                (cell.EffectiveBorderBrush as SolidColorBrush).Color = cell.IsSelected ? borderSelected : borderColor;
                (cell.DayNumberForeground as SolidColorBrush).Color = numFgColor;
                (cell.EffectiveHeaderBackground as SolidColorBrush).Color = headerBgColor;
            }
            else // Dark Theme
            {
                // === PALETA KOLORÓW DLA MOTYWU CIEMNEGO (Z OSTATECZNYMI POPRAWKAMI) ===
                var transparent = Colors.Transparent;
                var shadeDayOff = Color.FromArgb(0x28, 255, 255, 255);
                var shadeActiveDay = Color.FromArgb(0x0D, 255, 255, 255);
                var headerBgStandard = Color.FromArgb(0x4D, 255, 255, 255);
                var headerBgDayOff = Color.FromArgb(0x59, 255, 255, 255);
                var textNormal = Color.FromArgb(0xF2, 255, 255, 255);

                // ZMIANA: Zunifikowane, przyciemnione kolory dla WSZYSTKICH nieaktywnych dni
                var headerBgOtherMonth = Color.FromArgb(0x0D, 255, 255, 255); // Bardzo subtelne tło nagłówka (5% bieli)
                var borderOtherMonth = headerBgOtherMonth;                   // Taka sama ramka
                var textOtherMonth = Color.FromArgb(0x33, 255, 255, 255);     // Mocno przyciemniona czcionka (20% bieli)

                Color bgColor, borderColor, numFgColor, headerBgColor;

                if (cell.InMonth)
                {
                    bgColor = cell.IsDayOff ? shadeDayOff : shadeActiveDay;
                }
                else
                {
                    bgColor = transparent;
                }

                if (cell.InMonth)
                {
                    headerBgColor = cell.IsDayOff ? headerBgDayOff : headerBgStandard;
                    borderColor = headerBgStandard;
                    numFgColor = textNormal;
                }
                else // Dni z innego miesiąca
                {
                    // ZMIANA: Uproszczona logika - wszystkie nieaktywne dni wyglądają tak samo
                    headerBgColor = headerBgOtherMonth;
                    borderColor = borderOtherMonth;
                    numFgColor = textOtherMonth;
                }

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