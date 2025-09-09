using GrafikoMat.Core.Data;
using GrafikoMat.Services;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Supabase;
using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;

namespace GrafikoMat.Views.Settings
{
    public sealed partial class ConnectionSettingsView : UserControl, INotifyPropertyChanged
    {
        public event Action? ReloadRequired;
        public event PropertyChangedEventHandler? PropertyChanged;

        private SettingsService? _settingsService;
        private AppSettings? _appSettings;

        #region Właściwości do bindowania danych (INotifyPropertyChanged)

        private bool _isLoading;
        public bool IsLoading { get => _isLoading; set => SetProperty(ref _isLoading, value); }

        private bool _isStatusMessageOpen;
        public bool IsStatusMessageOpen { get => _isStatusMessageOpen; set => SetProperty(ref _isStatusMessageOpen, value); }

        private string _statusMessageTitle = string.Empty;
        public string StatusMessageTitle { get => _statusMessageTitle; set => SetProperty(ref _statusMessageTitle, value); }

        private string _statusMessage = string.Empty;
        public string StatusMessage { get => _statusMessage; set => SetProperty(ref _statusMessage, value); }

        private InfoBarSeverity _statusMessageSeverity = InfoBarSeverity.Informational;
        public InfoBarSeverity StatusMessageSeverity { get => _statusMessageSeverity; set { SetProperty(ref _statusMessageSeverity, value); OnPropertyChanged(nameof(IsError)); } }

        public bool IsError => StatusMessageSeverity == InfoBarSeverity.Error;

        #endregion

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
            IsLoading = true;
            try
            {
                bool isOk = await RunConnectionTestAsync();
                if (isOk)
                {
                    await ShowTemporaryMessage("Sukces!", "Połączenie z bazą Supabase jest aktywne.", InfoBarSeverity.Success);
                }
            }
            finally
            {
                IsLoading = false;
            }
        }

        private async void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            if (_settingsService == null || _appSettings == null) return;

            IsLoading = true;
            try
            {
                var newSettings = _appSettings with
                {
                    SupabaseUrl = UrlTextBox.Text,
                    SupabaseAnonKey = ApiKeyTextBox.Text
                };
                await _settingsService.SaveSettingsAsync(newSettings);

                bool isConnectionOk = await RunConnectionTestAsync(showErrors: false);

                if (isConnectionOk)
                {
                    await ShowTemporaryMessage("Zapisano i połączono", "Ustawienia zostały zapisane, a połączenie z bazą danych jest aktywne.", InfoBarSeverity.Success);
                    ReloadRequired?.Invoke();
                }
                else
                {
                    ShowStatusMessage("Zapisano, lecz brak połączenia", "Ustawienia zostały zapisane, ale nie udało się nawiązać połączenia z bazą. Sprawdź poprawność danych.", InfoBarSeverity.Warning);
                }
            }
            finally
            {
                IsLoading = false;
            }
        }

        private async Task<bool> RunConnectionTestAsync(bool showErrors = true)
        {
            IsStatusMessageOpen = false;
            try
            {
                var url = UrlTextBox.Text;
                var key = ApiKeyTextBox.Text;

                if (string.IsNullOrWhiteSpace(url) || string.IsNullOrWhiteSpace(key))
                {
                    if (showErrors) ShowStatusMessage("Błąd Walidacji", "URL i klucz API nie mogą być puste.", InfoBarSeverity.Error);
                    return false;
                }

                var options = new SupabaseOptions { AutoConnectRealtime = false, AutoRefreshToken = false };
                var tempClient = new Client(url, key, options);
                await tempClient.From<Unit>().Select("id").Limit(1).Get();
                return true;
            }
            catch (Exception ex)
            {
                if (showErrors) ShowStatusMessage("Błąd połączenia", $"Sprawdź dane i spróbuj ponownie. Szczegóły: {ex.Message}", InfoBarSeverity.Error);
                return false;
            }
        }

        #region Metody pomocnicze (InfoBar, INotifyPropertyChanged)

        private void ShowStatusMessage(string title, string message, InfoBarSeverity severity)
        {
            DispatcherQueue.TryEnqueue(() =>
            {
                StatusMessageTitle = title;
                StatusMessage = message;
                StatusMessageSeverity = severity;
                IsStatusMessageOpen = true;
            });
        }

        private async Task ShowTemporaryMessage(string title, string message, InfoBarSeverity severity)
        {
            ShowStatusMessage(title, message, severity);
            await Task.Delay(3000);
            DispatcherQueue.TryEnqueue(() =>
            {
                if (StatusMessageSeverity != InfoBarSeverity.Error)
                {
                    IsStatusMessageOpen = false;
                }
            });
        }

        private void SetProperty<T>(ref T storage, T value, [CallerMemberName] string? propertyName = null)
        {
            if (Equals(storage, value)) return;
            storage = value;
            OnPropertyChanged(propertyName);
        }

        private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        private void StatusInfoBar_Closed(InfoBar sender, InfoBarClosedEventArgs args)
        {
            IsStatusMessageOpen = false;
        }

        #endregion
    }
}