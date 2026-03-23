using CEA.Core.Enums;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CEA.Core.Entities
{
    public class Question : BaseEntity
    {
        [Required]
        public string Text { get; set; } = string.Empty;
        public string? Description { get; set; }
        public QuestionType QuestionType { get; set; }
        public bool IsRequired { get; set; } = true;
        public int DisplayOrder { get; set; } = 0;

        // Rating scale için (1-5 veya 1-10)
        public int? MinRating { get; set; }
        public int? MaxRating { get; set; }

        // NPS veya olumsuz durum tetikleyici
        public bool TriggerComplaintOnLowRating { get; set; } = false;
        public int? ComplaintThreshold { get; set; }  // Bu değerin altındaysa şikayet oluşur

        // Foreign Keys
        public int SurveyId { get; set; }

        // Navigation
        public Survey Survey { get; set; } = null!;
        public ICollection<QuestionOption> Options { get; set; } = new List<QuestionOption>();
        public ICollection<Answer> Answers { get; set; } = new List<Answer>();
    }
}
