using GrafikoMat.Core.Scheduling.Models;
using System;
using System.Collections.Generic;
using System.Threading;

namespace GrafikoMat.Core.Scheduling.Engines
{
    /// <summary>
    /// Fabryka odpowiedzialna za tworzenie instancji odpowiednich silników generujących grafik.
    /// </summary>
    public static class ScheduleSolverFactory
    {
        public static IScheduleSolver Create(
            SolverType solverType,
            ScheduleInput input,
            List<SolverPriority> priorities,
            IProgress<double>? progress = null,
            CancellationToken token = default)
        {
            switch (solverType)
            {
                case SolverType.Backtracking:
                    return new BacktrackingSolver(input, priorities, progress, token);

                case SolverType.SimulatedAnnealing:
                    return new SimulatedAnnealingSolver(input, priorities, progress, token);

                case SolverType.Genetic:
                    return new GeneticSolver(input, priorities, progress, token);

                case SolverType.TabuSearch:
                    return new TabuSearchSolver(input, priorities, progress, token);

                case SolverType.AntColony:
                    return new AntColonySolver(input, priorities, progress, token);

                case SolverType.AStar:
                    return new AStarSolver(input, priorities, progress, token);

                default:
                    // Domyślnie, w razie braku implementacji, używamy Backtrackingu.
                    return new BacktrackingSolver(input, priorities, progress, token);
            }
        }
    }
}