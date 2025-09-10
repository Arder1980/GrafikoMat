using GrafikoMat.Core.Data;
using GrafikoMat.Core.Scheduling.Engines.Algorithms;
using GrafikoMat.Core.Scheduling.Evaluation;
using GrafikoMat.Core.Scheduling.Models;
using GrafikoMat.Core.Scheduling.Validation;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;

namespace GrafikoMat.Core.Scheduling.Engines
{
    /// <summary>
    /// Deterministyczny silnik generujący grafik z użyciem algorytmu z nawrotami (backtracking).
    /// Gwarantuje znalezienie optymalnego rozwiązania poprzez pełne przeszukanie przestrzeni możliwych decyzji.
    /// Wersja zaadaptowana z GrafikWPF.
    /// </summary>
    public sealed class BacktrackingSolver : IScheduleSolver
    {
        private const int EMPTY = -1;
        private const int UNASSIGNED = int.MinValue;

        private readonly ScheduleInput _input;
        private readonly List<SolverPriority> _priorities;
        private readonly IProgress<double>? _progress;
        private readonly CancellationToken _cancellationToken;

        private readonly List<DateTime> _days;
        private readonly List<DoctorProfile> _doctors;
        private readonly Dictionary<string, int> _dutyLimitsByAbbr;

        // Tablice przechowujące bieżący stan w trakcie przeszukiwania
        private readonly int[] _assignments;       // przypisanie na dany dzień: indeks lekarza, EMPTY, lub UNASSIGNED
        private readonly int[] _workload;          // dyżury per lekarz
        private readonly int[] _conditionalsUsed;  // licznik "Mogę warunkowo" per lekarz

        // Przechowuje najlepszy znaleziony dotychczas wynik
        private ScheduleSolution? _bestSolution;

        private readonly bool _useFlowUpperBound = true;
        private readonly int _flowCheckFrequency = 6; // Sprawdzaj co 6 poziomów głębokości

        private readonly HashSet<string> _visitedStates = new HashSet<string>();

        public BacktrackingSolver(
            ScheduleInput scheduleInput,
            List<SolverPriority> priorities,
            IProgress<double>? progress = null,
            CancellationToken token = default)
        {
            _input = scheduleInput;
            _priorities = priorities;
            _progress = progress;
            _cancellationToken = token;

            _days = _input.DaysInMonth;
            _doctors = _input.Doctors.Where(d => !d.IsArchived).ToList();
            _dutyLimitsByAbbr = _input.DutyLimits;

            _assignments = Enumerable.Repeat(UNASSIGNED, _days.Count).ToArray();
            _workload = new int[_doctors.Count];
            _conditionalsUsed = new int[_doctors.Count];
        }

        public ScheduleSolution FindOptimalSolution()
        {
            // TODO: Dodać logowanie startu
            Dfs(depth: 0);

            _bestSolution ??= BuildSolutionFromState();

            // TODO: Dodać logowanie końca
            return _bestSolution;
        }

        private void Dfs(int depth)
        {
            _cancellationToken.ThrowIfCancellationRequested();

            var stateKey = BuildStateKey();
            if (_visitedStates.Contains(stateKey)) return;
            _visitedStates.Add(stateKey);

            if ((depth & 0x7FF) == 0) // Raportuj postęp co ~2048 węzłów
            {
                int filled = _assignments.Count(a => a != UNASSIGNED);
                _progress?.Report((double)filled / _assignments.Length);
            }

            int nextDayIndex = ChooseDayToAssign();
            if (nextDayIndex < 0) // Wszystkie dni przypisane, liść drzewa
            {
                ConsiderAsBest(BuildSolutionFromState());
                return;
            }

            // Sonda z użyciem Max-Flow, aby sprawdzić, czy dalsze przypisania są w ogóle możliwe
            if (_useFlowUpperBound && (depth % _flowCheckFrequency == 0))
            {
                int remainingAssignmentsPossible = FlowUpperBound.Calculate(
                    _days.Count, _doctors.Count,
                    getAvailabilityMask: (d, p) => IsHardFeasible(d, p) ? AvailabilityMask.Any : AvailabilityMask.None,
                    getRemainingCapacityPerDoctor: p =>
                    {
                        int limit = _dutyLimitsByAbbr.GetValueOrDefault(_doctors[p].Abbreviation, 0);
                        return limit == 0 ? int.MaxValue : Math.Max(0, limit - _workload[p]);
                    },
                    isDayAllowed: d => _assignments[d] == UNASSIGNED
                );

                if (remainingAssignmentsPossible == 0)
                {
                    // Nie da się już nikogo przypisać. Zamykamy resztę jako puste i oceniamy.
                    var changedIndices = new List<int>();
                    for (int i = 0; i < _assignments.Length; i++)
                        if (_assignments[i] == UNASSIGNED) { _assignments[i] = EMPTY; changedIndices.Add(i); }

                    ConsiderAsBest(BuildSolutionFromState());

                    // Przywracamy stan do dalszego przeszukiwania
                    foreach (var i in changedIndices) _assignments[i] = UNASSIGNED;
                    return;
                }
            }

            var candidates = GetOrderedCandidates(nextDayIndex);

            // Rozgałęzienie po lekarzach
            foreach (var doctorIndex in candidates)
            {
                if (!TryAssign(nextDayIndex, doctorIndex)) continue;
                Dfs(depth + 1);
                Unassign(nextDayIndex, doctorIndex);
            }

            // Rozgałęzienie dla pustego dnia (na końcu)
            TryAssignEmpty(nextDayIndex);
            Dfs(depth + 1);
            UnassignEmpty(nextDayIndex);
        }

