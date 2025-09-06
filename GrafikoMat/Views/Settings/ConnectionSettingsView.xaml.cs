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

        public ConnectionSettingsView()
        {
            this.InitializeComponent();
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
            await RunConnectionTestAsync();
        }

        private async void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            if (_settingsService == null || _appSettings == null) return;

            SetBusy(true);

            var newSettings = _appSettings with
            {
                SupabaseUrl = UrlTextBox.Text,
                SupabaseAnonKey = ApiKeyTextBox.Text
            };

            await _settingsService.SaveSettingsAsync(newSettings);

            // Po zapisaniu, automatycznie uruchamiamy test
            bool isConnectionOk = await RunConnectionTestAsync();

            SetBusy(false);

            // Przeładowujemy usługi w tle tylko jeśli połączenie jest prawidłowe
            if (isConnectionOk)
            {
                ReloadRequired?.Invoke();
            }
        }

        private async Task<bool> RunConnectionTestAsync()
        {
            StatusInfoBar.IsOpen = false;
            var url = UrlTextBox.Text;
            var key = ApiKeyTextBox.Text;

            if (string.IsNullOrWhiteSpace(url) || string.IsNullOrWhiteSpace(key))
            {
                StatusInfoBar.Severity = InfoBarSeverity.Error;
                StatusInfoBar.Message = "URL i klucz API nie mogą być puste.";
                StatusInfoBar.IsOpen = true;
                return false;
            }

            try
            {
                var options = new SupabaseOptions { AutoConnectRealtime = false, AutoRefreshToken = false };
                var tempClient = new Client(url, key, options);
                await tempClient.From<Unit>().Select("id").Limit(1).Get();

                StatusInfoBar.Severity = InfoBarSeverity.Success;
                StatusInfoBar.Message = "Połączenie udane!";
                StatusInfoBar.IsOpen = true;
                return true;
            }
            catch (Exception ex)
            {
                StatusInfoBar.Severity = InfoBarSeverity.Error;
                StatusInfoBar.Message = $"Błąd połączenia: {ex.Message}";
                StatusInfoBar.IsOpen = true;
                return false;
            }
        }

        private void SetBusy(bool isBusy)
        {
            MainPanel.Opacity = isBusy ? 0.5 : 1.0;
            LoadingRing.IsActive = isBusy;
            TestButton.IsEnabled = !isBusy;
            SaveButton.IsEnabled = !isBusy;
        }
    }
}