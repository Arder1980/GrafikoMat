using CommunityToolkit.Mvvm.Input;
using GrafikoMat.Common;
using GrafikoMat.Core.Data;
using GrafikoMat.Models;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows.Input;

namespace GrafikoMat.ViewModels
{
    // ================== NOWA KLASA POMOCNICZA ==================
    /// <summary>
    /// Reprezentuje pojedynczego lekarza na liście wyboru w widoku deklaracji.
    /// Przechowuje profil oraz nazwę wyświetlaną (potencjalnie ze skrótem).
    /// </summary>
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
    // ==========================================================

    public enum SlotPart { Full, Day, Night }
    public record SelectedSlot(int Index, SlotPart Part);

    public sealed class DeclarationsViewModel : ObservableObject
    {
        private readonly Dictionary<string, DoctorMonthDeclaration> _sharedDeclarations;
        private readonly Action _onSaveCallback;
        private readonly bool _use12hShiftsByDefault;

        public int Year { get; }
        public int MonthIndex { get; }
        public string MonthHeader => $"Deklaracje dyżurowe na {PolishMonth(MonthIndex + 1)} {Year}";

        // ================== ZMIANA TYPU KOLEKCJI ==================
        public ObservableCollection<DoctorDeclarationViewModel> Doctors { get; } = new();
        public ObservableCollection<DayCell> DayCells { get; } = new();

        public HashSet<SelectedSlot> SelectedSlots { get; } = new();

        public bool CanSwitchDoctors { get; }

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

        // Zaktualizowana właściwość zwracająca czysty profil
        public DoctorProfile? SelectedDoctor =>
            (_selectedDoctorIndex >= 0 && _selectedDoctorIndex < Doctors.Count) ?
            Doctors[_selectedDoctorIndex].Profile : null;
        public ICommand SaveCommand { get; }
        public ICommand ClearSelectionCommand { get; }
        public ICommand SelectNextDoctorCommand { get; }
        public ICommand SelectPrevDoctorCommand { get; }

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

            // ================== NOWA LOGIKA GENEROWANIA NAZW WYŚWIETLANYCH ==================
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
            // =============================================================================

            _selectedDoctorIndex = (Doctors.Count > 0) ? Math.Clamp(initialDoctorIndex, 0, Doctors.Count - 1) : -1;

            SaveCommand = new RelayCommand(() => { CommitChangesToSharedState(); _onSaveCallback?.Invoke(); });
            ClearSelectionCommand = new RelayCommand(ClearSelection);
            SelectNextDoctorCommand = new RelayCommand(SelectNextDoctor, () => CanSwitchDoctors && Doctors.Count > 1);
            SelectPrevDoctorCommand = new RelayCommand(SelectPrevDoctor, () => CanSwitchDoctors && Doctors.Count > 1);

            BuildCalendarShell();
            LoadDeclarationsForSelectedDoctor();
        }

        private void BuildCalendarShell()
        {
            DayCells.Clear();
            var firstDay = new DateTime(Year, MonthIndex + 1, 1);
            int offset = ((int)firstDay.DayOfWeek + 6) % 7;
            int daysInMonth = DateTime.DaysInMonth(Year, MonthIndex + 1);
            int weeks = (int)Math.Ceiling((offset + daysInMonth) / 7.0);
            var startDate = firstDay.AddDays(-offset);
            for (int i = 0; i < weeks * 7; i++)
            {
                var date = startDate.AddDays(i);
                var cell = new DayCell(i, date, date.Month == MonthIndex + 1,
                    date.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday,
                    // ZMIANA: Użycie IsPublicHoliday zamiast GetHolidayName do określania dni wolnych
                    PolishHolidays.IsPublicHoliday(date),
                    PolishHolidays.GetHolidayName(date));

                if (cell.InMonth) cell.IsSplit = _use12hShiftsByDefault;
                DayCells.Add(cell);
            }
        }

