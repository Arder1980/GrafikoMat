using Supabase.Postgrest.Attributes;
using Supabase.Postgrest.Models;
using System;
using System.Text.Json.Serialization;

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

        // ================== ZMIANA: Zastąpienie IsAdmin przez AdminLevel ==================
        [Column("admin_level")]
        public int AdminLevel { get; set; } = 0;

        [JsonIgnore]
        public bool IsAdmin => AdminLevel > 0;
        // =================================================================================

        [Column("is_archived")]
        public bool IsArchived { get; set; } = false;

        [Column("requires_password_change")]
        public bool RequiresPasswordChange { get; set; }

        [Column("created_at")]
        public DateTimeOffset CreatedAt { get; set; }

        [JsonIgnore]
        public string FullName => $"{LastName} {FirstName}";

        public object Clone()
        {
            return this.MemberwiseClone();
        }
    }

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

        // ================== ZMIANA: Zastąpienie IsAdmin przez AdminLevel ==================
        [Column("admin_level")]
        public int AdminLevel { get; set; }
        // =================================================================================

        [Column("is_archived")]
        public bool IsArchived { get; set; }

        [Column("requires_password_change")]
        public bool RequiresPasswordChange { get; set; }
    }
}