using GrafikoMat.Common;
using GrafikoMat.Core.Data;
using GrafikoMat.Models;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows.Input;
using CommunityToolkit.Mvvm.Input;
using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;
using Windows.UI;

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
        public DoctorProfile? SelectedDoctor => (_selectedDoctorIndex >= 0 && _selectedDoctorIndex < Doctors.Count) ? Doctors[_selectedDoctorIndex] : null;

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

            SaveCommand = new RelayCommand(() => {
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
            var firstDayOfMonth = new DateTime(Year, MonthIndex + 1, 1);
            int offset = ((int)firstDayOfMonth.DayOfWeek + 6) % 7;
            int daysInMonth = DateTime.DaysInMonth(Year, MonthIndex + 1);
            int weeks = (int)Math.Ceiling((offset + daysInMonth) / 7.0);
            var startDate = firstDayOfMonth.AddDays(-offset);

            for (int i = 0; i < weeks * 7; i++)
            {
                var date = startDate.AddDays(i);
                bool inMonth = (date.Month == MonthIndex + 1);
                var cell = new DayCell(i, date, inMonth);
                DayCells.Add(cell);
            }
        }

        private void LoadDeclarationsForSelectedDoctor()
        {
            ClearSelection();
            foreach (var cell in DayCells) { cell.ClearData(); }
            if (SelectedDoctor == null) { return; }

            var key = Key(SelectedDoctor.FullName, Year, MonthIndex);
            if (_sharedDeclarations.TryGetValue(key, out var decl))
            {
                foreach (var cell in DayCells)
                {
                    if (!cell.InMonth) continue;
                    int dayIdx = cell.Date.Day - 1;
                    if (dayIdx >= 0 && dayIdx < decl.Days.Length)
                    {
                        var dayDecl = decl.Days[dayIdx];
                        cell.IsSplit = dayDecl.Mode == DayMode.Split12;
                        cell.SymbolFull = dayDecl.Full ?? "";
                        cell.SymbolDay = dayDecl.Day ?? "";
                        cell.SymbolNight = dayDecl.Night ?? "";
                    }
                }
            }
        }

        public void CommitChangesToSharedState()
        {
            if (SelectedDoctor == null) return;
            var key = Key(SelectedDoctor.FullName, Year, MonthIndex);
            int daysInMonth = DateTime.DaysInMonth(Year, MonthIndex + 1);
            var result = new DoctorMonthDeclaration { Doctor = SelectedDoctor.FullName, Year = Year, MonthIndex = MonthIndex, Days = new DayDeclaration[daysInMonth] };
            for (int i = 0; i < daysInMonth; i++) result.Days[i] = new DayDeclaration();
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
            if (Doctors.Count > 1) SelectedDoctorIndex = (SelectedDoctorIndex + 1) % Doctors.Count;
        }

        private void SelectPrevDoctor()
        {
            if (Doctors.Count > 1) SelectedDoctorIndex = (SelectedDoctorIndex - 1 + Doctors.Count) % Doctors.Count;
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

        private static string PolishMonth(int month) => new[]
            { "","Styczeń","Luty","Marzec","Kwiecień","Maj","Czerwiec","Lipiec","Sierpień","Wrzesień","Październik","Listopad","Grudzień" }[month];

        private static string Key(string doctor, int year, int monthIndex) => $"{doctor}|{year:D4}-{monthIndex:D2}";
    }

    public sealed class DayCell : ObservableObject
    {
        public int Index { get; }
        public DateTime Date { get; }
        public bool InMonth { get; }
        public string DayNumber => Date.Day.ToString("00");
        public double HeaderOpacity => InMonth ? 1.0 : 0.4;

        public bool IsInteractive => InMonth;

        public bool IsDayOff { get; }
        public string? HolidayName { get; }
        public Visibility HolidayVisibility => string.IsNullOrEmpty(HolidayName) ? Visibility.Collapsed : Visibility.Visible;

        private bool _isSplit;
        public bool IsSplit { get => _isSplit; set => SetProperty(ref _isSplit, value); }

        private string _symbolFull = "";
        public string SymbolFull { get => _symbolFull; set => SetProperty(ref _symbolFull, value); }

        private string _symbolDay = "";
        public string SymbolDay { get => _symbolDay; set => SetProperty(ref _symbolDay, value); }

        private string _symbolNight = "";
        public string SymbolNight { get => _symbolNight; set => SetProperty(ref _symbolNight, value); }

        private bool _isSelected;
        public bool IsSelected { get => _isSelected; private set => SetProperty(ref _isSelected, value); }

        public Brush Background
        {
            get
            {
                if (!InMonth) return Application.Current.Resources["SubtleFillColorSecondaryBrush"] as Brush;
                if (_isSelected) return new SolidColorBrush(Color.FromArgb(0x2A, 0x33, 0x99, 0xFF));
                if (IsDayOff) return Application.Current.Resources["ControlFillColorSecondaryBrush"] as Brush;
                return Application.Current.Resources["ControlFillColorDefaultBrush"] as Brush;
            }
        }

        public Brush BorderBrush
        {
            get
            {
                if (!InMonth) return Application.Current.Resources["ControlStrokeColorSecondaryBrush"] as Brush;
                if (_isSelected) return new SolidColorBrush(Color.FromArgb(0xFF, 0x33, 0x99, 0xFF));
                return new SolidColorBrush(Color.FromArgb(0x30, 0, 0, 0));
            }
        }

        public Thickness BorderThickness => _isSelected ? new Thickness(2.0) : new Thickness(1.0);

        public void SetSelected(bool selected)
        {
            if (IsSelected != selected)
            {
                IsSelected = selected;
                OnPropertyChanged(nameof(Background));
                OnPropertyChanged(nameof(BorderBrush));
                OnPropertyChanged(nameof(BorderThickness));
            }
        }

        public void ClearData()
        {
            SymbolFull = "";
            SymbolDay = "";
            SymbolNight = "";
            IsSplit = false;
        }

        public DayCell(int index, DateTime date, bool inMonth)
        {
            Index = index;
            Date = date;
            InMonth = inMonth;

            HolidayName = PolishHolidays.GetHolidayName(date);
            IsDayOff = (date.DayOfWeek == DayOfWeek.Saturday || date.DayOfWeek == DayOfWeek.Sunday || HolidayName != null);
        }
    }
}