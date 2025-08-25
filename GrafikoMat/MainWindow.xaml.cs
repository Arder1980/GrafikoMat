using GrafikoMat.ViewModels;
using Microsoft.UI;
using Microsoft.UI.Composition.SystemBackdrops;              // DesktopAcrylicBackdrop
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using System;
using System.ComponentModel;
using System.Runtime.InteropServices;                         // Win32 P/Invoke
using Windows.Foundation;
using Windows.Graphics;
using Windows.UI;                                           // Colors
using WinRT.Interop;

namespace GrafikoMat
{
    public sealed partial class MainWindow : Window
    {
        // Minimalny rozmiar
        private const int MIN_W = 1280;
        private const int MIN_H = 720;

        // Parametry tabeli (skalowanie)
        private const int NameColWidth = 220;
        private const int DayColWidth = 40;
        private const int RowHeight = 32;

        private AppWindow? _appWindow;
        private Grid? _leftGrid;

        public MainViewModel ViewModel { get; }

        public MainWindow()
        {
            ViewModel = new MainViewModel();
            this.InitializeComponent();

            // Własny topbar jako obszar przeciągania; systemowe przyciski zostają
            this.ExtendsContentIntoTitleBar = true;
            this.SetTitleBar(TopBarRow);

            InitAppWindow();
            SetupBackdrop();                 // „oszronione szkło”
            EnforceMinSize();

            BuildLeftTable();
            ViewModel.PropertyChanged += ViewModelOnPropertyChanged;

            // Reakcje
            this.SizeChanged += (_, __) => { SetupBackdrop(); EnforceMinSize(); ApplyLeftTableScale(); };
            this.Activated += (_, __) => SetupBackdrop();

            LeftTableHost.SizeChanged += (_, __) => ApplyLeftTableScale();
        }

        private void ViewModelOnPropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(MainViewModel.SelectedYear) ||
                e.PropertyName == nameof(MainViewModel.SelectedMonthIndex))
            {
                BuildLeftTable();
            }
        }

        private void InitAppWindow()
        {
            var hwnd = WindowNative.GetWindowHandle(this);
            var windowId = Microsoft.UI.Win32Interop.GetWindowIdFromWindow(hwnd);
            _appWindow = AppWindow.GetFromWindowId(windowId);

            if (_appWindow is not null)
            {
                _appWindow.Title = string.Empty; // chowamy tytuł – zostawiamy systemowe przyciski
                _appWindow.Resize(new SizeInt32(MIN_W, MIN_H));

                // Nie ukrywamy przycisków systemowych ani nie modyfikujemy ich kolorów.
                try { _appWindow.TitleBar.ExtendsContentIntoTitleBar = true; } catch { /* starsze SDK */ }
            }
        }

        /// Desktop Acrylic (bardziej przejrzysty niż Mica) gdy okno NIE jest zmaksymalizowane.
        private void SetupBackdrop()
        {
            bool isMaximized = IsWindowMaximized();

            if (!isMaximized)
            {
                try
                {
                    RootGrid.Background = new SolidColorBrush(Colors.Transparent);
                    SystemBackdrop = new DesktopAcrylicBackdrop();
                }
                catch
                {
                    SystemBackdrop = new MicaBackdrop(); // fallback
                }
            }
            else
            {
                SystemBackdrop = null;
                RootGrid.Background = GetLightFallbackBrush();
            }
        }

        private Brush GetLightFallbackBrush()
        {
            if (Application.Current.Resources.TryGetValue("SolidBackgroundFillColorBaseBrush", out var val) && val is Brush b)
                return b;
            return new SolidColorBrush(Color.FromArgb(0xFF, 0xF7, 0xF7, 0xF7)); // jaśniejsze (subtelnie)
        }

        // Minimalny rozmiar
        private void EnforceMinSize()
        {
            if (_appWindow is null) return;
            var size = _appWindow.Size;
            int w = size.Width, h = size.Height;
            int nw = w < MIN_W ? MIN_W : w;
            int nh = h < MIN_H ? MIN_H : h;
            if (nw != w || nh != h)
                _appWindow.Resize(new SizeInt32(nw, nh));
        }

        // Maksymalizacja – detekcja
        [DllImport("user32.dll")] private static extern bool IsZoomed(IntPtr hWnd);
        private bool IsWindowMaximized()
        {
            var hwnd = WindowNative.GetWindowHandle(this);
            return IsZoomed(hwnd);
        }

        // Skalowanie tabeli (bez przewijania poziomego)
        private void ApplyLeftTableScale()
        {
            if (_leftGrid is null) return;

            int year = ViewModel.SelectedYear;
            int month = ViewModel.SelectedMonthIndex + 1;
            int days = DateTime.DaysInMonth(year, month);
            int rowsCount = ViewModel.DoctorRows.Count + 1;

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

        // Budowa tabeli (nagłówek z dniami, wiersze lekarzy)
        private void BuildLeftTable()
        {
            LeftTableHost.Children.Clear();

            int year = ViewModel.SelectedYear;
            int month = ViewModel.SelectedMonthIndex + 1;
            int daysInMonth = DateTime.DaysInMonth(year, month);
            int rowsCount = ViewModel.DoctorRows.Count + 1;

            var grid = new Grid();
            _leftGrid = grid;

            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(NameColWidth) });
            for (int d = 1; d <= daysInMonth; d++)
                grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(DayColWidth) });

            for (int r = 0; r < rowsCount; r++)
                grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(RowHeight) });

            var borderBrush = new SolidColorBrush(Color.FromArgb(0x18, 0x00, 0x00, 0x00)); // subtelniejsze (25% mniej krycia)
            var weekendFill = new SolidColorBrush(Color.FromArgb(0x0A, 0x00, 0x00, 0x00)); // delikatniejsze tło weekendów

            // nagłówek
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

            // wiersze lekarzy
            for (int r = 0; r < ViewModel.DoctorRows.Count; r++)
            {
                var doctor = ViewModel.DoctorRows[r];
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
                    cell.Child = new TextBlock { Text = "", HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
                    grid.Children.Add(cell);
                }
            }

            LeftTableHost.Children.Add(grid);
            ApplyLeftTableScale();
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

        // ======== Nawigacja rok/miesiąc ========
        private void OnYearPrev(object sender, RoutedEventArgs e) => ViewModel.PrevYear();
        private void OnYearNext(object sender, RoutedEventArgs e) => ViewModel.NextYear();
        private void OnMonthPrev(object sender, RoutedEventArgs e) => ViewModel.PrevMonth();
        private void OnMonthNext(object sender, RoutedEventArgs e) => ViewModel.NextMonth();
    }
}
