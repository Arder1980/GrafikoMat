using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml;
using System;
using System.Threading.Tasks;

namespace GrafikoMat.Services
{
    /// <summary>
    /// Implementacja serwisu dialogów dla WinUI 3.
    /// </summary>
    public class DialogService : IDialogService
    {
        private XamlRoot? _xamlRoot;

        public void SetXamlRoot(XamlRoot xamlRoot)
        {
            _xamlRoot = xamlRoot ?? throw new ArgumentNullException(nameof(xamlRoot));
        }

        public async Task<ContentDialogResult> ShowMessageAsync(string title, string content, string primaryButtonText = "OK")
        {
            return await ShowDialogAsync(
                title,
                content,
                primaryButtonText: primaryButtonText,
                secondaryButtonText: null,
                closeButtonText: null
            );
        }

        public async Task<ContentDialogResult> ShowConfirmationAsync(
            string title,
            string content,
            string primaryButtonText = "OK",
            string secondaryButtonText = "Anuluj")
        {
            return await ShowDialogAsync(
                title,
                content,
                primaryButtonText: primaryButtonText,
                secondaryButtonText: secondaryButtonText,
                closeButtonText: null,
                defaultButton: ContentDialogButton.Secondary
            );
        }

        public async Task<ContentDialogResult> ShowDialogAsync(
            string title,
            object content,
            string? primaryButtonText = null,
            string? secondaryButtonText = null,
            string? closeButtonText = null,
            ContentDialogButton defaultButton = ContentDialogButton.Primary)
        {
            if (_xamlRoot == null)
            {
                throw new InvalidOperationException(
                    "XamlRoot nie został ustawiony. Wywołaj SetXamlRoot() przed użyciem DialogService.");
            }

            var dialog = new ContentDialog
            {
                Title = title,
                Content = content,
                PrimaryButtonText = primaryButtonText,
                SecondaryButtonText = secondaryButtonText,
                CloseButtonText = closeButtonText,
                DefaultButton = defaultButton,
                XamlRoot = _xamlRoot
            };

            return await dialog.ShowAsync();
        }
    }
}
