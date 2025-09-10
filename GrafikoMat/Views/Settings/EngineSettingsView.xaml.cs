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
        }

        private void EngineRadioButton_Checked(object sender, RoutedEventArgs e)
        {
            if (sender is RadioButton { DataContext: EngineOption selectedOption })
            {
                ViewModel.SelectedEngine = selectedOption;
            }
        }
    }
}