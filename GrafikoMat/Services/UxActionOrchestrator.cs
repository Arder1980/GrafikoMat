using CommunityToolkit.Mvvm.Messaging;
using GrafikoMat.Controls;
using Microsoft.UI.Xaml.Controls;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace GrafikoMat.Services
{
    public class UxActionOrchestrator : IUxActionOrchestrator, IRecipient<HideOverlayExplicitlyMessage>, IRecipient<CancelOperationMessage>
    {
        private readonly IMessenger _messenger;
        private readonly Dictionary<Guid, CancellationTokenSource> _cancellationSources = new();

        public UxActionOrchestrator(IMessenger messenger)
        {
            _messenger = messenger;
            _messenger.Register<HideOverlayExplicitlyMessage>(this);
            _messenger.Register<CancelOperationMessage>(this);
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

        public void Receive(CancelOperationMessage message)
        {
            // Anuluj operację jeśli istnieje CancellationTokenSource dla tego widoku
            if (_cancellationSources.TryGetValue(message.ViewId, out var cts))
            {
                cts.Cancel();
            }
        }

        /// <summary>
        /// Wykonuje długotrwałą operację z obsługą progressu, dynamicznych komunikatów i możliwością anulowania.
        /// </summary>
        public async Task<bool> PerformLongRunningTaskAsync(
            Guid viewId,
            string title,
            string engineName,
            Func<IProgress<(double progress, string? statusText)>, CancellationToken, Task> operationAsync,
            string? successMessage = null,
            string errorMessageTitle = "Błąd",
            bool isCancellable = true,
            bool showIndeterminateProgress = false)
        {
            // Utwórz CancellationTokenSource dla tej operacji
            var cts = new CancellationTokenSource();
            _cancellationSources[viewId] = cts;

            try
            {
                // Pokaż nakładkę z progressem (ProgressBar lub ProgressRing w zależności od showIndeterminateProgress)
                _messenger.Send(new ShowProgressOverlayMessage(viewId, title, engineName, isCancellable, showIndeterminateProgress));

                // Małe opóźnienie aby overlay się wyświetlił
                await Task.Delay(100);

                // Utwórz Progress który będzie wysyłać aktualizacje przez Messenger
                var progress = new Progress<(double progress, string? statusText)>(update =>
                {
                    _messenger.Send(new UpdateProgressMessage(viewId, update.progress, update.statusText));
                });

                // Wykonaj operację w tle
                await operationAsync(progress, cts.Token);

                // Sprawdź czy została anulowana
                if (cts.Token.IsCancellationRequested)
                {
                    _messenger.Send(new ShowStatusOverlayMessage(viewId, "Anulowano", "Operacja została anulowana przez użytkownika.", InfoBarSeverity.Warning));
                    await Task.Delay(2000);
                    _messenger.Send(new HideOverlayMessage(viewId));
                    return false;
                }

                // Sukces
                if (!string.IsNullOrEmpty(successMessage))
                {
                    _messenger.Send(new ShowStatusOverlayMessage(viewId, "Sukces", successMessage, InfoBarSeverity.Success));
                    await Task.Delay(2000);
                }

                _messenger.Send(new HideOverlayMessage(viewId));
                return true;
            }
            catch (OperationCanceledException)
            {
                // Anulowanie przez CancellationToken
                _messenger.Send(new ShowStatusOverlayMessage(viewId, "Anulowano", "Operacja została anulowana.", InfoBarSeverity.Warning));
                await Task.Delay(2000);
                _messenger.Send(new HideOverlayMessage(viewId));
                return false;
            }
            catch (Exception ex)
            {
                // Błąd wykonania
                _messenger.Send(new ShowStatusOverlayMessage(viewId, errorMessageTitle, ex.Message, InfoBarSeverity.Error));
                // Nie ukrywamy automatycznie - użytkownik musi zamknąć ręcznie
                return false;
            }
            finally
            {
                // Usuń CancellationTokenSource
                _cancellationSources.Remove(viewId);
                cts.Dispose();
            }
        }
    }
}