        private void LoadDeclarationsForSelectedDoctor()
        {
            ClearSelection();
            foreach (var cell in DayCells)
            {
                if (cell.InMonth) cell.IsSplit = _use12hShiftsByDefault;
                cell.ClearData();
            }
            if (SelectedDoctor == null) return;

            var key = Key(SelectedDoctor.FullName, Year, MonthIndex);
            if (_sharedDeclarations.TryGetValue(key, out var decl))
            {
                foreach (var cell in DayCells.Where(c => c.InMonth))
                {
                    int dayIdx = cell.Date.Day - 1;
                    if (dayIdx >= 0 && dayIdx < decl.Days.Length)
                    {
                        var d = decl.Days[dayIdx];
                        cell.IsSplit = d.Mode == DayMode.Split12;
                        cell.SymbolFull = d.Full ?? "";
                        cell.SymbolDay = d.Day ?? "";
                        cell.SymbolNight = d.Night ?? "";
                    }
                }
            }
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

        private void SelectNextDoctor()
        {
            if (Doctors.Count > 1)
                SelectedDoctorIndex = (SelectedDoctorIndex + 1) % Doctors.Count;
        }

        private void SelectPrevDoctor()
        {
            if (Doctors.Count > 1)
                SelectedDoctorIndex = (SelectedDoctorIndex - 1 + Doctors.Count) % Doctors.Count;
        }

        public void SelectSingleSlot(int index, SlotPart part)
        {
            SelectedSlots.Clear();
            if (index >= 0 && index < DayCells.Count)
            {
                SelectedSlots.Add(new SelectedSlot(index, part));
            }
            UpdateSelectionVisuals();
        }

        public void ToggleSlotSelection(int index, SlotPart part)
        {
            if (index < 0 || index >= DayCells.Count) return;

            var slotToToggle = new SelectedSlot(index, part);
            if (SelectedSlots.Contains(slotToToggle))
            {
                SelectedSlots.Remove(slotToToggle);
            }
            else
            {
                SelectedSlots.Add(slotToToggle);
            }
            UpdateSelectionVisuals();
        }

        public void SelectRange(int startIndex, int endIndex, SlotPart partToSelect)
        {
            SelectedSlots.Clear();
            int start = Math.Min(startIndex, endIndex);
            int end = Math.Max(startIndex, endIndex);

            for (int i = start; i <= end; i++)
            {
                var cell = DayCells[i];
                if (!cell.InMonth) continue;

                if (cell.IsSplit)
                {
                    if (partToSelect == SlotPart.Day || partToSelect == SlotPart.Night)
                    {
                        SelectedSlots.Add(new SelectedSlot(i, partToSelect));
                    }
                }
                else
                {
                    if (partToSelect == SlotPart.Full)
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
                var cell = DayCells[i];
                if (!cell.InMonth) continue;

                if (expandToFullDays && cell.IsSplit)
                {
                    SelectedSlots.Add(new SelectedSlot(i, SlotPart.Day));
                    SelectedSlots.Add(new SelectedSlot(i, SlotPart.Night));
                }
                else
                {
                    if (cell.IsSplit)
                    {
                        if (startPart == SlotPart.Day || startPart == SlotPart.Night)
                        {
                            SelectedSlots.Add(new SelectedSlot(i, startPart));
                        }
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
            var selectedByCellIndex = SelectedSlots.GroupBy(s => s.Index)
                .ToDictionary(g => g.Key, g => g.Select(s => s.Part).ToHashSet());

            foreach (var cell in DayCells)
            {
                if (selectedByCellIndex.TryGetValue(cell.Index, out var selectedParts))
                {
                    cell.UpdateSelection(selectedParts);
                }
                else
                {
                    cell.UpdateSelection(new HashSet<SlotPart>());
                }
            }
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

        public Brush EffectiveBackground { get; set; }
        public Brush EffectiveBorderBrush { get; set; }
        public Brush DayNumberForeground { get; set; }
        public Brush EffectiveHeaderBackground { get; set; }

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

            EffectiveBackground = new SolidColorBrush();
            EffectiveBorderBrush = new SolidColorBrush();
            DayNumberForeground = new SolidColorBrush();
            EffectiveHeaderBackground = new SolidColorBrush();
        }

        public void NotifyBrushUpdate()
        {
            OnPropertyChanged(nameof(EffectiveBackground));
            OnPropertyChanged(nameof(EffectiveBorderBrush));
            OnPropertyChanged(nameof(DayNumberForeground));
            OnPropertyChanged(nameof(EffectiveHeaderBackground));
        }
    }
}