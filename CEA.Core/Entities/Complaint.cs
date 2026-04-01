using CEA.Core.Enum;
using CEA.Core.Enums;

namespace CEA.Core.Entities
{
    public class Complaint : BaseEntity
    {
        // Temel Bilgiler
        public string TicketNumber { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public ComplaintStatus Status { get; set; } = ComplaintStatus.New;
        public string Category { get; set; } = string.Empty;
        public string Priority { get; set; } = "Medium";

        // Müşteri Bilgileri
        public string CustomerEmail { get; set; } = string.Empty;
        public string? CustomerName { get; set; }
        public string? CustomerPhone { get; set; }
        public bool CustomerNotified { get; set; } = false;

        // Atama Bilgileri
        public string? AssignedToUserId { get; set; }
        public DateTime? AssignedAt { get; set; }

        // SLA Alanları (YENİ EKLENENLER)
        public DateTime? DueDate { get; set; }                    // SLA Hedef Tarihi
        public int? ResponseTimeMinutes { get; set; }           // İlk yanıt süresi
        public int? ResolutionTimeMinutes { get; set; }           // Toplam çözüm süresi
        public bool IsSlaBreached { get; set; } = false;          // SLA aşıldı mı?
        public string? BreachReason { get; set; }                // Aşım sebebi

        // Çözüm/Kapanış Bilgileri
        public DateTime? ResolvedAt { get; set; }
        public DateTime? ClosedAt { get; set; }                  // Tamamen kapatma tarihi
        public string? ResolutionNotes { get; set; }
        public string? ResolutionType { get; set; }              // "Çözüldü", "Reddedildi", vb.

        // Kategori ve Kaynak (YENİ)
        public string? SubCategory { get; set; }                 // Alt kategori
        public string? Source { get; set; } = "Survey";           // "Survey", "Email", "Phone", "Web"

        // Foreign Keys
        public int SurveyResponseId { get; set; }
        public int TriggerQuestionId { get; set; }

        // Navigation Properties
        public SurveyResponse SurveyResponse { get; set; } = null!;
        public Question TriggerQuestion { get; set; } = null!;
        public ApplicationUser? AssignedToUser { get; set; }
        public ICollection<ComplaintNote> Notes { get; set; } = new List<ComplaintNote>();

        // Hesaplanmış Properties (Read-only)
        public string TimeElapsedDisplay => GetTimeElapsedDisplay();
        public string SlaStatusDisplay => GetSlaStatusDisplay();

        private string GetTimeElapsedDisplay()
        {
            var endTime = ResolvedAt ?? DateTime.Now;
            var duration = endTime - CreatedAt;

            if (duration.TotalDays >= 1)
                return $"{duration.TotalDays:F1} gün";
            if (duration.TotalHours >= 1)
                return $"{duration.TotalHours:F1} saat";
            return $"{duration.TotalMinutes:F0} dk";
        }

        private string GetSlaStatusDisplay()
        {
            if (IsSlaBreached) return "SLA Aşıldı 🔴";
            if (DueDate.HasValue && DateTime.Now > DueDate.Value.AddMinutes(-30)) return "Acil 🟡";
            if (Status == ComplaintStatus.Resolved || Status == ComplaintStatus.Closed) return "Zamanında ✅";
            return "Devam Ediyor 🟢";
        }
    }
}