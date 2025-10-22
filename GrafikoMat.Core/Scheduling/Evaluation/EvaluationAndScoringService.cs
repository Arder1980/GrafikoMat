using GrafikoMat.Core.Data;
using GrafikoMat.Core.Scheduling.Models;
using System;
using System.Collections.Generic;
using System.Linq;

namespace GrafikoMat.Core.Scheduling.Evaluation
{
    /// <summary>
    /// Zawiera statyczne metody do obliczania metryk jakościowych i oceny (score) gotowego grafiku.
    /// Wersja zaadaptowana z GrafikWPF.
    /// </summary>
    public static class EvaluationAndScoringService
    {
        // Wagi wykładnicze dla pozycji w hierarchii priorytetów
        // Pierwsza pozycja (najważniejszy priorytet): 1e15, druga: 1e12, trzecia: 1e9, itd.
        private static readonly double[] PositionWeights = new[]
        {
            1_000_000_000_000_000.0,  // Pozycja 0 (najważniejszy priorytet)
            1_000_000_000_000.0,      // Pozycja 1
            1_000_000_000.0,          // Pozycja 2
            1_000_000.0,              // Pozycja 3
            1_000.0                   // Pozycja 4
        };

        // Dodatkowe gratyfikatory (poza priorytetami)
        private const double RESERVATION_WEIGHT = 100_000_000_000_000_000.0; // Najwyższa możliwa waga
        private const double WANTS_WEIGHT = 10;
        private const double AVAILABLE_WEIGHT = 1;

        /// <summary>
        /// Oblicza agregatową ocenę (score) rozwiązania, zgodnie z zadaną kolejnością priorytetów.
        /// Wyższa wartość oznacza lepszy grafik.
        /// UWAGA: Wagi są teraz wykładnicze i zależne od POZYCJI w liście priorytetów,
        /// co zapewnia spójność z leksykograficznym porównaniem w SolutionComparer.
        /// </summary>
        public static double CalculateScore(ScheduleSolution solution, List<SolverPriority> priorities, ScheduleInput scheduleInput)
        {
            if (solution == null) return double.MinValue;

            double totalDays = scheduleInput.DaysInMonth.Count;
            if (totalDays == 0) return 0;

            double normContinuity = solution.InitialContinuity / totalDays;
            double normCoverage = solution.TotalAssignments / totalDays;

            // Uwaga: dla wskaźników „im mniej, tym lepiej” normalizujemy tak, by 1 = idealnie, a 0 = bardzo źle.
            double normFairness = 1.0 / (1.0 + solution.FairnessIndex);
            double normSpacing = 1.0 / (1.0 + solution.SpacingIndex);

            double score = 0;

            // Rezerwacje traktujemy jako twardy bonus o ogromnej wadze
            score += solution.FulfilledReservations * RESERVATION_WEIGHT;

            var normalizedValues = new Dictionary<SolverPriority, double>
            {
                { SolverPriority.InitialContinuity, normContinuity },
                { SolverPriority.TotalAssignments,  normCoverage   },
                { SolverPriority.Fairness,          normFairness   },
                { SolverPriority.Spacing,           normSpacing    }
            };

            foreach (var priority in priorities)
            {
                var index = priorities.IndexOf(priority);
                if (normalizedValues.TryGetValue(priority, out double normalizedValue))
                {
                    double weight = index < PositionWeights.Length ? PositionWeights[index] : 1.0;
                    score += normalizedValue * weight;
                }
            }

            // Miękkie preferencje — drobne punkty „za chęci”
            score += solution.FulfilledWants * WANTS_WEIGHT;
            score += solution.FulfilledAvailables * AVAILABLE_WEIGHT;

            return score;
        }

        /// <summary>
        /// Konwertuje metryki rozwiązania na wektor liczb całkowitych w celu porównania leksykograficznego.
        /// Służy do deterministycznego decydowania, które z dwóch rozwiązań jest lepsze.
        /// </summary>
        public static long[] ToIntVector(ScheduleSolution solution, List<SolverPriority> priorities)
        {
            var vector = new long[priorities.Count + 3]; // 3 dodatkowe metryki (Rezerwacje, Chce, Może)
            int i = 0;

            var metricsMap = new Dictionary<SolverPriority, long>
            {
                { SolverPriority.TotalAssignments, solution.TotalAssignments },
                { SolverPriority.InitialContinuity, solution.InitialContinuity },

                // Dla „im mniej, tym lepiej” bierzemy ujemne wartości, aby mniejsza (lepsza) wartość była większa w porządku leksykograficznym.
                { SolverPriority.Fairness,  -(long)Math.Round(1_000_000.0 * solution.FairnessIndex) },
                { SolverPriority.Spacing,   -(long)Math.Round(1_000_000.0 * solution.SpacingIndex)  },
            };

            foreach (var p in priorities)
                vector[i++] = metricsMap.GetValueOrDefault(p, 0);

            vector[i++] = solution.FulfilledReservations;
            vector[i++] = solution.FulfilledWants;
            vector[i++] = solution.FulfilledAvailables;

            return vector;
        }

