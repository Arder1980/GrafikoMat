using GrafikoMat.Common;
using GrafikoMat.Models;
using GrafikoMat.ViewModels;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Input;
using System;

namespace GrafikoMat.Views
{
    public sealed partial class DeclarationsView : UserControl
    {
        public DeclarationsViewModel? ViewModel { get; private set; }

        public event Action? CloseRequested;
        public event Action<DoctorMonthDeclaration>? SaveRequested;
        public event Action<DoctorMonthDeclaration>? SaveAndCloseRequested;

        public DeclarationsView()
        {
            InitializeComponent();
        }

        public void LoadContext(int year, int monthIndex, string[] doctorNames, int selectedDoctorIndex)
        {
            ViewModel = new DeclarationsViewModel(year, monthIndex, doctorNames, selectedDoctorIndex);
            this.DataContext = ViewModel;
        }

        public void TriggerSave() { if (ViewModel != null) SaveRequested?.Invoke(ViewModel.ToResult()); }
        public void TriggerSaveAndClose() { if (ViewModel != null) SaveAndCloseRequested?.Invoke(ViewModel.ToResult()); }

        private void OnPrevDoctor(object sender, RoutedEventArgs e) => ViewModel?.SelectPrevDoctor();
        private void OnNextDoctor(object sender, RoutedEventArgs e) => ViewModel?.SelectNextDoctor();

        private bool _dragSelecting;
        private int? _startIndex;

        private void OnCellTapped(object sender, TappedRoutedEventArgs e)
        {
            if ((sender as FrameworkElement)?.DataContext is DayCell cell && ViewModel != null)
            {
                ViewModel.SelectSingle(cell.Index);
                _startIndex = cell.Index;
            }
        }

        private void OnCellPointerPressed(object sender, PointerRoutedEventArgs e)
        {
            if (e.Pointer.IsInContact && e.GetCurrentPoint(null).Properties.IsLeftButtonPressed
                && (sender as FrameworkElement)?.DataContext is DayCell cell && ViewModel != null)
            {
                _dragSelecting = true;
                _startIndex = cell.Index;
                ViewModel.SelectSingle(cell.Index);
            }
        }

        private void OnCellPointerReleased(object sender, PointerRoutedEventArgs e)
        {
            if (_dragSelecting && (sender as FrameworkElement)?.DataContext is DayCell cell && _startIndex is int s && ViewModel != null)
                ViewModel.SelectRange(s, cell.Index);
            _dragSelecting = false;
        }

        private void OnCellRightTapped(object sender, RightTappedRoutedEventArgs e)
        {
            if ((sender as FrameworkElement)?.DataContext is not DayCell cell || ViewModel is null) return;

            if (!ViewModel.SelectedIndices.Contains(cell.Index))
                ViewModel.SelectSingle(cell.Index);

            var menu = BuildContextMenu(cell);
            menu.ShowAt(sender as FrameworkElement, new FlyoutShowOptions { Placement = FlyoutPlacementMode.Bottom });
        }

        private MenuFlyout BuildContextMenu(DayCell cell)
        {
            var menu = new MenuFlyout();

            var setDecl = new MenuFlyoutSubItem { Text = "Ustaw deklarację…" };
            foreach (var sym in new[] { '-', 'D', 'N', 'X', 'P' })
            {
                setDecl.Items.Add(new MenuFlyoutItem
                {
                    Text = sym == '-' ? "— (puste)" : sym.ToString(),
                    Command = new RelayCommand(_ => ViewModel?.ApplySymbolToSelection(sym))
                });
            }
            menu.Items.Add(setDecl);

            menu.Items.Add(new MenuFlyoutSeparator());

            menu.Items.Add(new MenuFlyoutItem
            {
                Text = cell.IsSplit ? "Scal dzień (24h)" : "Podziel na 12+12h",
                Command = new RelayCommand(_ => ViewModel?.ToggleSplitForSelectedDays())
            });

            menu.Items.Add(new MenuFlyoutSeparator());

            menu.Items.Add(new MenuFlyoutItem
            {
                Text = "Wyczyść",
                Command = new RelayCommand(_ => ViewModel?.ClearSelectionSymbols())
            });

            menu.Items.Add(new MenuFlyoutItem
            {
                Text = "Ustaw co-dyżurnego…",
                Command = new RelayCommand(_ => ViewModel?.EditCoDutyPlaceholder()),
                Opacity = 0.7
            });

            return menu;
        }

        private void OnClearSelection(object sender, RoutedEventArgs e) => ViewModel?.ClearSelection();
        private void OnSave(object sender, RoutedEventArgs e) => TriggerSave();
        private void OnSaveAndClose(object sender, RoutedEventArgs e) => TriggerSaveAndClose();
        private void OnCloseRequested(object sender, RoutedEventArgs e) => CloseRequested?.Invoke();
    }
}
