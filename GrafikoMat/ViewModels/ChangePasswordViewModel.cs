using GrafikoMat.Common;
using GrafikoMat.Services;
using System;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.Input; // ZMIANA: Dodajemy ten using

namespace GrafikoMat.ViewModels
{
    public class ChangePasswordViewModel : ObservableObject
    {
        private readonly SupabaseService _supabaseService;
        public event Action<string>? OnError;
        public event Action? OnSuccess;

        private string _newPassword = string.Empty;
        public string NewPassword
        {
            get => _newPassword;
            set
            {
                if (SetProperty(ref _newPassword, value))
                {
                    // ZMIANA: Odwołujemy się do nowej komendy
                    SavePasswordCommand.NotifyCanExecuteChanged();
                }
            }
        }

        private string _confirmPassword = string.Empty;
        public string ConfirmPassword
        {
            get => _confirmPassword;
            set
            {
                if (SetProperty(ref _confirmPassword, value))
                {
                    // ZMIANA: Odwołujemy się do nowej komendy
                    SavePasswordCommand.NotifyCanExecuteChanged();
                }
            }
        }

        // ZMIANA: Używamy AsyncRelayCommand, ponieważ operacja zapisu jest asynchroniczna
        public AsyncRelayCommand SavePasswordCommand { get; }

        public ChangePasswordViewModel(SupabaseService supabaseService)
        {
            _supabaseService = supabaseService;
            // ZMIANA: Inicjalizujemy AsyncRelayCommand
            SavePasswordCommand = new AsyncRelayCommand(
                SavePasswordAsync,
                CanSavePassword);
        }

        private bool CanSavePassword()
        {
            // Ta metoda jest teraz używana przez CanExecute komendy
            return !string.IsNullOrEmpty(NewPassword) && !string.IsNullOrEmpty(ConfirmPassword);
        }

        private async Task SavePasswordAsync()
        {
            OnError?.Invoke(string.Empty); // Clear previous errors

            if (NewPassword.Length < 8)
            {
                OnError?.Invoke("Hasło musi mieć co najmniej 8 znaków.");
                return;
            }

            if (NewPassword != ConfirmPassword)
            {
                OnError?.Invoke("Wprowadzone hasła nie są identyczne.");
                return;
            }

            try
            {
                await _supabaseService.UpdateUserPasswordAsync(NewPassword);
                OnSuccess?.Invoke();
            }
            catch (Exception ex)
            {
                OnError?.Invoke($"Wystąpił błąd: {ex.Message}");
            }
        }
    }
}