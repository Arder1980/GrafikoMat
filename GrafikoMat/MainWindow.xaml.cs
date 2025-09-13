using CommunityToolkit.Mvvm.Input;
using GrafikoMat.Common;
using GrafikoMat.Core.Data;
using GrafikoMat.Core.Repositories;
using GrafikoMat.Models;
using GrafikoMat.Repositories;
using GrafikoMat.Services;
using GrafikoMat.ViewModels;
using GrafikoMat.Views;
using Microsoft.UI;
using Microsoft.UI.Composition;
using Microsoft.UI.Composition.SystemBackdrops;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Animation;
using Microsoft.UI.Xaml.Media.Animation;
using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Windows.Graphics;
using Windows.UI;
using WinRT;
using WinRT.Interop;

namespace GrafikoMat
{
    public sealed partial class MainWindow : Window
    {
        private const int MIN_W = 1600;
        private const int MIN_H = 1000;
        private AppWindow? _appWindow;
        public MainViewModel ViewModel { get; }
        public ObservableCollection<UiAction> Actions { get; } = new();

        // ZMIANA: Usunięto pole _declarationsView. Będzie tworzone w locie.
        private readonly DashboardView _dashboardView = new();
        private SettingsView? _settingsView;
        private ManagementView? _managementView;

        private bool _isAnimating;
        private bool _isClosing;
        private Storyboard? _activeStoryboard;
        private delegate IntPtr WndProc(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);
        private WndProc? _newWndProc;
        private IntPtr _oldWndProc;
        private bool _isInitialSizeSet = false;

        private readonly SettingsService _settingsService;
        private readonly SupabaseService _supabaseService;
        private AppSettings? _appSettings;
        private IDoctorRepository? _doctorRepository;
        private IUnitRepository? _unitRepository;
        private IAssignmentRepository? _assignmentRepository;

        DesktopAcrylicController? _acrylicController;
        SystemBackdropConfiguration? _backdropConfiguration;

