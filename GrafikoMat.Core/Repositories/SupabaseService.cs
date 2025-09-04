using GrafikoMat.Core.Repositories;
using GrafikoMat.Repositories;
// using Supabase; // W przyszłości odkomentujemy po dodaniu paczki NuGet

namespace GrafikoMat.Services
{
    /// <summary>
    /// Centralny punkt dostępu do backendu Supabase.
    /// Inicjalizuje klienta i udostępnia poszczególne repozytoria.
    /// </summary>
    public sealed class SupabaseService
    {
        // private readonly Client _client;

        public IDoctorRepository Doctors { get; }

        public SupabaseService()
        {
            var url = "https://TWOJ_PROJEKT.supabase.co";
            var apiKey = "TWOJ_KLUCZ_API_ANON";

            // _client = new Client(url, apiKey);

            // Inicjalizujemy implementacje repozytoriów, wstrzykując im klienta Supabase
            // Doctors = new SupabaseDoctorRepository(_client);
            Doctors = new SupabaseDoctorRepository(); // Tymczasowo
        }
    }
}