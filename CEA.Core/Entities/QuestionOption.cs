using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CEA.Core.Entities
{
    public class QuestionOption : BaseEntity
    {
        public string Text { get; set; } = string.Empty;
        public int DisplayOrder { get; set; } = 0;
        public int? ScoreValue { get; set; }  // Analiz için puan değeri

        // Foreign Keys
        public int QuestionId { get; set; }

        // Navigation
        public Question Question { get; set; } = null!;
    }
}
