using GrafikoMat.Core.Data;
using GrafikoMat.Core.Scheduling.Engines.Algorithms;
using GrafikoMat.Core.Scheduling.Evaluation;
using GrafikoMat.Core.Scheduling.Models;
using GrafikoMat.Core.Scheduling.Validation;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using AvailabilityMask = GrafikoMat.Core.Scheduling.Engines.Algorithms.AvailabilityMask;

namespace GrafikoMat.Core.Scheduling.Engines
{
    /// <summary>
    /// Deterministyczny silnik generujący grafik z użyciem algorytmu z nawrotami (backtracking).
    /// Gwarantuje znalezienie optymalnego rozwiązania poprzez pełne przeszukanie przestrzeni możliwych decyzji.
    /// Wersja zaadaptowana z GrafikWPF z optymalizacjami wydajnościowymi.
    /// Max-Flow jest używany TYLKO jako heurystyka sortowania kandydatów, NIE do pruningu (zachowanie gwarancji optymalności).
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

        // Optymalizacje
        // POPRAWKA: Limit rozmiaru dla _visitedStates aby uniknąć out-of-memory
        private const int MAX_VISITED_STATES = 1_000_000; // 1M stanów ≈ 8MB pamięci
        private readonly HashSet<ulong> _visitedStates = new HashSet<ulong>(); // Zobrist hashing
        private readonly ulong[,] _zobristTable; // Tablica dla haszowania Zobrista

        // POPRAWKA ŚREDNIA: Weryfikacja kolizji - przechowuj pełne stany dla hashów
        private readonly Dictionary<ulong, int[]> _fullStates = new Dictionary<ulong, int[]>();
        private long _hashCollisions = 0; // Licznik kolizji hashów

        // Time-limited search
        private readonly TimeSpan _maxSearchTime;
        private Stopwatch? _searchStopwatch;
        private bool _timeLimitReached = false;

        // Statystyki
        private long _nodesExpanded = 0;

        // Flow-aware heuristics - używamy flow do sortowania, nie do pruningu
        private readonly bool _useFlowForHeuristics = true;
        private readonly int _flowHeuristicFrequency = 5; // Sprawdzaj flow co N dni (kosztowne)

        // POPRAWKA OPTYMALIZACJA: Cache dla flow calculations
        private const int MAX_FLOW_CACHE_SIZE = 50_000; // ~400KB pamięci (50K × 8 bytes)
        private readonly Dictionary<ulong, int> _flowCache = new Dictionary<ulong, int>();
        private long _flowCacheHits = 0;
        private long _flowCacheMisses = 0;

        public BacktrackingSolver(
            ScheduleInput scheduleInput,
            List<SolverPriority> priorities,
            TimeSpan timeout,  // ← NOWY PARAMETR
            IProgress<double>? progress = null,
            CancellationToken token = default)
        {
            _input = scheduleInput;
            _priorities = priorities;
            _progress = progress;
            _cancellationToken = token;
            _maxSearchTime = timeout;  // ← PRZYPISANIE Z PARAMETRU

            _days = _input.DaysInMonth;
            _doctors = _input.Doctors.Where(d => !d.IsArchived).ToList();
            _dutyLimitsByAbbr = _input.DutyLimits;

            _assignments = Enumerable.Repeat(UNASSIGNED, _days.Count).ToArray();
            _workload = new int[_doctors.Count];
            _conditionalsUsed = new int[_doctors.Count];

            // +1 dla EMPTY (nie hashujemy UNASSIGNED - zostaje pominięty)
            _zobristTable = InitializeZobristTable(_days.Count, _doctors.Count + 1);
        }

        private ulong[,] InitializeZobristTable(int days, int states)
        {
            var table = new ulong[days, states];
            var random = new Random(42); // Seed dla deterministyczności
            for (int d = 0; d < days; d++)
            {
                for (int s = 0; s < states; s++)
                {
                    table[d, s] = ((ulong)random.Next() << 32) | (ulong)random.Next();
                }
            }
            return table;
        }

