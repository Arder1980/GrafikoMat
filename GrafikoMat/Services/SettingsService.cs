using System;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using Windows.Storage;

namespace GrafikoMat.Services
{
    /// <summary>
    /// Model przechowujący ustawienia aplikacji. Użycie 'record' upraszcza definicję.
    /// </summary>
    public record AppSettings
    {
        public string UnitName { get; init; } = "Moja Jednostka Medyczna";
        public string Theme { get; init; } = "Light"; // "Light" or "Dark"
        public string Engine { get; init; } = "Klasyczny";
        public string PriorityOrder { get; init; } = "Dostępność > Sprawiedliwość > Preferencje";
        public WindowSize LastWindowSize { get; init; } = new(1600, 1000);
    }

    public record WindowSize(int Width, int Height);

    /// <summary>
    /// Serwis do zarządzania lokalnymi ustawieniami aplikacji, zapisywanymi w pliku JSON.
    /// </summary>
    public sealed class SettingsService
    {
        private const string SETTINGS_FILENAME = "settings.json";
        private static readonly string _settingsPath = Path.Combine(ApplicationData.Current.LocalFolder.Path, SETTINGS_FILENAME);

        private AppSettings? _currentSettings;

        /// <summary>
        /// Asynchronicznie wczytuje ustawienia z pliku lub zwraca domyślne, jeśli plik nie istnieje.
        /// </summary>
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
            catch (Exception ex)
            {
                // TODO: Dodać logowanie błędu
                _currentSettings = null;
            }

            _currentSettings ??= new AppSettings();
            return _currentSettings;
        }

        /// <summary>
        /// Zapisuje bieżący obiekt ustawień do pliku JSON.
        /// </summary>
        public async Task SaveSettingsAsync(AppSettings settings)
        {
            if (settings == null) throw new ArgumentNullException(nameof(settings));

            _currentSettings = settings;
            var json = JsonSerializer.Serialize(settings, new JsonSerializerOptions { WriteIndented = true });
            await File.WriteAllTextAsync(_settingsPath, json);
        }
    }
}