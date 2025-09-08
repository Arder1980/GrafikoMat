using CommunityToolkit.Mvvm.Input;
using GrafikoMat.Common;
using GrafikoMat.Core.Data;
using GrafikoMat.Core.Repositories;
using GrafikoMat.Models;
using GrafikoMat.Services; // NOWY using
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Windows.Input;


namespace GrafikoMat.ViewModels
{
    public class MainViewModel : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler? PropertyChanged;
        private void OnPropertyChanged([CallerMemberName] string? name = null) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

        // ZMIANA: Usunięcie repozytorium lekarzy, będziemy działać na DataService
        private DataService? _dataService;

        // NOWE: Pola do zarządzania stanem jednostek
        private readonly List<Unit> _userUnits = new();
        private int _activeUnitIndex = -1;

        // NOWA: Właściwość przechowująca informację o statusie admina
        public bool IsCurrentUserAdmin { get; private set; }

        // NOWA: Właściwość zwracająca pełny obiekt aktywnej jednostki
        public Unit? ActiveUnit => _activeUnitIndex >= 0 && _activeUnitIndex < _userUnits.Count
            ? _userUnits[_activeUnitIndex]
            : null;

        // ZMIANA: Gettery tych właściwości pobierają teraz dane z ActiveUnit
        public string ActiveUnitHospitalName
        {
            get => ActiveUnit?.HospitalFullName ?? "GrafikoMat Dyżurowy";
        }

        public string ActiveUnitDepartmentName
        {
            get => ActiveUnit?.DepartmentName ?? (IsCurrentUserAdmin ? "Panel Administratora" : "Brak przypisanych jednostek");
        }


        public ObservableCollection<int> Years { get; } = new(new[] { 2024, 2025, 2026, 2027, 2028 });

        private int _selectedYear = DateTime.Today.Year;
        public int SelectedYear { get => _selectedYear; set { if (_selectedYear != value) { EnsureYearInList(value); _selectedYear = value; OnPropertyChanged(); UpdateRosterForSelectedMonth(); } } }

        public string[] Months { get; } = new[] { "Styczeń", "Luty", "Marzec", "Kwiecień", "Maj", "Czerwiec", "Lipiec", "Sierpień", "Wrzesień", "Październik", "Listopad", "Grudzień" };
        private int _selectedMonthIndex = DateTime.Today.Month - 1;
        public int SelectedMonthIndex { get => _selectedMonthIndex; set { if (_selectedMonthIndex != value) { _selectedMonthIndex = value; OnPropertyChanged(); UpdateRosterForSelectedMonth(); } } }

        public ObservableCollection<DoctorRow> DoctorRows { get; } = new();
        public ObservableCollection<RosterRow> RosterRows { get; } = new();

        private string _engineName = "Silnik: Klasyczny";
        public string EngineName { get => _engineName; set { if (_engineName != value) { _engineName = value; OnPropertyChanged(); } } }

        private string _priorityOrder = "Priorytety: Dostępność > Sprawiedliwość > Preferencje";
        public string PriorityOrder { get => _priorityOrder; set { if (_priorityOrder != value) { _priorityOrder = value; OnPropertyChanged(); } } }

        private readonly Dictionary<string, DoctorMonthDeclaration> _declByKey = new();
        private static string Key(string doctor, int year, int monthIndex) => $"{doctor}|{year:D4}-{monthIndex:D2}";

        public ICommand SwitchToPreviousUnitCommand { get; set; }
        public ICommand SwitchToNextUnitCommand { get; set; }

        // ZMIANA: Konstruktor nie przyjmuje już repozytorium
        public MainViewModel()
        {
            // ZMIANA: Implementacja komend zostanie dodana w Etapie 2
            SwitchToPreviousUnitCommand = new RelayCommand(() => { });
            SwitchToNextUnitCommand = new RelayCommand(() => { });
            UpdateRosterForSelectedMonth();
        }

        // NOWA METODA: Do wstrzykiwania DataService po jego utworzeniu w MainWindow
        public void SetDataService(DataService? dataService)
        {
            _dataService = dataService;
        }

