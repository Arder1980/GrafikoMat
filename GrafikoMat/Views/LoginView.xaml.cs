using GrafikoMat.Services;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using System;
using System.Threading.Tasks;
using Windows.System;

namespace GrafikoMat.Views
{
    public sealed partial class LoginView : UserControl
    {
        private readonly SupabaseService _supabaseService;
        private int _failedAttempts = 0;
        private DateTime? _lockoutUntil = null;
        private const int MaxFailedAttempts = 3;
        private const int LockoutDurationSeconds = 30;

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

            // Sprawdź czy konto jest zablokowane
            if (_lockoutUntil.HasValue)
            {
                if (DateTime.Now < _lockoutUntil.Value)
                {
                    var remainingSeconds = (int)(_lockoutUntil.Value - DateTime.Now).TotalSeconds;
                    ShowError($"Zbyt wiele nieudanych prób logowania. Spróbuj ponownie za {remainingSeconds} sekund.");
                    return;
                }
                else
                {
                    // Blokada wygasła - resetuj
                    _lockoutUntil = null;
                    _failedAttempts = 0;
                }
            }

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
                    // Sukces - resetuj licznik nieudanych prób
                    _failedAttempts = 0;
                    _lockoutUntil = null;

                    // ZAPISZ sesję do LocalFolder\supabase_session.json
                    await _supabaseService.SaveCurrentSessionAsync();

                    // Dopiero potem sygnalizuj sukces do okna głównego
                    LoginSuccess?.Invoke();
                }

            }
            catch (Exception)
            {
                // Nieudana próba logowania
                _failedAttempts++;

                if (_failedAttempts >= MaxFailedAttempts)
                {
                    _lockoutUntil = DateTime.Now.AddSeconds(LockoutDurationSeconds);
                    ShowError($"Zbyt wiele nieudanych prób logowania. Konto zablokowane na {LockoutDurationSeconds} sekund.");
                }
                else
                {
                    var remainingAttempts = MaxFailedAttempts - _failedAttempts;
                    ShowError($"Logowanie nie powiodło się. Pozostało prób: {remainingAttempts}");
                }
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

        private void PasswordBox_KeyDown(object sender, KeyRoutedEventArgs e)
        {
            if (e.Key == VirtualKey.Enter)
            {
                e.Handled = true;
                LoginButton_Click(this, new RoutedEventArgs());
            }
        }

        // ZMIANA: Dodano nową metodę
        private async void ForgotPassword_Click(object sender, RoutedEventArgs e)
        {
            var dialog = App.CreateThemedDialog();
            dialog.Title = "Resetowanie hasła";
            dialog.Content = "Funkcja resetowania hasła jest dostępna w aplikacji webowej. Administrator może również zresetować Twoje hasło w panelu zarządzania.";
            dialog.PrimaryButtonText = "OK";
            await dialog.ShowAsync();
        }
    }
}