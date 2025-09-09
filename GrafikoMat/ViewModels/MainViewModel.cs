using CommunityToolkit.Mvvm.Input;
using GrafikoMat.Common;
using GrafikoMat.Core.Data;
using GrafikoMat.Core.Repositories;
using GrafikoMat.Models;
using GrafikoMat.Services;
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

        private DataService? _dataService;
        // NOWE: Pole do przechowywania serwisu ustawień
        private SettingsService? _settingsService;

        private readonly List<DoctorProfile> _allDoctors = new();
        private readonly List<UnitDoctorAssignment> _allAssignments = new();

        private readonly List<Unit> _userUnits = new();
        private int _activeUnitIndex = -1;

        public bool IsCurrentUserAdmin { get; private set; }

        public Unit? ActiveUnit => _activeUnitIndex >= 0 && _activeUnitIndex < _userUnits.Count
            ? _userUnits[_activeUnitIndex]
            : null;

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

        public MainViewModel()
        {
            SwitchToPreviousUnitCommand = new RelayCommand(SwitchToPreviousUnit);
            SwitchToNextUnitCommand = new RelayCommand(SwitchToNextUnit);
            UpdateRosterForSelectedMonth();

            // NOWY KROK: Subskrybujemy zdarzenie, aby wykryć zmianę jednostki i zapisać ją.
            this.PropertyChanged += OnMainViewModelPropertyChanged;
        }

        public void SetDataService(DataService? dataService)
        {
            _dataService = dataService;
        }

        // NOWA METODA: Do wstrzykiwania SettingsService
        public void SetSettingsService(SettingsService settingsService)
        {
            _settingsService = settingsService;
        }

        public async Task LoadUserAndUnitDataAsync()
        {
            if (_dataService == null || _settingsService == null) return;

            _allDoctors.Clear();
            _allAssignments.Clear();
            _allDoctors.AddRange(await _dataService.GetAllDoctorsAsync());
            _allAssignments.AddRange(await _dataService.GetAllAssignmentsAsync());

            var userProfile = await _dataService.GetCurrentDoctorProfileAsync();
            if (userProfile == null)
            {
                return;
            }

            IsCurrentUserAdmin = userProfile.IsAdmin;
            OnPropertyChanged(nameof(IsCurrentUserAdmin));

            _userUnits.Clear();

            if (IsCurrentUserAdmin)
            {
                var allUnits = await _dataService.GetAllUnitsAsync();
                allUnits.Sort();
                _userUnits.AddRange(allUnits);
            }
            else
            {
                var assignments = _allAssignments.Where(a => a.DoctorId == userProfile.Id).ToList();
                if (assignments.Any())
                {
                    var allUnits = await _dataService.GetAllUnitsAsync();
                    var assignedUnitIds = assignments.Select(a => a.UnitId).ToHashSet();
                    var filteredUnits = allUnits.Where(u => assignedUnitIds.Contains(u.Id)).ToList();
                    filteredUnits.Sort();
                    _userUnits.AddRange(filteredUnits);
                }
            }

            // ZMIANA: Dodajemy logikę odczytu i przywracania ostatniej jednostki
            var settings = await _settingsService.LoadSettingsAsync();
            var lastUnitId = settings.LastActiveUnitId;

            int targetIndex = 0; // Domyślnie pierwsza jednostka z listy

            if (lastUnitId.HasValue)
            {
                // Szukamy, czy zapisana jednostka jest dostępna dla bieżącego użytkownika
                int foundIndex = _userUnits.FindIndex(u => u.Id == lastUnitId.Value);

                // Jeśli znaleziono (indeks >= 0), to ustawiamy ją jako docelową
                if (foundIndex != -1)
                {
                    targetIndex = foundIndex;
                }
                // Jeśli nie znaleziono, `targetIndex` pozostaje 0, co automatycznie
                // obsługuje przypadek, gdy nowy użytkownik nie ma dostępu do starej jednostki.
            }

            // Ustawiamy aktywny indeks, obsługując przypadek, gdy użytkownik nie ma żadnych jednostek
            _activeUnitIndex = _userUnits.Any() ? targetIndex : -1;

            OnPropertyChanged(nameof(ActiveUnit));
            OnPropertyChanged(nameof(ActiveUnitHospitalName));
            OnPropertyChanged(nameof(ActiveUnitDepartmentName));
        }

        private void SwitchToNextUnit()
        {
            if (_userUnits.Count == 0) return;
            _activeUnitIndex = (_activeUnitIndex + 1) % _userUnits.Count;

            OnPropertyChanged(nameof(ActiveUnit));
            OnPropertyChanged(nameof(ActiveUnitHospitalName));
            OnPropertyChanged(nameof(ActiveUnitDepartmentName));

            LoadDataForActiveUnit();
        }

        private void SwitchToPreviousUnit()
        {
            if (_userUnits.Count == 0) return;
            _activeUnitIndex = (_activeUnitIndex - 1 + _userUnits.Count) % _userUnits.Count;

            OnPropertyChanged(nameof(ActiveUnit));
            OnPropertyChanged(nameof(ActiveUnitHospitalName));
            OnPropertyChanged(nameof(ActiveUnitDepartmentName));

            LoadDataForActiveUnit();
        }

        public void LoadDataForActiveUnit()
        {
            DoctorRows.Clear();

            if (ActiveUnit != null)
            {
                var doctorIdsForUnit = _allAssignments
                    .Where(a => a.UnitId == ActiveUnit.Id && a.IsActive)
                    .Select(a => a.DoctorId)
                    .ToHashSet();

                if (doctorIdsForUnit.Any())
                {
                    var doctorsForUnit = _allDoctors
                        .Where(d => doctorIdsForUnit.Contains(d.Id) && !d.IsArchived);

                    foreach (var doctor in doctorsForUnit.OrderBy(d => d.LastName))
                    {
                        var key = Key(doctor.FullName, SelectedYear, SelectedMonthIndex);
                        bool hasDecls = _declByKey.ContainsKey(key);
                        DoctorRows.Add(new DoctorRow(doctor.FullName, hasDecls));
                    }
                }
            }

            UpdateRosterForSelectedMonth();
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

        // NOWE METODY: Logika zapisu ustawień
        private void OnMainViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(ActiveUnit))
            {
                SaveCurrentUnitAsync();
            }
        }

        private async void SaveCurrentUnitAsync()
        {
            if (_settingsService == null) return;

            var settings = await _settingsService.LoadSettingsAsync();

            // Używamy `ActiveUnit?.Id`, co da `null` jeśli żadna jednostka nie jest aktywna
            var newSettings = settings with { LastActiveUnitId = ActiveUnit?.Id };

            await _settingsService.SaveSettingsAsync(newSettings);
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