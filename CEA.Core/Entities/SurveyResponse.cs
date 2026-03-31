namespace CEA.Core.Entities
{
    public class SurveyResponse : BaseEntity
    {
        public DateTime SubmittedAt { get; set; } = DateTime.Now;
        public DateTime? CompletedAt { get; set; }

        public int? CustomerId { get; set; }
        public Customer? Customer { get; set; }
        public string? CustomerEmail { get; set; }
        public string? CustomerName { get; set; }
        public string? CustomerPhone { get; set; }
        public string? CustomerIdentifier { get; set; }
        public string? IpAddress { get; set; }
        public string? UserAgent { get; set; }

        public decimal? OverallSatisfactionScore { get; set; }
        public int? NpsScore { get; set; }
        public int ResponseYear { get; set; }
        public int ResponseMonth { get; set; }

        public int SurveyId { get; set; }
        public string? UserId { get; set; }

        public Survey Survey { get; set; } = null!;
        public ApplicationUser? User { get; set; }
        public ICollection<Answer> Answers { get; set; } = new List<Answer>();

        // DÜZELTİLDİ: Bu navigation'ı kaldır - çakışma yaratıyor
        // public ICollection<Complaint> GeneratedComplaints { get; set; } = new List<Complaint>();
    }
}