        /// <summary>
        /// Wybiera "najtrudniejszy" dzień (MRV - Minimum Remaining Values): najkrótsza lista legalnych kandydatów.
        /// </summary>
        private int ChooseDayToAssign()
        {
            int bestDayIndex = -1;
            int minCandidatesCount = int.MaxValue;

            for (int i = 0; i < _days.Count; i++)
            {
                if (_assignments[i] != UNASSIGNED) continue;

                int count = GetOrderedCandidates(i).Count + 1; // +1 dla opcji "pusty dzień"
                if (count < minCandidatesCount)
                {
                    minCandidatesCount = count;
                    bestDayIndex = i;
                    if (minCandidatesCount <= 1) break; // Optymalizacja: nie da się bardziej ograniczyć
                }
            }
            return bestDayIndex;
        }

        private List<int> GetOrderedCandidates(int dayIndex)
        {
            var candidates = new List<int>();
            for (int i = 0; i < _doctors.Count; i++)
            {
                if (IsHardFeasible(dayIndex, i))
                {
                    candidates.Add(i);
                }
            }
            // TODO: Dodać logikę sortowania kandydatów (LCV - Least Constraining Value)
            return candidates;
        }

        private bool IsHardFeasible(int dayIndex, int doctorIndex)
        {
            var doctor = _doctors[doctorIndex];
            var day = _days[dayIndex];

            // Limit lekarza
            int limit = _dutyLimitsByAbbr.GetValueOrDefault(doctor.Abbreviation, 0);
            if (limit > 0 && _workload[doctorIndex] >= limit) return false;

            // Dostępność
            var availability = _input.Availability[day][doctor.Abbreviation];
            if (Declarations.IsHardBlock(availability)) return false;

            // Dzień po dniu
            if (dayIndex > 0 && _assignments[dayIndex - 1] == doctorIndex) return false;

            // Sąsiedztwo "Innego dyżuru"
            if (_input.Availability.TryGetValue(day.AddDays(-1), out var yesterdayAvail) && yesterdayAvail[doctor.Abbreviation] == AvailabilityType.OtherDuty) return false;
            if (_input.Availability.TryGetValue(day.AddDays(1), out var tomorrowAvail) && tomorrowAvail[doctor.Abbreviation] == AvailabilityType.OtherDuty) return false;

            // "Mogę warunkowo"
            if (availability == AvailabilityType.ConditionallyAvailable && _conditionalsUsed[doctorIndex] >= 1) return false;

            return true;
        }

        private bool TryAssign(int dayIndex, int doctorIndex)
        {
            if (!IsHardFeasible(dayIndex, doctorIndex)) return false;

            _assignments[dayIndex] = doctorIndex;
            _workload[doctorIndex]++;
            if (_input.Availability[_days[dayIndex]][_doctors[doctorIndex].Abbreviation] == AvailabilityType.ConditionallyAvailable)
            {
                _conditionalsUsed[doctorIndex]++;
            }
            return true;
        }

        private void Unassign(int dayIndex, int doctorIndex)
        {
            if (_input.Availability[_days[dayIndex]][_doctors[doctorIndex].Abbreviation] == AvailabilityType.ConditionallyAvailable)
            {
                _conditionalsUsed[doctorIndex]--;
            }
            _workload[doctorIndex]--;
            _assignments[dayIndex] = UNASSIGNED;
        }

        private void TryAssignEmpty(int dayIndex) => _assignments[dayIndex] = EMPTY;
        private void UnassignEmpty(int dayIndex) => _assignments[dayIndex] = UNASSIGNED;

        private void ConsiderAsBest(ScheduleSolution candidate)
        {
            if (_bestSolution == null || SolutionComparer.CompareSolutionsByPriorities(candidate, _bestSolution, _priorities, _input) > 0)
            {
                _bestSolution = candidate;
                // TODO: Logowanie poprawy
            }
        }

        private ScheduleSolution BuildSolutionFromState()
        {
            var assignmentsMap = new Dictionary<DateTime, DoctorProfile?>(_days.Count);
            for (int i = 0; i < _days.Count; i++)
            {
                if (_assignments[i] >= 0)
                    assignmentsMap[_days[i]] = _doctors[_assignments[i]];
                else
                    assignmentsMap[_days[i]] = null;
            }

            var finalWorkload = new Dictionary<string, int>();
            for (int i = 0; i < _doctors.Count; i++)
            {
                finalWorkload[_doctors[i].Abbreviation] = _workload[i];
            }

            return EvaluationAndScoringService.CalculateMetrics(assignmentsMap, finalWorkload, _input);
        }

        private string BuildStateKey()
        {
            var sb = new StringBuilder(_assignments.Length * 3 + _workload.Length * 2 + _conditionalsUsed.Length * 2);
            sb.Append('A');
            for (int i = 0; i < _assignments.Length; i++) { sb.Append(_assignments[i]); sb.Append(';'); }
            sb.Append('W');
            for (int i = 0; i < _workload.Length; i++) { sb.Append(_workload[i]); sb.Append(','); }
            sb.Append('M');
            for (int i = 0; i < _conditionalsUsed.Length; i++) { sb.Append(_conditionalsUsed[i]); sb.Append(','); }
            return sb.ToString();
        }
    }
}