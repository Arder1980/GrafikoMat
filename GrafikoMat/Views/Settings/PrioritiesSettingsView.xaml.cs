using GrafikoMat.ViewModels;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace GrafikoMat.Views.Settings
{
    public sealed partial class PrioritiesSettingsView : UserControl
    {
        public PrioritiesSettingsViewModel ViewModel { get; }

        public PrioritiesSettingsView(PrioritiesSettingsViewModel viewModel)
        {
            this.InitializeComponent();
            ViewModel = viewModel;
        }

        // ZMIANA: Dodano brakującą metodę obsługi zdarzenia DropCompleted.
        // To naprawia problem z brakiem aktualizacji wskaźników rangi po przeciągnięciu.
        private void ActivePrioritiesListView_DropCompleted(UIElement sender, DropCompletedEventArgs args)
        {
            // Po operacji przeciągnij-i-upuść, nakazujemy ViewModelowi odświeżenie
            // numerów porządkowych i stanu przycisków.
            ViewModel.RefreshListState();
        }
    }
}