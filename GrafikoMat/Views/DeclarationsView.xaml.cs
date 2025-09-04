using GrafikoMat.Models;
using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using System;
using System.Collections.ObjectModel;
using Windows.UI;

namespace GrafikoMat.Views
{
    public sealed partial class DeclarationsView : UserControl
    {
        public event Action<DoctorMonthDeclaration>? SaveRequested;
        public event Action<DoctorMonthDeclaration>? SaveAndCloseRequested;
        public event Action? CloseRequested;

        public ObservableCollection<DayCell> CalendarItems { get; } = new();

        // ZMIANA: Udostępniamy elementy UI na zewnątrz dla MainWindow
        public UIElement LeftColumn => LeftColumnGrid;
        public UIElement CalendarView => CalendarItemsControl;

        private int _year;
        private int _monthIndex;
        private string[] _doctorNames = Array.Empty<string>();
        private int _selectedDoctorIndex = -1;

        public DeclarationsView()
        {
            InitializeComponent();
            var today = DateTime.Today;
            var testDoctors = new[] { "dr Anna Testowa", "dr Bartosz Przykładowy", "dr Celina Demo" };
            LoadContext(today.Year, today.Month - 1, testDoctors, 0);
        }

        // ZMIANA: Metoda OnDeclarationsViewLoaded została usunięta.
        // ZMIANA: Metoda AnimateCalendarIn została usunięta.

        public void LoadContext(int year, int monthIndex, string[] doctorNames, int selectedDoctorIndex)
        {
            _year = year;
            _monthIndex = Math.Clamp(monthIndex, 0, 11);
            _doctorNames = (doctorNames != null && doctorNames.Length > 0) ? doctorNames : new[] { "..." };
            _selectedDoctorIndex = Math.Clamp(selectedDoctorIndex, 0, _doctorNames.Length - 1);

            MonthRun.Text = $"{PolishMonth(_monthIndex)} {_year}";
            DoctorsCombo.ItemsSource = _doctorNames;
            DoctorsCombo.SelectedIndex = selectedDoctorIndex; // Używamy przekazanego indeksu

            BuildCalendarData();
        }

        private void BuildCalendarData()
        {
            CalendarItems.Clear();
            var firstDayOfMonth = new DateTime(_year, _monthIndex + 1, 1);
            int offset = ((int)firstDayOfMonth.DayOfWeek + 6) % 7;
            int daysInMonth = DateTime.DaysInMonth(_year, _monthIndex + 1);
            int weeks = (int)Math.Ceiling((offset + daysInMonth) / 7.0);
            var startDate = firstDayOfMonth.AddDays(-offset);

            for (int i = 0; i < weeks * 7; i++)
            {
                var date = startDate.AddDays(i);
                CalendarItems.Add(new DayCell(date, date.Month == _monthIndex + 1));
            }
        }

        public void TriggerClearSelection() { /* ... */ }
        public void TriggerSave() { SaveRequested?.Invoke(ToResult()); }
        public void TriggerSaveAndClose() { SaveAndCloseRequested?.Invoke(ToResult()); CloseRequested?.Invoke(); }

        private void OnSaveClick(object sender, RoutedEventArgs e) => TriggerSave();
        private void OnSaveAndCloseClick(object sender, RoutedEventArgs e) => TriggerSaveAndClose();

        private void OnDoctorSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            _selectedDoctorIndex = DoctorsCombo.SelectedIndex;
            // Tutaj w przyszłości można dodać logikę odświeżania danych dla wybranego lekarza
        }

        private void OnCellRightTapped(object sender, RightTappedRoutedEventArgs e)
        {
            e.Handled = true;
            if ((sender as FrameworkElement)?.DataContext is DayCell cell) { }
        }

        private DoctorMonthDeclaration ToResult()
        {
            int y = _year;
            int m = _monthIndex;
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
    }

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