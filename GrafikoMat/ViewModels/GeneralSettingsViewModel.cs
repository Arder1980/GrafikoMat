using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using GrafikoMat.Common;
using GrafikoMat.Core.Scheduling;
using GrafikoMat.Core.Scheduling.Models;
using GrafikoMat.Models;
using GrafikoMat.Services;
using System;
using System.Diagnostics;
using System.Management;
using System.Threading.Tasks;

namespace GrafikoMat.ViewModels
{
    public class GeneralSettingsViewModel : ObservableObject
    {
        private readonly SettingsService _settingsService;
        private readonly SupabaseService _supabaseService;
        private readonly IUxActionOrchestrator _orchestrator;
        private AppSettings _appSettings;
        private Guid _viewId;
        private bool _isInitializing = true;
        private bool _isDirty = false;
        private AppTheme _originalTheme; // Zapamiętujemy oryginalny motyw

        // === MOTYW ===
        private AppTheme _selectedTheme;
        public AppTheme SelectedTheme
        {
            get => _selectedTheme;
            set
            {
                if (SetProperty(ref _selectedTheme, value) && !_isInitializing)
                {
                    // Zastosuj zmianę wizualnie natychmiast (bez zapisu - zapis jest przyciskiem)
                    ApplyThemeChangeVisually(value);
                    MarkAsDirty();
                }
            }
        }

        private void ApplyThemeChangeVisually(AppTheme theme)
        {
            try
            {
                Microsoft.UI.Xaml.ElementTheme newElementTheme = theme switch
                {
                    AppTheme.Light => Microsoft.UI.Xaml.ElementTheme.Light,
                    AppTheme.Dark => Microsoft.UI.Xaml.ElementTheme.Dark,
                    _ => Microsoft.UI.Xaml.ElementTheme.Default
                };

                // Bezpieczne sprawdzenie czy okno jest gotowe
                if (App.MainRoot?.Content is Microsoft.UI.Xaml.FrameworkElement rootElement)
                {
                    rootElement.RequestedTheme = newElementTheme;
                    App.MainRoot.RefreshTheme();
                }

                ThemeManagerService.Instance.SetTheme(newElementTheme);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"ApplyThemeChangeVisually error: {ex.Message}");
                // Ignoruj błąd - motyw zostanie zastosowany przy następnym zapisie
            }
        }

        // === TIMEOUT ===
        private int _timeoutMinutesValue;
        public int TimeoutMinutesValue
        {
            get => _timeoutMinutesValue;
            set { if (SetProperty(ref _timeoutMinutesValue, value)) MarkAsDirty(); }
        }
        public int TimeoutMinutesMin => SolverDefaults.TimeoutMinutes.Min;
        public int TimeoutMinutesMax => SolverDefaults.TimeoutMinutes.Max;
        public int TimeoutMinutesStep => SolverDefaults.TimeoutMinutes.Step;

        // === WIELOWĄTKOWOŚĆ ===
        private bool _isAutoThreadCount;
        public bool IsAutoThreadCount
        {
            get => _isAutoThreadCount;
            set
            {
                if (SetProperty(ref _isAutoThreadCount, value))
                {
                    OnPropertyChanged(nameof(IsCustomThreadCount));
                    OnPropertyChanged(nameof(IsMaxThreadsWarningVisible));
                    MarkAsDirty();
                }
            }
        }

        public bool IsCustomThreadCount
        {
            get => !IsAutoThreadCount;
            set => IsAutoThreadCount = !value;
        }

        private int _customThreadCountValue;
        public int CustomThreadCountValue
        {
            get => _customThreadCountValue;
            set
            {
                if (SetProperty(ref _customThreadCountValue, value))
                {
                    OnPropertyChanged(nameof(IsMaxThreadsWarningVisible));
                    MarkAsDirty();
                }
            }
        }

        public bool IsMaxThreadsWarningVisible => IsCustomThreadCount && CustomThreadCountValue >= DetectedThreadCount;

        public int CustomThreadCountMin => SolverDefaults.CustomThreadCount.Min;
        public int CustomThreadCountMax => SolverDefaults.CustomThreadCount.Max;
        public int CustomThreadCountStep => SolverDefaults.CustomThreadCount.Step;
        public int DetectedThreadCount => ParallelismConfig.ProcessorCount;
        public int OptimalThreadCount => ParallelismConfig.OptimalParallelism;
        public string ProcessorName => GetProcessorName();
        public string AutoThreadCountLabel => "Automatycznie (zalecane)";

        private static string GetProcessorName()
        {
            try
            {
                using (var searcher = new ManagementObjectSearcher("SELECT Name FROM Win32_Processor"))
                {
                    foreach (var obj in searcher.Get())
                    {
                        var name = obj["Name"]?.ToString()?.Trim();
                        if (!string.IsNullOrEmpty(name))
                        {
                            return name;
                        }
                    }
                }
            }
            catch
            {
                // W przypadku błędu zwróć podstawową informację
            }
            return "Nieznany procesor";
        }

