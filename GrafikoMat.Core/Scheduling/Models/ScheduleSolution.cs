using GrafikoMat.Core.Data;
using System;
using System.Collections.Generic;
using System.Linq;

namespace GrafikoMat.Core.Scheduling.Models
{
    /// <summary>
    /// Reprezentuje finalny, obliczony grafik wraz z metrykami jakościowymi.
    /// Wersja zaadaptowana z GrafikWPF (RozwiazanyGrafik).
    /// </summary>
    public class ScheduleSolution
    {
        /// <summary>
        /// Główny wynik - słownik przypisujący lekarza (lub null) do konkretnego dnia.
        /// </summary>
        public Dictionary<DateTime, DoctorProfile?> Assignments { get; set; } = new();

        /// <summary>
        /// Długość nieprzerwanego ciągu obsadzonych dni od początku miesiąca.
        /// </summary>
        public int InitialContinuity { get; set; }

        /// <summary>
        /// Całkowita liczba dni, w których przydzielono dyżur.
        /// </summary>
        public int TotalAssignments => Assignments.Values.Count(l => l != null);

        public int FulfilledReservations { get; set; }
        public int FulfilledWants { get; set; }
        public int FulfilledAvailables { get; set; }

        /// <summary>
        /// Wskaźnik sprawiedliwości: odchylenie standardowe procentowych obciążeń lekarzy względem ich limitów.
        /// Im niższa wartość, tym bardziej sprawiedliwy grafik.
        /// </summary>
        public double FairnessIndex { get; set; }

        /// <summary>
        /// Wskaźnik równomierności: średnie odchylenie standardowe odstępów między dyżurami dla poszczególnych lekarzy.
        /// Im niższa wartość, tym bardziej równomiernie rozłożone dyżury w czasie.
        /// </summary>
        public double SpacingIndex { get; set; }

        /// <summary>
        /// Słownik z finalnym obciążeniem każdego z lekarzy (ile dyżurów otrzymał).
        /// Klucz: Symbol (Abbreviation), Wartość: Liczba dyżurów.
        /// </summary>
        public Dictionary<string, int> FinalWorkload { get; set; } = new();
    }
}