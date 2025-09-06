using GrafikoMat.Services; // Dodaj using
using GrafikoMat.ViewModels;
using Microsoft.UI.Xaml.Controls;

namespace GrafikoMat.Views
{
    public sealed partial class ManagementView : UserControl
    {
        public ManagementViewModel ViewModel { get; }

        // Zmieniamy konstruktor
        public ManagementView(DataService dataService)
        {
            this.InitializeComponent();
            ViewModel = new ManagementViewModel(dataService);
            this.DataContext = ViewModel;
        }
    }
}