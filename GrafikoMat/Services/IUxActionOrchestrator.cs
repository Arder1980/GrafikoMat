using System;
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
    }
}