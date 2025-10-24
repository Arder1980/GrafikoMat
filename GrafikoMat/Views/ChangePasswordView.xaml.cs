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
        #pragma warning disable CS0067
        public event Action? PasswordChangeCancelled;
        #pragma warning restore CS0067

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