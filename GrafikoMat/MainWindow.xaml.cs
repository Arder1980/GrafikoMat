using GrafikoMat.Common;
using GrafikoMat.Models;
using GrafikoMat.ViewModels;
using GrafikoMat.Views;
using Microsoft.UI;
using Microsoft.UI.Composition.SystemBackdrops;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows.Input;
using Windows.Graphics;
using Windows.UI;
using WinRT.Interop;

namespace GrafikoMat
{
    public sealed partial class MainWindow : Window
    {
        private const int MIN_W = 1280;
        private const int MIN_H = 720;

        private AppWindow? _appWindow;

        public MainViewModel ViewModel { get; }
        public ObservableCollection<UiAction> Actions { get; } = new();

        private readonly DashboardView _dashboardView = new();
        private readonly DeclarationsView _declarationsView = new();

        public MainWindow()
        {
            ViewModel = new MainViewModel();
            InitializeComponent();

            this.ExtendsContentIntoTitleBar = true;
            this.SetTitleBar(TopBarRow);

            InitAppWindow();
            SetupBackdrop();
            EnforceMinSize();

            // start: dashboard w Viewporcie
            _dashboardView.Attach(ViewModel);
            ViewportPresenter.Content = _dashboardView;
            BuildActionsForDashboard();

            // hooki z edytora deklaracji
            _declarationsView.SaveRequested += OnDeclSave;
            _declarationsView.SaveAndCloseRequested += OnDeclSaveAndClose;
            _declarationsView.CloseRequested += OnDeclCloseOnly;

            this.SizeChanged += (_, __) => { SetupBackdrop(); EnforceMinSize(); };
            this.Activated += (_, __) => SetupBackdrop();
        }

        // ==== Backdrop/AppWindow ====
        private void InitAppWindow()
        {
            var hwnd = WindowNative.GetWindowHandle(this);
            var windowId = Microsoft.UI.Win32Interop.GetWindowIdFromWindow(hwnd);
            _appWindow = AppWindow.GetFromWindowId(windowId);

            if (_appWindow is not null)
            {
                _appWindow.Title = string.Empty;
                _appWindow.Resize(new SizeInt32(MIN_W, MIN_H));
                try { _appWindow.TitleBar.ExtendsContentIntoTitleBar = true; } catch { }
            }
        }

