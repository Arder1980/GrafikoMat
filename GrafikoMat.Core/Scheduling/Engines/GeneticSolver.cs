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
    /// PRECISION+ Edition: Zaawansowany algorytm genetyczny z trzema falami optymalizacji.
    /// 
    /// FALA 1 - Fundamenty wydajności:
    /// - Zwiększony elityzm (15% najlepszych)
    /// - Efektywna mutacja (bez marnowania GenerateNeighbor)
    /// - Większy turniej (rozmiar 8)
    /// - Early stopping (brak poprawy → przerwij)
    /// - Inteligentne naprawianie (tylko gdy potrzeba)
    /// 
    /// FALA 2 - Inteligentne operatory:
    /// - Uniform crossover (lepsza eksploracja)
    /// - Smart mutation (preferencja dni z niską dostępnością)
    /// - Adaptacyjny mutation rate (15% → 3%)
    /// - Walidacja przed naprawą
    /// 
    /// FALA 3 - Zaawansowane techniki:
    /// - Island model (3 subpopulacje + migracja)
    /// - Diversity maintenance (odrzucanie duplikatów)
    /// - Hybrydyzacja (lokalny hill-climbing dla top 20%)
    /// - Monitoring stagnacji populacji
    /// 
    /// Cel: 97-99% jakości optymalnej przy 5-10× szybszym działaniu.
    /// </summary>
    public class GeneticSolver : IScheduleSolver
    {
        private class Chromosome
        {
            public Dictionary<DateTime, DoctorProfile?> Genes { get; set; }
            public double Fitness { get; set; }
            public int Hash { get; private set; }

            public Chromosome(Dictionary<DateTime, DoctorProfile?> genes)
            {
                Genes = genes;
                Fitness = 0.0;
                Hash = ComputeHash(genes);
            }

            public Chromosome Clone()
            {
                var cloned = new Chromosome(new Dictionary<DateTime, DoctorProfile?>(Genes))
                {
                    Fitness = this.Fitness,
                    Hash = this.Hash
                };
                return cloned;
            }

            public void UpdateHash()
            {
                Hash = ComputeHash(Genes);
            }

            private static int ComputeHash(Dictionary<DateTime, DoctorProfile?> genes)
            {
                unchecked
                {
                    int hash = 17;
                    foreach (var kvp in genes.OrderBy(x => x.Key))
                    {
                        hash = hash * 31 + kvp.Key.GetHashCode();
                        hash = hash * 31 + (kvp.Value?.Abbreviation?.GetHashCode() ?? 0);
                    }
                    return hash;
                }
            }
        }

        private class Island
        {
            public List<Chromosome> Population { get; set; } = new();
            public double BestFitness { get; set; } = double.MinValue;
            public Chromosome? BestChromosome { get; set; }
        }

        private readonly int _populationSize;
        private readonly int _generations;
        private readonly TimeSpan _timeout;
        private Stopwatch? _stopwatch;
        private const double InitialCrossoverRate = 0.90;
        private const int TournamentSize = 8;
        private const int PatienceGenerations = 30;
        private int _noImprovementCounter = 0;
        private double _lastBestFitness = double.MinValue;
        private const int IslandCount = 3;
        private const int MigrationInterval = 20;
        private const int MigrantsCount = 2;
        private const int HillClimbingInterval = 15;
        private const double HillClimbingTopPercentage = 0.20;

        private readonly ScheduleInput _scheduleInput;
        private readonly List<SolverPriority> _priorities;
        private readonly IProgress<double>? _progressReporter;
        private readonly CancellationToken _cancellationToken;
        private readonly SolverUtility _utility;
        private readonly Random _random = new();
        private List<Island> _islands = new();
        private HashSet<int> _seenHashes = new();

        public GeneticSolver(
            ScheduleInput scheduleInput,
            List<SolverPriority> priorities,
            int populationSize,
            int generations,
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

            _populationSize = populationSize;
            _generations = generations;
        }

        private double GetAdaptiveMutationRate(int generation)
        {
            double progress = (double)generation / _generations;
            return 0.15 - (0.12 * progress);
        }

        public ScheduleSolution FindOptimalSolution()
        {
            _stopwatch = Stopwatch.StartNew();
            InitializeIslands();

            for (int generation = 0; generation < _generations; generation++)
            {
                _cancellationToken.ThrowIfCancellationRequested();

                if (_stopwatch != null && _stopwatch.Elapsed >= _timeout)
                {
                    break;
                }

                Parallel.For(0, IslandCount, islandIdx =>
                {
                    EvolveIsland(_islands[islandIdx], generation);
                });

                CalculateFitnessForAllIslands();

                var globalBest = _islands
                    .Where(i => i.BestChromosome != null)
                    .OrderByDescending(i => i.BestFitness)
                    .First();

                if (globalBest.BestFitness > _lastBestFitness * 1.001)
                {
                    _lastBestFitness = globalBest.BestFitness;
                    _noImprovementCounter = 0;
                }
                else
                {
                    _noImprovementCounter++;
                    if (_noImprovementCounter >= PatienceGenerations)
                    {
                        _progressReporter?.Report(1.0);
                        break;
                    }
                }

                if ((generation + 1) % MigrationInterval == 0 && generation > 0)
                {
                    PerformMigration();
                }

                if ((generation + 1) % HillClimbingInterval == 0 && generation > 0)
                {
                    PerformHillClimbing();
                }

                _progressReporter?.Report((double)(generation + 1) / _generations);
            }

            var finalBest = _islands
                .Where(i => i.BestChromosome != null)
                .OrderByDescending(i => i.BestFitness)
                .First()
                .BestChromosome!;

            var finalWorkload = _utility.CalculateWorkload(finalBest.Genes);
            var finalSolution = EvaluationAndScoringService.CalculateMetrics(finalBest.Genes, finalWorkload, _scheduleInput);

            _stopwatch?.Stop();

            finalSolution = finalSolution with
            {
                ComputationTime = _stopwatch?.Elapsed ?? TimeSpan.Zero,
                OptimalityNote = _stopwatch?.Elapsed >= _timeout
                    ? "Best solution found (timeout reached)"
                    : _noImprovementCounter >= PatienceGenerations
                        ? "Best solution found (early stopping)"
                        : "Solution found (evolution completed)"
            };

            return finalSolution;
        }

        private void InitializeIslands()
        {
            _islands = new List<Island>();
            int populationPerIsland = _populationSize / IslandCount;
            const int MaxAttemptsPerChromosome = 100; // Maksymalna liczba prób dla jednego chromosomu

            for (int i = 0; i < IslandCount; i++)
            {
                var island = new Island();

                for (int j = 0; j < populationPerIsland; j++)
                {
                    Chromosome? chromosome = null;
                    int attempts = 0;

                    // Próbuj znaleźć unikalny chromosom, ale z limitem prób
                    while (attempts < MaxAttemptsPerChromosome)
                    {
                        chromosome = new Chromosome(_utility.CreateRandomSolution());

                        if (!_seenHashes.Contains(chromosome.Hash))
                        {
                            _seenHashes.Add(chromosome.Hash);
                            break; // Znaleziono unikalny
                        }

                        attempts++;
                    }

                    // Jeśli znaleziono chromosom (unikalny lub po wyczerpaniu prób)
                    if (chromosome != null)
                    {
                        island.Population.Add(chromosome);

                        // Jeśli wyczerpano próby, dodaj mimo duplikatu (rzadki przypadek małej przestrzeni)
                        if (attempts >= MaxAttemptsPerChromosome && !_seenHashes.Contains(chromosome.Hash))
                        {
                            _seenHashes.Add(chromosome.Hash);
                        }
                    }
                }

                _islands.Add(island);
            }
        }

        private void EvolveIsland(Island island, int generation)
        {
            var newPopulation = new ConcurrentBag<Chromosome>();
            int eliteCount = Math.Max(1, (int)(island.Population.Count * 0.15));

            var elites = island.Population
                .OrderByDescending(c => c.Fitness)
                .Take(eliteCount)
                .ToList();

            foreach (var elite in elites)
            {
                newPopulation.Add(elite.Clone());
            }

            int remaining = island.Population.Count - eliteCount;

            Parallel.For(0, remaining, _ =>
            {
                var parent1 = Selection(island.Population);
                var parent2 = Selection(island.Population);
                var child = Crossover(parent1, parent2);
                Mutation(child, generation);

                lock (_seenHashes)
                {
                    if (!_seenHashes.Contains(child.Hash))
                    {
                        newPopulation.Add(child);
                        _seenHashes.Add(child.Hash);
                    }
                    else
                    {
                        var mutant = elites[_random.Next(elites.Count)].Clone();
                        Mutation(mutant, generation);
                        mutant.UpdateHash();
                        newPopulation.Add(mutant);
                    }
                }
            });

            island.Population = newPopulation.ToList();
        }

        private void CalculateFitnessForAllIslands()
        {
            foreach (var island in _islands)
            {
                Parallel.ForEach(island.Population, chromosome =>
                {
                    var workload = _utility.CalculateWorkload(chromosome.Genes);
                    var metrics = EvaluationAndScoringService.CalculateMetrics(
                        chromosome.Genes, workload, _scheduleInput);
                    chromosome.Fitness = EvaluationAndScoringService.CalculateScore(
                        metrics, _priorities, _scheduleInput);
                });

                var best = island.Population.OrderByDescending(c => c.Fitness).First();
                island.BestFitness = best.Fitness;
                island.BestChromosome = best.Clone();
            }
        }
        // KONTYNUACJA GeneticSolver.cs - operatory genetyczne i metody pomocnicze

        private Chromosome Selection(List<Chromosome> population)
        {
            var tournament = new List<Chromosome>();
            for (int i = 0; i < TournamentSize; i++)
            {
                tournament.Add(population[_random.Next(population.Count)]);
            }
            return tournament.OrderByDescending(c => c.Fitness).First();
        }

        private Chromosome Crossover(Chromosome parent1, Chromosome parent2)
        {
            if (_random.NextDouble() > InitialCrossoverRate)
            {
                return parent1.Clone();
            }

            var days = _scheduleInput.DaysInMonth;
            var childGenes = new Dictionary<DateTime, DoctorProfile?>();

            foreach (var day in days)
            {
                childGenes[day] = _random.NextDouble() < 0.5
                    ? parent1.Genes[day]
                    : parent2.Genes[day];
            }

            if (NeedsRepair(childGenes))
            {
                ConstraintValidationService.RepairSchedule(childGenes, _scheduleInput);
            }

            return new Chromosome(childGenes);
        }

        private void Mutation(Chromosome chromosome, int generation)
        {
            double mutationRate = GetAdaptiveMutationRate(generation);
            var days = _scheduleInput.DaysInMonth.ToList();

            var daysByDifficulty = days.OrderBy(day =>
            {
                var availableCount = _scheduleInput.Availability[day]
                    .Count(kvp => kvp.Value == AvailabilityType.Available ||
                                  kvp.Value == AvailabilityType.ConditionallyAvailable);
                return availableCount;
            }).ToList();

            bool anyMutation = false;

            foreach (var day in daysByDifficulty)
            {
                if (_random.NextDouble() < mutationRate)
                {
                    var currentDoctor = chromosome.Genes[day];
                    var workload = _utility.CalculateWorkload(chromosome.Genes);
                    var usedConditionals = CalculateUsedConditionals(chromosome.Genes);

                    var candidates = ConstraintValidationService.GetValidCandidatesForDay(
                        day, _scheduleInput, chromosome.Genes, workload, usedConditionals);

                    if (candidates.Any())
                    {
                        var sortedCandidates = candidates
                            .OrderBy(d => workload.GetValueOrDefault(d.Abbreviation, 0))
                            .ToList();

                        var chosenDoctor = _random.NextDouble() < 0.7
                            ? sortedCandidates[_random.Next(Math.Min(3, sortedCandidates.Count))]
                            : sortedCandidates[_random.Next(sortedCandidates.Count)];

                        if (chosenDoctor?.Abbreviation != currentDoctor?.Abbreviation)
                        {
                            chromosome.Genes[day] = chosenDoctor;
                            anyMutation = true;
                        }
                    }
                }
            }

            if (anyMutation)
            {
                if (NeedsRepair(chromosome.Genes))
                {
                    ConstraintValidationService.RepairSchedule(chromosome.Genes, _scheduleInput);
                }
                chromosome.UpdateHash();
            }
        }

        private bool NeedsRepair(Dictionary<DateTime, DoctorProfile?> schedule)
        {
            var workload = _utility.CalculateWorkload(schedule);
            var usedConditionals = CalculateUsedConditionals(schedule);

            foreach (var (doctorAbbr, count) in workload)
            {
                var limit = _scheduleInput.DutyLimits.GetValueOrDefault(doctorAbbr, 0);
                if (limit > 0 && count > limit)
                    return true;
            }

            foreach (var doctorAbbr in usedConditionals)
            {
                int conditionalCount = 0;
                foreach (var kvp in schedule)
                {
                    if (kvp.Value?.Abbreviation == doctorAbbr &&
                        _scheduleInput.Availability[kvp.Key][doctorAbbr] == AvailabilityType.ConditionallyAvailable)
                    {
                        conditionalCount++;
                        if (conditionalCount > 1)
                            return true;
                    }
                }
            }

            return false;
        }

        private HashSet<string> CalculateUsedConditionals(Dictionary<DateTime, DoctorProfile?> schedule)
        {
            var used = new HashSet<string>();
            foreach (var kvp in schedule)
            {
                if (kvp.Value != null &&
                    _scheduleInput.Availability[kvp.Key][kvp.Value.Abbreviation] == AvailabilityType.ConditionallyAvailable)
                {
                    used.Add(kvp.Value.Abbreviation);
                }
            }
            return used;
        }

        private void PerformMigration()
        {
            for (int i = 0; i < IslandCount; i++)
            {
                var sourceIsland = _islands[i];
                var targetIsland = _islands[(i + 1) % IslandCount];

                var migrants = sourceIsland.Population
                    .OrderByDescending(c => c.Fitness)
                    .Take(MigrantsCount)
                    .Select(c => c.Clone())
                    .ToList();

                var worst = targetIsland.Population
                    .OrderBy(c => c.Fitness)
                    .Take(MigrantsCount)
                    .ToList();

                foreach (var w in worst)
                {
                    targetIsland.Population.Remove(w);
                }

                targetIsland.Population.AddRange(migrants);
            }
        }

        private void PerformHillClimbing()
        {
            foreach (var island in _islands)
            {
                int topCount = Math.Max(1, (int)(island.Population.Count * HillClimbingTopPercentage));
                var topChromosomes = island.Population
                    .OrderByDescending(c => c.Fitness)
                    .Take(topCount)
                    .ToList();

                Parallel.ForEach(topChromosomes, chromosome =>
                {
                    var current = chromosome.Clone();

                    for (int attempt = 0; attempt < 3; attempt++)
                    {
                        var neighbor = current.Clone();

                        var randomDay = _scheduleInput.DaysInMonth[_random.Next(_scheduleInput.DaysInMonth.Count)];
                        var workload = _utility.CalculateWorkload(neighbor.Genes);
                        var usedConditionals = CalculateUsedConditionals(neighbor.Genes);

                        var candidates = ConstraintValidationService.GetValidCandidatesForDay(
                            randomDay, _scheduleInput, neighbor.Genes, workload, usedConditionals);

                        if (candidates.Any())
                        {
                            neighbor.Genes[randomDay] = candidates[_random.Next(candidates.Count)];

                            if (NeedsRepair(neighbor.Genes))
                            {
                                ConstraintValidationService.RepairSchedule(neighbor.Genes, _scheduleInput);
                            }

                            neighbor.UpdateHash();

                            var neighborWorkload = _utility.CalculateWorkload(neighbor.Genes);
                            var neighborMetrics = EvaluationAndScoringService.CalculateMetrics(
                                neighbor.Genes, neighborWorkload, _scheduleInput);
                            neighbor.Fitness = EvaluationAndScoringService.CalculateScore(
                                neighborMetrics, _priorities, _scheduleInput);

                            if (neighbor.Fitness > current.Fitness)
                            {
                                current = neighbor;
                            }
                        }
                    }

                    if (current.Fitness > chromosome.Fitness)
                    {
                        chromosome.Genes = current.Genes;
                        chromosome.Fitness = current.Fitness;
                        chromosome.UpdateHash();
                    }
                });
            }
        }
    }
}