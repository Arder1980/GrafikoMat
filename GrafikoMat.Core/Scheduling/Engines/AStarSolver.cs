using GrafikoMat.Core.Data;
using GrafikoMat.Core.Scheduling.Evaluation;
using GrafikoMat.Core.Scheduling.Models;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;

namespace GrafikoMat.Core.Scheduling.Engines
{
    /// <summary>
    /// Implementacja algorytmu A* do wyszukiwania optymalnego grafiku.
    /// Gwarantuje znalezienie rozwiązania optymalnego poprzez inteligentne przeszukiwanie z heurystyką admissible.
    /// Wykorzystuje hierarchiczne porównanie metryk zgodnie z priorytetami użytkownika.
    /// </summary>
    public sealed class AStarSolver : IScheduleSolver
    {
        private const int EMPTY = -1;
        private long _sequence;

        private readonly ScheduleInput _input;
        private readonly List<SolverPriority> _priorities;
        private readonly IProgress<double>? _progress;
        private readonly CancellationToken _cancellationToken;

        public AStarSolver(
            ScheduleInput scheduleInput,
            List<SolverPriority> priorities,
            IProgress<double>? progress = null,
            CancellationToken cancellationToken = default)
        {
            _input = scheduleInput;
            _priorities = priorities;
            _progress = progress;
            _cancellationToken = cancellationToken;
            _sequence = 0;
        }

        public ScheduleSolution FindOptimalSolution()
        {
            var stopwatch = Stopwatch.StartNew();
            var days = _input.DaysInMonth;
            var doctors = _input.Doctors.Where(d => !d.IsArchived).ToList();
            int dayCount = days.Count;
            int doctorCount = doctors.Count;

            var initialAssignments = Enumerable.Repeat(SchedulingRules.UNASSIGNED, dayCount).ToArray();
            var initialWorkload = new int[doctorCount];
            var initialConditionals = new int[doctorCount];

            var context0 = new SchedulingRules.Context(
                days, doctors, _input.Availability, _input.DutyLimits, _priorities,
                initialAssignments, initialWorkload, initialConditionals, isPrefixActive: true);

            ScheduleSolution? bestCompleteSolution = null;
            double bestCompleteReward = double.NegativeInfinity;

            var priorityQueue = new PriorityQueue<SchedulingRules.Context, PriorityKey>();
            var visitedStates = new HashSet<string>();

            long expandedNodes = 0;
            long prunedByUpperBound = 0;
            long duplicateStates = 0;

            // Dodaj początkowy stan do kolejki
            Enqueue(priorityQueue, context0);

            while (priorityQueue.Count > 0)
            {
                _cancellationToken.ThrowIfCancellationRequested();

                var context = priorityQueue.Dequeue();
                expandedNodes++;

                // Memoizacja: sprawdź czy ten stan był już odwiedzony
                string stateKey = BuildStateKey(context);
                if (visitedStates.Contains(stateKey))
                {
                    duplicateStates++;
                    continue;
                }
                visitedStates.Add(stateKey);

                // Sprawdź czy to kompletne rozwiązanie
                int nextDayIndex = context.GetEarliestUnassignedDayIndex();
                if (nextDayIndex < 0)
                {
                    // Kompletny grafik - oceń i zapisz jeśli najlepszy
                    var solution = BuildSolution(context);
                    double reward = EvaluateSolutionReward(solution);

                    if (reward > bestCompleteReward)
                    {
                        bestCompleteSolution = solution;
                        bestCompleteReward = reward;
                    }
                    continue;
                }

                // Upper bound pruning: jeśli ten częściowy stan nie może poprawić najlepszego kompletnego, skip
                double stateReward = PartialSolutionEvaluator.EvaluateState(context, _priorities, _input);
                if (bestCompleteSolution != null && stateReward <= bestCompleteReward)
                {
                    prunedByUpperBound++;
                    continue;
                }

                // Ekspansja węzła: generuj stany-dzieci
                ExpandNode(context, priorityQueue);

                // Raportuj postęp co ~1024 węzły
                if ((expandedNodes & 0x3FF) == 0)
                {
                    int assignedCount = context.Assignments.Count(a => a >= 0);
                    double progressRatio = (double)assignedCount / Math.Max(1, context.DayCount);
                    _progress?.Report(progressRatio);
                }
            }

            stopwatch.Stop();
            _progress?.Report(1.0);

            // Finalizacja: dodaj informacje diagnostyczne
            bool isOptimal = (priorityQueue.Count == 0); // Kolejka pusta = przeszukano wszystko
            string optimalityNote = isOptimal
                ? "Optimal solution found (search space exhausted)"
                : "Best solution found (computation cancelled)";

            if (bestCompleteSolution == null)
            {
                // Nie znaleziono żadnego kompletnego rozwiązania - zwróć pusty grafik
                bestCompleteSolution = BuildEmptySolution();
                optimalityNote = "No complete solution found";
                isOptimal = false;
            }

            // Stwórz nowe rozwiązanie z diagnostyką
            return new ScheduleSolution
            {
                Assignments = bestCompleteSolution.Assignments,
                InitialContinuity = bestCompleteSolution.InitialContinuity,
                FulfilledReservations = bestCompleteSolution.FulfilledReservations,
                FulfilledWants = bestCompleteSolution.FulfilledWants,
                FulfilledAvailables = bestCompleteSolution.FulfilledAvailables,
                FairnessIndex = bestCompleteSolution.FairnessIndex,
                SpacingIndex = bestCompleteSolution.SpacingIndex,
                FinalWorkload = bestCompleteSolution.FinalWorkload,

                // Diagnostyka
                IsProvablyOptimal = isOptimal,
                NodesExpanded = expandedNodes,
                ComputationTime = stopwatch.Elapsed,
                OptimalityNote = $"{optimalityNote} | Nodes: {expandedNodes:N0}, Pruned: {prunedByUpperBound:N0}, Duplicates: {duplicateStates:N0}"
            };

            void Enqueue(PriorityQueue<SchedulingRules.Context, PriorityKey> queue, SchedulingRules.Context ctx)
            {
                double reward = PartialSolutionEvaluator.EvaluateState(ctx, _priorities, _input);
                var key = new PriorityKey(reward: reward, sequence: _sequence++);
                queue.Enqueue(ctx, key);
            }
        }

