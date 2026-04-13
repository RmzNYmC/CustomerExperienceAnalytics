using CEA.Core.Enum;
using CEA.Core.ViewModels;
using CEA.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace CEA.Business.Services
{
    public interface IAnalyticsService
    {
        Task<PeriodAnalysisResult> GetMonthlyAnalysisAsync(int surveyId, int year, int month);
        Task<PeriodAnalysisResult> GetYearlyAnalysisAsync(int surveyId, int year);
        Task<List<PeriodAnalysisResult>> GetComparisonAnalysisAsync(int surveyId, int startYear, int endYear);
        Task<DashboardSummary> GetDashboardSummaryAsync();
        Task<byte[]> ExportToPdfAsync(int surveyId, int year, int? month);
        Task InvalidateCacheAsync(int surveyId);
    }

    public class AnalyticsService : IAnalyticsService
    {
        private readonly ApplicationDbContext _context;
        private readonly IPdfReportService _pdfService;
        private readonly ICacheService _cacheService;
        private readonly ILogger<AnalyticsService> _logger;

        public AnalyticsService(
            ApplicationDbContext context,
            IPdfReportService pdfService,
            ICacheService cacheService,
            ILogger<AnalyticsService> logger)
        {
            _context = context;
            _pdfService = pdfService;
            _cacheService = cacheService;
            _logger = logger;
        }

        public async Task<PeriodAnalysisResult> GetMonthlyAnalysisAsync(int surveyId, int year, int month)
        {
            var cacheKey = $"analytics:monthly:{surveyId}:{year}:{month}";

            var cached = await _cacheService.GetAsync<PeriodAnalysisResult>(cacheKey);
            if (cached != null) return cached;

            var responses = await _context.SurveyResponses
                .AsNoTracking()
                .Where(r => r.SurveyId == surveyId
                    && r.ResponseYear == year
                    && r.ResponseMonth == month
                    && !r.IsDeleted)
                .Include(r => r.Answers)
                .ThenInclude(a => a.Question)
                .ToListAsync();

            var result = await AnalyzeResponses(responses, surveyId, year, month);
            await _cacheService.SetAsync(cacheKey, result, TimeSpan.FromHours(1));

            return result;
        }

        public async Task<PeriodAnalysisResult> GetYearlyAnalysisAsync(int surveyId, int year)
        {
            var cacheKey = $"analytics:yearly:{surveyId}:{year}";

            var cached = await _cacheService.GetAsync<PeriodAnalysisResult>(cacheKey);
            if (cached != null) return cached;

            var responses = await _context.SurveyResponses
                .AsNoTracking()
                .Where(r => r.SurveyId == surveyId && r.ResponseYear == year && !r.IsDeleted)
                .Include(r => r.Answers)
                .ThenInclude(a => a.Question)
                .ToListAsync();

            var result = await AnalyzeResponses(responses, surveyId, year, null);
            await _cacheService.SetAsync(cacheKey, result, TimeSpan.FromHours(2));

            return result;
        }

        public async Task<List<PeriodAnalysisResult>> GetComparisonAnalysisAsync(int surveyId, int startYear, int endYear)
        {
            var results = new List<PeriodAnalysisResult>();

            // SIRAYLA İŞLE - Paralel değil!
            for (int year = startYear; year <= endYear; year++)
            {
                results.Add(await GetYearlyAnalysisAsync(surveyId, year));
            }

            return results;
        }

        public async Task<DashboardSummary> GetDashboardSummaryAsync()
        {
            var cacheKey = "analytics:dashboard:summary";

            var cached = await _cacheService.GetAsync<DashboardSummary>(cacheKey);
            if (cached != null) return cached;

            var currentYear = DateTime.Now.Year;
            var currentMonth = DateTime.Now.Month;

            // ✅ KESİN ÇÖZÜM: TÜM SORGULAR SIRAYLI (await tek tek)
            var summary = new DashboardSummary();

            // 1. Toplam anketler
            summary.TotalSurveys = await _context.Surveys
                .AsNoTracking()
                .CountAsync(s => !s.IsDeleted);

            // 2. Aktif anketler
            summary.ActiveSurveys = await _context.Surveys
                .AsNoTracking()
                .CountAsync(s => s.Status == SurveyStatus.Active);

            // 3. Bu ay yanıtlar
            summary.TotalResponsesThisMonth = await _context.SurveyResponses
                .AsNoTracking()
                .CountAsync(r => r.ResponseYear == currentYear
                    && r.ResponseMonth == currentMonth
                    && !r.IsDeleted);

            // 4. Bu yıl yanıtlar
            summary.TotalResponsesThisYear = await _context.SurveyResponses
                .AsNoTracking()
                .CountAsync(r => r.ResponseYear == currentYear
                    && !r.IsDeleted);

            // 5. Açık şikayetler
            summary.OpenComplaints = await _context.Complaints
                .AsNoTracking()
                .CountAsync(c => c.Status != ComplaintStatus.Closed
                    && c.Status != ComplaintStatus.Resolved
                    && !c.IsDeleted);

            // 6. Kritik şikayetler
            summary.CriticalComplaints = await _context.Complaints
                .AsNoTracking()
                .CountAsync(c => c.Priority == "Critical"
                    && c.Status != ComplaintStatus.Closed
                    && !c.IsDeleted);

            // 7. Aylık trend - SIRAYLI HESAPLA (paralel değil!)
            summary.MonthlyTrend = new List<TrendDataPoint>();
            for (int i = 5; i >= 0; i--)
            {
                var date = DateTime.Now.AddMonths(-i);
                var count = await _context.SurveyResponses
                    .AsNoTracking()
                    .CountAsync(r => r.ResponseYear == date.Year
                        && r.ResponseMonth == date.Month
                        && !r.IsDeleted);

                summary.MonthlyTrend.Add(new TrendDataPoint
                {
                    Label = $"{date.Month:00}/{date.Year}",
                    Value = count,
                    Count = count
                });
            }

            // 8. NPS hesaplamaları
            summary.AverageNpsThisMonth = await CalculateNpsForPeriod(currentYear, currentMonth);
            summary.AverageNpsThisYear = await CalculateNpsForPeriod(currentYear, null);

            // 9. Memnuniyet dağılımı
            await CalculateSatisfactionDistribution(summary, currentYear, currentMonth);

            await _cacheService.SetAsync(cacheKey, summary, TimeSpan.FromMinutes(15));

            return summary;
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

        public async Task InvalidateCacheAsync(int surveyId)
        {
            await _cacheService.RemoveByPrefixAsync($"analytics:monthly:{surveyId}");
            await _cacheService.RemoveByPrefixAsync($"analytics:yearly:{surveyId}");
            await _cacheService.RemoveAsync("analytics:dashboard:summary");

            _logger.LogInformation("Cache invalidated for survey {SurveyId}", surveyId);
        }

        private async Task<PeriodAnalysisResult> AnalyzeResponses(
            List<Core.Entities.SurveyResponse> responses, int surveyId, int year, int? month)
        {
            var survey = await _context.Surveys
                .AsNoTracking()
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
                .Select(a => a.NumericAnswer!.Value)
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
                .Select(a => (decimal)a.NumericAnswer!.Value)
                .ToList();

            result.AverageSatisfaction = ratingAnswers.Any() ? ratingAnswers.Average() : 0;

            // Şikayet sayısı
            result.ComplaintCount = await _context.Complaints
                .AsNoTracking()
                .CountAsync(c => responses.Select(r => r.Id).Contains(c.SurveyResponseId));

            // Soru bazlı analiz
            foreach (var question in survey.Questions
                .Where(q => !q.IsDeleted)
                .OrderBy(q => q.DisplayOrder))
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

                switch (question.QuestionType)
                {
                    case QuestionType.SingleChoice:
                    case QuestionType.MultipleChoice:
                        var optionCounts = questionAnswers
                            .Where(a => !string.IsNullOrEmpty(a.TextAnswer))
                            .GroupBy(a => a.TextAnswer)
                            .Select(g => new { OptionText = g.Key, Count = g.Count() })
                            .ToList();

                        foreach (var opt in optionCounts)
                        {
                            qa.Distribution.Add(new AnswerDistribution
                            {
                                Label = opt.OptionText,
                                Count = opt.Count,
                                Percentage = questionAnswers.Any()
                                    ? (decimal)opt.Count / questionAnswers.Count * 100
                                    : 0
                            });
                        }
                        break;

                    case QuestionType.RatingScale:
                    case QuestionType.NpsScore:
                        var numericValues = questionAnswers
                            .Where(a => a.NumericAnswer.HasValue)
                            .Select(a => a.NumericAnswer!.Value)
                            .ToList();

                        qa.AverageScore = numericValues.Any()
                            ? (decimal)numericValues.Average()
                            : 0;
                        int minRating = question.MinRating ?? 1;  // Varsayılan 1
                        int maxRating = question.MaxRating ?? 5;  // Varsayılan 5

                        // Eğer NPS ise ve Min/Max tanımlanmamışsa 0-10 kullan
                        if (question.QuestionType == QuestionType.NpsScore && !question.MinRating.HasValue && !question.MaxRating.HasValue)
                        {
                            minRating = 0;
                            maxRating = 10;
                        }

                        for (int i = minRating; i <= maxRating; i++)
                        {
                            var count = numericValues.Count(x => x == i);
                            qa.Distribution.Add(new AnswerDistribution
                            {
                                Label = i.ToString(),
                                Count = count,
                                Percentage = numericValues.Any() ? (decimal)count / numericValues.Count * 100 : 0
                            });
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
            var query = _context.SurveyResponses
                .AsNoTracking()
                .Where(r => r.SurveyId == surveyId && r.ResponseYear == year);

            if (month.HasValue)
                query = query.Where(r => r.ResponseMonth == month);

            var totalSent = await query
                .Select(r => r.CustomerEmail)
                .Distinct()
                .CountAsync();

            var totalResponses = await query.CountAsync();

            return totalSent > 0 ? (decimal)totalResponses / totalSent * 100 : 0;
        }

        private async Task<decimal> CalculateNpsForPeriod(int year, int? month)
        {
            var query = _context.Answers
                .AsNoTracking()
                .Include(a => a.Question)
                .Where(a => a.Question.QuestionType == QuestionType.NpsScore
                    && a.Response.ResponseYear == year
                    && !a.Response.IsDeleted);

            if (month.HasValue)
                query = query.Where(a => a.Response.ResponseMonth == month);

            var scores = await query
                .Where(a => a.NumericAnswer.HasValue)
                .Select(a => a.NumericAnswer!.Value)
                .ToListAsync();

            if (!scores.Any()) return 0;

            int promoters = scores.Count(x => x >= 9);
            int detractors = scores.Count(x => x <= 6);
            return ((decimal)(promoters - detractors) / scores.Count) * 100;
        }

        private async Task CalculateSatisfactionDistribution(DashboardSummary summary, int year, int? month)
        {
            var query = _context.Answers
                .AsNoTracking()
                .Include(a => a.Question)
                .Where(a => (a.Question.QuestionType == QuestionType.NpsScore ||
                             a.Question.QuestionType == QuestionType.RatingScale)
                            && a.Response.ResponseYear == year
                            && !a.Response.IsDeleted);

            if (month.HasValue)
                query = query.Where(a => a.Response.ResponseMonth == month);

            var answers = await query
                .Where(a => a.NumericAnswer.HasValue)
                .Select(a => a.NumericAnswer!.Value)
                .ToListAsync();

            if (!answers.Any())
            {
                summary.PositivePercentage = 60;
                summary.NeutralPercentage = 25;
                summary.NegativePercentage = 15;
                summary.PositiveCount = 0;
                summary.NeutralCount = 0;
                summary.NegativeCount = 0;
                summary.TotalFeedbackCount = 0;
                return;
            }

            int total = answers.Count;
            int positive = answers.Count(x => x >= 9 || x >= 4);
            int neutral = answers.Count(x => (x >= 7 && x <= 8) || x == 3);
            int negative = answers.Count(x => x <= 6 || x <= 2);

            summary.PositivePercentage = Math.Round((decimal)positive / total * 100, 1);
            summary.NeutralPercentage = Math.Round((decimal)neutral / total * 100, 1);
            summary.NegativePercentage = Math.Round((decimal)negative / total * 100, 1);

            var totalPercentage = summary.PositivePercentage + summary.NeutralPercentage + summary.NegativePercentage;
            if (totalPercentage != 100 && total > 0)
            {
                summary.PositivePercentage += (100 - totalPercentage);
            }

            summary.PositiveCount = positive;
            summary.NeutralCount = neutral;
            summary.NegativeCount = negative;
            summary.TotalFeedbackCount = total;
        }
    }
}