using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml;
using System.Threading.Tasks;

namespace GrafikoMat.Services
{
    /// <summary>
    /// Interfejs serwisu do wyświetlania dialogów.
    /// Abstrakcja pozwalająca ViewModelom pokazywać dialogi bez bezpośredniej referencji do XamlRoot.
    /// </summary>
    public interface IDialogService
    {
        /// <summary>
        /// Ustawia XamlRoot używany do wyświetlania dialogów.
        /// Powinno być wywołane przez View podczas inicjalizacji.
        /// </summary>
        void SetXamlRoot(XamlRoot xamlRoot);

        /// <summary>
        /// Wyświetla prosty dialog informacyjny z jednym przyciskiem.
        /// </summary>
        Task<ContentDialogResult> ShowMessageAsync(string title, string content, string primaryButtonText = "OK");

        /// <summary>
        /// Wyświetla dialog z dwoma przyciskami (potwierdzenie/anulowanie).
        /// </summary>
        Task<ContentDialogResult> ShowConfirmationAsync(
            string title,
            string content,
            string primaryButtonText = "OK",
            string secondaryButtonText = "Anuluj"
        );

        /// <summary>
        /// Wyświetla w pełni konfigurowalny dialog.
        /// </summary>
        Task<ContentDialogResult> ShowDialogAsync(
            string title,
            object content,
            string? primaryButtonText = null,
            string? secondaryButtonText = null,
            string? closeButtonText = null,
            ContentDialogButton defaultButton = ContentDialogButton.Primary
        );
    }
}
