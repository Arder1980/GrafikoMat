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
using Microsoft.UI.Xaml.Documents;
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
using GrafikoMat.Common;

namespace GrafikoMat
{
    public sealed partial class MainWindow : Window, IRecipient<SettingsHaveChangedMessage>, IRecipient<UnitDataChangedMessage>
    {
        // Bazowe minimalne wymiary (przy 100% DPI scaling)
        private const int MIN_W = 1600;
        private const int MIN_H = 1000;

        // Przeskalowane wymiary (obliczane dynamicznie na podstawie DPI)
        private int _scaledMinWidth = MIN_W;
        private int _scaledMinHeight = MIN_H;
        private double _currentScaleFactor = 1.0;

        private AppWindow? _appWindow;
        public MainViewModel ViewModel { get; }
        public ObservableCollection<Models.UiAction> ActionsLeft { get; } = new();
        public ObservableCollection<Models.UiAction> ActionsRight { get; } = new();

        private readonly DashboardView _dashboardView = new();
        private SettingsView? _settingsView;
        private ManagementView? _managementView;
        private DeclarationsView? _currentDeclarationsView;

        private bool _isAnimating;
        private bool _isClosing;
        private Storyboard? _activeStoryboard;

        private int _previousUnitIndex = -1;
        private Guid? _previousUnitIdForDeclarations = null; // ✅ DODANE - osobne pole dla widoku deklaracji
        private bool _hamburgerEventsAttached = false;

        private delegate IntPtr WndProc(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);
        private WndProc? _newWndProc;
        private IntPtr _oldWndProc;
        private GCHandle _wndProcGCHandle;

        private AcrylicBackdropManager? _backdropManager;

        private readonly SettingsService _settingsService;
        private readonly SupabaseService _supabaseService;
        private AppSettings? _appSettings;
        private IDoctorRepository? _doctorRepository;
        private IUnitRepository? _unitRepository;
        private IAssignmentRepository? _assignmentRepository;
        private IDeclarationRepository? _declarationRepository;
        private ICoDutyNotificationRepository? _coDutyNotificationRepository;
        private CoDutyNotificationService? _coDutyNotificationService;
        private Guid? _currentUserId;  // ID zalogowanego użytkownika

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

            // Inicjalizuj backdrop z kaskadowym fallbackiem (Acrylic → Mica → Solid)
            _backdropManager = new AcrylicBackdropManager();
            _backdropManager.Initialize(this);
            // Nie musimy już ręcznie obsługiwać fallbacku - AcrylicBackdropManager robi to automatycznie

            RootGrid.Loaded += async (s, e) => {
                // WAŻNE: Oblicz DPI scale gdy XAML jest już załadowany
                CalculateScaleFactor();
                System.Diagnostics.Debug.WriteLine($"RootGrid.Loaded: DPI calculated, scale={_currentScaleFactor:F2}x");

                ApplyTitleBarMenuStyling();
                await InitializeApplicationAsync();
            };

            _dashboardView.Attach(ViewModel);

            this.Activated += OnWindowActivated;
            this.Closed += OnWindowClosed;
            (this.Content as FrameworkElement).ActualThemeChanged += OnActualThemeChanged;

            WeakReferenceMessenger.Default.Register<SettingsHaveChangedMessage>(this);
            WeakReferenceMessenger.Default.Register<UnitDataChangedMessage>(this);
        }

        public async void Receive(SettingsHaveChangedMessage message)
        {
            if (_settingsService != null)
            {
                await DispatcherQueue.EnqueueAsync(async () => {
                    _appSettings = await _settingsService.LoadSettingsAsync(forceReload: true);
                    await ViewModel.UpdateFooterFromSettingsAsync();
                    ApplyTitleBarMenuStyling();
                });
            }
        }

        public void Receive(UnitDataChangedMessage message)
        {
            _ = DispatcherQueue.EnqueueAsync(async () => {
                await ViewModel.LoadUserAndUnitDataAsync();
                await ViewModel.LoadDataForActiveUnitAsync();
            });
        }

        private void OnWindowActivated(object? sender, WindowActivatedEventArgs e)
        {
            if (_isClosing) return;

            _backdropManager?.SetIsInputActive(e.WindowActivationState != WindowActivationState.Deactivated);

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
                // ✅ DODANE: Nie animuj nagłówka, gdy jesteśmy w widoku deklaracji
                if (_currentDeclarationsView != null)
                {
                    // W widoku deklaracji - obsługa jest w OnMainViewModelPropertyChangedForDeclarations
                    return;
                }

                if (ViewModel.IsUnitContextActive && _previousUnitIndex != -1)
                {
                    int previousIndex = _previousUnitIndex;
                    bool isNext = ViewModel.CurrentUnitIndex > previousIndex || (previousIndex == ViewModel.TotalUnitsCount - 1 && ViewModel.CurrentUnitIndex == 0);

                    if (ViewModel.CurrentUnitIndex != _previousUnitIndex)
                    {
                        await AnimateUnitHeaderChange(isNext);
                    }
                }
                _previousUnitIndex = ViewModel.CurrentUnitIndex;

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
            System.Diagnostics.Debug.WriteLine($"[DECL] PropertyChanged: {e.PropertyName}");

            if (e.PropertyName != nameof(MainViewModel.ActiveUnit)) return;
            if (_currentDeclarationsView == null || ViewModel.ActiveUnit == null) return;

            Guid currentUnitId = ViewModel.ActiveUnit.Id;
            int currentIndex = ViewModel.CurrentUnitIndex;

            System.Diagnostics.Debug.WriteLine($"[DECL] Unit change detected. Previous: {_previousUnitIdForDeclarations}, Current: {currentUnitId}");

            // Jeśli nie mamy zapisanej poprzedniej jednostki - zapisz i nie animuj
            if (_previousUnitIdForDeclarations == null)
            {
                System.Diagnostics.Debug.WriteLine($"[DECL] First time - saving unit ID without animation");
                _previousUnitIdForDeclarations = currentUnitId;
                return;
            }

            // Jeśli ta sama jednostka - nie animuj
            if (_previousUnitIdForDeclarations == currentUnitId)
            {
                System.Diagnostics.Debug.WriteLine($"[DECL] Same unit - no animation");
                return;
            }

            System.Diagnostics.Debug.WriteLine($"[DECL] Different unit - animating!");

            // Określ kierunek animacji
            int previousIndex = ViewModel.GetUnitIndexById(_previousUnitIdForDeclarations.Value);
            int totalUnits = ViewModel.TotalUnitsCount;

            bool isNext;
            if (currentIndex == 0 && previousIndex == totalUnits - 1)
            {
                // Przejście z ostatniej do pierwszej - w prawo
                isNext = true;
            }
            else if (currentIndex == totalUnits - 1 && previousIndex == 0)
            {
                // Przejście z pierwszej do ostatniej - w lewo
                isNext = false;
            }
            else
            {
                // Normalne porównanie
                isNext = currentIndex > previousIndex;
            }

            System.Diagnostics.Debug.WriteLine($"[DECL] Animation direction: previousIndex={previousIndex}, currentIndex={currentIndex}, isNext={isNext}");

            _previousUnitIdForDeclarations = currentUnitId;

            // ✅ DODANE - animuj nagłówek równolegle z kalendarzem
            var headerTask = AnimateUnitHeaderChange(isNext);
            var calendarTask = AnimateUnitChangeInDeclarationsOnly(isNext);

            // Czekaj na obie animacje
            await Task.WhenAll(headerTask, calendarTask);
        }

        private async Task AnimateUnitChangeInDeclarationsOnly(bool isNext)
        {
            System.Diagnostics.Debug.WriteLine($"[ANIM] Starting calendar animation, isNext: {isNext}");

            if (_currentDeclarationsView == null || ViewModel.ActiveUnit == null)
            {
                System.Diagnostics.Debug.WriteLine($"[ANIM] Aborted - view or unit is null");
                return;
            }

            // Pobierz lekarzy dla NOWEJ jednostki
            await ViewModel.LoadDataForActiveUnitAsync();
            var doctorsForUnit = ViewModel.DoctorRows.Select(dr => dr.Profile).ToList();

            if (!doctorsForUnit.Any())
            {
                System.Diagnostics.Debug.WriteLine($"[ANIM] No doctors - showing empty calendar");
            }
            else
            {
                System.Diagnostics.Debug.WriteLine($"[ANIM] Found {doctorsForUnit.Count} doctors for unit {ViewModel.ActiveUnit.Name}");
            }

            int initialIndex = 0;
            if (!ViewModel.IsCurrentUserAdmin && _doctorRepository != null && doctorsForUnit.Any())
            {
                try
                {
                    var currentProfile = await _doctorRepository.GetCurrentDoctorProfileAsync();
                    if (currentProfile != null)
                    {
                        var idx = doctorsForUnit.FindIndex(d => d.Id == currentProfile.Id);
                        if (idx >= 0) initialIndex = idx;
                    }
                }
                catch { initialIndex = 0; }
            }

            var calendarGrid = _currentDeclarationsView.FindName("CalendarGridView") as GridView;
            System.Diagnostics.Debug.WriteLine($"[ANIM] CalendarGrid found: {calendarGrid != null}");

            // ✅ DODANE - ukryj overlay PRZED wyjazdem
            if (_currentDeclarationsView.ViewModel?.HasNoDoctors == true)
            {
                System.Diagnostics.Debug.WriteLine($"[ANIM] Hiding overlay before slide-out");
                _currentDeclarationsView.HideOverlay();
                await Task.Delay(200); // Poczekaj na fade-out
            }

            // ✅ KLUCZOWA ZMIANA - animacja wyjścia TYLKO gdy kalendarz jest widoczny
            if (calendarGrid != null && calendarGrid.Opacity > 0)
            {
                if (calendarGrid.RenderTransform == null || calendarGrid.RenderTransform is not TranslateTransform)
                {
                    calendarGrid.RenderTransform = new TranslateTransform();
                }

                System.Diagnostics.Debug.WriteLine($"[ANIM] Starting slide-out animation");

                // ANIMACJA WYJŚCIA
                var storyboard = new Storyboard();
                var duration = TimeSpan.FromMilliseconds(350);
                var easing = new CubicEase { EasingMode = EasingMode.EaseInOut };

                var slideOutX = new DoubleAnimation { From = 0, To = isNext ? -600 : 600, Duration = duration, EasingFunction = easing };
                Storyboard.SetTarget(slideOutX, calendarGrid);
                Storyboard.SetTargetProperty(slideOutX, "(UIElement.RenderTransform).(TranslateTransform.X)");

                var fadeOut = new DoubleAnimation { From = 1, To = 0, Duration = duration, EasingFunction = easing };
                Storyboard.SetTarget(fadeOut, calendarGrid);
                Storyboard.SetTargetProperty(fadeOut, "Opacity");

                storyboard.Children.Add(slideOutX);
                storyboard.Children.Add(fadeOut);

                var tcs = new TaskCompletionSource();
                storyboard.Completed += (s, e) =>
                {
                    System.Diagnostics.Debug.WriteLine($"[ANIM] Slide-out completed");
                    tcs.TrySetResult();
                };

                try { storyboard.Begin(); await tcs.Task; }
                catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"[ANIM] Error animating calendar (exit): {ex.Message}"); }

