using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using Windows.Storage;

namespace GrafikoMat.Services
{
    public record UnitProfile
    {
        public Guid Id { get; init; } = Guid.NewGuid();
        public string Name { get; init; } = "Nowa jednostka";

        // ZMIANA: Dodajemy nowe pola dla profilu
        public string HospitalFullName { get; init; } = string.Empty;
        public string DepartmentName { get; init; } = string.Empty;

        public string SupabaseUrl { get; init; } = string.Empty;
        public string SupabaseApiKey { get; init; } = string.Empty;
    }

    public record AppSettings
    {
        public List<UnitProfile> UnitProfiles { get; init; } = new();
        public Guid? ActiveUnitProfileId { get; init; }

        // ZMIANA: Przenosimy globalne ustawienia tutaj
        public string Theme { get; init; } = "Light"; // "Light", "Dark", "System"
        public string EngineType { get; init; } = "Klasyczny";
        public string PriorityOrder { get; init; } = "Dostępność > Sprawiedliwość > Preferencje";

        public WindowSize LastWindowSize { get; init; } = new(1600, 1000);
    }

    public record WindowSize(int Width, int Height);

    public sealed class SettingsService
    {
        private const string SETTINGS_FILENAME = "settings.json";
        private static readonly string _settingsPath = Path.Combine(ApplicationData.Current.LocalFolder.Path, SETTINGS_FILENAME);
        private AppSettings? _currentSettings;

        public async Task<AppSettings> LoadSettingsAsync()
        {
            if (_currentSettings != null)
                return _currentSettings;

            try
            {
                if (File.Exists(_settingsPath))
                {
                    var json = await File.ReadAllTextAsync(_settingsPath);
                    _currentSettings = JsonSerializer.Deserialize<AppSettings>(json);
                }
            }
            catch (Exception) { _currentSettings = null; }

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