using System;
using System.Collections.Specialized;
using System.ComponentModel;
using GrafikoMat.Common;
using GrafikoMat.Services;
using GrafikoMat.ViewModels;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Windows.UI;

namespace GrafikoMat.Views
{
    public sealed partial class DashboardView : UserControl
    {
        private const int NameColWidth = 220;
        private const int RowHeight = 32;

        private MainViewModel? _vm;
        public DashboardView()
        {

            InitializeComponent();
            DeclarationsGrid.SizeChanged += (s, e) => BuildLeftTable();
            // Subskrypcja na zmianę motywu, aby przerysować tabelę
            this.ActualThemeChanged += OnThemeChanged;
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
            if (_vm != null && (e.PropertyName ==
                nameof(MainViewModel.SelectedYear) ||
                e.PropertyName == nameof(MainViewModel.SelectedMonthIndex)))
            {
                BuildLeftTable();
            }
        }

        private void OnThemeChanged(FrameworkElement sender, object args)
        {
            // Gdy motyw 
            // się zmienia, przerysuj tabelę z nowymi kolorami
            BuildLeftTable();
        }

        private void OnYearPrev(object sender, RoutedEventArgs e) => _vm?.PrevYear();
        private void OnYearNext(object sender, RoutedEventArgs e) => _vm?.NextYear();
        private void OnMonthPrev(object sender, RoutedEventArgs e) => _vm?.PrevMonth();
        private void OnMonthNext(object sender, RoutedEventArgs e) => _vm?.NextMonth();

        private void BuildLeftTable()
        {
            if (_vm is null || this.ActualWidth == 0) return;
            DeclarationsGrid.Children.Clear();
            DeclarationsGrid.RowDefinitions.Clear();
            DeclarationsGrid.ColumnDefinitions.Clear();

            int year = _vm.SelectedYear;
            int month = _vm.SelectedMonthIndex + 1;
            int daysInMonth = DateTime.DaysInMonth(year, month);
            int rowsCount = _vm.DoctorRows.Count + 1;

            // ZMIANA: Pobieramy motyw z "Wyroczni"
            var currentTheme = ThemeManagerService.Instance.CurrentTheme;

            // Definiowanie palety kolorów w zależności od motywu
            SolidColorBrush borderBrush, dayOffFill;
            if (currentTheme == ElementTheme.Light)
            {

                borderBrush = new SolidColorBrush(Color.FromArgb(0x18, 0, 0, 0));
                dayOffFill = new SolidColorBrush(Color.FromArgb(0x0A, 0, 0, 0));
            }
            else // Dark
            {
                borderBrush = new SolidColorBrush(Color.FromArgb(0x18, 255, 255, 255));
                dayOffFill = new SolidColorBrush(Color.FromArgb(0x0A, 255, 255, 255));
            }

            DeclarationsGrid.ColumnDefinitions.Add(new ColumnDefinition
            {
                Width = new
                GridLength(NameColWidth)
            });
            for (int d = 1; d <= daysInMonth; d++)
            {
                DeclarationsGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            }

            DeclarationsGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            for (int r = 1; r < rowsCount; r++)
            {

                DeclarationsGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(RowHeight) });
            }

            var hdrName = new Border { BorderBrush = borderBrush, BorderThickness = new Thickness(0, 0, 1, 1) };
            Grid.SetRow(hdrName, 0);
            Grid.SetColumn(hdrName, 0);
            hdrName.Child = new TextBlock { Text = "Dyżurny", FontWeight = Microsoft.UI.Text.FontWeights.SemiBold, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(8, 0, 8, 0) };
            DeclarationsGrid.Children.Add(hdrName);

