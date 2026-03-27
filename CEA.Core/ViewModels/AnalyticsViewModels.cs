using CEA.Core.Enums;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CEA.Core.ViewModels
{
    // Aylık/Yıllık analiz sonuçları
    public class PeriodAnalysisResult
    {
        public int Year { get; set; }
        public int? Month { get; set; }
        public string PeriodLabel => Month.HasValue ? $"{Year}-{Month:D2}" : $"{Year}";

        public int TotalResponses { get; set; }
        public decimal AverageSatisfaction { get; set; }
        public decimal NpsScore { get; set; }
        public int ComplaintCount { get; set; }
        public decimal ResponseRate { get; set; }

        public List<QuestionAnalysis> QuestionBreakdown { get; set; } = new();
    }

    public class QuestionAnalysis
    {
        public int QuestionId { get; set; }
        public string QuestionText { get; set; } = string.Empty;
        public QuestionType Type { get; set; }
        public decimal AverageScore { get; set; }
        public int ResponseCount { get; set; }
        public List<AnswerDistribution> Distribution { get; set; } = new();
    }

    public class AnswerDistribution
    {
        public string Label { get; set; } = string.Empty;
        public int Count { get; set; }
        public decimal Percentage { get; set; }
    }

    // Dashboard için özet kartlar
    public class DashboardSummary
    {
        public int TotalSurveys { get; set; }
        public int ActiveSurveys { get; set; }
        public int TotalResponsesThisMonth { get; set; }
        public int TotalResponsesThisYear { get; set; }
        public decimal AverageNpsThisMonth { get; set; }
        public decimal AverageNpsThisYear { get; set; }
        public int OpenComplaints { get; set; }
        public int CriticalComplaints { get; set; }
        public List<TrendDataPoint> MonthlyTrend { get; set; } = new();
        public decimal PositivePercentage { get; set; }    // Olumlu (😊)
        public decimal NeutralPercentage { get; set; }     // Orta (😐)
        public decimal NegativePercentage { get; set; }    // Olumsuz (😡)
        //Sayısal dağılımlar(Toplam Geri Bildirimler kartı için)
    public int PositiveCount { get; set; }      // Olumlu sayısı
        public int NeutralCount { get; set; }       // Orta sayısı  
        public int NegativeCount { get; set; }      // Olumsuz sayısı
        public int TotalFeedbackCount { get; set; } // Toplam geri bildirim sayısı
    }

    public class TrendDataPoint
    {
        public string Label { get; set; } = string.Empty;
        public decimal Value { get; set; }
        public int Count { get; set; }
    }
}
