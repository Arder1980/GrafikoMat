using GrafikoMat.Core.Data;
using GrafikoMat.Core.Repositories;
using GrafikoMat.Services;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using SbClient = Supabase.Client;

namespace GrafikoMat.Repositories
{
    public class SupabaseDeclarationRepository : IDeclarationRepository
    {
        private readonly SupabaseService _supabaseService;
        private SbClient _supabase => _supabaseService.Client!;

        public SupabaseDeclarationRepository(SupabaseService supabaseService)
        {
            _supabaseService = supabaseService ?? throw new ArgumentNullException(nameof(supabaseService));
        }

        public async Task<List<Declaration>> GetDeclarationsForUnitMonthAsync(Guid unitId, int year, int month)
        {
            if (_supabase == null)
                throw new InvalidOperationException("Klient Supabase nie jest zainicjalizowany.");

            var response = await _supabase
                .From<Declaration>()
                .Where(d => d.UnitId == unitId)
                .Where(d => d.Year == year)
                .Where(d => d.Month == month)
                .Get();

            return response.Models ?? new List<Declaration>();
        }

        public async Task<Declaration?> GetDeclarationForDoctorAsync(Guid unitId, Guid doctorId, int year, int month)
        {
            if (_supabase == null)
                throw new InvalidOperationException("Klient Supabase nie jest zainicjalizowany.");

            try
            {
                var response = await _supabase
                    .From<Declaration>()
                    .Where(d => d.UnitId == unitId)
                    .Where(d => d.DoctorId == doctorId)
                    .Where(d => d.Year == year)
                    .Where(d => d.Month == month)
                    .Limit(1)
                    .Single();

                return response;
            }
            catch (Exception)
            {
                // Jeśli nie ma deklaracji dla tego lekarza/jednostki/miesiąca, zwróć null
                return null;
            }
        }

        public async Task<Declaration> SaveDeclarationAsync(Declaration declaration)
        {
            if (_supabase == null)
                throw new InvalidOperationException("Klient Supabase nie jest zainicjalizowany.");

            declaration.LastModified = DateTime.UtcNow;

            // Sprawdź czy deklaracja już istnieje
            if (declaration.Id > 0)
            {
                // Aktualizacja istniejącej deklaracji
                await _supabase
                    .From<Declaration>()
                    .Where(d => d.Id == declaration.Id)
                    .Update(declaration);

                return declaration;
            }
            else
            {
                // Wstawianie nowej deklaracji
                var response = await _supabase
                    .From<Declaration>()
                    .Insert(declaration);

                var insertedDeclaration = response.Models?.FirstOrDefault();
                if (insertedDeclaration == null)
                    throw new Exception("Nie udało się wstawić deklaracji do bazy danych.");

                return insertedDeclaration;
            }
        }

        public async Task DeleteDeclarationAsync(long declarationId)
        {
            if (_supabase == null)
                throw new InvalidOperationException("Klient Supabase nie jest zainicjalizowany.");

            await _supabase
                .From<Declaration>()
                .Where(d => d.Id == declarationId)
                .Delete();
        }
    }
}
