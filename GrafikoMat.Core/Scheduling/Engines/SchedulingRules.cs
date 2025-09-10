using GrafikoMat.Core.Data;
using GrafikoMat.Core.Scheduling.Evaluation;
using GrafikoMat.Core.Scheduling.Models;
using System;
using System.Collections.Generic;
using System.Linq;

namespace GrafikoMat.Core.Scheduling.Engines
{
    /// <summary>
    /// Zawiera zaawansowane reguły i heurystyki do oceny i porządkowania kandydatów
    /// na potrzeby zaawansowanych silników, takich jak A*.
    /// Wersja zaadaptowana z GrafikWPF.
    /// </summary>
    internal static class SchedulingRules
    {
        public const int UNASSIGNED = int.MinValue;

        /// <summary>
        /// Kontekst przechowujący pełny, bieżący stan przeszukiwania dla solvera.
        /// </summary>
        public sealed class Context
        {
            public IReadOnlyList<DateTime> Days { get; }
            public IReadOnlyList<DoctorProfile> Doctors { get; }
            public IReadOnlyDictionary<DateTime, Dictionary<string, AvailabilityType>> Availability { get; }
            public IReadOnlyDictionary<string, int> DutyLimitsByAbbr { get; }
            public IReadOnlyList<SolverPriority> Priorities { get; }

            public int[] Assignments;
            public int[] Workload;
            public int[] ConditionalsUsed;
            public bool IsPrefixActive;

            public readonly int DayCount, DoctorCount;
            public readonly Dictionary<string, int> DoctorIndexByAbbr;

            public Context(
                IReadOnlyList<DateTime> days,
                IReadOnlyList<DoctorProfile> doctors,
                IReadOnlyDictionary<DateTime, Dictionary<string, AvailabilityType>> availability,
                IReadOnlyDictionary<string, int> limits,
                IReadOnlyList<SolverPriority> priorities,
                int[] assignments,
                int[] workload,
                int[] conditionalsUsed,
                bool isPrefixActive)
            {
                Days = days;
                Doctors = doctors;
                Availability = availability;
                DutyLimitsByAbbr = limits;
                Priorities = priorities;

                Assignments = assignments;
                Workload = workload;
                ConditionalsUsed = conditionalsUsed;
                IsPrefixActive = isPrefixActive;

                DayCount = days.Count;
                DoctorCount = doctors.Count;
                DoctorIndexByAbbr = new Dictionary<string, int>(DoctorCount);
                for (int i = 0; i < DoctorCount; i++) DoctorIndexByAbbr[doctors[i].Abbreviation] = i;
            }

            public int GetLimitFor(int doctorIndex) =>
                DutyLimitsByAbbr.TryGetValue(Doctors[doctorIndex].Abbreviation, out var lim) ? lim : 0;

            public AvailabilityType GetAvailability(int dayIndex, int doctorIndex)
            {
                if (!Availability.TryGetValue(Days[dayIndex], out var map)) return AvailabilityType.Unavailable;
                return map.TryGetValue(Doctors[doctorIndex].Abbreviation, out var t) ? t : AvailabilityType.Unavailable;
            }

            public int GetEarliestUnassignedDayIndex()
            {
                for (int d = 0; d < DayCount; d++) if (Assignments[d] == UNASSIGNED) return d;
                return -1;
            }
        }

        /// <summary>
        /// Sprawdza, czy przypisanie lekarza do dnia jest zgodne z twardymi regułami.
        /// </summary>
        public static bool IsHardFeasible(int dayIndex, int doctorIndex, Context ctx)
        {
            if (doctorIndex < 0 || doctorIndex >= ctx.DoctorCount) return false;
            if (dayIndex < 0 || dayIndex >= ctx.DayCount) return false;

            if (ctx.Workload[doctorIndex] >= ctx.GetLimitFor(doctorIndex)) return false;

            var availability = ctx.GetAvailability(dayIndex, doctorIndex);
            if (Declarations.IsHardBlock(availability)) return false;

            if (!Declarations.BypassesAdjacencyRules(availability))
            {
                if (dayIndex > 0 && ctx.Assignments[dayIndex - 1] == doctorIndex) return false;
                if (dayIndex + 1 < ctx.DayCount && ctx.Assignments[dayIndex + 1] == doctorIndex) return false;
            }

            if (!Declarations.BypassesAdjacencyRules(availability) && IsNextToOtherDuty(dayIndex, doctorIndex, ctx)) return false;

            if (availability == AvailabilityType.ConditionallyAvailable && ctx.ConditionalsUsed[doctorIndex] >= 1) return false;

            return true;
        }

