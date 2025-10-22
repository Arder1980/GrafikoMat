using System;
using System.Collections.Generic;

namespace GrafikoMat.Core.Scheduling.Models
{
    /// <summary>
    /// Zawiera komunikaty statusu dla różnych solverów, wyświetlane użytkownikowi podczas generowania grafiku.
    /// Komunikaty są dobierane na podstawie postępu (0.0-1.0) dla metaheurystyk,
    /// lub losowo rotowane dla solverów deterministycznych (Backtracking, A*).
    /// </summary>
    public static class SolverMessages
    {
        private static readonly Random _random = new Random();

        /// <summary>
        /// Komunikaty dla Algorytmu Genetycznego (8 etapów ewolucji).
        /// </summary>
        private static readonly string[] GeneticMessages = new[]
        {
            "Tworzenie pierwszej populacji grafików",
            "Ocena przystosowania każdego osobnika",
            "Selekcja naturalna eliminuje najsłabsze grafiki",
            "Krzyżowanie genów najlepszych rozwiązań",
            "Wprowadzanie losowych mutacji do genomu",
            "Ewolucja kolejnego pokolenia w toku",
            "Przetrwanie i reprodukcja najlepiej przystosowanych",
            "Narodziny ostatecznego, najdoskonalszego grafiku"
        };

        /// <summary>
        /// Komunikaty dla Kolonii Mrówek (8 etapów poszukiwań).
        /// </summary>
        private static readonly string[] AntColonyMessages = new[]
        {
            "Wysłanie mrówek-zwiadowców na poszukiwania grafiku",
            "Przecieranie pierwszych, przypadkowych szlaków",
            "Pozostawianie śladów feromonowych na trasach",
            "Ocena pierwszych znalezionych rozwiązań",
            "Kolejne mrówki przybywają do znalezisk, wzmacniając ślady feromonowe",
            "Parowanie feromonów z rzadko uczęszczanych ścieżek",
            "Konwergencja kolonii wokół głównego szlaku",
            "Utrwalenie trasy do najlepszego grafiku"
        };

        /// <summary>
        /// Komunikaty dla Symulowanego Wyżarzania (8 etapów hartowania).
        /// </summary>
        private static readonly string[] SimulatedAnnealingMessages = new[]
        {
            "Rozgrzewanie struktury grafiku do wysokiej temperatury",
            "Materiał staje się plastyczny i podatny na zmiany",
            "Chaotyczne przekształcenia wewnętrzne pod wpływem ciepła",
            "Stopniowe, kontrolowane obniżanie temperatury",
            "Układ krzepnie i stabilizuje się",
            "Formowanie się trwałych wiązań krystalicznych",
            "Hartowanie w celu osiągnięcia idealnych właściwości",
            "Otrzymanie ostatecznego, doskonałego grafiku"
        };

        /// <summary>
        /// Komunikaty dla Przeszukiwania z Tabu (8 etapów eksploracji labiryntu).
        /// </summary>
        private static readonly string[] TabuSearchMessages = new[]
        {
            "Wejście do labiryntu możliwych grafików",
            "Badanie wszystkich dostępnych rozwidleń",
            "Obranie najbardziej obiecującej ścieżki",
            "Zamknięcie przebytego korytarza by uniknąć zapętlenia",
            "Unikanie powrotu do niedawno odwiedzonych miejsc",
            "Systematyczna eksploracja odległych zakątków",
            "Dochodzenie do coraz lepszych rozwiązań",
            "Odnalezienie wyjścia z najlepszym grafkiem"
        };

        /// <summary>
        /// Komunikaty dla solwerów deterministycznych (Backtracking, A*).
        /// Są losowo rotowane co ~10-15s, ponieważ nie mają deterministycznego postępu.
        /// </summary>
        private static readonly string[] DeterministicMessages = new[]
        {
            "Systematyczne przeszukiwanie drzewa decyzji",
            "Metodyczna analiza możliwych kombinacji",
            "Dogłębna eksploracja przestrzeni rozwiązań"
        };

        /// <summary>
        /// Zwraca komunikat statusu dla danego typu solvera i postępu.
        /// </summary>
        /// <param name="solverType">Typ solvera.</param>
        /// <param name="progress">Postęp (0.0-1.0). Dla solverów deterministycznych jest ignorowany.</param>
        /// <returns>Komunikat statusu do wyświetlenia użytkownikowi.</returns>
        public static string GetStatusMessage(SolverType solverType, double progress)
        {
            string[] messages = solverType switch
            {
                SolverType.Genetic => GeneticMessages,
                SolverType.AntColony => AntColonyMessages,
                SolverType.SimulatedAnnealing => SimulatedAnnealingMessages,
                SolverType.TabuSearch => TabuSearchMessages,
                SolverType.Backtracking => DeterministicMessages,
                SolverType.AStar => DeterministicMessages,
                _ => new[] { "Generowanie grafiku w toku..." }
            };

            // Dla solwerów deterministycznych (Backtracking, A*) zwracamy losowy komunikat
            if (solverType == SolverType.Backtracking || solverType == SolverType.AStar)
            {
                return messages[_random.Next(messages.Length)];
            }

            // Dla metaheurystyk: wybieramy komunikat na podstawie postępu
            int index = Math.Clamp((int)(progress * messages.Length), 0, messages.Length - 1);
            return messages[index];
        }

        /// <summary>
        /// Formatuje licznik przeanalizowanych stanów dla solwerów deterministycznych.
        /// </summary>
        /// <param name="nodesExpanded">Liczba przeanalizowanych stanów/węzłów.</param>
        /// <returns>Sformatowany tekst licznika (np. "Przeanalizowano 12 847 stanów").</returns>
        public static string FormatNodeCount(int nodesExpanded)
        {
            return $"Przeanalizowano {nodesExpanded:N0} stanów";
        }

        /// <summary>
        /// Sprawdza czy dany solver jest deterministyczny (używa indeterminate progress).
        /// </summary>
        public static bool IsDeterministicSolver(SolverType solverType)
        {
            return solverType == SolverType.Backtracking || solverType == SolverType.AStar;
        }
    }
}