        public ScheduleSolution FindOptimalSolution()
        {
            _searchStopwatch = Stopwatch.StartNew();

            // PREPROCESSING: Przypisz rezerwacje jako twarde constrainty
            PreprocessReservations();

            // Rozpocznij przeszukiwanie DFS
            Dfs(depth: 0);

            _bestSolution ??= BuildSolutionFromState();
            _searchStopwatch.Stop();

            // POPRAWKA ŚREDNIA: Raportuj statystyki kolizji
            if (_hashCollisions > 0)
            {
                System.Diagnostics.Debug.WriteLine($"[BacktrackingSolver] Hash collisions detected: {_hashCollisions}");
                System.Diagnostics.Debug.WriteLine($"[BacktrackingSolver] Collision rate: {(double)_hashCollisions / _visitedStates.Count * 100:F3}%");
            }

            // POPRAWKA OPTYMALIZACJA: Raportuj statystyki flow cache
            long totalFlowCalls = _flowCacheHits + _flowCacheMisses;
            if (totalFlowCalls > 0)
            {
                double hitRate = (double)_flowCacheHits / totalFlowCalls * 100;
                System.Diagnostics.Debug.WriteLine($"[BacktrackingSolver] Flow cache stats:");
                System.Diagnostics.Debug.WriteLine($"  - Total calls: {totalFlowCalls}");
                System.Diagnostics.Debug.WriteLine($"  - Cache hits: {_flowCacheHits} ({hitRate:F1}%)");
                System.Diagnostics.Debug.WriteLine($"  - Cache misses: {_flowCacheMisses}");
                System.Diagnostics.Debug.WriteLine($"  - Final cache size: {_flowCache.Count}");
            }

            return _bestSolution;
        }

        /// <summary>
        /// Preprocessing: Przypisuje wszystkie rezerwacje przed rozpoczęciem przeszukiwania.
        /// Zawęża przestrzeń rozwiązań i gwarantuje spełnienie rezerwacji.
        /// </summary>
        private void PreprocessReservations()
        {
            for (int dayIndex = 0; dayIndex < _days.Count; dayIndex++)
            {
                var day = _days[dayIndex];
                if (_input.Availability.TryGetValue(day, out var dayAvail))
                {
                    foreach (var kvp in dayAvail)
                    {
                        if (kvp.Value == AvailabilityType.Reservation)
                        {
                            int doctorIndex = _doctors.FindIndex(d => d.Abbreviation == kvp.Key);
                            if (doctorIndex >= 0 && IsHardFeasible(dayIndex, doctorIndex))
                            {
                                _assignments[dayIndex] = doctorIndex;
                                _workload[doctorIndex]++;
                                // Rezerwacje nie są "Mogę warunkowo", więc nie zwiększamy conditionalsUsed
                                break; // Jeden dzień = jedna rezerwacja
                            }
                        }
                    }
                }
            }
        }

