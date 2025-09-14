using GrafikoMat.Services;
using GrafikoMat.ViewModels;
using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using System;
using System.Linq;
using Windows.Foundation;
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

            // Rejestrujemy obsługę zdarzeń myszy dla całej kontrolki GridView
            // Użycie AddHandler z 'handledEventsToo = true' gwarantuje, że zdarzenia do nas dotrą
            this.Loaded += (_, __) =>
            {
                CalendarGridView.AddHandler(PointerPressedEvent, new PointerEventHandler(Calendar_PointerPressed), true);
                CalendarGridView.AddHandler(PointerMovedEvent, new PointerEventHandler(Calendar_PointerMoved), true);
                CalendarGridView.AddHandler(PointerReleasedEvent, new PointerEventHandler(Calendar_PointerReleased), true);
                CalendarGridView.AddHandler(PointerExitedEvent, new PointerEventHandler(Calendar_PointerExited), true);
                CalendarGridView.AddHandler(RightTappedEvent, new RightTappedEventHandler(Calendar_RightTapped), true);
            };
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
                var transparent = Colors.Transparent;
                var shadeActiveDay = Color.FromArgb(0x0D, 0, 0, 0);
                var shadeDayOff = Color.FromArgb(0x26, 0, 0, 0);
                var headerBgActive = Color.FromArgb(0x59, 0, 0, 0);
                var headerBgOtherMonth = Color.FromArgb(0x0D, 0, 0, 0);
                var borderLight = Color.FromArgb(0x4D, 0, 0, 0);
                var borderOther = Color.FromArgb(0x0D, 0, 0, 0);
                var textNormal = Color.FromArgb(0xBF, 0, 0, 0);
                var textMuted = Color.FromArgb(0x1A, 0, 0, 0);

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
                var transparent = Colors.Transparent;
                var shadeActiveDay = Color.FromArgb(0x0D, 255, 255, 255);
                var shadeDayOff = Color.FromArgb(0x26, 255, 255, 255);
                var headerBgActive = Color.FromArgb(0x59, 255, 255, 255);
                var headerBgOtherMonth = Color.FromArgb(0x0D, 255, 255, 255);
                var borderLight = Color.FromArgb(0x4D, 255, 255, 255);
                var borderOther = Color.FromArgb(0x0D, 255, 255, 255);
                var textNormal = Color.FromArgb(0xE6, 255, 255, 255);
                var textMuted = Color.FromArgb(0x1A, 255, 255, 255);

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

        private int GetIndexFromPoint(Point p)
        {
            if (ViewModel == null || ViewModel.DayCells.Count == 0 || p.X < 0 || p.Y < 0) return -1;

            var grid = CalendarGridView;
            for (int i = 0; i < ViewModel.DayCells.Count; i++)
            {
                if (grid.ContainerFromIndex(i) is FrameworkElement container)
                {
                    var transform = container.TransformToVisual(grid);
                    var bounds = transform.TransformBounds(new Rect(0, 0, container.ActualWidth, container.ActualHeight));
                    if (bounds.Contains(p))
                    {
                        return i;
                    }
                }
            }
            return -1;
        }

        private void Calendar_PointerPressed(object sender, PointerRoutedEventArgs e)
        {
            if (ViewModel == null) return;
            var point = e.GetCurrentPoint(CalendarGridView).Position;
            int index = GetIndexFromPoint(point);

            if (index != -1 && ViewModel.DayCells[index].IsInteractive)
            {
                _isDragging = true;
                _dragStartIndex = index;
                CalendarGridView.CapturePointer(e.Pointer);

                ViewModel.SelectSingle(index);
                UpdateAllCellBrushes();
                e.Handled = true;
            }
        }

        private void Calendar_PointerMoved(object sender, PointerRoutedEventArgs e)
        {
            if (ViewModel != null && _isDragging)
            {
                var point = e.GetCurrentPoint(CalendarGridView).Position;
                int index = GetIndexFromPoint(point);

                if (index != -1 && ViewModel.DayCells[index].IsInteractive)
                {
                    ViewModel.SelectRange(_dragStartIndex, index);
                    UpdateAllCellBrushes();
                    e.Handled = true;
                }
            }
        }

        private void Calendar_PointerReleased(object sender, PointerRoutedEventArgs e)
        {
            if (_isDragging)
            {
                _isDragging = false;
                CalendarGridView.ReleasePointerCapture(e.Pointer);
                e.Handled = true;
            }
        }

        private void Calendar_PointerExited(object sender, PointerRoutedEventArgs e)
        {
            if (_isDragging)
            {
                _isDragging = false;
                CalendarGridView.ReleasePointerCaptures();
                e.Handled = true;
            }
        }

        private void Calendar_RightTapped(object sender, RightTappedRoutedEventArgs e)
        {
            if (ViewModel == null) return;
            var point = e.GetPosition(CalendarGridView);
            int index = GetIndexFromPoint(point);

            var tappedCell = (index >= 0 && index < ViewModel.DayCells.Count) ? ViewModel.DayCells[index] : null;

            if (tappedCell != null && tappedCell.IsInteractive)
            {
                if (!ViewModel.SelectedIndices.Contains(tappedCell.Index))
                {
                    ViewModel.SelectSingle(tappedCell.Index);
                    UpdateAllCellBrushes();
                }

                // TODO: Logika menu kontekstowego
                e.Handled = true;
            }
        }
    }
}