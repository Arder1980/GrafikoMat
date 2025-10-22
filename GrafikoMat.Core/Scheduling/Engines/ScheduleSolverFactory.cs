using GrafikoMat.Core.Scheduling.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

namespace GrafikoMat.Core.Scheduling.Engines
{
    public static class ScheduleSolverFactory
    {
        public static IScheduleSolver Create(
            ScheduleInput input,
            SolverParameters parameters,
            List<SolverPriority> activePriorities,
            IProgress<double>? progress = null,
            CancellationToken token = default)
        {
            // Walidacja parametrów wejściowych
            ValidateInput(input, activePriorities);

            var timeout = TimeSpan.FromMinutes(parameters.TimeoutMinutes);

            switch (parameters.SolverType)
            {
                case SolverType.Backtracking:
                    return new BacktrackingSolver(input, activePriorities, timeout, progress, token);

                case SolverType.SimulatedAnnealing:
                    return new SimulatedAnnealingSolver(input, activePriorities, parameters.CoolingRate, timeout, progress, token);

                case SolverType.Genetic:
                    return new GeneticSolver(input, activePriorities, parameters.GeneticPopulationSize, parameters.GeneticGenerations, timeout, progress, token);

                case SolverType.TabuSearch:
                    return new TabuSearchSolver(input, activePriorities, parameters.TabuListSize, parameters.TabuMaxIterations, timeout, progress, token);

                case SolverType.AntColony:
                    return new AntColonySolver(input, activePriorities, parameters.AntColonyAnts, parameters.AntColonyGenerations, timeout, progress, token);

                case SolverType.AStar:
                    return new AStarSolver(input, activePriorities, timeout, progress, token);

                default:
                    return new BacktrackingSolver(input, activePriorities, timeout, progress, token);
            }
        }

        /// <summary>
        /// Waliduje parametry wejściowe przed utworzeniem solvera.
        /// Rzuca ArgumentException lub ArgumentNullException w przypadku niepoprawnych danych.
        /// </summary>
        private static void ValidateInput(ScheduleInput input, List<SolverPriority> activePriorities)
        {
            if (input == null)
                throw new ArgumentNullException(nameof(input), "ScheduleInput nie może być null");

            if (input.Doctors == null)
                throw new ArgumentNullException(nameof(input.Doctors), "Lista lekarzy nie może być null");

            if (input.Doctors.Count == 0)
                throw new ArgumentException("Lista lekarzy nie może być pusta. Dodaj przynajmniej jednego lekarza.", nameof(input.Doctors));

            var activeDoctors = input.Doctors.Where(d => !d.IsArchived).ToList();
            if (activeDoctors.Count == 0)
                throw new ArgumentException("Wszyscy lekarze są zarchiwizowani. Musi być przynajmniej jeden aktywny lekarz.", nameof(input.Doctors));

            if (input.Availability == null)
                throw new ArgumentNullException(nameof(input.Availability), "Słownik dostępności nie może być null");

            // DaysInMonth jest computed property, ale sprawdźmy czy Availability ma jakieś dni
            if (input.DaysInMonth.Count == 0)
                throw new ArgumentException("Brak dni w miesiącu. Słownik Availability musi zawierać przynajmniej jeden dzień.", nameof(input.Availability));

            if (input.DutyLimits == null)
                throw new ArgumentNullException(nameof(input.DutyLimits), "Słownik limitów dyżurów nie może być null");

            if (activePriorities == null)
                throw new ArgumentNullException(nameof(activePriorities), "Lista priorytetów nie może być null");

            if (activePriorities.Count == 0)
                throw new ArgumentException("Lista priorytetów nie może być pusta. Dodaj przynajmniej jeden priorytet.", nameof(activePriorities));

            // Sprawdzenie spójności danych: każdy dzień powinien mieć dostępność dla wszystkich lekarzy
            foreach (var day in input.DaysInMonth)
            {
                if (!input.Availability.ContainsKey(day))
                    throw new ArgumentException($"Brak danych dostępności dla dnia {day:yyyy-MM-dd}", nameof(input.Availability));

                var dayAvailability = input.Availability[day];
                if (dayAvailability == null)
                    throw new ArgumentException($"Dostępność dla dnia {day:yyyy-MM-dd} nie może być null", nameof(input.Availability));

                // Sprawdź czy każdy aktywny lekarz ma wpis dostępności
                foreach (var doctor in activeDoctors)
                {
                    if (!dayAvailability.ContainsKey(doctor.Abbreviation))
                        throw new ArgumentException(
                            $"Brak wpisu dostępności dla lekarza {doctor.Abbreviation} w dniu {day:yyyy-MM-dd}",
                            nameof(input.Availability));
                }
            }
        }
    }
}