using CommunityToolkit.Mvvm.Input;
using GrafikoMat.Common;
using GrafikoMat.Core.Data;
using GrafikoMat.Core.Repositories;
using GrafikoMat.Core.Scheduling;
using GrafikoMat.Models;
using SlotPart = GrafikoMat.Core.Enums.SlotPart;
using CoDutyStatus = GrafikoMat.Core.Enums.CoDutyStatus;
using DayMode = GrafikoMat.Core.Enums.DayMode;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;

namespace GrafikoMat.ViewModels
{
    public class DoctorDeclarationViewModel
    {
        public DoctorProfile Profile { get; }
        public string DisplayName { get; }

        public DoctorDeclarationViewModel(DoctorProfile profile, string displayName)
        {
            Profile = profile;
            DisplayName = displayName;
        }
    }

    public sealed partial class DeclarationsViewModel : ObservableObject, IDisposable
    {
        private readonly IDictionaryLike<string, DoctorMonthDeclaration> _sharedDeclarations;
        private readonly Action _onSaveCallback;

        /// <summary>
        /// Event wywoływany po zapisaniu deklaracji do bazy.
        /// </summary>
        public event EventHandler? DeclarationsSaved;
        private bool _use12hShiftsByDefault;
        private MonthLayout _monthLayout;
        private bool _isDisposed;
        private bool _isDirty = false;
        private bool _isLoading = false; // Flaga zapobiegająca oznaczaniu jako dirty podczas ładowania

        // ✅ DODANE - repozytorium i ID jednostki
        private readonly IDeclarationRepository? _declarationRepository;
        private readonly ISpecialDayRepository? _specialDayRepository;
        private readonly Guid? _currentUnitId;

        public int Year { get; }
        public int MonthIndex { get; }
        public string MonthHeader => $"Deklaracje dyżurowe na {PolishMonth(MonthIndex + 1)} {Year}";

        public ObservableCollection<DoctorDeclarationViewModel> Doctors { get; } = new();
        public ObservableCollection<DayCell> DayCells { get; } = new();

        public HashSet<SelectedSlot> SelectedSlots { get; } = new();

        public bool CanSwitchDoctors { get; }

        // ✅ DODANE - właściwości sprawdzające czy są lekarze
        public bool HasNoDoctors => Doctors.Count == 0;
        public bool HasDoctors => Doctors.Count > 0;

        private int _selectedDoctorIndex;
        public int SelectedDoctorIndex
        {
            get => _selectedDoctorIndex;
            set
            {
                if (_selectedDoctorIndex == value) return;
                CommitChangesToSharedState();
                SetProperty(ref _selectedDoctorIndex, value);
                OnPropertyChanged(nameof(SelectedDoctor));
                LoadDeclarationsForSelectedDoctor();
            }
        }

        public DoctorProfile? SelectedDoctor =>
            (_selectedDoctorIndex >= 0 && _selectedDoctorIndex < Doctors.Count) ?
            Doctors[_selectedDoctorIndex].Profile : null;

        private int _currentUnitIndex;
        public int CurrentUnitIndex
        {
            get => _currentUnitIndex;
            set => SetProperty(ref _currentUnitIndex, value);
        }

        public RelayCommand SaveCommand { get; }
        public AsyncRelayCommand SaveAsyncCommand { get; }
        public RelayCommand ClearSelectionCommand { get; }
        public AsyncRelayCommand ClearDeclarationsCommand { get; }
        public RelayCommand SelectNextDoctorCommand { get; }
        public RelayCommand SelectPrevDoctorCommand { get; }

        public DeclarationsViewModel(
            int year, int monthIndex, List<DoctorProfile> doctors, int initialDoctorIndex,
            IDictionaryLike<string, DoctorMonthDeclaration> sharedDeclarations, bool isAdmin,
            bool use12hShifts, Action onSaveCallback, IDeclarationRepository? declarationRepository = null,
            Guid? unitId = null, ISpecialDayRepository? specialDayRepository = null)
        {
            // Walidacja parametrów
            if (doctors == null || doctors.Count == 0)
                throw new ArgumentException("Lista lekarzy nie może być pusta", nameof(doctors));

            if (year < 2000 || year > 2100)
                throw new ArgumentOutOfRangeException(nameof(year), "Rok musi być w zakresie 2000-2100");

            if (monthIndex < 0 || monthIndex > 11)
                throw new ArgumentOutOfRangeException(nameof(monthIndex), "Indeks miesiąca musi być w zakresie 0-11");

            _sharedDeclarations = sharedDeclarations ?? throw new ArgumentNullException(nameof(sharedDeclarations));
            _onSaveCallback = onSaveCallback ?? throw new ArgumentNullException(nameof(onSaveCallback));

            Year = year;
            MonthIndex = monthIndex;
            CanSwitchDoctors = isAdmin;
            _use12hShiftsByDefault = use12hShifts;
            _currentUnitIndex = 0;
            _declarationRepository = declarationRepository;
            _specialDayRepository = specialDayRepository;
            _currentUnitId = unitId;

            // Załaduj dni specjalne asynchronicznie
            _ = LoadSpecialDaysAsync();

            _monthLayout = new MonthLayout(year, monthIndex + 1, use12hShifts);

            var duplicateFullNames = doctors
                .GroupBy(d => d.FullName)
                .Where(g => g.Count() > 1)
                .Select(g => g.Key)
                .ToHashSet();

            foreach (var doc in doctors)
            {
                string displayName = duplicateFullNames.Contains(doc.FullName)
                    ? $"{doc.FullName} ({doc.Abbreviation})"
                    : doc.FullName;
                Doctors.Add(new DoctorDeclarationViewModel(doc, displayName));
            }

            _selectedDoctorIndex = (Doctors.Count > 0) ?
                Math.Clamp(initialDoctorIndex, 0, Doctors.Count - 1) : -1;

            BuildCalendarShell();
            LoadDeclarationsForSelectedDoctor();

            SaveCommand = new RelayCommand(() => _ = DoSaveAsync(), () => _isDirty);
            SaveAsyncCommand = new AsyncRelayCommand(DoSaveAsync, () => _isDirty);
            ClearSelectionCommand = new RelayCommand(ClearSelection);
            ClearDeclarationsCommand = new AsyncRelayCommand(ClearAllDeclarationsAsync);
            SelectNextDoctorCommand = new RelayCommand(SelectNextDoctor, () => CanSwitchDoctors && Doctors.Count > 1);
            SelectPrevDoctorCommand = new RelayCommand(SelectPrevDoctor, () => CanSwitchDoctors && Doctors.Count > 1);

            // Subskrybuj zmiany w DayCells (symbol changes będą oznaczać dirty)
            SubscribeToCellChanges();

            // ✅ DODANE - powiadomienie o HasNoDoctors i HasDoctors
            OnPropertyChanged(nameof(HasNoDoctors));
            OnPropertyChanged(nameof(HasDoctors));
        }

