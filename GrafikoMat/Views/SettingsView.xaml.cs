using GrafikoMat.Controls;
using GrafikoMat.Core.Repositories;
using GrafikoMat.Services;
using GrafikoMat.ViewModels;
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
                    if (_appSettings != null)
                    {
                        // ZMIANA: Dodano brakujący argument 'this.DispatcherQueue' do konstruktora
                        var prioritiesViewModel = new PrioritiesSettingsViewModel(_settingsService, _appSettings, this.DispatcherQueue);
                        var prioritiesView = new PrioritiesSettingsView(prioritiesViewModel);
                        var container = new ActionContainer { Content = prioritiesView };
                        prioritiesViewModel.SetViewId(container.GetViewId());
                        viewToLoad = container;
                    }
                    break;
                case "Wybór silnika":
                    if (_appSettings != null)
                    {
                        var engineView = new EngineSettingsView(_settingsService, _appSettings);
                        var container = new ActionContainer { Content = engineView };
                        engineView.ViewModel.SetViewId(container.GetViewId());
                        viewToLoad = container;
                    }
                    break;
                case "Wygląd":
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