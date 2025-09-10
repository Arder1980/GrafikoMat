using GrafikoMat.Core.Repositories; // NOWY USING
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

        // ZMIANA: Nowy, rozbudowany konstruktor
        public ManagementView(
            IDoctorRepository doctorRepo,
            IUnitRepository unitRepo,
            IAssignmentRepository assignmentRepo,
            SupabaseService supabaseService,
            DispatcherQueue dispatcher)
        {
            this.InitializeComponent();
            ViewModel = new ManagementViewModel(doctorRepo, unitRepo, assignmentRepo, supabaseService, dispatcher);
            this.Loaded += ManagementView_Loaded;
        }

        private async void ManagementView_Loaded(object sender, RoutedEventArgs e)
        {
            this.Loaded -= ManagementView_Loaded;
            ViewModel.IsLoading = true;
            try
            {
                await ViewModel.InitializeAsync();
            }
            finally
            {
                ViewModel.IsLoading = false;
            }
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

        private void NameTextBox_LostFocus(object sender, RoutedEventArgs e)
        {
            if (ViewModel.EditorViewModel != null)
            {
                ViewModel.EditorViewModel.FirstName = ViewModel.EditorViewModel.SanitizeName(ViewModel.EditorViewModel.FirstName);
                ViewModel.EditorViewModel.LastName = ViewModel.EditorViewModel.SanitizeName(ViewModel.EditorViewModel.LastName);
            }
        }
    }
}