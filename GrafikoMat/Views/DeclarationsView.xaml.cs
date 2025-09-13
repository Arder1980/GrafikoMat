using GrafikoMat.ViewModels;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using System;

namespace GrafikoMat.Views
{
    public sealed partial class DeclarationsView : UserControl
    {
        public event Action? SaveRequested;
        public event Action? SaveAndCloseRequested;
        public event Action? CloseRequested;

        public DeclarationsViewModel ViewModel => this.DataContext as DeclarationsViewModel;
        private bool _isDragging = false;
        private int _dragStartIndex = -1;

        public DeclarationsView()
        {
            this.InitializeComponent();
            // Subskrypcja zdarzenia zmiany motywu
            this.ActualThemeChanged += OnThemeChanged;
            this.Unloaded += OnDeclarationsViewUnloaded;
        }

        public void AttachViewModel(DeclarationsViewModel vm)
        {
            this.DataContext = vm;
            // Wymuś aktualizację kolorów przy pierwszym dołączeniu
            UpdateAllCellBrushes();
        }

        private void OnSaveClick(object sender, RoutedEventArgs e)
        {
            ViewModel?.SaveCommand.Execute(null);
            SaveRequested?.Invoke();
        }

        private void OnSaveAndCloseClick(object sender, RoutedEventArgs e)
        {
            ViewModel?.SaveCommand.Execute(null);
            SaveAndCloseRequested?.Invoke();
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
                e.Handled = true;
            }
        }

        private void Cell_PointerMoved(object sender, PointerRoutedEventArgs e)
        {
            if (ViewModel != null && _isDragging && sender is FrameworkElement element && element.DataContext is DayCell cell)
            {
                if (!cell.IsInteractive) return;
                ViewModel.SelectRange(_dragStartIndex, cell.Index);
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
                // TODO: Logika menu kontekstowego
                e.Handled = true;
            }
        }

        private void OnThemeChanged(FrameworkElement sender, object args)
        {
            // Gdy motyw się zmienia, zaktualizuj pędzle we wszystkich komórkach
            UpdateAllCellBrushes();
        }

        private void UpdateAllCellBrushes()
        {
            if (ViewModel?.DayCells == null) return;

            foreach (var cell in ViewModel.DayCells)
            {
                cell.UpdateBrushesForTheme(this.ActualTheme);
            }
        }

        private void OnDeclarationsViewUnloaded(object sender, RoutedEventArgs e)
        {
            // Anuluj subskrypcję, aby uniknąć wycieków pamięci
            this.ActualThemeChanged -= OnThemeChanged;
            this.Unloaded -= OnDeclarationsViewUnloaded;
        }
    }
}