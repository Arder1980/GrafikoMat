using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using CommunityToolkit.WinUI;
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
    public class MainViewModel : ObservableObject, IRecipient<SettingsHaveChangedMessage>, IRecipient<UnitDataChangedMessage>
    {
        private ScheduleSolution? _lastGeneratedSolution = null;

        protected bool SetProperty<T>(ref T backingStore, T value, [CallerMemberName] string propertyName = "")
        {
            if (EqualityComparer<T>.Default.Equals(backingStore, value))
            {
                return false;
            }

            backingStore = value;
            OnPropertyChanged(propertyName);
            return true;
        }

        private IDoctorRepository? _doctorRepository;
        private IUnitRepository? _unitRepository;
        private IAssignmentRepository? _assignmentRepository;
        private SettingsService? _settingsService;
        private IUxActionOrchestrator? _orchestrator;

        private readonly Guid _viewId = Guid.NewGuid();

        private readonly List<DoctorProfile> _allDoctors = new();
        private readonly List<UnitDoctorAssignment> _allAssignments = new();
        private readonly List<Unit> _userUnits = new();
        private int _activeUnitIndex = -1;

        public bool IsCurrentUserAdmin { get; private set; }

        private bool _isUnitContextActive = true;
        public bool IsUnitContextActive { get => _isUnitContextActive; set => SetProperty(ref _isUnitContextActive, value); }

        private string _currentViewTitle = string.Empty;
        public string CurrentViewTitle
        {
            get => _currentViewTitle;
            set => SetProperty(ref _currentViewTitle, value);
        }

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
        public Dictionary<string, DoctorMonthDeclaration> Declarations => _declByKey;

        private static string Key(string doctor, int year, int monthIndex) => $"{doctor}|{year:D4}-{monthIndex:D2}";

        public ICommand SwitchToPreviousUnitCommand { get; set; }
        public ICommand SwitchToNextUnitCommand { get; set; }

        public int TotalUnitsCount => _userUnits.Count;
        public int CurrentUnitIndex => _activeUnitIndex;

        public MainViewModel()
        {
            SwitchToPreviousUnitCommand = new RelayCommand(SwitchToPreviousUnit);
            SwitchToNextUnitCommand = new RelayCommand(SwitchToNextUnit);
            UpdateRosterForSelectedMonth(); // Inicjalizuje pustą listę RosterRows
            WeakReferenceMessenger.Default.Register<SettingsHaveChangedMessage>(this);
            WeakReferenceMessenger.Default.Register<UnitDataChangedMessage>(this);
        }

        public void Receive(SettingsHaveChangedMessage message)
        {
            if (App.MainRoot?.DispatcherQueue != null)
            {
                _ = App.MainRoot.DispatcherQueue.EnqueueAsync(UpdateFooterFromSettingsAsync);
            }
        }

        public void Receive(UnitDataChangedMessage message)
        {
            // POPRAWKA: async void jest niebezpieczne - zamiast tego używamy fire-and-forget z Task.Run
            _ = Task.Run(async () =>
            {
                try
                {
                    await LoadUserAndUnitDataAsync();

                    // Przełącz na UI thread dla LoadDataForActiveUnit
                    if (App.MainRoot?.DispatcherQueue != null)
                    {
                        await App.MainRoot.DispatcherQueue.EnqueueAsync(() =>
                        {
                            LoadDataForActiveUnit();
                            return Task.CompletedTask;
                        });
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[MainViewModel] Receive(UnitDataChangedMessage) failed: {ex.Message}");
                    // W przypadku błędu - loguj ale nie crashuj aplikacji
                }
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

        public void SetOrchestrator(IUxActionOrchestrator orchestrator)
        {
            _orchestrator = orchestrator;
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
            OnPropertyChanged(nameof(TotalUnitsCount));
            OnPropertyChanged(nameof(CurrentUnitIndex));
        }

        private void SwitchToNextUnit()
        {
            if (_userUnits.Count == 0) return;
            _activeUnitIndex = (_activeUnitIndex + 1) % _userUnits.Count;
            OnPropertyChanged(nameof(ActiveUnit));
            OnPropertyChanged(nameof(ActiveUnitHospitalName));
            OnPropertyChanged(nameof(ActiveUnitDepartmentName));
            OnPropertyChanged(nameof(CurrentUnitIndex));
            LoadDataForActiveUnit();
        }

        private void SwitchToPreviousUnit()
        {
            if (_userUnits.Count == 0) return;
            _activeUnitIndex = (_activeUnitIndex - 1 + _userUnits.Count) % _userUnits.Count;
            OnPropertyChanged(nameof(ActiveUnit));
            OnPropertyChanged(nameof(ActiveUnitHospitalName));
            OnPropertyChanged(nameof(ActiveUnitDepartmentName));
            OnPropertyChanged(nameof(CurrentUnitIndex));
            LoadDataForActiveUnit();
        }

        public int GetUnitIndexById(Guid unitId)
        {
            for (int i = 0; i < _userUnits.Count; i++)
            {
                if (_userUnits[i].Id == unitId)
                {
                    return i;
                }
            }
            return -1; // Nie znaleziono jednostki
        }

        public void LoadDataForActiveUnit()
        {
            DoctorRows.Clear();
            if (ActiveUnit == null)
            {
                // Jeśli nie ma aktywnej jednostki, wyczyść też wynik grafiku
                _lastGeneratedSolution = null;
                UpdateRosterForSelectedMonth();
                return;
            }

            var doctorIdsForUnit = _allAssignments.Where(a => a.UnitId == ActiveUnit.Id && a.IsActive).Select(a => a.DoctorId).ToHashSet();
            if (!doctorIdsForUnit.Any())
            {
                // Jeśli nie ma lekarzy, wyczyść też wynik grafiku
                _lastGeneratedSolution = null;
                UpdateRosterForSelectedMonth();
                return;
            }

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
            // Po załadowaniu lekarzy odśwież widok grafiku (może użyć _lastGeneratedSolution)
            UpdateRosterForSelectedMonth();
        }

        private void UpdateRosterForSelectedMonth()
        {
            RosterRows.Clear();
            int daysInMonth = DateTime.DaysInMonth(SelectedYear, SelectedMonthIndex + 1);
            bool allowTele = ActiveUnit?.AllowTeleradiologyFallback ?? false; // Pobierz ustawienie dla aktywnej jednostki

            for (int day = 1; day <= daysInMonth; day++)
            {
                var date = new DateTime(SelectedYear, SelectedMonthIndex + 1, day);
                string dateLabel = $"{date:dd.MM} ({PolishDayOfWeek(date.DayOfWeek)})";

                bool isDayOff = date.DayOfWeek == DayOfWeek.Saturday ||
                                date.DayOfWeek == DayOfWeek.Sunday ||
                                PolishHolidays.IsPublicHoliday(date);
                bool isLast = (day == daysInMonth);

                string dutyLabel = "—"; // Domyślnie brak obsady

                // Sprawdź, czy mamy wygenerowany grafik dla tego miesiąca
                if (_lastGeneratedSolution != null)
                {
                    if (_lastGeneratedSolution.Assignments.TryGetValue(date, out var doctor) && doctor != null)
                    {
                        dutyLabel = doctor.Abbreviation; // Przypisany lekarz
                    }
                    else // Brak przypisanego lekarza (null w solution.Assignments)
                    {
                        // Zastosuj logikę teleradiologii
                        dutyLabel = allowTele ? "TELE" : "—"; // Użyj "TELE" jeśli dozwolone, inaczej "—"
                    }
                }

                RosterRows.Add(new RosterRow(dateLabel, dutyLabel, isDayOff, isLast));
            }
        }


        public void RefreshDeclarationsForDashboard()
        {
            if (ActiveUnit == null) return;
            foreach (var docRow in DoctorRows)
            {
                var key = Key(docRow.Profile.FullName, SelectedYear, SelectedMonthIndex);
                docRow.HasDeclarations = _declByKey.ContainsKey(key);
            }
        }

        public async Task SaveCurrentUnitAsync()
        {
            if (_settingsService == null) return;
            var settings = await _settingsService.LoadSettingsAsync();
            var newSettings = settings with { LastActiveUnitId = ActiveUnit?.Id };
            await _settingsService.SaveSettingsAsync(newSettings);
        }

        public async Task UpdateFooterFromSettingsAsync()
        {
            if (_settingsService == null) return;
            var settings = await _settingsService.LoadSettingsAsync(forceReload: true);
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
            SolverPriority.InitialContinuity => "Ciągłość początkowa",
            SolverPriority.TotalAssignments => "Maksymalizacja obsady",
            SolverPriority.Fairness => "Sprawiedliwość obciążenia",
            SolverPriority.Spacing => "Równomierność rozłożenia",
            SolverPriority.DeclarationCompliance => "Zgodność z deklaracjami",
            _ => priority.ToString()
        };

        private static string PolishDayOfWeek(DayOfWeek dow) => new[] { "Niedziela", "Poniedziałek", "Wtorek", "Środa", "Czwartek", "Piątek", "Sobota" }[(int)dow];

        public void PrevYear() => SelectedYear -= 1;
        public void NextYear() => SelectedYear += 1;
        public void PrevMonth() { if (SelectedMonthIndex == 0) { SelectedMonthIndex = 11; PrevYear(); } else SelectedMonthIndex -= 1; }
        public void NextMonth() { if (SelectedMonthIndex == 11) { SelectedMonthIndex = 0; NextYear(); } else SelectedMonthIndex += 1; }

        private void EnsureYearInList(int year) { if (!Years.Contains(year)) { int i = 0; while (i < Years.Count && Years[i] < year) i++; Years.Insert(i, year); } }

        public void ApplyDoctorMonth(DoctorMonthDeclaration dm) { _declByKey[Key(dm.Doctor, dm.Year, dm.MonthIndex)] = dm; var row = DoctorRows.FirstOrDefault(r => r.Profile.FullName == dm.Doctor); if (row != null) row.HasDeclarations = true; OnPropertyChanged(nameof(Declarations)); }

        public (bool has, DayMode mode, string? full, string? day, string? night) TryGetEntry(string doctor, int year, int monthIndex, int dayIndex) { if (_declByKey.TryGetValue(Key(doctor, year, monthIndex), out var dm) && dayIndex >= 0 && dayIndex < dm.Days.Length) { var d = dm.Days[dayIndex]; return (true, d.Mode, d.Full, d.Day, d.Night); } return (false, DayMode.Full24, null, null, null); }

        public async Task GenerateScheduleAsync()
        {
            if (_settingsService == null || ActiveUnit == null || _orchestrator == null)
            {
                // TODO: Pokaż błąd - brak jednostki, serwisu ustawień lub orkiestratora
                return;
            }

            var settings = await _settingsService.LoadSettingsAsync();
            var activePriorities = settings.Priorities.Where(p => p.IsActive).Select(p => p.Priority).ToList();
            if (!activePriorities.Any())
            {
                // TODO: Pokaż błąd - brak priorytetów
                return;
            }

            // --- Przygotowanie ScheduleInput ---
            var doctorsForSchedule = DoctorRows.Select(dr => dr.Profile).ToList();
            if (!doctorsForSchedule.Any())
            {
                // TODO: Pokaż błąd - brak lekarzy dla jednostki
                return;
            }

            // Przykład budowania Availability (na podstawie danych z _declByKey - DO ROZWINIĘCIA)
            var availability = new Dictionary<DateTime, Dictionary<string, AvailabilityType>>();
            int daysInMonth = DateTime.DaysInMonth(SelectedYear, SelectedMonthIndex + 1);
            for (int d = 1; d <= daysInMonth; d++)
            {
                var date = new DateTime(SelectedYear, SelectedMonthIndex + 1, d);
                var dayAvailability = new Dictionary<string, AvailabilityType>();
                foreach (var doctor in doctorsForSchedule)
                {
                    // Tutaj trzeba by wczytać prawdziwą deklarację z _declByKey i zmapować na AvailabilityType
                    // Na razie przykład: co drugi lekarz jest dostępny
                    dayAvailability[doctor.Abbreviation] = (doctorsForSchedule.IndexOf(doctor) % 2 == 0) ? AvailabilityType.Available : AvailabilityType.Unavailable;
                }
                availability[date] = dayAvailability;
            }

            var scheduleInput = new ScheduleInput
            {
                Doctors = doctorsForSchedule,
                Availability = availability,
                DutyLimits = doctorsForSchedule.ToDictionary(dr => dr.Abbreviation, dr => 10) // Przykładowy limit, TODO: Wczytać prawdziwe limity
            };
            // --- Koniec przygotowania ScheduleInput ---

            var solverParams = new SolverParameters
            {
                SolverType = settings.SelectedSolver,
                TimeoutMinutes = settings.TimeoutMinutes,
                CoolingRate = settings.CoolingRate,
                GeneticPopulationSize = settings.GeneticPopulationSize,
                GeneticGenerations = settings.GeneticGenerations,
                AntColonyAnts = settings.AntColonyAnts,
                AntColonyGenerations = settings.AntColonyGenerations,
                TabuListSize = settings.TabuListSize,
                TabuMaxIterations = settings.TabuMaxIterations
            };

            // Wykryj czy solver jest deterministyczny (nie ma sensownego progressu 0-100%)
            bool isDeterministicSolver = SolverMessages.IsDeterministicSolver(settings.SelectedSolver);

            // Wygeneruj nazwę silnika do wyświetlenia
            var engineName = settings.SelectedSolver switch
            {
                SolverType.Backtracking => "BacktrackingSolver (algorytm z nawrotami)",
                SolverType.AStar => "AStarSolver (algorytm A*)",
                SolverType.Genetic => "GeneticSolver (algorytm genetyczny)",
                SolverType.SimulatedAnnealing => "SimulatedAnnealingSolver (algorytm symulowanego wyżarzania)",
                SolverType.TabuSearch => "TabuSearchSolver (algorytm przeszukiwania z tabu)",
                SolverType.AntColony => "AntColonySolver (algorytm kolonii mrówek)",
                _ => settings.SelectedSolver.ToString()
            };

            // Użyj orkiestratora do długotrwałej operacji z progressem
            bool success = await _orchestrator.PerformLongRunningTaskAsync(
                viewId: _viewId,
                title: "Generowanie grafiku",
                engineName: engineName,
                operationAsync: async (progress, cancellationToken) =>
                {
                    // Walidacja nastąpi w ScheduleSolverFactory.Create() - rzuci ArgumentException
                    var solver = ScheduleSolverFactory.Create(scheduleInput, solverParams, activePriorities, progress: new Progress<double>(p =>
                    {
                        // Pobierz odpowiedni komunikat dla tego solvera i postępu
                        string statusText = SolverMessages.GetStatusMessage(settings.SelectedSolver, p);
                        progress.Report((p, statusText));
                    }), token: cancellationToken);

                    // POPRAWKA: FindOptimalSolution() już obsługuje CancellationToken wewnętrznie
                    // Task.Run nie jest potrzebny - solver sam zarządza wątkami
                    _lastGeneratedSolution = await Task.Run(() =>
                    {
                        try
                        {
                            return solver.FindOptimalSolution();
                        }
                        catch (OperationCanceledException)
                        {
                            // Token został anulowany w trakcie wykonywania solvera
                            throw;
                        }
                    }, cancellationToken);
                },
                successMessage: "Grafik został wygenerowany pomyślnie.",
                errorMessageTitle: "Błąd generowania grafiku",
                isCancellable: true,
                showIndeterminateProgress: isDeterministicSolver  // ← ProgressRing dla Backtracking/A*, ProgressBar dla metaheurystyk
            );

            // Odśwież widok tabeli z wynikiem (RosterRows)
            if (success)
            {
                UpdateRosterForSelectedMonth();
            }
        }
    }

    public class DoctorRow : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler? PropertyChanged;
        private void Raise(string n) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(n));
        public DoctorProfile Profile { get; }
        public string DisplayName { get; }
        private bool _has;
        public bool HasDeclarations { get => _has; set { if (_has != value) { _has = value; Raise(nameof(HasDeclarations)); } } }

        public DoctorRow(DoctorProfile profile, string displayName, bool hasDeclarations)
        {
            Profile = profile;
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
            DateLabel = dateLabel; DutyLabel = dutyLabel; IsDayOff = isDayOff; IsLast = isLast;
        }
    }
}