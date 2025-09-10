using GrafikoMat.Core.Scheduling.Models;

namespace GrafikoMat.Core.Scheduling.Engines
{
    /// <summary>
    /// Definiuje wspólny kontrakt dla wszystkich silników obliczeniowych generujących grafik.
    /// </summary>
    public interface IScheduleSolver
    {
        /// <summary>
        /// Uruchamia proces obliczeniowy w celu znalezienia najlepszego możliwego grafiku.
        /// </summary>
        /// <returns>Obiekt ScheduleSolution zawierający najlepsze znalezione rozwiązanie.</returns>
        ScheduleSolution FindOptimalSolution();
    }
}