using GrafikoMat.Core.Data;
using GrafikoMat.Core.Scheduling.Models;
using GrafikoMat.Core.Scheduling.Validation;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace GrafikoMat.Core.Scheduling.Engines
{
    /// <summary>
    /// Klasa pomocnicza dla silników metaheurystycznych, dostarczająca metody do tworzenia
    /// rozwiązań początkowych (losowych, chciwych) oraz do generowania sąsiadów.
    /// ROZSZERZONA WERSJA z smart neighbors i cache'owaniem.
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
                    var selected = candidates[_random.Next(candidates.Count)];
                    assignments[day] = selected;
                    workload[selected.Abbreviation]++;
                    if (_scheduleInput.Availability[day][selected.Abbreviation] == AvailabilityType.ConditionallyAvailable)
                    {
                        usedConditionals.Add(selected.Abbreviation);
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
        /// Generuje sąsiada poprzez zmianę 1 losowego dnia (podstawowa wersja).
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

        /// <summary>
        /// NOWA METODA: Inteligentne generowanie sąsiada z różnymi typami ruchów.
        /// Wybór typu ruchu zależy od temperatury (przy wysokiej temp - drastyczne zmiany, przy niskiej - delikatne).
        /// </summary>
        public Dictionary<DateTime, DoctorProfile?> GenerateSmartNeighbor(
            Dictionary<DateTime, DoctorProfile?> currentSchedule,
            double temperature)
        {
            // Przy wysokiej temperaturze: 40% single, 40% swap, 20% shift
            // Przy niskiej temperaturze: 80% single, 15% swap, 5% shift
            double tempRatio = temperature / 1000.0; // normalizacja (InitialTemp = 1000)
            double swapThreshold = 0.4 + (0.4 * tempRatio);
            double shiftThreshold = 0.8 + (0.15 * tempRatio);

            double rand = _random.NextDouble();

            if (rand < swapThreshold)
                return GenerateSwapNeighbor(currentSchedule);
            else if (rand < shiftThreshold)
                return GenerateShiftNeighbor(currentSchedule);
            else
                return GenerateNeighbor(currentSchedule); // single day change
        }

        /// <summary>
        /// Generuje sąsiada przez zamianę dwóch dni miejscami (swap).
        /// </summary>
        private Dictionary<DateTime, DoctorProfile?> GenerateSwapNeighbor(Dictionary<DateTime, DoctorProfile?> currentSchedule)
        {
            var newSchedule = new Dictionary<DateTime, DoctorProfile?>(currentSchedule);

            if (_scheduleInput.DaysInMonth.Count < 2)
                return newSchedule;

            var day1 = _scheduleInput.DaysInMonth[_random.Next(_scheduleInput.DaysInMonth.Count)];
            var day2 = _scheduleInput.DaysInMonth[_random.Next(_scheduleInput.DaysInMonth.Count)];

            if (day1 == day2)
                return newSchedule;

            // Swap
            var temp = newSchedule[day1];
            newSchedule[day1] = newSchedule[day2];
            newSchedule[day2] = temp;

            return newSchedule;
        }

        /// <summary>
        /// Generuje sąsiada przez przesunięcie ciągłego bloku dyżurów.
        /// Znajduje ciągły fragment obsadzony przez tego samego lekarza i próbuje przesunąć go w czasie.
        /// </summary>
        private Dictionary<DateTime, DoctorProfile?> GenerateShiftNeighbor(Dictionary<DateTime, DoctorProfile?> currentSchedule)
        {
            var newSchedule = new Dictionary<DateTime, DoctorProfile?>(currentSchedule);

            // Znajdź losowy blok ciągły (2-5 dni)
            var sortedDays = _scheduleInput.DaysInMonth.OrderBy(d => d).ToList();
            if (sortedDays.Count < 3)
                return newSchedule;

            int startIdx = _random.Next(sortedDays.Count - 2);
            int blockLength = Math.Min(_random.Next(2, 6), sortedDays.Count - startIdx);

            // Sprawdź czy to faktycznie ciągły blok tego samego lekarza
            var firstDoctor = newSchedule[sortedDays[startIdx]];
            if (firstDoctor == null)
                return newSchedule;

            bool isContinuous = true;
            for (int i = 1; i < blockLength; i++)
            {
                if (newSchedule[sortedDays[startIdx + i]]?.Abbreviation != firstDoctor.Abbreviation)
                {
                    isContinuous = false;
                    break;
                }
            }

            if (!isContinuous)
                return newSchedule; // Nie jest ciągły, zwróć bez zmian

            // Przesuń blok o 1-3 dni (forward lub backward)
            int shift = _random.Next(-3, 4);
            if (shift == 0 || startIdx + shift < 0 || startIdx + shift + blockLength > sortedDays.Count)
                return newSchedule;

            // Wykonaj przesunięcie (uproszczona wersja - można rozbudować)
            for (int i = 0; i < blockLength; i++)
            {
                newSchedule[sortedDays[startIdx + i]] = null;
            }
            for (int i = 0; i < blockLength; i++)
            {
                if (startIdx + shift + i >= 0 && startIdx + shift + i < sortedDays.Count)
                    newSchedule[sortedDays[startIdx + shift + i]] = firstDoctor;
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

        /// <summary>
        /// NOWA METODA: Oblicza szybki hash stanu dla cache'owania.
        /// </summary>
        public string ComputeQuickHash(Dictionary<DateTime, DoctorProfile?> assignments)
        {
            var sb = new StringBuilder(assignments.Count * 8);
            foreach (var day in assignments.Keys.OrderBy(d => d))
            {
                var doctor = assignments[day];
                sb.Append(doctor?.Abbreviation ?? "NULL");
                sb.Append(';');
            }
            return sb.ToString();
        }
    }
}