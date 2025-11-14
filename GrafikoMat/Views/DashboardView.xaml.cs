using System;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using GrafikoMat.Common;
using GrafikoMat.Core.Enums;
using GrafikoMat.Services;
using GrafikoMat.ViewModels;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI;
using Windows.UI;

namespace GrafikoMat.Views
{
    public sealed partial class DashboardView : UserControl, INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler? PropertyChanged;
        private void OnPropertyChanged([CallerMemberName] string? name = null) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

        private Brush _dynamicBorderBrush = new SolidColorBrush(Colors.Transparent);
        public Brush DynamicBorderBrush
        {
            get => _dynamicBorderBrush;
            set
            {
                if (_dynamicBorderBrush != value)
                {
                    _dynamicBorderBrush = value;
                    OnPropertyChanged();
                }
            }
        }

        private const int NameColWidth = 220;
        private const int RowHeight = 32;

        private MainViewModel? _vm;
        private SizeChangedEventHandler? _sizeChangedHandler;

        public DashboardView()
        {
            InitializeComponent();
            _sizeChangedHandler = (s, e) => BuildLeftTable();
            DeclarationsGrid.SizeChanged += _sizeChangedHandler;
            this.ActualThemeChanged += OnThemeChanged;
            this.Unloaded += OnDashboardViewUnloaded;
        }

        private void OnDashboardViewUnloaded(object sender, RoutedEventArgs e)
        {
            // Odsubskrybuj event handlery kontrolek
            if (_sizeChangedHandler != null)
            {
                DeclarationsGrid.SizeChanged -= _sizeChangedHandler;
                _sizeChangedHandler = null;
            }

            this.ActualThemeChanged -= OnThemeChanged;

            // Odsubskrybuj od ViewModelu
            if (_vm != null)
            {
                _vm.PropertyChanged -= OnVmPropertyChanged;
                _vm.DoctorRows.CollectionChanged -= OnDoctorRowsChanged;
                _vm = null;
            }

            this.Unloaded -= OnDashboardViewUnloaded;
        }

        public void Attach(MainViewModel vm)
        {
            if (_vm != null)
            {
                _vm.PropertyChanged -= OnVmPropertyChanged;
                _vm.DoctorRows.CollectionChanged -= OnDoctorRowsChanged;
            }

            _vm = vm;
            this.DataContext = vm;
            if (_vm != null)
            {
                _vm.PropertyChanged += OnVmPropertyChanged;
                _vm.DoctorRows.CollectionChanged += OnDoctorRowsChanged;
            }
        }

        private void OnDoctorRowsChanged(object? sender, NotifyCollectionChangedEventArgs e)
        {
            DispatcherQueue.TryEnqueue(() =>
            {
                BuildLeftTable();
            });
        }