        private void BuildCalendarShell()
        {
            var oldCells = DayCells.ToList();

            DayCells.Clear();

            var firstDay = new DateTime(Year, MonthIndex + 1, 1);
            int offset = ((int)firstDay.DayOfWeek + 6) % 7;
            int daysInMonth = DateTime.DaysInMonth(Year, MonthIndex + 1);
            int weeks = (int)Math.Ceiling((offset + daysInMonth) / 7.0);
            var startDate = firstDay.AddDays(-offset);

            System.Diagnostics.Debug.WriteLine($"[BUILD] BuildCalendarShell START");
            System.Diagnostics.Debug.WriteLine($"[BUILD] _use12hShiftsByDefault = {_use12hShiftsByDefault}");
            System.Diagnostics.Debug.WriteLine($"[BUILD] _monthLayout Year={_monthLayout.Year}, Month={_monthLayout.Month}, UseTwelveHourByDefault={_monthLayout.UseTwelveHourByDefault}");

            for (int i = 0; i < weeks * 7; i++)
            {
                var date = startDate.AddDays(i);

                var cell = new DayCell(i, date, date.Month == MonthIndex + 1,
                    date.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday,
                    PolishHolidays.IsPublicHoliday(date),
                    PolishHolidays.GetHolidayName(date, _currentUnitId));

                if (cell.InMonth)
                {
                    var dateOnly = new DateOnly(date.Year, date.Month, date.Day);
                    cell.IsSplit = _monthLayout.IsSplit(dateOnly);

                    System.Diagnostics.Debug.WriteLine($"[BUILD] Day {date.Day}: IsSplit = {cell.IsSplit}");

                    var oldCell = oldCells.FirstOrDefault(c => c.Date.Date == date.Date);
                    if (oldCell != null)
                    {
                        cell.SymbolFull = oldCell.SymbolFull;
                        cell.SymbolDay = oldCell.SymbolDay;
                        cell.SymbolNight = oldCell.SymbolNight;
                    }
                }

                DayCells.Add(cell);
            }

            System.Diagnostics.Debug.WriteLine($"[BUILD] BuildCalendarShell END - added {DayCells.Count} cells");
        }

        private void SubscribeToCellChanges()
        {
            foreach (var cell in DayCells)
            {
                cell.PropertyChanged += OnCellPropertyChanged;
            }
        }

        private void UnsubscribeFromCellChanges()
        {
            foreach (var cell in DayCells)
            {
                cell.PropertyChanged -= OnCellPropertyChanged;
            }
        }

        private void OnCellPropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (_isLoading || _isDisposed) return;

            // Tylko zmiany w symbolach oznaczają dirty
            if (e.PropertyName == nameof(DayCell.SymbolFull) ||
                e.PropertyName == nameof(DayCell.SymbolDay) ||
                e.PropertyName == nameof(DayCell.SymbolNight))
            {
                MarkAsDirty();
            }
        }

        private void MarkAsDirty()
        {
            if (_isLoading) return;
            _isDirty = true;
            SaveCommand.NotifyCanExecuteChanged();
            OnPropertyChanged(nameof(HasUnsavedChanges));
        }

        public void LoadDeclarationsForSelectedDoctor()
        {
            _isLoading = true; // Wyłącz dirty tracking podczas ładowania
            ClearSelection();

            System.Diagnostics.Debug.WriteLine($"[LOAD] LoadDeclarationsForSelectedDoctor START");

            foreach (var cell in DayCells.Where(c => c.InMonth))
            {
                var dateOnly = new DateOnly(cell.Date.Year, cell.Date.Month, cell.Date.Day);
                cell.IsSplit = _monthLayout.IsSplit(dateOnly);
                cell.ClearData();
            }

            if (SelectedDoctor == null)
            {
                System.Diagnostics.Debug.WriteLine($"[LOAD] No selected doctor");
                return;
            }

            System.Diagnostics.Debug.WriteLine($"[LOAD] Selected doctor: {SelectedDoctor.FullName}");

            var key = Key(SelectedDoctor.Id, Year, MonthIndex);
            if (_sharedDeclarations.TryGetValue(key, out var decl))
            {
                System.Diagnostics.Debug.WriteLine($"[LOAD] Found declarations for {key}");

                foreach (var cell in DayCells.Where(c => c.InMonth))
                {
                    int dayIdx = cell.Date.Day - 1;
                    if (dayIdx >= 0 && dayIdx < decl.Days.Length)
                    {
                        var d = decl.Days[dayIdx];

                        bool hasSavedData = !string.IsNullOrWhiteSpace(d.Full) ||
                                           !string.IsNullOrWhiteSpace(d.Day) ||
                                           !string.IsNullOrWhiteSpace(d.Night);

                        if (hasSavedData)
                        {
                            cell.IsSplit = d.Mode == DayMode.Split12;
                            System.Diagnostics.Debug.WriteLine($"[LOAD] Day {cell.Date.Day}: Has saved data, Mode={d.Mode}, IsSplit={cell.IsSplit}");
                        }
                        else
                        {
                            System.Diagnostics.Debug.WriteLine($"[LOAD] Day {cell.Date.Day}: Empty, keeping default IsSplit={cell.IsSplit}");
                        }

                        cell.SymbolFull = d.Full ?? "";
                        cell.SymbolDay = d.Day ?? "";
                        cell.SymbolNight = d.Night ?? "";

                        // WSPÓŁDYŻURNI: Wypełnij informacje o partnerze
                        if (d.CoDutyPartnerId.HasValue)
                        {
                            var partner = Doctors.FirstOrDefault(doc => doc.Profile.Id == d.CoDutyPartnerId.Value);
                            string partnerDisplayName = partner != null ? $"z {partner.DisplayName}" : "z ?";
                            string statusGlyph = d.CoDutyStatus.HasValue && (int)d.CoDutyStatus.Value == (int)CoDutyStatus.Accepted ? "👥" : "⏳";

                            // Użyj CoDutySlotPart aby określić który slot ustawić
                            SlotPart slotPart = d.CoDutySlotPart.HasValue ? (SlotPart)(int)d.CoDutySlotPart.Value : SlotPart.Full;

                            if (slotPart == SlotPart.Full || d.Mode == DayMode.Full24)
                            {
                                cell.CoDutyPartnerFull = partnerDisplayName;
                                cell.CoDutyStatusGlyphFull = statusGlyph;
                            }
                            else if (slotPart == SlotPart.Day)
                            {
                                cell.CoDutyPartnerDay = partnerDisplayName;
                                cell.CoDutyStatusGlyphDay = statusGlyph;
                            }
                            else if (slotPart == SlotPart.Night)
                            {
                                cell.CoDutyPartnerNight = partnerDisplayName;
                                cell.CoDutyStatusGlyphNight = statusGlyph;
                            }
                        }
                        else
                        {
                            // Wyczyść pola współdyżurnych
                            cell.CoDutyPartnerFull = "";
                            cell.CoDutyStatusGlyphFull = "";
                            cell.CoDutyPartnerDay = "";
                            cell.CoDutyStatusGlyphDay = "";
                            cell.CoDutyPartnerNight = "";
                            cell.CoDutyStatusGlyphNight = "";
                        }
                    }
                }
            }
            else
            {
                System.Diagnostics.Debug.WriteLine($"[LOAD] No declarations found for {key}");
            }

            System.Diagnostics.Debug.WriteLine($"[LOAD] LoadDeclarationsForSelectedDoctor END");
            _isLoading = false; // Włącz dirty tracking po zakończeniu ładowania
        }

