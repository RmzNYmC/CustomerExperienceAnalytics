using CEA.Core.Enum;
using CEA.Core.Enums;

namespace CEA.Core.Entities
{
    public class Complaint : BaseEntity
    {
        public string TicketNumber { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public ComplaintStatus Status { get; set; } = ComplaintStatus.New;
        public string Category { get; set; } = string.Empty;
        public string Priority { get; set; } = "Medium";

        public string CustomerEmail { get; set; } = string.Empty;
        public string? CustomerName { get; set; }
        public string? CustomerPhone { get; set; }
        public bool CustomerNotified { get; set; } = false;

        public string? AssignedToUserId { get; set; }
        public DateTime? AssignedAt { get; set; }
        public DateTime? DueDate { get; set; }
        public DateTime? ResolvedAt { get; set; }
        public string? ResolutionNotes { get; set; }

        // Foreign Keys
        public int SurveyResponseId { get; set; }
        public int TriggerQuestionId { get; set; }

        // Navigation Properties
        public SurveyResponse SurveyResponse { get; set; } = null!;
        public Question TriggerQuestion { get; set; } = null!;
        public ApplicationUser? AssignedToUser { get; set; }
        public ICollection<ComplaintNote> Notes { get; set; } = new List<ComplaintNote>();
    }
}