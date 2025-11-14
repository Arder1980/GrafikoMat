using GrafikoMat.Core.Scheduling.Models;
using GrafikoMat.Models;
using GrafikoMat.Services;
using GrafikoMat.ViewModels;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;
using System.Collections.Generic;

namespace GrafikoMat.Views.Settings
{
    public sealed partial class EngineSettingsView : UserControl
    {
        public EngineSettingsViewModel ViewModel { get; }
        public event Action<List<UiAction>>? ActionsChanged;

        public EngineSettingsView(SettingsService settingsService, AppSettings appSettings)
        {
            this.InitializeComponent();
            var orchestrator = ((App)Application.Current).Services.GetRequiredService<IUxActionOrchestrator>();
            ViewModel = new EngineSettingsViewModel(settingsService, appSettings, orchestrator);
            this.Loaded += OnLoaded;
        }

        private async void OnLoaded(object sender, RoutedEventArgs e)
        {
            // Ustaw stan początkowy bez animacji
            UpdateVisualStates(useTransitions: false);

            // Najpierw wyślij pustą listę aby wyczyścić panel akcji
            ActionsChanged?.Invoke(new List<UiAction>());

            // Opóźnij rejestrację akcji aby uniknąć migania przycisku podczas inicjalizacji
            await System.Threading.Tasks.Task.Delay(100);

            if (this.IsLoaded)
            {
                var actions = new List<UiAction>
                {
                    new UiAction("Zapisz zmiany", ViewModel.SaveCommand, isPrimary: true)
                };
                ActionsChanged?.Invoke(actions);
            }
        }

        private void EngineRadioButton_Checked(object sender, RoutedEventArgs e)
        {
            if (sender is RadioButton { DataContext: EngineOption selectedOption })
            {
                ViewModel.SelectedEngine = selectedOption;
                // Uruchom animowane przejście do nowego stanu
                UpdateVisualStates(useTransitions: true);
            }
        }

        private void UpdateVisualStates(bool useTransitions)
        {
            if (ViewModel.SelectedEngine == null)
            {
                VisualStateManager.GoToState(this, "DetailsCollapsed", useTransitions);
                VisualStateManager.GoToState(this, "SettingsCollapsed", useTransitions);
            }
            else
            {
                VisualStateManager.GoToState(this, "DetailsVisible", useTransitions);
                if (ViewModel.HasConfigurableParameters)
                {
                    VisualStateManager.GoToState(this, "SettingsVisible", useTransitions);
                }
                else
                {
                    VisualStateManager.GoToState(this, "SettingsCollapsed", useTransitions);
                }
            }
        }
    }
}