                var transform = calendarGrid.RenderTransform as TranslateTransform;
                if (transform != null) transform.X = isNext ? 600 : -600;
                calendarGrid.Opacity = 0;
            }

            System.Diagnostics.Debug.WriteLine($"[ANIM] Calling ReloadForNewUnit");

            // ✅ ZMIENIONE - użyj metody z blokowaniem
            _currentDeclarationsView.ReloadWithoutAutomaticUpdate(() =>
            {
                _currentDeclarationsView.ViewModel?.ReloadForNewUnit(
                    doctorsForUnit,
                    initialIndex,
                    ViewModel.ActiveUnit.UseTwelveHourShiftsByDefault,
                    ViewModel.CurrentUnitIndex);
            });

            // ✅ USUNIĘTE - nie resetuj ItemsSource, DayCells.Clear() w ReloadForNewUnit to załatwi
            await Task.Delay(150);

            System.Diagnostics.Debug.WriteLine($"[ANIM] Calling UpdateAllCellBrushes");
            _currentDeclarationsView?.UpdateAllCellBrushes();
            _currentDeclarationsView?.UpdateCalendarOpacity();

            await Task.Delay(50);

            if (calendarGrid != null)
            {
                System.Diagnostics.Debug.WriteLine($"[ANIM] Starting slide-in animation");

                // ANIMACJA WJAZDU
                var storyboard2 = new Storyboard();
                var duration2 = TimeSpan.FromMilliseconds(350);
                var easing2 = new CubicEase { EasingMode = EasingMode.EaseInOut };

                var slideInX = new DoubleAnimation { From = isNext ? 600 : -600, To = 0, Duration = duration2, EasingFunction = easing2 };
                Storyboard.SetTarget(slideInX, calendarGrid);
                Storyboard.SetTargetProperty(slideInX, "(UIElement.RenderTransform).(TranslateTransform.X)");

                var fadeIn = new DoubleAnimation { From = 0, To = 1, Duration = duration2, EasingFunction = easing2 };
                Storyboard.SetTarget(fadeIn, calendarGrid);
                Storyboard.SetTargetProperty(fadeIn, "Opacity");

                storyboard2.Children.Add(slideInX);
                storyboard2.Children.Add(fadeIn);

                var tcs2 = new TaskCompletionSource();
                storyboard2.Completed += (s, e) =>
                {
                    System.Diagnostics.Debug.WriteLine($"[ANIM] Slide-in completed");
                    tcs2.TrySetResult();
                };

                try { storyboard2.Begin(); await tcs2.Task; }
                catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"[ANIM] Error animating calendar (entry): {ex.Message}"); }
            }

            // ✅ DODANE - pokaż overlay PO wjeździe
            if (_currentDeclarationsView.ViewModel?.HasNoDoctors == true)
            {
                System.Diagnostics.Debug.WriteLine($"[ANIM] Showing overlay after slide-in");
                _currentDeclarationsView.ShowOverlay();
            }

            System.Diagnostics.Debug.WriteLine($"[ANIM] Calendar animation complete");
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

            var slideOutX = new DoubleAnimation { From = 0, To = isNext ? -400 : 400, Duration = duration, EasingFunction = easing };
            Storyboard.SetTarget(slideOutX, unitContextGrid);
            Storyboard.SetTargetProperty(slideOutX, "(UIElement.RenderTransform).(TranslateTransform.X)");

            var fadeOut = new DoubleAnimation { From = 1, To = 0, Duration = duration, EasingFunction = easing };
            Storyboard.SetTarget(fadeOut, unitContextGrid);
            Storyboard.SetTargetProperty(fadeOut, "Opacity");

            storyboard.Children.Add(slideOutX);
            storyboard.Children.Add(fadeOut);

            var tcs = new TaskCompletionSource();
            storyboard.Completed += (s, e) => tcs.TrySetResult();
            try { storyboard.Begin(); await tcs.Task; }
            catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"Error animating header exit: {ex.Message}"); return; }

            var transform = unitContextGrid.RenderTransform as TranslateTransform;
            if (transform != null) transform.X = isNext ? 400 : -400;
            unitContextGrid.Opacity = 0;
            await Task.Delay(50);

            var storyboard2 = new Storyboard();
            var slideInX = new DoubleAnimation { From = isNext ? 400 : -400, To = 0, Duration = duration, EasingFunction = easing };
            Storyboard.SetTarget(slideInX, unitContextGrid);
            Storyboard.SetTargetProperty(slideInX, "(UIElement.RenderTransform).(TranslateTransform.X)");

            var fadeIn = new DoubleAnimation { From = 0, To = 1, Duration = duration, EasingFunction = easing };
            Storyboard.SetTarget(fadeIn, unitContextGrid);
            Storyboard.SetTargetProperty(fadeIn, "Opacity");

            storyboard2.Children.Add(slideInX);
            storyboard2.Children.Add(fadeIn);

            var tcs2 = new TaskCompletionSource();
            storyboard2.Completed += (s, e) => tcs2.TrySetResult();
            try { storyboard2.Begin(); await tcs2.Task; }
            catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"Error animating header entry: {ex.Message}"); }
        }

        private async Task InitializeApplicationAsync()
        {
            await ReloadSettingsAndServicesAsync();

            if (_appSettings == null || string.IsNullOrWhiteSpace(_appSettings.SupabaseUrl) || string.IsNullOrWhiteSpace(_appSettings.SupabaseAnonKey))
            {
                ShowInitialConnectionSetup();
                return;
            }

            var restored = await _supabaseService.RestoreSessionIfAnyAsync();
            if (!restored && !_supabaseService.IsAuthenticated)
            {
                ViewModel.IsUnitContextActive = false;
                ViewModel.CurrentViewTitle = "Logowanie";
                bool loggedIn = await ShowLoginScreenAsync();
                if (!loggedIn)
                {
                    this.Close();
                    return;
                }
            }

            await ContinueInitializationAfterLoginAsync();
        }

        private async Task ContinueInitializationAfterLoginAsync()
        {
            if (!_supabaseService.IsAuthenticated)
            {
                await ShowInfo("Błąd krytyczny", "Nie udało się uwierzytelnić użytkownika. Aplikacja zostanie zamknięta.");
                this.Close();
                return;
            }

            if (_doctorRepository != null)
            {
                try
                {
                    var profile = await _doctorRepository.GetCurrentDoctorProfileAsync();
                    if (profile != null && profile.RequiresPasswordChange)
                    {
                        ViewModel.IsUnitContextActive = false;
                        ViewModel.CurrentViewTitle = "Wymagana zmiana hasła";
                        bool passwordChanged = await ShowForcePasswordChangeAsync();
                        if (!passwordChanged)
                        {
                            await _supabaseService.SignOutAsync();
                            this.Close();
                            return;
                        }
                        await _doctorRepository.ClearPasswordChangeFlagAsync(profile.Id);
                    }
                    else if (profile == null)
                    {
                        await ShowInfo("Błąd krytyczny", "Nie można wczytać profilu zalogowanego użytkownika.");
                        await _supabaseService.SignOutAsync();
                        this.Close();
                        return;
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
            else
            {
                await ShowInfo("Błąd krytyczny", "Nie można zainicjalizować połączenia z bazą danych. Sprawdź ustawienia.");
                this.Close();
                return;
            }

            await LoadDataAndShowDashboardAsync();
            InitialLoadingOverlay.Visibility = Visibility.Collapsed;
            StartupOverlayContent.Content = null;
            await RunEntranceAnimationAsync();

            // Sprawdź powiadomienia o współdyżurach
            await UpdateNotificationBadgeAsync();
            await ShowStartupNotificationsAsync();
        }

        private void ShowInitialConnectionSetup()
        {
            ViewModel.IsUnitContextActive = false;
            ViewModel.CurrentViewTitle = "Konfiguracja Połączenia";
            var setupView = new Views.Settings.ConnectionSettingsView();
            setupView.IsInitialSetupMode = true;
            setupView.Initialize(_settingsService, _appSettings ?? new AppSettings());

            setupView.ConnectionEstablished += async () =>
            {
                await DispatcherQueue.EnqueueAsync(async () => {
                    StartupOverlayContent.Content = null;
                    InitialLoadingOverlay.Visibility = Visibility.Visible;
                });

                await ReloadSettingsAndServicesAsync();
                bool loggedIn = await ShowLoginScreenAsync();

                await DispatcherQueue.EnqueueAsync(async () => {
                    if (loggedIn)
                    {
                        await ContinueInitializationAfterLoginAsync();
                    }
                    else
                    {
                        this.Close();
                    }
                });
            };

            StartupOverlayContent.Content = setupView;
            InitialLoadingOverlay.Visibility = Visibility.Collapsed;
        }

        private async Task RunEntranceAnimationAsync()
        {
            TopBarRow.Opacity = 0; TopBarRow.Visibility = Visibility.Visible;
            UnitSelectionPanel.Opacity = 0; UnitSelectionPanel.Visibility = Visibility.Visible;
            MainContentPanel.Opacity = 0; MainContentPanel.Visibility = Visibility.Visible;
            ActionButtonsPanel.Opacity = 0; ActionButtonsPanel.Visibility = Visibility.Visible;
            BottomStatusBar.Opacity = 0; BottomStatusBar.Visibility = Visibility.Visible;

            TopBarRow.RenderTransform ??= new TranslateTransform();
            UnitSelectionPanel.RenderTransform ??= new TranslateTransform();
            MainContentPanel.RenderTransform ??= new TranslateTransform();
            BottomStatusBar.RenderTransform ??= new TranslateTransform();

            (TopBarRow.RenderTransform as TranslateTransform).Y = -20;
            (UnitSelectionPanel.RenderTransform as TranslateTransform).Y = -20;
            (MainContentPanel.RenderTransform as TranslateTransform).Y = -20;
            (BottomStatusBar.RenderTransform as TranslateTransform).Y = 20;

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
                    _declarationRepository = new SupabaseDeclarationRepository(_supabaseService);
                    _coDutyNotificationRepository = new SupabaseCoDutyNotificationRepository(_supabaseService);

                    // Inicjalizuj serwis powiadomień
                    if (_coDutyNotificationRepository != null && _declarationRepository != null && _doctorRepository != null && _unitRepository != null)
                    {
                        _coDutyNotificationService = new CoDutyNotificationService(
                            _coDutyNotificationRepository,
                            _declarationRepository,
                            _doctorRepository,
                            _unitRepository);

                        _coDutyNotificationService.NotificationCountChanged += OnNotificationCountChanged;
                    }
                }
                else
                {
                    _doctorRepository = null;
                    _unitRepository = null;
                    _assignmentRepository = null;
                    _coDutyNotificationRepository = null;
                    _coDutyNotificationService = null;
                }
            }
            else
            {
                _supabaseService.Initialize(string.Empty, string.Empty);
                _doctorRepository = null;
                _unitRepository = null;
                _assignmentRepository = null;
            }
            ViewModel.SetRepositories(_doctorRepository, _unitRepository, _assignmentRepository, _declarationRepository);
        }

        private async void RefreshDataServicesAsync()
        {
            await ReloadSettingsAndServicesAsync();
            if (ViewportCurrent.Content == _settingsView)
            {
                SwitchToSettings(forceRefresh: true);
            }
        }

        private async Task<bool> ShowLoginScreenAsync()
        {
            InitialLoadingOverlay.Visibility = Visibility.Visible;
            StartupOverlayContent.Content = null;
            await Task.Delay(50);

            if (_supabaseService != null && _supabaseService.IsAuthenticated)
            {
                InitialLoadingOverlay.Visibility = Visibility.Collapsed;
                return true;
            }

            var tcs = new TaskCompletionSource<bool>();
            var loginView = new LoginView(_supabaseService!);

            Func<Task> fadeOutAndCleanup = async () =>
            {
                var sb = new Storyboard();
                var fadeOutAnim = new DoubleAnimation
                {
                    To = 0,
                    Duration = TimeSpan.FromMilliseconds(200),
                    EasingFunction = new CubicEase { EasingMode = EasingMode.EaseIn }
                };
                Storyboard.SetTarget(fadeOutAnim, LoginOverlay);
                Storyboard.SetTargetProperty(fadeOutAnim, "Opacity");
                sb.Children.Add(fadeOutAnim);

                var fadeTcs = new TaskCompletionSource();
                sb.Completed += (_, _) =>
                {
                    LoginOverlay.Content = null;
                    InitialLoadingOverlay.Visibility = Visibility.Collapsed;
                    fadeTcs.TrySetResult();
                };
                sb.Begin();
                await fadeTcs.Task;
            };

            loginView.CancelRequested += async () =>
            {
                await fadeOutAndCleanup();
                tcs.TrySetResult(false);
            };

            loginView.LoginSuccess += async () =>
            {
                await _supabaseService.SaveCurrentSessionAsync();
                await fadeOutAndCleanup();
                tcs.TrySetResult(true);
            };

            LoginOverlay.Opacity = 0;
            LoginOverlay.Content = loginView;
            InitialLoadingOverlay.Visibility = Visibility.Collapsed;

            var fadeInSb = new Storyboard();
            var fadeInAnim = new DoubleAnimation
            {
                From = 0,
                To = 1,
                Duration = TimeSpan.FromMilliseconds(300),
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
            };
            Storyboard.SetTarget(fadeInAnim, LoginOverlay);
            Storyboard.SetTargetProperty(fadeInAnim, "Opacity");
            fadeInSb.Children.Add(fadeInAnim);
            fadeInSb.Begin();

            return await tcs.Task;
        }

        private async Task<bool> ShowForcePasswordChangeAsync()
        {
            InitialLoadingOverlay.Visibility = Visibility.Visible;
            LoginOverlay.Content = null;
            await Task.Delay(50);

            var tcs = new TaskCompletionSource<bool>();
            var changePasswordView = new ChangePasswordView(_supabaseService);

            Func<Task> fadeOutAndCleanup = async () =>
            {
                var sb = new Storyboard();
                var fadeOutAnim = new DoubleAnimation { To = 0, Duration = TimeSpan.FromMilliseconds(200), EasingFunction = new CubicEase { EasingMode = EasingMode.EaseIn } };
                Storyboard.SetTarget(fadeOutAnim, LoginOverlay);
                Storyboard.SetTargetProperty(fadeOutAnim, "Opacity");
                sb.Children.Add(fadeOutAnim);

                var fadeTcs = new TaskCompletionSource();
                sb.Completed += (_, _) => { LoginOverlay.Content = null; InitialLoadingOverlay.Visibility = Visibility.Collapsed; fadeTcs.TrySetResult(); };
                sb.Begin();
                await fadeTcs.Task;
            };

            changePasswordView.PasswordChangeSuccess += async () => {
                await fadeOutAndCleanup();
                tcs.TrySetResult(true);
            };
            changePasswordView.PasswordChangeCancelled += async () => {
                await fadeOutAndCleanup();
                tcs.TrySetResult(false);
            };

            LoginOverlay.Opacity = 0;
            LoginOverlay.Content = changePasswordView;
            InitialLoadingOverlay.Visibility = Visibility.Collapsed;

            var fadeInSb = new Storyboard();
            var fadeInAnim = new DoubleAnimation { From = 0, To = 1, Duration = TimeSpan.FromMilliseconds(300), EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut } };
            Storyboard.SetTarget(fadeInAnim, LoginOverlay);
            Storyboard.SetTargetProperty(fadeInAnim, "Opacity");
            fadeInSb.Children.Add(fadeInAnim);
            fadeInSb.Begin();

            return await tcs.Task;
        }

        private async Task LoadDataAndShowDashboardAsync()
        {
            InitialLoadingOverlay.Visibility = Visibility.Visible;
            await Task.Delay(50);

            if (_doctorRepository != null)
            {
                await ViewModel.LoadUserAndUnitDataAsync();
                await ViewModel.LoadDataForActiveUnitAsync();
            }

            ViewportCurrent.Content = _dashboardView;
            BuildActionsForDashboard();
            ResetViewportState();
            ViewModel.IsUnitContextActive = true;
            ViewModel.CurrentViewTitle = string.Empty;

            InitialLoadingOverlay.Visibility = Visibility.Collapsed;
        }

        private async void SwitchToSettings(bool forceRefresh = false)
        {
            if ((_isClosing || _appSettings == null) && !forceRefresh) return;
            _settingsView = new SettingsView();
            _settingsView.ReloadRequired += RefreshDataServicesAsync;

            // DODAJ OBSŁUGĘ AKCJI
            _settingsView.ActionButtonsChanged += (actions) =>
            {
                DispatcherQueue.TryEnqueue(() =>
                {
                    ActionsRight.Clear();
                    foreach (var action in actions)
                    {
                        ActionsRight.Add(action);
                    }
                });
            };

            _settingsView.Initialize(_unitRepository, _settingsService, _supabaseService, _appSettings);

            if (!forceRefresh)
            {
                await AnimateToAsync(_settingsView, forward: true);
            }
            else
            {
                ViewportCurrent.Content = _settingsView;
                ResetViewportState();
            }

            BuildActionsForSettings();
            ViewModel.IsUnitContextActive = false;
            ViewModel.CurrentViewTitle = "Ustawienia";
        }

        private async void SwitchToManagement()
        {
            if (_isClosing || _isAnimating) return;
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
            ActionsLeft.Add(new Models.UiAction("Ustawienia", new RelayCommand(() => SwitchToSettings())));
            ActionsLeft.Add(new Models.UiAction("Zarządzanie dyżurnymi", new RelayCommand(() => SwitchToManagement())));
            ActionsLeft.Add(new Models.UiAction("Edytuj deklaracje dyżurowe", new RelayCommand(() => SwitchToDeclarations())));

            ActionsRight.Add(new Models.UiAction("Generuj grafik", new AsyncRelayCommand(ViewModel.GenerateScheduleAsync), isPrimary: true));
            ActionsRight.Add(new Models.UiAction("Eksportuj...", new RelayCommand(ExportPlaceholder)));
        }

        private void BuildActionsForDeclarations(DeclarationsView view)
        {
            ActionsLeft.Clear();
            ActionsRight.Clear();
            ActionsLeft.Add(new Models.UiAction("Anuluj", new RelayCommand(view.OnDeclCloseOnly)));
            ActionsLeft.Add(new Models.UiAction("Wyczyść deklaracje", new RelayCommand(async () => await ConfirmAndClearDeclarationsAsync(view))));

            ActionsRight.Add(new Models.UiAction("Zapisz", new RelayCommand(() => view.ViewModel?.SaveCommand.Execute(null))));
            ActionsRight.Add(new Models.UiAction("Zapisz i zamknij", new RelayCommand(view.OnDeclSaveAndCloseOnly), isPrimary: true));
        }

        private async Task ConfirmAndClearDeclarationsAsync(DeclarationsView view)
        {
            if (view?.ViewModel == null)
                return;

            var dialog = new ContentDialog
            {
                Title = "Potwierdzenie",
                Content = $"Czy na pewno chcesz usunąć wszystkie deklaracje dla lekarza {view.ViewModel.SelectedDoctor?.FullName} z miesiąca {view.ViewModel.MonthHeader}?\n\nTa operacja nie może być cofnięta.",
                PrimaryButtonText = "Usuń",
                CloseButtonText = "Anuluj",
                DefaultButton = ContentDialogButton.Close,
                XamlRoot = this.Content.XamlRoot
            };

            var result = await dialog.ShowAsync();

            if (result == ContentDialogResult.Primary)
            {
                await view.ViewModel.ClearDeclarationsCommand.ExecuteAsync(null);
            }
        }

        private void BuildActionsForSettings()
        {
            ActionsLeft.Clear();
            ActionsLeft.Add(new Models.UiAction("Wstecz", new RelayCommand(SwitchToDashboard), isPrimary: false));

            // Wyczyść prawe akcje - będą ustawiane dynamicznie przez podwidoki
            ActionsRight.Clear();
        }

        /// <summary>
        /// Publiczna metoda umożliwiająca widokom rejestrację swoich akcji w panelu przycisków.
        /// Używana przez widoki ustawień i inne podwidoki do dodawania przycisków typu "Zapisz".
        /// </summary>
        public void RegisterViewActions(params Models.UiAction[] actions)
        {
            ActionsRight.Clear();
            foreach (var action in actions)
            {
                ActionsRight.Add(action);
            }
        }

        /// <summary>
        /// Czyści akcje zarejestrowane przez widoki (używane przy zmianie podwidoku lub wyjściu).
        /// </summary>
        public void ClearViewActions()
        {
            ActionsRight.Clear();
        }

        private void BuildActionsForManage()
        {
            ActionsLeft.Clear();
            ActionsRight.Clear();
            ActionsLeft.Add(new Models.UiAction("Wstecz", new RelayCommand(() => SwitchToDashboard())));

            // Dodaj przycisk Zapisz dla ManagementView
            if (_managementView != null)
            {
                ActionsRight.Add(new Models.UiAction("Zapisz", _managementView.ViewModel.SaveDoctorCommand, isPrimary: true));
            }
        }

        private async void SwitchToDashboard()
        {
            if (_isClosing || _isAnimating) return;

            // Sprawdź czy wychodzisz z ustawień z niezapisanymi zmianami
            System.Diagnostics.Debug.WriteLine($"[SwitchToDashboard] ViewportCurrent.Content type: {ViewportCurrent.Content?.GetType().Name}");
            System.Diagnostics.Debug.WriteLine($"[SwitchToDashboard] _settingsView is null: {_settingsView == null}");
            System.Diagnostics.Debug.WriteLine($"[SwitchToDashboard] Are equal: {ViewportCurrent.Content == _settingsView}");

            if (ViewportCurrent.Content == _settingsView && _settingsView != null)
            {
                System.Diagnostics.Debug.WriteLine($"[SwitchToDashboard] Inside settings check, HasUnsavedChanges: {_settingsView.HasUnsavedChanges()}");

                if (_settingsView.HasUnsavedChanges())
                {
                    System.Diagnostics.Debug.WriteLine("[SwitchToDashboard] Showing dialog for unsaved changes");

                    var dialog = App.CreateThemedDialog();
                    dialog.Title = "Niezapisane zmiany";
                    dialog.Content = "Masz niezapisane zmiany w ustawieniach. Co chcesz zrobić?";
                    dialog.PrimaryButtonText = "Zapisz i wyjdź";
                    dialog.SecondaryButtonText = "Odrzuć zmiany";
                    dialog.CloseButtonText = "Anuluj";
                    dialog.DefaultButton = ContentDialogButton.Primary;

                    var result = await dialog.ShowAsync();

                    if (result == ContentDialogResult.Primary)
                    {
                        // Użytkownik chce zapisać
                        await _settingsView.SaveCurrentSettingsAsync();
                        // Po zapisaniu kontynuuj wyjście
                    }
                    else if (result == ContentDialogResult.Secondary)
                    {
                        // Użytkownik odrzuca zmiany - przywróć oryginalne ustawienia
                        _settingsView.RestoreOriginalSettings();
                        // Kontynuuj wyjście
                    }
                    else
                    {
                        // Użytkownik anulował - nie wychodź z ustawień
                        return;
                    }
                }
            }

            // Sprawdź czy wychodzisz z zarządzania dyżurnymi z niezapisanymi zmianami
            if (ViewportCurrent.Content == _managementView && _managementView != null)
            {
                System.Diagnostics.Debug.WriteLine($"[SwitchToDashboard] Inside management check, HasUnsavedChanges: {_managementView.ViewModel.HasUnsavedChanges}");

                if (_managementView.ViewModel.HasUnsavedChanges)
                {
                    System.Diagnostics.Debug.WriteLine("[SwitchToDashboard] Showing dialog for unsaved changes in management");

                    var dialog = App.CreateThemedDialog();
                    dialog.Title = "Niezapisane zmiany";
                    dialog.Content = "Masz niezapisane zmiany w danych dyżurnego. Co chcesz zrobić?";
                    dialog.PrimaryButtonText = "Zapisz i wyjdź";
                    dialog.SecondaryButtonText = "Odrzuć zmiany";
                    dialog.CloseButtonText = "Anuluj";
                    dialog.DefaultButton = ContentDialogButton.Primary;

                    var result = await dialog.ShowAsync();

                    if (result == ContentDialogResult.Primary)
                    {
                        // Użytkownik chce zapisać
                        await _managementView.ViewModel.SaveDoctorCommand.ExecuteAsync(null);
                        // Po zapisaniu kontynuuj wyjście
                    }
                    else if (result == ContentDialogResult.Secondary)
                    {
                        // Użytkownik odrzuca zmiany - kontynuuj wyjście
                        // (ManagementView nie ma specjalnej logiki przywracania)
                    }
                    else
                    {
                        // Użytkownik anulował - nie wychodź z zarządzania
                        return;
                    }
                }
            }

            ViewModel.PropertyChanged -= OnMainViewModelPropertyChangedForDeclarations;

            // POPRAWKA: Dispose DeclarationsViewModel przed nullowaniem
            _currentDeclarationsView?.ViewModel?.Dispose();
            _currentDeclarationsView = null;
            _previousUnitIdForDeclarations = null; // ✅ ZMIENIONE

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

            var frozenYear = ViewModel.SelectedYear;
            var frozenMonthIndex = ViewModel.SelectedMonthIndex;

            var doctorsForUnit = ViewModel.DoctorRows.Select(dr => dr.Profile).ToList();
            if (!doctorsForUnit.Any())
            {
                _ = ShowInfo("Brak lekarzy", "Brak aktywnych lekarzy przypisanych do tej jednostki.");
                return;
            }

            int initialIndex = 0;
            if (!ViewModel.IsCurrentUserAdmin && _doctorRepository != null)
            {
                try
                {
                    var currentProfile = await _doctorRepository.GetCurrentDoctorProfileAsync();
                    if (currentProfile != null)
                    {
                        var idx = doctorsForUnit.FindIndex(d => d.Id == currentProfile.Id);
                        if (idx >= 0) initialIndex = idx;
                    }
                }
                catch { initialIndex = 0; }
            }

            var declarationsVm = new DeclarationsViewModel(
                frozenYear, frozenMonthIndex, doctorsForUnit, initialIndex,
                ViewModel.Declarations, ViewModel.IsCurrentUserAdmin,
                ViewModel.ActiveUnit.UseTwelveHourShiftsByDefault,
                () => { ViewModel.RefreshDeclarationsForDashboard(); },
                _declarationRepository,
                ViewModel.ActiveUnit.Id
             );

            declarationsVm.CurrentUnitIndex = ViewModel.CurrentUnitIndex;

            // ✅ DODANE - załaduj deklaracje z Supabase
            _ = declarationsVm.LoadDeclarationsFromSupabaseAsync();

            var declarationsView = new DeclarationsView();

            // Ustaw repozytoria dla funkcjonalności współdyżurnych
            declarationsView.CoDutyNotificationRepository = _coDutyNotificationRepository;
            declarationsView.DeclarationRepository = _declarationRepository;
            declarationsView.DoctorRepository = _doctorRepository;
            declarationsView.ActiveUnitId = ViewModel.ActiveUnit?.Id;

            // ✅ ZMIENIONE: Zapisz aktualną jednostkę PRZED zarejestrowaniem handlera
            _previousUnitIdForDeclarations = ViewModel.ActiveUnit.Id;

            ViewModel.PropertyChanged += OnMainViewModelPropertyChangedForDeclarations;

            void DeclCloseHandler()
            {
                ViewModel.PropertyChanged -= OnMainViewModelPropertyChangedForDeclarations;
                _previousUnitIdForDeclarations = null; // ✅ ZMIENIONE
                SwitchToDashboard();
            }

            async void DeclSaveAndCloseHandler()
            {
                // Użyj SaveAsyncCommand i poczekaj na zakończenie
                if (declarationsView.ViewModel?.SaveAsyncCommand != null)
                {
                    await declarationsView.ViewModel.SaveAsyncCommand.ExecuteAsync(null);
                }

                ViewModel.PropertyChanged -= OnMainViewModelPropertyChangedForDeclarations;
                _previousUnitIdForDeclarations = null;
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
            if (el == null) return;
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
            if (cur == null || nxt == null) return;

            var curT = (cur.RenderTransform as TranslateTransform) ?? new TranslateTransform();
            var nxtT = (nxt.RenderTransform as TranslateTransform) ?? new TranslateTransform();
            cur.RenderTransform = curT;
            nxt.RenderTransform = nxtT;
            curT.X = 0; nxtT.X = 0;
            cur.Opacity = 1; nxt.Opacity = 0;
            var ch = TryGetHeader(cur.Content);
            var nh = TryGetHeader(nxt.Content);
            if (ch != null) { var t = (ch.RenderTransform as TranslateTransform) ?? new TranslateTransform(); t.X = 0; ch.RenderTransform = t; }
            if (nh != null) { var t = (nh.RenderTransform as TranslateTransform) ?? new TranslateTransform(); t.X = 0; nh.RenderTransform = t; }
        }

        private async Task AnimateToAsync(FrameworkElement nextView, bool forward)
        {
            if (nextView == null) return;
            if (_isClosing) { ViewportCurrent.Content = nextView; ResetViewportState(); return; }
            if (ReferenceEquals(ViewportCurrent.Content, nextView)) return;

            if (_isAnimating)
            {
                _activeStoryboard?.Stop();
                _activeStoryboard = null;
                _isAnimating = false;
                if (ViewportNext?.Content == nextView) DetachFromParent(nextView);
                ViewportNext.Content = null;
                ViewportCurrent.Content = nextView;
                ResetViewportState();
                return;
            }

            _isAnimating = true;

            var curPresenter = ViewportCurrent;
            var nxtPresenter = ViewportNext;
            if (curPresenter == null || nxtPresenter == null)
            {
                _isAnimating = false;
                return;
            }

            DetachFromParent(nextView);
            ResetViewportState();
            nxtPresenter.Content = nextView;

            curPresenter.RenderTransform ??= new TranslateTransform();
            nxtPresenter.RenderTransform ??= new TranslateTransform();

            var curTransform = (curPresenter.RenderTransform as TranslateTransform)!;
            var nxtTransform = (nxtPresenter.RenderTransform as TranslateTransform)!;
            var curHeader = TryGetHeader(curPresenter.Content);
            var nxtHeader = TryGetHeader(nxtPresenter.Content);
            TranslateTransform? curHeaderTransform = null;
            TranslateTransform? nxtHeaderTransform = null;
            if (curHeader != null) { curHeaderTransform = (curHeader.RenderTransform as TranslateTransform) ?? new TranslateTransform(); curHeader.RenderTransform = curHeaderTransform; }
            if (nxtHeader != null) { nxtHeaderTransform = (nxtHeader.RenderTransform as TranslateTransform) ?? new TranslateTransform(); nxtHeader.RenderTransform = nxtHeaderTransform; }
            double offset = 64;
            double fromNext = forward ? +offset : -offset;
            double toCur = forward ? -offset : +offset;

            nxtTransform.X = fromNext; nxtPresenter.Opacity = 0;
            curTransform.X = 0; curPresenter.Opacity = 1;
            if (nxtHeaderTransform != null) nxtHeaderTransform.X = fromNext * 0.5;
            if (curHeaderTransform != null) curHeaderTransform.X = 0;

            var sb = new Storyboard();
            _activeStoryboard = sb;
            var dur = TimeSpan.FromMilliseconds(280);
            var easeIn = new CubicEase { EasingMode = EasingMode.EaseIn };
            var easeOut = new CubicEase { EasingMode = EasingMode.EaseOut };
            var curX = new DoubleAnimation { From = 0, To = toCur, Duration = dur, EasingFunction = easeIn };
            Storyboard.SetTarget(curX, curTransform);
            Storyboard.SetTargetProperty(curX, "X");
            sb.Children.Add(curX);
            var curOp = new DoubleAnimation { From = 1, To = 0, Duration = dur, EasingFunction = easeIn };
            Storyboard.SetTarget(curOp, curPresenter);
            Storyboard.SetTargetProperty(curOp, "Opacity");
            sb.Children.Add(curOp);
            var nxtX = new DoubleAnimation { From = fromNext, To = 0, Duration = dur, EasingFunction = easeOut };
            Storyboard.SetTarget(nxtX, nxtTransform);
            Storyboard.SetTargetProperty(nxtX, "X");
            sb.Children.Add(nxtX);
            var nxtOp = new DoubleAnimation { From = 0, To = 1, Duration = dur, EasingFunction = easeOut };
            Storyboard.SetTarget(nxtOp, nxtPresenter);
            Storyboard.SetTargetProperty(nxtOp, "Opacity");
            sb.Children.Add(nxtOp);
            if (curHeaderTransform != null && curHeader != null)
            {
                var curHX = new DoubleAnimation { From = 0, To = toCur * 0.5, Duration = dur, EasingFunction = easeIn };
                Storyboard.SetTarget(curHX, curHeaderTransform);
                Storyboard.SetTargetProperty(curHX, "X");
                sb.Children.Add(curHX);
            }
            if (nxtHeaderTransform != null && nxtHeader != null)
            {
                var nxtHX = new DoubleAnimation { From = fromNext * 0.5, To = 0, Duration = dur, EasingFunction = easeOut };
                Storyboard.SetTarget(nxtHX, nxtHeaderTransform);
                Storyboard.SetTargetProperty(nxtHX, "X");
                sb.Children.Add(nxtHX);
            }
            var tcs = new TaskCompletionSource<bool>();
            sb.Completed += (_, __) =>
            {
                bool enqueued = DispatcherQueue.TryEnqueue(() =>
                {
                    if (!ReferenceEquals(_activeStoryboard, sb) || _isClosing)
                    {
                        if (ReferenceEquals(_activeStoryboard, sb)) _activeStoryboard = null;
                        _isAnimating = false;
                        return;
                    }
                    _activeStoryboard = null;

                    try
                    {
                        ViewportCurrent.Content = null;
                        ViewportNext.Content = null;
                        ResetViewportState();
                        ViewportCurrent.Content = nextView;
                    }
                    catch (ArgumentException ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"Error setting ContentPresenter.Content after animation cleanup: {ex}");
                        ViewportCurrent.Content = nextView;
                        ViewportNext.Content = null;
                        ResetViewportState();
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"Unexpected error during animation cleanup: {ex}");
                        ViewportCurrent.Content = nextView;
                        ViewportNext.Content = null;
                        ResetViewportState();
                    }
                    finally
                    {
                        _isAnimating = false;
                    }
                });
                if (!enqueued)
                {
                    _isAnimating = false;
                    System.Diagnostics.Debug.WriteLine("Failed to enqueue final animation cleanup");
                }

                tcs.TrySetResult(true);
            };

            try { sb.Begin(); await tcs.Task; }
            catch
            {
                if (!_isClosing)
                {
                    ViewportCurrent.Content = nextView;
                    ViewportNext.Content = null;
                    ResetViewportState();
                }
                _activeStoryboard = null;
                _isAnimating = false;
            }
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

            // POPRAWKA: Cleanup event handlers przed dispose

            // Odepnij event handlery ViewModel
            if (ViewModel != null)
            {
                ViewModel.PropertyChanged -= OnMainViewModelPropertyChanged;
            }

            // Odepnij event handlery AppWindow
            if (_appWindow != null)
            {
                _appWindow.Changed -= OnAppWindowChanged;
            }

            // Odepnij event handler motywu
            if (this.Content is FrameworkElement rootElement)
            {
                rootElement.ActualThemeChanged -= OnActualThemeChanged;
            }

            // Odepnij event handlery hamburger menu (jeśli były załączone)
            if (_hamburgerEventsAttached && TitleBarMenuButton != null)
            {
                TitleBarMenuButton.PointerEntered -= OnHamburgerPointerEntered;
                TitleBarMenuButton.PointerExited -= OnHamburgerPointerExited;
                TitleBarMenuButton.PointerPressed -= OnHamburgerPointerPressed;
                TitleBarMenuButton.PointerReleased -= OnHamburgerPointerReleased;
                _hamburgerEventsAttached = false;
            }

            // Dispose backdrop manager
            _backdropManager?.Dispose();
            _backdropManager = null;

            // Przywróć oryginalny WndProc
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

            // Zwolnij GCHandle
            if (_wndProcGCHandle.IsAllocated)
            {
                _wndProcGCHandle.Free();
            }

            // Wyrejestruj z messengera
            WeakReferenceMessenger.Default.Unregister<SettingsHaveChangedMessage>(this);
            WeakReferenceMessenger.Default.Unregister<UnitDataChangedMessage>(this);

            // Cleanup widoków
            _settingsView = null;
            _managementView = null;

            // POPRAWKA: Dispose DeclarationsViewModel przed nullowaniem
            _currentDeclarationsView?.ViewModel?.Dispose();
            _currentDeclarationsView = null;

            System.Diagnostics.Debug.WriteLine("[MainWindow] Cleanup completed");
        }

        private void OnActualThemeChanged(FrameworkElement sender, object args)
        {
            ApplyTitleBarMenuStyling();
            // BackdropManager automatycznie obsługuje zmiany motywu
            _ = DispatcherQueue.TryEnqueue(async () => await SaveWindowStateAsync());
        }

        private void ApplyTitleBarMenuStyling()
        {
            var hwnd = WindowNative.GetWindowHandle(this);
            if (hwnd == IntPtr.Zero || _appWindow == null) return;

            bool isDarkTheme = RootGrid.ActualTheme == ElementTheme.Dark;

            System.Diagnostics.Debug.WriteLine($"ApplyTitleBarMenuStyling: isDarkTheme = {isDarkTheme}, ActualTheme = {RootGrid.ActualTheme}");

            var titleBarColors = isDarkTheme
                ? new
                {
                    Bg = Color.FromArgb(0xFF, 0x20, 0x20, 0x20),
                    Fg = Colors.White,
                    InactiveBg = Color.FromArgb(0xFF, 0x18, 0x18, 0x18),
                    InactiveFg = Color.FromArgb(0xFF, 0x99, 0x99, 0x99),
                    BtnBg = Colors.Transparent,
                    BtnFg = Colors.White,
                    BtnHoverBg = Color.FromArgb(0xFF, 0x50, 0x50, 0x50),
                    BtnHoverFg = Colors.White,
                    BtnPressBg = Color.FromArgb(0xFF, 0x40, 0x40, 0x40),
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
                    BtnHoverBg = Color.FromArgb(0xFF, 0xC0, 0xC0, 0xC0),
                    BtnHoverFg = Colors.Black,
                    BtnPressBg = Color.FromArgb(0xFF, 0xA8, 0xA8, 0xA8),
                    BtnPressFg = Colors.Black
                };

            if (_appWindow.TitleBar != null)
            {
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
            }

            if (TitleBarMenuButton != null && !_hamburgerEventsAttached)
            {
                TitleBarMenuButton.PointerEntered += OnHamburgerPointerEntered;
                TitleBarMenuButton.PointerExited += OnHamburgerPointerExited;
                TitleBarMenuButton.PointerPressed += OnHamburgerPointerPressed;
                TitleBarMenuButton.PointerReleased += OnHamburgerPointerReleased;
                _hamburgerEventsAttached = true;
            }

            if (TitleBarMenuButton != null)
            {
                TitleBarMenuButton.Background = new SolidColorBrush(titleBarColors.BtnBg);
            }

            if (TitleBarMenuButton?.Flyout is MenuFlyout menuFlyout)
            {
                menuFlyout.MenuFlyoutPresenterStyle = new Style(typeof(MenuFlyoutPresenter));
                menuFlyout.MenuFlyoutPresenterStyle.Setters.Add(new Setter(MenuFlyoutPresenter.BackgroundProperty, new SolidColorBrush(titleBarColors.Bg)));
                menuFlyout.MenuFlyoutPresenterStyle.Setters.Add(new Setter(MenuFlyoutPresenter.BorderBrushProperty, new SolidColorBrush(Color.FromArgb(0x33, 0x00, 0x00, 0x00))));
                menuFlyout.MenuFlyoutPresenterStyle.Setters.Add(new Setter(MenuFlyoutPresenter.BorderThicknessProperty, new Thickness(1)));
                menuFlyout.MenuFlyoutPresenterStyle.Setters.Add(new Setter(MenuFlyoutPresenter.PaddingProperty, new Thickness(4)));
                menuFlyout.MenuFlyoutPresenterStyle.Setters.Add(new Setter(MenuFlyoutPresenter.CornerRadiusProperty, new CornerRadius(8)));
            }

            try
            {
                int useDarkMode = isDarkTheme ? 1 : 0;
                DwmSetWindowAttribute(hwnd, DWMWA_USE_IMMERSIVE_DARK_MODE, ref useDarkMode, sizeof(int));
                DwmSetWindowAttribute(hwnd, DWMWA_USE_IMMERSIVE_DARK_MODE_BEFORE_20H1, ref useDarkMode, sizeof(int));
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to set DWM attribute: {ex.Message}");
            }
        }

        private void OnHamburgerPointerEntered(object sender, Microsoft.UI.Xaml.Input.PointerRoutedEventArgs e)
        {
            if (TitleBarMenuButton == null) return;
            bool isDark = RootGrid.ActualTheme == ElementTheme.Dark;

            var hoverColor = isDark
                ? Color.FromArgb(0xFF, 0x50, 0x50, 0x50)
                : Colors.Transparent;

            TitleBarMenuButton.Background = new SolidColorBrush(hoverColor);
        }

        private void OnHamburgerPointerExited(object sender, Microsoft.UI.Xaml.Input.PointerRoutedEventArgs e)
        {
            if (TitleBarMenuButton == null) return;
            TitleBarMenuButton.Background = new SolidColorBrush(Colors.Transparent);
        }

        private void OnHamburgerPointerPressed(object sender, Microsoft.UI.Xaml.Input.PointerRoutedEventArgs e)
        {
            if (TitleBarMenuButton == null) return;
            bool isDark = RootGrid.ActualTheme == ElementTheme.Dark;

            var pressColor = isDark
                ? Color.FromArgb(0xFF, 0x40, 0x40, 0x40)
                : Colors.Transparent;

            TitleBarMenuButton.Background = new SolidColorBrush(pressColor);
        }

        private void OnHamburgerPointerReleased(object sender, Microsoft.UI.Xaml.Input.PointerRoutedEventArgs e)
        {
            if (TitleBarMenuButton == null) return;
            bool isDark = RootGrid.ActualTheme == ElementTheme.Dark;

            var hoverColor = isDark
                ? Color.FromArgb(0xFF, 0x50, 0x50, 0x50)
                : Colors.Transparent;

            TitleBarMenuButton.Background = new SolidColorBrush(hoverColor);
        }

        private void UpdateHeaderAnimation(bool isUnitContextActive)
        {
            var sb = new Storyboard();
            var ease = new CubicEase { EasingMode = EasingMode.EaseInOut };
            var duration = TimeSpan.FromMilliseconds(300);

            var heightAnim = new DoubleAnimation { To = isUnitContextActive ? 80 : 0, Duration = duration, EasingFunction = ease };
            Storyboard.SetTarget(heightAnim, UnitSelectionPanel);
            Storyboard.SetTargetProperty(heightAnim, "Height");
            sb.Children.Add(heightAnim);

            var opacityAnim = new DoubleAnimation { To = isUnitContextActive ? 1 : 0, Duration = duration, EasingFunction = ease };
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

                // DPI scale będzie obliczony w RootGrid.Loaded (gdy XamlRoot jest dostępny)

                // Monitoruj zmiany rozmiaru i wymuszaj minimalny rozmiar
                _appWindow.Changed += OnAppWindowChanged;

                System.Diagnostics.Debug.WriteLine($"InitAppWindow: Window initialized, DPI will be calculated in RootGrid.Loaded");
            }

            // Zachowaj hook Win32 jako backup (niektóre systemy go respektują)
            SubclassWindow(hwnd);
        }

        private void CalculateScaleFactor()
        {
            try
            {
                // Użyj XamlRoot.RasterizationScale - najbardziej niezawodna metoda w WinUI 3
                if (RootGrid?.XamlRoot != null)
                {
                    _currentScaleFactor = RootGrid.XamlRoot.RasterizationScale;

                    // Oblicz przeskalowane minimalne wymiary
                    _scaledMinWidth = (int)Math.Ceiling(MIN_W * _currentScaleFactor);
                    _scaledMinHeight = (int)Math.Ceiling(MIN_H * _currentScaleFactor);

                    System.Diagnostics.Debug.WriteLine($"CalculateScaleFactor: RasterizationScale={_currentScaleFactor:F2}x ({(_currentScaleFactor * 100):F0}%)");
                    System.Diagnostics.Debug.WriteLine($"CalculateScaleFactor: Base={MIN_W}×{MIN_H}, Scaled={_scaledMinWidth}×{_scaledMinHeight}");
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine("CalculateScaleFactor: XamlRoot not available yet, using defaults");
                    _currentScaleFactor = 1.0;
                    _scaledMinWidth = MIN_W;
                    _scaledMinHeight = MIN_H;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"CalculateScaleFactor failed: {ex.Message}, using defaults");
                _currentScaleFactor = 1.0;
                _scaledMinWidth = MIN_W;
                _scaledMinHeight = MIN_H;
            }
        }

        private void OnAppWindowChanged(AppWindow sender, AppWindowChangedEventArgs args)
        {
            // Sprawdź czy okno zostało przeniesione (może zmienić się DPI)
            if (args.DidPositionChange)
            {
                // Przelicz scale factor - mogliśmy przejść na monitor z innym DPI
                var oldScaleFactor = _currentScaleFactor;
                CalculateScaleFactor();

                if (Math.Abs(_currentScaleFactor - oldScaleFactor) > 0.01)
                {
                    System.Diagnostics.Debug.WriteLine($"DPI changed: {oldScaleFactor:F2}x → {_currentScaleFactor:F2}x");

                    // Wymuszaj nowy minimalny rozmiar jeśli okno jest za małe dla nowego DPI
                    if (_appWindow != null)
                    {
                        var currentSize = _appWindow.Size;
                        if (currentSize.Width < _scaledMinWidth || currentSize.Height < _scaledMinHeight)
                        {
                            _appWindow.Resize(new SizeInt32(
                                Math.Max(currentSize.Width, _scaledMinWidth),
                                Math.Max(currentSize.Height, _scaledMinHeight)
                            ));
                        }
                    }
                }
            }

            // Sprawdź czy zmienił się rozmiar
            if (args.DidSizeChange && _appWindow != null)
            {
                var currentSize = _appWindow.Size;
                bool needsResize = false;
                int newWidth = currentSize.Width;
                int newHeight = currentSize.Height;

                // Użyj przeskalowanych wymiarów
                if (currentSize.Width < _scaledMinWidth)
                {
                    newWidth = _scaledMinWidth;
                    needsResize = true;
                    System.Diagnostics.Debug.WriteLine($"AppWindow: Width too small ({currentSize.Width}), enforcing {_scaledMinWidth} (scale={_currentScaleFactor:F2}x)");
                }

                if (currentSize.Height < _scaledMinHeight)
                {
                    newHeight = _scaledMinHeight;
                    needsResize = true;
                    System.Diagnostics.Debug.WriteLine($"AppWindow: Height too small ({currentSize.Height}), enforcing {_scaledMinHeight} (scale={_currentScaleFactor:F2}x)");
                }

                if (needsResize)
                {
                    _appWindow.Resize(new SizeInt32(newWidth, newHeight));
                }
            }
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
            const uint WM_SIZE = 0x0005;
            const uint WM_MOVE = 0x0003;

            try
            {
                if (msg == WM_GETMINMAXINFO)
                {
                    var mmi = Marshal.PtrToStructure<MINMAXINFO>(lParam);

                    // Log przed zmianą
                    System.Diagnostics.Debug.WriteLine($"WM_GETMINMAXINFO: Before - ptMinTrackSize=({mmi.ptMinTrackSize.x}, {mmi.ptMinTrackSize.y})");

                    // Użyj przeskalowanych wymiarów
                    mmi.ptMinTrackSize.x = _scaledMinWidth;
                    mmi.ptMinTrackSize.y = _scaledMinHeight;
                    Marshal.StructureToPtr(mmi, lParam, true);

                    // Log po zmianie
                    System.Diagnostics.Debug.WriteLine($"WM_GETMINMAXINFO: After - Set to ({_scaledMinWidth}, {_scaledMinHeight}) at {_currentScaleFactor:F2}x scale");
                }
                else if ((msg == WM_SIZE || msg == WM_MOVE) && !_isClosing && _appWindow != null && _settingsService != null && DispatcherQueue != null)
                {
                    bool enqueued = DispatcherQueue.TryEnqueue(async () => await SaveWindowStateAsync());
                    if (!enqueued) System.Diagnostics.Debug.WriteLine("Failed to enqueue SaveWindowStateAsync");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error in NewWindowProc: {ex.Message}");
            }

            return (_oldWndProc != IntPtr.Zero) ? CallWindowProc(_oldWndProc, hWnd, msg, wParam, lParam) : IntPtr.Zero;
        }

        private async Task SaveWindowStateAsync()
        {
            if (_isClosing || _appWindow == null || _settingsService == null) return;

            try
            {
                var presenter = _appWindow.Presenter as OverlappedPresenter;
                if (presenter == null) return;

                bool isMaximized = presenter.State == OverlappedPresenterState.Maximized;
                var currentSize = _appWindow.Size;
                var currentPosition = _appWindow.Position;

                System.Diagnostics.Debug.WriteLine($"Saving window state: isMaximized={isMaximized}, state={presenter.State}, size={currentSize.Width}x{currentSize.Height}, pos={currentPosition.X},{currentPosition.Y}");

                var currentSettings = await _settingsService.LoadSettingsAsync();

                var newSettings = currentSettings with
                {
                    WasWindowMaximized = isMaximized,
                    LastWindowSize = isMaximized ? currentSettings.LastWindowSize : new WindowSize(currentSize.Width, currentSize.Height),
                    LastWindowPosition = isMaximized ? currentSettings.LastWindowPosition : new WindowPosition(currentPosition.X, currentPosition.Y)
                };

                if (newSettings != currentSettings)
                {
                    await _settingsService.SaveSettingsAsync(newSettings);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error saving window state: {ex.Message}");
            }
        }

        private void OnHelp(object? sender, RoutedEventArgs e)
        {
            if (_isClosing) return;
            _ = ShowInfo("Pomoc", "Funkcja pomocy będzie wkrótce dostępna.");
        }

        private async void OnAbout(object? sender, RoutedEventArgs e)
        {
            if (_isClosing) return;

            var stackPanel = new StackPanel { Spacing = 8 };

            stackPanel.Children.Add(new TextBlock
            {
                Text = "GrafikoMat Dyżurowy",
                FontSize = 18,
                FontWeight = Microsoft.UI.Text.FontWeights.SemiBold
            });

            stackPanel.Children.Add(new TextBlock
            {
                Text = "Generator grafików dyżurów lekarskich",
                FontSize = 14,
                Opacity = 0.8
            });

            stackPanel.Children.Add(new TextBlock
            {
                Text = "Wersja: 1.0\n© 2025 Adam Lemanowicz",
                FontSize = 12,
                Margin = new Thickness(0, 8, 0, 0)
            });

            var licensePanel = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 4 };
            licensePanel.Children.Add(new TextBlock { Text = "Licencja:", FontSize = 12 });
            var licenseLink = new HyperlinkButton
            {
                Content = "MIT License",
                NavigateUri = new Uri("https://opensource.org/licenses/MIT"),
                Padding = new Thickness(0),
                FontSize = 12
            };
            licensePanel.Children.Add(licenseLink);
            stackPanel.Children.Add(licensePanel);

            stackPanel.Children.Add(new TextBlock
            {
                Text = "Autor koncepcji i architektury:\nAdam Lemanowicz",
                FontSize = 12,
                Margin = new Thickness(0, 12, 0, 0)
            });

            stackPanel.Children.Add(new TextBlock
            {
                Text = "Kod generowany przez modele AI:\n• Claude Sonnet 4.5 (Anthropic)\n• ChatGPT o1 (OpenAI)\n• Gemini 2.5 Pro (Google)",
                FontSize = 12,
                Margin = new Thickness(0, 8, 0, 0)
            });

            stackPanel.Children.Add(new TextBlock
            {
                Text = "Program powstał w oparciu o współpracę człowieka z AI,\ngdzie autor definiuje wymagania i weryfikuje rezultaty,\na modele językowe implementują funkcjonalności.",
                FontSize = 11,
                Opacity = 0.7,
                Margin = new Thickness(0, 12, 0, 0),
                TextWrapping = TextWrapping.Wrap
            });

            var dialog = App.CreateThemedDialog();
            dialog.Title = "O programie";
            dialog.Content = stackPanel;
            dialog.CloseButtonText = "OK";
            dialog.DefaultButton = ContentDialogButton.Close;

            // Upewnij się, że dialog ma właściwy XamlRoot dla animacji
            if (RootGrid?.XamlRoot != null)
            {
                dialog.XamlRoot = RootGrid.XamlRoot;
            }

            try
            {
                await dialog.ShowAsync();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error showing About dialog: {ex.Message}");
            }
        }

        private async void OnLogout(object? sender, RoutedEventArgs e)
        {
            if (_isClosing) return;

            await SaveWindowStateAsync();
            await _supabaseService.SignOutAsync();

            _activeStoryboard?.Stop();
            _activeStoryboard = null;
            _isAnimating = false;

            TopBarRow.Visibility = Visibility.Collapsed;
            UnitSelectionPanel.Visibility = Visibility.Collapsed;
            MainContentPanel.Visibility = Visibility.Collapsed;
            ActionButtonsPanel.Visibility = Visibility.Collapsed;
            BottomStatusBar.Visibility = Visibility.Collapsed;
            ViewportCurrent.Content = null;
            LoginOverlay.Content = null;
            StartupOverlayContent.Content = null;

            ViewModel.IsUnitContextActive = false;
            ViewModel.CurrentViewTitle = string.Empty;
            _previousUnitIndex = -1;
            _previousUnitIdForDeclarations = null; // ✅ DODANE

            InitialLoadingOverlay.Visibility = Visibility.Visible;
            await Task.Delay(50);

            await InitializeApplicationAsync();
        }

        private void ExportPlaceholder()
        {
            _ = ShowInfo("Eksport", "Funkcja eksportu nie jest jeszcze zaimplementowana.");
        }

        public async Task<ContentDialogResult> ShowInfo(string title, string message)
        {
            if (_isClosing) return ContentDialogResult.None;
            ContentDialogResult result = ContentDialogResult.None;
            if (DispatcherQueue != null)
            {
                await DispatcherQueue.EnqueueAsync(async () => {
                    var dialog = App.CreateThemedDialog();
                    dialog.Title = title;
                    dialog.Content = message;
                    dialog.CloseButtonText = "OK";
                    dialog.DefaultButton = ContentDialogButton.Close;
                    try { result = await dialog.ShowAsync(); } catch { }
                });
            }
            else
            {
                System.Diagnostics.Debug.WriteLine("DispatcherQueue is null in ShowInfo");
            }

            return result;
        }

        public void RefreshTheme()
        {
            ApplyTitleBarMenuStyling();
        }

        [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Auto)]
        private static extern IntPtr SetWindowLongPtr(IntPtr hWnd, int nIndex, IntPtr dwNewLong);

        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        private static extern IntPtr CallWindowProc(IntPtr lpPrevWndFunc, IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);

        [DllImport("dwmapi.dll")]
        private static extern int DwmSetWindowAttribute(IntPtr hwnd, int dwAttribute, ref int pvAttribute, int cbAttribute);
        private const int DWMWA_USE_IMMERSIVE_DARK_MODE_BEFORE_20H1 = 19;
        private const int DWMWA_USE_IMMERSIVE_DARK_MODE = 20;

        [StructLayout(LayoutKind.Sequential)]
        private struct POINT { public int x; public int y; }

        [StructLayout(LayoutKind.Sequential)]
        private struct MINMAXINFO
        {
            public POINT ptReserved;
            public POINT ptMaxSize;
            public POINT ptMaxPosition;
            public POINT ptMinTrackSize;
            public POINT ptMaxTrackSize;
        }

        // ============================================================================
        // Obsługa powiadomień o współdyżurach
        // ============================================================================

        private async void NotificationButton_Click(object sender, RoutedEventArgs e)
        {
            await LoadAndShowNotificationsAsync();
            NotificationFlyout.ShowAt(NotificationButton);
        }

        private async Task LoadAndShowNotificationsAsync()
        {
            if (_coDutyNotificationService == null || _doctorRepository == null)
                return;

            var currentUser = await _doctorRepository.GetCurrentDoctorProfileAsync();
            if (currentUser == null)
                return;

            var notifications = await _coDutyNotificationService.GetPendingNotificationsAsync(currentUser.Id);

            // Usuń wszystkie elementy POZA NoNotificationsText
            var itemsToRemove = NotificationsContainer.Children
                .Where(child => child != NoNotificationsText)
                .ToList();

            foreach (var item in itemsToRemove)
            {
                NotificationsContainer.Children.Remove(item);
            }

            if (notifications.Count == 0)
            {
                NoNotificationsText.Visibility = Visibility.Visible;
            }
            else
            {
                NoNotificationsText.Visibility = Visibility.Collapsed;

                foreach (var notification in notifications)
                {
                    var card = CreateNotificationCard(notification);
                    NotificationsContainer.Children.Add(card);
                }
            }
        }

        private Border CreateNotificationCard(CoDutyNotificationViewModel notification)
        {
            var grid = new Grid
            {
                Padding = new Thickness(12),
                RowSpacing = 4
            };

            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            // Wiersz 1: Tekst główny
            var mainText = new TextBlock
            {
                FontWeight = new Windows.UI.Text.FontWeight { Weight = 600 }
            };
            mainText.Inlines.Add(new Run { Text = notification.FromDoctorName });
            mainText.Inlines.Add(new Run { Text = " zaprasza do współdyżuru" });
            Grid.SetRow(mainText, 0);
            grid.Children.Add(mainText);

            // Wiersz 2: Szczegóły
            var detailsText = new TextBlock
            {
                FontSize = 12,
                Opacity = 0.8,
                Margin = new Thickness(0, 4, 0, 0)
            };
            detailsText.Inlines.Add(new Run { Text = notification.UnitName });
            detailsText.Inlines.Add(new Run { Text = " • " });
            detailsText.Inlines.Add(new Run { Text = notification.DateText });
            detailsText.Inlines.Add(new Run { Text = " • " });
            detailsText.Inlines.Add(new Run { Text = notification.SlotTypeText });
            Grid.SetRow(detailsText, 1);
            grid.Children.Add(detailsText);

            // Wiersz 3: Przyciski
            var buttonsPanel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Spacing = 8,
                Margin = new Thickness(0, 8, 0, 0)
            };

            var acceptButton = new Button
            {
                Content = "Akceptuj",
                Style = (Style)Application.Current.Resources["AccentButtonStyle"],
                Tag = notification.Id
            };
            acceptButton.Click += async (s, e) => await AcceptNotification_Click(notification.Id);

            var rejectButton = new Button
            {
                Content = "Odrzuć",
                Tag = notification.Id
            };
            rejectButton.Click += async (s, e) => await RejectNotification_Click(notification.Id);

            buttonsPanel.Children.Add(acceptButton);
            buttonsPanel.Children.Add(rejectButton);

            Grid.SetRow(buttonsPanel, 2);
            grid.Children.Add(buttonsPanel);

            var border = new Border
            {
                Background = (Brush)Application.Current.Resources["LayerFillColorDefaultBrush"],
                CornerRadius = new CornerRadius(4),
                Child = grid
            };

            return border;
        }

        private async Task AcceptNotification_Click(long notificationId)
        {
            if (_coDutyNotificationService == null)
                return;

            var success = await _coDutyNotificationService.AcceptNotificationAsync(notificationId);
            if (success)
            {
                NotificationFlyout.Hide();
                await LoadAndShowNotificationsAsync();
                await UpdateNotificationBadgeAsync();
            }
        }

        private async Task RejectNotification_Click(long notificationId)
        {
            if (_coDutyNotificationService == null)
                return;

            var success = await _coDutyNotificationService.RejectNotificationAsync(notificationId);
            if (success)
            {
                NotificationFlyout.Hide();
                await LoadAndShowNotificationsAsync();
                await UpdateNotificationBadgeAsync();
            }
        }

        private async void OnNotificationCountChanged(object? sender, EventArgs e)
        {
            await DispatcherQueue.EnqueueAsync(async () =>
            {
                await UpdateNotificationBadgeAsync();
            });
        }

        private async Task UpdateNotificationBadgeAsync()
        {
            if (_coDutyNotificationService == null || _doctorRepository == null)
            {
                NotificationBadge.Visibility = Visibility.Collapsed;
                return;
            }

            var currentUser = await _doctorRepository.GetCurrentDoctorProfileAsync();
            if (currentUser == null)
            {
                NotificationBadge.Visibility = Visibility.Collapsed;
                return;
            }

            var count = await _coDutyNotificationService.GetPendingCountAsync(currentUser.Id);

            if (count > 0)
            {
                NotificationBadge.Value = count;
                NotificationBadge.Visibility = Visibility.Visible;
            }
            else
            {
                NotificationBadge.Visibility = Visibility.Collapsed;
            }
        }

        private async Task ShowStartupNotificationsAsync()
        {
            if (_coDutyNotificationService == null || _doctorRepository == null)
                return;

            var currentUser = await _doctorRepository.GetCurrentDoctorProfileAsync();
            if (currentUser == null)
                return;

            var count = await _coDutyNotificationService.GetPendingCountAsync(currentUser.Id);
            if (count == 0)
                return;

            var dialog = App.CreateThemedDialog();
            dialog.Title = "Prośby o współdyżur";

            string countText = count == 1 ? "nową prośbę" :
                              count < 5 ? "nowe prośby" :
                              "nowych próśb";

            dialog.Content = $"Masz {count} {countText} o wspólny dyżur.";
            dialog.PrimaryButtonText = "Pokaż";
            dialog.CloseButtonText = "Później";
            dialog.XamlRoot = this.Content.XamlRoot;

            var result = await dialog.ShowAsync();
            if (result == ContentDialogResult.Primary)
            {
                await LoadAndShowNotificationsAsync();
                NotificationFlyout.ShowAt(NotificationButton);
            }
        }
    }

    public class UiActionTemplateSelector : DataTemplateSelector
    {
        public DataTemplate? NormalButtonTemplate { get; set; }
        public DataTemplate? PrimaryButtonTemplate { get; set; }

        protected override DataTemplate SelectTemplateCore(object item, DependencyObject container)
        {
            if (item is Models.UiAction action)
            {
                return action.IsPrimary ? (PrimaryButtonTemplate ?? NormalButtonTemplate!) : NormalButtonTemplate!;
            }
            return NormalButtonTemplate!;
        }
    }
}