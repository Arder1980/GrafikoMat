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

    // NOWA STRUKTURA: Reprezentuje pojedyncze ustawienie priorytetu (typ + czy aktywny)
    public record PrioritySetting(SolverPriority Priority, bool IsActive);

    public record AppSettings
    {
        public string SupabaseUrl { get; init; } = string.Empty;
        public string SupabaseAnonKey { get; init; } = string.Empty;
        public SolverType SelectedSolver { get; init; } = SolverType.Backtracking;
        public AppTheme Theme { get; init; } = AppTheme.SystemDefault;
        public WindowSize LastWindowSize { get; init; } = new(1600, 1000);
        public WindowPosition LastWindowPosition { get; init; } = new(0, 0); // NOWA WŁAŚCIWOŚĆ
        public bool WasWindowMaximized { get; init; } = false; // NOWA WŁAŚCIWOŚĆ
        public Guid? LastActiveUnitId { get; init; }

        // NOWA WŁAŚCIWOŚĆ: Przechowuje listę ustawień priorytetów
        public List<PrioritySetting> Priorities { get; init; } = new();
    }

    public record WindowSize(int Width, int Height);

    public record WindowPosition(int X, int Y); // NOWY REKORD

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

            // ZMIANA: Upewnij się, że priorytety istnieją i są kompletne
            if (_currentSettings.Priorities == null || !_currentSettings.Priorities.Any())
            {
                // Jeśli lista jest pusta (nowe ustawienia), stwórz domyślną
                _currentSettings = _currentSettings with { Priorities = GetDefaultPriorities() };
            }
            else
            {
                // Jeśli lista istnieje, sprawdź, czy zawiera wszystkie możliwe priorytety
                // (na wypadek dodania nowych w przyszłych wersjach enum SolverPriority)
                var existingPriorities = _currentSettings.Priorities.Select(p => p.Priority).ToHashSet();
                var allPriorities = (SolverPriority[])Enum.GetValues(typeof(SolverPriority));
                bool needsUpdate = false;
                var updatedList = new List<PrioritySetting>(_currentSettings.Priorities);

                foreach (var priority in allPriorities)
                {
                    if (!existingPriorities.Contains(priority))
                    {
                        updatedList.Add(new PrioritySetting(priority, true)); // Nowe priorytety domyślnie aktywne
                        needsUpdate = true;
                    }
                }
                if (needsUpdate)
                {
                    _currentSettings = _currentSettings with { Priorities = updatedList };
                }
            }

            return _currentSettings;
        }

        public void SaveSettings(AppSettings settings)
        {
            if (settings == null) throw new ArgumentNullException(nameof(settings));
            _currentSettings = settings;
            var json = JsonSerializer.Serialize(settings, new JsonSerializerOptions { WriteIndented = true });
            // Używamy synchronicznej metody zapisu do pliku
            File.WriteAllText(_settingsPath, json);
        }

        public async Task SaveSettingsAsync(AppSettings settings)
        {
            if (settings == null) throw new ArgumentNullException(nameof(settings));
            _currentSettings = settings;
            var json = JsonSerializer.Serialize(settings, new JsonSerializerOptions { WriteIndented = true });
            await File.WriteAllTextAsync(_settingsPath, json);
        }

        // NOWA METODA: Tworzy domyślną, początkową listę priorytetów
        private List<PrioritySetting> GetDefaultPriorities()
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
    }
}