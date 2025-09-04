using Postgrest.Models;
using System;

namespace GrafikoMat.Core.Data
{
    /// <summary>
    /// Reprezentuje pojedynczego lekarza/dyżurnego w systemie.
    /// To jest główny model danych, który będzie mapowany na tabelę w bazie danych.
    /// </summary>
    [Postgrest.Attributes.Table("doctors")] // ZMIANA: Pełna nazwa, aby usunąć niejednoznaczność
    public class Doctor : BaseModel
    {
        /// <summary>Klucz główny (UUID w Supabase).</summary>
        [Postgrest.Attributes.PrimaryKey("id", false)] // ZMIANA: Pełna nazwa
        public Guid Id { get; set; }

        /// <summary>Pełne imię i nazwisko.</summary>
        [Postgrest.Attributes.Column("full_name")] // ZMIANA: Pełna nazwa
        public string FullName { get; set; } = string.Empty;

        /// <summary>Email używany do logowania.</summary>
        [Postgrest.Attributes.Column("email")] // ZMIANA: Pełna nazwa
        public string Email { get; set; } = string.Empty;
    }
}