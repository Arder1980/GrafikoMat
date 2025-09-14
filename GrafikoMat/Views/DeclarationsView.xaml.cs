using GrafikoMat.Services;
using GrafikoMat.ViewModels;            // DeclarationsViewModel, DayCell
using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;          // TransformToVisual
using System;
using System.Linq;
using Windows.Foundation;               // Rect
using Windows.UI;                        // Color, Colors

namespace GrafikoMat.Views
{
    public sealed partial class DeclarationsView : UserControl
    {
        public event Action? SaveAndCloseRequested;
        public event Action? CloseRequested;

        public void OnDeclCloseOnly() => CloseRequested?.Invoke();
        public void OnDeclSaveAndCloseOnly() => SaveAndCloseRequested?.Invoke();

        public DeclarationsViewModel ViewModel => this.DataContext as DeclarationsViewModel;

        // --- przeciąganie zakresu ---
        private bool _isDragging = false;
        private int _dragStartIndex = -1;

        // tłumik zapętleń SelectionChanged ↔ programowe SelectedItems
        private bool _suppressSelectionChanged = false;

        public DeclarationsView()
        {
            this.InitializeComponent();
            this.ActualThemeChanged += OnThemeChanged;
            this.Unloaded += OnDeclarationsViewUnloaded;

            // Pewne podpięcie wskaźnika (nawet jeśli dzieci oznaczą Handled).
            this.Loaded += (_, __) =>
            {
                if (CalendarGrid is GridView grid)
                {
                    grid.AddHandler(UIElement.PointerPressedEvent, (PointerEventHandler)Calendar_PointerPressed, true);
                    grid.AddHandler(UIElement.PointerMovedEvent, (PointerEventHandler)Calendar_PointerMoved, true);
                    grid.AddHandler(UIElement.PointerReleasedEvent, (PointerEventHandler)Calendar_PointerReleased, true);
                    grid.AddHandler(UIElement.PointerCanceledEvent, (PointerEventHandler)Calendar_PointerCanceled, true);
                    grid.PointerCaptureLost += Grid_PointerCaptureLost;
                }
            };
        }

        public void AttachViewModel(DeclarationsViewModel vm)
        {
            this.DataContext = vm;
            UpdateAllCellBrushes();
        }

        // ==========================
        //  HELPERY SELEKCJI
        // ==========================

        // Solidne wyznaczanie indeksu: po prostokątach kontenerów
        private int GetIndexUnderPointer(GridView grid, Point pt)
        {
            if (ViewModel == null || ViewModel.DayCells.Count == 0) return -1;

            int n = ViewModel.DayCells.Count;
            for (int i = 0; i < n; i++)
            {
                if (grid.ContainerFromIndex(i) is GridViewItem gvi)
                {
                    if (gvi.ActualWidth <= 0 || gvi.ActualHeight <= 0) continue;

                    var t = gvi.TransformToVisual(grid);
                    var bounds = t.TransformBounds(new Rect(0, 0, gvi.ActualWidth, gvi.ActualHeight));
                    if (bounds.Contains(pt))
                    {
                        if (ViewModel.DayCells[i].IsInteractive)
                            return i;
                        else
                            return -1;
                    }
                }
            }
            return -1;
        }

        private int GetIndexUnderPointer(GridView grid, PointerRoutedEventArgs e)
            => GetIndexUnderPointer(grid, e.GetCurrentPoint(grid).Position);

        private int GetIndexUnderPointer(GridView grid, RightTappedRoutedEventArgs e)
            => GetIndexUnderPointer(grid, e.GetPosition(grid));

        private void SafeSyncGridViewSelectionToViewModel(GridView grid)
        {
            if (ViewModel == null) return;

            _suppressSelectionChanged = true;
            try
            {
                grid.SelectedItems.Clear();
                foreach (var item in ViewModel.DayCells)
                    if (ViewModel.SelectedIndices.Contains(item.Index))
                        grid.SelectedItems.Add(item);
            }
            finally { _suppressSelectionChanged = false; }
        }

        private void UpdateVmFromGridSelected(GridView grid)
        {
            if (ViewModel == null) return;

            ViewModel.SelectedIndices.Clear();
            foreach (var item in grid.SelectedItems.OfType<DayCell>())
                ViewModel.SelectedIndices.Add(item.Index);
        }