        // NOWA, KLUCZOWA METODA: Wczytuje dane o użytkowniku i na tej podstawie listę jednostek
        public async Task LoadUserAndUnitDataAsync()
        {
            if (_dataService == null) return;

            var userProfile = await _dataService.GetCurrentDoctorProfileAsync();
            if (userProfile == null)
            {
                // Obsługa błędu - nie udało się pobrać profilu
                return;
            }

            IsCurrentUserAdmin = userProfile.IsAdmin;
            OnPropertyChanged(nameof(IsCurrentUserAdmin));

            _userUnits.Clear();

            if (IsCurrentUserAdmin)
            {
                // ADMIN: Wczytaj wszystkie jednostki
                var allUnits = await _dataService.GetAllUnitsAsync();
                allUnits.Sort(); // Używa IComparable zaimplementowanego w Unit.cs
                _userUnits.AddRange(allUnits);
            }
            else
            {
                // ZWYKŁY UŻYTKOWNIK: Wczytaj tylko przypisane jednostki
                var assignments = await _dataService.GetAssignmentsForDoctorAsync(userProfile.Id);
                if (assignments.Any())
                {
                    var allUnits = await _dataService.GetAllUnitsAsync();
                    var assignedUnitIds = assignments.Select(a => a.UnitId).ToHashSet();
                    var filteredUnits = allUnits.Where(u => assignedUnitIds.Contains(u.Id)).ToList();
                    filteredUnits.Sort();
                    _userUnits.AddRange(filteredUnits);
                }
            }

            // Ustaw pierwszą jednostkę jako aktywną (w Etapie 4 dodamy tu logikę przywracania)
            _activeUnitIndex = _userUnits.Any() ? 0 : -1;

            // Odśwież UI
            OnPropertyChanged(nameof(ActiveUnit));
            OnPropertyChanged(nameof(ActiveUnitHospitalName));
            OnPropertyChanged(nameof(ActiveUnitDepartmentName));
        }


        // ZMIANA: Metoda zostanie zmodyfikowana w Etapie 3. Na razie czyści listę lekarzy.
        public async Task LoadDoctorsAsync()
        {
            DoctorRows.Clear();
            // W Etapie 3 ta metoda zostanie rozbudowana o wczytywanie lekarzy
            // dla aktywnej jednostki (ActiveUnit)
            await Task.CompletedTask;
        }

        private void UpdateRosterForSelectedMonth()
        {
            RosterRows.Clear();
            int daysInMonth = DateTime.DaysInMonth(SelectedYear, SelectedMonthIndex + 1);
            for (int day = 1; day <= daysInMonth; day++)
            {
                var date = new DateTime(SelectedYear, SelectedMonthIndex + 1, day);
                string dateLabel = $"{date:dd.MM} ({PolishDayOfWeek(date.DayOfWeek)})";
                bool isDayOff = date.DayOfWeek == DayOfWeek.Saturday || date.DayOfWeek == DayOfWeek.Sunday || PolishHolidays.IsHoliday(date);
                bool isLast = (day == daysInMonth);
                RosterRows.Add(new RosterRow(dateLabel, "—", isDayOff, isLast));
            }
        }

        private static string PolishDayOfWeek(DayOfWeek dow) => dow switch { DayOfWeek.Monday => "Poniedziałek", DayOfWeek.Tuesday => "Wtorek", DayOfWeek.Wednesday => "Środa", DayOfWeek.Thursday => "Czwartek", DayOfWeek.Friday => "Piątek", DayOfWeek.Saturday => "Sobota", DayOfWeek.Sunday => "Niedziela", _ => "" };
        public void PrevYear() => SelectedYear -= 1;
        public void NextYear() => SelectedYear += 1;
        public void PrevMonth() { if (SelectedMonthIndex == 0) { SelectedMonthIndex = 11; PrevYear(); } else SelectedMonthIndex -= 1; }
        public void NextMonth() { if (SelectedMonthIndex == 11) { SelectedMonthIndex = 0; NextYear(); } else SelectedMonthIndex += 1; }

        private void EnsureYearInList(int year) { if (!Years.Contains(year)) { int i = 0; while (i < Years.Count && Years[i] < year) i++; Years.Insert(i, year); } }

        public void ApplyDoctorMonth(DoctorMonthDeclaration dm) { _declByKey[Key(dm.Doctor, dm.Year, dm.MonthIndex)] = dm; var row = DoctorRows.FirstOrDefault(r => r.Name == dm.Doctor); if (row != null) row.HasDeclarations = true; OnPropertyChanged(nameof(_declByKey)); }

        public (bool has, DayMode mode, string? full, string? day, string? night) TryGetEntry(string doctor, int year, int monthIndex, int dayIndex) { if (_declByKey.TryGetValue(Key(doctor, year, monthIndex), out var dm) && dayIndex >= 0 && dayIndex < dm.Days.Length) { var d = dm.Days[dayIndex]; return (true, d.Mode, d.Full, d.Day, d.Night); } return (false, DayMode.Full24, null, null, null); }
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
        public bool IsDayOff { get; }
        public bool IsLast { get; }

        public RosterRow(string dateLabel, string dutyLabel, bool isDayOff, bool isLast)
        {
            DateLabel = dateLabel;
            DutyLabel = dutyLabel;
            IsDayOff = isDayOff;
            IsLast = isLast;
        }
    }
}