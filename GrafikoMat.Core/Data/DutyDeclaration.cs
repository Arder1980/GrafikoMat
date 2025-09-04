using System;

namespace GrafikoMat.Core.Data
{
    /// <summary>
    /// Reprezentuje kompletny zestaw deklaracji jednego lekarza na jeden miesiąc.
    /// To jest główny model danych, który będzie mapowany na tabelę w bazie danych.
    /// </summary>
    public sealed class DutyDeclaration
    {
        /// <summary>Klucz główny (auto-inkrementowany lub UUID).</summary>
        public long Id { get; set; }

        /// <summary>Klucz obcy wskazujący na lekarza.</summary>
        public Guid DoctorId { get; set; }

        /// <summary>Rok, którego dotyczy deklaracja.</summary>
        public int Year { get; set; }

        /// <summary>Miesiąc, którego dotyczy deklaracja.</summary>
        public int Month { get; set; }

        /// <summary>Dane deklaracji, np. w formacie JSON, aby zachować elastyczność.</summary>
        public string? DeclarationDataJson { get; set; }

        /// <summary>Data ostatniej modyfikacji.</summary>
        public DateTimeOffset LastModified { get; set; }
    }
}