using GrafikoMat.Core.Data;
using GrafikoMat.Core.Scheduling.Models; // Upewnij się, że ta linia istnieje
using System;
using System.Collections.Generic;
using System.Linq;

namespace GrafikoMat.Core.Scheduling.Validation
{
    /// <summary>
    /// Zawiera metody statyczne do walidacji twardych ograniczeń grafiku.
    /// Sprawdza, czy kandydat jest poprawny dla danego dnia w oparciu o niezmienne reguły.
    /// Wersja zaadaptowana z GrafikWPF.
    /// </summary>
    public static class ConstraintValidationService
    {
        public static List<DoctorProfile> GetValidCandidatesForDay(
            DateTime day,
            ScheduleInput scheduleInput,
            IReadOnlyDictionary<DateTime, DoctorProfile?> currentAssignments,
            IReadOnlyDictionary<string, int> currentWorkload,
            IReadOnlySet<string> usedConditionalDuties)
        {
            var daysInMonth = scheduleInput.DaysInMonth;
            var previousDayDoctor = day > daysInMonth.First() && currentAssignments.TryGetValue(day.AddDays(-1), out var yesterdayDoctor) ? yesterdayDoctor : null;

            var candidates = new List<DoctorProfile>();
            foreach (var doctor in scheduleInput.Doctors.Where(l => !l.IsArchived))
            {
                if (IsValidCandidate(doctor, day, scheduleInput, previousDayDoctor, currentWorkload, usedConditionalDuties))
                {
                    candidates.Add(doctor);
                }
            }
            return candidates;
        }

        private static bool IsValidCandidate(DoctorProfile doctor, DateTime day, ScheduleInput scheduleInput, DoctorProfile? previousDayDoctor, IReadOnlyDictionary<string, int> currentWorkload, IReadOnlySet<string> usedConditionalDuties)
        {
            int dutyLimit = scheduleInput.DutyLimits.GetValueOrDefault(doctor.Abbreviation, 0);

            // Reguła: Przekroczony limit dyżurów
            if (dutyLimit > 0 && currentWorkload.GetValueOrDefault(doctor.Abbreviation, 0) >= dutyLimit)
                return false;

            var availabilityToday = scheduleInput.Availability[day][doctor.Abbreviation];

            // Reguła: Twarde blokady (Urlop, Inny Dyżur, Niedostępny)
            if (Declarations.IsHardBlock(availabilityToday))
                return false;

            // Reguła: Zakaz dyżuru dzień po dniu
            if (previousDayDoctor?.Id == doctor.Id)
                return false;

            // Reguła: Zakaz dyżuru w sąsiedztwie "Innego Dyżuru"
            var tomorrow = day.AddDays(1);
            if (scheduleInput.Availability.ContainsKey(tomorrow) && scheduleInput.Availability[tomorrow][doctor.Abbreviation] == AvailabilityType.OtherDuty)
                return false;

            var yesterday = day.AddDays(-1);
            if (scheduleInput.Availability.ContainsKey(yesterday) && scheduleInput.Availability[yesterday][doctor.Abbreviation] == AvailabilityType.OtherDuty)
                return false;

            // Reguła: Tylko jeden dyżur warunkowy w miesiącu
            if (availabilityToday == AvailabilityType.ConditionallyAvailable && usedConditionalDuties.Contains(doctor.Abbreviation))
                return false;

            return true;
        }

