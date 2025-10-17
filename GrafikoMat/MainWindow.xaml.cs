using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
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
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Animation;
using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows.Input;
using Windows.Graphics;
using Windows.UI;
using WinRT;
using WinRT.Interop;

namespace GrafikoMat
{
    public sealed partial class MainWindow : Window, IRecipient<SettingsHaveChangedMessage>
    {
        private const int MIN_W = 1600;
        private const int MIN_H = 1000;
        private AppWindow? _appWindow;
        public MainViewModel ViewModel { get; }
        public ObservableCollection<UiAction> ActionsLeft { get; } = new();
        public ObservableCollection<UiAction> ActionsRight { get; } = new();

        private readonly DashboardView _dashboardView = new();
        private SettingsView? _settingsView;
        private ManagementView? _managementView;
        private DeclarationsView? _currentDeclarationsView;

        private bool _isAnimating;
        private bool _isClosing;
        private Storyboard? _activeStoryboard;

        private int _previousUnitIndex = -1;

        private delegate IntPtr WndProc(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);
        private WndProc? _newWndProc;
        private IntPtr _oldWndProc;
        private GCHandle _wndProcGCHandle;

        private DesktopAcrylicController? _acrylicController;
        private SystemBackdropConfiguration? _backdropConfiguration;

        private readonly SettingsService _settingsService;
        private readonly SupabaseService _supabaseService;
        private AppSettings? _appSettings;
        private IDoctorRepository? _doctorRepository;
        private IUnitRepository? _unitRepository;
        private IAssignmentRepository? _assignmentRepository;

        public MainWindow()
        {
            this.InitializeComponent();
            _settingsService = new SettingsService();
            _supabaseService = new SupabaseService();

            ViewModel = new MainViewModel();
            ViewModel.SetSettingsService(_settingsService);
            ViewModel.PropertyChanged += OnMainViewModelPropertyChanged;

            this.ExtendsContentIntoTitleBar = true;
            this.SetTitleBar(DragBar);
            InitAppWindow();
            ApplyTitleBarMenuStyling();

            TryInitializeBackdropController();

            RootGrid.Loaded += async (s, e) => await InitializeApplicationAsync();
            _dashboardView.Attach(ViewModel);

            this.Activated += OnWindowActivated;
            this.Closed += OnWindowClosed;
            (this.Content as FrameworkElement).ActualThemeChanged += OnActualThemeChanged;

            WeakReferenceMessenger.Default.Register<SettingsHaveChangedMessage>(this);
        }

        public async void Receive(SettingsHaveChangedMessage message)
        {
            if (_settingsService != null)
            {
                _appSettings = await _settingsService.LoadSettingsAsync(forceReload: true);
            }
        }

        private void OnWindowActivated(object? sender, WindowActivatedEventArgs e)
        {
            if (_isClosing) return;

            if (_backdropConfiguration != null)
            {
                _backdropConfiguration.IsInputActive = e.WindowActivationState != WindowActivationState.Deactivated;
            }

            ApplyTitleBarMenuStyling();
        }

