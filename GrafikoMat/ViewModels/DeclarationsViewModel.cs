using GrafikoMat.Common;
using GrafikoMat.Core.Data;
using GrafikoMat.Models;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows.Input;
using CommunityToolkit.Mvvm.Input;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;

namespace GrafikoMat.ViewModels
{
    public sealed class DeclarationsViewModel : ObservableObject
    {
        private readonly Dictionary<string, DoctorMonthDeclaration> _sharedDeclarations;
        private readonly Action _onSaveCallback;

        public int Year { get; }
        public int MonthIndex { get; }
        public string MonthHeader => $"Deklaracje dyżurowe na {PolishMonth(MonthIndex + 1)} {Year}";
        public ObservableCollection<DoctorProfile> Doctors { get; } = new();
        public ObservableCollection<DayCell> DayCells { get; } = new();
        public HashSet<int> SelectedIndices { get; } = new();

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

        public DoctorProfile? SelectedDoctor =>
            (_selectedDoctorIndex >= 0 && _selectedDoctorIndex < Doctors.Count) ?
            Doctors[_selectedDoctorIndex] : null;

        public ICommand SaveCommand { get; }
        public ICommand ClearSelectionCommand { get; }
        public ICommand SelectNextDoctorCommand { get; }
        public ICommand SelectPrevDoctorCommand { get; }

        public DeclarationsViewModel(
            int year,
            int monthIndex,
            List<DoctorProfile> doctors,
            int initialDoctorIndex,
            Dictionary<string, DoctorMonthDeclaration> sharedDeclarations,
            bool isAdmin,
            Action onSaveCallback)
        {
            Year = year;
            MonthIndex = monthIndex;
            _sharedDeclarations = sharedDeclarations;
            CanSwitchDoctors = isAdmin;
            _onSaveCallback = onSaveCallback;

            doctors.ForEach(d => Doctors.Add(d));
            _selectedDoctorIndex = (Doctors.Count > 0) ? Math.Clamp(initialDoctorIndex, 0, Doctors.Count - 1) : -1;

            SaveCommand = new RelayCommand(() =>
            {
                CommitChangesToSharedState();
                _onSaveCallback?.Invoke();
            });
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
            int offset = ((int)firstDay.DayOfWeek + 6) % 7; // pon=0
            int daysInMonth = DateTime.DaysInMonth(Year, MonthIndex + 1);
            int weeks = (int)Math.Ceiling((offset + daysInMonth) / 7.0);
            var startDate = firstDay.AddDays(-offset);

            for (int i = 0; i < weeks * 7; i++)
            {
                var date = startDate.AddDays(i);
                bool inMonth = (date.Month == MonthIndex + 1);

                var holiday = PolishHolidays.GetHolidayName(date);
                bool isWeekend = (date.DayOfWeek == DayOfWeek.Saturday || date.DayOfWeek == DayOfWeek.Sunday);
                bool isHoliday = !string.IsNullOrEmpty(holiday);

                var cell = new DayCell(i, date, inMonth, isWeekend, isHoliday, holiday);
                DayCells.Add(cell);
            }
        }

        private void LoadDeclarationsForSelectedDoctor()
        {
            ClearSelection();
            foreach (var cell in DayCells) cell.ClearData();
            if (SelectedDoctor == null) return;

            var key = Key(SelectedDoctor.FullName, Year, MonthIndex);
            if (_sharedDeclarations.TryGetValue(key, out var decl))
            {
                foreach (var cell in DayCells)
                {
                    if (!cell.InMonth) continue;
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

        public void SelectSingle(int index)
        {
            SelectedIndices.Clear();
            if (index >= 0 && index < DayCells.Count) SelectedIndices.Add(index);
            UpdateSelectionVisuals();
        }

        public void SelectRange(int a, int b)
        {
            SelectedIndices.Clear();
            int start = Math.Min(a, b);
            int end = Math.Max(a, b);
            for (int i = start; i <= end; i++) SelectedIndices.Add(i);
            UpdateSelectionVisuals();
        }

        public void ClearSelection()
        {
            SelectedIndices.Clear();
            UpdateSelectionVisuals();
        }

        private void UpdateSelectionVisuals()
        {
            foreach (var c in DayCells)
                c.SetSelected(SelectedIndices.Contains(c.Index));
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

        // Pędzle publiczne, aby Widok mógł je ustawić
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

        private bool _isSelected;
        public bool IsSelected { get; private set; }

        public void SetSelected(bool selected)
        {
            if (IsSelected == selected) return;
            IsSelected = selected;
            OnPropertyChanged(nameof(BorderThickness));
        }

        public Thickness BorderThickness => IsSelected ? new Thickness(2.0) : new Thickness(1.0);

        public void ClearData()
        {
            SymbolFull = "";
            SymbolDay = "";
            SymbolNight = "";
            IsSplit = false;
        }

        public DayCell(int index, DateTime date, bool inMonth, bool isWeekend, bool isHoliday, string? holidayName)
        {
            Index = index;
            Date = date;
            InMonth = inMonth;
            IsWeekend = isWeekend;
            IsHoliday = isHoliday;
            HolidayName = holidayName;

            // Inicjalizacja pustymi pędzlami - Widok nada im właściwe kolory
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