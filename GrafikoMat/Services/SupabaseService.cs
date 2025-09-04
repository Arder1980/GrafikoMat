using GrafikoMat.Core.Repositories;
using GrafikoMat.Repositories;
using Supabase;

namespace GrafikoMat.Services
{
    /// <summary>
    /// Centralny punkt dostępu do backendu Supabase.
    /// TERAZ jest inicjalizowany dynamicznie na podstawie wybranego profilu.
    /// </summary>
    public sealed class SupabaseService
    {
        private Client? _client;

        // ZMIANA: Repozytoria są teraz 'nullable', bo mogą nie być zainicjalizowane na starcie
        public IDoctorRepository? Doctors { get; private set; }
        // public IDeclarationRepository? Declarations { get; private set; } // W przyszłości

        /// <summary>
        /// Inicjalizuje serwis (i wszystkie repozytoria) dla konkretnego profilu jednostki.
        /// Ta metoda będzie wywoływana przy starcie aplikacji i przy każdej zmianie jednostki.
        /// </summary>
        public void Initialize(UnitProfile profile)
        {
            if (string.IsNullOrWhiteSpace(profile.SupabaseUrl) || string.IsNullOrWhiteSpace(profile.SupabaseApiKey))
            {
                // Jeśli profil nie ma danych, de-inicjalizujemy serwis
                _client = null;
                Doctors = null;
                return;
            }

            var options = new SupabaseOptions
            {
                AutoRefreshToken = true,
                AutoConnectRealtime = true
            };

            _client = new Client(profile.SupabaseUrl, profile.SupabaseApiKey, options);

            Doctors = new SupabaseDoctorRepository(_client);
        }
    }
}