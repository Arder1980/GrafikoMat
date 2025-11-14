using GrafikoMat.Services;
using GrafikoMat.ViewModels;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using System;
using Windows.System;

namespace GrafikoMat.Views
{
    public sealed partial class LoginView : UserControl
    {
        public LoginViewModel ViewModel { get; }

        public event Action? LoginSuccess
        {
            add => ViewModel.LoginSuccess += value;
            remove => ViewModel.LoginSuccess -= value;
        }

        public event Action? CancelRequested
        {
            add => ViewModel.CancelRequested += value;
            remove => ViewModel.CancelRequested -= value;
        }

        public LoginView(SupabaseService supabaseService)
        {
            ViewModel = new LoginViewModel(supabaseService);
            this.InitializeComponent();
        }

        private void PasswordBox_KeyDown(object sender, KeyRoutedEventArgs e)
        {
            if (e.Key == VirtualKey.Enter)
            {
                e.Handled = true;
                if (ViewModel.LoginCommand.CanExecute(null))
                {
                    ViewModel.LoginCommand.Execute(null);
                }
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