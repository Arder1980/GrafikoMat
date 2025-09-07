using GrafikoMat.Common;
using GrafikoMat.Services;
using GrafikoMat.ViewModels;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Windows.ApplicationModel.DataTransfer;

namespace GrafikoMat.Views
{
    public sealed partial class ManagementView : UserControl
    {
        public ManagementViewModel ViewModel { get; }

        public ManagementView(DataService dataService, DispatcherQueue dispatcher)
        {
            this.InitializeComponent();
            ViewModel = new ManagementViewModel(dataService, dispatcher);
            this.DataContext = ViewModel;
        }

        // Metoda do kopiowania hasła - bez zmian, bo przycisk Odnów usunęliśmy z XAML
        private void CopyPassword_Click(object sender, RoutedEventArgs e)
        {
            if (ViewModel.EditorViewModel != null && !string.IsNullOrEmpty(ViewModel.EditorViewModel.Password))
            {
                var dataPackage = new DataPackage();
                dataPackage.SetText(ViewModel.EditorViewModel.Password);
                Clipboard.SetContent(dataPackage);
            }
        }
    }
}