        public void CommitChangesToSharedState()
        {
            if (SelectedDoctor == null) return;
            var key = Key(SelectedDoctor.Id, Year, MonthIndex);
            int daysInMonth = DateTime.DaysInMonth(Year, MonthIndex + 1);

            // Pobierz istniejącą deklarację lub utwórz nową
            if (!_sharedDeclarations.TryGetValue(key, out var existingDeclaration))
            {
                existingDeclaration = new DoctorMonthDeclaration
                {
                    DoctorId = SelectedDoctor.Id,
                    Doctor = SelectedDoctor.FullName,
                    Year = Year,
                    MonthIndex = MonthIndex,
                    Days = Enumerable.Range(0, daysInMonth).Select(_ => new DayDeclaration()).ToArray()
                };
            }

            // Aktualizuj tylko symbole dyżurów (zachowaj pola co-duty)
            foreach (var cell in DayCells.Where(c => c.InMonth))
            {
                int dayIdx = cell.Date.Day - 1;
                if (dayIdx < 0 || dayIdx >= existingDeclaration.Days.Length) continue;

                // Zachowaj pola co-duty z istniejącej deklaracji
                var existingCoDutyPartnerId = existingDeclaration.Days[dayIdx].CoDutyPartnerId;
                var existingCoDutyStatus = existingDeclaration.Days[dayIdx].CoDutyStatus;
                var existingCoDutyInitiatorId = existingDeclaration.Days[dayIdx].CoDutyInitiatorId;
                var existingCoDutySlotPart = existingDeclaration.Days[dayIdx].CoDutySlotPart;

                if (!cell.IsSplit)
                {
                    existingDeclaration.Days[dayIdx].Mode = DayMode.Full24;
                    existingDeclaration.Days[dayIdx].Full = string.IsNullOrWhiteSpace(cell.SymbolFull) ? null : cell.SymbolFull;
                }
                else
                {
                    existingDeclaration.Days[dayIdx].Mode = DayMode.Split12;
                    existingDeclaration.Days[dayIdx].Day = string.IsNullOrWhiteSpace(cell.SymbolDay) ? null : cell.SymbolDay;
                    existingDeclaration.Days[dayIdx].Night = string.IsNullOrWhiteSpace(cell.SymbolNight) ? null : cell.SymbolNight;
                }

                // Przywróć pola co-duty
                existingDeclaration.Days[dayIdx].CoDutyPartnerId = existingCoDutyPartnerId;
                existingDeclaration.Days[dayIdx].CoDutyStatus = existingCoDutyStatus;
                existingDeclaration.Days[dayIdx].CoDutyInitiatorId = existingCoDutyInitiatorId;
                existingDeclaration.Days[dayIdx].CoDutySlotPart = existingCoDutySlotPart;

                if (existingCoDutyPartnerId.HasValue)
                {
                    System.Diagnostics.Debug.WriteLine($"[COMMIT] Dzień {dayIdx + 1}: Przywrócono CoDuty - partnerId={existingCoDutyPartnerId}, status={existingCoDutyStatus}, slotPart={existingCoDutySlotPart}");
                }
            }

            _sharedDeclarations[key] = existingDeclaration;
        }

        private async Task DoSaveAsync()
        {
            System.Diagnostics.Debug.WriteLine($"[SAVE] DoSaveAsync called");
            CommitChangesToSharedState();

            // ✅ DODANE - zapis do Supabase jeśli repozytorium jest dostępne
            System.Diagnostics.Debug.WriteLine($"[SAVE] _declarationRepository={_declarationRepository != null}, _currentUnitId={_currentUnitId}, SelectedDoctor={SelectedDoctor?.FullName}");

            if (_declarationRepository != null && _currentUnitId.HasValue && SelectedDoctor != null)
            {
                System.Diagnostics.Debug.WriteLine($"[SAVE] Attempting to save to Supabase...");
                try
                {
                    await SaveToSupabaseAsync();
                    System.Diagnostics.Debug.WriteLine($"[SAVE] Save to Supabase completed successfully");
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[SAVE] ERROR during save to Supabase: {ex.Message}");
                    System.Diagnostics.Debug.WriteLine($"[SAVE] Stack trace: {ex.StackTrace}");
                    if (ex.InnerException != null)
                    {
                        System.Diagnostics.Debug.WriteLine($"[SAVE] Inner exception: {ex.InnerException.Message}");
                    }
                }
            }
            else
            {
                System.Diagnostics.Debug.WriteLine($"[SAVE] Skipping Supabase save - repository or unit not available");
            }

            _onSaveCallback?.Invoke();
            _isDirty = false;
            SaveCommand.NotifyCanExecuteChanged();
            SaveAsyncCommand.NotifyCanExecuteChanged();
            OnPropertyChanged(nameof(HasUnsavedChanges));

            // Wywołaj event po zapisie - DeclarationsView użyje tego do wysłania powiadomień
            DeclarationsSaved?.Invoke(this, EventArgs.Empty);
        }

