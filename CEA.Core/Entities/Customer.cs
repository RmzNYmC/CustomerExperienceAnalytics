using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace CEA.Core.Entities
{
    public class Customer : BaseEntity
    {
        [Required(ErrorMessage = "E-posta adresi zorunludur.")]
        [EmailAddress(ErrorMessage = "Geçerli bir e-posta adresi giriniz.")]
        [StringLength(255)]
        public string Email { get; set; } = string.Empty;

        [Required(ErrorMessage = "İsim zorunludur.")]
        [StringLength(200)]
        public string Name { get; set; } = string.Empty;

        [StringLength(200)]
        public string? CompanyName { get; set; }

        [StringLength(50)]
        public string? Phone { get; set; }

        [StringLength(100)]
        public string? Segment { get; set; } // "VIP", "Standard", "Enterprise", "B2B"

        [StringLength(500)]
        public string? Notes { get; set; }

        public bool EmailVerified { get; set; } = false;
        public bool BounceEmail { get; set; } = false;

        // Navigation
        public ICollection<SurveyResponse> Responses { get; set; } = new List<SurveyResponse>();
    }
}