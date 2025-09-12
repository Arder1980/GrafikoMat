using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using GrafikoMat.Common;
using GrafikoMat.Core.Data;
using GrafikoMat.Core.Repositories;
using GrafikoMat.Core.Scheduling.Engines;
using GrafikoMat.Core.Scheduling.Models;
using GrafikoMat.Models;
using GrafikoMat.Services;
using Microsoft.UI.Xaml;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using System.Windows.Input;

namespace GrafikoMat.ViewModels
{
    public class MainViewModel : INotifyPropertyChanged, IRecipient<SettingsHaveChangedMessage>
    {
        public event PropertyChangedEventHandler? PropertyChanged;
        private void OnPropertyChanged([CallerMemberName] string? name = null) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

        private IDoctorRepository? _doctorRepository;
        private IUnitRepository? _unitRepository;
        private IAssignmentRepository? _assignmentRepository;
        private SettingsService? _settingsService;

        private readonly List<DoctorProfile> _allDoctors = new();
        private readonly List<UnitDoctorAssignment> _allAssignments = new();
        private readonly List<Unit> _userUnits = new();
        private int _activeUnitIndex = -1;

        public bool IsCurrentUserAdmin { get; private set; }

        [JsonIgnore]
        public XamlRoot? XamlRoot { get; set; }

        public Unit? ActiveUnit => _activeUnitIndex >= 0 && _activeUnitIndex < _userUnits.Count
            ? _userUnits[_activeUnitIndex]
            : null;

        public string ActiveUnitHospitalName => ActiveUnit?.HospitalFullName ?? "GrafikoMat Dyżurowy";
        public string ActiveUnitDepartmentName => ActiveUnit?.DepartmentName ?? (IsCurrentUserAdmin ? "Panel Administratora" : "Brak przypisanych jednostek");

        public ObservableCollection<int> Years { get; } = new(new[] { 2024, 2025, 2026, 2027, 2028 });
        private int _selectedYear = DateTime.Today.Year;
        public int SelectedYear { get => _selectedYear; set { if (_selectedYear != value) { EnsureYearInList(value); _selectedYear = value; OnPropertyChanged(); UpdateRosterForSelectedMonth(); } } }

        public string[] Months { get; } = new[] { "Styczeń", "Luty", "Marzec", "Kwiecień", "Maj", "Czerwiec", "Lipiec", "Sierpień", "Wrzesień", "Październik", "Listopad", "Grudzień" };
        private int _selectedMonthIndex = DateTime.Today.Month - 1;
        public int SelectedMonthIndex { get => _selectedMonthIndex; set { if (_selectedMonthIndex != value) { _selectedMonthIndex = value; OnPropertyChanged(); UpdateRosterForSelectedMonth(); } } }

        public ObservableCollection<DoctorRow> DoctorRows { get; } = new();
        public ObservableCollection<RosterRow> RosterRows { get; } = new();

        private string _engineName = "Silnik: (nieustawiony)";
        public string EngineName { get => _engineName; set { if (_engineName != value) { _engineName = value; OnPropertyChanged(); } } }

        private string _priorityOrder = "Priorytety: (nieustawione)";
        public string PriorityOrder { get => _priorityOrder; set { if (_priorityOrder != value) { _priorityOrder = value; OnPropertyChanged(); } } }

        // NOWA WŁAŚCIWOŚĆ: Przechowuje nazwę zalogowanego użytkownika do wyświetlenia w menu
        private string _currentUserName = "Brak danych";
        public string CurrentUserName
        {
            get => _currentUserName;
            set
            {
                if (_currentUserName != value)
                {
                    _currentUserName = value;
                    OnPropertyChanged();
                }
            }
        }

        private readonly Dictionary<string, DoctorMonthDeclaration> _declByKey = new();
        private static string Key(string doctor, int year, int monthIndex) => $"{doctor}|{year:D4}-{monthIndex:D2}";

        public ICommand SwitchToPreviousUnitCommand { get; set; }
        public ICommand SwitchToNextUnitCommand { get; set; }

        public MainViewModel()
        {
            SwitchToPreviousUnitCommand = new RelayCommand(SwitchToPreviousUnit);
            SwitchToNextUnitCommand = new RelayCommand(SwitchToNextUnit);
            UpdateRosterForSelectedMonth();
            this.PropertyChanged += OnMainViewModelPropertyChanged;

            WeakReferenceMessenger.Default.Register<SettingsHaveChangedMessage>(this);
        }