        /// <summary>
        /// Czyści wszystkie deklaracje dla aktualnie wybranego lekarza i usuwa je z Supabase.
        /// </summary>
        private async Task ClearAllDeclarationsAsync()
        {
            if (SelectedDoctor == null)
                return;

            System.Diagnostics.Debug.WriteLine($"[CLEAR] Czyszczenie deklaracji dla {SelectedDoctor.FullName}");

            // Wyczyść wszystkie symbole w komórkach
            foreach (var cell in DayCells)
            {
                cell.ClearData();
            }

            // Zaktualizuj shared state
            CommitChangesToSharedState();

            // Usuń z Supabase jeśli repozytorium jest dostępne
            if (_declarationRepository != null && _currentUnitId.HasValue)
            {
                try
                {
                    // Znajdź ID deklaracji dla tego lekarza
                    var existingDeclaration = await _declarationRepository.GetDeclarationForDoctorAsync(
                        _currentUnitId.Value,
                        SelectedDoctor.Id,
                        Year,
                        MonthIndex + 1
                    );

                    if (existingDeclaration != null)
                    {
                        System.Diagnostics.Debug.WriteLine($"[CLEAR] Usuwanie deklaracji ID={existingDeclaration.Id} z Supabase");
                        await _declarationRepository.DeleteDeclarationAsync(existingDeclaration.Id);
                        System.Diagnostics.Debug.WriteLine($"[CLEAR] Deklaracja usunięta pomyślnie");
                    }
                    else
                    {
                        System.Diagnostics.Debug.WriteLine($"[CLEAR] Brak deklaracji w bazie do usunięcia");
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[CLEAR] ERROR podczas usuwania z Supabase: {ex.Message}");
                }
            }

            _onSaveCallback?.Invoke();
            _isDirty = false;
            SaveCommand.NotifyCanExecuteChanged();
            SaveAsyncCommand.NotifyCanExecuteChanged();
            OnPropertyChanged(nameof(HasUnsavedChanges));
        }

        /// <summary>
        /// Ładuje wszystkie deklaracje dla jednostki z Supabase i synchronizuje ze stanem lokalnym.
        /// </summary>
        public async Task LoadDeclarationsFromSupabaseAsync()
        {
            System.Diagnostics.Debug.WriteLine($"[LOAD-SUPABASE] LoadDeclarationsFromSupabaseAsync called");

            if (_declarationRepository == null || !_currentUnitId.HasValue)
            {
                System.Diagnostics.Debug.WriteLine($"[LOAD-SUPABASE] Brak repozytorium lub unitId - pomijam ładowanie z Supabase");
                System.Diagnostics.Debug.WriteLine($"[LOAD-SUPABASE] _declarationRepository={_declarationRepository != null}, _currentUnitId={_currentUnitId}");
                return;
            }

            try
            {
                System.Diagnostics.Debug.WriteLine($"[LOAD-SUPABASE] Ładowanie deklaracji z Supabase dla jednostki {_currentUnitId.Value}, {Year}-{MonthIndex + 1}");

                var declarations = await _declarationRepository.GetDeclarationsForUnitMonthAsync(
                    _currentUnitId.Value,
                    Year,
                    MonthIndex + 1  // W bazie miesiące są 1-12
                );

                System.Diagnostics.Debug.WriteLine($"[LOAD-SUPABASE] Pobrano {declarations.Count} deklaracji z Supabase");

                // Konwertuj deklaracje z bazy do lokalnego formatu
                foreach (var declaration in declarations)
                {
                    System.Diagnostics.Debug.WriteLine($"[LOAD-SUPABASE] Przetwarzanie deklaracji dla doctor_id={declaration.DoctorId}");

                    var doctor = Doctors.FirstOrDefault(d => d.Profile.Id == declaration.DoctorId);
                    if (doctor == null)
                    {
                        System.Diagnostics.Debug.WriteLine($"[LOAD-SUPABASE] Pomijam deklarację - nie znaleziono lekarza ID={declaration.DoctorId}");
                        continue;
                    }

                    var key = Key(doctor.Profile.Id, Year, MonthIndex);
                    int daysInMonth = DateTime.DaysInMonth(Year, MonthIndex + 1);

                    // Ustaw DoctorId w deklaracji
                    declaration.DoctorId = doctor.Profile.Id;
                    var doctorDeclaration = new DoctorMonthDeclaration
                    {
                        DoctorId = doctor.Profile.Id,
                        Doctor = doctor.Profile.FullName,
                        Year = Year,
                        MonthIndex = MonthIndex,
                        Days = Enumerable.Range(0, daysInMonth).Select(_ => new DayDeclaration()).ToArray()
                    };

                    // Konwertuj dane JSON do DayDeclaration
                    if (declaration.DeclarationDataJson?.Days != null)
                    {
                        System.Diagnostics.Debug.WriteLine($"[LOAD-SUPABASE] Konwersja {declaration.DeclarationDataJson.Days.Count} dni dla {doctor.Profile.FullName}");
                        foreach (var dayDto in declaration.DeclarationDataJson.Days)
                        {
                            int dayIndex = dayDto.Day - 1;
                            if (dayIndex >= 0 && dayIndex < doctorDeclaration.Days.Length)
                            {
                                doctorDeclaration.Days[dayIndex] = new DayDeclaration
                                {
                                    Mode = dayDto.Mode == DayMode.Split12 ? DayMode.Split12 : DayMode.Full24,
                                    Full = dayDto.Full,
                                    Day = dayDto.DaySlot,
                                    Night = dayDto.Night,
                                    // Współdyżurni
                                    CoDutyPartnerId = dayDto.CoDutyPartnerId,
                                    CoDutyStatus = dayDto.CoDutyStatus.HasValue ? (GrafikoMat.Models.CoDutyStatus)(int)dayDto.CoDutyStatus.Value : null,
                                    CoDutyInitiatorId = dayDto.CoDutyInitiatorId,
                                    CoDutySlotPart = dayDto.CoDutySlotPart.HasValue ? (GrafikoMat.Models.SlotPart)(int)dayDto.CoDutySlotPart.Value : null
                                };
                                System.Diagnostics.Debug.WriteLine($"[LOAD-SUPABASE] Dzień {dayDto.Day}: mode={dayDto.Mode}, full={dayDto.Full}, day={dayDto.DaySlot}, night={dayDto.Night}");
                            }
                        }
                    }
                    else
                    {
                        System.Diagnostics.Debug.WriteLine($"[LOAD-SUPABASE] BRAK danych JSON dla {doctor.Profile.FullName}");
                    }

                    _sharedDeclarations[key] = doctorDeclaration;
                    System.Diagnostics.Debug.WriteLine($"[LOAD-SUPABASE] Załadowano deklarację dla {doctor.Profile.FullName}, key={key}");
                }

                // Odśwież widok dla aktualnie wybranego lekarza
                System.Diagnostics.Debug.WriteLine($"[LOAD-SUPABASE] Odświeżanie widoku dla wybranego lekarza");
                LoadDeclarationsForSelectedDoctor();
                System.Diagnostics.Debug.WriteLine($"[LOAD-SUPABASE] Zakończono ładowanie z Supabase");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[LOAD-SUPABASE] BŁĄD podczas ładowania deklaracji z Supabase: {ex.Message}");
                System.Diagnostics.Debug.WriteLine($"[LOAD-SUPABASE] Stack trace: {ex.StackTrace}");
            }
        }

        /// <summary>
        /// Zapisuje deklaracje aktualnie wybranego lekarza do Supabase.
        /// </summary>
        private async Task SaveToSupabaseAsync()
        {
            if (_declarationRepository == null || !_currentUnitId.HasValue || SelectedDoctor == null)
                return;

            System.Diagnostics.Debug.WriteLine($"[SAVE] Rozpoczynam zapis deklaracji do Supabase dla {SelectedDoctor.FullName}");

            // Pobierz istniejącą deklarację z bazy (jeśli istnieje)
            var existingDeclaration = await _declarationRepository.GetDeclarationForDoctorAsync(
                _currentUnitId.Value,
                SelectedDoctor.Id,
                Year,
                MonthIndex + 1  // W bazie miesiące są 1-12, a nie 0-11
            );

            int daysInMonth = DateTime.DaysInMonth(Year, MonthIndex + 1);
            var days = new List<DayDeclarationDto>();

            // Pobierz lokalną deklarację z _sharedDeclarations (zawiera pola co-duty)
            var key = Key(SelectedDoctor.Id, Year, MonthIndex);
            _sharedDeclarations.TryGetValue(key, out var localDeclaration);

            // Konwertuj dane z DayCells do formatu JSON
            foreach (var cell in DayCells.Where(c => c.InMonth))
            {
                int dayNumber = cell.Date.Day;

                var dayDto = new DayDeclarationDto
                {
                    Day = dayNumber,
                    Mode = cell.IsSplit ? DayMode.Split12 : DayMode.Full24
                };

                if (cell.IsSplit)
                {
                    dayDto.DaySlot = string.IsNullOrWhiteSpace(cell.SymbolDay) ? null : cell.SymbolDay;
                    dayDto.Night = string.IsNullOrWhiteSpace(cell.SymbolNight) ? null : cell.SymbolNight;
                }
                else
                {
                    dayDto.Full = string.IsNullOrWhiteSpace(cell.SymbolFull) ? null : cell.SymbolFull;
                }

                // Skopiuj pola współdyżurnego z lokalnej deklaracji
                if (localDeclaration != null)
                {
                    int dayIndex = dayNumber - 1;
                    if (dayIndex >= 0 && dayIndex < localDeclaration.Days.Length)
                    {
                        dayDto.CoDutyPartnerId = localDeclaration.Days[dayIndex].CoDutyPartnerId;
                        dayDto.CoDutyStatus = localDeclaration.Days[dayIndex].CoDutyStatus.HasValue
                            ? (CoDutyStatus)(int)localDeclaration.Days[dayIndex].CoDutyStatus.Value
                            : null;
                        dayDto.CoDutyInitiatorId = localDeclaration.Days[dayIndex].CoDutyInitiatorId;
                        dayDto.CoDutySlotPart = localDeclaration.Days[dayIndex].CoDutySlotPart.HasValue
                            ? (SlotPart)(int)localDeclaration.Days[dayIndex].CoDutySlotPart.Value
                            : null;

                        if (dayDto.CoDutyPartnerId.HasValue)
                        {
                            System.Diagnostics.Debug.WriteLine($"[SAVE] Dzień {dayNumber}: CoDuty - partnerId={dayDto.CoDutyPartnerId}, status={dayDto.CoDutyStatus}, slotPart={dayDto.CoDutySlotPart}");
                        }
                    }
                }

                days.Add(dayDto);
            }

            var declarationData = new DeclarationDataJson
            {
                Days = days
            };

            Declaration declaration;

            if (existingDeclaration != null)
            {
                // Aktualizuj istniejącą deklarację
                declaration = existingDeclaration;
                declaration.DeclarationDataJson = declarationData;
                System.Diagnostics.Debug.WriteLine($"[SAVE] Aktualizacja istniejącej deklaracji ID={declaration.Id}");
            }
            else
            {
                // Utwórz nową deklarację
                declaration = new Declaration
                {
                    UnitId = _currentUnitId.Value,
                    DoctorId = SelectedDoctor.Id,
                    Year = Year,
                    Month = MonthIndex + 1,  // W bazie miesiące są 1-12
                    DeclarationDataJson = declarationData
                };
                System.Diagnostics.Debug.WriteLine($"[SAVE] Tworzenie nowej deklaracji");
            }

            await _declarationRepository.SaveDeclarationAsync(declaration);
            System.Diagnostics.Debug.WriteLine($"[SAVE] Deklaracja zapisana pomyślnie");
        }

        /// <summary>
        /// Zwraca listę dni gdzie użytkownik jest inicjatorem współdyżuru z statusem "pending".
        /// Format: (day, partnerId, slotPart)
        /// </summary>
        public List<(int Day, Guid PartnerId, string SlotPart)> GetPendingCoDutyNotificationsToSend()
        {
            var result = new List<(int Day, Guid PartnerId, string SlotPart)>();

            if (SelectedDoctor == null)
                return result;

            var key = Key(SelectedDoctor.Id, Year, MonthIndex);
            if (!_sharedDeclarations.TryGetValue(key, out var declaration))
                return result;

            for (int i = 0; i < declaration.Days.Length; i++)
            {
                var day = declaration.Days[i];

                // Sprawdź czy jestem inicjatorem i status to "pending"
                if (day.CoDutyInitiatorId == SelectedDoctor.Id &&
                    day.CoDutyStatus.HasValue && (int)day.CoDutyStatus.Value == (int)GrafikoMat.Models.CoDutyStatus.Pending &&
                    day.CoDutyPartnerId.HasValue)
                {
                    int dayNumber = i + 1;

                    // Użyj zapisanego CoDutySlotPart lub domyślnie "full"
                    var slotPart = day.CoDutySlotPart.HasValue
                        ? day.CoDutySlotPart.Value.ToString().ToLowerInvariant()
                        : "full";

                    result.Add((dayNumber, day.CoDutyPartnerId.Value, slotPart));

                    System.Diagnostics.Debug.WriteLine($"[GetPendingNotifications] Dzień {dayNumber}: partner={day.CoDutyPartnerId.Value}, slotPart={slotPart}");
                }
            }

            System.Diagnostics.Debug.WriteLine($"[GetPendingNotifications] Znaleziono {result.Count} powiadomień do wysłania");
            return result;
        }

        public void SelectSingleSlot(int index, SlotPart part)
        {
            System.Diagnostics.Debug.WriteLine($"[SELECT] SelectSingleSlot: index={index}, part={part}");

            SelectedSlots.Clear();
            SelectedSlots.Add(new SelectedSlot(index, part));

            System.Diagnostics.Debug.WriteLine($"[SELECT] Added to SelectedSlots: {part}");

            UpdateSelectionVisuals();
        }

        public void ToggleSlotSelection(int index, SlotPart part, bool isMultiSelect)
        {
            System.Diagnostics.Debug.WriteLine($"[SELECT] ToggleSlotSelection: index={index}, part={part}, isMultiSelect={isMultiSelect}");

            var slot = new SelectedSlot(index, part);

            if (isMultiSelect)
            {
                if (SelectedSlots.Contains(slot))
                {
                    System.Diagnostics.Debug.WriteLine($"[SELECT] Removing slot from selection");
                    SelectedSlots.Remove(slot);
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine($"[SELECT] Adding slot to selection");
                    SelectedSlots.Add(slot);
                }
            }
            else
            {
                System.Diagnostics.Debug.WriteLine($"[SELECT] Replacing selection with single slot");
                SelectedSlots.Clear();
                SelectedSlots.Add(slot);
            }

            System.Diagnostics.Debug.WriteLine($"[SELECT] Total selected slots: {SelectedSlots.Count}");
            UpdateSelectionVisuals();
        }

        public void SelectRangeOfSlots(int fromIndex, int toIndex)
        {
            SelectedSlots.Clear();
            int start = Math.Min(fromIndex, toIndex);
            int end = Math.Max(fromIndex, toIndex);
            for (int i = start; i <= end; i++)
            {
                if (i >= 0 && i < DayCells.Count && DayCells[i].InMonth)
                {
                    if (DayCells[i].IsSplit)
                    {
                        SelectedSlots.Add(new SelectedSlot(i, SlotPart.Day));
                        SelectedSlots.Add(new SelectedSlot(i, SlotPart.Night));
                    }
                    else
                    {
                        SelectedSlots.Add(new SelectedSlot(i, SlotPart.Full));
                    }
                }
            }
            UpdateSelectionVisuals();
        }

        public void SelectDragRange(int startIndex, int endIndex, SlotPart startPart, SlotPart endPart)
        {
            SelectedSlots.Clear();
            int start = Math.Min(startIndex, endIndex);
            int end = Math.Max(startIndex, endIndex);

            bool expandToFullDays = (startPart != endPart) &&
                                     (startPart == SlotPart.Day || startPart == SlotPart.Night) &&
                                     (endPart == SlotPart.Day || endPart == SlotPart.Night);

            // Jeśli oba Day lub oba Night, zaznaczaj tylko ten sam typ
            bool selectSameTypeOnly = (startPart == endPart) &&
                                       (startPart == SlotPart.Day || startPart == SlotPart.Night);

            for (int i = start; i <= end; i++)
            {
                if (i < 0 || i >= DayCells.Count) continue;
                var cell = DayCells[i];
                if (!cell.InMonth) continue;

                if (expandToFullDays && cell.IsSplit)
                {
                    SelectedSlots.Add(new SelectedSlot(i, SlotPart.Day));
                    SelectedSlots.Add(new SelectedSlot(i, SlotPart.Night));
                }
                else if (i == startIndex && i == endIndex)
                {
                    SelectedSlots.Add(new SelectedSlot(i, startPart));
                }
                else if (i == startIndex)
                {
                    SelectedSlots.Add(new SelectedSlot(i, startPart));
                }
                else if (i == endIndex)
                {
                    SelectedSlots.Add(new SelectedSlot(i, endPart));
                }
                else
                {
                    // Środkowe komórki
                    if (selectSameTypeOnly && cell.IsSplit)
                    {
                        // Zaznaczaj tylko Day lub tylko Night (ten sam typ co start/end)
                        SelectedSlots.Add(new SelectedSlot(i, startPart));
                    }
                    else if (cell.IsSplit)
                    {
                        // Mieszane typy (Day->Night lub odwrotnie) - zaznacz oba
                        SelectedSlots.Add(new SelectedSlot(i, SlotPart.Day));
                        SelectedSlots.Add(new SelectedSlot(i, SlotPart.Night));
                    }
                    else
                    {
                        SelectedSlots.Add(new SelectedSlot(i, SlotPart.Full));
                    }
                }
            }

            UpdateSelectionVisuals();
        }

        public void ClearSelection()
        {
            SelectedSlots.Clear();
            UpdateSelectionVisuals();
        }

        public void UpdateSelectionVisuals()
        {
            if (_isDisposed) return;

            System.Diagnostics.Debug.WriteLine($"[SELECT] UpdateSelectionVisuals START, SelectedSlots.Count = {SelectedSlots.Count}");

            foreach (var cell in DayCells)
            {
                var selectedParts = new HashSet<SlotPart>();

                foreach (var sel in SelectedSlots.Where(s => s.Index == cell.Index))
                {
                    selectedParts.Add(sel.Part);
                    System.Diagnostics.Debug.WriteLine($"[SELECT] Cell {cell.Index} Day {cell.Date.Day}: Adding part {sel.Part}");
                }

                cell.UpdateSelection(selectedParts);

                if (selectedParts.Count > 0)
                {
                    System.Diagnostics.Debug.WriteLine($"[SELECT] Cell {cell.Index} Day {cell.Date.Day}: IsFullSelected={cell.IsFullSelected}, IsDaySelected={cell.IsDaySelected}, IsNightSelected={cell.IsNightSelected}");
                }
            }
        }

        private void SelectNextDoctor()
        {
            if (!CanSwitchDoctors || Doctors.Count <= 1) return;
            SelectedDoctorIndex = (SelectedDoctorIndex + 1) % Doctors.Count;
        }

        private void SelectPrevDoctor()
        {
            if (!CanSwitchDoctors || Doctors.Count <= 1) return;
            SelectedDoctorIndex = (SelectedDoctorIndex - 1 + Doctors.Count) % Doctors.Count;
        }

        public bool HasDeclarationInSlot(int cellIndex, SlotPart slotPart)
        {
            if (cellIndex < 0 || cellIndex >= DayCells.Count)
                return false;

            var cell = DayCells[cellIndex];
            if (!cell.InMonth)
                return false;

            return slotPart switch
            {
                SlotPart.Full => !string.IsNullOrWhiteSpace(cell.SymbolFull),
                SlotPart.Day => !string.IsNullOrWhiteSpace(cell.SymbolDay),
                SlotPart.Night => !string.IsNullOrWhiteSpace(cell.SymbolNight),
                _ => false
            };
        }

        public void ReloadForNewUnit(List<DoctorProfile> doctors, int initialDoctorIndex, bool use12hShifts, int newUnitIndex = 0)
        {
            _use12hShiftsByDefault = use12hShifts;
            _monthLayout = new MonthLayout(Year, MonthIndex + 1, use12hShifts);
            _currentUnitIndex = newUnitIndex;

            CommitChangesToSharedState();

            Doctors.Clear();
            var duplicateFullNames = doctors
                .GroupBy(d => d.FullName)
                .Where(g => g.Count() > 1)
                .Select(g => g.Key)
                .ToHashSet();

            foreach (var doc in doctors)
            {
                string displayName = duplicateFullNames.Contains(doc.FullName)
                    ? $"{doc.FullName} ({doc.Abbreviation})"
                    : doc.FullName;
                Doctors.Add(new DoctorDeclarationViewModel(doc, displayName));
            }

            _selectedDoctorIndex = (Doctors.Count > 0) ?
                Math.Clamp(initialDoctorIndex, 0, Doctors.Count - 1) : -1;

            DayCells.Clear();

            BuildCalendarShell();
            LoadDeclarationsForSelectedDoctor();

            OnPropertyChanged(nameof(Doctors));
            OnPropertyChanged(nameof(SelectedDoctor));
            OnPropertyChanged(nameof(SelectedDoctorIndex));
            OnPropertyChanged(nameof(DayCells));

            // ✅ DODANE - powiadomienie o HasNoDoctors i HasDoctors
            OnPropertyChanged(nameof(HasNoDoctors));
            OnPropertyChanged(nameof(HasDoctors));
        }

        private static string PolishMonth(int month) => new[] { "", "Styczeń", "Luty", "Marzec", "Kwiecień", "Maj", "Czerwiec", "Lipiec", "Sierpień", "Wrzesień", "Październik", "Listopad", "Grudzień" }[month];
        private static string Key(Guid doctorId, int year, int monthIndex) => $"{doctorId}|{year:D4}-{monthIndex:D2}";
    }