            for (int d = 1; d <= daysInMonth; d++)
            {
                var date = new DateTime(year, month, d);
                // ZMIANA: Użycie IsPublicHoliday(date) zamiast GetHolidayName(date) != null
                bool isDayOff = date.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday || PolishHolidays.IsPublicHoliday(date);
                bool isLastColumn = (d == daysInMonth);
                var cell = new Border
                {
                    BorderBrush = borderBrush,

                    BorderThickness = isLastColumn ? new Thickness(0, 0, 0, 1) : new Thickness(0, 0, 1, 1),
                    Background = isDayOff ? dayOffFill : null
                };
                Grid.SetRow(cell, 0);
                Grid.SetColumn(cell, d);
                cell.Child = new TextBlock
                {
                    Text = $"{d:00}\n{DowPlShort(date.DayOfWeek)}",
                    TextAlignment = TextAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center,
                    LineHeight = 14,
                    Padding
                    = new Thickness(0, 4, 0, 4)
                };
                DeclarationsGrid.Children.Add(cell);
            }

            for (int r = 0; r < _vm.DoctorRows.Count; r++)
            {
                var doctor = _vm.DoctorRows[r];
                int row = r + 1;

                var nameCell = new Border { BorderBrush = borderBrush, BorderThickness = new Thickness(0, 0, 1, 1) };
                Grid.SetRow(nameCell, row); Grid.SetColumn(nameCell, 0);

                nameCell.Child = new TextBlock
                {
                    Text = doctor.DisplayName,
                    VerticalAlignment = VerticalAlignment.Center,
                    Margin = new Thickness(8, 0, 8, 0),
                    Opacity = doctor.HasDeclarations ? 1.0 : 0.6
                };
                DeclarationsGrid.Children.Add(nameCell);

                for (int d = 1; d <= daysInMonth; d++)
                {
                    var date = new DateTime(year, month, d);
                    // ZMIANA: Użycie IsPublicHoliday(date) zamiast GetHolidayName(date) != null
                    bool isDayOff = date.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday || PolishHolidays.IsPublicHoliday(date);
                    bool isLastColumn = (d == daysInMonth);
                    var
                        cell = new Border
                        {
                            BorderBrush = borderBrush,
                            BorderThickness = isLastColumn ? new Thickness(0, 0, 0, 1) : new Thickness(0, 0, 1, 1),

                            Background = isDayOff ? dayOffFill : null
                        };
                    Grid.SetRow(cell, row); Grid.SetColumn(cell, d);

                    var entry = _vm.TryGetEntry(doctor.Profile.FullName, year, _vm.SelectedMonthIndex, d - 1);
                    FrameworkElement content;
                    if (!entry.has || (entry.mode == Models.DayMode.Full24 && string.IsNullOrEmpty(entry.full)) || (entry.mode == Models.DayMode.Split12 && string.IsNullOrEmpty(entry.day) && string.IsNullOrEmpty(entry.night)))

                    {
                        content = new TextBlock { Text = "", HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
                    }
                    else if (entry.mode == Models.DayMode.Full24)
                    {

                        content = new TextBlock { Text = entry.full, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center, FontSize = 14 };
                    }
                    else // Split12
                    {

                        var g = new Grid();
                        g.RowDefinitions.Add(new RowDefinition());
                        g.RowDefinitions.Add(new RowDefinition());
                        g.Children.Add(new Border { BorderBrush = new SolidColorBrush(Color.FromArgb(0x50, 0, 0, 0)), BorderThickness = new Thickness(0, 0, 0, 1), VerticalAlignment = VerticalAlignment.Center });
                        var tb1 = new TextBlock { Text = entry.day, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center, FontSize = 12 };
                        var tb2 = new TextBlock { Text = entry.night, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center, FontSize = 12 };
                        Grid.SetRow(tb1, 0); Grid.SetRow(tb2, 1);
                        g.Children.Add(tb1); g.Children.Add(tb2);
                        content = g;
                    }

                    cell.Child = content;
                    DeclarationsGrid.Children.Add(cell);
                }
            }
        }
        private static string DowPlShort(DayOfWeek dow) => dow switch { DayOfWeek.Monday => "Pon", DayOfWeek.Tuesday => "Wto", DayOfWeek.Wednesday => "Śro", DayOfWeek.Thursday => "Czw", DayOfWeek.Friday => "Pt", DayOfWeek.Saturday => "Sob", DayOfWeek.Sunday => "Nie", _ => "" };
    }
}