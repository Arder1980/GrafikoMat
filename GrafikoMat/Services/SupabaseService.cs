using System;
using System.Diagnostics;
using System.Reflection;
using System.Threading.Tasks;
using GrafikoMat.Core.Repositories;
using GrafikoMat.Repositories;
using Supabase;
using Supabase.Gotrue;
using SbClient = Supabase.Client;

namespace GrafikoMat.Services
{
    /// <summary>
    /// Centralny serwis Supabase: inicjalizacja klienta, repozytoria oraz trwałość sesji (zapis/odczyt do pliku).
    /// Nie polega na interfejsach sesji z SDK – zapis/odczyt realizujemy ręcznie przez FileSessionHandler.
    /// </summary>
    public sealed class SupabaseService
    {
        private SbClient? _client;
        public SbClient? Client => _client;

        public IDoctorRepository? Doctors { get; private set; }

        /// <summary>
        /// Prosta informacja: czy SDK widzi bieżącą sesję po swojej stronie.
        /// </summary>
        public bool IsAuthenticated => _client?.Auth?.CurrentSession != null;

        /// <summary>
        /// Tworzy klienta Supabase. Wywołuj przy starcie lub po zmianie ustawień.
        /// </summary>
        public void Initialize(string url, string apiKey)
        {
            if (string.IsNullOrWhiteSpace(url) || string.IsNullOrWhiteSpace(apiKey))
            {
                _client = null;
                Doctors = null;
                Debug.WriteLine("[SupabaseService] Initialize: brak URL/Key – klient wyłączony.");
                return;
            }

            var options = new SupabaseOptions
            {
                AutoRefreshToken = true,
                AutoConnectRealtime = true
            };

            _client = new SbClient(url, apiKey, options);

            // Użyj istniejącego repozytorium opartego o Supabase z Twojego projektu.
            Doctors = new SupabaseDoctorRepository(_client);

            Debug.WriteLine("[SupabaseService] Initialize: klient utworzony.");
        }

        /// <summary>
        /// Alias dla RestoreSessionIfAnyAsync – tak, by MainWindow mógł wywołać starszą nazwę.
        /// </summary>
        public Task<bool> TryRestoreSessionAsync() => RestoreSessionIfAnyAsync();

