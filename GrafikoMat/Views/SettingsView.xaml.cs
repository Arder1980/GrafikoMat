using GrafikoMat.Controls;
using GrafikoMat.Core.Repositories;
using GrafikoMat.Models;
using GrafikoMat.Services;
using GrafikoMat.ViewModels;
using GrafikoMat.Views.Settings;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media.Animation;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace GrafikoMat.Views
{
    // Nowa, pomocnicza struktura danych dla elementu menu
    public record SettingsMenuItem(string Title, string Subtitle);

    public sealed partial class SettingsView : UserControl
    {
        private IUnitRepository? _unitRepository;
        private SettingsService? _settingsService;
        private SupabaseService? _supabaseService;
        private AppSettings? _appSettings;
        #pragma warning disable CS0067
        public event Action? ReloadRequired;
        #pragma warning restore CS0067
        public event Action<List<UiAction>>? ActionButtonsChanged;

        public SettingsView()
        {
            this.InitializeComponent();

            // ZMIANA: Usunięto "Połączenie z Bazą Danych" z listy (przeniesione do Ustawień Ogólnych)
            SettingsMenu.ItemsSource = new List<SettingsMenuItem>
    {
        new("Ustawienia Ogólne", "Globalne ustawienia aplikacji"),
        new("Zarządzanie Jednostkami", "Dodawanie i edycja szpitali oraz oddziałów."),
        new("Priorytety Obliczeń Grafiku", "Ustalanie kolejności i wagi kryteriów optymalizacji."),
        new("Silnik Obliczeniowy", "Wybór algorytmu używanego do generowania grafików.")
    };

            SettingsMenu.SelectedIndex = -1;
        }
        public void Initialize(IUnitRepository? unitRepository, SettingsService settingsService, SupabaseService supabaseService, AppSettings settings)
        {
            _unitRepository = unitRepository;
            _settingsService = settingsService;
            _supabaseService = supabaseService;
            _appSettings = settings;
        }

        private void SettingsMenu_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (e.AddedItems.FirstOrDefault() is not SettingsMenuItem selectedItem)
            {
                SettingsDetailContent.Content = null;
                return;
            }
            LoadSubView(selectedItem.Title);
        }

        private async void LoadSubView(string? selectedItemName)
        {
            // Wyczyść akcje z poprzedniego widoku
            ActionButtonsChanged?.Invoke(new List<UiAction>());

            if (_settingsService == null)
            {
                SettingsDetailContent.Content = null;
                return;
            }

            // Fade out poprzedniego widoku
            if (SettingsDetailContent.Content != null)
            {
                var fadeOut = new DoubleAnimation
                {
                    From = 1.0,
                    To = 0.0,
                    Duration = TimeSpan.FromMilliseconds(150),
                    EasingFunction = new CubicEase { EasingMode = EasingMode.EaseIn }
                };

                var storyboard = new Storyboard();
                storyboard.Children.Add(fadeOut);
                Storyboard.SetTarget(fadeOut, SettingsDetailContent);
                Storyboard.SetTargetProperty(fadeOut, "Opacity");

                storyboard.Begin();
                await Task.Delay(150);
            }

            // Upewniamy się, że zawsze mamy najświeższe ustawienia
            _appSettings = await _settingsService.LoadSettingsAsync(forceReload: true);
            object? viewToLoad = null;

            switch (selectedItemName)
            {
                case "Ustawienia Ogólne":
                    if (_appSettings != null && _supabaseService != null)
                    {
                        var generalView = new GeneralSettingsView(_settingsService, _supabaseService, _appSettings);
                        generalView.ActionsChanged += (actions) =>
                        {
                            ActionButtonsChanged?.Invoke(actions);
                        };
                        var containerGeneral = new ActionContainer { Content = generalView };
                        generalView.ViewModel.SetViewId(containerGeneral.GetViewId());
                        viewToLoad = containerGeneral;
                    }
                    break;
                case "Zarządzanie Jednostkami":
                    if (_unitRepository != null)
                    {
                        var unitsView = new UnitsSettingsView(_unitRepository);
                        unitsView.ActionsChanged += (actions) =>
                        {
                            ActionButtonsChanged?.Invoke(actions);
                        };
                        viewToLoad = unitsView;
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
                        prioritiesView.ActionsChanged += (actions) =>
                        {
                            ActionButtonsChanged?.Invoke(actions);
                        };
                        var containerPriorities = new ActionContainer { Content = prioritiesView };
                        prioritiesViewModel.SetViewId(containerPriorities.GetViewId());
                        viewToLoad = containerPriorities;
                    }
                    break;
                case "Silnik Obliczeniowy":
                    if (_appSettings != null)
                    {
                        var engineView = new EngineSettingsView(_settingsService, _appSettings);
                        engineView.ActionsChanged += (actions) =>
                        {
                            ActionButtonsChanged?.Invoke(actions);
                        };
                        var containerEngine = new ActionContainer { Content = engineView };
                        engineView.ViewModel.SetViewId(containerEngine.GetViewId());
                        viewToLoad = containerEngine;
                    }
                    break;
                    // USUNIĘTE: case "Połączenie z Bazą Danych" - przeniesione do Ustawień Ogólnych
            }

            // Ustaw nowy widok
            SettingsDetailContent.Content = viewToLoad;

            // Fade in nowego widoku
            if (viewToLoad != null)
            {
                var fadeIn = new DoubleAnimation
                {
                    From = 0.0,
                    To = 1.0,
                    Duration = TimeSpan.FromMilliseconds(200),
                    EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
                };

                var storyboard = new Storyboard();
                storyboard.Children.Add(fadeIn);
                Storyboard.SetTarget(fadeIn, SettingsDetailContent);
                Storyboard.SetTargetProperty(fadeIn, "Opacity");

                storyboard.Begin();
            }
        }
    }
}