        public MainWindow()
        {
            this.InitializeComponent();
            _settingsService = new SettingsService();
            _supabaseService = new SupabaseService();

            ViewModel = new MainViewModel();
            ViewModel.SetSettingsService(_settingsService);

            this.ExtendsContentIntoTitleBar = true;
            this.SetTitleBar(DragBar);

            InitAppWindow();
            ApplyTitleBarMenuStyling();
            RootGrid.Loaded += async (s, e) => await InitializeApplicationAsync();
            _dashboardView.Attach(ViewModel);
            ViewModel.PropertyChanged += OnMainViewModelPropertyChanged;

            // ZMIANA: Usunięto subskrypcje zdarzeń dla _declarationsView w konstruktorze

            this.Activated += OnWindowActivated;
            this.Closed += OnWindowClosed;
            (this.Content as FrameworkElement).ActualThemeChanged += OnActualThemeChanged;
        }
        private async void OnMainViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(MainViewModel.EngineName) || e.PropertyName == nameof(MainViewModel.PriorityOrder))
            {
                // Animacja wygaszenia stopki
                var sb = new Storyboard();
                var fadeOut = new DoubleAnimation { To = 0, Duration = TimeSpan.FromMilliseconds(150) };
                Storyboard.SetTarget(fadeOut, BottomStatusBar);
                Storyboard.SetTargetProperty(fadeOut, "Opacity");
                sb.Children.Add(fadeOut);

                var tcs = new TaskCompletionSource();
                sb.Completed += (_, _) => tcs.TrySetResult();
                sb.Begin();
                await tcs.Task;

                // W tym momencie tekst w tle został już podmieniony przez ViewModel

                // Animacja pojawienia się stopki z nowym tekstem
                sb = new Storyboard();
                var fadeIn = new DoubleAnimation { To = 1, Duration = TimeSpan.FromMilliseconds(150) };
                Storyboard.SetTarget(fadeIn, BottomStatusBar);
                Storyboard.SetTargetProperty(fadeIn, "Opacity");
                sb.Children.Add(fadeIn);
                sb.Begin();
            }
        }

        private async Task InitializeApplicationAsync()
        {
            await ReloadSettingsAndServicesAsync();
            if (_appSettings == null || string.IsNullOrWhiteSpace(_appSettings.SupabaseUrl) || string.IsNullOrWhiteSpace(_appSettings.SupabaseAnonKey))
            {
                var setupView = new Views.Settings.ConnectionSettingsView();
                setupView.Initialize(_settingsService, _appSettings ?? new AppSettings());
                setupView.ReloadRequired += async () => { StartupOverlayContent.Content = null; await InitializeApplicationAsync(); };
                StartupOverlayContent.Content = setupView;
                InitialLoadingOverlay.Visibility = Visibility.Collapsed;
                return;
            }
            var restored = await _supabaseService.RestoreSessionIfAnyAsync();
            if (!restored && !_supabaseService.IsAuthenticated)
            {
                bool loggedIn = await ShowLoginScreenAsync();
                if (!loggedIn) { this.Close(); return; }
            }
            if (_doctorRepository != null && _supabaseService.IsAuthenticated)
            {
                try
                {
                    var profile = await _doctorRepository.GetCurrentDoctorProfileAsync();
                    if (profile != null && profile.RequiresPasswordChange)
                    {
                        bool passwordChanged = await ShowForcePasswordChangeAsync();
                        if (!passwordChanged) { await _supabaseService.SignOutAsync(); this.Close(); return; }
                        await _doctorRepository.ClearPasswordChangeFlagAsync(profile.Id);
                    }
                }
                catch (Exception ex)
                {
                    await ShowInfo("Błąd krytyczny", $"Nie można wczytać profilu użytkownika: {ex.Message}");
                    await _supabaseService.SignOutAsync();
                    this.Close();
                    return;
                }
            }
            await LoadDataAndShowDashboardAsync();
            InitialLoadingOverlay.Visibility = Visibility.Collapsed;
            StartupOverlayContent.Content = null;
            await RunEntranceAnimationAsync();
        }

        private async Task RunEntranceAnimationAsync()
        {
            var sb = new Storyboard();
            var ease = new CubicEase { EasingMode = EasingMode.EaseOut };
            var duration = new Duration(TimeSpan.FromMilliseconds(400));
            void AddAnimation(UIElement target, string property, double to, double beginTimeMs)
            {
                var anim = new DoubleAnimation { To = to, Duration = duration, EasingFunction = ease, BeginTime = TimeSpan.FromMilliseconds(beginTimeMs) };
                Storyboard.SetTarget(anim, target);
                Storyboard.SetTargetProperty(anim, property);
                sb.Children.Add(anim);
            }
            AddAnimation(TopBarRow, "Opacity", 1, 0);
            AddAnimation(TopBarRow, "(UIElement.RenderTransform).(TranslateTransform.Y)", 0, 0);
            AddAnimation(UnitSelectionPanel, "Opacity", 1, 100);
            AddAnimation(UnitSelectionPanel, "(UIElement.RenderTransform).(TranslateTransform.Y)", 0, 100);
            AddAnimation(MainContentPanel, "Opacity", 1, 200);
            AddAnimation(MainContentPanel, "(UIElement.RenderTransform).(TranslateTransform.Y)", 0, 200);
            AddAnimation(ActionButtonsPanel, "Opacity", 1, 250);
            AddAnimation(BottomStatusBar, "Opacity", 1, 300);
            AddAnimation(BottomStatusBar, "(UIElement.RenderTransform).(TranslateTransform.Y)", 0, 300);
            var tcs = new TaskCompletionSource();
            sb.Completed += (_, _) => tcs.TrySetResult();
            sb.Begin();
            await tcs.Task;
            SetTitleBar(DragBar);
        }

        private async Task ReloadSettingsAndServicesAsync()
        {
            _appSettings = await _settingsService.LoadSettingsAsync();
            if (_appSettings != null && !string.IsNullOrWhiteSpace(_appSettings.SupabaseUrl) && !string.IsNullOrWhiteSpace(_appSettings.SupabaseAnonKey))
            {
                _supabaseService.Initialize(_appSettings.SupabaseUrl, _appSettings.SupabaseAnonKey);
                await _supabaseService.RestoreSessionIfAnyAsync();
                if (_supabaseService.Client != null)
                {
                    _doctorRepository = new SupabaseDoctorRepository(_supabaseService.Client);
                    _unitRepository = new SupabaseUnitRepository(_supabaseService.Client);
                    _assignmentRepository = new SupabaseAssignmentRepository(_supabaseService.Client);
                }
            }
            else
            {
                _supabaseService.Initialize(string.Empty, string.Empty);
                _doctorRepository = null;
                _unitRepository = null;
                _assignmentRepository = null;
            }
            ViewModel.SetRepositories(_doctorRepository, _unitRepository, _assignmentRepository);
        }

        private async void RefreshDataServicesAsync()
        {
            await ReloadSettingsAndServicesAsync();
            if (ViewportCurrent.Content == _settingsView) { SwitchToSettings(forceRefresh: true); }
        }

        private async Task<bool> ShowLoginScreenAsync()
        {
            if (_supabaseService != null && _supabaseService.IsAuthenticated) return true;
            var tcs = new TaskCompletionSource<bool>();
            var loginView = new LoginView(_supabaseService!);
            loginView.CancelRequested += () => { LoginOverlay.Content = null; tcs.TrySetResult(false); };
            loginView.LoginSuccess += async () => { await _supabaseService.SaveCurrentSessionAsync(); LoginOverlay.Content = null; tcs.TrySetResult(true); };
            LoginOverlay.Content = loginView;
            return await tcs.Task;
        }

        private async Task<bool> ShowForcePasswordChangeAsync()
        {
            var tcs = new TaskCompletionSource<bool>();
            var changePasswordView = new ChangePasswordView(_supabaseService);
            changePasswordView.PasswordChangeSuccess += () => { LoginOverlay.Content = null; tcs.TrySetResult(true); };
            changePasswordView.PasswordChangeCancelled += () => { LoginOverlay.Content = null; tcs.TrySetResult(false); };
            LoginOverlay.Content = changePasswordView;
            return await tcs.Task;
        }

        private async Task LoadDataAndShowDashboardAsync()
        {
            if (_doctorRepository != null) { await ViewModel.LoadUserAndUnitDataAsync(); ViewModel.LoadDataForActiveUnit(); }
            ViewportCurrent.Content = _dashboardView;
            BuildActionsForDashboard();
            ResetViewportState();
        }

        private async void SwitchToSettings(bool forceRefresh = false)
        {
            if ((_isClosing || _appSettings == null) && !forceRefresh) return;
            _settingsView = new SettingsView();
            _settingsView.ReloadRequired += RefreshDataServicesAsync;
            _settingsView.Initialize(_unitRepository, _settingsService, _appSettings);
            if (!forceRefresh) { await AnimateToAsync(_settingsView, forward: true); } else { ViewportCurrent.Content = _settingsView; }
            BuildActionsForSettings();
        }

        private async void SwitchToManagement()
        {
            if (_isClosing) return;
            if (_doctorRepository == null || _unitRepository == null || _assignmentRepository == null) { await ShowInfo("Brak aktywnego połączenia", "Sprawdź konfigurację połączenia w ustawieniach."); return; }
            _managementView = new ManagementView(_doctorRepository, _unitRepository, _assignmentRepository, _supabaseService, this.DispatcherQueue);
            await AnimateToAsync(_managementView, forward: true);
            BuildActionsForManage();
        }

        private void BuildActionsForDashboard()
        {
            Actions.Clear();
            Actions.Add(new UiAction("Ustawienia", new RelayCommand(() => SwitchToSettings())));
            Actions.Add(new UiAction("Zarządzanie dyżurnymi", new RelayCommand(() => SwitchToManagement())));
            Actions.Add(new UiAction("Edytuj deklaracje dyżurowe", new RelayCommand(() => SwitchToDeclarations())));
            Actions.Add(new UiAction("Generuj grafik", new AsyncRelayCommand(ViewModel.GenerateScheduleAsync)));
            Actions.Add(new UiAction("Eksportuj...", new RelayCommand(ExportPlaceholder)));
        }

        private void BuildActionsForDeclarations(DeclarationsView view)
        {
            Actions.Clear();
            Actions.Add(new UiAction("Anuluj", new RelayCommand(view.OnDeclCloseOnly)));
            Actions.Add(new UiAction("Wyczyść zaznaczenie", new RelayCommand(() => view.ViewModel.ClearSelectionCommand.Execute(null))));
            Actions.Add(new UiAction("Zapisz", new RelayCommand(() => view.ViewModel.SaveCommand.Execute(null))));
            Actions.Add(new UiAction("Zapisz i zamknij", new RelayCommand(view.OnDeclSaveAndCloseOnly)));
        }

        private void BuildActionsForSettings()
        {
            Actions.Clear();
            Actions.Add(new UiAction("Wstecz", new RelayCommand(() => SwitchToDashboard())));
        }

        private void BuildActionsForManage()
        {
            Actions.Clear();
            Actions.Add(new UiAction("Wstecz", new RelayCommand(() => SwitchToDashboard())));
        }

        private async void SwitchToDashboard()
        {
            if (_isClosing || _isAnimating) return;
            await ViewModel.UpdateFooterFromSettingsAsync();
            await AnimateToAsync(_dashboardView, false);
            BuildActionsForDashboard();
        }

        // ZMIANA: Cała metoda została gruntownie przebudowana
        private async void SwitchToDeclarations()
        {
            if (_isClosing || _isAnimating) return;
            var frozenYear = ViewModel.SelectedYear;
            var frozenMonthIndex = ViewModel.SelectedMonthIndex;

            var doctorsForUnit = ViewModel.DoctorRows.Select(dr => dr.Profile).ToList();
            if (!doctorsForUnit.Any())
            {
                _ = ShowInfo("Brak lekarzy", "Brak aktywnych lekarzy przypisanych do tej jednostki.");
                return;
            }

            int initialIndex = 0;
            if (!ViewModel.IsCurrentUserAdmin)
            {
                try
                {
                    if (_doctorRepository != null)
                    {
                        var currentProfile = await _doctorRepository.GetCurrentDoctorProfileAsync();
                        if (currentProfile != null)
                        {
                            var idx = doctorsForUnit.FindIndex(d => d.Id == currentProfile.Id);
                            if (idx >= 0) initialIndex = idx;
                        }
                    }
                }
                catch { initialIndex = 0; }
            }

            var declarationsVm = new DeclarationsViewModel(
                frozenYear,
                frozenMonthIndex,
                doctorsForUnit,
                initialIndex,
                ViewModel.Declarations,
                ViewModel.IsCurrentUserAdmin,
                () => {
                    ViewModel.RefreshDeclarationsForDashboard();
                }
            );

            // ZAWSZE tworzymy nową, świeżą instancję widoku
            var declarationsView = new DeclarationsView();

            // Definiujemy, co się stanie po zamknięciu widoku (np. przez przycisk "Anuluj")
            void DeclCloseHandler() => SwitchToDashboard();
            void DeclSaveAndCloseHandler()
            {
                declarationsView.ViewModel.SaveCommand.Execute(null);
                SwitchToDashboard();
            }

            // Subskrybujemy zdarzenia dla tej konkretnej instancji
            declarationsView.CloseRequested += DeclCloseHandler;
            declarationsView.SaveAndCloseRequested += DeclSaveAndCloseHandler;

            declarationsView.AttachViewModel(declarationsVm);

            await AnimateToAsync(declarationsView, true);
            BuildActionsForDeclarations(declarationsView);
        }

        private static void DetachFromParent(FrameworkElement el)
        {
            if (el.Parent is ContentControl cc) cc.Content = null;
            else if (el.Parent is Border b) b.Child = null;
            else if (el.Parent is Panel p) p.Children.Remove(el);
        }

        private static FrameworkElement? TryGetHeader(object? content)
        {
            if (content is FrameworkElement fe) return fe.FindName("ViewHeader") as FrameworkElement;
            return null;
        }

        private void ResetViewportState()
        {
            var cur = ViewportCurrent;
            var nxt = ViewportNext;
            var curT = (cur.RenderTransform as TranslateTransform) ?? new TranslateTransform();
            var nxtT = (nxt.RenderTransform as TranslateTransform) ?? new TranslateTransform();
            cur.RenderTransform = curT;
            nxt.RenderTransform = nxtT;
            curT.X = 0;
            nxtT.X = 0;
            cur.Opacity = 1;
            nxt.Opacity = 0;
            var ch = TryGetHeader(cur.Content);
            var nh = TryGetHeader(nxt.Content);
            if (ch != null) { var t = (ch.RenderTransform as TranslateTransform) ?? new TranslateTransform(); t.X = 0; ch.RenderTransform = t; }
            if (nh != null) { var t = (nh.RenderTransform as TranslateTransform) ?? new TranslateTransform(); t.X = 0; nh.RenderTransform = t; }
        }

        private async Task AnimateToAsync(FrameworkElement nextView, bool forward)
        {
            if (_isClosing) { ViewportCurrent.Content = nextView; ResetViewportState(); return; }
            if (ReferenceEquals(ViewportCurrent.Content, nextView)) return;
            if (_isAnimating)
            {
                DetachFromParent(nextView);
                _activeStoryboard?.Stop();
                _activeStoryboard = null;
                ViewportNext.Content = null;
                ViewportCurrent.Content = nextView;
                ResetViewportState();
                return;
            }
            _isAnimating = true;
            var curPresenter = ViewportCurrent;
            var nxtPresenter = ViewportNext;
            DetachFromParent(nextView);
            ResetViewportState();
            var curTransform = (curPresenter.RenderTransform as TranslateTransform)!;
            var nxtTransform = (nxtPresenter.RenderTransform as TranslateTransform)!;
            var curHeader = TryGetHeader(curPresenter.Content);
            var nxtHeader = TryGetHeader(nextView);
            TranslateTransform? curHeaderTransform = null;
            TranslateTransform? nxtHeaderTransform = null;
            if (curHeader != null) { curHeaderTransform = (curHeader.RenderTransform as TranslateTransform) ?? new TranslateTransform(); curHeader.RenderTransform = curHeaderTransform; }
            if (nxtHeader != null) { nxtHeaderTransform = (nxtHeader.RenderTransform as TranslateTransform) ?? new TranslateTransform(); nxtHeader.RenderTransform = nxtHeaderTransform; }
            double offset = 64;
            double fromNext = forward ? +offset : -offset;
            double toCur = forward ? -offset : +offset;
            nxtTransform.X = fromNext;
            nxtPresenter.Opacity = 0;
            curTransform.X = 0;
            curPresenter.Opacity = 1;
            if (nxtHeaderTransform != null) nxtHeaderTransform.X = fromNext * 0.5;
            if (curHeaderTransform != null) curHeaderTransform.X = 0;
            nxtPresenter.Content = nextView;
            var sb = new Storyboard();
            _activeStoryboard = sb;
            var dur = TimeSpan.FromMilliseconds(280);
            var easeIn = new CubicEase { EasingMode = EasingMode.EaseIn };
            var easeOut = new CubicEase { EasingMode = EasingMode.EaseOut };
            var curX = new DoubleAnimation { From = 0, To = toCur, Duration = dur, EasingFunction = easeIn };
            Storyboard.SetTarget(curX, curPresenter);
            Storyboard.SetTargetProperty(curX, "(UIElement.RenderTransform).(TranslateTransform.X)");
            sb.Children.Add(curX);
            var curOp = new DoubleAnimation { From = 1, To = 0, Duration = dur, EasingFunction = easeIn };
            Storyboard.SetTarget(curOp, curPresenter);
            Storyboard.SetTargetProperty(curOp, "Opacity");
            sb.Children.Add(curOp);
            var nxtX = new DoubleAnimation { From = fromNext, To = 0, Duration = dur, EasingFunction = easeOut };
            Storyboard.SetTarget(nxtX, nxtPresenter);
            Storyboard.SetTargetProperty(nxtX, "(UIElement.RenderTransform).(TranslateTransform.X)");
            sb.Children.Add(nxtX);
            var nxtOp = new DoubleAnimation { From = 0, To = 1, Duration = dur, EasingFunction = easeOut };
            Storyboard.SetTarget(nxtOp, nxtPresenter);
            Storyboard.SetTargetProperty(nxtOp, "Opacity");
            sb.Children.Add(nxtOp);
            if (curHeaderTransform != null)
            {
                var curHX = new DoubleAnimation { From = 0, To = toCur * 0.5, Duration = dur, EasingFunction = easeIn };
                Storyboard.SetTarget(curHX, curHeader);
                Storyboard.SetTargetProperty(curHX, "(UIElement.RenderTransform).(TranslateTransform.X)");
                sb.Children.Add(curHX);
            }
            if (nxtHeaderTransform != null)
            {
                var nxtHX = new DoubleAnimation { From = fromNext * 0.5, To = 0, Duration = dur, EasingFunction = easeOut };
                Storyboard.SetTarget(nxtHX, nxtHeader);
                Storyboard.SetTargetProperty(nxtHX, "(UIElement.RenderTransform).(TranslateTransform.X)");
                sb.Children.Add(nxtHX);
            }
            var tcs = new TaskCompletionSource<bool>();
            sb.Completed += (_, __) => { _activeStoryboard = null; tcs.TrySetResult(true); };
            try { sb.Begin(); await tcs.Task; } catch { }
            if (_isClosing) return;
            ViewportNext.Content = null;
            ViewportCurrent.Content = nextView;
            ResetViewportState();
            _isAnimating = false;
        }

        private void TitleBarMenuButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is FrameworkElement fe) FlyoutBase.ShowAttachedFlyout(fe);
        }

        private void OnWindowClosed(object sender, WindowEventArgs args)
        {
            _isClosing = true;
            try { _activeStoryboard?.Stop(); } catch { }
            _activeStoryboard = null;
            _isAnimating = false;
            this.Activated -= OnWindowActivated;
            this.Closed -= OnWindowClosed;
            (this.Content as FrameworkElement).ActualThemeChanged -= OnActualThemeChanged;
            if (_appWindow != null) _appWindow.Changed -= OnAppWindowChanged;

            // ZMIANA: Usunięto odpinanie zdarzeń od nieistniejącego już pola _declarationsView

            if (_settingsView != null) _settingsView.ReloadRequired -= RefreshDataServicesAsync;
            try { ViewportNext.Content = null; } catch { }
            try { ViewportCurrent.Content = null; } catch { }
            if (_acrylicController != null) { _acrylicController.Dispose(); _acrylicController = null; }
            this.SystemBackdrop = null;
        }

        // ZMIANA: Usunięto metody OnDeclCloseOnly i OnDeclSaveAndCloseOnly
        // Logika została przeniesiona do metody SwitchToDeclarations

        private async void ExportPlaceholder() => await ShowInfo("Eksport", "Tu dodamy eksport do XLSX/PDF (np. ClosedXML + szablony).");
        private async Task ShowInfo(string title, string message)
        {
            var dlg = App.CreateThemedDialog();
            dlg.Title = title;
            dlg.Content = message;
            dlg.PrimaryButtonText = "OK";
            await dlg.ShowAsync();
        }

        private void ApplyTitleBarMenuStyling()
        {
            if (_appWindow?.TitleBar is not AppWindowTitleBar titleBar) return;
            var pad = TopBarRow.Padding;
            TopBarRow.Padding = new Thickness(pad.Left, pad.Top, titleBar.RightInset, pad.Bottom);
            TitleBarMenuButton.Height = TopBarRow.Height;
            TitleBarMenuButton.MinWidth = 46;
            TitleBarMenuButton.Resources["ControlCornerRadius"] = new CornerRadius(0);

            var isLightTheme = (this.Content as FrameworkElement)?.ActualTheme == ElementTheme.Light;
            var baseFgColor = isLightTheme ? Colors.Black : Colors.White;
            var bgHover = isLightTheme ? Color.FromArgb(20, 0, 0, 0) : Color.FromArgb(20, 255, 255, 255);
            var bgPressed = isLightTheme ? Color.FromArgb(40, 0, 0, 0) : Color.FromArgb(40, 255, 255, 255);
            var fgInactive = isLightTheme ? Color.FromArgb(0x99, 0, 0, 0) : Color.FromArgb(0x99, 0xFF, 0xFF, 0xFF);

            titleBar.ButtonForegroundColor = baseFgColor;
            titleBar.ButtonHoverForegroundColor = baseFgColor;
            titleBar.ButtonHoverBackgroundColor = bgHover;
            titleBar.ButtonPressedForegroundColor = baseFgColor;
            titleBar.ButtonPressedBackgroundColor = bgPressed;
            titleBar.ButtonInactiveForegroundColor = fgInactive;

            TitleBarMenuButton.Foreground = new SolidColorBrush(baseFgColor);
            TitleBarMenuButton.Resources["ButtonForegroundPointerOver"] = new SolidColorBrush(baseFgColor);
            TitleBarMenuButton.Resources["ButtonForegroundPressed"] = new SolidColorBrush(baseFgColor);
            TitleBarMenuButton.Resources["ButtonBackgroundPointerOver"] = new SolidColorBrush(bgHover);
            TitleBarMenuButton.Resources["ButtonBackgroundPressed"] = new SolidColorBrush(bgPressed);
        }

        #region Window Setup and Win32 Interop
        private void TitleBarSettings_Click(object sender, RoutedEventArgs e) => SwitchToSettings();
        private void InitAppWindow()
        {
            var hwnd = WindowNative.GetWindowHandle(this);
            var windowId = Win32Interop.GetWindowIdFromWindow(hwnd);
            _appWindow = AppWindow.GetFromWindowId(windowId);
            if (_appWindow is not null)
            {
                _appWindow.Title = string.Empty;
                try { _appWindow.TitleBar.ExtendsContentIntoTitleBar = true; } catch { }
                _appWindow.Changed += OnAppWindowChanged;
                SubclassWindow(hwnd);
            }
        }
        private void OnActualThemeChanged(FrameworkElement sender, object args)
        {
            if (_backdropConfiguration != null) { TrySetSystemBackdrop(); }
            ApplyTitleBarMenuStyling();
        }
        private void TrySetSystemBackdrop()
        {
            if (_isClosing || !DesktopAcrylicController.IsSupported()) { return; }
            if (_backdropConfiguration == null) { _backdropConfiguration = new SystemBackdropConfiguration(); }
            if (this.Content is FrameworkElement rootElement)
            {
                _backdropConfiguration.Theme = rootElement.ActualTheme switch
                {
                    ElementTheme.Dark => SystemBackdropTheme.Dark,
                    ElementTheme.Light => SystemBackdropTheme.Light,
                    _ => SystemBackdropTheme.Default
                };
            }
            bool isMaximized = _appWindow?.Presenter is OverlappedPresenter p && p.State == OverlappedPresenterState.Maximized;
            if (!isMaximized)
            {
                if (_acrylicController == null)
                {
                    _acrylicController = new DesktopAcrylicController();
                    _acrylicController.AddSystemBackdropTarget(this.As<ICompositionSupportsSystemBackdrop>());
                    _acrylicController.SetSystemBackdropConfiguration(_backdropConfiguration);
                }
                RootGrid.Background = new SolidColorBrush(Colors.Transparent);
            }
            else
            {
                if (_acrylicController != null)
                {
                    _acrylicController.Dispose();
                    _acrylicController = null;
                }
                RootGrid.Background = (Brush)Application.Current.Resources["ApplicationPageBackgroundThemeBrush"];
            }
        }
        private void OnAppWindowChanged(AppWindow sender, AppWindowChangedEventArgs args)
        {
            if (args.DidPresenterChange && !_isClosing) { TrySetSystemBackdrop(); }
            ApplyTitleBarMenuStyling();
        }
        private void OnWindowActivated(object? sender, WindowActivatedEventArgs e)
        {
            if (!_isInitialSizeSet && _appWindow != null)
            {
                _appWindow.Resize(new SizeInt32(MIN_W, MIN_H));
                _isInitialSizeSet = true;
                TrySetSystemBackdrop();
            }
            if (_isClosing) return;
            if (_backdropConfiguration != null)
            {
                _backdropConfiguration.IsInputActive = e.WindowActivationState != WindowActivationState.Deactivated;
            }
            ApplyTitleBarMenuStyling();
        }
        private void SubclassWindow(IntPtr hwnd)
        {
            _newWndProc = new WndProc(AppWndProc);
            _oldWndProc = SetWindowLongPtr(hwnd, -4, Marshal.GetFunctionPointerForDelegate(_newWndProc));
        }
        private IntPtr AppWndProc(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam)
        {
            if (msg == 0x0024)
            {
                var minMaxInfo = Marshal.PtrToStructure<MINMAXINFO>(lParam);
                var dpi = GetDpiForWindow(hWnd);
                float scalingFactor = dpi / 96f;
                minMaxInfo.ptMinTrackSize.x = (int)(MIN_W * scalingFactor);
                minMaxInfo.ptMinTrackSize.y = (int)(MIN_H * scalingFactor);
                Marshal.StructureToPtr(minMaxInfo, lParam, true);
                return IntPtr.Zero;
            }
            return CallWindowProc(_oldWndProc, hWnd, msg, wParam, lParam);
        }
        private async void HelpMenuItem_Click(object sender, RoutedEventArgs e) => await ShowInfo("Pomoc", "Funkcjonalność w trakcie budowy. W tym miejscu zostanie wyświetlony system pomocy lub dokumentacja programu.");
        private async void AboutMenuItem_Click(object sender, RoutedEventArgs e) => await ShowInfo("O programie", $"GrafikoMat Dyżurowy v1.0 (Alpha)\n\nUżytkownik: Adam Lemanowicz\nElbląg, 24.12.1980");
        private async void SignOutMenuItem_Click(object sender, RoutedEventArgs e)
        {
            await _supabaseService.SignOutAsync();
            ViewModel.SetRepositories(null, null, null);
            _doctorRepository = null;
            _unitRepository = null;
            _assignmentRepository = null;
            InitialLoadingOverlay.Visibility = Visibility.Visible;
            await InitializeApplicationAsync();
        }

        [StructLayout(LayoutKind.Sequential)] public struct MINMAXINFO { public POINT ptReserved; public POINT ptMaxSize; public POINT ptMaxPosition; public POINT ptMinTrackSize; public POINT ptMaxTrackSize; }
        [StructLayout(LayoutKind.Sequential)] public struct POINT { public int x; public int y; }
        [DllImport("user32.dll", SetLastError = true)] private static extern IntPtr SetWindowLongPtr(IntPtr hWnd, int nIndex, IntPtr dwNewLong);
        [DllImport("user32.dll")] private static extern IntPtr CallWindowProc(IntPtr lpPrevWndFunc, IntPtr hWnd, uint uMsg, IntPtr wParam, IntPtr lParam);
        [DllImport("user32.dll")] private static extern uint GetDpiForWindow(IntPtr hWnd);
        #endregion
    }
}