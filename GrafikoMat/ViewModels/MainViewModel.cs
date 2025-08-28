using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using GrafikoMat.Models;

namespace GrafikoMat.ViewModels
{
    public class MainViewModel : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler? PropertyChanged;
        private void OnPropertyChanged([CallerMemberName] string? name = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

        // Jednostka
        private string _unitName = "Jednostka: (ustaw w Ustawieniach)";
        public string UnitName
        {
            get => _unitName;
            set { if (_unitName != value) { _unitName = value; OnPropertyChanged(); } }
        }

        // Lata / miesiące
        public ObservableCollection<int> Years { get; } = new(new[] { 2024, 2025, 2026, 2027, 2028 });

        private int _selectedYear = 2025;
        public int SelectedYear
        {
            get => _selectedYear;
            set { if (_selectedYear != value) { EnsureYearInList(value); _selectedYear = value; OnPropertyChanged(); } }
        }

        public string[] Months { get; } = new[]
        {
            "Styczeń","Luty","Marzec","Kwiecień","Maj","Czerwiec",
            "Lipiec","Sierpień","Wrzesień","Październik","Listopad","Grudzień"
        };

        private int _selectedMonthIndex = 0; // 0=Styczeń
        public int SelectedMonthIndex
        {
            get => _selectedMonthIndex;
            set { if (_selectedMonthIndex != value) { _selectedMonthIndex = value; OnPropertyChanged(); } }
        }

        // Dane do lewej/prawej tabeli (placeholder)
        public ObservableCollection<DoctorRow> DoctorRows { get; } = new();
        public ObservableCollection<RosterRow> RosterRows { get; } = new();

        // Stopka
        private string _engineName = "Silnik: Klasyczny";
        public string EngineName
        {
            get => _engineName;
            set { if (_engineName != value) { _engineName = value; OnPropertyChanged(); } }
        }

        private string _priorityOrder = "Priorytety: Dostępność > Sprawiedliwość > Preferencje";
        public string PriorityOrder
        {
            get => _priorityOrder;
            set { if (_priorityOrder != value) { _priorityOrder = value; OnPropertyChanged(); } }
        }

        // Deklaracje (tymczasowy magazyn UI)
        private readonly Dictionary<string, DoctorMonthDeclaration> _declByKey = new();
        private static string Key(string doctor, int year, int monthIndex)
            => $"{doctor}|{year:D4}-{monthIndex:D2}";

        public MainViewModel()
        {
            // Demo – kilka wierszy
            DoctorRows.Add(new DoctorRow("Kowalski Jan", false));
            DoctorRows.Add(new DoctorRow("Nowak Anna", true));
            DoctorRows.Add(new DoctorRow("Wiśniewski Piotr", true));
            DoctorRows.Add(new DoctorRow("Zielińska Maria", false));

            RosterRows.Add(new RosterRow("01 (Pon)", "—"));
            RosterRows.Add(new RosterRow("02 (Wto)", "—"));
            RosterRows.Add(new RosterRow("03 (Śro)", "—"));
        }

        public void PrevYear() => SelectedYear -= 1;
        public void NextYear() => SelectedYear += 1;

        public void PrevMonth()
        {
            if (SelectedMonthIndex == 0) { SelectedMonthIndex = 11; PrevYear(); }
            else SelectedMonthIndex -= 1;
        }

        public void NextMonth()
        {
            if (SelectedMonthIndex == 11) { SelectedMonthIndex = 0; NextYear(); }
            else SelectedMonthIndex += 1;
        }

        private void EnsureYearInList(int year)
        {
            if (!Years.Contains(year))
            {
                int i = 0; while (i < Years.Count && Years[i] < year) i++;
                Years.Insert(i, year);
            }
        }

        // Zastosuj wynik z okna „Deklaracje…”
        public void ApplyDoctorMonth(DoctorMonthDeclaration dm)
        {
            _declByKey[Key(dm.Doctor, dm.Year, dm.MonthIndex)] = dm;

            var row = DoctorRows.FirstOrDefault(r => r.Name == dm.Doctor);
            if (row != null) row.HasDeclarations = true;

            OnPropertyChanged(nameof(_declByKey)); // sygnał do rebuildu
        }

        // Pobierz wpis do renderu
        public (bool has, DayMode mode, string? full, string? day, string? night)
            TryGetEntry(string doctor, int year, int monthIndex, int dayIndex)
        {
            if (_declByKey.TryGetValue(Key(doctor, year, monthIndex), out var dm) &&
                dayIndex >= 0 && dayIndex < dm.Days.Length)
            {
                var d = dm.Days[dayIndex];
                return (true, d.Mode, d.Full, d.Day, d.Night);
            }
            return (false, DayMode.Full24, null, null, null);
        }
    }

    public class DoctorRow : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler? PropertyChanged;
        private void Raise(string n) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(n));

        public string Name { get; }
        private bool _has;
        public bool HasDeclarations { get => _has; set { if (_has != value) { _has = value; Raise(nameof(HasDeclarations)); } } }

        public DoctorRow(string name, bool hasDeclarations) { Name = name; _has = hasDeclarations; }
    }

    public class RosterRow
    {
        public string DateLabel { get; }
        public string DutyLabel { get; }
        public RosterRow(string dateLabel, string dutyLabel) { DateLabel = dateLabel; DutyLabel = dutyLabel; }
    }
}
