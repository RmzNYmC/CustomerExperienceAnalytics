using CEA.Core.Enum;
using CEA.Core.Enums;
using CEA.Core.ViewModels;
using CEA.Data;
using Microsoft.EntityFrameworkCore;

namespace CEA.Business.Services
{
    public interface IAnalyticsService
    {
        Task<PeriodAnalysisResult> GetMonthlyAnalysisAsync(int surveyId, int year, int month);
        Task<PeriodAnalysisResult> GetYearlyAnalysisAsync(int surveyId, int year);
        Task<List<PeriodAnalysisResult>> GetComparisonAnalysisAsync(int surveyId, int startYear, int endYear);
        Task<DashboardSummary> GetDashboardSummaryAsync();
        Task<byte[]> ExportToPdfAsync(int surveyId, int year, int? month);
    }

    public class AnalyticsService : IAnalyticsService
    {
        private readonly ApplicationDbContext _context;
        private readonly IPdfReportService _pdfService;

        public AnalyticsService(ApplicationDbContext context, IPdfReportService pdfService)
        {
            _context = context;
            _pdfService = pdfService;
        }

        public async Task<PeriodAnalysisResult> GetMonthlyAnalysisAsync(int surveyId, int year, int month)
        {
            var responses = await _context.SurveyResponses
                .Where(r => r.SurveyId == surveyId
                    && r.ResponseYear == year
                    && r.ResponseMonth == month
                    && !r.IsDeleted)
                .Include(r => r.Answers)
                .ThenInclude(a => a.Question)
                .ToListAsync();

            return await AnalyzeResponses(responses, surveyId, year, month);
        }

        public async Task<PeriodAnalysisResult> GetYearlyAnalysisAsync(int surveyId, int year)
        {
            var responses = await _context.SurveyResponses
                .Where(r => r.SurveyId == surveyId
                    && r.ResponseYear == year
                    && !r.IsDeleted)
                .Include(r => r.Answers)
                .ThenInclude(a => a.Question)
                .ToListAsync();

            return await AnalyzeResponses(responses, surveyId, year, null);
        }

        private async Task<PeriodAnalysisResult> AnalyzeResponses(
            List<Core.Entities.SurveyResponse> responses, int surveyId, int year, int? month)
        {
            var survey = await _context.Surveys
                .Include(s => s.Questions)
                .ThenInclude(q => q.Options)
                .FirstAsync(s => s.Id == surveyId);

            var result = new PeriodAnalysisResult
            {
                Year = year,
                Month = month,
                TotalResponses = responses.Count,
                ResponseRate = await CalculateResponseRate(surveyId, year, month)
            };

            // NPS Hesaplama
            var npsAnswers = responses
                .SelectMany(r => r.Answers)
                .Where(a => a.Question.QuestionType == QuestionType.NpsScore && a.NumericAnswer.HasValue)
                .Select(a => a.NumericAnswer!.Value)  // ! eklendi - null değil biliyoruz
                .ToList();

            if (npsAnswers.Any())
            {
                int promoters = npsAnswers.Count(x => x >= 9);
                int detractors = npsAnswers.Count(x => x <= 6);
                int total = npsAnswers.Count;
                result.NpsScore = ((decimal)(promoters - detractors) / total) * 100;
            }

            // Genel Memnuniyet
            var ratingAnswers = responses
                .SelectMany(r => r.Answers)
                .Where(a => a.Question.QuestionType == QuestionType.RatingScale && a.NumericAnswer.HasValue)
                .Select(a => (decimal)a.NumericAnswer!.Value)  // ! eklendi
                .ToList();

            result.AverageSatisfaction = ratingAnswers.Any()
                ? ratingAnswers.Average()
                : 0;

            // Şikayet sayısı
            result.ComplaintCount = await _context.Complaints
                .CountAsync(c => responses.Select(r => r.Id).Contains(c.SurveyResponseId));

            // Soru bazlı analiz
            foreach (var question in survey.Questions.Where(q => !q.IsDeleted).OrderBy(q => q.DisplayOrder))
            {
                var questionAnswers = responses
                    .SelectMany(r => r.Answers)
                    .Where(a => a.QuestionId == question.Id)
                    .ToList();

                var qa = new QuestionAnalysis
                {
                    QuestionId = question.Id,
                    QuestionText = question.Text,
                    Type = question.QuestionType,
                    ResponseCount = questionAnswers.Count
                };

                // Dağılım hesaplama
                switch (question.QuestionType)
                {
                    case QuestionType.SingleChoice:
                    case QuestionType.MultipleChoice:
                        var optionCounts = questionAnswers
                            .SelectMany(a => (a.SelectedOptionIds ?? "").Split(',', StringSplitOptions.RemoveEmptyEntries))
                            .GroupBy(x => x)
                            .Select(g => new { OptionId = g.Key, Count = g.Count() })
                            .ToList();

                        foreach (var opt in question.Options)
                        {
                            var count = optionCounts.FirstOrDefault(x => x.OptionId == opt.Id.ToString())?.Count ?? 0;
                            qa.Distribution.Add(new AnswerDistribution
                            {
                                Label = opt.Text,
                                Count = count,
                                Percentage = questionAnswers.Any() ? (decimal)count / questionAnswers.Count * 100 : 0
                            });
                        }
                        break;

                    case QuestionType.RatingScale:
                    case QuestionType.NpsScore:
                        var numericValues = questionAnswers
                            .Where(a => a.NumericAnswer.HasValue)
                            .Select(a => a.NumericAnswer!.Value)  // ! eklendi
                            .ToList();

                        // HATA DÜZELTİLDİ: (decimal) cast eklendi
                        qa.AverageScore = numericValues.Any() ? (decimal)numericValues.Average() : 0;

                        // Rating dağılımı - null kontrolü eklendi
                        if (question.MinRating.HasValue && question.MaxRating.HasValue)
                        {
                            for (int i = question.MinRating.Value; i <= question.MaxRating.Value; i++)
                            {
                                var count = numericValues.Count(x => x == i);
                                qa.Distribution.Add(new AnswerDistribution
                                {
                                    Label = i.ToString(),
                                    Count = count,
                                    Percentage = numericValues.Any() ? (decimal)count / numericValues.Count * 100 : 0
                                });
                            }
                        }
                        break;

                    case QuestionType.TextOpen:
                        qa.Distribution.Add(new AnswerDistribution
                        {
                            Label = "Metin Yanıtları",
                            Count = questionAnswers.Count(x => !string.IsNullOrEmpty(x.TextAnswer)),
                            Percentage = 100
                        });
                        break;
                }

                result.QuestionBreakdown.Add(qa);
            }

            return result;
        }