        /// <summary>
        /// Rozwija węzeł: generuje wszystkie możliwe stany-dzieci (pusty dzień + każdy kandydat).
        /// </summary>
        private void ExpandNode(SchedulingRules.Context context, PriorityQueue<SchedulingRules.Context, PriorityKey> queue)
        {
            int nextDayIndex = context.GetEarliestUnassignedDayIndex();
            if (nextDayIndex < 0) return; // Nie powinno się zdarzyć

            var candidates = SchedulingRules.OrderCandidates(nextDayIndex, context);

            // Gałąź 1: Pusty dzień
            var emptyChild = CloneContext(context);
            emptyChild.Assignments[nextDayIndex] = EMPTY;
            emptyChild.IsPrefixActive = false; // Dziura w grafiku przerywa prefix
            EnqueueChild(emptyChild);

            // Gałęzie 2+: Każdy kandydat
            foreach (int docIndex in candidates)
            {
                var child = CloneContext(context);
                child.Assignments[nextDayIndex] = docIndex;
                child.Workload[docIndex]++;

                if (child.GetAvailability(nextDayIndex, docIndex) == AvailabilityType.ConditionallyAvailable)
                {
                    child.ConditionalsUsed[docIndex]++;
                }

                // Sprawdź czy prefix jest aktywny
                bool keepsPrefix = (nextDayIndex == 0) || (child.Assignments[nextDayIndex - 1] != SchedulingRules.UNASSIGNED);
                child.IsPrefixActive = child.IsPrefixActive && keepsPrefix;

                EnqueueChild(child);
            }

            void EnqueueChild(SchedulingRules.Context childContext)
            {
                double reward = PartialSolutionEvaluator.EvaluateState(childContext, _priorities, _input);
                var key = new PriorityKey(reward: reward, sequence: _sequence++);
                queue.Enqueue(childContext, key);
            }
        }

        /// <summary>
        /// Klucz priorytetowy dla kolejki: wyższy reward = wyższy priorytet (maksymalizacja).
        /// </summary>
        private readonly struct PriorityKey : IComparable<PriorityKey>
        {
            public readonly double Reward; // Im wyższy, tym lepszy
            public readonly long Sequence; // Tie-breaker

            public PriorityKey(double reward, long sequence)
            {
                Reward = reward;
                Sequence = sequence;
            }

            public int CompareTo(PriorityKey other)
            {
                // ODWRÓCONE: wyższy reward = wyższy priorytet (dequeue first)
                int c = other.Reward.CompareTo(this.Reward);
                if (c != 0) return c;
                return Sequence.CompareTo(other.Sequence); // Starsze najpierw (FIFO przy remisie)
            }
        }

