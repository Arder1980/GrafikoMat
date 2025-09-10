using Supabase.Postgrest.Attributes;
using Supabase.Postgrest.Models;
using System;

namespace GrafikoMat.Core.Data
{
    [Table("doctors")]
    public class DoctorProfile : BaseModel, ICloneable
    {
        [PrimaryKey("id")]
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
        public bool IsAdmin { get; set; } = false;

        [Column("is_archived")]
        public bool IsArchived { get; set; } = false;

        [Column("requires_password_change")]
        public bool RequiresPasswordChange { get; set; }

        [Column("created_at")]
        public DateTime CreatedAt { get; set; }

        public string FullName => $"{LastName} {FirstName}";

        public object Clone()
        {
            return this.MemberwiseClone();
        }
    }

    /// <summary>
    /// Klasa DTO (Data Transfer Object) używana wyłącznie do aktualizacji podzbioru danych lekarza w bazie.
    /// Zapobiega to nadpisywaniu przez pomyłkę kolumn zarządzanych przez bazę danych (np. created_at).
    /// </summary>
    [Table("doctors")]
    public class DoctorForUpdate : BaseModel
    {
        [PrimaryKey("id")]
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
    }
}