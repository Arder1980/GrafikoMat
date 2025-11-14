using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GrafikoMat.Services;
using System;
using System.Threading.Tasks;

namespace GrafikoMat.ViewModels
{
    public partial class LoginViewModel : ObservableObject
    {
        private readonly SupabaseService _supabaseService;

        [ObservableProperty]
        private string _email = "";

        [ObservableProperty]
        private string _password = "";

        [ObservableProperty]
        private string? _errorMessage;

        [ObservableProperty]
        private bool _isLoading;

        private int _failedAttempts = 0;
        private DateTime? _lockoutUntil = null;
        private const int MaxFailedAttempts = 3;
        private const int LockoutDurationSeconds = 30;

        public event Action? LoginSuccess;
        public event Action? CancelRequested;

        public LoginViewModel(SupabaseService supabaseService)
        {
            _supabaseService = supabaseService;
        }

        [RelayCommand]
        private async Task LoginAsync()
        {
            ErrorMessage = null;

            // Sprawdź czy konto jest zablokowane
            if (_lockoutUntil.HasValue)
            {
                if (DateTime.Now < _lockoutUntil.Value)
                {
                    var remainingSeconds = (int)(_lockoutUntil.Value - DateTime.Now).TotalSeconds;
                    ErrorMessage = $"Zbyt wiele nieudanych prób logowania. Spróbuj ponownie za {remainingSeconds} sekund.";
                    return;
                }
                else
                {
                    // Blokada wygasła - resetuj
                    _lockoutUntil = null;
                    _failedAttempts = 0;
                }
            }

            if (string.IsNullOrWhiteSpace(Email) || string.IsNullOrWhiteSpace(Password))
            {
                ErrorMessage = "Adres e-mail i hasło są wymagane.";
                return;
            }

            IsLoading = true;

            try
            {
                if (_supabaseService.Client == null)
                {
                    ErrorMessage = "Brak połączenia z bazą danych. Sprawdź ustawienia.";
                    return;
                }

                var session = await _supabaseService.Client.Auth.SignIn(Email, Password);
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
                    ErrorMessage = $"Zbyt wiele nieudanych prób logowania. Konto zablokowane na {LockoutDurationSeconds} sekund.";
                }
                else
                {
                    var remainingAttempts = MaxFailedAttempts - _failedAttempts;
                    ErrorMessage = $"Logowanie nie powiodło się. Pozostało prób: {remainingAttempts}";
                }
            }
            finally
            {
                IsLoading = false;
            }
        }

        [RelayCommand]
        private void Cancel()
        {
            CancelRequested?.Invoke();
        }

        [RelayCommand]
        private async Task ForgotPasswordAsync()
        {
            // Informacja o resetowaniu hasła - bez dostępu do UI
            // View obsłuży pokazanie dialogu przez event lub inny mechanizm
            await Task.CompletedTask;
        }
    }
}
