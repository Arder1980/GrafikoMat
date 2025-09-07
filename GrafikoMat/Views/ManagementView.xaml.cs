using GrafikoMat.Services;
using GrafikoMat.ViewModels;
using Microsoft.UI.Dispatching; // ZMIANA: Dodajemy ten using
using Microsoft.UI.Xaml.Controls;

namespace GrafikoMat.Views
{
    public sealed partial class ManagementView : UserControl
    {
        public ManagementViewModel ViewModel { get; }

        // ZMIANA: Konstruktor przyjmuje DispatcherQueue.
        public ManagementView(DataService dataService, DispatcherQueue dispatcher)
        {
            this.InitializeComponent();
            // ZMIANA: Przekazujemy dispatcher dalej do ViewModelu.
            ViewModel = new ManagementViewModel(dataService, dispatcher);
            this.DataContext = ViewModel;
        }
    }
}