    public sealed class DayCell : ObservableObject
    {
        public int Index { get; }
        public DateTime Date { get; }
        public bool InMonth { get; }
        public bool IsInteractive => InMonth;
        public string DayNumber => Date.Day.ToString("00");
        public bool IsWeekend { get; }
        public bool IsHoliday { get; }
        public bool IsDayOff => IsHoliday || IsWeekend;
        public string? HolidayName { get; }
        public Visibility HolidayVisibility => string.IsNullOrEmpty(HolidayName) ? Visibility.Collapsed : Visibility.Visible;

        private Brush _effectiveBackground = new SolidColorBrush();
        public Brush EffectiveBackground
        {
            get => _effectiveBackground;
            set => SetProperty(ref _effectiveBackground, value);
        }

        private Brush _effectiveBorderBrush = new SolidColorBrush();
        public Brush EffectiveBorderBrush
        {
            get => _effectiveBorderBrush;
            set => SetProperty(ref _effectiveBorderBrush, value);
        }

        private Brush _dayNumberForeground = new SolidColorBrush();
        public Brush DayNumberForeground
        {
            get => _dayNumberForeground;
            set => SetProperty(ref _dayNumberForeground, value);
        }

        private Brush _effectiveHeaderBackground = new SolidColorBrush();
        public Brush EffectiveHeaderBackground
        {
            get => _effectiveHeaderBackground;
            set => SetProperty(ref _effectiveHeaderBackground, value);
        }