        /// <summary>
        /// Klonuje kontekst (głęboka kopia tablic).
        /// </summary>
        private static SchedulingRules.Context CloneContext(SchedulingRules.Context src)
        {
            var assignments = new int[src.DayCount];
            Array.Copy(src.Assignments, assignments, src.DayCount);

            var workload = new int[src.DoctorCount];
            Array.Copy(src.Workload, workload, src.DoctorCount);

            var conditionals = new int[src.DoctorCount];
            Array.Copy(src.ConditionalsUsed, conditionals, src.DoctorCount);

            return new SchedulingRules.Context(
                src.Days, src.Doctors, src.Availability, src.DutyLimitsByAbbr, src.Priorities,
                assignments, workload, conditionals, src.IsPrefixActive
            );
        }

        /// <summary>
        /// Buduje klucz stanu dla memoizacji (unikanie duplikatów).
        /// </summary>
        private static string BuildStateKey(SchedulingRules.Context ctx)
        {
            // Prosty hash: assignments + workload + conditionals
            var parts = new List<string>(ctx.DayCount + ctx.DoctorCount * 2);

            parts.Add("A:");
            foreach (var a in ctx.Assignments)
                parts.Add(a.ToString());

            parts.Add("|W:");
            foreach (var w in ctx.Workload)
                parts.Add(w.ToString());

            parts.Add("|C:");
            foreach (var c in ctx.ConditionalsUsed)
                parts.Add(c.ToString());

            return string.Join(",", parts);
        }

        /// <summary>
        /// Konwertuje kontekst na ScheduleSolution.
        /// </summary>
        private ScheduleSolution BuildSolution(SchedulingRules.Context ctx)
        {
            var map = new Dictionary<DateTime, DoctorProfile?>(ctx.DayCount);
            var perDoctorWorkload = new Dictionary<string, int>(ctx.DoctorCount);

            foreach (var doc in ctx.Doctors)
                perDoctorWorkload[doc.Abbreviation] = 0;

            for (int d = 0; d < ctx.DayCount; d++)
            {
                int assignmentIndex = ctx.Assignments[d];
                var date = ctx.Days[d];

                if (assignmentIndex >= 0)
                {
                    var doctor = ctx.Doctors[assignmentIndex];
                    map[date] = doctor;
                    perDoctorWorkload[doctor.Abbreviation]++;
                }
                else
                {
                    map[date] = null;
                }
            }

            return EvaluationAndScoringService.CalculateMetrics(map, perDoctorWorkload, _input);
        }

        /// <summary>
        /// Tworzy puste rozwiązanie (fallback gdy nie znaleziono żadnego).
        /// </summary>
        private ScheduleSolution BuildEmptySolution()
        {
            var emptyMap = new Dictionary<DateTime, DoctorProfile?>();
            foreach (var day in _input.DaysInMonth)
                emptyMap[day] = null;

            var emptyWorkload = new Dictionary<string, int>();
            foreach (var doc in _input.Doctors.Where(d => !d.IsArchived))
                emptyWorkload[doc.Abbreviation] = 0;

            return EvaluationAndScoringService.CalculateMetrics(emptyMap, emptyWorkload, _input);
        }

        /// <summary>
        /// Oblicza reward (wartość maksymalizowaną) dla kompletnego rozwiązania.
        /// Używa tej samej logiki co PartialSolutionEvaluator dla spójności.
        /// </summary>
        private double EvaluateSolutionReward(ScheduleSolution solution)
        {
            double reward = 0.0;
            double scale = 1e15;

            // Rezerwacje najważniejsze
            reward += solution.FulfilledReservations * 1e18;

            int totalDays = _input.DaysInMonth.Count;

            foreach (var priority in _priorities)
            {
                double metricValue = 0.0;

                switch (priority)
                {
                    case SolverPriority.InitialContinuity:
                        metricValue = solution.InitialContinuity / (double)Math.Max(1, totalDays);
                        break;

                    case SolverPriority.TotalAssignments:
                        metricValue = solution.TotalAssignments / (double)Math.Max(1, totalDays);
                        break;

                    case SolverPriority.Fairness:
                        metricValue = 1.0 / (1.0 + solution.FairnessIndex);
                        break;

                    case SolverPriority.Spacing:
                        metricValue = 1.0 / (1.0 + solution.SpacingIndex);
                        break;

                    case SolverPriority.DeclarationCompliance:
                        int totalDeclared = solution.FulfilledWants + solution.FulfilledAvailables;
                        if (totalDeclared > 0)
                        {
                            double weightedSum = solution.FulfilledWants * 2.0 + solution.FulfilledAvailables * 1.0;
                            metricValue = weightedSum / (2.0 * totalDeclared);
                        }
                        break;
                }

                reward += metricValue * scale;
                scale /= 1000.0;
            }

            // Dodatkowe bonusy
            reward += solution.FulfilledWants * 1e6;
            reward += solution.FulfilledAvailables * 1e3;

            return reward;
        }
    }
}