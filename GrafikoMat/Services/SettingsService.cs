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
            var activePriorities = settings.Priorities
                .Where(p => p.IsActive)
                .Select(p => p.Priority)
                .ToList();

            if (activePriorities.Count == 0)
            {
                activePriorities = new List<SolverPriority>
                {
                    SolverPriority.InitialContinuity,
                    SolverPriority.TotalAssignments,
                    SolverPriority.Fairness,
                    SolverPriority.Spacing,
                    SolverPriority.DeclarationCompliance
                };
            }

            return new SolverParameters
            {
                SolverType = settings.SelectedSolver,
                TimeoutMinutes = settings.TimeoutMinutes,
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