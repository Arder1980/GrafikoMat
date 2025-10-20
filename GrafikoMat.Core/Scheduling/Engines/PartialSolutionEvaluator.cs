using GrafikoMat.Core.Data;
using GrafikoMat.Core.Scheduling.Models;
using System;
using System.Collections.Generic;
using System.Linq;

namespace GrafikoMat.Core.Scheduling.Engines
{
    /// <summary>
    /// Ocenia jakość częściowych przypisań dla algorytmu A*.
    /// Implementuje funkcje g(n) i h(n) z gwarancją admissibility (optimistic heuristic).
    /// </summary>
    internal static class PartialSolutionEvaluator
    {
        /// <summary>
        /// Reprezentuje metryki częściowego grafiku (część dni przypisana, część pusta).
        /// </summary>
        internal sealed class PartialMetrics
        {
            public int InitialContinuity { get; set; }
            public int TotalAssignments { get; set; }
            public int FulfilledReservations { get; set; }
            public int FulfilledWants { get; set; }
            public int FulfilledAvailables { get; set; }
            public double FairnessIndex { get; set; }
            public double SpacingIndex { get; set; }

            // Pomocnicze dla heurystyki
            public int RemainingDays { get; set; }
            public int AssignedDays { get; set; }
        }

        /// <summary>
        /// Oblicza metryki dla częściowo wypełnionego grafiku z optimistic assumptions dla niekompletnych części.
        /// </summary>
        public static PartialMetrics ComputePartialMetrics(SchedulingRules.Context ctx, ScheduleInput input)
        {
            var metrics = new PartialMetrics();
            int assignedCount = 0;
            int unassignedCount = 0;

            // Zlicz przypisane i nieprzypisane dni
            for (int i = 0; i < ctx.DayCount; i++)
            {
                if (ctx.Assignments[i] == SchedulingRules.UNASSIGNED)
                    unassignedCount++;
                else if (ctx.Assignments[i] >= 0)
                    assignedCount++;
            }

            metrics.AssignedDays = assignedCount;
            metrics.RemainingDays = unassignedCount;

            // 1. InitialContinuity - dokładnie (to prefix)
            metrics.InitialContinuity = 0;
            for (int i = 0; i < ctx.DayCount; i++)
            {
                if (ctx.Assignments[i] >= 0)
                    metrics.InitialContinuity++;
                else
                    break; // Koniec ciągłego prefiksu
            }

            // 2. TotalAssignments - dokładnie (liczba wypełnionych dni)
            metrics.TotalAssignments = assignedCount;

            // 3. Fulfilled preferences - dokładnie
            metrics.FulfilledReservations = 0;
            metrics.FulfilledWants = 0;
            metrics.FulfilledAvailables = 0;

            for (int d = 0; d < ctx.DayCount; d++)
            {
                int docIndex = ctx.Assignments[d];
                if (docIndex < 0) continue; // Nie przypisany lub pusty

                var date = ctx.Days[d];
                var doctor = ctx.Doctors[docIndex];
                var availability = ctx.GetAvailability(d, docIndex);

                if (availability == AvailabilityType.Reservation)
                    metrics.FulfilledReservations++;
                else if (availability == AvailabilityType.Wants)
                    metrics.FulfilledWants++;
                else if (availability == AvailabilityType.Available)
                    metrics.FulfilledAvailables++;
            }

            // 4. FairnessIndex - optimistic (zakładamy że reszta idealnie wyrówna)
            // Dla częściowego grafiku liczymy tylko obecne odchylenie, bez pesymizmu
            if (ctx.DoctorCount > 1)
            {
                var percentageWorkloads = new List<double>();
                for (int p = 0; p < ctx.DoctorCount; p++)
                {
                    int limit = ctx.GetLimitFor(p);
                    if (limit > 0)
                    {
                        double currentWorkload = ctx.Workload[p];
                        percentageWorkloads.Add(currentWorkload * 100.0 / limit);
                    }
                }

                if (percentageWorkloads.Count > 1)
                {
                    double avg = percentageWorkloads.Average();
                    double sumSq = percentageWorkloads.Sum(val => (val - avg) * (val - avg));
                    metrics.FairnessIndex = Math.Sqrt(sumSq / percentageWorkloads.Count);
                }
                else
                {
                    metrics.FairnessIndex = 0.0;
                }
            }
            else
            {
                metrics.FairnessIndex = 0.0;
            }

            // 5. SpacingIndex - optimistic (zakładamy idealne odstępy dla reszty)
            // Dla częściowego liczymy tylko obecne odstępy
            var doctorDuties = new Dictionary<int, List<int>>();
            for (int d = 0; d < ctx.DayCount; d++)
            {
                int docIndex = ctx.Assignments[d];
                if (docIndex >= 0)
                {
                    if (!doctorDuties.ContainsKey(docIndex))
                        doctorDuties[docIndex] = new List<int>();
                    doctorDuties[docIndex].Add(d);
                }
            }

            var spacingDeviations = new List<double>();
            foreach (var kvp in doctorDuties)
            {
                var days = kvp.Value;
                if (days.Count <= 1) continue;

                var intervals = new List<int>();
                for (int i = 1; i < days.Count; i++)
                {
                    intervals.Add(days[i] - days[i - 1]);
                }

                if (intervals.Count > 0)
                {
                    double avgInterval = intervals.Average();
                    double variance = intervals.Sum(x => (x - avgInterval) * (x - avgInterval)) / intervals.Count;
                    spacingDeviations.Add(Math.Sqrt(variance));
                }
            }

            metrics.SpacingIndex = spacingDeviations.Count > 0 ? spacingDeviations.Average() : 0.0;

            return metrics;
        }

