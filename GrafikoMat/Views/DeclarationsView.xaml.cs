using System;
using System.Linq;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Animation;
using GrafikoMat.ViewModels;

namespace GrafikoMat.Views
{
    public sealed partial class DeclarationsView : UserControl
    {
        private const int Columns = 7;
        private const double ItemMargin = 4.0; // musi odpowiadać Margin w XAML

        private Storyboard? _calendarSb;
        private DeclarationsViewModel? _vm;

        public DeclarationsView()
        {
            this.InitializeComponent();
            this.Loaded += OnLoaded;
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            // Gdy ItemsPanelRoot się pojawi, policz pierwszy layout
            CalendarItems.Loaded += (_, __) => UpdateCalendarLayout();
            CalendarItems.LayoutUpdated += (_, __) => UpdateCalendarLayout();
            UpdateCalendarLayout();
        }

        public void LoadContext(int year, int monthIndex, string[] doctorNames, int selectedDoctorIndex)
        {
            _vm = new DeclarationsViewModel(year, monthIndex, doctorNames, selectedDoctorIndex);
            this.DataContext = _vm;
            UpdateCalendarLayout();
        }

        // API dla MainWindow (pasek akcji)
        public event Action<Models.DoctorMonthDeclaration>? SaveRequested;
        public event Action<Models.DoctorMonthDeclaration>? SaveAndCloseRequested;
        public event Action? CloseRequested;
        public void TriggerSave() => OnSave(this, new RoutedEventArgs());
        public void TriggerSaveAndClose() => OnSaveAndClose(this, new RoutedEventArgs());
        public void TriggerClearSelection() => OnClearSelection(this, new RoutedEventArgs());

        // Zmiana lekarza -> lekki slide
        private void OnPrevDoctor(object sender, RoutedEventArgs e) { BumpDoctor(-1); AnimateCalendarSlide(+1); }
        private void OnNextDoctor(object sender, RoutedEventArgs e) { BumpDoctor(+1); AnimateCalendarSlide(-1); }

        private void BumpDoctor(int delta)
        {
            if (_vm is null) return;
            try
            {
                var list = _vm.Doctors?.ToList() ?? [];
                if (list.Count == 0) return;

                var cur = _vm.SelectedDoctor;
                int idx = Math.Max(0, list.IndexOf(cur));
                int next = (idx + delta + list.Count) % list.Count;
                _vm.SelectedDoctor = list[next];
            }
            catch { /* bez wywrotki UI */ }
        }

        private void AnimateCalendarSlide(int direction)
        {
            _calendarSb?.Stop();

            var tt = CalendarAnimHost.RenderTransform as TranslateTransform ?? new TranslateTransform();
            CalendarAnimHost.RenderTransform = tt;

            double offset = 120 * Math.Sign(direction);
            tt.X = offset;
            CalendarAnimHost.Opacity = 0.0;

            var sb = new Storyboard();
            _calendarSb = sb;

            var dur = TimeSpan.FromMilliseconds(220);
            var ease = new CubicEase { EasingMode = EasingMode.EaseOut };

            var animX = new DoubleAnimation { From = offset, To = 0, Duration = dur, EasingFunction = ease };
            Storyboard.SetTarget(animX, CalendarAnimHost);
            Storyboard.SetTargetProperty(animX, "(UIElement.RenderTransform).(TranslateTransform.X)");
            sb.Children.Add(animX);

            var animOp = new DoubleAnimation { From = 0, To = 1, Duration = dur, EasingFunction = ease };
            Storyboard.SetTarget(animOp, CalendarAnimHost);
            Storyboard.SetTargetProperty(animOp, "Opacity");
            sb.Children.Add(animOp);

            sb.Begin();
        }

        // === DYNAMICZNE DOPASOWANIE KAFLI ===

        private void OnCalendarSizeChanged(object sender, SizeChangedEventArgs e)
            => UpdateCalendarLayout();

        private void UpdateCalendarLayout()
        {
            if (CalendarItems.ItemsPanelRoot is not ItemsWrapGrid wrap) return;

            // 1) POBIERZ REALNY OBSZAR DOSTĘPNY DLA PRAWEJ KOLUMNY
            double availableWidth = Math.Max(0, RightHost.ActualWidth);
            double availableHeight = Math.Max(0, RightHost.ActualHeight);

            if (availableWidth <= 0 || availableHeight <= 0)
                return;

            // 2) ROZCIĄGNIJ ItemsControl, żeby panel dostał pełną szerokość
            CalendarItems.Width = availableWidth;

            // 3) Ile wierszy będzie (max 6)
            int count = CalendarItems.Items.Count;
            int rows = Math.Max(1, (int)Math.Ceiling(count / (double)Columns));
            rows = Math.Min(rows, 6);

            // 4) Wysokość nagłówka (dni tygodnia)
            double headerHeight = WeekHeaderGrid.ActualHeight;

            // 5) LICZENIA: szerokość i wysokość kafla, tak by:
            //    - 7 kolumn zmieściło się poziomo (z marginesami),
            //    - wszystkie rzędy zmieściły się pionowo (bez scrolla).
            double totalMarginsW = 2 * ItemMargin * Columns;
            double itemWidth = (availableWidth - totalMarginsW) / Columns;
            itemWidth = Math.Floor(Math.Max(60, itemWidth)); // sensowny dolny limit

            double spaceForGrid = Math.Max(0, availableHeight - headerHeight);
            double totalMarginsH = 2 * ItemMargin * rows;
            double itemHeight = (spaceForGrid - totalMarginsH) / rows;
            itemHeight = Math.Floor(Math.Max(50, itemHeight)); // priorytet: zmieścić się pionowo

            // 6) Zastosuj do panelu
            wrap.ItemWidth = itemWidth;
            wrap.ItemHeight = itemHeight;

            // 7) Nagłówki wyrównane do siatki
            WeekHeaderGrid.ColumnSpacing = 2 * ItemMargin;
            for (int i = 0; i < WeekHeaderGrid.ColumnDefinitions.Count; i++)
                WeekHeaderGrid.ColumnDefinitions[i].Width = new GridLength(itemWidth);

            // lewy margines = ItemMargin (jak kafle)
            if (Math.Abs(WeekHeaderGrid.Margin.Left - ItemMargin) > 0.5)
                WeekHeaderGrid.Margin = new Thickness(ItemMargin, 0, 0, 0);
        }

        // ==== Handlery (Twoja logika) ====
        private void OnCellTapped(object sender, Microsoft.UI.Xaml.Input.TappedRoutedEventArgs e) { }
        private void OnCellRightTapped(object sender, Microsoft.UI.Xaml.Input.RightTappedRoutedEventArgs e) { }
        private void OnCellPointerPressed(object sender, Microsoft.UI.Xaml.Input.PointerRoutedEventArgs e) { }
        private void OnCellPointerReleased(object sender, Microsoft.UI.Xaml.Input.PointerRoutedEventArgs e) { }
        private void OnClearSelection(object sender, RoutedEventArgs e) { }
        private void OnSave(object sender, RoutedEventArgs e) { /* SaveRequested?.Invoke(dm); */ }
        private void OnSaveAndClose(object sender, RoutedEventArgs e) { /* SaveAndCloseRequested?.Invoke(dm); */ }
        private void OnCloseRequested(object sender, RoutedEventArgs e) { CloseRequested?.Invoke(); }
    }
}
