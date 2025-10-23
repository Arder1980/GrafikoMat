using GrafikoMat.Core.Scheduling.Models;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Windows.Storage;

namespace GrafikoMat.Services
{
    public enum AppTheme { Light, Dark, SystemDefault }

    /// <summary>
    /// Zachowanie aplikacji przy przekroczeniu limitu czasu obliczeń.
    /// </summary>
    public enum TimeoutBehavior
    {
        /// <summary>Pokaż dialog informacyjny i zwróć najlepsze znalezione rozwiązanie</summary>
        ShowResultAndInform,
        /// <summary>Zapytaj użytkownika czy przedłużyć czas obliczeniowy</summary>
        AskToExtend
    }

    /// <summary>
    /// Poziom logowania zdarzeń aplikacji.
    /// </summary>
    public enum AppLogLevel
    {
        /// <summary>Brak logowania</summary>
        Off,
        /// <summary>Tylko błędy krytyczne</summary>
        ErrorsOnly,
        /// <summary>Błędy + ostrzeżenia</summary>
        ErrorsAndWarnings,
        /// <summary>Pełne logowanie wszystkich zdarzeń</summary>
        Full
    }

    /// <summary>
    /// Poziom logowania procesu generowania grafiku przez silniki.
    /// </summary>
    public enum SolverLogLevel
    {
        /// <summary>Brak logowania silników</summary>
        Off,
        /// <summary>Podstawowy - tylko podsumowanie (~1 KB na generowanie)</summary>
        Basic,
        /// <summary>Szczegółowy - postęp + statystyki (~10-50 KB na generowanie)</summary>
        Detailed,
        /// <summary>Debug - pełna diagnostyka (~100 KB - 10 MB na generowanie)</summary>
        Debug
    }

    public record PrioritySetting(SolverPriority Priority, bool IsActive);

    public record AppSettings
    {
        public string SupabaseUrl { get; init; } = string.Empty;
        public string SupabaseAnonKey { get; init; } = string.Empty;

        public SolverType SelectedSolver { get; init; } = SolverType.Backtracking;
        public AppTheme Theme { get; init; } = AppTheme.SystemDefault;
        public WindowSize LastWindowSize { get; init; } = new(1600, 1000);
        public WindowPosition LastWindowPosition { get; init; } = new(0, 0);
        public bool WasWindowMaximized { get; init; } = false;
        public Guid? LastActiveUnitId { get; init; }

        public List<PrioritySetting> Priorities { get; init; } = new();

        // Timeout globalny (w minutach)
        public int TimeoutMinutes { get; init; } = SolverDefaults.TimeoutMinutes.Default;

        // Wielowątkowość (null = automatyczne wykrywanie)
        public int? CustomThreadCount { get; init; } = SolverDefaults.CustomThreadCount.Default;

        // Zachowanie przy przekroczeniu timeout
        public TimeoutBehavior TimeoutBehavior { get; init; } = TimeoutBehavior.ShowResultAndInform;

        // Poziomy logowania
        public AppLogLevel AppLogLevel { get; init; } = AppLogLevel.ErrorsOnly;
        public SolverLogLevel SolverLogLevel { get; init; } = SolverLogLevel.Off;

        // Parametry silników - używamy wartości domyślnych z SolverDefaults
        public double CoolingRate { get; init; } = SolverDefaults.CoolingRate.Default;
        public int GeneticPopulationSize { get; init; } = SolverDefaults.GeneticPopulationSize.Default;
        public int GeneticGenerations { get; init; } = SolverDefaults.GeneticGenerations.Default;
        public int AntColonyAnts { get; init; } = SolverDefaults.AntColonyAnts.Default;
        public int AntColonyGenerations { get; init; } = SolverDefaults.AntColonyGenerations.Default;
        public int TabuListSize { get; init; } = SolverDefaults.TabuListSize.Default;
        public int TabuMaxIterations { get; init; } = SolverDefaults.TabuMaxIterations.Default;
    }

    public record WindowSize(int Width, int Height);

    public record WindowPosition(int X, int Y);

    public sealed class SettingsService
    {
        private const string SETTINGS_FILENAME = "settings.json";
        private static readonly string _settingsPath = Path.Combine(ApplicationData.Current.LocalFolder.Path, SETTINGS_FILENAME);
        private AppSettings? _currentSettings;

        /// <summary>
        /// Zwraca domyślną kolejność priorytetów używaną przy pierwszym uruchomieniu aplikacji.
        /// Kolejność od najważniejszego do najmniej ważnego.
        /// </summary>
        private static List<PrioritySetting> GetDefaultPriorities()
        {
            return new List<PrioritySetting>
            {
                new(SolverPriority.InitialContinuity, true),
                new(SolverPriority.TotalAssignments, true),
                new(SolverPriority.Fairness, true),
                new(SolverPriority.Spacing, true),
                new(SolverPriority.DeclarationCompliance, true)
            };
        }

        public async Task<AppSettings> LoadSettingsAsync(bool forceReload = false)
        {
            if (_currentSettings != null && !forceReload)
            {
                return _currentSettings;
            }

            try
            {
                if (File.Exists(_settingsPath))
                {
                    var json = await File.ReadAllTextAsync(_settingsPath);
                    _currentSettings = JsonSerializer.Deserialize<AppSettings>(json);
                }
            }
            catch (Exception)
            {
                _currentSettings = null;
            }

            _currentSettings ??= new AppSettings();

            // ✅ NOWE: Jeśli lista priorytetów jest pusta, użyj domyślnych wartości
            if (_currentSettings.Priorities.Count == 0)
            {
                _currentSettings = _currentSettings with { Priorities = GetDefaultPriorities() };
            }

            return _currentSettings;
        }

        public async Task SaveSettingsAsync(AppSettings settings)
        {
            _currentSettings = settings;
            try
            {
                var options = new JsonSerializerOptions { WriteIndented = true };
                var json = JsonSerializer.Serialize(settings, options);
                await File.WriteAllTextAsync(_settingsPath, json);
            }
            catch (Exception)
            {
                // W przypadku błędu - logowanie lub rethrow
            }
        }

        public SolverParameters BuildSolverParameters(AppSettings settings)
        {
            // Po LoadSettingsAsync() lista Priorities zawsze ma wartości (domyślne lub zapisane)
            var activePriorities = settings.Priorities
                .Where(p => p.IsActive)
                .Select(p => p.Priority)
                .ToList();

            // Bezpieczeństwo - jeśli wszystkie priorytety są nieaktywne, użyj domyślnych
            if (activePriorities.Count == 0)
            {
                activePriorities = GetDefaultPriorities()
                    .Select(p => p.Priority)
                    .ToList();
            }

            return new SolverParameters
            {
                SolverType = settings.SelectedSolver,
                TimeoutMinutes = settings.TimeoutMinutes,
                CustomThreadCount = settings.CustomThreadCount,
                CoolingRate = settings.CoolingRate,
                GeneticPopulationSize = settings.GeneticPopulationSize,
                GeneticGenerations = settings.GeneticGenerations,
                AntColonyAnts = settings.AntColonyAnts,
                AntColonyGenerations = settings.AntColonyGenerations,
                TabuListSize = settings.TabuListSize,
                TabuMaxIterations = settings.TabuMaxIterations
            };
        }
    }
}