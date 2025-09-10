using GrafikoMat.Controls;
using GrafikoMat.Core.Repositories;
using GrafikoMat.Services;
using GrafikoMat.Views.Settings;
using Microsoft.UI.Xaml.Controls;
using System;
using System.Linq;

namespace GrafikoMat.Views
{
    public sealed partial class SettingsView : UserControl
    {
        private IUnitRepository? _unitRepository;
        private SettingsService? _settingsService;
        private AppSettings? _appSettings;
        public event Action? ReloadRequired;

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

            _appSettings = await _settingsService.LoadSettingsAsync();

            // Domyślnie ładujemy widok bezpośrednio.
            // Dla widoków z zapisem opakujemy je w ActionContainer.
            object? viewToLoad = null;

            switch (selectedItem)
            {
                case "Baza danych":
                    var connectionView = new ConnectionSettingsView();
                    connectionView.Initialize(_settingsService, _appSettings);
                    connectionView.ReloadRequired += () => ReloadRequired?.Invoke();
                    viewToLoad = connectionView;
                    break;
                case "Jednostki":
                    if (_unitRepository != null)
                    {
                        viewToLoad = new UnitsSettingsView(_unitRepository);
                    }
                    else
                    {
                        viewToLoad = new TextBlock { Text = "Skonfiguruj połączenie z bazą danych, aby zarządzać jednostkami.", VerticalAlignment = Microsoft.UI.Xaml.VerticalAlignment.Center, HorizontalAlignment = Microsoft.UI.Xaml.HorizontalAlignment.Center };
                    }
                    break;
                case "Priorytety":
                    viewToLoad = new PrioritiesSettingsView();
                    break;
                case "Wybór silnika":
                    if (_appSettings != null)
                    {
                        var engineView = new EngineSettingsView(_settingsService, _appSettings);

                        // Tworzymy kontener i umieszczamy w nim nasz widok
                        var container = new ActionContainer { Content = engineView };

                        // Przekazujemy ID kontenera do ViewModelu, aby wiedział, do kogo wysyłać komunikaty
                        engineView.ViewModel.SetViewId(container.GetViewId());
                        viewToLoad = container;
                    }
                    break;
                case "Wygląd":
                    viewToLoad = new AppearanceSettingsView();
                    break;
            }

            SettingsDetailContent.Content = viewToLoad;
        }
    }
}