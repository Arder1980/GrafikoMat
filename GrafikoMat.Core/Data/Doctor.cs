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

        [Column("requires_password_change")]
        public bool RequiresPasswordChange { get; set; }

        [Column("created_at")]
        public DateTime CreatedAt { get; set; }

        // ZMIANA: Dodajemy nową, wygodną właściwość
        public string FullName => $"{LastName} {FirstName}";

        public object Clone()
        {
            return this.MemberwiseClone();
        }
    }
}