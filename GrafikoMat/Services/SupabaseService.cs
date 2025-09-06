using GrafikoMat.Core.Repositories;
using GrafikoMat.Repositories;
using Supabase;
using SbClient = Supabase.Client;

namespace GrafikoMat.Services
{
    public sealed class SupabaseService
    {
        private SbClient? _client;
        public SbClient? Client => _client;

        public IDoctorRepository? Doctors { get; private set; }

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
                // Uwaga: W Twojej wersji SDK nie ma właściwości SessionPersistence w SupabaseOptions.
                // Trwałość sesji (FileSessionHandler) podpinamy/wywołujemy ręcznie w miejscach logowania/wylogowania.
            };

            _client = new SbClient(url, apiKey, options);

            // Repozytoria korzystają z jednego klienta
            Doctors = new SupabaseDoctorRepository(_client);
        }
    }
}