        // ==========================
        //  KOLORY / BRUSH’E
        // ==========================
        private void UpdateCellBrushes(DayCell cell)
        {
            var currentTheme = ThemeManagerService.Instance.CurrentTheme;

            // BURSZTYNOWY akcent dla zaznaczenia: #FFC107
            var borderSelected = Color.FromArgb(0xFF, 0x33, 0x99, 0xFF); // niebieski jak wcześniej

            if (currentTheme == ElementTheme.Light)
            {
                var transparent = Colors.Transparent;
                var shadeActiveDay = Color.FromArgb(0x0D, 0, 0, 0);
                var shadeDayOff = Color.FromArgb(0x26, 0, 0, 0);
                var headerBgActive = Color.FromArgb(0x59, 0, 0, 0);
                var headerBgOtherMonth = Color.FromArgb(0x0D, 0, 0, 0);
                var borderLight = Color.FromArgb(0x4D, 0, 0, 0);
                var borderOther = Color.FromArgb(0x0D, 0, 0, 0);
                var textNormal = Color.FromArgb(0xBF, 0, 0, 0);
                var textMuted = Color.FromArgb(0x1A, 0, 0, 0);

                Color bgColor = cell.InMonth ? (cell.IsDayOff ? shadeDayOff : shadeActiveDay) : transparent;
                Color headerBgColor = cell.InMonth ? headerBgActive : headerBgOtherMonth;
                Color borderColor = cell.InMonth ? borderLight : borderOther;
                Color numFgColor = cell.InMonth ? textNormal : textMuted;

                (cell.EffectiveBackground as Microsoft.UI.Xaml.Media.SolidColorBrush).Color = bgColor;
                (cell.EffectiveBorderBrush as Microsoft.UI.Xaml.Media.SolidColorBrush).Color = cell.IsSelected ? borderSelected : borderColor;
                (cell.DayNumberForeground as Microsoft.UI.Xaml.Media.SolidColorBrush).Color = numFgColor;
                (cell.EffectiveHeaderBackground as Microsoft.UI.Xaml.Media.SolidColorBrush).Color = headerBgColor;
            }
            else // Dark Theme
            {
                var transparent = Colors.Transparent;
                var shadeActiveDay = Color.FromArgb(0x0D, 255, 255, 255);
                var shadeDayOff = Color.FromArgb(0x26, 255, 255, 255);
                var headerBgActive = Color.FromArgb(0x59, 255, 255, 255);
                var headerBgOtherMonth = Color.FromArgb(0x0D, 255, 255, 255);
                var borderLight = Color.FromArgb(0x4D, 255, 255, 255);
                var borderOther = Color.FromArgb(0x0D, 255, 255, 255);
                var textNormal = Color.FromArgb(0xE6, 255, 255, 255);
                var textMuted = Color.FromArgb(0x1A, 255, 255, 255);

                Color bgColor = cell.InMonth ? (cell.IsDayOff ? shadeDayOff : shadeActiveDay) : transparent;
                Color headerBgColor = cell.InMonth ? headerBgActive : headerBgOtherMonth;
                Color borderColor = cell.InMonth ? borderLight : borderOther;
                Color numFgColor = cell.InMonth ? textNormal : textMuted;

                (cell.EffectiveBackground as Microsoft.UI.Xaml.Media.SolidColorBrush).Color = bgColor;
                (cell.EffectiveBorderBrush as Microsoft.UI.Xaml.Media.SolidColorBrush).Color = cell.IsSelected ? borderSelected : borderColor;
                (cell.DayNumberForeground as Microsoft.UI.Xaml.Media.SolidColorBrush).Color = numFgColor;
                (cell.EffectiveHeaderBackground as Microsoft.UI.Xaml.Media.SolidColorBrush).Color = headerBgColor;
            }

            cell.NotifyBrushUpdate();
        }

        private void OnThemeChanged(FrameworkElement sender, object args) => UpdateAllCellBrushes();

        private void UpdateAllCellBrushes()
        {
            if (ViewModel?.DayCells == null) return;
            foreach (var cell in ViewModel.DayCells)
                UpdateCellBrushes(cell);
        }

