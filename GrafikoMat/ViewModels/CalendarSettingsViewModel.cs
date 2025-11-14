using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GrafikoMat.Core.Data;
using GrafikoMat.Core.Repositories;
using GrafikoMat.Models;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;

namespace GrafikoMat.ViewModels
{
    public partial class CalendarSettingsViewModel : ObservableObject
    {
        private readonly ISpecialDayRepository _specialDayRepository;
        private readonly IUnitRepository? _unitRepository;
        private bool _isDirty = false;
        private bool _isInitializing = false;
        private readonly List<Guid> _deletedCustomDayIds = new();

        // Rok dla ferii zimowych (combo: strzałki + dropdown)
        [ObservableProperty]
        private int _selectedWinterYear = DateTime.Now.Year;

        [ObservableProperty]
        private List<int> _availableWinterYears = new();

        // Ograniczenia dat dla ferii zimowych (styczeń-marzec)
        [ObservableProperty]
        private DateTimeOffset _winterMinDate;

        [ObservableProperty]
        private DateTimeOffset _winterMaxDate;

        // Jednostka (dropdown: Globalne + lista jednostek)
        [ObservableProperty]
        private UnitSelectionItem? _selectedUnit;

        [ObservableProperty]
        private ObservableCollection<UnitSelectionItem> _availableUnits = new();

        // FERIE ZIMOWE (zawsze widoczne)
        [ObservableProperty]
        private DateOnly? _winterHolidayStartDate;

        [ObservableProperty]
        private string _winterHolidayRangeText = "Ustaw dzień rozpoczęcia ferii";

        private Guid? _winterHolidayId; // ID w bazie (dla update)

        // INNE DNI SPECJALNE
        [ObservableProperty]
        private ObservableCollection<SpecialDayViewModel> _customSpecialDays = new();

        // Komendy
        public RelayCommand SaveCommand { get; }
        public RelayCommand PreviousWinterYearCommand { get; }
        public RelayCommand NextWinterYearCommand { get; }
        public AsyncRelayCommand AddCustomDayCommand { get; }
        public RelayCommand<SpecialDayViewModel> DeleteCustomDayCommand { get; }

        public CalendarSettingsViewModel(
            ISpecialDayRepository specialDayRepository,
            IUnitRepository? unitRepository = null)
        {
            _specialDayRepository = specialDayRepository ?? throw new ArgumentNullException(nameof(specialDayRepository));
            _unitRepository = unitRepository;

            SaveCommand = new RelayCommand(async () => await SaveAsync(), () => _isDirty);
            PreviousWinterYearCommand = new RelayCommand(PreviousWinterYear);
            NextWinterYearCommand = new RelayCommand(NextWinterYear);
            AddCustomDayCommand = new AsyncRelayCommand(AddCustomDayAsync);
            DeleteCustomDayCommand = new RelayCommand<SpecialDayViewModel>(DeleteCustomDay);

            InitializeWinterYears();
            UpdateWinterDateLimits();
        }

        /// <summary>
        /// Inicjalizuje listę dostępnych lat dla ferii zimowych (rok bieżący + 3 lata do przodu)
        /// </summary>
        private void InitializeWinterYears()
        {
            int currentYear = DateTime.Now.Year;
            AvailableWinterYears = Enumerable.Range(currentYear, 4).ToList();
        }

        /// <summary>
        /// Aktualizuje ograniczenia dat dla ferii zimowych (styczeń-marzec)
        /// </summary>
        private void UpdateWinterDateLimits()
        {
            WinterMinDate = new DateTimeOffset(new DateTime(SelectedWinterYear, 1, 1));
            WinterMaxDate = new DateTimeOffset(new DateTime(SelectedWinterYear, 3, 31));
        }

        /// <summary>
        /// Ładuje jednostki do dropdown
        /// </summary>
        public async Task LoadUnitsAsync()
        {
            _isInitializing = true;

            AvailableUnits.Clear();

            // Pierwsza pozycja - "Globalne (domyślne)"
            AvailableUnits.Add(new UnitSelectionItem
            {
                Id = null,
                DisplayName = "Globalne (domyślne)"
            });

            // Załaduj jednostki z bazy
            if (_unitRepository != null)
            {
                var units = await _unitRepository.GetAllAsync();
                foreach (var unit in units.OrderBy(u => u.HospitalFullName).ThenBy(u => u.Name))
                {
                    AvailableUnits.Add(new UnitSelectionItem
                    {
                        Id = unit.Id,
                        DisplayName = $"{unit.HospitalFullName} - {unit.Name}"
                    });
                }
            }

            // Domyślnie wybierz "Globalne"
            SelectedUnit = AvailableUnits.First();

            _isInitializing = false;
        }

