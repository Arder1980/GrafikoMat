using GrafikoMat.Core.Data;
using Supabase.Postgrest.Attributes;
using Supabase.Postgrest.Models;
using System;
using System.Collections.Generic;
using GrafikoMat.Core.Scheduling;

namespace GrafikoMat.Core.Declarations
{
    // NOWA KLASA - DTO (Data Transfer Object) do aktualizacji danych lekarza.
    // Zawiera tylko te pola, które mają swoje odpowiedniki w bazie danych.
    [Table("doctors")]
    public class DoctorForUpdate : BaseModel
    {
        [PrimaryKey("id", shouldInsert: false)] // Klucz główny, ale nie wstawiamy go przy tworzeniu
        public Guid Id { get; set; }

        [Column("first_name")]
        public string FirstName { get; set; } = string.Empty;

        [Column("last_name")]
        public string LastName { get; set; } = string.Empty;

        [Column("abbreviation")]
        public string Abbreviation { get; set; } = string.Empty;

        [Column("email")]
        public string Email { get; set; } = string.Empty;

        [Column("is_admin")]
        public bool IsAdmin { get; set; }

        [Column("is_archived")]
        public bool IsArchived { get; set; }

        [Column("requires_password_change")]
        public bool RequiresPasswordChange { get; set; }

        // Celowo pomijamy 'created_at' i 'FullName'
    }


    /// <summary>
    /// Deklaracje dyżurowe lekarza dla jednego miesiąca (wejście do generatora).
    /// </summary>
    public sealed class DoctorDeclarationMonth
    {
        /// <summary>Id lekarza (np. GUID lub skrót – format po Twojej stronie).</summary>
        public string DoctorId { get; init; } = string.Empty;

        /// <summary>Rok/miesiąc, którego dotyczą deklaracje.</summary>
        public int Year { get; init; }
        public int Month { get; init; }

        /// <summary>Limit dyżurów dla tego lekarza w tym miesiącu.</summary>
        public int DutyLimit { get; set; }

        /// <summary>Lista wpisów dziennych/slotowych z symbolami deklaracji.</summary>
        public List<DeclarationEntry> Entries { get; } = new();

        public DoctorDeclarationMonth(string doctorId, int year, int month, int dutyLimit = 0)
        {
            DoctorId = doctorId;
            Year = year;
            Month = month;
            DutyLimit = dutyLimit;
        }
    }

    /// <summary>
    /// Pojedyncza deklaracja na slot: symbol jednoliterowy + opcjonalny preferowany współdyżurny.
    /// </summary>
    public sealed class DeclarationEntry
    {
        /// <summary>Slot (dzień + część doby).</summary>
        public DutySlot Slot { get; init; }

        /// <summary>
        /// Jednoliterowy symbol deklaracji (np. 'D', 'N', 'X', 'P' – zgodnie z Twoją legendą).
        /// </summary>
        public char Symbol { get; set; }

        /// <summary>
        /// Opcjonalny preferowany współdyżurny (co-dyżurny) – preferencja wejściowa.
        /// Generator może, ale nie musi ją spełnić.
        /// </summary>
        public string? CoDutyPreferredDoctorId { get; set; }

        public DeclarationEntry(DutySlot slot, char symbol, string? coDutyPreferredDoctorId = null)
        {
            Slot = slot;
            Symbol = symbol;
            CoDutyPreferredDoctorId = coDutyPreferredDoctorId;
        }
    }
}