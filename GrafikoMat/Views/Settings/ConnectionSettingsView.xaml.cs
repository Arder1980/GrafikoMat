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
        public event Action? ReloadRequired;

        private SettingsService? _settingsService;
        private AppSettings? _appSettings;
        private readonly IUxActionOrchestrator _orchestrator;

        public ConnectionSettingsView()
        {
            this.InitializeComponent();
            _orchestrator = ServiceProvider.GetService<IUxActionOrchestrator>();
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
            await _orchestrator.PerformActionAsync(
                viewId: ActionContainer.GetViewId(),
                actionAsync: async () =>
                {
                    // W tym przypadku sama weryfikacja jest akcją
                    await Task.CompletedTask;
                },
                verificationAsync: async () =>
                {
                    return await RunConnectionTestAsync();
                },
                successMessage: "Połączenie z bazą danych Supabase jest aktywne.",
                errorMessageTitle: "Błąd połączenia"
            );
        }

        private async void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            if (_settingsService == null || _appSettings == null) return;

            var newSettings = _appSettings with
            {
                SupabaseUrl = UrlTextBox.Text,
                SupabaseAnonKey = ApiKeyTextBox.Text
            };

            await _orchestrator.PerformActionAsync(
                viewId: ActionContainer.GetViewId(),
                actionAsync: async () =>
                {
                    await _settingsService.SaveSettingsAsync(newSettings);
                },
                verificationAsync: async () =>
                {
                    // Weryfikacja polega na udanym teście połączenia z nowymi danymi
                    return await RunConnectionTestAsync();
                },
                successMessage: "Ustawienia zostały zapisane, a połączenie z bazą danych jest aktywne.",
                errorMessageTitle: "Błąd zapisu"
            );

            // Jeśli weryfikacja się powiodła (orka nie rzuciła wyjątku), przeładuj
            ReloadRequired?.Invoke();
        }

        private async Task<bool> RunConnectionTestAsync()
        {
            var url = UrlTextBox.Text;
            var key = ApiKeyTextBox.Text;

            if (string.IsNullOrWhiteSpace(url) || string.IsNullOrWhiteSpace(key))
            {
                throw new Exception("URL i klucz API nie mogą być puste.");
            }

            var options = new SupabaseOptions { AutoConnectRealtime = false, AutoRefreshToken = false };
            var tempClient = new Client(url, key, options);

            // Próba wykonania prostego zapytania do bazy
            await tempClient.From<Unit>().Select("id").Limit(1).Get();
            return true;
        }
    }
}