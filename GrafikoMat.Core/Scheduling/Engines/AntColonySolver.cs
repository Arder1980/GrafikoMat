using GrafikoMat.Core.Data;
using GrafikoMat.Core.Scheduling.Evaluation;
using GrafikoMat.Core.Scheduling.Models;
using GrafikoMat.Core.Scheduling.Validation;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace GrafikoMat.Core.Scheduling.Engines
{
    /// <summary>
    /// Implementacja silnika generującego grafik za pomocą algorytmu kolonii mrówek.
    /// Wersja zaadaptowana z GrafikWPF.
    /// </summary>
    public class AntColonySolver : IScheduleSolver
    {
        private readonly int _numAnts;
        private readonly int _maxGenerations;
        private const double EvaporationRate = 0.5; // Współczynnik odparowywania feromonu
        private const double Alpha = 1.0; // Wpływ feromonu
        private const double Beta = 5.0;  // Wpływ heurystyki
        private const double Q0 = 0.7;    // Prawdopodobieństwo wyboru najlepszej ścieżki

        private readonly ScheduleInput _scheduleInput;
        private readonly List<SolverPriority> _priorities;
        private readonly IProgress<double>? _progressReporter;
        private readonly CancellationToken _cancellationToken;
        private readonly SolverUtility _utility;
        private readonly Random _random = new();
        private Dictionary<DateTime, Dictionary<string, double>> _pheromoneMatrix = new();

        public AntColonySolver(ScheduleInput scheduleInput, List<SolverPriority> priorities, IProgress<double>? progress = null, CancellationToken cancellationToken = default)
        {
            _scheduleInput = scheduleInput;
            _priorities = priorities;
            _progressReporter = progress;
            _cancellationToken = cancellationToken;
            _utility = new SolverUtility(scheduleInput);

            _numAnts = Math.Max(50, _scheduleInput.Doctors.Count * 3);
            _maxGenerations = Math.Max(200, _scheduleInput.DaysInMonth.Count * 15);
        }

        public ScheduleSolution FindOptimalSolution()
        {
            InitializePheromones();
            var bestSolution = new Dictionary<DateTime, DoctorProfile?>();
            double bestFitness = double.MinValue;

            for (int i = 0; i < _maxGenerations; i++)
            {
                _cancellationToken.ThrowIfCancellationRequested();
                var solutions = new ConcurrentBag<Dictionary<DateTime, DoctorProfile?>>();

                Parallel.For(0, _numAnts, ant =>
                {
                    _cancellationToken.ThrowIfCancellationRequested();
                    var solution = BuildSolutionForAnt();
                    solutions.Add(solution);
                });

                var bestInGeneration = solutions.AsParallel().OrderByDescending(s =>
                {
                    var metrics = EvaluationAndScoringService.CalculateMetrics(s, _utility.CalculateWorkload(s), _scheduleInput);
                    return EvaluationAndScoringService.CalculateScore(metrics, _priorities, _scheduleInput);
                }).First();

                var bestMetricsInGeneration = EvaluationAndScoringService.CalculateMetrics(bestInGeneration, _utility.CalculateWorkload(bestInGeneration), _scheduleInput);
                var bestFitnessInGeneration = EvaluationAndScoringService.CalculateScore(bestMetricsInGeneration, _priorities, _scheduleInput);

                if (bestFitnessInGeneration > bestFitness)
                {
                    bestFitness = bestFitnessInGeneration;
                    bestSolution = bestInGeneration;
                }

                EvaporatePheromones();
                UpdatePheromones(bestSolution, bestFitness);

                _progressReporter?.Report((double)(i + 1) / _maxGenerations);
            }

            var finalWorkload = _utility.CalculateWorkload(bestSolution);
            return EvaluationAndScoringService.CalculateMetrics(bestSolution, finalWorkload, _scheduleInput);
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

            foreach (var day in _scheduleInput.DaysInMonth)
            {
                var candidates = ConstraintValidationService.GetValidCandidatesForDay(day, _scheduleInput, newSolution, workload, usedConditionals);
                if (!candidates.Any())
                {
                    newSolution[day] = null;
                    continue;
                }

                var attractiveness = candidates.ToDictionary(
                    candidate => candidate,
                    candidate => Math.Pow(_pheromoneMatrix[day][candidate.Abbreviation], Alpha) * Math.Pow(GetHeuristicValue(day, candidate), Beta)
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

        private void UpdatePheromones(Dictionary<DateTime, DoctorProfile?> solution, double fitness)
        {
            if (fitness <= 0) return;
            double pheromoneDeposit = Math.Pow(fitness / 1_000_000_000_000, 2);

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