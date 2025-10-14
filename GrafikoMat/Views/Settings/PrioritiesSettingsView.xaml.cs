using GrafikoMat.ViewModels;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System.Linq;

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

        private void ActivePrioritiesListView_DragItemsCompleted(ListViewBase sender, DragItemsCompletedEventArgs args)
        {
            // Po operacji przeciągnij-i-upuść, która automatycznie zmienia kolejność w kolekcji,
            // po prostu nakazujemy ViewModelowi odświeżenie stanu (rang i przycisku zapisu).
            ViewModel.RefreshListState(markAsDirty: true);
        }
    }
}