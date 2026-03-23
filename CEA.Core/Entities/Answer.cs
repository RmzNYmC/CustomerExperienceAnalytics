using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CEA.Core.Entities
{
    public class Answer : BaseEntity
    {
        // Açık uçlu cevaplar için metin
        public string? TextAnswer { get; set; }

        // Rating/NPS için sayısal değer
        public int? NumericAnswer { get; set; }

        // Seçenekler için (virgülle ayrılmış ID'ler çoklu seçim için)
        public string? SelectedOptionIds { get; set; }

        // Hesaplanan puan
        public decimal? Score { get; set; }

        // Foreign Keys
        public int ResponseId { get; set; }
        public int QuestionId { get; set; }

        // Navigation
        public SurveyResponse Response { get; set; } = null!;
        public Question Question { get; set; } = null!;
    }
}