        private async Task<decimal> CalculateResponseRate(int surveyId, int year, int? month)
        {
            var totalSent = await _context.SurveyResponses
                .Where(r => r.SurveyId == surveyId && r.ResponseYear == year)
                .Select(r => r.CustomerEmail)
                .Distinct()
                .CountAsync();

            var totalResponses = await _context.SurveyResponses
                .CountAsync(r => r.SurveyId == surveyId
                    && r.ResponseYear == year
                    && (!month.HasValue || r.ResponseMonth == month));

            return totalSent > 0 ? (decimal)totalResponses / totalSent * 100 : 0;
        }

        public async Task<DashboardSummary> GetDashboardSummaryAsync()
        {
            var currentYear = DateTime.Now.Year;
            var currentMonth = DateTime.Now.Month;

            var summary = new DashboardSummary
            {
                TotalSurveys = await _context.Surveys.CountAsync(s => !s.IsDeleted),
                ActiveSurveys = await _context.Surveys.CountAsync(s => s.Status == SurveyStatus.Active),

                TotalResponsesThisMonth = await _context.SurveyResponses
                    .CountAsync(r => r.ResponseYear == currentYear && r.ResponseMonth == currentMonth),

                TotalResponsesThisYear = await _context.SurveyResponses
                    .CountAsync(r => r.ResponseYear == currentYear),

                OpenComplaints = await _context.Complaints
                    .CountAsync(c => c.Status != ComplaintStatus.Closed && c.Status != ComplaintStatus.Resolved && !c.IsDeleted),

                CriticalComplaints = await _context.Complaints
                    .CountAsync(c => c.Priority == "Critical" && c.Status != ComplaintStatus.Closed && !c.IsDeleted)
            };

            // Son 6 aylık trend
            for (int i = 5; i >= 0; i--)
            {
                var date = DateTime.Now.AddMonths(-i);
                var count = await _context.SurveyResponses
                    .CountAsync(r => r.ResponseYear == date.Year && r.ResponseMonth == date.Month);

                summary.MonthlyTrend.Add(new TrendDataPoint
                {
                    Label = $"{date.Month:00}/{date.Year}",
                    Value = count
                });
            }

            // NPS hesaplamaları
            var thisMonthNps = await CalculateNpsForPeriod(currentYear, currentMonth);
            var thisYearNps = await CalculateNpsForPeriod(currentYear, null);

            summary.AverageNpsThisMonth = thisMonthNps;
            summary.AverageNpsThisYear = thisYearNps;

            return summary;
        }

        private async Task<decimal> CalculateNpsForPeriod(int year, int? month)
        {
            var query = _context.Answers
                .Include(a => a.Question)
                .Where(a => a.Question.QuestionType == QuestionType.NpsScore
                    && a.Response.ResponseYear == year
                    && !a.Response.IsDeleted);

            if (month.HasValue)
                query = query.Where(a => a.Response.ResponseMonth == month);

            var scores = await query
                .Where(a => a.NumericAnswer.HasValue)
                .Select(a => a.NumericAnswer!.Value)  // ! eklendi
                .ToListAsync();

            if (!scores.Any()) return 0;

            int promoters = scores.Count(x => x >= 9);
            int detractors = scores.Count(x => x <= 6);
            return ((decimal)(promoters - detractors) / scores.Count) * 100;
        }

        public async Task<List<PeriodAnalysisResult>> GetComparisonAnalysisAsync(int surveyId, int startYear, int endYear)
        {
            var results = new List<PeriodAnalysisResult>();
            for (int year = startYear; year <= endYear; year++)
            {
                results.Add(await GetYearlyAnalysisAsync(surveyId, year));
            }
            return results;
        }

        public async Task<byte[]> ExportToPdfAsync(int surveyId, int year, int? month)
        {
            var survey = await _context.Surveys.FindAsync(surveyId)
                ?? throw new Exception("Anket bulunamadı");

            PeriodAnalysisResult analysis;
            if (month.HasValue)
                analysis = await GetMonthlyAnalysisAsync(surveyId, year, month.Value);
            else
                analysis = await GetYearlyAnalysisAsync(surveyId, year);

            return _pdfService.GenerateAnalyticsReport(analysis, survey.Title);
        }
    }
}