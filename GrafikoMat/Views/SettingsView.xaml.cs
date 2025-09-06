using GrafikoMat.Services;
using GrafikoMat.Views.Settings;
using Microsoft.UI.Xaml.Controls;
using System.Linq;

namespace GrafikoMat.Views
{
    public sealed partial class SettingsView : UserControl
    {
        private DataService? _dataService;
        private SettingsService? _settingsService;
        private AppSettings? _appSettings;
        public event System.Action? ReloadRequired;

        public SettingsView()
        {
            this.InitializeComponent();
            SettingsMenu.ItemsSource = new[]
            {
                "Baza danych",
                "Jednostki",
                "Priorytety",
                "Wybór silnika",
                "Wygląd"
            };
            // ZMIANA: Usunęliśmy automatyczne zaznaczanie
            SettingsMenu.SelectedIndex = -1;
        }

        public void Initialize(DataService? dataService, SettingsService settingsService, AppSettings settings)
        {
            _dataService = dataService;
            _settingsService = settingsService;
            _appSettings = settings;

            // ZMIANA: Czyścimy zawartość przy każdym wejściu do ustawień
            SettingsDetailContent.Content = null;
            SettingsMenu.SelectedIndex = -1;
        }

        private void SettingsMenu_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (e.AddedItems.FirstOrDefault() is not string selectedItem) return;
            LoadSubView(selectedItem);
        }

        private void LoadSubView(string? selectedItem)
        {
            if (_settingsService == null || _appSettings == null)
            {
                SettingsDetailContent.Content = null;
                return;
            }

            switch (selectedItem)
            {
                case "Baza danych":
                    var connectionView = new ConnectionSettingsView();
                    connectionView.Initialize(_settingsService, _appSettings);
                    connectionView.ReloadRequired += () => ReloadRequired?.Invoke();
                    SettingsDetailContent.Content = connectionView;
                    break;

                case "Jednostki":
                    if (_dataService != null)
                    {
                        var unitsView = new UnitsSettingsView();
                        unitsView.Initialize(_dataService);
                        SettingsDetailContent.Content = unitsView;
                    }
                    else
                    {
                        SettingsDetailContent.Content = new TextBlock
                        {
                            Text = "Skonfiguruj i zapisz połączenie z bazą danych, aby zarządzać jednostkami.",
                            VerticalAlignment = Microsoft.UI.Xaml.VerticalAlignment.Center,
                            HorizontalAlignment = Microsoft.UI.Xaml.HorizontalAlignment.Center
                        };
                    }
                    break;

                case "Priorytety":
                    SettingsDetailContent.Content = new PrioritiesSettingsView();
                    break;

                case "Wybór silnika":
                    SettingsDetailContent.Content = new EngineSettingsView();
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