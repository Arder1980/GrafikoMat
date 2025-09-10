using GrafikoMat.Core.Data;
using GrafikoMat.Core.Scheduling.Evaluation;
using GrafikoMat.Core.Scheduling.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

namespace GrafikoMat.Core.Scheduling.Engines
{
    /// <summary>
    /// Implementacja silnika generującego grafik za pomocą algorytmu symulowanego wyżarzania.
    /// Wersja zaadaptowana z GrafikWPF.
    /// </summary>
    public class SimulatedAnnealingSolver : IScheduleSolver
    {
        private const double InitialTemperature = 1000.0;
        private const double CoolingRate = 0.995;
        private readonly int _iterationsPerTemperature;

        private readonly ScheduleInput _scheduleInput;
        private readonly List<SolverPriority> _priorities;
        private readonly IProgress<double>? _progressReporter;
        private readonly CancellationToken _cancellationToken;
        private readonly Random _random = new();
        private readonly SolverUtility _utility;

        public SimulatedAnnealingSolver(ScheduleInput scheduleInput, List<SolverPriority> priorities, IProgress<double>? progress = null, CancellationToken cancellationToken = default)
        {
            _scheduleInput = scheduleInput;
            _priorities = priorities;
            _progressReporter = progress;
            _cancellationToken = cancellationToken;
            _utility = new SolverUtility(scheduleInput);
            _iterationsPerTemperature = Math.Max(100, _scheduleInput.Doctors.Count * 10);
        }

        public ScheduleSolution FindOptimalSolution()
        {
            // Zaczynamy od rozwiązania "chciwego", a nie w pełni losowego.
            var currentSolution = _utility.CreateGreedyInitialSolution();
            var bestSolution = new Dictionary<DateTime, DoctorProfile?>(currentSolution);

            var currentMetrics = EvaluationAndScoringService.CalculateMetrics(currentSolution, _utility.CalculateWorkload(currentSolution), _scheduleInput);
            double bestFitness = EvaluationAndScoringService.CalculateScore(currentMetrics, _priorities, _scheduleInput);
            double currentFitness = bestFitness;

            double temperature = InitialTemperature;
            int totalIterations = (int)Math.Log(0.1 / InitialTemperature, CoolingRate) * _iterationsPerTemperature;
            int currentIteration = 0;

            while (temperature > 0.1)
            {
                for (int i = 0; i < _iterationsPerTemperature; i++)
                {
                    _cancellationToken.ThrowIfCancellationRequested();

                    var newSolution = _utility.GenerateNeighbor(currentSolution);
                    Validation.ConstraintValidationService.RepairSchedule(newSolution, _scheduleInput);

                    var newMetrics = EvaluationAndScoringService.CalculateMetrics(newSolution, _utility.CalculateWorkload(newSolution), _scheduleInput);
                    double newFitness = EvaluationAndScoringService.CalculateScore(newMetrics, _priorities, _scheduleInput);

                    if (newFitness > currentFitness || _random.NextDouble() < Math.Exp((newFitness - currentFitness) / temperature))
                    {
                        currentSolution = newSolution;
                        currentFitness = newFitness;

                        if (currentFitness > bestFitness)
                        {
                            bestSolution = new Dictionary<DateTime, DoctorProfile?>(currentSolution);
                            bestFitness = currentFitness;
                        }
                    }
                    currentIteration++;
                }
                temperature *= CoolingRate;

                if (totalIterations > 0)
                {
                    _progressReporter?.Report((double)currentIteration / totalIterations);
                }
            }

            var finalWorkload = _utility.CalculateWorkload(bestSolution);
            return EvaluationAndScoringService.CalculateMetrics(bestSolution, finalWorkload, _scheduleInput);
        }
    }
}