using GrafikoMat.Core.Scheduling.Models;
using System;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using Windows.Storage;

namespace GrafikoMat.Services
{
    public record AppSettings
    {
        public string SupabaseUrl { get; init; } = string.Empty;
        public string SupabaseAnonKey { get; init; } = string.Empty;
        public SolverType SelectedSolver { get; init; } = SolverType.Backtracking;
        public string Theme { get; init; } = "Light";
        public WindowSize LastWindowSize { get; init; } = new(1600, 1000);
        public Guid? LastActiveUnitId { get; init; }
    }

    public record WindowSize(int Width, int Height);

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
            if (settings == null) throw new ArgumentNullException(nameof(settings));
            _currentSettings = settings;
            var json = JsonSerializer.Serialize(settings, new JsonSerializerOptions { WriteIndented = true });
            await File.WriteAllTextAsync(_settingsPath, json);
        }
    }
}