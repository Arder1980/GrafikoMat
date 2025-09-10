using CommunityToolkit.Mvvm.Messaging;
using GrafikoMat.Controls;
using Microsoft.UI.Xaml.Controls;
using System;
using System.Threading.Tasks;

namespace GrafikoMat.Services
{
    public class UxActionOrchestrator : IUxActionOrchestrator
    {
        private readonly IMessenger _messenger;

        public UxActionOrchestrator(IMessenger messenger)
        {
            _messenger = messenger;
        }

        public async Task PerformActionAsync(
            Guid viewId,
            Func<Task> actionAsync,
            Func<Task<bool>> verificationAsync,
            string successMessage,
            string errorMessageTitle)
        {
            _messenger.Send(new ShowBusyOverlayMessage(viewId));

            try
            {
                await actionAsync();

                if (!await verificationAsync())
                {
                    throw new Exception("Weryfikacja po zapisie zakończyła się niepowodzeniem.");
                }

                _messenger.Send(new ShowStatusOverlayMessage(viewId, "Sukces", successMessage, InfoBarSeverity.Success));
            }
            catch (Exception ex)
            {
                _messenger.Send(new ShowStatusOverlayMessage(viewId, errorMessageTitle, ex.Message, InfoBarSeverity.Error));
            }
            finally
            {
                await Task.Delay(3000);
                _messenger.Send(new HideOverlayMessage(viewId));
            }
        }

        public async Task PerformLoadAsync(Guid viewId, Func<Task> loadActionAsync)
        {
            _messenger.Send(new ShowBusyOverlayMessage(viewId));
            try
            {
                await loadActionAsync();
            }
            catch (Exception ex)
            {
                // Jeśli ładowanie się nie powiedzie, pokaż błąd i schowaj nakładkę
                _messenger.Send(new ShowStatusOverlayMessage(viewId, "Błąd ładowania danych", ex.Message, InfoBarSeverity.Error));
                await Task.Delay(3000);
            }
            finally
            {
                // Zawsze na końcu ukryj nakładkę
                _messenger.Send(new HideOverlayMessage(viewId));
            }
        }
    }
}