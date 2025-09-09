using GrafikoMat.Common;
using GrafikoMat.Services;
using System;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.Input;

namespace GrafikoMat.ViewModels
{
    public class ChangePasswordViewModel : ObservableObject
    {
        private readonly SupabaseService _supabaseService;
        public event Action<string>? OnError;
        public event Action? OnSuccess;

        private bool _isLoading;
        public bool IsLoading
        {
            get => _isLoading;
            set => SetProperty(ref _isLoading, value);
        }

        private string _newPassword = string.Empty;
        public string NewPassword
        {
            get => _newPassword;
            set
            {
                if (SetProperty(ref _newPassword, value))
                {
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
                    SavePasswordCommand.NotifyCanExecuteChanged();
                }
            }
        }

        public AsyncRelayCommand SavePasswordCommand { get; }

        public ChangePasswordViewModel(SupabaseService supabaseService)
        {
            _supabaseService = supabaseService;
            SavePasswordCommand = new AsyncRelayCommand(
                SavePasswordAsync,
                CanSavePassword);
        }

        private bool CanSavePassword()
        {
            return !string.IsNullOrEmpty(NewPassword) && !string.IsNullOrEmpty(ConfirmPassword);
        }

        private async Task SavePasswordAsync()
        {
            OnError?.Invoke(string.Empty);
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

            IsLoading = true;
            try
            {
                await _supabaseService.UpdateUserPasswordAsync(NewPassword);
                OnSuccess?.Invoke();
            }
            catch (Exception ex)
            {
                OnError?.Invoke($"Wystąpił błąd: {ex.Message}");
            }
            finally
            {
                IsLoading = false;
            }
        }
    }
}