using System;
using System.ComponentModel;
using GrafikoMat.ViewModels;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Windows.Foundation;
using Windows.UI;

namespace GrafikoMat.Views
{
    public sealed partial class DashboardView : UserControl
    {
        private const int NameColWidth = 220;
        private const int DayColWidth = 40;
        private const int RowHeight = 32;

        private Grid? _leftGrid;
        private MainViewModel? _vm;

        public DashboardView()
        {
            InitializeComponent();
            LeftTableHost.SizeChanged += (_, __) => ApplyLeftTableScale();
        }

        public void Attach(MainViewModel vm)
        {
            if (_vm != null)
                _vm.PropertyChanged -= OnVmPropertyChanged;

            _vm = vm;
            this.DataContext = vm;
            _vm.PropertyChanged += OnVmPropertyChanged;

            BuildLeftTable();
        }

        private void OnVmPropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(MainViewModel.SelectedYear) ||
                e.PropertyName == nameof(MainViewModel.SelectedMonthIndex))
            {
                BuildLeftTable();
            }
        }

        private void OnYearPrev(object sender, RoutedEventArgs e) => _vm?.PrevYear();
        private void OnYearNext(object sender, RoutedEventArgs e) => _vm?.NextYear();
        private void OnMonthPrev(object sender, RoutedEventArgs e) => _vm?.PrevMonth();
        private void OnMonthNext(object sender, RoutedEventArgs e) => _vm?.NextMonth();

        private void BuildLeftTable()
        {
            if (_vm is null) return;
            LeftTableHost.Children.Clear();

            int year = _vm.SelectedYear;
            int monthIndex = _vm.SelectedMonthIndex;
            int month = monthIndex + 1;
            int daysInMonth = DateTime.DaysInMonth(year, month);
            int rowsCount = _vm.DoctorRows.Count + 1;

            var grid = new Grid();
            _leftGrid = grid;

            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(NameColWidth) });
            for (int d = 1; d <= daysInMonth; d++)
                grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(DayColWidth) });

            for (int r = 0; r < rowsCount; r++)
                grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(RowHeight) });

            var borderBrush = new SolidColorBrush(Color.FromArgb(0x18, 0x00, 0x00, 0x00));
            var weekendFill = new SolidColorBrush(Color.FromArgb(0x0A, 0x00, 0x00, 0x00));

            var hdrName = new Border { BorderBrush = borderBrush, BorderThickness = new Thickness(0, 0, 1, 1) };
            Grid.SetRow(hdrName, 0); Grid.SetColumn(hdrName, 0);
            hdrName.Child = new TextBlock { Text = "Dyżurny", FontWeight = Microsoft.UI.Text.FontWeights.SemiBold, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(8, 0, 8, 0) };
            grid.Children.Add(hdrName);

            for (int d = 1; d <= daysInMonth; d++)
            {
                var date = new DateTime(year, month, d);
                bool weekend = date.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday;

                var cell = new Border
                {
                    BorderBrush = borderBrush,
                    BorderThickness = new Thickness(0, 0, 1, 1),
                    Background = weekend ? weekendFill : null
                };
                Grid.SetRow(cell, 0); Grid.SetColumn(cell, d);
                cell.Child = new TextBlock
                {
                    Text = $"{d:00}\n{DowPl(date.DayOfWeek)}",
                    TextAlignment = TextAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center,
                    LineHeight = 14,
                    Padding = new Thickness(0, 4, 0, 0)
                };
                grid.Children.Add(cell);
            }

            for (int r = 0; r < _vm.DoctorRows.Count; r++)
            {
                var doctor = _vm.DoctorRows[r];
                int row = r + 1;

                var nameCell = new Border { BorderBrush = borderBrush, BorderThickness = new Thickness(0, 0, 1, 1) };
                Grid.SetRow(nameCell, row); Grid.SetColumn(nameCell, 0);
                nameCell.Child = new TextBlock
                {
                    Text = doctor.Name,
                    VerticalAlignment = VerticalAlignment.Center,
                    Margin = new Thickness(8, 0, 8, 0),
                    Opacity = doctor.HasDeclarations ? 1.0 : 0.6
                };
                grid.Children.Add(nameCell);

                for (int d = 1; d <= daysInMonth; d++)
                {
                    var date = new DateTime(year, month, d);
                    bool weekend = date.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday;

                    var cell = new Border
                    {
                        BorderBrush = borderBrush,
                        BorderThickness = new Thickness(0, 0, 1, 1),
                        Background = weekend ? weekendFill : null
                    };
                    Grid.SetRow(cell, row); Grid.SetColumn(cell, d);

                    var entry = _vm.TryGetEntry(doctor.Name, year, monthIndex, d - 1);
                    FrameworkElement content;

                    if (!entry.has ||
                        (entry.mode == Models.DayMode.Full24 && string.IsNullOrEmpty(entry.full)) ||
                        (entry.mode == Models.DayMode.Split12 && string.IsNullOrEmpty(entry.day) && string.IsNullOrEmpty(entry.night)))
                    {
                        content = new TextBlock { Text = "", HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
                    }
                    else if (entry.mode == Models.DayMode.Full24)
                    {
                        content = new TextBlock { Text = entry.full, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center, FontSize = 14 };
                    }
                    else
                    {
                        var g = new Grid();
                        g.RowDefinitions.Add(new RowDefinition());
                        g.RowDefinitions.Add(new RowDefinition());
                        g.Children.Add(new Border
                        {
                            BorderBrush = new SolidColorBrush(Color.FromArgb(0x50, 0x00, 0x00, 0x00)),
                            BorderThickness = new Thickness(0, 0, 0, 1),
                            VerticalAlignment = VerticalAlignment.Center
                        });
                        var tb1 = new TextBlock { Text = entry.day, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center, FontSize = 12 };
                        var tb2 = new TextBlock { Text = entry.night, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center, FontSize = 12 };
                        Grid.SetRow(tb1, 0); Grid.SetRow(tb2, 1);
                        g.Children.Add(tb1); g.Children.Add(tb2);
                        content = g;
                    }

                    cell.Child = content;
                    grid.Children.Add(cell);
                }
            }

            LeftTableHost.Children.Add(grid);
            ApplyLeftTableScale();
        }

        private void ApplyLeftTableScale()
        {
            if (_vm is null || _leftGrid is null) return;

            int year = _vm.SelectedYear;
            int month = _vm.SelectedMonthIndex + 1;
            int days = DateTime.DaysInMonth(year, month);
            int rowsCount = _vm.DoctorRows.Count + 1;

            double requiredW = NameColWidth + days * DayColWidth;
            double requiredH = rowsCount * RowHeight;

            double availW = Math.Max(0, LeftTableHost.ActualWidth);
            double availH = Math.Max(0, LeftTableHost.ActualHeight);

            double scaleX = availW > 0 ? Math.Min(1.0, availW / requiredW) : 1.0;
            double scaleY = availH > 0 ? Math.Min(1.0, availH / requiredH) : 1.0;
            double scale = Math.Min(scaleX, scaleY);

            _leftGrid.RenderTransform = new ScaleTransform { ScaleX = scale, ScaleY = scale };
            _leftGrid.RenderTransformOrigin = new Point(0, 0);
        }

        private static string DowPl(DayOfWeek dow) => dow switch
        {
            DayOfWeek.Monday => "Pon",
            DayOfWeek.Tuesday => "Wto",
            DayOfWeek.Wednesday => "Śro",
            DayOfWeek.Thursday => "Czw",
            DayOfWeek.Friday => "Pią",
            DayOfWeek.Saturday => "Sob",
            DayOfWeek.Sunday => "Nie",
            _ => ""
        };
    }
}
