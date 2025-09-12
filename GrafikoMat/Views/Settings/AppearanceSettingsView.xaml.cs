using GrafikoMat.Services;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace GrafikoMat.Views.Settings
{
    public sealed partial class AppearanceSettingsView : UserControl
    {
        private SettingsService? _settingsService;
        private AppSettings? _appSettings;
        private bool _isInitializing = true;

        public AppearanceSettingsView()
        {
            this.InitializeComponent();
        }

        public void Initialize(SettingsService settingsService, AppSettings appSettings)
        {
            _settingsService = settingsService;
            _appSettings = appSettings;

            // Ustawienie początkowego zaznaczenia RadioButton na podstawie wczytanych ustawień
            switch (_appSettings.Theme)
            {
                case AppTheme.Light:
                    LightRadioButton.IsChecked = true;
                    break;
                case AppTheme.Dark:
                    DarkRadioButton.IsChecked = true;
                    break;
                default:
                    SystemRadioButton.IsChecked = true;
                    break;
            }
            _isInitializing = false;
        }

        private async void ThemeRadioButton_Checked(object sender, RoutedEventArgs e)
        {
            // Ignorujemy zdarzenie wywołane podczas inicjalizacji widoku
            if (_isInitializing || _settingsService == null || _appSettings == null) return;
            if (sender is not RadioButton selectedRadioButton) return;

            AppTheme newTheme;
            ElementTheme newElementTheme;

            if (selectedRadioButton == LightRadioButton)
            {
                newTheme = AppTheme.Light;
                newElementTheme = ElementTheme.Light;
            }
            else if (selectedRadioButton == DarkRadioButton)
            {
                newTheme = AppTheme.Dark;
                newElementTheme = ElementTheme.Dark;
            }
            else
            {
                newTheme = AppTheme.SystemDefault;
                newElementTheme = ElementTheme.Default;
            }

            // 1. Zastosuj zmianę natychmiast w UI
            if (App.MainRoot.Content is FrameworkElement rootElement)
            {
                rootElement.RequestedTheme = newElementTheme;
            }

            // 2. Zapisz nowe ustawienie
            var newSettings = _appSettings with { Theme = newTheme };
            await _settingsService.SaveSettingsAsync(newSettings);
            _appSettings = newSettings; // Zaktualizuj lokalną kopię ustawień
        }
    }
}