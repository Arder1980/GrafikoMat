using GrafikoMat.Core.Data;
using GrafikoMat.Core.Scheduling.Evaluation;
using GrafikoMat.Core.Scheduling.Models;
using GrafikoMat.Core.Scheduling.Validation;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace GrafikoMat.Core.Scheduling.Engines
{
    /// <summary>
    /// Implementacja algorytmu kolonii mrówek (ACO) z timeoutem.
    /// 
    /// WERSJA: PRECISION+ (Adaptive Beta + optymalizacje dla jakości 97-99%)
    /// 
    /// Kluczowe ulepszenia:
    /// - Adaptive Beta: 5.0 → 2.0 (początkowo słucha heurystyki, potem feromonów)
    /// - Elite Strategy: top 5 rozwiązań wzmacnia feromony (rank-based weights)
    /// - Early Stopping: zatrzymuje się po 25 generacjach bez poprawy >0.1%
    /// - Naprawiona formuła feromonowa: sensowna akumulacja śladów
    /// - Zredukowane parowanie: 0.15 zamiast 0.5 (lepsza pamięć algorytmu)
    /// 
    /// Docelowa wydajność:
    /// - 75-80% redukcja czasu obliczeń vs wersja bazowa
    /// - Jakość rozwiązań: 97-99% optimum
    /// </summary>
    public class AntColonySolver : IScheduleSolver
    {
        private readonly int _numAnts;
        private readonly int _maxGenerations;
        private readonly TimeSpan _timeout;
        private Stopwatch? _stopwatch;

        private const double EvaporationRate = 0.15;
        private const double Alpha = 1.2;
        private const double Q0 = 0.75;
        private const int PATIENCE = 25;
        private const double IMPROVEMENT_THRESHOLD = 0.001;
        private const int ELITE_COUNT = 5;

        private readonly ScheduleInput _scheduleInput;
        private readonly List<SolverPriority> _priorities;
        private readonly IProgress<double>? _progressReporter;
        private readonly CancellationToken _cancellationToken;
        private readonly SolverUtility _utility;
        private readonly Random _random = new();

        private Dictionary<DateTime, Dictionary<string, double>> _pheromoneMatrix = new();
        private int _currentGeneration;
        private int _noImprovementCount;

        public AntColonySolver(
            ScheduleInput scheduleInput,
            List<SolverPriority> priorities,
            int numAnts,
            int maxGenerations,
            TimeSpan timeout,
            IProgress<double>? progress = null,
            CancellationToken cancellationToken = default)
        {
            _scheduleInput = scheduleInput;
            _priorities = priorities;
            _progressReporter = progress;
            _cancellationToken = cancellationToken;
            _utility = new SolverUtility(scheduleInput);
            _timeout = timeout;

            _numAnts = numAnts;
            _maxGenerations = maxGenerations;
            _currentGeneration = 0;
            _noImprovementCount = 0;
        }

        public ScheduleSolution FindOptimalSolution()
        {
            _stopwatch = Stopwatch.StartNew();
            InitializePheromones();

            var bestSolution = new Dictionary<DateTime, DoctorProfile?>();
            double bestFitness = double.MinValue;

            for (_currentGeneration = 0; _currentGeneration < _maxGenerations; _currentGeneration++)
            {
                _cancellationToken.ThrowIfCancellationRequested();

                if (_stopwatch != null && _stopwatch.Elapsed >= _timeout)
                {
                    break;
                }

                var solutions = new ConcurrentBag<Dictionary<DateTime, DoctorProfile?>>();

                Parallel.For(0, _numAnts, ant =>
                {
                    _cancellationToken.ThrowIfCancellationRequested();
                    var solution = BuildSolutionForAnt();
                    solutions.Add(solution);
                });

                var evaluatedSolutions = solutions.AsParallel()
                    .Select(s => new
                    {
                        Solution = s,
                        Metrics = EvaluationAndScoringService.CalculateMetrics(
                            s,
                            _utility.CalculateWorkload(s),
                            _scheduleInput),
                        Fitness = 0.0
                    })
                    .Select(x => new
                    {
                        x.Solution,
                        x.Metrics,
                        Fitness = EvaluationAndScoringService.CalculateScore(
                            x.Metrics,
                            _priorities,
                            _scheduleInput)
                    })
                    .OrderByDescending(x => x.Fitness)
                    .Take(ELITE_COUNT)
                    .ToList();

                var bestInGeneration = evaluatedSolutions.First();

                if (bestInGeneration.Fitness > bestFitness * (1.0 + IMPROVEMENT_THRESHOLD))
                {
                    bestFitness = bestInGeneration.Fitness;
                    bestSolution = bestInGeneration.Solution;
                    _noImprovementCount = 0;
                }
                else
                {
                    _noImprovementCount++;

                    if (_noImprovementCount >= PATIENCE)
                    {
                        _progressReporter?.Report(1.0);
                        break;
                    }
                }

                EvaporatePheromones();
                UpdatePheromonesWithEliteStrategy(evaluatedSolutions);

                _progressReporter?.Report((double)(_currentGeneration + 1) / _maxGenerations);
            }

            var finalWorkload = _utility.CalculateWorkload(bestSolution);
            var finalSolution = EvaluationAndScoringService.CalculateMetrics(bestSolution, finalWorkload, _scheduleInput);

            _stopwatch?.Stop();

            finalSolution = finalSolution with
            {
                ComputationTime = _stopwatch?.Elapsed ?? TimeSpan.Zero,
                OptimalityNote = _stopwatch?.Elapsed >= _timeout
                    ? "Best solution found (timeout reached)"
                    : _noImprovementCount >= PATIENCE
                        ? "Best solution found (early stopping)"
                        : "Solution found (colony converged)"
            };

            _progressReporter?.Report(1.0);
            return finalSolution;
        }

        private void InitializePheromones()
        {
            _pheromoneMatrix = new Dictionary<DateTime, Dictionary<string, double>>();

            foreach (var day in _scheduleInput.DaysInMonth)
            {
                _pheromoneMatrix[day] = new Dictionary<string, double>();

                foreach (var doctor in _scheduleInput.Doctors)
                {
                    _pheromoneMatrix[day][doctor.Abbreviation] = 1.0;
                }
            }
        }

        private Dictionary<DateTime, DoctorProfile?> BuildSolutionForAnt()
        {
            var newSolution = new Dictionary<DateTime, DoctorProfile?>();
            var workload = _scheduleInput.Doctors.ToDictionary(l => l.Abbreviation, l => 0);
            var usedConditionals = new HashSet<string>();

            double currentBeta = GetAdaptiveBeta();

            foreach (var day in _scheduleInput.DaysInMonth)
            {
                var candidates = ConstraintValidationService.GetValidCandidatesForDay(
                    day,
                    _scheduleInput,
                    newSolution,
                    workload,
                    usedConditionals);

                if (!candidates.Any())
                {
                    newSolution[day] = null;
                    continue;
                }

                var attractiveness = candidates.ToDictionary(
                    candidate => candidate,
                    candidate => Math.Pow(_pheromoneMatrix[day][candidate.Abbreviation], Alpha)
                               * Math.Pow(GetHeuristicValue(day, candidate), currentBeta)
                );

                DoctorProfile? chosenDoctor;

                if (_random.NextDouble() < Q0)
                {
                    chosenDoctor = attractiveness.OrderByDescending(kvp => kvp.Value).First().Key;
                }
                else
                {
                    double totalAttractiveness = attractiveness.Values.Sum();
                    double randomValue = _random.NextDouble() * totalAttractiveness;
                    chosenDoctor = null;

                    foreach (var choice in attractiveness)
                    {
                        randomValue -= choice.Value;
                        if (randomValue <= 0)
                        {
                            chosenDoctor = choice.Key;
                            break;
                        }
                    }

                    chosenDoctor ??= candidates.Last();
                }

                newSolution[day] = chosenDoctor;
                workload[chosenDoctor!.Abbreviation]++;

                if (_scheduleInput.Availability[day][chosenDoctor.Abbreviation] == AvailabilityType.ConditionallyAvailable)
                {
                    usedConditionals.Add(chosenDoctor.Abbreviation);
                }
            }

            return newSolution;
        }

        private double GetAdaptiveBeta()
        {
            double progress = (double)_currentGeneration / _maxGenerations;
            return 5.0 - 3.0 * progress;
        }

        private double GetHeuristicValue(DateTime day, DoctorProfile doctor)
        {
            return _scheduleInput.Availability[day][doctor.Abbreviation] switch
            {
                AvailabilityType.Wants => 20.0,
                AvailabilityType.Available => 1.0,
                AvailabilityType.ConditionallyAvailable => 0.5,
                _ => 0.1
            };
        }

        private void EvaporatePheromones()
        {
            foreach (var day in _pheromoneMatrix.Keys.ToList())
            {
                foreach (var doctorAbbr in _pheromoneMatrix[day].Keys.ToList())
                {
                    _pheromoneMatrix[day][doctorAbbr] *= (1.0 - EvaporationRate);
                }
            }
        }

        private void UpdatePheromonesWithEliteStrategy<T>(List<T> eliteSolutions) where T : class
        {
            int rank = 0;
            foreach (dynamic elite in eliteSolutions)
            {
                rank++;
                double weight = (double)(ELITE_COUNT - rank + 1) / (ELITE_COUNT * (ELITE_COUNT + 1) / 2.0);
                UpdatePheromonesWeighted(elite.Solution, elite.Fitness, weight);
            }
        }

        private void UpdatePheromonesWeighted(
            Dictionary<DateTime, DoctorProfile?> solution,
            double fitness,
            double weight)
        {
            if (fitness <= 0) return;

            double normalizedFitness = Math.Min(fitness / 1_000_000_000_000.0, 1.0);
            double baseDeposit = 0.1 + 0.9 * normalizedFitness;
            double pheromoneDeposit = baseDeposit * weight;

            foreach (var entry in solution)
            {
                if (entry.Value != null)
                {
                    _pheromoneMatrix[entry.Key][entry.Value.Abbreviation] += pheromoneDeposit;
                }
            }
        }
    }
}