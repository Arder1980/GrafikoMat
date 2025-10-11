namespace GrafikoMat.Core.Scheduling.Models
{
    /// <summary>
    /// Przechowuje konfigurowalne parametry dla silników obliczeniowych.
    /// Pełni rolę kontraktu danych między warstwą UI a warstwą Core.
    /// </summary>
    public record SolverParameters
    {
        public SolverType SolverType { get; init; } = SolverType.Backtracking;

        // Simulated Annealing
        public double CoolingRate { get; init; } = 0.995;

        // Genetic Solver
        public int GeneticPopulationSize { get; init; } = 100;
        public int GeneticGenerations { get; init; } = 300;

        // Ant Colony
        public int AntColonyAnts { get; init; } = 75;
        public int AntColonyGenerations { get; init; } = 300;

        // Tabu Search
        public int TabuListSize { get; init; } = 30;
        public int TabuMaxIterations { get; init; } = 500;
    }
}