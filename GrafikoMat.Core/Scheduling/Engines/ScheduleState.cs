using GrafikoMat.Core.Data;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace GrafikoMat.Core.Scheduling.Engines
{
    /// <summary>
    /// Enkapsuluje pełny stan rozwiązania grafiku wraz z metrykami pochodnymi.
    /// Umożliwia szybkie klonowanie i przyrostowe aktualizacje bez ciągłego kopiowania słowników.
    /// </summary>
    internal class ScheduleState
    {
        public Dictionary<DateTime, DoctorProfile?> Assignments { get; private set; }
        public Dictionary<string, int> Workload { get; private set; }
        public Dictionary<string, int> ConditionalUsage { get; private set; }

        // Cached metrics (obliczane lazy lub po zmianach)
        public int InitialContinuity { get; set; }
        public int FulfilledReservations { get; set; }
        public int FulfilledWants { get; set; }
        public int FulfilledAvailables { get; set; }

        private string? _cachedHash;

        public ScheduleState(
            Dictionary<DateTime, DoctorProfile?> assignments,
            Dictionary<string, int> workload,
            Dictionary<string, int> conditionalUsage)
        {
            Assignments = assignments;
            Workload = workload;
            ConditionalUsage = conditionalUsage;
        }

        /// <summary>
        /// Tworzy głęboką kopię stanu
        /// </summary>
        public ScheduleState Clone()
        {
            return new ScheduleState(
                new Dictionary<DateTime, DoctorProfile?>(Assignments),
                new Dictionary<string, int>(Workload),
                new Dictionary<string, int>(ConditionalUsage))
            {
                InitialContinuity = InitialContinuity,
                FulfilledReservations = FulfilledReservations,
                FulfilledWants = FulfilledWants,
                FulfilledAvailables = FulfilledAvailables
            };
        }

        /// <summary>
        /// Oblicza szybki hash stanu (dla cache'owania odwiedzonych stanów)
        /// </summary>
        public string GetQuickHash()
        {
            if (_cachedHash != null)
                return _cachedHash;

            var sb = new StringBuilder(Assignments.Count * 8);
            foreach (var day in Assignments.Keys.OrderBy(d => d))
            {
                var doctor = Assignments[day];
                sb.Append(doctor?.Abbreviation ?? "NULL");
                sb.Append(';');
            }

            _cachedHash = sb.ToString();
            return _cachedHash;
        }

        /// <summary>
        /// Invaliduje cached hash po zmianie
        /// </summary>
        public void InvalidateHash()
        {
            _cachedHash = null;
        }

        /// <summary>
        /// Stosuje ruch (zmianę przypisania) i aktualizuje workload
        /// </summary>
        public void ApplyMove(DateTime day, DoctorProfile? oldDoctor, DoctorProfile? newDoctor)
        {
            Assignments[day] = newDoctor;

            if (oldDoctor != null)
                Workload[oldDoctor.Abbreviation]--;

            if (newDoctor != null)
                Workload[newDoctor.Abbreviation]++;

            InvalidateHash();
        }
    }
}