using GrafikoMat.Core.Scheduling.Models;
using GrafikoMat.Services;
using GrafikoMat.ViewModels;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace GrafikoMat.Views.Settings
{
    public sealed partial class EngineSettingsView : UserControl
    {
        public EngineSettingsViewModel ViewModel { get; }

        public EngineSettingsView(SettingsService settingsService, AppSettings appSettings)
        {
            this.InitializeComponent();
            ViewModel = new EngineSettingsViewModel(settingsService, appSettings);
            this.Loaded += OnLoaded;
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            // Ustaw stan początkowy bez animacji
            UpdateVisualStates(useTransitions: false);
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