        private void SetupBackdrop()
        {
            bool isMaximized = IsWindowMaximized();
            if (!isMaximized)
            {
                try { RootGrid.Background = new SolidColorBrush(Colors.Transparent); SystemBackdrop = new DesktopAcrylicBackdrop(); }
                catch { SystemBackdrop = new MicaBackdrop(); }
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
            return new SolidColorBrush(Color.FromArgb(0xFF, 0xF7, 0xF7, 0xF7));
        }

        [DllImport("user32.dll")] private static extern bool IsZoomed(IntPtr hWnd);
        private bool IsWindowMaximized()
        {
            var hwnd = WindowNative.GetWindowHandle(this);
            return IsZoomed(hwnd);
        }

        private void EnforceMinSize()
        {
            if (_appWindow is null) return;
            var size = _appWindow.Size;
            int nw = size.Width < MIN_W ? MIN_W : size.Width;
            int nh = size.Height < MIN_H ? MIN_H : size.Height;
            if (nw != size.Width || nh != size.Height)
                _appWindow.Resize(new SizeInt32(nw, nh));
        }

        // ==== Pasek akcji ====
        private void BuildActionsForDashboard()
        {
            Actions.Clear();
            Actions.Add(new UiAction("Ustawienia", new RelayCommand(_ => SwitchToSettings())));
            Actions.Add(new UiAction("Dodaj deklaracje dyżurowe", new RelayCommand(_ => SwitchToDeclarations())));
            Actions.Add(new UiAction("Zarządzanie dyżurnymi", new RelayCommand(_ => SwitchToManage())));
            Actions.Add(new UiAction("Generuj grafik", new RelayCommand(_ => GenerateRosterPlaceholder())));
            Actions.Add(new UiAction("Eksportuj...", new RelayCommand(_ => ExportPlaceholder())));
        }

        private void BuildActionsForDeclarations()
        {
            Actions.Clear();
            Actions.Add(new UiAction("Wstecz", new RelayCommand(_ => SwitchToDashboard())));
            Actions.Add(new UiAction("Zapisz", new RelayCommand(_ => _declarationsView.TriggerSave())));
            Actions.Add(new UiAction("Zapisz i zamknij", new RelayCommand(_ => _declarationsView.TriggerSaveAndClose())));
        }

        private void BuildActionsForSettings()
        {
            Actions.Clear();
            Actions.Add(new UiAction("Wstecz", new RelayCommand(_ => SwitchToDashboard())));
            Actions.Add(new UiAction("Zapisz ustawienia", new RelayCommand(_ => SaveSettingsPlaceholder())));
        }

        private void BuildActionsForManage()
        {
            Actions.Clear();
            Actions.Add(new UiAction("Wstecz", new RelayCommand(_ => SwitchToDashboard())));
            Actions.Add(new UiAction("Dodaj dyżurnego", new RelayCommand(_ => AddDoctorPlaceholder())));
        }

        // ==== Przełączanie widoków ====
        private void SwitchToDashboard()
        {
            _dashboardView.Attach(ViewModel);
            ViewportPresenter.Content = _dashboardView;
            BuildActionsForDashboard();
        }

        private void SwitchToDeclarations()
        {
            var names = ViewModel.DoctorRows.Select(d => d.Name).ToArray();
            _declarationsView.LoadContext(ViewModel.SelectedYear, ViewModel.SelectedMonthIndex, names, selectedDoctorIndex: 0);
            ViewportPresenter.Content = _declarationsView;
            BuildActionsForDeclarations();
        }

        private void SwitchToSettings()
        {
            ViewportPresenter.Content = new TextBlock { Text = "Ustawienia (w przygotowaniu)", Margin = new Thickness(12) };
            BuildActionsForSettings();
        }

        private void SwitchToManage()
        {
            ViewportPresenter.Content = new TextBlock { Text = "Zarządzanie dyżurnymi (w przygotowaniu)", Margin = new Thickness(12) };
            BuildActionsForManage();
        }

        // ==== Callbacks z DeclarationsView ====
        private void OnDeclSave(DoctorMonthDeclaration dm)
        {
            if (!string.IsNullOrWhiteSpace(dm.Doctor))
                ViewModel.ApplyDoctorMonth(dm);
        }

        private void OnDeclSaveAndClose(DoctorMonthDeclaration dm)
        {
            OnDeclSave(dm);
            SwitchToDashboard();
        }

        private void OnDeclCloseOnly() => SwitchToDashboard();

        // ==== Placeholdery brakujących metod ====
        private async void GenerateRosterPlaceholder()
            => await ShowInfo("Generuj grafik", "Tu będzie wywołanie algorytmu generowania grafiku oraz podgląd wyniku w prawej kolumnie.");

        private async void ExportPlaceholder()
            => await ShowInfo("Eksport", "Tu dodamy eksport do XLSX/PDF (np. ClosedXML + szablony).");

        private async void SaveSettingsPlaceholder()
            => await ShowInfo("Ustawienia", "Zapis ustawień (tryb 12h/24h, motyw, itp.) – w przygotowaniu.");

        private async void AddDoctorPlaceholder()
            => await ShowInfo("Dodaj dyżurnego", "Formularz dodania/edycji dyżurnego – w przygotowaniu.");

        private async Task ShowInfo(string title, string message)
        {
            var dlg = new ContentDialog
            {
                Title = title,
                Content = message,
                PrimaryButtonText = "OK",
                XamlRoot = RootGrid.XamlRoot
            };
            await dlg.ShowAsync();
        }
    }

    public sealed class UiAction
    {
        public string Label { get; }
        public ICommand Command { get; }
        public UiAction(string label, ICommand command) { Label = label; Command = command; }
    }
}