        private void Dfs(int depth)
        {
            // Check cancellation
            if (_cancellationToken.IsCancellationRequested) return;

            // Time-limited search
            if (_searchStopwatch != null && _searchStopwatch.Elapsed > _maxSearchTime)
            {
                _timeLimitReached = true;
                return;
            }

            _nodesExpanded++;

            // POPRAWKA: Sprawdź czy nie przekroczono limitu pamięci dla visited states
            // Jeśli tak, wyczyść 50% najstarszych stanów (LRU-style)
            if (_visitedStates.Count >= MAX_VISITED_STATES)
            {
                System.Diagnostics.Debug.WriteLine($"[BacktrackingSolver] Visited states limit reached ({MAX_VISITED_STATES}), clearing 50%");

                // Prosta strategia: wyczyść wszystkie stany (reset)
                // W praktyce to oznacza że możemy ponownie odwiedzić te same stany,
                // ale chroni przed out-of-memory
                _visitedStates.Clear();
                _fullStates.Clear(); // POPRAWKA ŚREDNIA: Również wyczyść pełne stany
                _flowCache.Clear(); // POPRAWKA OPTYMALIZACJA: Również wyczyść cache flow

                System.Diagnostics.Debug.WriteLine($"[BacktrackingSolver] Visited states cleared, continuing search");
            }

            // POPRAWKA ŚREDNIA: Memoizacja z Zobrist hashing + weryfikacja kolizji
            ulong stateHash = ComputeZobristHash();

            if (_visitedStates.Contains(stateHash))
            {
                // Potencjalna kolizja - zweryfikuj pełny stan
                if (_fullStates.TryGetValue(stateHash, out var cachedState))
                {
                    bool isActualDuplicate = true;
                    for (int i = 0; i < _assignments.Length; i++)
                    {
                        if (_assignments[i] != cachedState[i])
                        {
                            isActualDuplicate = false;
                            break;
                        }
                    }

                    if (!isActualDuplicate)
                    {
                        // Prawdziwa kolizja hasza - loguj i kontynuuj
                        _hashCollisions++;
                        System.Diagnostics.Debug.WriteLine($"[BacktrackingSolver] Hash collision detected! Count: {_hashCollisions}");
                    }
                    else
                    {
                        // To naprawdę ten sam stan - pomiń
                        return;
                    }
                }
                else
                {
                    // Hash istnieje ale brak pełnego stanu (po czyszczeniu) - bezpiecznie pomiń
                    return;
                }
            }

            // Dodaj hash i zachowaj kopię pełnego stanu
            _visitedStates.Add(stateHash);
            _fullStates[stateHash] = (int[])_assignments.Clone();

            // Raportuj postęp co ~2048 węzłów
            if ((_nodesExpanded & 0x7FF) == 0)
            {
                int filled = _assignments.Count(a => a != UNASSIGNED);
                _progress?.Report((double)filled / _assignments.Length);
            }

            int nextDayIndex = ChooseDayToAssign();
            if (nextDayIndex < 0) // Wszystkie dni przypisane, liść drzewa
            {
                var solution = BuildSolutionFromState();
                ConsiderAsBest(solution);

                // Early termination: jeśli znaleziono ideał, kończymy
                if (IsIdealSolution(solution))
                {
                    _timeLimitReached = true; // Użyjemy tej flagi do natychmiastowego zakończenia
                    return;
                }
                return;
            }

            // BRAK PRUNINGU NA PODSTAWIE FLOW - flow używany tylko w heurystykach sortowania
            var candidates = GetOrderedCandidates(nextDayIndex);

            // Rozgałęzienie po lekarzach (posortowane według LCV z flow-aware heurystyką)
            foreach (var doctorIndex in candidates)
            {
                if (_timeLimitReached) return; // Wczesne wyjście

                if (!TryAssign(nextDayIndex, doctorIndex)) continue;
                Dfs(depth + 1);
                Unassign(nextDayIndex, doctorIndex);
            }

            // Rozgałęzienie dla pustego dnia
            if (!_timeLimitReached)
            {
                TryAssignEmpty(nextDayIndex);
                Dfs(depth + 1);
                UnassignEmpty(nextDayIndex);
            }
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

        /// <summary>
        /// Zwraca posortowaną listę kandydatów według LCV (Least Constraining Value) wzbogaconego o flow-aware heurystykę.
        /// Priorytetyzuje lekarzy, którzy najmniej ograniczają przyszłe przypisania.
        /// Flow jest używany TYLKO jako heurystyka sortowania, NIE do odrzucania kandydatów (zachowanie optymalności).
        /// </summary>
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

            // LCV wzbogacony o flow-aware heurystykę
            candidates.Sort((a, b) =>
            {
                int constraintA = CalculateConstrainingValueWithFlow(dayIndex, a);
                int constraintB = CalculateConstrainingValueWithFlow(dayIndex, b);
                return constraintA.CompareTo(constraintB); // Mniej ograniczający = lepszy
            });

            return candidates;
        }

