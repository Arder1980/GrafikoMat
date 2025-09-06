using Postgrest.Attributes;
using Postgrest.Models;
using System;

namespace GrafikoMat.Core.Data
{
    /// <summary>
    /// Reprezentuje globalny profil dyżurnego w systemie.
    /// Mapuje się na tabelę 'doctors' w bazie danych.
    /// </summary>
    [Table("doctors")]
    public class DoctorProfile : BaseModel
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

        [Column("created_at")]
        public DateTime CreatedAt { get; set; }
    }
}