        // === BAZA DANYCH ===
        private string _supabaseUrl = string.Empty;
        public string SupabaseUrl
        {
            get => _supabaseUrl;
            set { if (SetProperty(ref _supabaseUrl, value)) MarkAsDirty(); }
        }

        private string _supabaseAnonKey = string.Empty;
        public string SupabaseAnonKey
        {
            get => _supabaseAnonKey;
            set { if (SetProperty(ref _supabaseAnonKey, value)) MarkAsDirty(); }
        }

        private string _connectionTestStatus = string.Empty;
        public string ConnectionTestStatus
        {
            get => _connectionTestStatus;
            set => SetProperty(ref _connectionTestStatus, value);
        }

        private bool _connectionTestSuccess;
        public bool ConnectionTestSuccess
        {
            get => _connectionTestSuccess;
            set => SetProperty(ref _connectionTestSuccess, value);
        }

        // === ZACHOWANIE PRZY TIMEOUT ===
        private TimeoutBehavior _timeoutBehavior;
        public TimeoutBehavior TimeoutBehavior
        {
            get => _timeoutBehavior;
            set { if (SetProperty(ref _timeoutBehavior, value)) MarkAsDirty(); }
        }

        public bool IsShowResultAndInform
        {
            get => TimeoutBehavior == TimeoutBehavior.ShowResultAndInform;
            set
            {
                if (value)
                {
                    TimeoutBehavior = TimeoutBehavior.ShowResultAndInform;
                    OnPropertyChanged(nameof(IsAskToExtend));
                }
            }
        }

        public bool IsAskToExtend
        {
            get => TimeoutBehavior == TimeoutBehavior.AskToExtend;
            set
            {
                if (value)
                {
                    TimeoutBehavior = TimeoutBehavior.AskToExtend;
                    OnPropertyChanged(nameof(IsShowResultAndInform));
                }
            }
        }

        // === LOGI APLIKACJI (TO-DO) ===
        private AppLogLevel _appLogLevel;
        public AppLogLevel AppLogLevel
        {
            get => _appLogLevel;
            set { if (SetProperty(ref _appLogLevel, value)) MarkAsDirty(); }
        }

        // === LOGI SILNIKÓW (TO-DO) ===
        private SolverLogLevel _solverLogLevel;
        public SolverLogLevel SolverLogLevel
        {
            get => _solverLogLevel;
            set { if (SetProperty(ref _solverLogLevel, value)) MarkAsDirty(); }
        }

        public IAsyncRelayCommand SaveCommand { get; }
        public IRelayCommand ResetTimeoutCommand { get; }
        public IRelayCommand ResetThreadCountCommand { get; }
        public IAsyncRelayCommand TestConnectionCommand { get; }

        public GeneralSettingsViewModel(SettingsService settingsService, SupabaseService supabaseService, AppSettings appSettings, IUxActionOrchestrator orchestrator)
        {
            _settingsService = settingsService;
            _supabaseService = supabaseService;
            _appSettings = appSettings;
            _orchestrator = orchestrator;

            // Załaduj aktualne wartości
            _selectedTheme = appSettings.Theme;
            _originalTheme = appSettings.Theme; // Zapamiętaj oryginalny motyw
            _timeoutMinutesValue = appSettings.TimeoutMinutes;
            _timeoutBehavior = appSettings.TimeoutBehavior;
            _appLogLevel = appSettings.AppLogLevel;
            _solverLogLevel = appSettings.SolverLogLevel;

            // Wielowątkowość
            _isAutoThreadCount = appSettings.CustomThreadCount == null;
            _customThreadCountValue = appSettings.CustomThreadCount ?? ParallelismConfig.OptimalParallelism;

            // Baza danych
            _supabaseUrl = appSettings.SupabaseUrl ?? string.Empty;
            _supabaseAnonKey = appSettings.SupabaseAnonKey ?? string.Empty;

            SaveCommand = new AsyncRelayCommand(SaveSettingsAsync, () => _isDirty);
            ResetTimeoutCommand = new RelayCommand(ResetTimeout);
            ResetThreadCountCommand = new RelayCommand(ResetThreadCount);
            TestConnectionCommand = new AsyncRelayCommand(TestConnectionAsync);

            // Zakończ inicjalizację - teraz zmiany będą aplikowane
            _isInitializing = false;
        }

        public void SetViewId(Guid viewId) => _viewId = viewId;

        /// <summary>
        /// Sprawdza czy są niezapisane zmiany.
        /// </summary>
        public bool HasUnsavedChanges => _isDirty;

        /// <summary>
        /// Przywraca oryginalny motyw jeśli były niezapisane zmiany.
        /// Wywołaj to przy opuszczaniu widoku ustawień (Unloaded).
        /// </summary>
        public void RestoreOriginalThemeIfDirty()
        {
            if (_isDirty && _selectedTheme != _originalTheme)
            {
                // Przywróć oryginalny motyw wizualnie
                ApplyThemeChangeVisually(_originalTheme);
            }
        }

