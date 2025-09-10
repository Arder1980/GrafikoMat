using GrafikoMat.Core.Repositories; // NOWY USING
using GrafikoMat.Services;
using GrafikoMat.Views.Settings;
using Microsoft.UI.Xaml.Controls;
using System.Linq;

namespace GrafikoMat.Views
{
    public sealed partial class SettingsView : UserControl
    {
        // ZMIANA: Zależności
        private IUnitRepository? _unitRepository;
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
            SettingsMenu.SelectedIndex = -1;
        }

        // ZMIANA: Nowa sygnatura metody Initialize
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
                    // ZMIANA: Sprawdzamy i przekazujemy repozytorium
                    if (_unitRepository != null)
                    {
                        var unitsView = new UnitsSettingsView();
                        unitsView.Initialize(_unitRepository);
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