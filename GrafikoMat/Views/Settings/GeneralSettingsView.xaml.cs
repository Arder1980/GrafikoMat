using GrafikoMat.Models;
using GrafikoMat.Services;
using GrafikoMat.ViewModels;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;
using System.Collections.Generic;

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
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            // Zarejestruj przycisk Zapisz w ActionButtonsPanel
            var actions = new List<UiAction>
            {
                new UiAction("Zapisz ustawienia", ViewModel.SaveCommand, isPrimary: true)
            };
            ActionsChanged?.Invoke(actions);
        }
    }
}