using CommunityToolkit.Mvvm.Input;
using GrafikoMat.Common;
using GrafikoMat.Core.Data;
using GrafikoMat.Core.Scheduling;
using GrafikoMat.Models;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;

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

    public enum SlotPart { Full, Day, Night }
    public record SelectedSlot(int Index, SlotPart Part);

    public sealed partial class DeclarationsViewModel : ObservableObject, IDisposable
    {
        private readonly Dictionary<string, DoctorMonthDeclaration> _sharedDeclarations;
        private readonly Action _onSaveCallback;
        private bool _use12hShiftsByDefault;
        private MonthLayout _monthLayout;
        private bool _isDisposed;
        private bool _isDirty = false;
        private bool _isLoading = false; // Flaga zapobiegająca oznaczaniu jako dirty podczas ładowania

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
        public RelayCommand ClearSelectionCommand { get; }
        public RelayCommand SelectNextDoctorCommand { get; }
        public RelayCommand SelectPrevDoctorCommand { get; }

        public DeclarationsViewModel(
            int year, int monthIndex, List<DoctorProfile> doctors, int initialDoctorIndex,
            Dictionary<string, DoctorMonthDeclaration> sharedDeclarations, bool isAdmin,
            bool use12hShifts, Action onSaveCallback)
        {
            Year = year;
            MonthIndex = monthIndex;
            _sharedDeclarations = sharedDeclarations;
            CanSwitchDoctors = isAdmin;
            _onSaveCallback = onSaveCallback;
            _use12hShiftsByDefault = use12hShifts;
            _currentUnitIndex = 0;

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

            SaveCommand = new RelayCommand(DoSave, () => _isDirty);
            ClearSelectionCommand = new RelayCommand(ClearSelection);
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
                    PolishHolidays.GetHolidayName(date));

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
        }

        private void LoadDeclarationsForSelectedDoctor()
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

            var key = Key(SelectedDoctor.FullName, Year, MonthIndex);
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
            var key = Key(SelectedDoctor.FullName, Year, MonthIndex);
            int daysInMonth = DateTime.DaysInMonth(Year, MonthIndex + 1);
            var result = new DoctorMonthDeclaration
            {
                Doctor = SelectedDoctor.FullName,
                Year = Year,
                MonthIndex = MonthIndex,
                Days = Enumerable.Range(0, daysInMonth).Select(_ => new DayDeclaration()).ToArray()
            };

            foreach (var cell in DayCells.Where(c => c.InMonth))
            {
                int dayIdx = cell.Date.Day - 1;
                if (dayIdx < 0 || dayIdx >= result.Days.Length) continue;

                if (!cell.IsSplit)
                {
                    result.Days[dayIdx].Mode = DayMode.Full24;
                    result.Days[dayIdx].Full = string.IsNullOrWhiteSpace(cell.SymbolFull) ? null : cell.SymbolFull;
                }
                else
                {
                    result.Days[dayIdx].Mode = DayMode.Split12;
                    result.Days[dayIdx].Day = string.IsNullOrWhiteSpace(cell.SymbolDay) ? null : cell.SymbolDay;
                    result.Days[dayIdx].Night = string.IsNullOrWhiteSpace(cell.SymbolNight) ? null : cell.SymbolNight;
                }
            }

            _sharedDeclarations[key] = result;
        }

        private void DoSave()
        {
            CommitChangesToSharedState();
            _onSaveCallback?.Invoke();
            _isDirty = false;
            SaveCommand.NotifyCanExecuteChanged();
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
                    if (cell.IsSplit)
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

        public void ClearSelection()
        {
            SelectedSlots.Clear();
            UpdateSelectionVisuals();
        }

        private void UpdateSelectionVisuals()
        {
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
        private static string Key(string doctor, int year, int monthIndex) => $"{doctor}|{year:D4}-{monthIndex:D2}";
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
        public string SymbolFull { get => _symbolFull; set => SetProperty(ref _symbolFull, value); }

        private string _symbolDay = "";
        public string SymbolDay { get => _symbolDay; set => SetProperty(ref _symbolDay, value); }

        private string _symbolNight = "";
        public string SymbolNight { get => _symbolNight; set => SetProperty(ref _symbolNight, value); }

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