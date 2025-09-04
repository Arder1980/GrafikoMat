using System;

namespace GrafikoMat.Core.Data
{
    /// <summary>
    /// Reprezentuje pojedynczego lekarza/dyżurnego w systemie.
    /// To jest główny model danych, który będzie mapowany na tabelę w bazie danych.
    /// </summary>
    public sealed class Doctor
    {
        /// <summary>Klucz główny (UUID w Supabase).</summary>
        public Guid Id { get; set; }

        /// <summary>Pełne imię i nazwisko.</summary>
        public string FullName { get; set; } = string.Empty;

        /// <summary>Email używany do logowania.</summary>
        public string Email { get; set; } = string.Empty;
    }
}