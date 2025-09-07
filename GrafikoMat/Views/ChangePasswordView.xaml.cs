using GrafikoMat.Services;
using GrafikoMat.ViewModels;
using Microsoft.UI.Xaml.Controls;
using System;

namespace GrafikoMat.Views
{
    public sealed partial class ChangePasswordView : UserControl
    {
        public ChangePasswordViewModel ViewModel { get; }

        public event Action? PasswordChangeSuccess;
        public event Action? PasswordChangeCancelled;

        public ChangePasswordView(SupabaseService supabaseService)
        {
            this.InitializeComponent();
            ViewModel = new ChangePasswordViewModel(supabaseService);
            ViewModel.OnError += ShowError;
            ViewModel.OnSuccess += () => PasswordChangeSuccess?.Invoke();
        }

        private void ShowError(string message)
        {
            ErrorBar.Message = message;
            ErrorBar.IsOpen = !string.IsNullOrEmpty(message);
        }
    }
}