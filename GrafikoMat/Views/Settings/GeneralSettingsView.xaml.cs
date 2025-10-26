using GrafikoMat.Models;
using GrafikoMat.Services;
using GrafikoMat.ViewModels;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace GrafikoMat.Views.Settings
{
    public sealed partial class GeneralSettingsView : UserControl
    {
        public GeneralSettingsViewModel ViewModel { get; }
        public event Action<List<UiAction>>? ActionsChanged;

        public GeneralSettingsView(SettingsService settingsService, SupabaseService supabaseService, AppSettings appSettings)
        {
            this.InitializeComponent();
            ViewModel = new GeneralSettingsViewModel(settingsService, supabaseService, appSettings);
            this.Loaded += OnLoaded;
            this.Unloaded += OnUnloaded;
        }

        private async void OnLoaded(object sender, RoutedEventArgs e)
        {
            // Najpierw wyślij pustą listę aby wyczyścić panel akcji
            ActionsChanged?.Invoke(new List<UiAction>());

            // Opóźnij rejestrację akcji aby uniknąć migania przycisku podczas inicjalizacji
            await Task.Delay(100);

            if (this.IsLoaded)
            {
                var actions = new List<UiAction>
                {
                    new UiAction("Zapisz zmiany", ViewModel.SaveCommand, isPrimary: true)
                };
                ActionsChanged?.Invoke(actions);
            }
        }

        private void OnUnloaded(object sender, RoutedEventArgs e)
        {
            // Przywróć oryginalny motyw jeśli były niezapisane zmiany
            ViewModel.RestoreOriginalThemeIfDirty();
        }
    }
}