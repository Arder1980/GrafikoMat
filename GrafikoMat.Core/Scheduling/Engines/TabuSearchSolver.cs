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
    /// Implementacja silnika generującego grafik za pomocą algorytmu przeszukiwania z zakazami (Tabu Search).
    /// Wersja zaadaptowana z GrafikWPF.
    /// </summary>
    public class TabuSearchSolver : IScheduleSolver
    {
        private readonly int _tabuListSize;
        private readonly int _maxIterations;
        private readonly ScheduleInput _scheduleInput;
        private readonly List<SolverPriority> _priorities;
        private readonly IProgress<double>? _progressReporter;
        private readonly CancellationToken _cancellationToken;
        private readonly SolverUtility _utility;

        public TabuSearchSolver(ScheduleInput scheduleInput, List<SolverPriority> priorities, IProgress<double>? progress = null, CancellationToken cancellationToken = default)
        {
            _scheduleInput = scheduleInput;
            _priorities = priorities;
            _progressReporter = progress;
            _cancellationToken = cancellationToken;
            _utility = new SolverUtility(scheduleInput);

            int problemSize = _scheduleInput.Doctors.Count * _scheduleInput.DaysInMonth.Count;
            _tabuListSize = Math.Max(20, problemSize / 10);
            _maxIterations = Math.Max(300, problemSize * 2);
        }

        public ScheduleSolution FindOptimalSolution()
        {
            // Start od rozwiązania „chciwego”
            var currentSolution = _utility.CreateGreedyInitialSolution();
            var bestSolution = new Dictionary<DateTime, DoctorProfile?>(currentSolution);

            var metrics = EvaluationAndScoringService.CalculateMetrics(bestSolution, _utility.CalculateWorkload(bestSolution), _scheduleInput);
            double bestFitness = EvaluationAndScoringService.CalculateScore(metrics, _priorities, _scheduleInput);

            var tabuList = new Queue<Dictionary<DateTime, DoctorProfile?>>();
            int iterationsWithoutImprovement = 0;
            int diversificationThreshold = _maxIterations / 4;

            for (int i = 0; i < _maxIterations; i++)
            {
                _cancellationToken.ThrowIfCancellationRequested();

                var neighbors = GenerateNeighbors(currentSolution);
                Dictionary<DateTime, DoctorProfile?>? bestNeighbor = null;
                double bestNeighborFitness = double.MinValue;

                foreach (var neighbor in neighbors)
                {
                    if (!IsInTabuList(neighbor, tabuList))
                    {
                        var neighborMetrics = EvaluationAndScoringService.CalculateMetrics(neighbor, _utility.CalculateWorkload(neighbor), _scheduleInput);
                        double neighborFitness = EvaluationAndScoringService.CalculateScore(neighborMetrics, _priorities, _scheduleInput);

                        if (neighborFitness > bestNeighborFitness)
                        {
                            bestNeighborFitness = neighborFitness;
                            bestNeighbor = neighbor;
                        }
                    }
                }

                if (bestNeighbor != null)
                {
                    currentSolution = bestNeighbor;
                    if (tabuList.Count >= _tabuListSize)
                    {
                        tabuList.Dequeue();
                    }
                    tabuList.Enqueue(currentSolution);

                    if (bestNeighborFitness > bestFitness)
                    {
                        bestSolution = new Dictionary<DateTime, DoctorProfile?>(currentSolution);
                        bestFitness = bestNeighborFitness;
                        iterationsWithoutImprovement = 0;
                    }
                    else
                    {
                        iterationsWithoutImprovement++;
                    }
                }

                // Jeśli algorytm utknął w lokalnym optimum, dokonaj dywersyfikacji
                if (iterationsWithoutImprovement > diversificationThreshold)
                {
                    currentSolution = Diversify(currentSolution);
                    iterationsWithoutImprovement = 0;
                }

                _progressReporter?.Report((double)(i + 1) / _maxIterations);
            }

            var finalWorkload = _utility.CalculateWorkload(bestSolution);
            return EvaluationAndScoringService.CalculateMetrics(bestSolution, finalWorkload, _scheduleInput);
        }

        private List<Dictionary<DateTime, DoctorProfile?>> GenerateNeighbors(Dictionary<DateTime, DoctorProfile?> current)
        {
            var neighbors = new List<Dictionary<DateTime, DoctorProfile?>>();
            int neighborsToGenerate = Math.Max(10, _scheduleInput.Doctors.Count / 2);
            for (int i = 0; i < neighborsToGenerate; i++)
            {
                neighbors.Add(_utility.GenerateNeighbor(current));
            }
            return neighbors;
        }

        private bool IsInTabuList(Dictionary<DateTime, DoctorProfile?> solution, Queue<Dictionary<DateTime, DoctorProfile?>> tabuList)
        {
            foreach (var tabuSolution in tabuList)
            {
                if (solution.Count == tabuSolution.Count && !solution.Except(tabuSolution).Any())
                {
                    return true;
                }
            }
            return false;
        }

        private Dictionary<DateTime, DoctorProfile?> Diversify(Dictionary<DateTime, DoctorProfile?> current)
        {
            var diversified = new Dictionary<DateTime, DoctorProfile?>(current);
            int numberOfSwaps = _scheduleInput.DaysInMonth.Count / 5;
            for (int i = 0; i < numberOfSwaps; i++)
            {
                diversified = _utility.GenerateNeighbor(diversified);
            }
            return diversified;
        }
    }
}