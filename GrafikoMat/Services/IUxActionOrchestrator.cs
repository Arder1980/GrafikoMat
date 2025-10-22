using System;
using System.Threading;
using System.Threading.Tasks;

namespace GrafikoMat.Services
{
    public interface IUxActionOrchestrator
    {
        /// <summary>
        /// Wykonuje operację zapisu, zarządzając stanami UI (ładowanie, komunikat o statusie).
        /// </summary>
        Task PerformActionAsync(
            Guid viewId,
            Func<Task> actionAsync,
            Func<Task<bool>> verificationAsync,
            string successMessage,
            string errorMessageTitle
        );

        /// <summary>
        /// Wykonuje operację ładowania danych, zarządzając stanem UI (tylko wskaźnik ładowania).
        /// </summary>
        /// <param name="viewId">ID widoku do przygaszenia.</param>
        /// <param name="loadActionAsync">Asynchroniczna operacja do wykonania w tle.</param>
        Task PerformLoadAsync(
            Guid viewId,
            Func<Task> loadActionAsync
        );

        /// <summary>
        /// Wykonuje długotrwałą operację z obsługą progressu, dynamicznych komunikatów i możliwością anulowania.
        /// Idealne dla obliczeń typu generowanie grafiku, które trwają kilka sekund/minut.
        /// </summary>
        /// <param name="viewId">ID widoku do przygaszenia.</param>
        /// <param name="title">Tytuł nakładki (np. "Generowanie grafiku").</param>
        /// <param name="engineName">Nazwa silnika do wyświetlenia (np. "GeneticSolver (algorytm genetyczny)").</param>
        /// <param name="operationAsync">Operacja do wykonania. Otrzymuje IProgress i CancellationToken.</param>
        /// <param name="successMessage">Komunikat sukcesu (opcjonalny).</param>
        /// <param name="errorMessageTitle">Tytuł komunikatu błędu.</param>
        /// <param name="isCancellable">Czy pokazać przycisk Cancel.</param>
        /// <param name="showIndeterminateProgress">Jeśli true, pokazuje ProgressRing (nieskończony spinner dla deterministycznych solwerów). Jeśli false, pokazuje ProgressBar (0-100% dla metaheurystyk).</param>
        /// <returns>True jeśli operacja zakończyła się sukcesem, False jeśli została anulowana lub wystąpił błąd.</returns>
        Task<bool> PerformLongRunningTaskAsync(
            Guid viewId,
            string title,
            string engineName,
            Func<IProgress<(double progress, string? statusText)>, CancellationToken, Task> operationAsync,
            string? successMessage = null,
            string errorMessageTitle = "Błąd",
            bool isCancellable = true,
            bool showIndeterminateProgress = false
        );
    }
}