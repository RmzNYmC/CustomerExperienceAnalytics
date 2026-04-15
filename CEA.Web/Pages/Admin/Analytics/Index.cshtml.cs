using CEA.Business.Services;
using CEA.Core.Enum;
using CEA.Core.ViewModels;
using CEA.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace CEA.Web.Pages.Admin.Analytics
{
    [Authorize(Policy = "CanViewAnalytics")]
    public class IndexModel : PageModel
    {
        private readonly IAnalyticsService _analyticsService;
        private readonly ApplicationDbContext _context;

        [BindProperty(SupportsGet = true)]
        public int? SelectedSurveyId { get; set; }

        [BindProperty(SupportsGet = true)]
        public int SelectedYear { get; set; } = DateTime.Now.Year;

        [BindProperty(SupportsGet = true)]
        public int? SelectedMonth { get; set; }

        [BindProperty(SupportsGet = true)]
        public string ComparisonType { get; set; } = "None";

        public PeriodAnalysisResult? AnalysisResult { get; set; }
        public PeriodAnalysisResult? ComparisonResult { get; set; }
        public string ComparisonLabel { get; set; } = "";

        public List<SelectListItem> SurveyList { get; set; } = new();
        public List<SelectListItem> YearList { get; set; } = new();
        public List<SelectListItem> MonthList { get; set; } = new();
        public NpsDistribution NpsDistribution { get; set; } = new();


        public IndexModel(IAnalyticsService analyticsService, ApplicationDbContext context)
        {
            _analyticsService = analyticsService;
            _context = context;
        }

        public async Task OnGetAsync()
        {
            await LoadDropdowns();

            if (SelectedSurveyId.HasValue)
            {
                await LoadAnalysis();
            }
        }

        private async Task LoadDropdowns()
        {
            // Anketler (MEVCUT - Değişmiyor)
            SurveyList = await _context.Surveys
                .AsNoTracking()
                .Where(s => !s.IsDeleted)
                .OrderByDescending(s => s.CreatedAt)
                .Select(s => new SelectListItem
                {
                    Value = s.Id.ToString(),
                    Text = $"{s.Title} ({s.AnalysisYear})"
                })
                .ToListAsync();

            // Yıllar (2020-2026) ✅ BU BLOK EKLENDİ/EKLENDİ
            YearList = Enumerable.Range(2020, 7)
                .Select(y => new SelectListItem
                {
                    Value = y.ToString(),
                    Text = y.ToString(),
                    Selected = y == SelectedYear
                })
                .ToList();

            // Aylar ✅ BU BLOK EKLENDİ/EKLENDİ
            MonthList = Enumerable.Range(1, 12)
                .Select(m => new SelectListItem
                {
                    Value = m.ToString(),
                    Text = System.Globalization.CultureInfo.CurrentCulture.DateTimeFormat.GetMonthName(m),
                    Selected = m == SelectedMonth
                })
                .ToList();
        }

        private async Task LoadAnalysis()
        {
            // Ana analiz
            if (SelectedMonth.HasValue)
            {
                AnalysisResult = await _analyticsService.GetMonthlyAnalysisAsync(
                    SelectedSurveyId.Value, SelectedYear, SelectedMonth.Value);
            }
            else
            {
                AnalysisResult = await _analyticsService.GetYearlyAnalysisAsync(
                    SelectedSurveyId.Value, SelectedYear);
            }

            // Karşılaştırma
            if (ComparisonType == "PreviousYear")
            {
                ComparisonResult = await _analyticsService.GetYearlyAnalysisAsync(
                    SelectedSurveyId.Value, SelectedYear - 1);
                ComparisonLabel = $"{SelectedYear - 1} Yılı ile Karşılaştırma";
            }
            else if (ComparisonType == "PreviousMonth" && SelectedMonth.HasValue)
            {
                var prevMonth = SelectedMonth.Value == 1 ? 12 : SelectedMonth.Value - 1;
                var prevYear = SelectedMonth.Value == 1 ? SelectedYear - 1 : SelectedYear;

                ComparisonResult = await _analyticsService.GetMonthlyAnalysisAsync(
                    SelectedSurveyId.Value, prevYear, prevMonth);
                ComparisonLabel = $"{prevMonth:00}/{prevYear} ile Karşılaştırma";
            }

            // NPS Dağılımı
            await CalculateNpsDistribution();
        }

        private async Task CalculateNpsDistribution()
        {
            if (!SelectedSurveyId.HasValue) return;

            var query = _context.Answers
                .AsNoTracking()
                .Include(a => a.Question)
                .Where(a => a.Question.QuestionType == QuestionType.NpsScore
                    && a.Response.SurveyId == SelectedSurveyId.Value
                    && a.Response.ResponseYear == SelectedYear
                    && !a.Response.IsDeleted);

            if (SelectedMonth.HasValue)
                query = query.Where(a => a.Response.ResponseMonth == SelectedMonth.Value);

            var scores = await query
                .Where(a => a.NumericAnswer.HasValue)
                .Select(a => a.NumericAnswer!.Value)
                .ToListAsync();

            NpsDistribution = new NpsDistribution
            {
                Promoters = scores.Count(x => x >= 9),
                Passives = scores.Count(x => x >= 7 && x <= 8),
                Detractors = scores.Count(x => x <= 6),
                Total = scores.Count  // ✅ BUNU EKLEYİN
            };
        }

        public async Task<IActionResult> OnGetExportPdfAsync(int surveyId, int year, int? month)
        {
            var pdfBytes = await _analyticsService.ExportToPdfAsync(surveyId, year, month);
            var fileName = $"Analiz_Raporu_{year}{(month.HasValue ? $"_{month:D2}" : "")}.pdf";
            return File(pdfBytes, "application/pdf", fileName);
        }
    }

    public class NpsDistribution
    {
        public int Promoters { get; set; }
        public int Passives { get; set; }
        public int Detractors { get; set; }
        public int Total { get; set; }  // ✅ EKLENDİ

        public double PromoterPercentage => Total > 0 ? (double)Promoters / Total * 100 : 0;      // ✅ EKLENDİ
        public double PassivePercentage => Total > 0 ? (double)Passives / Total * 100 : 0;       // ✅ EKLENDİ
        public double DetractorPercentage => Total > 0 ? (double)Detractors / Total * 100 : 0;   // ✅ EKLENDİ
    }
}