using GrafikoMat.ViewModels;
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

        // ZMIANA: Event handler został całkowicie usunięty.
        // Drag & drop jest teraz obsługiwany przez CollectionChanged event w ViewModel.
        // Nie potrzebujemy już ActivePrioritiesListView_DragItemsCompleted.
    }
}