        /// <summary>
        /// Próbuje wczytać sesję z pliku i wstrzyknąć ją do klienta Auth.
        /// Zwraca true, jeśli po operacji jesteśmy zalogowani (Auth.CurrentSession != null).
        /// </summary>
        public async Task<bool> RestoreSessionIfAnyAsync()
        {
            Debug.WriteLine("[SupabaseService] Restore: START");

            if (_client is null)
            {
                Debug.WriteLine("[SupabaseService] Restore: brak klienta – PRZERWANE");
                return false;
            }

            var fh = new FileSessionHandler();
            var saved = await fh.LoadAsync().ConfigureAwait(false);
            Debug.WriteLine("[SupabaseService] Restore: LoadAsync() -> " + (saved is null ? "NULL" : "OK"));

            if (saved is null) return false;

            var auth = _client.Auth;
            var authType = auth.GetType();

            // helper do refleksji z opcjonalnym await
            static async Task InvokeAwaitIfTask(System.Reflection.MethodInfo mi, object target, params object[] args)
            {
                var result = mi.Invoke(target, args);
                if (result is Task t) await t.ConfigureAwait(false);
            }

            try
            {
                // 1) NAJPIERW spróbuj SetSession(string,string,bool) – tego brakowało
                var setSession3 = authType.GetMethod(
                    "SetSession",
                    BindingFlags.Public | BindingFlags.Instance,
                    binder: null,
                    types: new[] { typeof(string), typeof(string), typeof(bool) },
                    modifiers: null);

                if (setSession3 != null)
                {
                    Debug.WriteLine("[SupabaseService] Restore: SetSession(access,refresh,force=true) AWAIT...");
                    await InvokeAwaitIfTask(setSession3, auth, saved.AccessToken, saved.RefreshToken, true);

                    if (auth.CurrentSession != null)
                    {
                        Debug.WriteLine("[SupabaseService] Restore: CurrentSession != null po SetSession(3).");
                        await SaveCurrentSessionAsync().ConfigureAwait(false);
                        Debug.WriteLine("[SupabaseService] Restore: END -> TRUE (po SetSession(3))");
                        return true;
                    }
                    else
                    {
                        Debug.WriteLine("[SupabaseService] Restore: nadal brak CurrentSession po SetSession(3).");
                    }
                }
                else
                {
                    Debug.WriteLine("[SupabaseService] Restore: Brak SetSession(string,string,bool) w SDK.");
                }

                // 2) Fallback: RefreshToken(string,string) – odśwież po tokenach z pliku
                var refreshToken2 = authType.GetMethod(
                    "RefreshToken",
                    BindingFlags.Public | BindingFlags.Instance,
                    binder: null,
                    types: new[] { typeof(string), typeof(string) },
                    modifiers: null);

                if (refreshToken2 != null)
                {
                    Debug.WriteLine("[SupabaseService] Restore: RefreshToken(access,refresh) AWAIT...");
                    await InvokeAwaitIfTask(refreshToken2, auth, saved.AccessToken, saved.RefreshToken);

                    if (auth.CurrentSession != null)
                    {
                        Debug.WriteLine("[SupabaseService] Restore: CurrentSession != null po RefreshToken(2).");
                        await SaveCurrentSessionAsync().ConfigureAwait(false);
                        Debug.WriteLine("[SupabaseService] Restore: END -> TRUE (po RefreshToken(2))");
                        return true;
                    }
                    else
                    {
                        Debug.WriteLine("[SupabaseService] Restore: nadal brak CurrentSession po RefreshToken(2).");
                    }
                }
                else
                {
                    Debug.WriteLine("[SupabaseService] Restore: Brak RefreshToken(string,string) w SDK.");
                }

                // 3) Ostateczny fallback: RefreshSession() – działa TYLKO gdy SDK już ma sesję w pamięci
                var refreshNoArgs = authType.GetMethod(
                    "RefreshSession",
                    BindingFlags.Public | BindingFlags.Instance,
                    binder: null,
                    types: Type.EmptyTypes,
                    modifiers: null);

                if (refreshNoArgs != null)
                {
                    Debug.WriteLine("[SupabaseService] Restore: RefreshSession() AWAIT...");
                    await InvokeAwaitIfTask(refreshNoArgs, auth);

                    if (auth.CurrentSession != null)
                    {
                        Debug.WriteLine("[SupabaseService] Restore: CurrentSession != null po RefreshSession().");
                        await SaveCurrentSessionAsync().ConfigureAwait(false);
                        Debug.WriteLine("[SupabaseService] Restore: END -> TRUE (po RefreshSession())");
                        return true;
                    }
                }
                else
                {
                    Debug.WriteLine("[SupabaseService] Restore: Brak RefreshSession() w SDK.");
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine("[SupabaseService] Restore: EX -> " + ex.Message);
            }

            Debug.WriteLine("[SupabaseService] Restore: END -> FALSE");
            return false;
        }

        /// <summary>
        /// Zapisuje bieżącą sesję do pliku (wołaj po udanym logowaniu oraz po odświeżeniu).
        /// </summary>
        public async Task SaveCurrentSessionAsync()
        {
            if (_client is null) return;
            var session = _client.Auth.CurrentSession;
            if (session is null)
            {
                Debug.WriteLine("[SupabaseService] SaveSession: brak bieżącej sesji.");
                return;
            }

            await new FileSessionHandler().SaveAsync(session).ConfigureAwait(false);
            Debug.WriteLine("[SupabaseService] SaveSession: sesja zapisana do pliku.");
        }

        /// <summary>
        /// Wylogowuje z Supabase i usuwa lokalny plik sesji.
        /// </summary>
        public async Task SignOutAndClearAsync()
        {
            if (_client != null)
            {
                try
                {
                    // W zależności od wersji SDK bywa SignOutAsync albo SignOut – obsłużmy obie.
                    var signOutAsync = _client.Auth.GetType().GetMethod("SignOutAsync",
                        BindingFlags.Public | BindingFlags.Instance, binder: null, types: Type.EmptyTypes, modifiers: null);

                    var signOut = _client.Auth.GetType().GetMethod("SignOut",
                        BindingFlags.Public | BindingFlags.Instance, binder: null, types: Type.EmptyTypes, modifiers: null);

                    if (signOutAsync != null)
                        await (Task)signOutAsync.Invoke(_client.Auth, null);
                    else if (signOut != null)
                        signOut.Invoke(_client.Auth, null);
                }
                catch (Exception ex)
                {
                    Debug.WriteLine("[SupabaseService] SignOut: wyjątek: " + ex.Message);
                }
            }

            new FileSessionHandler().Delete();
            Debug.WriteLine("[SupabaseService] SignOut: usunięto plik sesji.");
        }
        public async Task SignOutAsync()
        {
            try
            {
                if (_client != null)
                    await _client.Auth.SignOut(); // czyści sesję w SDK
            }
            catch { /* brak aktywnej sesji? pomijamy */ }

            try
            {
                new FileSessionHandler().Delete(); // usuń plik supabase_session.json
            }
            catch { /* pomijamy */ }

            System.Diagnostics.Debug.WriteLine("[SupabaseService] SignOut: SDK sign-out + usunięty plik sesji.");
        }

    }
}
