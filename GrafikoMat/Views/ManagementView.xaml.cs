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
            // ZMIANA: Usunęliśmy DataContext = ViewModel, ponieważ używamy x:Bind
        }

        private void CopyPassword_Click(object sender, RoutedEventArgs e)
        {
            if (ViewModel.EditorViewModel != null && !string.IsNullOrEmpty(ViewModel.EditorViewModel.Password))
            {
                var dataPackage = new DataPackage();
                dataPackage.SetText(ViewModel.EditorViewModel.Password);
                Clipboard.SetContent(dataPackage);
            }
        }

        // ZMIANA: Dodajemy nową metodę obsługi zdarzenia LostFocus
        private void NameTextBox_LostFocus(object sender, RoutedEventArgs e)
        {
            if (ViewModel.EditorViewModel != null)
            {
                // Wywołujemy publiczną metodę SanitizeName z ViewModelu,
                // aby sformatować tekst w polach po opuszczeniu ich przez użytkownika.
                ViewModel.EditorViewModel.FirstName = ViewModel.EditorViewModel.SanitizeName(ViewModel.EditorViewModel.FirstName);
                ViewModel.EditorViewModel.LastName = ViewModel.EditorViewModel.SanitizeName(ViewModel.EditorViewModel.LastName);
            }
        }
    }
}