        /// <summary>
        /// Główna metoda obliczająca wszystkie metryki jakości na podstawie surowych przypisań.
        /// </summary>
        public static ScheduleSolution CalculateMetrics(
            IReadOnlyDictionary<DateTime, DoctorProfile?> assignments,
            IReadOnlyDictionary<string, int> workload,
            ScheduleInput scheduleInput)
        {
            var days = scheduleInput.DaysInMonth;

            // Obliczanie najdłuższego prefiksu ciągłej obsady
            int initialContinuity = 0;
            foreach (var key in days.OrderBy(d => d))
            {
                if (assignments.TryGetValue(key, out var doctor) && doctor != null)
                    initialContinuity++;
                else
                    break;
            }

            // Zliczanie zrealizowanych preferencji
            int fulfilledReservations = 0;
            int fulfilledWants = 0;
            int fulfilledAvailables = 0;

            foreach (var entry in assignments.Where(x => x.Value != null))
            {
                var type = scheduleInput.Availability[entry.Key][entry.Value!.Abbreviation];
                if (type == AvailabilityType.Reservation) fulfilledReservations++;
                else if (type == AvailabilityType.Wants) fulfilledWants++;
                else if (type == AvailabilityType.Available) fulfilledAvailables++;
            }

            // Obliczanie wskaźnika sprawiedliwości (odchylenie std. obciążeń procentowych)
            var percentageWorkloads = new List<double>();
            foreach (var doctor in scheduleInput.Doctors.Where(l => !l.IsArchived))
            {
                int limit = scheduleInput.DutyLimits.GetValueOrDefault(doctor.Abbreviation, 0);
                if (limit > 0)
                {
                    double assignedDuties = workload.GetValueOrDefault(doctor.Abbreviation, 0);
                    percentageWorkloads.Add(assignedDuties * 100.0 / limit);
                }
            }

            double fairnessIndex = 0.0;
            if (percentageWorkloads.Count > 1)
            {
                double avg = percentageWorkloads.Average();
                double sumSq = percentageWorkloads.Sum(val => (val - avg) * (val - avg));
                fairnessIndex = Math.Sqrt(sumSq / percentageWorkloads.Count);
            }

            // Obliczanie wskaźnika równomierności (średnie odchylenie std. odstępów między dyżurami)
            double spacingIndex = 0.0;
            var spacingDeviations = new List<double>();

            foreach (var doctor in scheduleInput.Doctors.Where(l => !l.IsArchived))
            {
                var doctorDuties = assignments.Where(kvp => kvp.Value?.Id == doctor.Id)
                                  .Select(kvp => kvp.Key)
                                  .OrderBy(d => d)
                                  .ToList();

                if (doctorDuties.Count > 2)
                {
                    var gaps = new List<double>();
                    for (int i = 0; i < doctorDuties.Count - 1; i++)
                        gaps.Add((doctorDuties[i + 1] - doctorDuties[i]).TotalDays);

                    if (gaps.Any())
                    {
                        double avgGap = gaps.Average();
                        double sumSqG = gaps.Sum(val => (val - avgGap) * (val - avgGap));
                        spacingDeviations.Add(Math.Sqrt(sumSqG / gaps.Count));
                    }
                }
            }
            if (spacingDeviations.Any())
                spacingIndex = spacingDeviations.Average();

            return new ScheduleSolution
            {
                Assignments = new Dictionary<DateTime, DoctorProfile?>(assignments),
                InitialContinuity = initialContinuity,
                FulfilledReservations = fulfilledReservations,
                FulfilledWants = fulfilledWants,
                FulfilledAvailables = fulfilledAvailables,
                FinalWorkload = new Dictionary<string, int>(workload),
                FairnessIndex = fairnessIndex,
                SpacingIndex = spacingIndex
            };
        }
    }
}