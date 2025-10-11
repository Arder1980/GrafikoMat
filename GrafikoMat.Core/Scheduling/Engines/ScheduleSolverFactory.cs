using GrafikoMat.Core.Scheduling.Models;
using System;
using System.Collections.Generic;
using System.Threading;

namespace GrafikoMat.Core.Scheduling.Engines
{
    public static class ScheduleSolverFactory
    {
        public static IScheduleSolver Create(
            ScheduleInput input,
            SolverParameters parameters,
            List<SolverPriority> activePriorities,
            IProgress<double>? progress = null,
            CancellationToken token = default)
        {
            switch (parameters.SolverType)
            {
                case SolverType.Backtracking:
                    return new BacktrackingSolver(input, activePriorities, progress, token);
                case SolverType.SimulatedAnnealing:
                    return new SimulatedAnnealingSolver(input, activePriorities, parameters.CoolingRate, progress, token);
                case SolverType.Genetic:
                    return new GeneticSolver(input, activePriorities, parameters.GeneticPopulationSize, parameters.GeneticGenerations, progress, token);
                case SolverType.TabuSearch:
                    return new TabuSearchSolver(input, activePriorities, parameters.TabuListSize, parameters.TabuMaxIterations, progress, token);
                case SolverType.AntColony:
                    return new AntColonySolver(input, activePriorities, parameters.AntColonyAnts, parameters.AntColonyGenerations, progress, token);
                case SolverType.AStar:
                    return new AStarSolver(input, activePriorities, progress, token);
                default:
                    return new BacktrackingSolver(input, activePriorities, progress, token);
            }
        }
    }
}