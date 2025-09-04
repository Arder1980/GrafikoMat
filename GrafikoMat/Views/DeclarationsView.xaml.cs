using System;
using System.Collections.ObjectModel;
using Microsoft.UI;
using Microsoft.UI.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using GrafikoMat.Models;
using Windows.UI;

namespace GrafikoMat.Views
{
    public sealed partial class DeclarationsView : UserControl
    {
        public event Action<DoctorMonthDeclaration>? SaveRequested;
        public event Action<DoctorMonthDeclaration>? SaveAndCloseRequested;
        public event Action? CloseRequested;

        public ObservableCollection<DayCell> CalendarItems { get; } = new();

        private int _year;
        private int _monthIndex;
        private string[] _doctorNames = Array.Empty<string>();
        private int _selectedDoctorIndex = -1;
        private bool _isGridBuilt = false;

        public DeclarationsView()
        {
            InitializeComponent();

            this.Loaded += DeclarationsView_InitialBuild;
            CalendarGridHost.SizeChanged += CalendarGridHost_SizeChanged;

            var today = DateTime.Today;
            var testDoctors = new[] { "dr Anna Testowa", "dr Bartosz Przykładowy", "dr Celina Demo" };
            LoadContext(today.Year, today.Month - 1, testDoctors, 0);
        }

        private void DeclarationsView_InitialBuild(object sender, RoutedEventArgs e)
        {
            this.Loaded -= DeclarationsView_InitialBuild;
            BuildCalendarGrid();
        }

        private void CalendarGridHost_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            if (_isGridBuilt && e.NewSize.Width > 0 && e.NewSize.Height > 0)
            {
                BuildCalendarGrid();
            }
        }

        public void LoadContext(int year, int monthIndex, string[] doctorNames, int selectedDoctorIndex)
        {
            _year = year;
            _monthIndex = Math.Clamp(monthIndex, 0, 11);
            _doctorNames = (doctorNames != null && doctorNames.Length > 0) ? doctorNames : new[] { "..." };
            _selectedDoctorIndex = Math.Clamp(selectedDoctorIndex, 0, _doctorNames.Length - 1);

            MonthRun.Text = $"{PolishMonth(_monthIndex)} {_year}";
            DoctorsCombo.ItemsSource = _doctorNames;
            DoctorsCombo.SelectedIndex = _selectedDoctorIndex;

            if (_isGridBuilt)
            {
                BuildCalendarGrid();
            }
        }

        private void BuildCalendarGrid()
        {
            CalendarGridHost.Children.Clear();
            CalendarGridHost.RowDefinitions.Clear();
            CalendarGridHost.ColumnDefinitions.Clear();

            // ZMIANA: Dodajemy odstępy między komórkami
            CalendarGridHost.RowSpacing = 4;
            CalendarGridHost.ColumnSpacing = 4;

            var firstDayOfMonth = new DateTime(_year, _monthIndex + 1, 1);
            int offset = ((int)firstDayOfMonth.DayOfWeek + 6) % 7;
            int daysInMonth = DateTime.DaysInMonth(_year, _monthIndex + 1);
            int weeks = (int)Math.Ceiling((offset + daysInMonth) / 7.0);
            var startDate = firstDayOfMonth.AddDays(-offset);

            CalendarItems.Clear();
            for (int i = 0; i < weeks * 7; i++)
            {
                var date = startDate.AddDays(i);
                CalendarItems.Add(new DayCell(date, date.Month == _monthIndex + 1));
            }

            for (int c = 0; c < 7; c++)
                CalendarGridHost.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            CalendarGridHost.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            for (int w = 0; w < weeks; w++)
                CalendarGridHost.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

            var dayNames = new[] { "Pn", "Wt", "Śr", "Cz", "Pt", "So", "Nd" };
            for (int c = 0; c < 7; c++)
            {
                var textBlock = new TextBlock
                {
                    Text = dayNames[c],
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center,
                    Margin = new Thickness(0, 4, 0, 4),
                    Opacity = (c >= 5) ? 0.7 : 1.0
                };
                Grid.SetRow(textBlock, 0);
                Grid.SetColumn(textBlock, c);
                CalendarGridHost.Children.Add(textBlock);
            }

            for (int i = 0; i < CalendarItems.Count; i++)
            {
                var dayCellVM = CalendarItems[i];
                var border = new Border { DataContext = dayCellVM, CornerRadius = new CornerRadius(6) };
                border.Background = dayCellVM.Background;
                border.BorderBrush = dayCellVM.BorderBrush;
                border.BorderThickness = dayCellVM.BorderThickness;

                var contentGrid = new Grid { Padding = new Thickness(6) };
                var dayNumberText = new TextBlock
                {
                    Text = dayCellVM.DayNumber,
                    FontWeight = FontWeights.SemiBold,
                    Opacity = dayCellVM.HeaderOpacity,
                    Margin = new Thickness(2, 0, 2, 4)
                };
                contentGrid.Children.Add(dayNumberText);
                border.Child = contentGrid;

                border.RightTapped += OnCellRightTapped;

                int row = (i / 7) + 1;
                int col = i % 7;
                Grid.SetRow(border, row);
                Grid.SetColumn(border, col);
                CalendarGridHost.Children.Add(border);
            }
            _isGridBuilt = true;
        }

