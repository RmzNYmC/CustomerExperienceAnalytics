using System;
using System.ComponentModel.DataAnnotations;

namespace CEA.Core.Entities
{
    public class Setting : BaseEntity
    {
        [Required]
        [MaxLength(100)]
        public string Key { get; set; } = string.Empty;

        [Required]
        public string Value { get; set; } = string.Empty;

        [MaxLength(255)]
        public string? Description { get; set; }

        [MaxLength(50)]
        public string Category { get; set; } = "General"; // SMTP, General, Security vb.

        public bool IsEncrypted { get; set; } = false; // Şifre için
    }
}