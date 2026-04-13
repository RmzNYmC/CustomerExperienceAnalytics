using System.ComponentModel.DataAnnotations;

namespace CEA.Core.Entities
{
    public class AuditLog : BaseEntity
    {
        [MaxLength(50)]
        public string Action { get; set; } = string.Empty; // Create, Update, Delete, Assign

        [MaxLength(100)]
        public string EntityType { get; set; } = string.Empty; // Complaint, Survey, Customer

        public int? EntityId { get; set; }

        [MaxLength(1000)]
        public string? OldValues { get; set; } // JSON

        [MaxLength(1000)]
        public string? NewValues { get; set; } // JSON

        [MaxLength(100)]
        public string? UserId { get; set; }

        [MaxLength(255)]
        public string? UserName { get; set; }

        [MaxLength(50)]
        public string? IpAddress { get; set; }

        public bool Success { get; set; } = true;

        [MaxLength(500)]
        public string? ErrorMessage { get; set; }
    }
}