        private bool _isSplit;
        public bool IsSplit { get => _isSplit; set => SetProperty(ref _isSplit, value); }

        private string _symbolFull = "";
        public string SymbolFull
        {
            get => _symbolFull;
            set
            {
                if (SetProperty(ref _symbolFull, value))
                {
                    OnPropertyChanged(nameof(DisplaySymbolFull));
                }
            }
        }

        private string _symbolDay = "";
        public string SymbolDay
        {
            get => _symbolDay;
            set
            {
                if (SetProperty(ref _symbolDay, value))
                {
                    OnPropertyChanged(nameof(DisplaySymbolDay));
                }
            }
        }

        private string _symbolNight = "";
        public string SymbolNight
        {
            get => _symbolNight;
            set
            {
                if (SetProperty(ref _symbolNight, value))
                {
                    OnPropertyChanged(nameof(DisplaySymbolNight));
                }
            }
        }

        // Computed properties dla pełnych nazw deklaracji
        public string DisplaySymbolFull => ConvertCodeToDisplayName(_symbolFull);
        public string DisplaySymbolDay => ConvertCodeToDisplayName(_symbolDay);
        public string DisplaySymbolNight => ConvertCodeToDisplayName(_symbolNight);

        private static string ConvertCodeToDisplayName(string code)
        {
            return code switch
            {
                "MOG" => "Mogę",
                "CHC" => "Chcę",
                "WAR" => "Warunkowo",
                "REZ" => "Rezerwacja",
                "DYZ" => "Inny dyżur",
                "URL" => "Urlop",
                "---" => "Nie mogę",
                _ => code // Dla pustego stringa lub nieznanych kodów
            };
        }