        public void Receive(SettingsHaveChangedMessage message)
        {
            App.MainRoot?.DispatcherQueue.TryEnqueue(async () =>
            {
                await UpdateFooterFromSettingsAsync();
            });
        }

        public void SetRepositories(IDoctorRepository? doctorRepo, IUnitRepository? unitRepo, IAssignmentRepository? assignmentRepo)
        {
            _doctorRepository = doctorRepo;
            _unitRepository = unitRepo;
            _assignmentRepository = assignmentRepo;
        }

        public void SetSettingsService(SettingsService settingsService)
        {
            _settingsService = settingsService;
        }

        public async Task LoadUserAndUnitDataAsync()
        {
            if (_doctorRepository == null || _unitRepository == null || _assignmentRepository == null || _settingsService == null) return;

            await UpdateFooterFromSettingsAsync();

            _allDoctors.Clear();
            _allAssignments.Clear();
            _allDoctors.AddRange(await _doctorRepository.GetAllAsync());
            _allAssignments.AddRange(await _assignmentRepository.GetAllAsync());

            var userProfile = await _doctorRepository.GetCurrentDoctorProfileAsync();
            if (userProfile == null) return;

            // ZMIANA: Ustawienie nazwy zalogowanego użytkownika
            CurrentUserName = $"Zalogowano jako: {userProfile.FullName}";

            IsCurrentUserAdmin = userProfile.IsAdmin;
            OnPropertyChanged(nameof(IsCurrentUserAdmin));
            _userUnits.Clear();
            if (IsCurrentUserAdmin)
            {
                var allUnits = await _unitRepository.GetAllAsync();
                allUnits.Sort();
                _userUnits.AddRange(allUnits);
            }
            else
            {
                var assignments = _allAssignments.Where(a => a.DoctorId == userProfile.Id).ToList();
                if (assignments.Any())
                {
                    var allUnits = await _unitRepository.GetAllAsync();
                    var assignedUnitIds = assignments.Select(a => a.UnitId).ToHashSet();
                    var filteredUnits = allUnits.Where(u => assignedUnitIds.Contains(u.Id)).ToList();
                    filteredUnits.Sort();
                    _userUnits.AddRange(filteredUnits);
                }
            }

            var settings = await _settingsService.LoadSettingsAsync();
            var lastUnitId = settings.LastActiveUnitId;

            int targetIndex = 0;
            if (lastUnitId.HasValue)
            {
                int foundIndex = _userUnits.FindIndex(u => u.Id == lastUnitId.Value);
                if (foundIndex != -1)
                {
                    targetIndex = foundIndex;
                }
            }
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
            if (ActiveUnit == null) return;

            var doctorIdsForUnit = _allAssignments.Where(a => a.UnitId == ActiveUnit.Id && a.IsActive).Select(a => a.DoctorId).ToHashSet();
            if (!doctorIdsForUnit.Any()) return;

            var doctorsForUnit = _allDoctors.Where(d => doctorIdsForUnit.Contains(d.Id) && !d.IsArchived).OrderBy(d => d.LastName).ThenBy(d => d.FirstName).ToList();
            var duplicateFullNames = doctorsForUnit.GroupBy(d => d.FullName).Where(g => g.Count() > 1).Select(g => g.Key).ToHashSet();
            foreach (var doctor in doctorsForUnit)
            {
                var key = Key(doctor.FullName, SelectedYear, SelectedMonthIndex);
                bool hasDecls = _declByKey.ContainsKey(key);
                bool needsDifferentiator = duplicateFullNames.Contains(doctor.FullName);
                string displayName = needsDifferentiator ? $"{doctor.LastName} {doctor.FirstName} ({doctor.Abbreviation})" : $"{doctor.LastName} {doctor.FirstName}";
                DoctorRows.Add(new DoctorRow(doctor, displayName, hasDecls));
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

        private async void OnMainViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(ActiveUnit))
            {
                await SaveCurrentUnitAsync();
                await UpdateFooterFromSettingsAsync();
            }
        }

        private async Task SaveCurrentUnitAsync()
        {
            if (_settingsService == null) return;
            var settings = await _settingsService.LoadSettingsAsync();
            var newSettings = settings with { LastActiveUnitId = ActiveUnit?.Id };
            await _settingsService.SaveSettingsAsync(newSettings);
        }

