using GrafikoMat.Models;
using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using Windows.UI;

namespace GrafikoMat.ViewModels
{
    public sealed class DeclarationsViewModel : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler? PropertyChanged;
        private void Raise([CallerMemberName] string? n = null) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(n));

        // Parametry miesiąca
        public int Year { get; }
        public int MonthIndex { get; } // 0..11
        public string MonthHeader => $"{PolishMonth(MonthIndex)} {Year}";

        // Lekarze
        public ObservableCollection<DoctorMini> Doctors { get; } = new();

        // UWAGA: -1 oznacza „brak wyboru” (gdy lista lekarzy pusta)
        private int _selectedDoctorIndex = -1;
        public DoctorMini? SelectedDoctor
        {
            get => (_selectedDoctorIndex >= 0 && _selectedDoctorIndex < Doctors.Count) ? Doctors[_selectedDoctorIndex] : null;
            set
            {
                if (value == null) { _selectedDoctorIndex = -1; }
                else
                {
                    int idx = Doctors.IndexOf(value);
                    _selectedDoctorIndex = idx >= 0 ? idx : -1;
                }
                Raise();
            }
        }

        public int DutyLimit { get; set; } = 0;

        // Siatka dni (6x7) – 42 komórki
        public ObservableCollection<DayCell> DayCells { get; } = new();

        // Zaznaczenie
        public HashSet<int> SelectedIndices { get; } = new();

        public DeclarationsViewModel(int year, int monthIndex, string[] doctorNames, int selectedDoctorIndex)
        {
            Year = year; MonthIndex = monthIndex;

            if (doctorNames != null)
                foreach (var name in doctorNames)
                    Doctors.Add(new DoctorMini(name));

            // Jeśli nie ma lekarzy – brak wyboru; w przeciwnym razie zaclampuj bezpiecznie.
            _selectedDoctorIndex = Doctors.Count == 0
                ? -1
                : Math.Clamp(selectedDoctorIndex, 0, Doctors.Count - 1);

            BuildCalendar();
        }

        public void SelectPrevDoctor()
        {
            if (Doctors.Count == 0) return;
            if (_selectedDoctorIndex < 0) _selectedDoctorIndex = 0;
            else _selectedDoctorIndex = (_selectedDoctorIndex - 1 + Doctors.Count) % Doctors.Count;
            Raise(nameof(SelectedDoctor));
        }

        public void SelectNextDoctor()
        {
            if (Doctors.Count == 0) return;
            if (_selectedDoctorIndex < 0) _selectedDoctorIndex = 0;
            else _selectedDoctorIndex = (_selectedDoctorIndex + 1) % Doctors.Count;
            Raise(nameof(SelectedDoctor));
        }

        private void BuildCalendar()
        {
            DayCells.Clear();
            var first = new DateTime(Year, MonthIndex + 1, 1);
            int offset = ((int)first.DayOfWeek + 6) % 7; // Pon=0 ... Nd=6
            var start = first.AddDays(-offset);

            for (int i = 0; i < 42; i++)
            {
                var date = start.AddDays(i);
                bool inMonth = (date.Month == MonthIndex + 1);
                var cell = new DayCell(i, date, inMonth);
                DayCells.Add(cell);
            }
        }

        // ===== Zaznaczenie =====
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

        // ===== Deklaracje =====
        public void ApplySymbolToSelection(char sym)
        {
            foreach (var i in SelectedIndices)
            {
                var c = DayCells[i];
                if (!c.InMonth) continue;

                if (!c.IsSplit)
                {
                    c.SymbolFull = sym == '-' ? "" : sym.ToString();
                }
                else
                {
                    c.SymbolDay = sym == '-' ? "" : sym.ToString();
                    c.SymbolNight = sym == '-' ? "" : sym.ToString();
                }
            }
        }

        public void ToggleSplitForSelectedDays()
        {
            foreach (var i in SelectedIndices)
            {
                var c = DayCells[i];
                if (!c.InMonth) continue;
                c.IsSplit = !c.IsSplit;

                if (!c.IsSplit)
                {
                    c.SymbolFull = string.IsNullOrEmpty(c.SymbolDay) ? c.SymbolNight : c.SymbolDay;
                    c.SymbolDay = c.SymbolNight = "";
                }
            }
        }

        public void ClearSelectionSymbols()
        {
            foreach (var i in SelectedIndices)
            {
                var c = DayCells[i];
                c.SymbolFull = c.SymbolDay = c.SymbolNight = "";
            }
        }

        public void EditCoDutyPlaceholder()
        {
            // Do wpięcia później (ContentDialog z listą lekarzy)
        }

        public void SaveDraft()
        {
            // Miejsce na walidację/auto-korektę przed serializacją do Core
        }

        public DoctorMonthDeclaration ToResult()
        {
            int daysInMonth = DateTime.DaysInMonth(Year, MonthIndex + 1);
            var res = new DoctorMonthDeclaration
            {
                Doctor = SelectedDoctor?.Name ?? string.Empty, // przy braku lekarzy zwróci pustą nazwę
                Year = Year,
                MonthIndex = MonthIndex,
                Days = new DayDeclaration[daysInMonth]
            };

            for (int i = 0; i < daysInMonth; i++)
                res.Days[i] = new DayDeclaration();

            foreach (var cell in DayCells)
            {
                if (!cell.InMonth) continue;
                int di = cell.Date.Day - 1;

                if (!cell.IsSplit)
                {
                    res.Days[di].Mode = DayMode.Full24;
                    res.Days[di].Full = string.IsNullOrWhiteSpace(cell.SymbolFull) ? null : cell.SymbolFull;
                }
                else
                {
                    res.Days[di].Mode = DayMode.Split12;
                    res.Days[di].Day = string.IsNullOrWhiteSpace(cell.SymbolDay) ? null : cell.SymbolDay;
                    res.Days[di].Night = string.IsNullOrWhiteSpace(cell.SymbolNight) ? null : cell.SymbolNight;
                }
            }
            return res;
        }

        private static string PolishMonth(int idx) => new[]
        {
            "Styczeń","Luty","Marzec","Kwiecień","Maj","Czerwiec",
            "Lipiec","Sierpień","Wrzesień","Październik","Listopad","Grudzień"
        }[idx % 12];
    }

    public sealed class DoctorMini { public string Name { get; } public DoctorMini(string name) => Name = name; }

    public sealed class DayCell : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler? PropertyChanged;
        private void Raise([CallerMemberName] string? n = null) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(n));

        public int Index { get; }
        public DateTime Date { get; }
        public bool InMonth { get; }

        public string DayNumber => Date.Day.ToString("00");
        public double HeaderOpacity => InMonth ? 1.0 : 0.4;

        // Tryb 24h / 12+12
        private bool _isSplit;
        public bool IsSplit
        {
            get => _isSplit;
            set { if (_isSplit != value) { _isSplit = value; Raise(); Raise(nameof(Full24Visibility)); Raise(nameof(Split12Visibility)); } }
        }
        public Visibility Full24Visibility => IsSplit ? Visibility.Collapsed : Visibility.Visible;
        public Visibility Split12Visibility => IsSplit ? Visibility.Visible : Visibility.Collapsed;

        // Symbole
        private string _symbolFull = "";
        public string SymbolFull { get => _symbolFull; set { _symbolFull = value; Raise(); } }
        private string _symbolDay = "";
        public string SymbolDay { get => _symbolDay; set { _symbolDay = value; Raise(); } }
        private string _symbolNight = "";
        public string SymbolNight { get => _symbolNight; set { _symbolNight = value; Raise(); } }

        // Wizualka zaznaczenia
        private bool _selected;
        public void SetSelected(bool sel)
        {
            _selected = sel;
            Raise(nameof(BorderBrush)); Raise(nameof(BorderThickness)); Raise(nameof(Background)); Raise(nameof(SplitDividerBrush));
        }

        public Brush BorderBrush => _selected ? new SolidColorBrush(Color.FromArgb(0xFF, 0x33, 0x99, 0xFF))
                                              : new SolidColorBrush(Color.FromArgb(0x30, 0x00, 0x00, 0x00));
        public Thickness BorderThickness => _selected ? new Thickness(2) : new Thickness(1);
        public Brush Background
            => InMonth ? (_selected ? new SolidColorBrush(Color.FromArgb(0x12, 0x33, 0x99, 0xFF))
                                    : new SolidColorBrush(Colors.Transparent))
                       : new SolidColorBrush(Color.FromArgb(0x0E, 0x00, 0x00, 0x00));
        public Brush SplitDividerBrush => new SolidColorBrush(Color.FromArgb(0x50, 0x00, 0x00, 0x00));

        public DayCell(int index, DateTime date, bool inMonth)
        {
            Index = index; Date = date; InMonth = inMonth;
        }
    }
}
