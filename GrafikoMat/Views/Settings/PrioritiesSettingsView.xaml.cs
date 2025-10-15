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
            // Po tym, jak kontrolka ListView sama zaktualizowała swoją wewnętrzną kolejność,
            // pobieramy tę nową kolejność i przekazujemy ją do ViewModelu w celu synchronizacji.
            var newOrder = sender.Items.Cast<PriorityOptionViewModel>();
            ViewModel.UpdateOrderFromView(newOrder);
        }
    }
}