        public void TriggerClearSelection() { /* ... */ }
        public void TriggerSave() { SaveRequested?.Invoke(ToResult()); }
        public void TriggerSaveAndClose() { SaveAndCloseRequested?.Invoke(ToResult()); CloseRequested?.Invoke(); }

        private void OnSaveClick(object sender, RoutedEventArgs e) => TriggerSave();
        private void OnSaveAndCloseClick(object sender, RoutedEventArgs e) => TriggerSaveAndClose();

        private void OnDoctorSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            _selectedDoctorIndex = DoctorsCombo.SelectedIndex;
        }

        private void OnCellRightTapped(object sender, RightTappedRoutedEventArgs e)
        {
            e.Handled = true;
            if ((sender as FrameworkElement)?.DataContext is DayCell cell)
            {
                // Tutaj w przyszłości będzie logika menu kontekstowego
            }
        }

        private DoctorMonthDeclaration ToResult()
        {
            int y = _year; int m = _monthIndex;
            if (y <= 0) { var t = DateTime.Today; y = t.Year; m = t.Month - 1; }
            int daysInMonth = DateTime.DaysInMonth(y, m + 1);
            var res = new DoctorMonthDeclaration
            {
                Year = y,
                MonthIndex = m,
                Doctor = (_doctorNames.Length > 0 && _selectedDoctorIndex >= 0) ? _doctorNames[_selectedDoctorIndex] : string.Empty,
                Days = new DayDeclaration[daysInMonth]
            };
            for (int i = 0; i < daysInMonth; i++) res.Days[i] = new DayDeclaration();
            return res;
        }

        private static string PolishMonth(int idx) => new[] { "Styczeń", "Luty", "Marzec", "Kwiecień", "Maj", "Czerwiec", "Lipiec", "Sierpień", "Wrzesień", "Październik", "Listopad", "Grudzień" }[Math.Clamp(idx, 0, 11)];

        public sealed class DayCell
        {
            public DateTime Date { get; }
            public bool InMonth { get; }

            public DayCell(DateTime date, bool inMonth)
            {
                Date = date;
                InMonth = inMonth;
                UpdateBrushes();
            }

            public string DayNumber => Date.Day.ToString("00");
            public double HeaderOpacity => InMonth ? 1.0 : 0.45;
            public Brush Background { get; private set; }
            public Brush BorderBrush { get; private set; }
            public Thickness BorderThickness { get; private set; } = new Thickness(1);

            private void UpdateBrushes()
            {
                bool isToday = (Date.Date == DateTime.Today);
                Color bgColor = InMonth ? Colors.Transparent : Color.FromArgb(0x10, 0x80, 0x80, 0x80);
                Color borderColor = InMonth ? Color.FromArgb(0x30, 0, 0, 0) : Color.FromArgb(0x25, 0x60, 0x60, 0x60);
                if (isToday)
                {
                    bgColor = Color.FromArgb(0x22, 0x1E, 0x90, 0xFF);
                    borderColor = Color.FromArgb(0xAA, 0x1E, 0x90, 0xFF);
                }
                Background = new SolidColorBrush(bgColor);
                BorderBrush = new SolidColorBrush(borderColor);
            }
        }
    }
}