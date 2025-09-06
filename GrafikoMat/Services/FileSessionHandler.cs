using System;
using System.IO;
using System.Text.Json;
using Supabase.Gotrue;
using Supabase.Gotrue.Interfaces;

namespace GrafikoMat.Services
{
    /// <summary>
    /// Plikowa trwałość sesji zgodna z interfejsem IGotrueSessionPersistence{Session}.
    /// Zapis/odczyt do %LocalAppData%\GrafikoMat\supabase_session.json (synchronnie, by pasować do interfejsu).
    /// </summary>
    public sealed class FileSessionHandler : IGotrueSessionPersistence<Session>
    {
        private static readonly string AppDir =
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "GrafikoMat");
        private static readonly string SessionPath = Path.Combine(AppDir, "supabase_session.json");

        /// <summary>Zapisuje sesję (synchronnie).</summary>
        public void SaveSession(Session session)
        {
            if (session == null) return;
            Directory.CreateDirectory(AppDir);
            var json = JsonSerializer.Serialize(session);
            File.WriteAllText(SessionPath, json);
        }

        /// <summary>Ładuje sesję (synchronnie). Zwraca null, jeśli jej nie ma lub plik jest uszkodzony.</summary>
        public Session? LoadSession()
        {
            try
            {
                if (!File.Exists(SessionPath)) return null;
                var json = File.ReadAllText(SessionPath);
                if (string.IsNullOrWhiteSpace(json)) return null;
                return JsonSerializer.Deserialize<Session>(json);
            }
            catch
            {
                return null;
            }
        }

        /// <summary>Usuwa zapisaną sesję (synchronnie).</summary>
        public void DestroySession()
        {
            try
            {
                if (File.Exists(SessionPath))
                    File.Delete(SessionPath);
            }
            catch
            {
                // Ignorujemy – brak sesji w pliku i tak jest stanem „wylogowany”.
            }
        }
    }
}
