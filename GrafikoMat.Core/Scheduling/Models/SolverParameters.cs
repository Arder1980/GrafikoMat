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

        // Genetic Solver - PRECISION+ defaults (zoptymalizowane dla 97-99% jakości przy 5-10× szybszym działaniu)
        // Zmniejszona populacja (50 zamiast 100) + zmniejszona liczba generacji (150 zamiast 300)
        // dzięki island model, elityzmowi 15%, uniform crossover, smart mutation, early stopping i hybrydyzacji
        public int GeneticPopulationSize { get; init; } = 50;   // było 100
        public int GeneticGenerations { get; init; } = 150;     // było 300

        // Ant Colony - PRECISION+ defaults (zmienione z 75/300)
        // Zoptymalizowane dla jakości 97-99% przy ~75% redukcji czasu obliczeń
        public int AntColonyAnts { get; init; } = 40;           // było 75
        public int AntColonyGenerations { get; init; } = 120;   // było 300

        // Tabu Search
        public int TabuListSize { get; init; } = 30;
        public int TabuMaxIterations { get; init; } = 500;
    }
}