        private void OnVmPropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (_vm != null && (e.PropertyName == nameof(MainViewModel.SelectedYear) ||
                e.PropertyName == nameof(MainViewModel.SelectedMonthIndex)))
            {
                BuildLeftTable();
            }
        }

        private void OnThemeChanged(FrameworkElement sender, object args)
        {
            BuildLeftTable();
        }

        private void OnYearPrev(object sender, RoutedEventArgs e) => _vm?.PrevYear();
        private void OnYearNext(object sender, RoutedEventArgs e) => _vm?.NextYear();
        private void OnMonthPrev(object sender, RoutedEventArgs e) => _vm?.PrevMonth();
        private void OnMonthNext(object sender, RoutedEventArgs e) => _vm?.NextMonth();

        private void BuildLeftTable()
        {
            if (_vm is null || this.ActualWidth == 0) return;

            // NOWA IMPLEMENTACJA: Budowanie Grid z danych ViewModelu zamiast ręcznego tworzenia kontrolek
            DeclarationsGrid.Children.Clear();
            DeclarationsGrid.RowDefinitions.Clear();
            DeclarationsGrid.ColumnDefinitions.Clear();

            var cells = _vm.CalendarCells;
            if (cells.Count == 0) return;

            // Ustal liczbę wierszy i kolumn
            int maxRow = cells.Max(c => c.Row);
            int maxCol = cells.Max(c => c.Column);

            // Ustaw border brush dla dynamicznego motywu
            var currentTheme = ThemeManagerService.Instance.CurrentTheme;
            SolidColorBrush borderBrush = currentTheme == ElementTheme.Light
                ? new SolidColorBrush(Color.FromArgb(0x18, 0, 0, 0))
                : new SolidColorBrush(Color.FromArgb(0x18, 255, 255, 255));
            this.DynamicBorderBrush = borderBrush;

            // Utwórz definicje kolumn
            DeclarationsGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(NameColWidth) });
            for (int c = 1; c <= maxCol; c++)
            {
                DeclarationsGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            }

            // Utwórz definicje wierszy
            DeclarationsGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            for (int r = 1; r <= maxRow; r++)
            {
                DeclarationsGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(RowHeight) });
            }

            // Utwórz komórki z danych ViewModel
            foreach (var cellVM in cells)
            {
                var border = new Border
                {
                    Background = ParseHexColor(cellVM.BackgroundColor),
                    BorderBrush = ParseHexColor(cellVM.BorderColor),
                    BorderThickness = ParseThickness(cellVM.BorderThickness),
                    Padding = ParseThickness(cellVM.Padding)
                };

                var textBlock = new TextBlock
                {
                    Text = cellVM.Text,
                    FontWeight = cellVM.IsBold ? Microsoft.UI.Text.FontWeights.SemiBold : Microsoft.UI.Text.FontWeights.Normal,
                    FontSize = cellVM.FontSize,
                    Opacity = cellVM.Opacity,
                    LineHeight = cellVM.LineHeight,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center,
                    TextAlignment = TextAlignment.Center
                };

                border.Child = textBlock;
                Grid.SetRow(border, cellVM.Row);
                Grid.SetColumn(border, cellVM.Column);
                DeclarationsGrid.Children.Add(border);
            }
        }

        private static SolidColorBrush ParseHexColor(string hex)
        {
            if (string.IsNullOrWhiteSpace(hex) || !hex.StartsWith("#"))
                return new SolidColorBrush(Colors.Transparent);

            try
            {
                byte a = 255;
                int offset = 1;
                if (hex.Length == 9)
                {
                    a = Convert.ToByte(hex.Substring(1, 2), 16);
                    offset = 3;
                }
                byte r = Convert.ToByte(hex.Substring(offset, 2), 16);
                byte g = Convert.ToByte(hex.Substring(offset + 2, 2), 16);
                byte b = Convert.ToByte(hex.Substring(offset + 4, 2), 16);
                return new SolidColorBrush(Color.FromArgb(a, r, g, b));
            }
            catch
            {
                return new SolidColorBrush(Colors.Transparent);
            }
        }

        private static Thickness ParseThickness(string value)
        {
            if (string.IsNullOrWhiteSpace(value)) return new Thickness(0);

            var parts = value.Split(',');
            if (parts.Length == 4 &&
                double.TryParse(parts[0], out double left) &&
                double.TryParse(parts[1], out double top) &&
                double.TryParse(parts[2], out double right) &&
                double.TryParse(parts[3], out double bottom))
            {
                return new Thickness(left, top, right, bottom);
            }
            if (parts.Length == 1 && double.TryParse(parts[0], out double uniform))
            {
                return new Thickness(uniform);
            }
            return new Thickness(0);
        }
        private static string DowPlShort(DayOfWeek dow) => dow switch { DayOfWeek.Monday => "Pon", DayOfWeek.Tuesday => "Wto", DayOfWeek.Wednesday => "Śro", DayOfWeek.Thursday => "Czw", DayOfWeek.Friday => "Pt", DayOfWeek.Saturday => "Sob", DayOfWeek.Sunday => "Nie", _ => "" };

        /// <summary>
        /// Konwertuje 3-literowe kody deklaracji na skróty 1-literowe dla dashboard
        /// </summary>
        private static string? ConvertTo1LetterCode(string? code)
        {
            if (string.IsNullOrWhiteSpace(code))
                return code;

            return code switch
            {
                "MOG" => "M",
                "CHC" => "C",
                "WAR" => "W",
                "REZ" => "R",
                "DYZ" => "D",
                "URL" => "U",
                "---" => "-",
                _ => code // Dla nieznanych kodów lub już 1-literowych
            };
        }
    }
}