using GrafikoMat.Core.Data;
using GrafikoMat.Services;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Supabase;
using System;
using System.Threading.Tasks;

namespace GrafikoMat.Views.Settings
{
    public sealed partial class ConnectionSettingsView : UserControl
    {
        public event Action? ConnectionEstablished;
        // ZMIANA: Przywrócono ReloadRequired
        public event Action? ReloadRequired;

        private SettingsService? _settingsService;
        private AppSettings? _appSettings;
        private readonly IUxActionOrchestrator _orchestrator;

        public bool IsInitialSetupMode
        {
            get => (bool)GetValue(IsInitialSetupModeProperty);
            set => SetValue(IsInitialSetupModeProperty, value);
        }
        public static readonly DependencyProperty IsInitialSetupModeProperty =
            DependencyProperty.Register(nameof(IsInitialSetupMode), typeof(bool), typeof(ConnectionSettingsView), new PropertyMetadata(false));

        public ConnectionSettingsView()
        {
            this.InitializeComponent();
            _orchestrator = ServiceProvider.GetService<IUxActionOrchestrator>();
            DataContext = this;
        }

        public void Initialize(SettingsService service, AppSettings settings)
        {
            _settingsService = service;
            _appSettings = settings;

            if (_appSettings != null)
            {
                UrlTextBox.Text = _appSettings.SupabaseUrl;
                ApiKeyTextBox.Text = _appSettings.SupabaseAnonKey;
            }
        }

        private async void TestButton_Click(object sender, RoutedEventArgs e)
        {
            // Ta metoda będzie wywoływana tylko, gdy IsInitialSetupMode jest false
            await _orchestrator.PerformActionAsync(
                viewId: ActionContainer.GetViewId(),
                actionAsync: async () => { await Task.CompletedTask; }, // Sama weryfikacja jest akcją
                verificationAsync: RunConnectionTestAsync,
                successMessage: "Połączenie z bazą danych Supabase jest aktywne.",
                errorMessageTitle: "Błąd połączenia"
            );
        }

        private async void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            if (_settingsService == null || _appSettings == null) return;

            var newSettings = _appSettings with
            {
                SupabaseUrl = UrlTextBox.Text.Trim(),
                SupabaseAnonKey = ApiKeyTextBox.Text.Trim()
            };

            bool success = false;
            try
            {
                await _orchestrator.PerformActionAsync(
                    viewId: ActionContainer.GetViewId(),
                    actionAsync: async () =>
                    {
                        await _settingsService.SaveSettingsAsync(newSettings);
                    },
                    verificationAsync: RunConnectionTestAsync,
                    successMessage: "Pomyślnie nawiązano połączenie z bazą danych GrafikoMat w Supabase!",
                    errorMessageTitle: "Błąd zapisu lub połączenia"
                );
                success = true; // Doszliśmy tutaj, więc się udało
            }
            catch (Exception)
            {
                success = false; // Orkiestrator obsłużył błąd
            }

            // ZMIANA: Wywołaj odpowiedni event w zależności od trybu
            if (success)
            {
                if (IsInitialSetupMode)
                {
                    // W trybie inicjalnym, wywołaj ConnectionEstablished
                    ConnectionEstablished?.Invoke();
                }
                else
                {
                    // W normalnych ustawieniach, daj czas na przeczytanie komunikatu i wywołaj ReloadRequired
                    await Task.Delay(1500);
                    ReloadRequired?.Invoke();
                }
            }
        }


        private async Task<bool> RunConnectionTestAsync()
        {
            var url = UrlTextBox.Text.Trim();
            var key = ApiKeyTextBox.Text.Trim();

            if (string.IsNullOrWhiteSpace(url) || string.IsNullOrWhiteSpace(key))
            {
                throw new Exception("URL i klucz API nie mogą być puste.");
            }
            if (!url.StartsWith("http://") && !url.StartsWith("https://"))
            {
                throw new FormatException("Wprowadzony URL jest nieprawidłowy. Powinien zaczynać się od 'http://' lub 'https://'.");
            }

            var options = new SupabaseOptions { AutoConnectRealtime = false, AutoRefreshToken = false };
            var tempClient = new Client(url, key, options);

            try
            {
                await tempClient.From<Unit>().Select("id").Limit(1).Get();
                return true;
            }
            catch (Exception ex)
            {
                throw new Exception($"Nie można połączyć się z bazą danych Supabase. Sprawdź poprawność URL i klucza API, stan projektu w Supabase oraz połączenie internetowe. Szczegóły: {ex.Message}", ex);
            }
        }
    }
}