        private static bool IsNextToOtherDuty(int dayIndex, int doctorIndex, Context ctx)
        {
            var abbr = ctx.Doctors[doctorIndex].Abbreviation;
            if (dayIndex > 0)
            {
                var map = ctx.Availability[ctx.Days[dayIndex - 1]];
                if (map.TryGetValue(abbr, out var t) && t == AvailabilityType.OtherDuty) return true;
            }
            if (dayIndex + 1 < ctx.DayCount)
            {
                var map = ctx.Availability[ctx.Days[dayIndex + 1]];
                if (map.TryGetValue(abbr, out var t) && t == AvailabilityType.OtherDuty) return true;
            }
            return false;
        }

        /// <summary>
        /// Zwraca posortowaną listę najlepszych kandydatów na dany dzień zgodnie z hierarchią priorytetów.
        /// </summary>
        public static List<int> OrderCandidates(int dayIndex, Context ctx)
        {
            var legalCandidates = new List<int>(ctx.DoctorCount);
            for (int p = 0; p < ctx.DoctorCount; p++)
            {
                if (IsHardFeasible(dayIndex, p, ctx))
                {
                    legalCandidates.Add(p);
                }
            }

            if (legalCandidates.Count == 0) return legalCandidates;

            legalCandidates.Sort((a, b) => CompareCandidates(dayIndex, a, b, ctx));
            return legalCandidates;
        }

        private static int CompareCandidates(int dayIndex, int a, int b, Context ctx)
        {
            var availabilityA = ctx.GetAvailability(dayIndex, a);
            var availabilityB = ctx.GetAvailability(dayIndex, b);

            // Rezerwacje mają absolutny priorytet
            int rzA = availabilityA == AvailabilityType.Reservation ? 1 : 0;
            int rzB = availabilityB == AvailabilityType.Reservation ? 1 : 0;
            if (rzA != rzB) return rzB.CompareTo(rzA);

            // Porównanie zgodnie z priorytetami użytkownika
            foreach (var priority in ctx.Priorities)
            {
                int cmp = 0;
                switch (priority)
                {
                    case SolverPriority.Fairness:
                        double ratioA = (ctx.Workload[a] + 1.0) / Math.Max(1, ctx.GetLimitFor(a));
                        double ratioB = (ctx.Workload[b] + 1.0) / Math.Max(1, ctx.GetLimitFor(b));
                        cmp = ratioA.CompareTo(ratioB); // Preferujemy lekarza z mniejszym obciążeniem procentowym
                        break;

                    case SolverPriority.Spacing:
                        int distA = NearestAssignedDistance(dayIndex, a, ctx);
                        int distB = NearestAssignedDistance(dayIndex, b, ctx);
                        cmp = distB.CompareTo(distA); // Preferujemy lekarza, który miał dyżur dawniej
                        break;

                    case SolverPriority.DeclarationCompliance:
                        int prefA = Declarations.GetPreferenceWeight(availabilityA);
                        int prefB = Declarations.GetPreferenceWeight(availabilityB);
                        cmp = prefB.CompareTo(prefA); // Preferujemy deklarację o wyższej wadze
                        break;
                }
                if (cmp != 0) return cmp;
            }

            // Domyślne reguły rozstrzygające remisy
            int workloadCmp = ctx.Workload[a].CompareTo(ctx.Workload[b]);
            if (workloadCmp != 0) return workloadCmp; // Mniej dyżurów do tej pory

            return string.Compare(ctx.Doctors[a].Abbreviation, ctx.Doctors[b].Abbreviation, StringComparison.Ordinal);
        }

        private static int NearestAssignedDistance(int dayIndex, int doctorIndex, Context ctx)
        {
            int best = int.MaxValue;
            for (int d = dayIndex - 1; d >= 0; d--)
                if (ctx.Assignments[d] == doctorIndex) { best = Math.Min(best, dayIndex - d); break; }
            for (int d = dayIndex + 1; d < ctx.DayCount; d++)
                if (ctx.Assignments[d] == doctorIndex) { best = Math.Min(best, d - dayIndex); break; }
            return best == int.MaxValue ? 9999 : best;
        }
    }
}