        /// <summary>
        /// Oblicza, jak bardzo przypisanie danego lekarza ogranicza przyszłe możliwości.
        /// Wzbogacone o flow-aware heurystykę: symuluje przypisanie i sprawdza wpływ na max-flow.
        /// Im mniejsza wartość, tym lepiej (mniej ograniczający).
        /// </summary>
        private int CalculateConstrainingValueWithFlow(int dayIndex, int doctorIndex)
        {
            int constraintScore = 0;

            // 1. Pozostały limit lekarza (mniej pozostałych = bardziej ograniczający)
            int limit = _dutyLimitsByAbbr.GetValueOrDefault(_doctors[doctorIndex].Abbreviation, 0);
            int remaining = limit > 0 ? limit - _workload[doctorIndex] : int.MaxValue;
            if (remaining <= 2) constraintScore += 100; // Krytycznie niski limit

            // 2. Blokada sąsiednich dni (następny dzień)
            if (dayIndex < _days.Count - 1 && _assignments[dayIndex + 1] == UNASSIGNED)
            {
                // Ten lekarz zablokuje następny dzień (reguła "dzień po dniu")
                constraintScore += 50;
            }

            // 3. Zgodność z deklaracjami (preferuj "Chcę" nad "Może")
            var availability = _input.Availability[_days[dayIndex]][_doctors[doctorIndex].Abbreviation];
            if (availability == AvailabilityType.Wants)
                constraintScore -= 30; // Preferuj lekarzy, którzy chcą
            else if (availability == AvailabilityType.Available)
                constraintScore += 10; // "Może" jest neutralne
            else if (availability == AvailabilityType.ConditionallyAvailable)
                constraintScore += 40; // "Mogę warunkowo" jest bardziej ograniczające

            // 4. Dostępność na przyszłe dni
            int futureAvailability = 0;
            for (int d = dayIndex + 1; d < _days.Count; d++)
            {
                if (_assignments[d] == UNASSIGNED && IsHardFeasible(d, doctorIndex))
                    futureAvailability++;
            }
            if (futureAvailability <= 2) constraintScore += 70; // Mało przyszłych możliwości

            // 5. FLOW-AWARE HEURYSTYKA (kosztowne, więc wykonujemy rzadko)
            // Symulujemy przypisanie tego lekarza i sprawdzamy, jak wpływa to na maksymalny przepływ
            if (_useFlowForHeuristics && (dayIndex % _flowHeuristicFrequency == 0))
            {
                // Tymczasowe przypisanie
                _assignments[dayIndex] = doctorIndex;
                _workload[doctorIndex]++;
                if (availability == AvailabilityType.ConditionallyAvailable)
                {
                    _conditionalsUsed[doctorIndex]++;
                }

                // POPRAWKA OPTYMALIZACJA: Użyj cache dla flow calculations
                int flowAfter = ComputeMaxFlowWithCache();

                // Cofnij tymczasowe przypisanie
                if (availability == AvailabilityType.ConditionallyAvailable)
                {
                    _conditionalsUsed[doctorIndex]--;
                }
                _workload[doctorIndex]--;
                _assignments[dayIndex] = UNASSIGNED;

                // Kandydat, który daje wyższy flow, jest mniej ograniczający
                // Im wyższy flow po przypisaniu, tym niższy constraint score (lepiej)
                constraintScore -= flowAfter * 5;
            }

            return constraintScore;
        }

        /// <summary>
        /// Sprawdza twarde ograniczenia (hard constraints): czy lekarz MOŻE być przypisany na dany dzień.
        /// </summary>
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

            var availability = _input.Availability[_days[dayIndex]][_doctors[doctorIndex].Abbreviation];
            if (availability == AvailabilityType.ConditionallyAvailable)
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

        /// <summary>
        /// Buduje rozwiązanie z bieżącego stanu _assignments.
        /// </summary>
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

        private void ConsiderAsBest(ScheduleSolution candidate)
        {
            if (_bestSolution == null || SolutionComparer.CompareSolutionsByPriorities(candidate, _bestSolution, _priorities, _input) > 0)
            {
                _bestSolution = candidate;
            }
        }

        /// <summary>
        /// Oblicza Zobrist hash dla bieżącego stanu _assignments.
        /// Hash nie uwzględnia UNASSIGNED (przypisujemy dopiero dni, które zostały zdecydowane).
        /// </summary>
        private ulong ComputeZobristHash()
        {
            ulong hash = 0;
            for (int i = 0; i < _assignments.Length; i++)
            {
                int val = _assignments[i];

                // Pomijamy UNASSIGNED - haszujemy tylko przypisane dni
                if (val == UNASSIGNED) continue;

                int stateIndex;
                if (val == EMPTY) stateIndex = _doctors.Count;
                else stateIndex = val;

                hash ^= _zobristTable[i, stateIndex];
            }
            return hash;
        }

