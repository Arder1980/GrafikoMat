using GrafikoMat.Common;
using GrafikoMat.Services;
using System;
using System.Threading.Tasks;

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
                    SavePasswordCommand.RaiseCanExecuteChanged();
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
                    SavePasswordCommand.RaiseCanExecuteChanged();
                }
            }
        }

        public RelayCommand SavePasswordCommand { get; }

        public ChangePasswordViewModel(SupabaseService supabaseService)
        {
            _supabaseService = supabaseService;
            SavePasswordCommand = new RelayCommand(
                async _ => await SavePasswordAsync(),
                _ => CanSavePassword());
        }

        private bool CanSavePassword()
        {
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