using System;
using System.Diagnostics;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using Windows.Storage;
using Supabase.Gotrue;

namespace GrafikoMat.Services
{
    /// <summary>
    /// Zapis/odczyt sesji Supabase do pliku JSON w LocalFolder.
    /// Bez implementowania interfejsów SDK – wywoływany ręcznie z SupabaseService.
    /// </summary>
    public sealed class FileSessionHandler
    {
        private const string FileName = "supabase_session.json";
        private static readonly string SessionPath =
            Path.Combine(ApplicationData.Current.LocalFolder.Path, FileName);

        private static readonly JsonSerializerOptions JsonOpts = new()
        {
            WriteIndented = false,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
            PropertyNameCaseInsensitive = true
        };

        public async Task SaveAsync(Session session)
        {
            if (session is null)
            {
                Debug.WriteLine("[FileSessionHandler] SaveAsync: brak sesji do zapisania.");
                return;
            }

            try
            {
                var json = JsonSerializer.Serialize(session, JsonOpts);
                await File.WriteAllTextAsync(SessionPath, json).ConfigureAwait(false);
                Debug.WriteLine($"[FileSessionHandler] Sesja ZAPISANA do pliku: {SessionPath}");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[FileSessionHandler] Błąd zapisu do {SessionPath}: {ex.Message}");
            }
        }
        public static string SessionFilePath => SessionPath;

        public async Task<Session?> LoadAsync()
        {
            try
            {
                if (!File.Exists(SessionPath))
                {
                    Debug.WriteLine($"[FileSessionHandler] LoadAsync: brak pliku {SessionPath}");
                    return null;
                }

                var json = await File.ReadAllTextAsync(SessionPath).ConfigureAwait(false);
                if (string.IsNullOrWhiteSpace(json))
                {
                    Debug.WriteLine($"[FileSessionHandler] LoadAsync: plik {SessionPath} pusty.");
                    return null;
                }

                var session = JsonSerializer.Deserialize<Session>(json, JsonOpts);
                Debug.WriteLine($"[FileSessionHandler] Sesja WCZYTANA z pliku: {SessionPath}");
                return session;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[FileSessionHandler] Błąd odczytu z {SessionPath}: {ex.Message}");
                return null;
            }
        }

        public void Delete()
        {
            try
            {
                if (File.Exists(SessionPath))
                {
                    File.Delete(SessionPath);
                    Debug.WriteLine($"[FileSessionHandler] Sesja USUNIĘTA (plik {SessionPath})");
                }
                else
                {
                    Debug.WriteLine($"[FileSessionHandler] Delete: plik {SessionPath} nie istnieje.");
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[FileSessionHandler] Błąd usuwania {SessionPath}: {ex.Message}");
            }
        }
    }
}
