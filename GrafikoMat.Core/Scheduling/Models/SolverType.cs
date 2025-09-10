namespace GrafikoMat.Core.Scheduling.Models
{
    /// <summary>
    /// Definiuje dostępne typy silników (algorytmów) do generowania grafiku.
    /// </summary>
    public enum SolverType
    {
        Backtracking,
        AStar,
        Genetic,
        SimulatedAnnealing,
        TabuSearch,
        AntColony
    }
}