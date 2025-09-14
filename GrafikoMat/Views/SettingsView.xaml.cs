using GrafikoMat.Controls;
using GrafikoMat.Core.Repositories;
using GrafikoMat.Services;
using GrafikoMat.ViewModels;
using GrafikoMat.Views.Settings;
using Microsoft.UI.Xaml.Controls;
using System;
using System.Collections.Generic;
using System.Linq;

namespace GrafikoMat.Views
{
    // Nowa, pomocnicza struktura danych dla elementu menu
    public record SettingsMenuItem(string Title, string Subtitle);

    public sealed partial class SettingsView : UserControl
    {
        private IUnitRepository? _unitRepository;
        private SettingsService? _settingsService;
        private AppSettings? _appSettings;
        public event Action? ReloadRequired;
        public SettingsView()
        {
            this.InitializeComponent();

            // ZMIANA: Użycie nowej listy obiektów z poprawną kolejnością
            SettingsMenu.ItemsSource = new List<SettingsMenuItem>
            {
                new("Wygląd i Motyw", "Zmiana jasnego i ciemnego motywu aplikacji."),
                new("Połączenie z Bazą Danych", "Konfiguracja adresu URL i klucza API dla Supabase."),
                new("Zarządzanie Jednostkami", "Dodawanie i edycja szpitali oraz oddziałów."),
                new("Priorytety Obliczeń Grafiku", "Ustalanie kolejności i wagi kryteriów optymalizacji."),
                new("Silnik Obliczeniowy", "Wybór algorytmu używanego do generowania grafików.")
            };

            SettingsMenu.SelectedIndex = -1;
        }

        public void Initialize(IUnitRepository? unitRepository, SettingsService settingsService, AppSettings settings)
        {
            _unitRepository = unitRepository;
            _settingsService = settingsService;
            _appSettings = settings;

            // ZMIANA: Usunięto domyślne zaznaczanie pierwszego elementu
            // SettingsMenu.SelectedIndex = 0; 
        }

        private void SettingsMenu_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            // ZMIANA: Dostosowanie do nowego typu danych w menu
            if (e.AddedItems.FirstOrDefault() is not SettingsMenuItem selectedItem)
            {
                SettingsDetailContent.Content = null;
                return;
            }
            LoadSubView(selectedItem.Title);
        }

        private async void LoadSubView(string? selectedItemName)
        {
            if (_settingsService == null)
            {
                SettingsDetailContent.Content = null;
                return;
            }

            // Upewniamy się, że zawsze mamy najświeższe ustawienia
            _appSettings = await _settingsService.LoadSettingsAsync(forceReload: true);
            object? viewToLoad = null;

            switch (selectedItemName)
            {
                case "Połączenie z Bazą Danych":
                    var connectionView = new ConnectionSettingsView();
                    connectionView.Initialize(_settingsService, _appSettings);
                    connectionView.ReloadRequired += () => ReloadRequired?.Invoke();
                    viewToLoad = connectionView;
                    break;
                case "Zarządzanie Jednostkami":
                    if (_unitRepository != null)
                    {
                        viewToLoad = new UnitsSettingsView(_unitRepository);
                    }
                    else
                    {
                        viewToLoad = new TextBlock { Text = "Skonfiguruj połączenie z bazą danych, aby zarządzać jednostkami.", VerticalAlignment = Microsoft.UI.Xaml.VerticalAlignment.Center, HorizontalAlignment = Microsoft.UI.Xaml.HorizontalAlignment.Center };
                    }
                    break;
                case "Priorytety Obliczeń Grafiku":
                    if (_appSettings != null)
                    {
                        var prioritiesViewModel = new PrioritiesSettingsViewModel(_settingsService, _appSettings, this.DispatcherQueue);
                        var prioritiesView = new PrioritiesSettingsView(prioritiesViewModel);
                        var container = new ActionContainer { Content = prioritiesView };
                        prioritiesViewModel.SetViewId(container.GetViewId());
                        viewToLoad = container;
                    }
                    break;
                case "Silnik Obliczeniowy":
                    if (_appSettings != null)
                    {
                        var engineView = new EngineSettingsView(_settingsService, _appSettings);
                        var container = new ActionContainer { Content = engineView };
                        engineView.ViewModel.SetViewId(container.GetViewId());
                        viewToLoad = container;
                    }
                    break;
                case "Wygląd i Motyw":
                    var appearanceView = new AppearanceSettingsView();
                    if (_appSettings != null)
                    {
                        appearanceView.Initialize(_settingsService, _appSettings);
                    }
                    viewToLoad = appearanceView;
                    break;
            }

            SettingsDetailContent.Content = viewToLoad;
        }
    }
}