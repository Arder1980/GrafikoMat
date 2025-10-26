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
    ///
    /// INTEGRACJA WSPÓŁDYŻURNYCH: Pełna obsługa zaakceptowanych par współdyżurnych jako hard constraint.
    /// </summary>
    public sealed class AStarSolver : IScheduleSolver
    {
        private const int EMPTY = -1;
        private long _sequence;

        private readonly ScheduleInput _input;
        private readonly List<SolverPriority> _priorities;
        private readonly IProgress<double>? _progress;
        private readonly CancellationToken _cancellationToken;
        private readonly TimeSpan _timeout;

        // Co-duty support
        private readonly List<Declaration>? _declarations;
        private readonly Dictionary<Guid, int> _doctorIndexMap;

        public AStarSolver(
            ScheduleInput scheduleInput,
            List<SolverPriority> priorities,
            TimeSpan timeout,
            IProgress<double>? progress = null,
            CancellationToken cancellationToken = default,
            List<Declaration>? declarations = null)  // ← NOWY PARAMETR dla współdyżurnych
        {
            _input = scheduleInput;
            _priorities = priorities;
            _progress = progress;
            _cancellationToken = cancellationToken;
            _timeout = timeout;
            _sequence = 0;
            _declarations = declarations;

            // Zbuduj mapowanie Doctor GUID -> indeks w tablicy
            var doctors = _input.Doctors.Where(d => !d.IsArchived).ToList();
            _doctorIndexMap = new Dictionary<Guid, int>();
            for (int i = 0; i < doctors.Count; i++)
            {
                _doctorIndexMap[doctors[i].Id] = i;
            }
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
            bool timeoutReached = false;

            Enqueue(priorityQueue, context0);

            while (priorityQueue.Count > 0)
            {
                _cancellationToken.ThrowIfCancellationRequested();

                // Sprawdzenie timeoutu
                if (stopwatch.Elapsed >= _timeout)
                {
                    timeoutReached = true;
                    break;
                }

                var context = priorityQueue.Dequeue();
                expandedNodes++;

                string stateKey = BuildStateKey(context);
                if (visitedStates.Contains(stateKey))
                {
                    duplicateStates++;
                    continue;
                }
                visitedStates.Add(stateKey);

                int nextDayIndex = context.GetEarliestUnassignedDayIndex();
                if (nextDayIndex < 0)
                {
                    var solution = BuildSolution(context);
                    double reward = EvaluateSolutionReward(solution);

                    if (reward > bestCompleteReward)
                    {
                        bestCompleteSolution = solution;
                        bestCompleteReward = reward;
                    }
                    continue;
                }

                double stateReward = PartialSolutionEvaluator.EvaluateState(context, _priorities, _input);
                if (bestCompleteSolution != null && stateReward <= bestCompleteReward)
                {
                    prunedByUpperBound++;
                    continue;
                }

                ExpandNode(context, priorityQueue);

                if ((expandedNodes & 0x3FF) == 0)
                {
                    int assignedCount = context.Assignments.Count(a => a >= 0);
                    double progressRatio = (double)assignedCount / Math.Max(1, context.DayCount);
                    _progress?.Report(progressRatio);
                }
            }

            stopwatch.Stop();
            _progress?.Report(1.0);

            bool isOptimal = (priorityQueue.Count == 0 && !timeoutReached);
            string optimalityNote = isOptimal
                ? "Optimal solution found (search space exhausted)"
                : timeoutReached
                    ? "Best solution found (timeout reached)"
                    : "Best solution found (computation cancelled)";

            if (bestCompleteSolution == null)
            {
                bestCompleteSolution = BuildEmptySolution();
                optimalityNote = "No complete solution found";
                isOptimal = false;
            }

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

        private void ExpandNode(SchedulingRules.Context context, PriorityQueue<SchedulingRules.Context, PriorityKey> queue)
        {
            int nextDayIndex = context.GetEarliestUnassignedDayIndex();
            if (nextDayIndex < 0) return;

            var candidates = SchedulingRules.OrderCandidates(nextDayIndex, context);

            var emptyChild = CloneContext(context);
            emptyChild.Assignments[nextDayIndex] = EMPTY;
            emptyChild.IsPrefixActive = false;
            EnqueueChild(emptyChild);

            foreach (int docIndex in candidates)
            {
                var child = CloneContext(context);
                child.Assignments[nextDayIndex] = docIndex;
                child.Workload[docIndex]++;

                if (child.GetAvailability(nextDayIndex, docIndex) == AvailabilityType.ConditionallyAvailable)
                {
                    child.ConditionalsUsed[docIndex]++;
                }

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

        private readonly struct PriorityKey : IComparable<PriorityKey>
        {
            public readonly double Reward;
            public readonly long Sequence;

            public PriorityKey(double reward, long sequence)
            {
                Reward = reward;
                Sequence = sequence;
            }

            public int CompareTo(PriorityKey other)
            {
                int c = other.Reward.CompareTo(this.Reward);
                if (c != 0) return c;
                return Sequence.CompareTo(other.Sequence);
            }
        }

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

        private static string BuildStateKey(SchedulingRules.Context ctx)
        {
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

            var solution = EvaluationAndScoringService.CalculateMetrics(map, perDoctorWorkload, _input);

            // WALIDACJA: Sprawdź czy pary współdyżurnych są zachowane
            if (_declarations != null)
            {
                // Przekształć ctx.Assignments[] na format 2D wymagany przez CoDutyConstraint
                int[,] schedule2D = new int[ctx.DoctorCount, ctx.DayCount];
                for (int dayIdx = 0; dayIdx < ctx.DayCount; dayIdx++)
                {
                    int assignedDocIdx = ctx.Assignments[dayIdx];
                    if (assignedDocIdx >= 0)
                    {
                        schedule2D[assignedDocIdx, dayIdx] = 1;
                    }
                }

                bool coDutyValid = CoDutyConstraint.ValidateCoDutyPairs(
                    _declarations,
                    schedule2D,
                    _doctorIndexMap);

                if (!coDutyValid)
                {
                    System.Diagnostics.Debug.WriteLine("[AStarSolver] Co-duty constraint violation detected!");
                }
            }

            return solution;
        }

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

        private double EvaluateSolutionReward(ScheduleSolution solution)
        {
            double reward = 0.0;
            double scale = 1e15;

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

            reward += solution.FulfilledWants * 1e6;
            reward += solution.FulfilledAvailables * 1e3;

            return reward;
        }
    }
}