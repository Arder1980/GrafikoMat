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
                SupabaseUrl = UrlTextBox.Text,
                SupabaseAnonKey = ApiKeyTextBox.Text
            };

            try
            {
                await _orchestrator.PerformActionAsync(
                    viewId: ActionContainer.GetViewId(),
                    actionAsync: async () =>
                    {
                        if (!await RunConnectionTestAsync())
                        {
                            throw new Exception("Test połączenia z nowymi danymi nie powiódł się.");
                        }
                        await _settingsService.SaveSettingsAsync(newSettings);
                    },
                    verificationAsync: async () => true,
                    successMessage: "Ustawienia zostały zapisane. Aplikacja zostanie przeładowana.",
                    errorMessageTitle: "Błąd zapisu"
                );

                await Task.Delay(1500);
                ReloadRequired?.Invoke();
            }
            catch (Exception)
            {
                // Orkiestrator już wyświetlił błąd, więc nie robimy nic więcej
            }
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

            await tempClient.From<Unit>().Select("id").Limit(1).Get();
            return true;
        }
    }
}