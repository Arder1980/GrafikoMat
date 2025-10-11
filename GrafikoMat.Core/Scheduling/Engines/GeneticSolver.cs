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
    /// Implementacja silnika generującego grafik za pomocą algorytmu genetycznego.
    /// Wersja zaadaptowana z GrafikWPF.
    /// </summary>
    public class GeneticSolver : IScheduleSolver
    {
        private class Chromosome
        {
            public Dictionary<DateTime, DoctorProfile?> Genes { get; set; }
            public double Fitness { get; set; }

            public Chromosome(Dictionary<DateTime, DoctorProfile?> genes)
            {
                Genes = genes;
                Fitness = 0.0;
            }

            public Chromosome Clone()
            {
                return new Chromosome(new Dictionary<DateTime, DoctorProfile?>(Genes));
            }
        }

        private readonly int _populationSize;
        private readonly int _generations;
        private const double CrossoverRate = 0.85;
        private const double MutationRate = 0.05;
        private const int TournamentSize = 5;

        private readonly ScheduleInput _scheduleInput;
        private readonly List<SolverPriority> _priorities;
        private readonly IProgress<double>? _progressReporter;
        private readonly CancellationToken _cancellationToken;
        private readonly SolverUtility _utility;

        private List<Chromosome> _population = new();
        private readonly Random _random = new();

        // ZMIANA: Konstruktor został zaktualizowany, aby przyjmować parametry z zewnątrz.
        public GeneticSolver(
            ScheduleInput scheduleInput,
            List<SolverPriority> priorities,
            int populationSize, // <-- Nowy parametr
            int generations,    // <-- Nowy parametr
            IProgress<double>? progress = null,
            CancellationToken cancellationToken = default)
        {
            _scheduleInput = scheduleInput;
            _priorities = priorities;
            _progressReporter = progress;
            _cancellationToken = cancellationToken;
            _utility = new SolverUtility(scheduleInput);

            _populationSize = populationSize;
            _generations = generations;
        }

        public ScheduleSolution FindOptimalSolution()
        {
            CreateInitialPopulation();
            CalculateFitness();

            for (int i = 0; i < _generations; i++)
            {
                _cancellationToken.ThrowIfCancellationRequested();
                var newPopulation = new ConcurrentBag<Chromosome>();

                // Elityzm: najlepszy osobnik z poprzedniej generacji przechodzi do nowej bez zmian
                var best = _population.OrderByDescending(c => c.Fitness).First();
                newPopulation.Add(best.Clone());

                Parallel.For(1, _populationSize, _ =>
                {
                    var parent1 = Selection();
                    var parent2 = Selection();
                    var child = Crossover(parent1, parent2);
                    Mutation(child);
                    newPopulation.Add(child);
                });
                _population = newPopulation.ToList();
                CalculateFitness();
                _progressReporter?.Report((double)(i + 1) / _generations);
            }

            var finalBest = _population.OrderByDescending(c => c.Fitness).First();
            var finalWorkload = _utility.CalculateWorkload(finalBest.Genes);
            return EvaluationAndScoringService.CalculateMetrics(finalBest.Genes, finalWorkload, _scheduleInput);
        }

        private void CreateInitialPopulation()
        {
            _population = new List<Chromosome>();
            for (int i = 0; i < _populationSize; i++)
            {
                _population.Add(new Chromosome(_utility.CreateRandomSolution()));
            }
        }

        private void CalculateFitness()
        {
            Parallel.ForEach(_population, chromosome =>
            {
                var workload = _utility.CalculateWorkload(chromosome.Genes);
                var metrics = EvaluationAndScoringService.CalculateMetrics(chromosome.Genes, workload, _scheduleInput);
                chromosome.Fitness = EvaluationAndScoringService.CalculateScore(metrics, _priorities, _scheduleInput);
            });
        }

        private Chromosome Selection()
        {
            var tournament = new List<Chromosome>();
            for (int i = 0; i < TournamentSize; i++)
            {
                tournament.Add(_population[_random.Next(_populationSize)]);
            }
            return tournament.OrderByDescending(c => c.Fitness).First();
        }

        private Chromosome Crossover(Chromosome parent1, Chromosome parent2)
        {
            if (_random.NextDouble() > CrossoverRate)
            {
                return parent1.Clone();
            }

            var crossoverPoint = _random.Next(_scheduleInput.DaysInMonth.Count);
            var days = _scheduleInput.DaysInMonth;
            var childGenes = new Dictionary<DateTime, DoctorProfile?>();

            for (int i = 0; i < days.Count; i++)
            {
                childGenes[days[i]] = i < crossoverPoint ?
                    parent1.Genes[days[i]] : parent2.Genes[days[i]];
            }

            ConstraintValidationService.RepairSchedule(childGenes, _scheduleInput);
            return new Chromosome(childGenes);
        }

        private void Mutation(Chromosome chromosome)
        {
            foreach (var day in _scheduleInput.DaysInMonth)
            {
                if (_random.NextDouble() < MutationRate)
                {
                    // Generujemy sąsiada dla tego jednego dnia
                    var neighborForDay = _utility.GenerateNeighbor(chromosome.Genes);
                    chromosome.Genes[day] = neighborForDay[day];
                }
            }
            ConstraintValidationService.RepairSchedule(chromosome.Genes, _scheduleInput);
        }
    }
}