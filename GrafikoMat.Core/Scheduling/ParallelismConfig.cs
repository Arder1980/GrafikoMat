using System;
using System.Threading.Tasks;

namespace GrafikoMat.Core.Scheduling
{
    /// <summary>
    /// Centralna konfiguracja wielowątkowości dla silników obliczeniowych.
    /// Automatycznie wykrywa liczbę dostępnych wątków procesora i dostosowuje parametry równoległości.
    /// </summary>
    public static class ParallelismConfig
    {
        /// <summary>
        /// Liczba dostępnych wątków logicznych procesora (włącznie z HyperThreading/SMT).
        /// </summary>
        public static int ProcessorCount { get; } = Environment.ProcessorCount;

        /// <summary>
        /// Optymalna liczba wątków dla operacji równoległych.
        /// Pozostawia 1 wątek wolny dla UI i systemu operacyjnego.
        /// </summary>
        public static int OptimalParallelism => Math.Max(1, ProcessorCount - 1);

        /// <summary>
        /// Oblicza optymalną liczbę równoległych kandydatów dla algorytmów SA/Tabu.
        /// Zakres: 4-16 wątków (więcej może dać overhead).
        /// </summary>
        /// <param name="customThreadCount">Opcjonalna niestandardowa liczba wątków (null = auto)</param>
        public static int GetCandidatesCount(int? customThreadCount = null)
        {
            int threads = customThreadCount ?? OptimalParallelism;
            return Math.Clamp(threads, 4, 16);
        }

        /// <summary>
        /// Oblicza optymalną liczbę wysp dla algorytmu genetycznego.
        /// Zakres: 2-8 wysp (więcej może dać overhead synchronizacji).
        /// </summary>
        /// <param name="customThreadCount">Opcjonalna niestandardowa liczba wątków (null = auto)</param>
        public static int GetIslandsCount(int? customThreadCount = null)
        {
            int threads = customThreadCount ?? OptimalParallelism;
            return Math.Clamp(threads / 2, 2, 8);
        }

        /// <summary>
        /// Tworzy ParallelOptions z optymalnym MaxDegreeOfParallelism.
        /// </summary>
        /// <param name="customThreadCount">Opcjonalna niestandardowa liczba wątków (null = auto)</param>
        public static ParallelOptions CreateOptions(int? customThreadCount = null)
        {
            int threads = customThreadCount ?? OptimalParallelism;
            return new ParallelOptions
            {
                MaxDegreeOfParallelism = Math.Max(1, threads)
            };
        }

        /// <summary>
        /// Zwraca tekstowy opis konfiguracji wielowątkowości.
        /// </summary>
        public static string GetConfigurationInfo(int? customThreadCount = null)
        {
            int threads = customThreadCount ?? OptimalParallelism;
            return $"Procesor: {ProcessorCount} wątków, Używane: {threads} wątków";
        }
    }
}