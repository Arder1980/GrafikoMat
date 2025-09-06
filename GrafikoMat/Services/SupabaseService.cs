using GrafikoMat.Core.Repositories;
using GrafikoMat.Repositories;
using Supabase;

namespace GrafikoMat.Services
{
    /// <summary>
    /// Centralny punkt dostępu do backendu Supabase.
    /// </summary>
    public sealed class SupabaseService
    {
        private Client? _client;

        /// <summary>
        /// Publiczna właściwość zapewniająca dostęp do klienta Supabase.
        /// </summary>
        public Client? Client => _client;

        public IDoctorRepository? Doctors { get; private set; }

        /// <summary>
        /// Inicjalizuje serwis (i wszystkie repozytoria) dla globalnego połączenia.
        /// </summary>
        public void Initialize(string url, string apiKey)
        {
            if (string.IsNullOrWhiteSpace(url) || string.IsNullOrWhiteSpace(apiKey))
            {
                _client = null;
                Doctors = null;
                return;
            }

            var options = new SupabaseOptions
            {
                AutoRefreshToken = true,
                AutoConnectRealtime = true
            };
            _client = new Client(url, apiKey, options);

            Doctors = new SupabaseDoctorRepository(_client);
        }
    }
}