        private bool _isFullSelected;
        public bool IsFullSelected { get => _isFullSelected; private set => SetProperty(ref _isFullSelected, value); }

        private bool _isDaySelected;
        public bool IsDaySelected { get => _isDaySelected; private set => SetProperty(ref _isDaySelected, value); }

        private bool _isNightSelected;
        public bool IsNightSelected { get => _isNightSelected; private set => SetProperty(ref _isNightSelected, value); }

        private Thickness _daySelectionBorderThickness = new Thickness(0);
        public Thickness DaySelectionBorderThickness { get => _daySelectionBorderThickness; private set => SetProperty(ref _daySelectionBorderThickness, value); }

        private Thickness _nightSelectionBorderThickness = new Thickness(0);
        public Thickness NightSelectionBorderThickness { get => _nightSelectionBorderThickness; private set => SetProperty(ref _nightSelectionBorderThickness, value); }

        // ============================================================================
        // Pola dla współdyżurnych
        // ============================================================================

        private string _coDutyPartnerFull = "";
        public string CoDutyPartnerFull
        {
            get => _coDutyPartnerFull;
            set
            {
                if (SetProperty(ref _coDutyPartnerFull, value))
                {
                    OnPropertyChanged(nameof(HasCoDutyFull));
                }
            }
        }

        private string _coDutyPartnerDay = "";
        public string CoDutyPartnerDay
        {
            get => _coDutyPartnerDay;
            set
            {
                if (SetProperty(ref _coDutyPartnerDay, value))
                {
                    OnPropertyChanged(nameof(HasCoDutyDay));
                }
            }
        }

        private string _coDutyPartnerNight = "";
        public string CoDutyPartnerNight
        {
            get => _coDutyPartnerNight;
            set
            {
                if (SetProperty(ref _coDutyPartnerNight, value))
                {
                    OnPropertyChanged(nameof(HasCoDutyNight));
                }
            }
        }

        // Computed properties - czy jest współdyżurny
        public bool HasCoDutyFull => !string.IsNullOrEmpty(_coDutyPartnerFull);
        public bool HasCoDutyDay => !string.IsNullOrEmpty(_coDutyPartnerDay);
        public bool HasCoDutyNight => !string.IsNullOrEmpty(_coDutyPartnerNight);

        private string _coDutyStatusGlyphFull = "";
        public string CoDutyStatusGlyphFull
        {
            get => _coDutyStatusGlyphFull;
            set
            {
                if (SetProperty(ref _coDutyStatusGlyphFull, value))
                {
                    OnPropertyChanged(nameof(CoDutyStatusTooltipFull));
                }
            }
        }