        private async void OnMainViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(MainViewModel.IsUnitContextActive))
            {
                UpdateHeaderAnimation(ViewModel.IsUnitContextActive);
            }
            else if (e.PropertyName == nameof(MainViewModel.ActiveUnit))
            {
                // ZAWSZE animuj nagłówek przy zmianie jednostki
                if (ViewModel.IsUnitContextActive)
                {
                    int previousIndex = ViewModel.CurrentUnitIndex > 0 ? ViewModel.CurrentUnitIndex - 1 : ViewModel.TotalUnitsCount - 1;
                    bool isNext = ViewModel.CurrentUnitIndex > previousIndex || (previousIndex == ViewModel.TotalUnitsCount - 1 && ViewModel.CurrentUnitIndex == 0);

                    await AnimateUnitHeaderChange(isNext);
                }

                await ViewModel.SaveCurrentUnitAsync();
                await ViewModel.UpdateFooterFromSettingsAsync();
            }
            else if (e.PropertyName == nameof(MainViewModel.EngineName) || e.PropertyName == nameof(MainViewModel.PriorityOrder))
            {
                var sb = new Storyboard();
                var fadeOut = new DoubleAnimation { To = 0, Duration = TimeSpan.FromMilliseconds(150) };
                Storyboard.SetTarget(fadeOut, BottomStatusBar);
                Storyboard.SetTargetProperty(fadeOut, "Opacity");
                sb.Children.Add(fadeOut);
                var tcs = new TaskCompletionSource();
                sb.Completed += (_, _) => tcs.TrySetResult();
                sb.Begin();
                await tcs.Task;

                sb = new Storyboard();
                var fadeIn = new DoubleAnimation { To = 1, Duration = TimeSpan.FromMilliseconds(150) };
                Storyboard.SetTarget(fadeIn, BottomStatusBar);
                Storyboard.SetTargetProperty(fadeIn, "Opacity");
                sb.Children.Add(fadeIn);
                sb.Begin();
            }
        }

        private async void OnMainViewModelPropertyChangedForDeclarations(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName != nameof(MainViewModel.ActiveUnit)) return;
            if (_currentDeclarationsView == null || ViewModel.ActiveUnit == null) return;

            int currentIndex = ViewModel.CurrentUnitIndex;

            // POPRAWKA: Wykrywaj zmianę nawet przy pierwszym wejściu po inicjalizacji
            if (_previousUnitIndex == -1)
            {
                _previousUnitIndex = currentIndex;
                return; // Pierwsze załadowanie - bez animacji
            }

            if (_previousUnitIndex == currentIndex)
            {
                return; // Ta sama jednostka - brak zmiany
            }

            bool isNext = currentIndex > _previousUnitIndex ||
                          (_previousUnitIndex == ViewModel.TotalUnitsCount - 1 && currentIndex == 0);

            System.Diagnostics.Debug.WriteLine($"ANIMACJA: poprzedni={_previousUnitIndex}, obecny={currentIndex}, isNext={isNext}");

            _previousUnitIndex = currentIndex;

            await AnimateUnitChangeInDeclarations(isNext);
        }

        private async Task AnimateUnitChangeInDeclarations(bool isNext)
        {
            if (_currentDeclarationsView == null || ViewModel.ActiveUnit == null) return;

            System.Diagnostics.Debug.WriteLine($"AnimateUnitChangeInDeclarations: Start (isNext={isNext})");

            var frozenYear = ViewModel.SelectedYear;
            var frozenMonthIndex = ViewModel.SelectedMonthIndex;
            var doctorsForUnit = ViewModel.DoctorRows.Select(dr => dr.Profile).ToList();

            if (!doctorsForUnit.Any()) return;

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

            // Start animacji nagłówka
            var headerTask = AnimateUnitHeaderChange(isNext);

            // Start animacji kalendarza (wyjście)
            var calendarGrid = _currentDeclarationsView.FindName("CalendarGridView") as GridView;
            if (calendarGrid != null)
            {
                if (calendarGrid.RenderTransform == null || calendarGrid.RenderTransform is not TranslateTransform)
                {
                    calendarGrid.RenderTransform = new TranslateTransform();
                }

                var storyboard = new Storyboard();
                var duration = TimeSpan.FromMilliseconds(350);
                var easing = new CubicEase { EasingMode = EasingMode.EaseInOut };

                var slideOutX = new DoubleAnimation
                {
                    From = 0,
                    To = isNext ? -600 : 600,
                    Duration = duration,
                    EasingFunction = easing
                };
                Storyboard.SetTarget(slideOutX, calendarGrid);
                Storyboard.SetTargetProperty(slideOutX, "(UIElement.RenderTransform).(TranslateTransform.X)");

                var fadeOut = new DoubleAnimation
                {
                    From = 1,
                    To = 0,
                    Duration = duration,
                    EasingFunction = easing
                };
                Storyboard.SetTarget(fadeOut, calendarGrid);
                Storyboard.SetTargetProperty(fadeOut, "Opacity");

                storyboard.Children.Add(slideOutX);
                storyboard.Children.Add(fadeOut);

                var tcs = new TaskCompletionSource();
                storyboard.Completed += (s, e) => tcs.TrySetResult();

                try
                {
                    storyboard.Begin();
                    await tcs.Task;
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Błąd animacji kalendarza (wyjście): {ex.Message}");
                }

                var transform = calendarGrid.RenderTransform as TranslateTransform;
                if (transform != null)
                {
                    transform.X = isNext ? 600 : -600;
                }
                calendarGrid.Opacity = 0;
            }

            // Załaduj nowe dane
            System.Diagnostics.Debug.WriteLine("ReloadForNewUnit: Start");
            _currentDeclarationsView.ViewModel?.ReloadForNewUnit(
                doctorsForUnit,
                initialIndex,
                ViewModel.ActiveUnit.UseTwelveHourShiftsByDefault,
                ViewModel.CurrentUnitIndex
            );

            // Poczekaj na update bindingu
            await Task.Delay(150);

            // Odśwież brushe
            System.Diagnostics.Debug.WriteLine("RefreshCellBrushes: Start");
            _currentDeclarationsView.RefreshCellBrushes();

            await Task.Delay(50);

            // Animacja wejścia kalendarza
            if (calendarGrid != null)
            {
                var storyboard2 = new Storyboard();
                var duration = TimeSpan.FromMilliseconds(350);
                var easing = new CubicEase { EasingMode = EasingMode.EaseInOut };

                var slideInX = new DoubleAnimation
                {
                    From = isNext ? 600 : -600,
                    To = 0,
                    Duration = duration,
                    EasingFunction = easing
                };
                Storyboard.SetTarget(slideInX, calendarGrid);
                Storyboard.SetTargetProperty(slideInX, "(UIElement.RenderTransform).(TranslateTransform.X)");

                var fadeIn = new DoubleAnimation
                {
                    From = 0,
                    To = 1,
                    Duration = duration,
                    EasingFunction = easing
                };
                Storyboard.SetTarget(fadeIn, calendarGrid);
                Storyboard.SetTargetProperty(fadeIn, "Opacity");

                storyboard2.Children.Add(slideInX);
                storyboard2.Children.Add(fadeIn);

                var tcs2 = new TaskCompletionSource();
                storyboard2.Completed += (s, e) => tcs2.TrySetResult();

                try
                {
                    storyboard2.Begin();
                    await tcs2.Task;
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Błąd animacji kalendarza (wejście): {ex.Message}");
                }
            }

            await headerTask;
            System.Diagnostics.Debug.WriteLine("AnimateUnitChangeInDeclarations: Koniec");
        }

        private async Task AnimateUnitHeaderChange(bool isNext)
        {
            var unitContextGrid = UnitSelectionPanel.FindName("UnitContextGrid") as Grid;
            if (unitContextGrid == null) return;

            if (unitContextGrid.RenderTransform == null || unitContextGrid.RenderTransform is not TranslateTransform)
            {
                unitContextGrid.RenderTransform = new TranslateTransform();
            }

            var storyboard = new Storyboard();
            var duration = TimeSpan.FromMilliseconds(300);
            var easing = new CubicEase { EasingMode = EasingMode.EaseInOut };

            var slideOutX = new DoubleAnimation
            {
                From = 0,
                To = isNext ? -400 : 400,
                Duration = duration,
                EasingFunction = easing
            };
            Storyboard.SetTarget(slideOutX, unitContextGrid);
            Storyboard.SetTargetProperty(slideOutX, "(UIElement.RenderTransform).(TranslateTransform.X)");

            var fadeOut = new DoubleAnimation
            {
                From = 1,
                To = 0,
                Duration = duration,
                EasingFunction = easing
            };
            Storyboard.SetTarget(fadeOut, unitContextGrid);
            Storyboard.SetTargetProperty(fadeOut, "Opacity");

            storyboard.Children.Add(slideOutX);
            storyboard.Children.Add(fadeOut);

            var tcs = new TaskCompletionSource();
            storyboard.Completed += (s, e) => tcs.TrySetResult();

            try
            {
                storyboard.Begin();
                await tcs.Task;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Błąd animacji nagłówka wyjścia: {ex.Message}");
                return;
            }

            var transform = unitContextGrid.RenderTransform as TranslateTransform;
            if (transform != null)
            {
                transform.X = isNext ? 400 : -400;
            }
            unitContextGrid.Opacity = 0;

            await Task.Delay(50);

            var storyboard2 = new Storyboard();
            var slideInX = new DoubleAnimation
            {
                From = isNext ? 400 : -400,
                To = 0,
                Duration = duration,
                EasingFunction = easing
            };
            Storyboard.SetTarget(slideInX, unitContextGrid);
            Storyboard.SetTargetProperty(slideInX, "(UIElement.RenderTransform).(TranslateTransform.X)");

            var fadeIn = new DoubleAnimation
            {
                From = 0,
                To = 1,
                Duration = duration,
                EasingFunction = easing
            };
            Storyboard.SetTarget(fadeIn, unitContextGrid);
            Storyboard.SetTargetProperty(fadeIn, "Opacity");

            storyboard2.Children.Add(slideInX);
            storyboard2.Children.Add(fadeIn);

            var tcs2 = new TaskCompletionSource();
            storyboard2.Completed += (s, e) => tcs2.TrySetResult();

            try
            {
                storyboard2.Begin();
                await tcs2.Task;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Błąd animacji nagłówka wejścia: {ex.Message}");
            }
        }

        private async Task AnimateCalendarChange(bool isNext)
        {
            if (_currentDeclarationsView == null) return;

            var calendarGrid = _currentDeclarationsView.FindName("CalendarGridView") as GridView;
            if (calendarGrid == null)
            {
                System.Diagnostics.Debug.WriteLine("BŁĄD: Nie znaleziono CalendarGridView!");
                return;
            }

            if (calendarGrid.RenderTransform == null || calendarGrid.RenderTransform is not TranslateTransform)
            {
                calendarGrid.RenderTransform = new TranslateTransform();
            }

            var storyboard = new Storyboard();
            var duration = TimeSpan.FromMilliseconds(350);
            var easing = new CubicEase { EasingMode = EasingMode.EaseInOut };

            var slideOutX = new DoubleAnimation
            {
                From = 0,
                To = isNext ? -600 : 600,
                Duration = duration,
                EasingFunction = easing
            };
            Storyboard.SetTarget(slideOutX, calendarGrid);
            Storyboard.SetTargetProperty(slideOutX, "(UIElement.RenderTransform).(TranslateTransform.X)");

            var fadeOut = new DoubleAnimation
            {
                From = 1,
                To = 0,
                Duration = duration,
                EasingFunction = easing
            };
            Storyboard.SetTarget(fadeOut, calendarGrid);
            Storyboard.SetTargetProperty(fadeOut, "Opacity");

            storyboard.Children.Add(slideOutX);
            storyboard.Children.Add(fadeOut);

            var tcs = new TaskCompletionSource();
            storyboard.Completed += (s, e) => tcs.TrySetResult();

            try
            {
                storyboard.Begin();
                await tcs.Task;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Błąd animacji wyjścia: {ex.Message}");
                return;
            }

            var transform = calendarGrid.RenderTransform as TranslateTransform;
            if (transform != null)
            {
                transform.X = isNext ? 600 : -600;
            }
            calendarGrid.Opacity = 0;

            await Task.Delay(50);

            var storyboard2 = new Storyboard();
            var slideInX = new DoubleAnimation
            {
                From = isNext ? 600 : -600,
                To = 0,
                Duration = duration,
                EasingFunction = easing
            };
            Storyboard.SetTarget(slideInX, calendarGrid);
            Storyboard.SetTargetProperty(slideInX, "(UIElement.RenderTransform).(TranslateTransform.X)");

            var fadeIn = new DoubleAnimation
            {
                From = 0,
                To = 1,
                Duration = duration,
                EasingFunction = easing
            };
            Storyboard.SetTarget(fadeIn, calendarGrid);
            Storyboard.SetTargetProperty(fadeIn, "Opacity");

            storyboard2.Children.Add(slideInX);
            storyboard2.Children.Add(fadeIn);

            var tcs2 = new TaskCompletionSource();
            storyboard2.Completed += (s, e) => tcs2.TrySetResult();

            try
            {
                storyboard2.Begin();
                await tcs2.Task;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Błąd animacji wejścia: {ex.Message}");
            }
        }

        private async Task InitializeApplicationAsync()
        {
            await ReloadSettingsAndServicesAsync();
            if (_appSettings == null || string.IsNullOrWhiteSpace(_appSettings.SupabaseUrl) || string.IsNullOrWhiteSpace(_appSettings.SupabaseAnonKey))
            {
                ViewModel.IsUnitContextActive = false;
                ViewModel.CurrentViewTitle = "Konfiguracja Połączenia";
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
                ViewModel.IsUnitContextActive = false;
                ViewModel.CurrentViewTitle = "Logowanie";
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
                        ViewModel.IsUnitContextActive = false;
                        ViewModel.CurrentViewTitle = "Wymagana zmiana hasła";
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
                    _doctorRepository = new SupabaseDoctorRepository(_supabaseService);
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
            ViewModel.IsUnitContextActive = true;
            ViewModel.CurrentViewTitle = string.Empty;
        }

        private async void SwitchToSettings(bool forceRefresh = false)
        {
            if ((_isClosing || _appSettings == null) && !forceRefresh) return;
            _settingsView = new SettingsView();
            _settingsView.ReloadRequired += RefreshDataServicesAsync;
            _settingsView.Initialize(_unitRepository, _settingsService, _appSettings);
            if (!forceRefresh) { await AnimateToAsync(_settingsView, forward: true); } else { ViewportCurrent.Content = _settingsView; }
            BuildActionsForSettings();
            ViewModel.IsUnitContextActive = false;
            ViewModel.CurrentViewTitle = "Ustawienia";
        }

        private async void SwitchToManagement()
        {
            if (_isClosing) return;
            if (_doctorRepository == null || _unitRepository == null || _assignmentRepository == null) { await ShowInfo("Brak aktywnego połączenia", "Sprawdź konfigurację połączenia w ustawieniach."); return; }
            _managementView = new ManagementView(_doctorRepository, _unitRepository, _assignmentRepository, _supabaseService, this.DispatcherQueue);
            await AnimateToAsync(_managementView, forward: true);
            BuildActionsForManage();
            ViewModel.IsUnitContextActive = false;
            ViewModel.CurrentViewTitle = "Zarządzanie dyżurnymi";
        }

        private void BuildActionsForDashboard()
        {
            ActionsLeft.Clear();
            ActionsRight.Clear();
            ActionsLeft.Add(new UiAction("Ustawienia", new RelayCommand(() => SwitchToSettings())));
            ActionsLeft.Add(new UiAction("Zarządzanie dyżurnymi", new RelayCommand(() => SwitchToManagement())));
            ActionsLeft.Add(new UiAction("Edytuj deklaracje dyżurowe", new RelayCommand(() => SwitchToDeclarations())));

            ActionsRight.Add(new UiAction("Generuj grafik", new AsyncRelayCommand(ViewModel.GenerateScheduleAsync), isPrimary: true));
            ActionsRight.Add(new UiAction("Eksportuj...", new RelayCommand(ExportPlaceholder)));
        }

        private void BuildActionsForDeclarations(DeclarationsView view)
        {
            ActionsLeft.Clear();
            ActionsRight.Clear();
            ActionsLeft.Add(new UiAction("Anuluj", new RelayCommand(view.OnDeclCloseOnly)));
            ActionsLeft.Add(new UiAction("Wyczyść zaznaczenie", new RelayCommand(() => view.ViewModel.ClearSelectionCommand.Execute(null))));

            ActionsRight.Add(new UiAction("Zapisz", new RelayCommand(() => view.ViewModel.SaveCommand.Execute(null))));
            ActionsRight.Add(new UiAction("Zapisz i zamknij", new RelayCommand(view.OnDeclSaveAndCloseOnly), isPrimary: true));
        }

        private void BuildActionsForSettings()
        {
            ActionsLeft.Clear();
            ActionsRight.Clear();
            ActionsLeft.Add(new UiAction("Wstecz", new RelayCommand(() => SwitchToDashboard())));
        }

        private void BuildActionsForManage()
        {
            ActionsLeft.Clear();
            ActionsRight.Clear();
            ActionsLeft.Add(new UiAction("Wstecz", new RelayCommand(() => SwitchToDashboard())));
        }

        private async void SwitchToDashboard()
        {
            if (_isClosing || _isAnimating) return;

            ViewModel.PropertyChanged -= OnMainViewModelPropertyChangedForDeclarations;
            _currentDeclarationsView = null;

            await ViewModel.UpdateFooterFromSettingsAsync();
            await AnimateToAsync(_dashboardView, false);
            BuildActionsForDashboard();
            ViewModel.IsUnitContextActive = true;
            ViewModel.CurrentViewTitle = string.Empty;
        }

        private async void SwitchToDeclarations()
        {
            if (_isClosing || _isAnimating) return;
            if (ViewModel.ActiveUnit == null) return;

            // DODAJ: Reset poprzedniego indeksu
            _previousUnitIndex = ViewModel.CurrentUnitIndex;

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
                ViewModel.ActiveUnit.UseTwelveHourShiftsByDefault,
                () => {
                    ViewModel.RefreshDeclarationsForDashboard();
                }
             );

            declarationsVm.CurrentUnitIndex = ViewModel.CurrentUnitIndex;

            var declarationsView = new DeclarationsView();

            ViewModel.PropertyChanged += OnMainViewModelPropertyChangedForDeclarations;

            void DeclCloseHandler()
            {
                ViewModel.PropertyChanged -= OnMainViewModelPropertyChangedForDeclarations;
                _previousUnitIndex = -1; // DODAJ: Reset przy zamknięciu
                SwitchToDashboard();
            }

            void DeclSaveAndCloseHandler()
            {
                declarationsView.ViewModel.SaveCommand.Execute(null);
                ViewModel.PropertyChanged -= OnMainViewModelPropertyChangedForDeclarations;
                _previousUnitIndex = -1; // DODAJ: Reset przy zamknięciu
                SwitchToDashboard();
            }

            declarationsView.CloseRequested += DeclCloseHandler;
            declarationsView.SaveAndCloseRequested += DeclSaveAndCloseHandler;

            declarationsView.AttachViewModel(declarationsVm);
            _currentDeclarationsView = declarationsView;

            await AnimateToAsync(declarationsView, true);
            BuildActionsForDeclarations(declarationsView);
            ViewModel.IsUnitContextActive = true;
            ViewModel.CurrentViewTitle = "Edycja deklaracji";
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

            if (_acrylicController != null)
            {
                _acrylicController.Dispose();
                _acrylicController = null;
            }

            if (_oldWndProc != IntPtr.Zero)
            {
                try
                {
                    var hwnd = WindowNative.GetWindowHandle(this);
                    if (hwnd != IntPtr.Zero)
                    {
                        SetWindowLongPtr(hwnd, -4, _oldWndProc);
                    }
                }
                catch { }
                _oldWndProc = IntPtr.Zero;
            }

            if (_wndProcGCHandle.IsAllocated)
            {
                _wndProcGCHandle.Free();
            }

            WeakReferenceMessenger.Default.Unregister<SettingsHaveChangedMessage>(this);
        }

        private void OnActualThemeChanged(FrameworkElement sender, object args)
        {
            ApplyTitleBarMenuStyling();
        }

        private void ApplyTitleBarMenuStyling()
        {
            var hwnd = WindowNative.GetWindowHandle(this);
            if (hwnd == IntPtr.Zero || _appWindow == null) return;

            bool isDarkTheme = (Application.Current.RequestedTheme == ApplicationTheme.Dark) ||
                               ((Application.Current.RequestedTheme == ApplicationTheme.Light) == false &&
                                (this.Content as FrameworkElement)?.ActualTheme == ElementTheme.Dark);

            var titleBarColors = isDarkTheme
                ? new
                {
                    Bg = Color.FromArgb(0xFF, 0x20, 0x20, 0x20),
                    Fg = Colors.White,
                    InactiveBg = Color.FromArgb(0xFF, 0x18, 0x18, 0x18),
                    InactiveFg = Color.FromArgb(0xFF, 0x99, 0x99, 0x99),
                    BtnBg = Colors.Transparent,
                    BtnFg = Colors.White,
                    BtnHoverBg = Color.FromArgb(0xFF, 0x30, 0x30, 0x30),
                    BtnHoverFg = Colors.White,
                    BtnPressBg = Color.FromArgb(0xFF, 0x28, 0x28, 0x28),
                    BtnPressFg = Colors.White
                }
                : new
                {
                    Bg = Color.FromArgb(0xFF, 0xF3, 0xF3, 0xF3),
                    Fg = Colors.Black,
                    InactiveBg = Color.FromArgb(0xFF, 0xEB, 0xEB, 0xEB),
                    InactiveFg = Color.FromArgb(0xFF, 0x66, 0x66, 0x66),
                    BtnBg = Colors.Transparent,
                    BtnFg = Colors.Black,
                    BtnHoverBg = Color.FromArgb(0xFF, 0xE0, 0xE0, 0xE0),
                    BtnHoverFg = Colors.Black,
                    BtnPressBg = Color.FromArgb(0xFF, 0xD0, 0xD0, 0xD0),
                    BtnPressFg = Colors.Black
                };

            _appWindow.TitleBar.BackgroundColor = titleBarColors.Bg;
            _appWindow.TitleBar.ForegroundColor = titleBarColors.Fg;
            _appWindow.TitleBar.InactiveBackgroundColor = titleBarColors.InactiveBg;
            _appWindow.TitleBar.InactiveForegroundColor = titleBarColors.InactiveFg;
            _appWindow.TitleBar.ButtonBackgroundColor = titleBarColors.BtnBg;
            _appWindow.TitleBar.ButtonForegroundColor = titleBarColors.BtnFg;
            _appWindow.TitleBar.ButtonHoverBackgroundColor = titleBarColors.BtnHoverBg;
            _appWindow.TitleBar.ButtonHoverForegroundColor = titleBarColors.BtnHoverFg;
            _appWindow.TitleBar.ButtonPressedBackgroundColor = titleBarColors.BtnPressBg;
            _appWindow.TitleBar.ButtonPressedForegroundColor = titleBarColors.BtnPressFg;

            int useDarkMode = isDarkTheme ? 1 : 0;
            DwmSetWindowAttribute(hwnd, 20, ref useDarkMode, sizeof(int));
        }

        private void UpdateHeaderAnimation(bool isUnitContextActive)
        {
            var sb = new Storyboard();
            var ease = new CubicEase { EasingMode = EasingMode.EaseInOut };
            var duration = TimeSpan.FromMilliseconds(300);

            var heightAnim = new DoubleAnimation
            {
                To = isUnitContextActive ? 80 : 0,
                Duration = duration,
                EasingFunction = ease
            };
            Storyboard.SetTarget(heightAnim, UnitSelectionPanel);
            Storyboard.SetTargetProperty(heightAnim, "Height");
            sb.Children.Add(heightAnim);

            var opacityAnim = new DoubleAnimation
            {
                To = isUnitContextActive ? 1 : 0,
                Duration = duration,
                EasingFunction = ease
            };
            Storyboard.SetTarget(opacityAnim, UnitSelectionPanel);
            Storyboard.SetTargetProperty(opacityAnim, "Opacity");
            sb.Children.Add(opacityAnim);

            sb.Begin();
        }

        private void InitAppWindow()
        {
            var hwnd = WindowNative.GetWindowHandle(this);
            var windowId = Win32Interop.GetWindowIdFromWindow(hwnd);
            _appWindow = AppWindow.GetFromWindowId(windowId);
            if (_appWindow != null)
            {
                var presenter = _appWindow.Presenter as OverlappedPresenter;
                if (presenter != null)
                {
                    presenter.IsResizable = true;
                    presenter.IsMaximizable = true;
                    presenter.IsMinimizable = true;
                }
                _appWindow.Resize(new SizeInt32(MIN_W, MIN_H));
            }

            SubclassWindow(hwnd);
        }

        private void SubclassWindow(IntPtr hwnd)
        {
            _newWndProc = new WndProc(NewWindowProc);
            _wndProcGCHandle = GCHandle.Alloc(_newWndProc);
            var newWndProcPtr = Marshal.GetFunctionPointerForDelegate(_newWndProc);
            _oldWndProc = SetWindowLongPtr(hwnd, -4, newWndProcPtr);
        }

        private IntPtr NewWindowProc(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam)
        {
            const uint WM_GETMINMAXINFO = 0x0024;
            if (msg == WM_GETMINMAXINFO)
            {
                var mmi = Marshal.PtrToStructure<MINMAXINFO>(lParam);
                mmi.ptMinTrackSize.x = MIN_W;
                mmi.ptMinTrackSize.y = MIN_H;
                Marshal.StructureToPtr(mmi, lParam, true);
            }
            return CallWindowProc(_oldWndProc, hWnd, msg, wParam, lParam);
        }

        private void TryInitializeBackdropController()
        {
            if (DesktopAcrylicController.IsSupported())
            {
                _acrylicController = new DesktopAcrylicController();
                _backdropConfiguration = new SystemBackdropConfiguration();
                _acrylicController.TintColor = Color.FromArgb(0xFF, 0x10, 0x10, 0x10);
                _acrylicController.TintOpacity = 0.7f;
                _acrylicController.LuminosityOpacity = 0.1f;
                _acrylicController.FallbackColor = Color.FromArgb(0xFF, 0x20, 0x20, 0x20);

                ((FrameworkElement)this.Content).ActualThemeChanged += (s, e) =>
                {
                    if (_backdropConfiguration != null)
                    {
                        _backdropConfiguration.Theme = ((FrameworkElement)this.Content).ActualTheme switch
                        {
                            ElementTheme.Dark => SystemBackdropTheme.Dark,
                            ElementTheme.Light => SystemBackdropTheme.Light,
                            _ => SystemBackdropTheme.Default
                        };
                    }
                };

                _backdropConfiguration.Theme = ((FrameworkElement)this.Content).ActualTheme switch
                {
                    ElementTheme.Dark => SystemBackdropTheme.Dark,
                    ElementTheme.Light => SystemBackdropTheme.Light,
                    _ => SystemBackdropTheme.Default
                };

                _acrylicController.AddSystemBackdropTarget(this.As<ICompositionSupportsSystemBackdrop>());
                _acrylicController.SetSystemBackdropConfiguration(_backdropConfiguration);
            }
        }

        private async void OnLogout(object? sender, RoutedEventArgs e)
        {
            if (_isClosing) return;
            await _supabaseService.SignOutAsync();
            this.Close();
        }

        private void ExportPlaceholder()
        {
            _ = ShowInfo("Eksport", "Funkcja eksportu nie jest jeszcze zaimplementowana.");
        }

        public async Task<ContentDialogResult> ShowInfo(string title, string message)
        {
            if (_isClosing) return ContentDialogResult.None;
            var dialog = new ContentDialog
            {
                XamlRoot = this.Content.XamlRoot,
                Title = title,
                Content = message,
                CloseButtonText = "OK",
                DefaultButton = ContentDialogButton.Close
            };
            return await dialog.ShowAsync();
        }

        [DllImport("user32.dll", SetLastError = true)]
        private static extern IntPtr SetWindowLongPtr(IntPtr hWnd, int nIndex, IntPtr dwNewLong);

        [DllImport("user32.dll")]
        private static extern IntPtr CallWindowProc(IntPtr lpPrevWndFunc, IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);

        [DllImport("dwmapi.dll")]
        private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int attrValue, int attrSize);

        [StructLayout(LayoutKind.Sequential)]
        private struct POINT
        {
            public int x;
            public int y;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct MINMAXINFO
        {
            public POINT ptReserved;
            public POINT ptMaxSize;
            public POINT ptMaxPosition;
            public POINT ptMinTrackSize;
            public POINT ptMaxTrackSize;
        }
    }

    public class UiAction
    {
        public string Label { get; }
        public ICommand Command { get; }
        public bool IsPrimary { get; }

        public UiAction(string label, ICommand command, bool isPrimary = false)
        {
            Label = label;
            Command = command;
            IsPrimary = isPrimary;
        }
    }

    public class UiActionTemplateSelector : DataTemplateSelector
    {
        public DataTemplate? NormalButtonTemplate { get; set; }
        public DataTemplate? PrimaryButtonTemplate { get; set; }

        protected override DataTemplate SelectTemplateCore(object item, DependencyObject container)
        {
            if (item is UiAction action)
            {
                return action.IsPrimary ? (PrimaryButtonTemplate ?? NormalButtonTemplate!) : NormalButtonTemplate!;
            }
            return NormalButtonTemplate!;
        }
    }
}