        public async Task UpdateFooterFromSettingsAsync()
        {
            if (_settingsService == null) return;
            var settings = await _settingsService.LoadSettingsAsync();

            EngineName = $"Silnik: {GetSolverDisplayName(settings.SelectedSolver)}";

            var activePriorities = settings.Priorities
                .Where(p => p.IsActive)
                .Select(p => GetPriorityDisplayName(p.Priority));

            PriorityOrder = $"Priorytety: {string.Join(" > ", activePriorities)}";
        }

        private string GetSolverDisplayName(SolverType solver) => solver switch
        {
            SolverType.Backtracking => "BacktrackingSolver",
            SolverType.AStar => "AStarSolver",
            SolverType.Genetic => "GeneticSolver",
            SolverType.SimulatedAnnealing => "SimulatedAnnealingSolver",
            SolverType.TabuSearch => "TabuSearchSolver",
            SolverType.AntColony => "AntColonySolver",
            _ => solver.ToString()
        };

        private string GetPriorityDisplayName(SolverPriority priority) => priority switch
        {
            SolverPriority.InitialContinuity => "Maksymalizacja ciągłości początkowej",
            SolverPriority.TotalAssignments => "Maksymalizacja obsady",
            SolverPriority.Fairness => "Sprawiedliwość obciążenia",
            SolverPriority.Spacing => "Równomierność w czasie",
            SolverPriority.DeclarationCompliance => "Zgodność z preferencjami",
            _ => "N/A"
        };

        private static string PolishDayOfWeek(DayOfWeek dow) => dow switch { DayOfWeek.Monday => "Poniedziałek", DayOfWeek.Tuesday => "Wtorek", DayOfWeek.Wednesday => "Środa", DayOfWeek.Thursday => "Czwartek", DayOfWeek.Friday => "Piątek", DayOfWeek.Saturday => "Sobota", DayOfWeek.Sunday => "Niedziela", _ => "" };
        public void PrevYear() => SelectedYear -= 1;
        public void NextYear() => SelectedYear += 1;
        public void PrevMonth() { if (SelectedMonthIndex == 0) { SelectedMonthIndex = 11; PrevYear(); } else SelectedMonthIndex -= 1; }
        public void NextMonth() { if (SelectedMonthIndex == 11) { SelectedMonthIndex = 0; NextYear(); } else SelectedMonthIndex += 1; }
        private void EnsureYearInList(int year) { if (!Years.Contains(year)) { int i = 0; while (i < Years.Count && Years[i] < year) i++; Years.Insert(i, year); } }
        public void ApplyDoctorMonth(DoctorMonthDeclaration dm) { _declByKey[Key(dm.Doctor, dm.Year, dm.MonthIndex)] = dm; var row = DoctorRows.FirstOrDefault(r => r.Name == dm.Doctor); if (row != null) row.HasDeclarations = true; OnPropertyChanged(nameof(_declByKey)); }
        public (bool has, DayMode mode, string? full, string? day, string? night) TryGetEntry(string doctor, int year, int monthIndex, int dayIndex) { if (_declByKey.TryGetValue(Key(doctor, year, monthIndex), out var dm) && dayIndex >= 0 && dayIndex < dm.Days.Length) { var d = dm.Days[dayIndex]; return (true, d.Mode, d.Full, d.Day, d.Night); } return (false, DayMode.Full24, null, null, null); }

        public async Task GenerateScheduleAsync()
        {
            if (_settingsService == null) return;
            var settings = await _settingsService.LoadSettingsAsync();
            var activePriorities = settings.Priorities.Where(p => p.IsActive).Select(p => p.Priority).ToList();
            if (!activePriorities.Any()) return;
            var scheduleInput = new ScheduleInput();
            try
            {
                var solver = ScheduleSolverFactory.Create(settings.SelectedSolver, scheduleInput, activePriorities);
                var solution = await Task.Run(() => solver.FindOptimalSolution());
            }
            catch (Exception ex) { /* TODO: Błąd */ }
        }
    }

    public class DoctorRow : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler? PropertyChanged;
        private void Raise(string n) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(n));
        public string Name { get; }
        public string DisplayName { get; }
        private bool _has;
        public bool HasDeclarations { get => _has; set { if (_has != value) { _has = value; Raise(nameof(HasDeclarations)); } } }
        public DoctorRow(DoctorProfile profile, string displayName, bool hasDeclarations)
        {
            Name = profile.FullName;
            DisplayName = displayName;
            _has = hasDeclarations;
        }
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