        /// <summary>
        /// Metoda narzędziowa do naprawy wygenerowanego grafiku poprzez usunięcie przypisań,
        /// które naruszają twarde ograniczenia. Może być użyteczna po operacjach takich jak krzyżowanie w Algorytmie Genetycznym.
        /// </summary>
        public static void RepairSchedule(Dictionary<DateTime, DoctorProfile?> schedule, ScheduleInput scheduleInput)
        {
            bool changeMade;
            do
            {
                changeMade = false;
                var workload = CalculateWorkload(schedule, scheduleInput);
                var usedConditionals = CalculateUsedConditionals(schedule, scheduleInput);

                // Naprawa limitów dyżurów
                foreach (var doctorAbbr in workload.Keys.ToList())
                {
                    var limit = scheduleInput.DutyLimits.GetValueOrDefault(doctorAbbr, 0);
                    while (limit > 0 && workload[doctorAbbr] > limit)
                    {
                        var dutiesToRemove = schedule.Where(g => g.Value?.Abbreviation == doctorAbbr).ToList();
                        if (dutiesToRemove.Any())
                        {
                            var dutyToRemove = dutiesToRemove
                                .OrderBy(d => Declarations.GetPreferenceWeight(scheduleInput.Availability[d.Key][d.Value!.Abbreviation]))
                                .ThenByDescending(d => d.Key)
                                .First();

                            schedule[dutyToRemove.Key] = null;
                            workload[doctorAbbr]--;
                            changeMade = true;
                        }
                        else break;
                    }
                }

                // Naprawa użycia dyżurów warunkowych
                foreach (var doctorAbbr in usedConditionals.Keys.ToList())
                {
                    while (usedConditionals[doctorAbbr] > 1)
                    {
                        var conditionalDutyToRemove = schedule.FirstOrDefault(g => g.Value?.Abbreviation == doctorAbbr && scheduleInput.Availability[g.Key][g.Value.Abbreviation] == AvailabilityType.ConditionallyAvailable);
                        if (conditionalDutyToRemove.Key != default)
                        {
                            schedule[conditionalDutyToRemove.Key] = null;
                            usedConditionals[doctorAbbr]--;
                            changeMade = true;
                        }
                        else break;
                    }
                }

                // Naprawa reguł sąsiedztwa
                foreach (var day in scheduleInput.DaysInMonth)
                {
                    var doctor = schedule[day];
                    if (doctor == null) continue;

                    var availability = scheduleInput.Availability[day][doctor.Abbreviation];
                    if (Declarations.IsReservation(availability)) continue;

                    var yesterday = day.AddDays(-1);
                    if (schedule.ContainsKey(yesterday))
                    {
                        if (schedule[yesterday]?.Id == doctor.Id || scheduleInput.Availability[yesterday][doctor.Abbreviation] == AvailabilityType.OtherDuty)
                        {
                            schedule[day] = null;
                            changeMade = true;
                            continue;
                        }
                    }

                    var tomorrow = day.AddDays(1);
                    if (schedule.ContainsKey(tomorrow) && scheduleInput.Availability[tomorrow][doctor.Abbreviation] == AvailabilityType.OtherDuty)
                    {
                        schedule[day] = null;
                        changeMade = true;
                    }
                }

            } while (changeMade);
        }

        private static Dictionary<string, int> CalculateWorkload(IReadOnlyDictionary<DateTime, DoctorProfile?> schedule, ScheduleInput scheduleInput)
        {
            var workload = scheduleInput.Doctors.ToDictionary(l => l.Abbreviation, l => 0);
            foreach (var doctor in schedule.Values.Where(l => l != null))
            {
                if (doctor != null) workload[doctor.Abbreviation]++;
            }
            return workload;
        }

        private static Dictionary<string, int> CalculateUsedConditionals(IReadOnlyDictionary<DateTime, DoctorProfile?> schedule, ScheduleInput scheduleInput)
        {
            var used = scheduleInput.Doctors.ToDictionary(l => l.Abbreviation, l => 0);
            foreach (var pair in schedule)
            {
                if (pair.Value != null && scheduleInput.Availability.ContainsKey(pair.Key) && scheduleInput.Availability[pair.Key][pair.Value.Abbreviation] == AvailabilityType.ConditionallyAvailable)
                {
                    used[pair.Value.Abbreviation]++;
                }
            }
            return used;
        }
    }
}