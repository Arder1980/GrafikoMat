using System;
using System.Diagnostics;
using System.Reflection;
using System.Threading.Tasks;
using Supabase;
using Supabase.Gotrue;
using SbClient = Supabase.Client;

namespace GrafikoMat.Services
{
    public sealed class SupabaseService
    {
        private SbClient? _client;
        public SbClient? Client => _client;

        // ================== NOWE WŁAŚCIWOŚCI ==================
        public string? SupabaseUrl { get; private set; }
        public string? SupabaseAnonKey { get; private set; }
        // ======================================================

        public bool IsAuthenticated => _client?.Auth?.CurrentSession != null;

        public void Initialize(string url, string apiKey)
        {
            // Zapamiętujemy dane do późniejszego użytku
            this.SupabaseUrl = url;
            this.SupabaseAnonKey = apiKey;

            if (string.IsNullOrWhiteSpace(url) || string.IsNullOrWhiteSpace(apiKey))
            {
                _client = null;
                Debug.WriteLine("[SupabaseService] Initialize: brak URL/Key – klient wyłączony.");
                return;
            }

            var options = new SupabaseOptions
            {
                AutoRefreshToken = true,
                AutoConnectRealtime = true
            };
            _client = new SbClient(url, apiKey, options);
            Debug.WriteLine("[SupabaseService] Initialize: klient utworzony.");
        }

        public Task<bool> RestoreSessionIfAnyAsync()
        {
            Debug.WriteLine("[SupabaseService] Restore: START");
            if (_client is null)
            {
                Debug.WriteLine("[SupabaseService] Restore: brak klienta – PRZERWANE");
                return Task.FromResult(false);
            }

            return RestoreSessionInternalAsync();
        }

        private async Task<bool> RestoreSessionInternalAsync()
        {
            var fh = new FileSessionHandler();
            var saved = await fh.LoadAsync().ConfigureAwait(false);
            Debug.WriteLine("[SupabaseService] Restore: LoadAsync() -> " + (saved is null ? "NULL" : "OK"));
            if (saved is null || _client is null) return false;

            var auth = _client.Auth;
            var authType = auth.GetType();
            try
            {
                var setSession3 = authType.GetMethod("SetSession", BindingFlags.Public | BindingFlags.Instance, null, new[] { typeof(string), typeof(string), typeof(bool) }, null);
                if (setSession3 != null)
                {
                    Debug.WriteLine("[SupabaseService] Restore: SetSession(access,refresh,force=true) AWAIT...");
                    await InvokeAwaitIfTask(setSession3, auth, saved.AccessToken, saved.RefreshToken, true);
                    if (auth.CurrentSession != null)
                    {
                        await SaveCurrentSessionAsync().ConfigureAwait(false);
                        return true;
                    }
                }

                var refreshToken2 = authType.GetMethod("RefreshToken", BindingFlags.Public | BindingFlags.Instance, null, new[] { typeof(string), typeof(string) }, null);
                if (refreshToken2 != null)
                {
                    Debug.WriteLine("[SupabaseService] Restore: RefreshToken(access,refresh) AWAIT...");
                    await InvokeAwaitIfTask(refreshToken2, auth, saved.AccessToken, saved.RefreshToken);
                    if (auth.CurrentSession != null)
                    {
                        await SaveCurrentSessionAsync().ConfigureAwait(false);
                        return true;
                    }
                }

                var refreshNoArgs = authType.GetMethod("RefreshSession", BindingFlags.Public | BindingFlags.Instance, null, Type.EmptyTypes, null);
                if (refreshNoArgs != null)
                {
                    Debug.WriteLine("[SupabaseService] Restore: RefreshSession() AWAIT...");
                    await InvokeAwaitIfTask(refreshNoArgs, auth);
                    if (auth.CurrentSession != null)
                    {
                        await SaveCurrentSessionAsync().ConfigureAwait(false);
                        return true;
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine("[SupabaseService] Restore: EX -> " + ex.Message);
            }

            Debug.WriteLine("[SupabaseService] Restore: END -> FALSE");
            return false;
        }

        static async Task InvokeAwaitIfTask(MethodInfo mi, object target, params object[] args)
        {
            var result = mi.Invoke(target, args ?? Array.Empty<object>());
            if (result is Task t) await t.ConfigureAwait(false);
        }

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

        public async Task SignOutAsync()
        {
            try
            {
                if (_client != null)
                    await _client.Auth.SignOut();
            }
            catch { }

            try
            {
                new FileSessionHandler().Delete();
            }
            catch { }

            Debug.WriteLine("[SupabaseService] SignOut: SDK sign-out + usunięty plik sesji.");
        }

        public async Task UpdateUserPasswordAsync(string newPassword)
        {
            if (Client is null || !IsAuthenticated)
                throw new InvalidOperationException("Użytkownik nie jest zalogowany.");
            var attributes = new UserAttributes { Password = newPassword };
            await Client.Auth.Update(attributes);
        }
    }
}