        /// <summary>
        /// Ładuje dane dla wybranego roku i jednostki
        /// </summary>
        public async Task LoadDataAsync()
        {
            _isInitializing = true;

            try
            {
                var unitId = SelectedUnit?.Id;

                // Załaduj ferie zimowe
                var winterHolidays = await _specialDayRepository.GetSpecialDaysByTypeAsync(
                    SelectedWinterYear,
                    SpecialDayTypes.WinterHoliday,
                    unitId);

                // Filtruj dokładnie - globalne lub dla tej jednostki
                var winterHoliday = winterHolidays
                    .FirstOrDefault(wh => unitId.HasValue ? wh.UnitId == unitId : wh.UnitId == null);

                if (winterHoliday != null)
                {
                    _winterHolidayId = winterHoliday.Id;
                    WinterHolidayStartDate = winterHoliday.StartDate;
                }
                else
                {
                    _winterHolidayId = null;
                    WinterHolidayStartDate = null;
                }

                // Załaduj inne dni specjalne (z bieżącego roku + 3 lata do przodu)
                CustomSpecialDays.Clear();
                _deletedCustomDayIds.Clear(); // Wyczyść listę usuniętych przy ładowaniu
                int currentYear = DateTime.Now.Year;
                for (int year = currentYear; year <= currentYear + 3; year++)
                {
                    var allDays = await _specialDayRepository.GetSpecialDaysForYearAsync(year, unitId);

                    var customDays = allDays
                        .Where(sd => sd.Type == SpecialDayTypes.Custom)
                        .Where(sd => unitId.HasValue ? sd.UnitId == unitId : sd.UnitId == null)
                        .Select(sd => new SpecialDayViewModel(sd));

                    foreach (var day in customDays)
                    {
                        CustomSpecialDays.Add(day);
                    }
                }
            }
            finally
            {
                _isInitializing = false;
                _isDirty = false;
                SaveCommand.NotifyCanExecuteChanged();
            }
        }

        partial void OnSelectedWinterYearChanged(int value)
        {
            if (!_isInitializing)
            {
                UpdateWinterDateLimits();
                _ = Task.Run(async () =>
                {
                    try
                    {
                        await LoadDataAsync();
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"[CalendarSettingsViewModel] OnSelectedWinterYearChanged -> LoadDataAsync failed: {ex.Message}");
                    }
                });
            }
        }

