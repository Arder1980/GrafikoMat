using GrafikoMat.Core.Data;
using GrafikoMat.Core.Scheduling.Models;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace GrafikoMat.Core.Scheduling.Evaluation
{
    /// <summary>
    /// Wspólny (dla wszystkich silników) komparator rozwiązań zgodny z ustawioną kolejnością priorytetów.
    /// Wersja zaadaptowana z GrafikWPF.
    /// </summary>
    public static class SolutionComparer
    {
        /// <summary>
        /// Porównuje dwa rozwiązania zgodnie z kolejnością priorytetów.
        /// Zwraca >0 jeśli A lepsze, <0 jeśli B lepsze, 0 przy absolutnym remisie.
        /// </summary>
        public static int CompareSolutionsByPriorities(
            ScheduleSolution solA,
            ScheduleSolution solB,
            IReadOnlyList<SolverPriority> priorities,
            ScheduleInput input)
        {
            var prioList = priorities as List<SolverPriority> ?? priorities.ToList();

            // 1) Porównanie wektorów int wg priorytetów (główna metoda oceny)
            var vA = EvaluationAndScoringService.ToIntVector(solA, prioList);
            var vB = EvaluationAndScoringService.ToIntVector(solB, prioList);

            int c = LexicoCompare(vA, vB);
            if (c != 0) return c;

            // 2) Jeśli wektory są identyczne, porównaj surowe metryki jako tie-breaker
            var rawA = BuildRawVector(solA, priorities, input);
            var rawB = BuildRawVector(solB, priorities, input);

            c = RawCompareByPriorities(rawA, rawB, priorities);
            if (c != 0) return c;

            // 3) Jeśli nadal remis, użyj deterministycznego "odcisku palca" grafiku
            string fpA = Fingerprint(solA);
            string fpB = Fingerprint(solB);
            return string.Compare(fpA, fpB, StringComparison.Ordinal);
        }

        private static int LexicoCompare(long[] a, long[] b)
        {
            int n = Math.Min(a.Length, b.Length);
            for (int i = 0; i < n; i++)
            {
                if (a[i] > b[i]) return 1;
                if (a[i] < b[i]) return -1;
            }
            return a.Length.CompareTo(b.Length);
        }

        /// <summary>
        /// Buduje surowy wektor metryk (jako double) tylko dla priorytetów podanych przez użytkownika.
        /// </summary>
        private static double[] BuildRawVector(
            ScheduleSolution solution,
            IReadOnlyList<SolverPriority> priorities,
            ScheduleInput input)
        {
            var list = new List<double>(priorities.Count);
            foreach (var pr in priorities)
            {
                switch (pr)
                {
                    case SolverPriority.TotalAssignments:
                        list.Add(solution.TotalAssignments);
                        break;
                    case SolverPriority.InitialContinuity:
                        list.Add(solution.InitialContinuity);
                        break;
                    case SolverPriority.Fairness:
                        list.Add(solution.FairnessIndex);
                        break;
                    case SolverPriority.Spacing:
                        list.Add(solution.SpacingIndex);
                        break;
                    case SolverPriority.DeclarationCompliance:
                        list.Add(ComputeDeclarationComplianceRaw(solution, input));
                        break;
                    default:
                        list.Add(0);
                        break;
                }
            }
            return list.ToArray();
        }

        private static int RawCompareByPriorities(
            double[] a, double[] b, IReadOnlyList<SolverPriority> priorities)
        {
            for (int i = 0; i < priorities.Count; i++)
            {
                var pr = priorities[i];
                double va = a[i];
                double vb = b[i];
                int cmp;

                switch (pr)
                {
                    // WIĘCEJ = lepiej
                    case SolverPriority.TotalAssignments:
                    case SolverPriority.InitialContinuity:
                    case SolverPriority.DeclarationCompliance:
                        cmp = va.CompareTo(vb);
                        if (cmp != 0) return cmp;
                        break;

                    // MNIEJ = lepiej
                    case SolverPriority.Fairness:
                    case SolverPriority.Spacing:
                        cmp = (-va).CompareTo(-vb); // Odwracamy porównanie
                        if (cmp != 0) return cmp;
                        break;
                }
            }
            return 0;
        }

        /// <summary>
        /// Surowa "zgodność ważności deklaracji": ważona suma trafień CHC(2), MOG(1), WAR(0),
        /// przeskalowana do [0..1] przez maksymalną możliwą wagę (2 * liczba przydzielonych dni).
        /// </summary>
        private static double ComputeDeclarationComplianceRaw(ScheduleSolution solution, ScheduleInput input)
        {
            if (solution?.Assignments == null || solution.Assignments.Count == 0) return 0.0;

            double sum = 0.0;
            int count = 0;

            foreach (var kv in solution.Assignments)
            {
                var date = kv.Key;
                var doctor = kv.Value;
                if (doctor == null) continue;

                if (!input.Availability.TryGetValue(date, out var dayMap) || dayMap == null) continue;
                if (!dayMap.TryGetValue(doctor.Abbreviation, out var availabilityType)) continue;

                int weight = Declarations.GetPreferenceWeight(availabilityType);
                if (weight >= 0) // liczymy tylko pozytywne preferencje
                {
                    sum += weight;
                    count++;
                }
            }

            if (count == 0) return 0.0;

            // Maksymalna możliwa waga to 2 (za 'Chce'), więc normalizujemy przez 2 * liczba dni.
            double maxPossibleScore = 2.0 * count;
            if (maxPossibleScore == 0) return 0.0;

            return sum / maxPossibleScore;
        }

        private static string Fingerprint(ScheduleSolution solution)
        {
            var parts = new List<string>(solution.Assignments.Count);
            foreach (var kv in solution.Assignments.OrderBy(k => k.Key))
            {
                string day = kv.Key.ToString("yyyyMMdd", CultureInfo.InvariantCulture);
                string doctorAbbr = kv.Value?.Abbreviation ?? "---";
                parts.Add($"{day}:{doctorAbbr}");
            }
            return string.Join("|", parts);
        }
    }
}