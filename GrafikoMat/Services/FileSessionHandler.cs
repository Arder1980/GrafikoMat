using System;
using System.Diagnostics;
using System.IO;
using System.Security.Cryptography;
using System.Text;
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
                var jsonBytes = Encoding.UTF8.GetBytes(json);

                // Szyfrowanie używając Windows DPAPI (Data Protection API)
                var encryptedBytes = ProtectedData.Protect(
                    jsonBytes,
                    null, // Brak dodatkowej entropii
                    DataProtectionScope.CurrentUser // Tylko aktualny użytkownik może odszyfrować
                );

                await File.WriteAllBytesAsync(SessionPath, encryptedBytes).ConfigureAwait(false);
                Debug.WriteLine($"[FileSessionHandler] Sesja ZASZYFROWANA i zapisana do pliku: {SessionPath}");
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

                var fileBytes = await File.ReadAllBytesAsync(SessionPath).ConfigureAwait(false);
                if (fileBytes == null || fileBytes.Length == 0)
                {
                    Debug.WriteLine($"[FileSessionHandler] LoadAsync: plik {SessionPath} pusty.");
                    return null;
                }

                string json;

                try
                {
                    // Próba odszyfrowania (nowy format)
                    var decryptedBytes = ProtectedData.Unprotect(
                        fileBytes,
                        null,
                        DataProtectionScope.CurrentUser
                    );

                    json = Encoding.UTF8.GetString(decryptedBytes);
                    Debug.WriteLine($"[FileSessionHandler] Sesja ODSZYFROWANA i wczytana z pliku: {SessionPath}");
                }
                catch (CryptographicException)
                {
                    // Jeśli deszyfrowanie nie powiodło się, spróbuj odczytać jako plain text (stary format)
                    Debug.WriteLine($"[FileSessionHandler] Nie można odszyfrować - próba odczytu jako plain text (stary format)");
                    json = Encoding.UTF8.GetString(fileBytes);

                    // Jeśli udało się odczytać jako plain text, automatycznie konwertuj na zaszyfrowany format
                    var session = JsonSerializer.Deserialize<Session>(json, JsonOpts);
                    if (session != null)
                    {
                        Debug.WriteLine($"[FileSessionHandler] Konwersja starego formatu na zaszyfrowany...");
                        await SaveAsync(session).ConfigureAwait(false);
                    }
                    return session;
                }

                if (string.IsNullOrWhiteSpace(json))
                {
                    Debug.WriteLine($"[FileSessionHandler] LoadAsync: odszyfrowany JSON pusty.");
                    return null;
                }

                var sessionResult = JsonSerializer.Deserialize<Session>(json, JsonOpts);
                return sessionResult;
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