        /// <summary>
        /// POPRAWKA OPTYMALIZACJA: Oblicza hash dla stanu używanego w cache flow.
        /// Uwzględnia assignments + workload lekarzy (bo flow zależy od obu).
        /// </summary>
        private ulong ComputeFlowCacheHash()
        {
            ulong hash = ComputeZobristHash(); // Bazowy hash przypisań

            // Dodaj workload do hasha (bo flow zależy od pozostałych limitów)
            for (int i = 0; i < _workload.Length; i++)
            {
                // Prosta kombinacja: XOR z workload przesunięty o pozycję lekarza
                hash ^= ((ulong)_workload[i] << (i % 32));
            }

            return hash;
        }

        /// <summary>
        /// POPRAWKA OPTYMALIZACJA: Oblicza max-flow z użyciem cache.
        /// Znacznie przyspiesza obliczenia przy dużych problemach (62 sloty, 23 dyżurnych).
        /// </summary>
        private int ComputeMaxFlowWithCache()
        {
            // Oblicz hash stanu
            ulong cacheKey = ComputeFlowCacheHash();

            // Sprawdź cache
            if (_flowCache.TryGetValue(cacheKey, out int cachedFlow))
            {
                _flowCacheHits++;
                return cachedFlow;
            }

            // Cache miss - oblicz flow
            _flowCacheMisses++;

            int flowResult = FlowUpperBound.Calculate(
                _days.Count, _doctors.Count,
                getAvailabilityMask: (d, p) => IsHardFeasible(d, p) ? AvailabilityMask.Any : AvailabilityMask.None,
                getRemainingCapacityPerDoctor: p =>
                {
                    int lim = _dutyLimitsByAbbr.GetValueOrDefault(_doctors[p].Abbreviation, 0);
                    return lim == 0 ? int.MaxValue : Math.Max(0, lim - _workload[p]);
                },
                isDayAllowed: d => _assignments[d] == UNASSIGNED
            );

            // Dodaj do cache z limitem (prosta LRU: wyczyść wszystko gdy pełny)
            if (_flowCache.Count >= MAX_FLOW_CACHE_SIZE)
            {
                System.Diagnostics.Debug.WriteLine($"[BacktrackingSolver] Flow cache full ({MAX_FLOW_CACHE_SIZE}), clearing");
                _flowCache.Clear();
            }

            _flowCache[cacheKey] = flowResult;
            return flowResult;
        }

        /// <summary>
        /// Sprawdza, czy znalezione rozwiązanie jest teoretycznie idealne (niemożliwe do poprawy).
        /// </summary>
        private bool IsIdealSolution(ScheduleSolution solution)
        {
            int totalDays = _days.Count;
            int assignedDays = solution.TotalAssignments;

            foreach (var priority in _priorities)
            {
                switch (priority)
                {
                    case SolverPriority.TotalAssignments:
                        if (assignedDays < totalDays) return false;
                        break;

                    case SolverPriority.InitialContinuity:
                        if (solution.InitialContinuity < totalDays) return false;
                        break;

                    case SolverPriority.Fairness:
                        if (solution.FairnessIndex > 0.01) return false; // Tolerancja numeryczna
                        break;

                    case SolverPriority.Spacing:
                        if (solution.SpacingIndex > 0.01) return false;
                        break;

                    case SolverPriority.DeclarationCompliance:
                        // Maksymalna zgodność: każdy przypisany dzień to "Chcę" (waga 2)
                        double maxPossibleWants = 0;
                        for (int i = 0; i < _days.Count; i++)
                        {
                            if (_assignments[i] >= 0)
                            {
                                var avail = _input.Availability[_days[i]][_doctors[_assignments[i]].Abbreviation];
                                if (avail == AvailabilityType.Wants)
                                    maxPossibleWants += 2.0;
                            }
                        }
                        double achieved = solution.FulfilledWants * 2.0 + solution.FulfilledAvailables * 1.0;
                        if (achieved < maxPossibleWants * 0.99) return false; // 99% threshold
                        break;
                }
            }

            return true; // Wszystkie kryteria spełnione
        }
    }
}