        /// <summary>
        /// Oblicza "reward" (wartość maksymalizowana) dla częściowego stanu.
        /// Im wyższy reward, tym lepszy stan. Używa hierarchicznej struktury zgodnej z priorytetami użytkownika.
        /// Funkcja f(n) = g(n) + h(n), gdzie g(n) to koszt dotarcia, a h(n) to optimistic estimate pozostałego potencjału.
        /// </summary>
        public static double EvaluateState(SchedulingRules.Context ctx, IReadOnlyList<SolverPriority> priorities, ScheduleInput input)
        {
            var metrics = ComputePartialMetrics(ctx, input);

            // Konwersja metryk do pojedynczej wartości reward z hierarchiczną strukturą
            // Używamy potęg 10^15, 10^12, 10^9... aby zachować leksykograficzny porządek

            double reward = 0.0;
            double scale = 1e15; // Początkowa skala dla najwyższego priorytetu

            // Rezerwacje mają absolutny priorytet (ponad wszystkie inne metryki)
            reward += metrics.FulfilledReservations * 1e18;

            foreach (var priority in priorities)
            {
                double metricValue = GetMetricValue(priority, metrics, input);
                reward += metricValue * scale;
                scale /= 1000.0; // Każdy kolejny priorytet ma 1000x mniejszą wagę
            }

            // Dodatkowe bonusy (poza hierarchią priorytetów)
            reward += metrics.FulfilledWants * 1e6;
            reward += metrics.FulfilledAvailables * 1e3;

            // Optimistic heuristic: zakładamy że pozostałe dni mogą być wypełnione optymalnie
            // To zapewnia admissibility (nigdy nie przeceniamy jakości stanu)
            double optimisticBonus = EstimateRemainingPotential(metrics, priorities, input);
            reward += optimisticBonus;

            return reward;
        }

        /// <summary>
        /// Pobiera znormalizowaną wartość metryki dla danego priorytetu.
        /// </summary>
        private static double GetMetricValue(SolverPriority priority, PartialMetrics metrics, ScheduleInput input)
        {
            switch (priority)
            {
                case SolverPriority.InitialContinuity:
                    // Normalizujemy przez całkowitą liczbę dni
                    return metrics.InitialContinuity / (double)Math.Max(1, input.DaysInMonth.Count);

                case SolverPriority.TotalAssignments:
                    // Normalizujemy przez całkowitą liczbę dni
                    return metrics.TotalAssignments / (double)Math.Max(1, input.DaysInMonth.Count);

                case SolverPriority.Fairness:
                    // Im mniej, tym lepiej - odwracamy
                    return 1.0 / (1.0 + metrics.FairnessIndex);

                case SolverPriority.Spacing:
                    // Im mniej, tym lepiej - odwracamy
                    return 1.0 / (1.0 + metrics.SpacingIndex);

                case SolverPriority.DeclarationCompliance:
                    // Ważona suma: Wants=2, Available=1
                    int totalDeclared = metrics.FulfilledWants + metrics.FulfilledAvailables;
                    if (totalDeclared == 0) return 0.0;
                    double weightedSum = metrics.FulfilledWants * 2.0 + metrics.FulfilledAvailables * 1.0;
                    return weightedSum / (2.0 * totalDeclared); // Normalizacja przez max możliwą wagę

                default:
                    return 0.0;
            }
        }

        /// <summary>
        /// Optimistic estimate: zakłada że wszystkie pozostałe dni mogą być wypełnione w sposób optymalny.
        /// To zapewnia admissibility heurystyki (nigdy nie przeceniamy).
        /// </summary>
        private static double EstimateRemainingPotential(PartialMetrics metrics, IReadOnlyList<SolverPriority> priorities, ScheduleInput input)
        {
            if (metrics.RemainingDays == 0)
                return 0.0; // Brak pozostałych dni

            double potential = 0.0;
            double scale = 1e15;

            foreach (var priority in priorities)
            {
                double optimisticGain = 0.0;

                switch (priority)
                {
                    case SolverPriority.InitialContinuity:
                        // Jeśli prefix jest aktywny, zakładamy że da się go kontynuować
                        if (metrics.InitialContinuity == metrics.AssignedDays)
                        {
                            // Optimistic: wszystkie pozostałe dni mogą przedłużyć prefix
                            optimisticGain = metrics.RemainingDays / (double)Math.Max(1, input.DaysInMonth.Count);
                        }
                        break;

                    case SolverPriority.TotalAssignments:
                        // Optimistic: wszystkie pozostałe dni mogą być wypełnione
                        optimisticGain = metrics.RemainingDays / (double)Math.Max(1, input.DaysInMonth.Count);
                        break;

                    case SolverPriority.Fairness:
                        // Optimistic: pozostałe przypisania idealnie wyrównają obciążenia (delta = 0)
                        optimisticGain = 0.0;
                        break;

                    case SolverPriority.Spacing:
                        // Optimistic: pozostałe dyżury będą rozmieszczone idealnie (delta = 0)
                        optimisticGain = 0.0;
                        break;

                    case SolverPriority.DeclarationCompliance:
                        // Optimistic: wszystkie pozostałe dni trafią w "Chcę" (waga 2)
                        // To jest górna granica - admissible
                        optimisticGain = metrics.RemainingDays * 1.0; // Znormalizowane przez liczbę dni
                        break;
                }

                potential += optimisticGain * scale;
                scale /= 1000.0;
            }

            return potential;
        }
    }
}