        partial void OnSelectedUnitChanged(UnitSelectionItem? value)
        {
            if (!_isInitializing)
            {
                _ = Task.Run(async () =>
                {
                    try
                    {
                        await LoadDataAsync();
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"[CalendarSettingsViewModel] OnSelectedUnitChanged -> LoadDataAsync failed: {ex.Message}");
                    }
                });
            }
        }

        partial void OnWinterHolidayStartDateChanged(DateOnly? value)
        {
            UpdateWinterHolidayRangeText();
            MarkAsDirty();
        }

        /// <summary>
        /// Aktualizuje tekst wyświetlający zakres ferii
        /// </summary>
        private void UpdateWinterHolidayRangeText()
        {
            if (WinterHolidayStartDate.HasValue)
            {
                var startDate = WinterHolidayStartDate.Value;
                var endDate = startDate.AddDays(14); // Sobota + 14 dni = niedziela 2 tygodnie później

                WinterHolidayRangeText = $"Ferie zimowe: {startDate:dd.MM.yyyy} (sobota) - {endDate:dd.MM.yyyy} (niedziela)";
            }
            else
            {
                WinterHolidayRangeText = "Ustaw dzień rozpoczęcia ferii";
            }
        }

        private void MarkAsDirty()
        {
            if (_isInitializing) return;
            _isDirty = true;
            SaveCommand.NotifyCanExecuteChanged();
        }

        private void PreviousWinterYear()
        {
            int currentYear = DateTime.Now.Year;
            if (SelectedWinterYear > AvailableWinterYears.Min() && SelectedWinterYear > currentYear)
            {
                SelectedWinterYear--;
            }
            else if (SelectedWinterYear > currentYear)
            {
                // Jeśli poza zakresem ale większy niż bieżący - przesuń cały zakres
                int newYear = SelectedWinterYear - 1;
                AvailableWinterYears = Enumerable.Range(newYear, 4).ToList();
                SelectedWinterYear = newYear;
            }
            // Nie rób nic jeśli jesteśmy na bieżącym roku (nie cofamy się dalej)
        }

        private void NextWinterYear()
        {
            if (SelectedWinterYear < AvailableWinterYears.Max())
            {
                SelectedWinterYear++;
            }
            else
            {
                // Jeśli poza zakresem - przesuń cały zakres
                int newYear = SelectedWinterYear + 1;
                AvailableWinterYears = Enumerable.Range(newYear, 4).ToList();
                SelectedWinterYear = newYear;
            }
        }

        private async Task SaveAsync()
        {
            try
            {
                var unitId = SelectedUnit?.Id;

                // Zapisz ferie zimowe
                if (WinterHolidayStartDate.HasValue)
                {
                    var startDate = WinterHolidayStartDate.Value;
                    var endDate = startDate.AddDays(14); // Sobota + 14 dni = niedziela 2 tygodnie później

                    var winterHoliday = new SpecialDay
                    {
                        Id = _winterHolidayId ?? Guid.NewGuid(),
                        Type = SpecialDayTypes.WinterHoliday,
                        Name = "Ferie zimowe",
                        FullName = $"Ferie zimowe {SelectedWinterYear}",
                        StartDate = startDate,
                        EndDate = endDate,
                        Year = SelectedWinterYear,
                        UnitId = unitId,
                        IsSchoolBreak = true
                    };

                    if (_winterHolidayId.HasValue)
                    {
                        await _specialDayRepository.UpdateSpecialDayAsync(winterHoliday);
                    }
                    else
                    {
                        var created = await _specialDayRepository.CreateSpecialDayAsync(winterHoliday);
                        _winterHolidayId = created.Id;
                    }
                }

                // Usuń usunięte dni specjalne
                foreach (var deletedId in _deletedCustomDayIds)
                {
                    await _specialDayRepository.DeleteSpecialDayAsync(deletedId);
                }
                _deletedCustomDayIds.Clear();

                // Zapisz inne dni specjalne
                foreach (var customDay in CustomSpecialDays)
                {
                    var specialDay = customDay.ToSpecialDay();

                    if (customDay.IsNew)
                    {
                        await _specialDayRepository.CreateSpecialDayAsync(specialDay);
                        customDay.IsNew = false; // Już nie jest nowy po zapisaniu
                    }
                    else
                    {
                        await _specialDayRepository.UpdateSpecialDayAsync(specialDay);
                    }
                }

                _isDirty = false;
                SaveCommand.NotifyCanExecuteChanged();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[CalendarSettings] Error saving: {ex.Message}");
                throw;
            }
        }

        // Event do otwierania dialogu - implementacja w code-behind widoku
        public event Func<Task>? RequestAddCustomDay;

        private async Task AddCustomDayAsync()
        {
            if (RequestAddCustomDay != null)
            {
                await RequestAddCustomDay.Invoke();
            }
        }

        /// <summary>
        /// Dodaje nowy dzień specjalny do listy (wywoływane z code-behind po wypełnieniu dialogu)
        /// </summary>
        public void AddCustomDayToList(string name, string fullName, DateOnly startDate, DateOnly endDate)
        {
            var newDay = new SpecialDayViewModel(new SpecialDay
            {
                Id = Guid.NewGuid(),
                Type = SpecialDayTypes.Custom,
                Name = name,
                FullName = fullName,
                StartDate = startDate,
                EndDate = endDate,
                Year = startDate.Year,
                UnitId = SelectedUnit?.Id,
                IsSchoolBreak = false
            })
            {
                IsNew = true // Oznacz jako nowy dzień
            };

            CustomSpecialDays.Add(newDay);
            MarkAsDirty();
        }

        private void DeleteCustomDay(SpecialDayViewModel? day)
        {
            if (day != null)
            {
                // Jeśli nie jest nowy (czyli pochodzi z bazy), dodaj do listy usuniętych
                if (!day.IsNew)
                {
                    _deletedCustomDayIds.Add(day.Id);
                }

                CustomSpecialDays.Remove(day);
                MarkAsDirty();
            }
        }
    }

    /// <summary>
    /// Item dla dropdown wyboru jednostki
    /// </summary>
    public class UnitSelectionItem
    {
        public Guid? Id { get; set; }
        public string DisplayName { get; set; } = string.Empty;
    }

    /// <summary>
    /// ViewModel dla pojedynczego niestandardowego dnia specjalnego
    /// </summary>
    public class SpecialDayViewModel : ObservableObject
    {
        public Guid Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string FullName { get; set; } = string.Empty;
        public DateOnly StartDate { get; set; }
        public DateOnly EndDate { get; set; }
        public int Year { get; set; }
        public Guid? UnitId { get; set; }
        public bool IsNew { get; set; } = false;

        public SpecialDayViewModel(SpecialDay specialDay)
        {
            Id = specialDay.Id;
            Name = specialDay.Name;
            FullName = specialDay.FullName;
            StartDate = specialDay.StartDate;
            EndDate = specialDay.EndDate;
            Year = specialDay.Year;
            UnitId = specialDay.UnitId;
            IsNew = false; // Załadowane z bazy
        }

        public string DateRangeDisplay =>
            StartDate == EndDate
                ? StartDate.ToString("dd.MM.yyyy")
                : $"{StartDate:dd.MM} - {EndDate:dd.MM.yyyy}";

        /// <summary>
        /// Konwertuje ViewModel z powrotem do modelu SpecialDay
        /// </summary>
        public SpecialDay ToSpecialDay()
        {
            return new SpecialDay
            {
                Id = Id,
                Type = SpecialDayTypes.Custom,
                Name = Name,
                FullName = FullName,
                StartDate = StartDate,
                EndDate = EndDate,
                Year = Year,
                UnitId = UnitId,
                IsSchoolBreak = false
            };
        }
    }
}
