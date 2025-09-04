using GrafikoMat.Services;
using GrafikoMat.Views.Settings; // ZMIANA: Dodajemy using do naszego nowego folderu
using Microsoft.UI.Xaml.Controls;
using System.Linq;

namespace GrafikoMat.Views
{
    public sealed partial class SettingsView : UserControl
    {
        // ZMIANA: Dodajemy pola do przekazywania dalej
        private SettingsService? _settingsService;
        private AppSettings? _appSettings;
        public event System.Action? ReloadRequired;

        public SettingsView()
        {
            this.InitializeComponent();
            SettingsMenu.ItemsSource = new[]
            {
                "Profile Jednostek",
                "Lekarze i Użytkownicy",
                "Silnik i Priorytety",
                "Wygląd i Zachowanie"
            };
            SettingsMenu.SelectedIndex = 0;
        }

        // ZMIANA: Nowa metoda do inicjalizacji z MainWindow
        public void Initialize(SettingsService service, AppSettings settings)
        {
            _settingsService = service;
            _appSettings = settings;

            // Odświeżamy widok po wejściu
            LoadSubView(SettingsMenu.Items[0] as string);
        }

        private void SettingsMenu_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (e.AddedItems.FirstOrDefault() is not string selectedItem) return;
            LoadSubView(selectedItem);
        }

        // ZMIANA: Nowa, rozbudowana metoda do ładowania pod-widoków
        private void LoadSubView(string? selectedItem)
        {
            if (_settingsService == null || _appSettings == null) return;

            switch (selectedItem)
            {
                case "Profile Jednostek":
                    var profilesView = new ProfilesSettingsView();
                    // Przekazujemy serwisy i ustawienia oraz subskrybujemy event
                    profilesView.Initialize(_settingsService, _appSettings);
                    profilesView.ReloadRequired += () => ReloadRequired?.Invoke();
                    SettingsDetailContent.Content = profilesView;
                    break;
                case "Lekarze i Użytkownicy":
                    SettingsDetailContent.Content = new TextBlock { Text = "Tutaj będzie widok zarządzania lekarzami.", VerticalAlignment = Microsoft.UI.Xaml.VerticalAlignment.Center, HorizontalAlignment = Microsoft.UI.Xaml.HorizontalAlignment.Center };
                    break;
                default:
                    SettingsDetailContent.Content = null;
                    break;
            }
        }
    }
}