        private string _coDutyStatusGlyphDay = "";
        public string CoDutyStatusGlyphDay
        {
            get => _coDutyStatusGlyphDay;
            set
            {
                if (SetProperty(ref _coDutyStatusGlyphDay, value))
                {
                    OnPropertyChanged(nameof(CoDutyStatusTooltipDay));
                }
            }
        }

        private string _coDutyStatusGlyphNight = "";
        public string CoDutyStatusGlyphNight
        {
            get => _coDutyStatusGlyphNight;
            set
            {
                if (SetProperty(ref _coDutyStatusGlyphNight, value))
                {
                    OnPropertyChanged(nameof(CoDutyStatusTooltipNight));
                }
            }
        }

        // Computed properties - tooltipy dla statusu współdyżurnego
        public string CoDutyStatusTooltipFull => _coDutyStatusGlyphFull == "👥" ? "Zaakceptowane przez współdyżurnego" : "Oczekiwanie na akceptację współdyżurnego";
        public string CoDutyStatusTooltipDay => _coDutyStatusGlyphDay == "👥" ? "Zaakceptowane przez współdyżurnego" : "Oczekiwanie na akceptację współdyżurnego";
        public string CoDutyStatusTooltipNight => _coDutyStatusGlyphNight == "👥" ? "Zaakceptowane przez współdyżurnego" : "Oczekiwanie na akceptację współdyżurnego";

        // Wysokość i szerokość slotu dla dynamicznego skalowania czcionek
        private double _slotHeight = 48.0;
        public double SlotHeight { get => _slotHeight; set => SetProperty(ref _slotHeight, value); }

        private double _slotWidth = 100.0;
        public double SlotWidth { get => _slotWidth; set => SetProperty(ref _slotWidth, value); }

        public void UpdateSelection(HashSet<SlotPart> selectedParts)
        {
            IsFullSelected = selectedParts.Contains(SlotPart.Full);
            IsDaySelected = selectedParts.Contains(SlotPart.Day);
            IsNightSelected = selectedParts.Contains(SlotPart.Night);

            if (IsDaySelected && IsNightSelected)
            {
                DaySelectionBorderThickness = new Thickness(4, 4, 4, 2);
                NightSelectionBorderThickness = new Thickness(4, 2, 4, 4);
            }
            else if (IsDaySelected)
            {
                DaySelectionBorderThickness = new Thickness(4);
                NightSelectionBorderThickness = new Thickness(0);
            }
            else if (IsNightSelected)
            {
                NightSelectionBorderThickness = new Thickness(4);
                DaySelectionBorderThickness = new Thickness(0);
            }
            else
            {
                DaySelectionBorderThickness = new Thickness(0);
                NightSelectionBorderThickness = new Thickness(0);
            }
        }

        public void ClearData()
        {
            SymbolFull = "";
            SymbolDay = "";
            SymbolNight = "";
        }

        public DayCell(int index, DateTime date, bool inMonth, bool isWeekend, bool isHoliday, string? holidayName)
        {
            Index = index;
            Date = date;
            InMonth = inMonth;
            IsWeekend = isWeekend;
            IsHoliday = isHoliday;
            HolidayName = holidayName;
        }
    }

    // POPRAWKA: Rozszerzenie DeclarationsViewModel o Dispose
    public sealed partial class DeclarationsViewModel
    {
        /// <summary>
        /// Sprawdza czy są niezapisane zmiany.
        /// </summary>
        public bool HasUnsavedChanges => _isDirty;

        /// <summary>
        /// Aktualizuje pola współdyżurnego dla konkretnego dnia w lokalnych deklaracjach.
        /// </summary>
        public void UpdateCoDutyFields(Guid doctorId, int day, Guid? partnerId, string? status, Guid? initiatorId, string? slotPart)
        {
            var key = Key(doctorId, Year, MonthIndex);
            if (!_sharedDeclarations.TryGetValue(key, out var declaration))
            {
                System.Diagnostics.Debug.WriteLine($"[ViewModel.UpdateCoDuty] Nie znaleziono deklaracji dla doctorId={doctorId}");
                return;
            }

            int dayIndex = day - 1;
            if (dayIndex >= 0 && dayIndex < declaration.Days.Length)
            {
                declaration.Days[dayIndex].CoDutyPartnerId = partnerId;
                declaration.Days[dayIndex].CoDutyStatus = status != null && Enum.TryParse<GrafikoMat.Models.CoDutyStatus>(status, true, out var parsedStatus)
                    ? parsedStatus
                    : null;
                declaration.Days[dayIndex].CoDutyInitiatorId = initiatorId;
                declaration.Days[dayIndex].CoDutySlotPart = slotPart != null && Enum.TryParse<GrafikoMat.Models.SlotPart>(slotPart, true, out var parsedSlotPart)
                    ? parsedSlotPart
                    : null;

                _isDirty = true;
                SaveCommand.NotifyCanExecuteChanged();
                OnPropertyChanged(nameof(HasUnsavedChanges));
                System.Diagnostics.Debug.WriteLine($"[ViewModel.UpdateCoDuty] Zaktualizowano dzień {day} dla doctorId={doctorId}: partner={partnerId}, status={status}, slotPart={slotPart}");
            }
        }

        /// <summary>
        /// Ładuje dni specjalne z bazy danych i ustawia cache w PolishHolidays.
        /// </summary>
        private async Task LoadSpecialDaysAsync()
        {
            if (_specialDayRepository == null) return;

            try
            {
                var specialDays = await _specialDayRepository.GetSpecialDaysForYearAsync(Year, _currentUnitId);
                Common.PolishHolidays.SetSpecialDaysCache(Year, _currentUnitId, specialDays);

                System.Diagnostics.Debug.WriteLine($"[DeclarationsViewModel] Loaded {specialDays.Count} special days for year {Year}");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[DeclarationsViewModel] Error loading special days: {ex.Message}");
                // Nie przerywaj inicjalizacji - kontynuuj bez dni specjalnych
            }
        }

        /// <summary>
        /// Zwalnia zasoby używane przez ViewModel.
        /// </summary>
        public void Dispose()
        {
            if (_isDisposed) return;

            // Commit ostatnie zmiany przed dispose
            CommitChangesToSharedState();

            // Odsubskrybuj event handlery
            UnsubscribeFromCellChanges();

            // Wyczyść kolekcje
            Doctors.Clear();
            DayCells.Clear();
            SelectedSlots.Clear();

            _isDisposed = true;

            System.Diagnostics.Debug.WriteLine($"[DeclarationsViewModel] Disposed for {Year}-{MonthIndex + 1}");
        }
    }
}