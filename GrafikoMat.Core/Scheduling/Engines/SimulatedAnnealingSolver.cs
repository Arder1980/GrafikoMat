using GrafikoMat.Core.Data;
using GrafikoMat.Core.Scheduling.Evaluation;
using GrafikoMat.Core.Scheduling.Models;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace GrafikoMat.Core.Scheduling.Engines
{
    /// <summary>
    /// ZOPTYMALIZOWANA implementacja silnika Simulated Annealing.
    /// 
    /// KLUCZOWE OPTYMALIZACJE (WARIANT B):
    /// - Adaptacyjne parametry (temperatura, cooling rate) dopasowane do rozmiaru problemu
    /// - Early stopping przy stagnacji (500 iteracji bez poprawy)
    /// - Cache odwiedzonych stanów (HashSet) - unika duplikatów
    /// - Smart neighbor generation (swap, shift, single) zależna od temperatury
    /// - Równoległa eksploracja 4 kandydatów w każdej iteracji
    /// - Delta evaluation (przyrostowe obliczanie fitness) - OPCJONALNE, tu używamy quick estimate
    /// 
    /// OCZEKIWANY REZULTAT: 85-90% redukcja czasu, 97-99% jakości
    /// </summary>
    public class SimulatedAnnealingSolver : IScheduleSolver
    {
        // ==================== STAŁE KONFIGURACYJNE ====================

        private const int PARALLEL_CANDIDATES = 4;        // Liczba równolegle testowanych kandydatów
        private const int MAX_STAGNATION = 500;           // Max iteracji bez poprawy przed early stop
        private const int MAX_VISITED_CACHE = 50000;      // Max rozmiar cache stanów (memory limit)

        // ==================== POLA PRYWATNE ====================

        private readonly ScheduleInput _scheduleInput;
        private readonly List<SolverPriority> _priorities;
        private readonly IProgress<double>? _progressReporter;
        private readonly CancellationToken _cancellationToken;
        private readonly Random _random = new();
        private readonly SolverUtility _utility;
        private readonly DeltaEvaluator _deltaEvaluator;

        private readonly double _coolingRate;
        private readonly int _iterationsPerTemperature;
        private readonly double _initialTemperature;

        private HashSet<string> _visitedStates = new();

        // ==================== KONSTRUKTOR ====================

        public SimulatedAnnealingSolver(
            ScheduleInput scheduleInput,
            List<SolverPriority> priorities,
            double coolingRate,
            IProgress<double>? progress = null,
            CancellationToken cancellationToken = default)
        {
            _scheduleInput = scheduleInput;
            _priorities = priorities;
            _progressReporter = progress;
            _cancellationToken = cancellationToken;
            _utility = new SolverUtility(scheduleInput);
            _deltaEvaluator = new DeltaEvaluator(scheduleInput, priorities);

            // ==================== ADAPTACYJNE PARAMETRY ====================

            int daysCount = scheduleInput.DaysInMonth.Count;
            int doctorCount = scheduleInput.Doctors.Count(d => !d.IsArchived);
            double problemComplexity = Math.Log(daysCount * doctorCount + 1);

            // Temperatura początkowa skalowana do rozmiaru problemu
            _initialTemperature = 500.0 * problemComplexity;

            // Szybsze chłodzenie dla większych problemów
            _coolingRate = daysCount > 20 ? 0.97 : (coolingRate > 0 ? coolingRate : 0.985);

            // Mniej iteracji na temperaturę (ale więcej temperatur dzięki szybszemu cooling)
            _iterationsPerTemperature = Math.Max(30, doctorCount * 2);
        }

        // ==================== GŁÓWNA METODA ROZWIĄZYWANIA ====================

        public ScheduleSolution FindOptimalSolution()
        {
            // === INICJALIZACJA ===

            var currentSolution = _utility.CreateGreedyInitialSolution();
            var bestSolution = new Dictionary<DateTime, DoctorProfile?>(currentSolution);

            var currentWorkload = _utility.CalculateWorkload(currentSolution);
            var currentMetrics = EvaluationAndScoringService.CalculateMetrics(currentSolution, currentWorkload, _scheduleInput);
            double bestFitness = EvaluationAndScoringService.CalculateScore(currentMetrics, _priorities, _scheduleInput);
            double currentFitness = bestFitness;

            double temperature = _initialTemperature;
            int totalIterations = (int)(Math.Log(0.1 / _initialTemperature) / Math.Log(_coolingRate)) * _iterationsPerTemperature;
            int currentIteration = 0;
            int iterationsWithoutImprovement = 0;

            // Dodaj stan początkowy do cache
            string initialHash = _utility.ComputeQuickHash(currentSolution);
            _visitedStates.Add(initialHash);

            // === GŁÓWNA PĘTLA SYMULOWANEGO WYŻARZANIA ===

            while (temperature > 0.1)
            {
                for (int i = 0; i < _iterationsPerTemperature; i++)
                {
                    _cancellationToken.ThrowIfCancellationRequested();

                    // ==================== RÓWNOLEGŁA EKSPLORACJA SĄSIADÓW ====================

                    var candidates = new ConcurrentBag<(Dictionary<DateTime, DoctorProfile?> solution, double fitness, string hash)>();

                    Parallel.For(0, PARALLEL_CANDIDATES, candidateIdx =>
                    {
                        try
                        {
                            // Generuj smart neighbor (typ ruchu zależy od temperatury)
                            var neighbor = _utility.GenerateSmartNeighbor(currentSolution, temperature);

                            // Napraw jeśli narusza twarde ograniczenia
                            Validation.ConstraintValidationService.RepairSchedule(neighbor, _scheduleInput);

                            // Oblicz hash (do cache)
                            string hash = _utility.ComputeQuickHash(neighbor);

                            // Szybka estymacja fitness (zamiast pełnego przeliczenia)
                            var neighborWorkload = _utility.CalculateWorkload(neighbor);
                            double estimatedFitness = _deltaEvaluator.QuickEstimateFitness(neighbor, neighborWorkload);

                            candidates.Add((neighbor, estimatedFitness, hash));
                        }
                        catch
                        {
                            // Ignoruj błędy w równoległych wątkach
                        }
                    });

                    if (!candidates.Any())
                        continue;

                    // Wybierz najlepszego kandydata
                    var (bestCandidate, estimatedFitness, candidateHash) = candidates.OrderByDescending(c => c.fitness).First();

                    // ==================== CACHE STANÓW - UNIKAJ DUPLIKATÓW ====================

                    if (_visitedStates.Contains(candidateHash))
                    {
                        iterationsWithoutImprovement++;
                        currentIteration++;
                        continue; // Pomiń jeśli już odwiedzony
                    }

                    // Pełne przeliczenie fitness tylko dla wybranego kandydata
                    var newWorkload = _utility.CalculateWorkload(bestCandidate);
                    var newMetrics = EvaluationAndScoringService.CalculateMetrics(bestCandidate, newWorkload, _scheduleInput);
                    double newFitness = EvaluationAndScoringService.CalculateScore(newMetrics, _priorities, _scheduleInput);

                    // ==================== KRYTERIUM AKCEPTACJI ====================

                    bool accept = false;

                    if (newFitness > currentFitness)
                    {
                        // Akceptuj poprawę
                        accept = true;
                    }
                    else
                    {
                        // Akceptuj pogorszenie z prawdopodobieństwem
                        double acceptanceProbability = Math.Exp((newFitness - currentFitness) / temperature);
                        accept = _random.NextDouble() < acceptanceProbability;
                    }

                    if (accept)
                    {
                        currentSolution = bestCandidate;
                        currentWorkload = newWorkload;
                        currentFitness = newFitness;

                        // Dodaj do cache (z limitem pamięci)
                        if (_visitedStates.Count < MAX_VISITED_CACHE)
                        {
                            _visitedStates.Add(candidateHash);
                        }

                        // Sprawdź czy to nowy best
                        if (currentFitness > bestFitness)
                        {
                            bestSolution = new Dictionary<DateTime, DoctorProfile?>(currentSolution);
                            bestFitness = currentFitness;
                            iterationsWithoutImprovement = 0;
                        }
                        else
                        {
                            iterationsWithoutImprovement++;
                        }
                    }
                    else
                    {
                        iterationsWithoutImprovement++;
                    }

                    // ==================== EARLY STOPPING ====================

                    if (iterationsWithoutImprovement > MAX_STAGNATION)
                    {
                        // Przerwij jeśli długo brak poprawy
                        goto finish; // Wyjście z zagnieżdżonych pętli
                    }

                    currentIteration++;
                }

                // Chłodzenie
                temperature *= _coolingRate;

                // Raportuj postęp
                if (totalIterations > 0)
                {
                    _progressReporter?.Report(Math.Min(1.0, (double)currentIteration / totalIterations));
                }
            }

        finish:

            // === FINALIZACJA ===

            var finalWorkload = _utility.CalculateWorkload(bestSolution);
            var finalSolution = EvaluationAndScoringService.CalculateMetrics(bestSolution, finalWorkload, _scheduleInput);

            // Raportuj 100%
            _progressReporter?.Report(1.0);

            return finalSolution;
        }
    }
}