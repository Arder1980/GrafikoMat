using GrafikoMat.Services;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;
using System.Threading.Tasks;

namespace GrafikoMat.Views
{
    public sealed partial class LoginView : UserControl
    {
        private readonly SupabaseService _supabaseService;

        public event Action? LoginSuccess;
        public event Action? CancelRequested;

        public LoginView(SupabaseService supabaseService)
        {
            this.InitializeComponent();
            _supabaseService = supabaseService;
        }

        private async void LoginButton_Click(object sender, RoutedEventArgs e)
        {
            ShowError(null); // Ukryj poprzedni błąd
            var email = EmailTextBox.Text;
            var password = PasswordBox.Password;

            if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
            {
                ShowError("Adres e-mail i hasło są wymagane.");
                return;
            }

            try
            {
                if (_supabaseService.Client == null)
                {
                    ShowError("Brak połączenia z bazą danych. Sprawdź ustawienia.");
                    return;
                }

                var session = await _supabaseService.Client.Auth.SignIn(email, password);
                if (session?.User != null)
                {
                    LoginSuccess?.Invoke();
                }
            }
            catch (Exception)
            {
                ShowError("Logowanie nie powiodło się. Sprawdź dane i spróbuj ponownie.");
            }
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            CancelRequested?.Invoke();
        }

        private void ShowError(string? message)
        {
            if (string.IsNullOrEmpty(message))
            {
                ErrorBar.IsOpen = false;
                ErrorBar.Opacity = 0;
            }
            else
            {
                ErrorBar.Message = message;
                ErrorBar.IsOpen = true;
                ErrorBar.Opacity = 1;
            }
        }
    }
}