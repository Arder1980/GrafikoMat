using GrafikoMat.Core.Data;
using GrafikoMat.Core.Scheduling.Models;
using GrafikoMat.Core.Scheduling.Validation;
using System;
using System.Collections.Generic;
using System.Linq;

namespace GrafikoMat.Core.Scheduling.Engines
{
    /// <summary>
    /// Klasa pomocnicza dla silników metaheurystycznych, dostarczająca metody do tworzenia
    /// rozwiązań początkowych (losowych, chciwych) oraz do generowania sąsiadów.
    /// Wersja zaadaptowana z GrafikWPF.
    /// </summary>
    internal class SolverUtility
    {
        private readonly ScheduleInput _scheduleInput;
        private readonly Random _random = new();

        public SolverUtility(ScheduleInput scheduleInput)
        {
            _scheduleInput = scheduleInput;
        }

        /// <summary>
        /// Tworzy rozwiązanie początkowe w sposób "chciwy" - dla każdego dnia wybiera
        /// najlepszego dostępnego kandydata na podstawie hierarchii preferencji.
        /// </summary>
        public Dictionary<DateTime, DoctorProfile?> CreateGreedyInitialSolution()
        {
            var assignments = new Dictionary<DateTime, DoctorProfile?>();
            var workload = _scheduleInput.Doctors.ToDictionary(l => l.Abbreviation, l => 0);
            var usedConditionals = new HashSet<string>();

            foreach (var day in _scheduleInput.DaysInMonth)
            {
                var candidates = ConstraintValidationService.GetValidCandidatesForDay(day, _scheduleInput, assignments, workload, usedConditionals);
                if (candidates.Any())
                {
                    var bestCandidate = candidates
                        .OrderByDescending(l => Declarations.GetPreferenceWeight(_scheduleInput.Availability[day][l.Abbreviation]))
                        .ThenBy(l => workload[l.Abbreviation])
                        .First();

                    assignments[day] = bestCandidate;
                    workload[bestCandidate.Abbreviation]++;
                    if (_scheduleInput.Availability[day][bestCandidate.Abbreviation] == AvailabilityType.ConditionallyAvailable)
                    {
                        usedConditionals.Add(bestCandidate.Abbreviation);
                    }
                }
                else
                {
                    assignments[day] = null;
                }
            }
            return assignments;
        }

        /// <summary>
        /// Tworzy w pełni losowe, ale poprawne (zgodne z twardymi regułami) rozwiązanie.
        /// </summary>
        public Dictionary<DateTime, DoctorProfile?> CreateRandomSolution()
        {
            var assignments = new Dictionary<DateTime, DoctorProfile?>();
            var workload = _scheduleInput.Doctors.ToDictionary(l => l.Abbreviation, l => 0);
            var usedConditionals = new HashSet<string>();
            foreach (var day in _scheduleInput.DaysInMonth)
            {
                var candidates = ConstraintValidationService.GetValidCandidatesForDay(day, _scheduleInput, assignments, workload, usedConditionals);
                if (candidates.Any())
                {
                    var chosen = candidates[_random.Next(candidates.Count)];
                    assignments[day] = chosen;
                    workload[chosen.Abbreviation]++;
                    if (_scheduleInput.Availability[day][chosen.Abbreviation] == AvailabilityType.ConditionallyAvailable)
                    {
                        usedConditionals.Add(chosen.Abbreviation);
                    }
                }
                else
                {
                    assignments[day] = null;
                }
            }
            return assignments;
        }


        /// <summary>
        /// Generuje sąsiada danego rozwiązania poprzez losową modyfikację jednego dnia.
        /// </summary>
        public Dictionary<DateTime, DoctorProfile?> GenerateNeighbor(Dictionary<DateTime, DoctorProfile?> currentSchedule)
        {
            var newSchedule = new Dictionary<DateTime, DoctorProfile?>(currentSchedule);
            var dayToChange = _scheduleInput.DaysInMonth[_random.Next(_scheduleInput.DaysInMonth.Count)];

            var workload = CalculateWorkload(newSchedule);
            var usedConditionals = CalculateUsedConditionals(newSchedule).Keys.ToHashSet();

            var candidates = ConstraintValidationService.GetValidCandidatesForDay(dayToChange, _scheduleInput, newSchedule, workload, usedConditionals);
            if (candidates.Any())
            {
                newSchedule[dayToChange] = candidates[_random.Next(candidates.Count)];
            }
            else
            {
                newSchedule[dayToChange] = null;
            }

            return newSchedule;
        }

        public Dictionary<string, int> CalculateWorkload(IReadOnlyDictionary<DateTime, DoctorProfile?> assignments)
        {
            var workload = _scheduleInput.Doctors.ToDictionary(l => l.Abbreviation, l => 0);
            foreach (var doctor in assignments.Values)
            {
                if (doctor != null)
                {
                    workload[doctor.Abbreviation]++;
                }
            }
            return workload;
        }

        private Dictionary<string, int> CalculateUsedConditionals(IReadOnlyDictionary<DateTime, DoctorProfile?> assignments)
        {
            var used = _scheduleInput.Doctors.ToDictionary(l => l.Abbreviation, l => 0);
            foreach (var pair in assignments)
            {
                if (pair.Value != null && _scheduleInput.Availability.ContainsKey(pair.Key) && _scheduleInput.Availability[pair.Key][pair.Value.Abbreviation] == AvailabilityType.ConditionallyAvailable)
                {
                    used[pair.Value.Abbreviation]++;
                }
            }
            return used;
        }
    }
}