        private void MarkAsDirty()
        {
            if (_isInitializing) return;
            _isDirty = true;
            SaveCommand.NotifyCanExecuteChanged();
        }

        private void ResetTimeout()
        {
            TimeoutMinutesValue = SolverDefaults.TimeoutMinutes.Default;
        }

        private void ResetThreadCount()
        {
            IsAutoThreadCount = true;
            CustomThreadCountValue = ParallelismConfig.OptimalParallelism;
        }

        private async Task TestConnectionAsync()
        {
            if (string.IsNullOrWhiteSpace(SupabaseUrl) || string.IsNullOrWhiteSpace(SupabaseAnonKey))
            {
                ConnectionTestStatus = "Wypełnij oba pola";
                ConnectionTestSuccess = false;
                return;
            }

            ConnectionTestStatus = "Testowanie...";
            ConnectionTestSuccess = false;

            bool success = await _supabaseService.TestConnectionAsync(SupabaseUrl, SupabaseAnonKey);

            if (success)
            {
                ConnectionTestStatus = "✓ Połączenie udane";
                ConnectionTestSuccess = true;
            }
            else
            {
                ConnectionTestStatus = "✗ Błąd połączenia";
                ConnectionTestSuccess = false;
            }
        }

        private async Task SaveSettingsAsync()
        {
            // Sprawdź czy dane bazy się zmieniły
            bool dbChanged = (_appSettings.SupabaseUrl != SupabaseUrl ||
                             _appSettings.SupabaseAnonKey != SupabaseAnonKey);

            if (dbChanged)
            {
                // Test połączenia w tle
                bool testOk = false;
                try
                {
                    await _orchestrator.PerformActionAsync(
                        viewId: _viewId,
                        actionAsync: async () =>
                        {
                            testOk = await _supabaseService.TestConnectionAsync(SupabaseUrl, SupabaseAnonKey);
                            if (!testOk)
                            {
                                throw new Exception("Nie można połączyć się z bazą danych. Sprawdź poprawność danych dostępowych.");
                            }
                        },
                        verificationAsync: null!,
                        successMessage: null!,
                        errorMessageTitle: "Błąd połączenia z bazą"
                    );
                }
                catch
                {
                    return; // Anuluj zapis
                }

                // Jeśli test przeszedł, zapisz wszystko
                var newSettings = _appSettings with
                {
                    Theme = SelectedTheme,
                    TimeoutMinutes = TimeoutMinutesValue,
                    TimeoutBehavior = TimeoutBehavior,
                    AppLogLevel = AppLogLevel,
                    SolverLogLevel = SolverLogLevel,
                    CustomThreadCount = IsAutoThreadCount ? null : CustomThreadCountValue,
                    SupabaseUrl = SupabaseUrl,
                    SupabaseAnonKey = SupabaseAnonKey
                };

                await _settingsService.SaveSettingsAsync(newSettings);
                _appSettings = newSettings;

                // Countdown i restart
                await ShowCountdownAndRestartAsync();
            }
            else
            {
                // Normalny zapis bez restartu
                var newSettings = _appSettings with
                {
                    Theme = SelectedTheme,
                    TimeoutMinutes = TimeoutMinutesValue,
                    TimeoutBehavior = TimeoutBehavior,
                    AppLogLevel = AppLogLevel,
                    SolverLogLevel = SolverLogLevel,
                    CustomThreadCount = IsAutoThreadCount ? null : CustomThreadCountValue
                };

                await _orchestrator.PerformActionAsync(
                    viewId: _viewId,
                    actionAsync: async () => await _settingsService.SaveSettingsAsync(newSettings),
                    verificationAsync: async () =>
                    {
                        var saved = await _settingsService.LoadSettingsAsync(forceReload: true);
                        return saved.Theme == newSettings.Theme &&
                               saved.TimeoutMinutes == newSettings.TimeoutMinutes &&
                               saved.TimeoutBehavior == newSettings.TimeoutBehavior &&
                               saved.CustomThreadCount == newSettings.CustomThreadCount;
                    },
                    successMessage: "Ustawienia ogólne zostały pomyślnie zapisane.",
                    errorMessageTitle: "Błąd zapisu ustawień"
                );

                _appSettings = newSettings;
                _originalTheme = SelectedTheme; // Zaktualizuj oryginalny motyw po zapisie
                _isDirty = false;
                SaveCommand.NotifyCanExecuteChanged();
                WeakReferenceMessenger.Default.Send(new SettingsHaveChangedMessage());

                // Zastosuj zmianę motywu natychmiast
                ApplyThemeChangeVisually(SelectedTheme);
            }
        }

        private async Task ShowCountdownAndRestartAsync()
        {
            // Prosty komunikat - restart po 1 sekundzie
            await Task.Delay(1000);

            // Restart aplikacji
            var exePath = Process.GetCurrentProcess().MainModule?.FileName;
            if (!string.IsNullOrEmpty(exePath))
            {
                Process.Start(exePath);
                Environment.Exit(0);
            }
        }
    }
}