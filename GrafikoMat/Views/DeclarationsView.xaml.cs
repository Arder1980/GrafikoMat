using GrafikoMat.Services;
using GrafikoMat.ViewModels;
using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using System;
using System.Linq;
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

        private void Calendar_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (ViewModel == null || sender is not GridView gridView) return;

            // ZMIANA: Blokada zaznaczania dni nieaktywnych
            var invalidSelections = e.AddedItems.OfType<DayCell>().Where(c => !c.IsInteractive).ToList();
            if (invalidSelections.Any())
            {
                foreach (var invalid in invalidSelections)
                {
                    gridView.SelectedItems.Remove(invalid);
                }
            }

            // Zdejmij nasz wizualny wskaźnik z komórek, które zostały odznaczone
            foreach (var item in e.RemovedItems)
            {
                if (item is DayCell cell)
                {
                    cell.SetSelected(false);
                    UpdateCellBrushes(cell);
                }
            }

            // Ustaw nasz wizualny wskaźnik na nowo wybranych komórkach
            foreach (var item in e.AddedItems)
            {
                if (item is DayCell cell)
                {
                    cell.SetSelected(true);
                    UpdateCellBrushes(cell);
                }
            }

            // Zsynchronizuj listę indeksów w ViewModelu
            ViewModel.SelectedIndices.Clear();
            foreach (var item in gridView.SelectedItems)
            {
                if (item is DayCell cell)
                {
                    ViewModel.SelectedIndices.Add(cell.Index);
                }
            }
        }

        private void Calendar_RightTapped(object sender, RightTappedRoutedEventArgs e)
        {
            if (ViewModel == null) return;

            var originalSource = e.OriginalSource as FrameworkElement;
            var tappedCell = originalSource?.DataContext as DayCell;

            if (tappedCell != null && tappedCell.IsInteractive)
            {
                var gridView = (GridView)sender;
                if (!gridView.SelectedItems.Contains(tappedCell))
                {
                    ViewModel.ClearSelection();
                    gridView.SelectedItems.Add(tappedCell);
                }

                // TODO: Logika menu kontekstowego
                e.Handled = true;
            }
        }
    }
}