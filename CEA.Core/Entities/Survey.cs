using CEA.Core.Enum;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CEA.Core.Entities
{
    public class Survey : BaseEntity
    {
        public string Title { get; set; } = string.Empty;
        public string? Description { get; set; }
        public string? WelcomeMessage { get; set; }
        public string? ThankYouMessage { get; set; }
        public SurveyStatus Status { get; set; } = SurveyStatus.Draft;

        // Tarih aralığı
        public DateTime? StartDate { get; set; }
        public DateTime? EndDate { get; set; }

        // Dönemsel analiz için
        public int AnalysisYear { get; set; } = DateTime.Now.Year;
        public int? AnalysisMonth { get; set; }  // Null ise yıllık anket

        // Kimlik doğrulama ayarları
        public bool RequiresAuthentication { get; set; } = false;
        public bool AllowMultipleResponses { get; set; } = false;
        public string? AccessCode { get; set; }  // Anonim erişim için kod

        // Unique URL token
        public string PublicToken { get; set; } = Guid.NewGuid().ToString("N")[..10];

        // Foreign Keys
        public string CreatedByUserId { get; set; } = string.Empty;

        // Navigation Properties
        public ApplicationUser CreatedByUser { get; set; } = null!;
        public ICollection<Question> Questions { get; set; } = new List<Question>();
        public ICollection<SurveyResponse> Responses { get; set; } = new List<SurveyResponse>();
    }
}
