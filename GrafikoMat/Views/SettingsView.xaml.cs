using GrafikoMat.Core.Repositories;
using GrafikoMat.Services;
using GrafikoMat.Views.Settings;
using Microsoft.UI.Xaml.Controls;
using System.Linq;

namespace GrafikoMat.Views
{
    public sealed partial class SettingsView : UserControl
    {
        private IUnitRepository? _unitRepository;
        private SettingsService? _settingsService;
        private AppSettings? _appSettings;
        public event System.Action? ReloadRequired;

        public SettingsView()
        {
            this.InitializeComponent();
            SettingsMenu.ItemsSource = new[]
            {
                "Baza danych", "Jednostki", "Priorytety", "Wybór silnika", "Wygląd"
            };
            SettingsMenu.SelectedIndex = -1;
        }

        public void Initialize(IUnitRepository? unitRepository, SettingsService settingsService, AppSettings settings)
        {
            _unitRepository = unitRepository;
            _settingsService = settingsService;
            _appSettings = settings;

            SettingsMenu.SelectedIndex = 0;
        }

        private void SettingsMenu_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (e.AddedItems.FirstOrDefault() is not string selectedItem)
            {
                SettingsDetailContent.Content = null;
                return;
            }
            LoadSubView(selectedItem);
        }

        private async void LoadSubView(string? selectedItem)
        {
            if (_settingsService == null)
            {
                SettingsDetailContent.Content = null;
                return;
            }

            // KLUCZOWA ZMIANA: Zawsze pobieramy najnowszą wersję ustawień z serwisu
            _appSettings = await _settingsService.LoadSettingsAsync();

            switch (selectedItem)
            {
                case "Baza danych":
                    var connectionView = new ConnectionSettingsView();
                    connectionView.Initialize(_settingsService, _appSettings);
                    connectionView.ReloadRequired += () => ReloadRequired?.Invoke();
                    SettingsDetailContent.Content = connectionView;
                    break;
                case "Jednostki":
                    if (_unitRepository != null)
                    {
                        SettingsDetailContent.Content = new UnitsSettingsView(_unitRepository);
                    }
                    else
                    {
                        SettingsDetailContent.Content = new TextBlock { Text = "Skonfiguruj połączenie z bazą danych, aby zarządzać jednostkami.", VerticalAlignment = Microsoft.UI.Xaml.VerticalAlignment.Center, HorizontalAlignment = Microsoft.UI.Xaml.HorizontalAlignment.Center };
                    }
                    break;
                case "Priorytety":
                    SettingsDetailContent.Content = new PrioritiesSettingsView();
                    break;
                case "Wybór silnika":
                    SettingsDetailContent.Content = new EngineSettingsView(_settingsService, _appSettings);
                    break;
                case "Wygląd":
                    SettingsDetailContent.Content = new AppearanceSettingsView();
                    break;
                default:
                    SettingsDetailContent.Content = null;
                    break;
            }
        }
    }
}