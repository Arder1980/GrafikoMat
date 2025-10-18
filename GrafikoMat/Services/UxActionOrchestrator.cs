using CommunityToolkit.Mvvm.Messaging;
using GrafikoMat.Controls;
using Microsoft.UI.Xaml.Controls;
using System;
using System.Threading.Tasks;

namespace GrafikoMat.Services
{
    public class UxActionOrchestrator : IUxActionOrchestrator, IRecipient<HideOverlayExplicitlyMessage>
    {
        private readonly IMessenger _messenger;

        public UxActionOrchestrator(IMessenger messenger)
        {
            _messenger = messenger;
            _messenger.Register<HideOverlayExplicitlyMessage>(this);
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

                // Sukces: wyświetlamy i czekamy
                _messenger.Send(new ShowStatusOverlayMessage(viewId, "Sukces", successMessage, InfoBarSeverity.Success));
                await Task.Delay(3000);
                _messenger.Send(new HideOverlayMessage(viewId));
            }
            catch (Exception ex)
            {
                // Błąd: wyświetlamy i nie czekamy – komunikat zostanie na ekranie!
                _messenger.Send(new ShowStatusOverlayMessage(viewId, errorMessageTitle, ex.Message, InfoBarSeverity.Error));

                // Będziemy czekać na ręczne zamknięcie wiadomości przez użytkownika,
                // co zostanie przekazane jako HideOverlayExplicitlyMessage
            }
        }

        public async Task PerformLoadAsync(Guid viewId, Func<Task> loadActionAsync)
        {
            _messenger.Send(new ShowBusyOverlayMessage(viewId));

            // Dodajemy małe opóźnienie, aby overlay mógł się poprawnie wyświetlić
            await Task.Delay(100);

            try
            {
                await loadActionAsync();

                // Dodajemy małe opóźnienie przed ukryciem, aby uniknąć "migania"
                await Task.Delay(200);
            }
            catch (Exception ex)
            {
                // Jeśli ładowanie się nie powiedzie, pokaż błąd i schowaj nakładkę po 3s.
                _messenger.Send(new ShowStatusOverlayMessage(viewId, "Błąd ładowania danych", ex.Message, InfoBarSeverity.Error));
                await Task.Delay(3000); // Automatyczne zamykanie dla błędów ładowania (nie blokujemy UI)
            }
            finally
            {
                // Zawsze na końcu ukryj nakładkę
                _messenger.Send(new HideOverlayMessage(viewId));
            }
        }

        public void Receive(HideOverlayExplicitlyMessage message)
        {
            // Ta wiadomość jest ignorowana przez orkiestratora,
            // ale musi być zaimplementowana, aby móc się zarejestrować
        }
    }
}