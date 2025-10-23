using GrafikoMat.Services;
using GrafikoMat.ViewModels;
using Microsoft.UI.Xaml.Controls;

namespace GrafikoMat.Views.Settings
{
    public sealed partial class GeneralSettingsView : UserControl
    {
        public GeneralSettingsViewModel ViewModel { get; }

        public GeneralSettingsView(SettingsService settingsService, SupabaseService supabaseService, AppSettings appSettings)
        {
            this.InitializeComponent();
            ViewModel = new GeneralSettingsViewModel(settingsService, supabaseService, appSettings);
        }
    }
}