        private void OnDeclarationsViewUnloaded(object sender, RoutedEventArgs e)
        {
            this.ActualThemeChanged -= OnThemeChanged;
            this.Unloaded -= OnDeclarationsViewUnloaded;
        }

        // ==========================
        //  SELEKCJA – integracja z GridView
        // ==========================
        private void Calendar_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_suppressSelectionChanged) return;                     // tłumik zapętleń
            if (ViewModel == null || sender is not GridView grid) return;
            if (_isDragging) return;                                   // podczas drag sterujemy sami

            // odznaczone
            foreach (var item in e.RemovedItems)
                if (item is DayCell cell) { cell.SetSelected(false); UpdateCellBrushes(cell); }

            // nowo zaznaczone
            foreach (var item in e.AddedItems)
                if (item is DayCell cell && cell.IsInteractive) { cell.SetSelected(true); UpdateCellBrushes(cell); }

            UpdateVmFromGridSelected(grid);
        }

        // --- DRAG SELECT ---
        private void Calendar_PointerPressed(object sender, PointerRoutedEventArgs e)
        {
            if (ViewModel == null || sender is not GridView grid) return;

            var p = e.GetCurrentPoint(grid);
            if (!p.Properties.IsLeftButtonPressed) return;

            int idx = GetIndexUnderPointer(grid, e);
            if (idx < 0) return;

            _isDragging = true;
            _dragStartIndex = idx;

            ViewModel.SelectSingle(idx);
            SafeSyncGridViewSelectionToViewModel(grid);
            UpdateAllCellBrushes();                 // ← odśwież kolory natychmiast

            grid.CapturePointer(e.Pointer);
            e.Handled = true;
        }

        private void Calendar_PointerMoved(object sender, PointerRoutedEventArgs e)
        {
            if (!_isDragging || ViewModel == null || sender is not GridView grid) return;

            var p = e.GetCurrentPoint(grid);
            if (!p.Properties.IsLeftButtonPressed) { EndDrag(grid, e.Pointer); return; }

            int idx = GetIndexUnderPointer(grid, e);
            if (idx < 0 || _dragStartIndex < 0) return;

            ViewModel.SelectRange(_dragStartIndex, idx);
            SafeSyncGridViewSelectionToViewModel(grid);
            UpdateAllCellBrushes();                 // ← odśwież w trakcie przeciągania
            e.Handled = true;
        }

        private void Calendar_PointerReleased(object sender, PointerRoutedEventArgs e)
        {
            if (sender is not GridView grid) return;
            UpdateVmFromGridSelected(grid);         // finalizacja VM
            EndDrag(grid, e.Pointer);
            e.Handled = true;
        }

        private void Calendar_PointerCanceled(object sender, PointerRoutedEventArgs e)
        {
            if (sender is not GridView grid) return;
            EndDrag(grid, e.Pointer);
            e.Handled = true;
        }

        private void Grid_PointerCaptureLost(object sender, PointerRoutedEventArgs e)
        {
            if (sender is not GridView grid) return;
            EndDrag(grid, e.Pointer);
        }

        private void EndDrag(GridView grid, Pointer pointer)
        {
            if (_isDragging)
            {
                grid.ReleasePointerCaptures();
                _isDragging = false;
                _dragStartIndex = -1;
            }
        }

        private void Calendar_RightTapped(object sender, RightTappedRoutedEventArgs e)
        {
            if (_isDragging) return; // ignoruj „długie przytrzymanie” podczas drag
            if (ViewModel == null || sender is not GridView grid) return;

            int idx = GetIndexUnderPointer(grid, e);
            if (idx < 0) return;

            var tappedCell = ViewModel.DayCells[idx];
            if (!tappedCell.IsInteractive) return;

            if (!ViewModel.SelectedIndices.Contains(idx))
            {
                ViewModel.SelectSingle(idx);
                SafeSyncGridViewSelectionToViewModel(grid);
                UpdateVmFromGridSelected(grid);
                UpdateAllCellBrushes();             // ← odśwież po PPM
            }

            // FlyoutBase.ShowAttachedFlyout(grid); // jeśli masz kontekstówkę
            e.Handled = true;
        }
    }
}
