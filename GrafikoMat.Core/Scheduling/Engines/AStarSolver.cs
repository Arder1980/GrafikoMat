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
    /// Implementacja silnika A* do wyszukiwania optymalnego grafiku.
    /// Jest to algorytm przeszukiwania heurystycznego, który stara się inteligentnie wybierać najbardziej obiecujące ścieżki.
    /// Wersja zaadaptowana z GrafikWPF.
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

            ScheduleSolution? bestSolution = null;

            var priorityQueue = new PriorityQueue<SchedulingRules.Context, PriorityKey>();
            Enqueue(context0);

            long expandedNodes = 0;
            var stopwatch = Stopwatch.StartNew();

            while (priorityQueue.Count > 0)
            {
                _cancellationToken.ThrowIfCancellationRequested();
                var context = priorityQueue.Dequeue();
                expandedNodes++;

                int nextDayIndex = context.GetEarliestUnassignedDayIndex();
                if (nextDayIndex < 0) // Liść drzewa - znaleziono pełne rozwiązanie
                {
                    var solution = BuildSolution(context);
                    if (bestSolution == null || SolutionComparer.CompareSolutionsByPriorities(solution, bestSolution, _priorities, _input) > 0)
                    {
                        bestSolution = solution;
                    }
                    continue;
                }

                var candidates = SchedulingRules.OrderCandidates(nextDayIndex, context);

                // Gałąź dla pustego dnia
                var emptyChild = CloneContext(context);
                emptyChild.Assignments[nextDayIndex] = EMPTY;
                emptyChild.IsPrefixActive = false; // Dziura w grafiku przerywa prefiks
                Enqueue(emptyChild);

                // Gałęzie dla każdego kandydata
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

                    Enqueue(child);
                }

                if ((expandedNodes & 0x3FF) == 0) // Raportuj postęp co ~1024 węzły
                {
                    double assignedRatio = (double)(context.DayCount - context.Assignments.Count(a => a == SchedulingRules.UNASSIGNED)) / Math.Max(1, context.DayCount);
                    _progress?.Report(assignedRatio);
                }
            }

            _progress?.Report(1.0);
            return bestSolution ?? BuildSolution(context0);

            void Enqueue(SchedulingRules.Context sctx)
            {
                int assignedCount = sctx.Assignments.Count(a => a >= 0);
                // Heurystyka: preferuj stany z większą liczbą już przypisanych dyżurów
                var key = new PriorityKey(cost: -assignedCount, sequence: _sequence++);
                priorityQueue.Enqueue(sctx, key);
            }
        }

        private readonly struct PriorityKey : IComparable<PriorityKey>
        {
            public readonly int Cost; // Koszt (im niższy, tym lepszy)
            public readonly long Sequence; // Do rozstrzygania remisów
            public PriorityKey(int cost, long sequence) { Cost = cost; Sequence = sequence; }

            public int CompareTo(PriorityKey other)
            {
                int c = Cost.CompareTo(other.Cost);
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

        private ScheduleSolution BuildSolution(SchedulingRules.Context ctx)
        {
            var map = new Dictionary<DateTime, DoctorProfile?>(ctx.DayCount);
            var perDoctorWorkload = new Dictionary<string, int>(ctx.DoctorCount);
            foreach (var doc in ctx.Doctors) perDoctorWorkload[doc.Abbreviation] = 0;

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
    }
}