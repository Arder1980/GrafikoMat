using Supabase.Gotrue;
using Supabase.Gotrue.Interfaces;
using System;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using Windows.Storage;

namespace GrafikoMat.Services
{
    /// <summary>
    /// Implementacja obsługi sesji, która zapisuje i odczytuje sesję z lokalnego pliku.
    /// </summary>
    public class FileSessionHandler : ISupabaseSessionHandler
    {
        private const string SESSION_FILENAME = "user_session.json";
        private static readonly string _sessionPath = Path.Combine(ApplicationData.Current.LocalFolder.Path, SESSION_FILENAME);

        /// <summary>
        /// Wywoływane przez klienta Supabase, gdy sesja (tokeny) zostanie pomyślnie pobrana.
        /// </summary>
        public async void SaveSession(Session session)
        {
            try
            {
                var json = JsonSerializer.Serialize(session);
                await File.WriteAllTextAsync(_sessionPath, json);
            }
            catch (Exception)
            {
                // W docelowej aplikacji warto tu dodać logowanie błędów
            }
        }

        /// <summary>
        /// Wywoływane przez klienta Supabase przy starcie, aby spróbować wczytać istniejącą sesję.
        /// </summary>
        public Session? LoadSession()
        {
            try
            {
                if (File.Exists(_sessionPath))
                {
                    var json = File.ReadAllText(_sessionPath);
                    return JsonSerializer.Deserialize<Session>(json);
                }
            }
            catch (Exception)
            {
                // Błąd odczytu - traktujemy jak brak sesji
            }
            return null;
        }

        /// <summary>
        /// Wywoływane, gdy użytkownik się wyloguje.
        /// </summary>
        public void DeleteSession()
        {
            try
            {
                if (File.Exists(_sessionPath))
                {
                    File.Delete(_sessionPath);
                }
            }
            catch (Exception)
            {
                // Obsługa błędu
            }
        }
    }
}