using CEA.Business.Services;
using CEA.Core.Enums;
using CEA.Core.ViewModels;
using CEA.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace CEA.Web.Pages.Admin.Analytics
{
    [Authorize(Policy = "CanViewReports")]
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
        public NpsDistribution NpsDistribution { get; set; } = new();
        public List<string> MonthlyLabels { get; set; } = new();
        public List<decimal> MonthlyScores { get; set; } = new();

        public IndexModel(IAnalyticsService analyticsService, ApplicationDbContext context)
        {
            _analyticsService = analyticsService;
            _context = context;
        }

        public async Task OnGetAsync()
        {
            // Anket listesini doldur
            SurveyList = await _context.Surveys
                .Where(s => !s.IsDeleted)
                .OrderByDescending(s => s.CreatedAt)
                .Select(s => new SelectListItem
                {
                    Value = s.Id.ToString(),
                    Text = $"{s.Title} ({s.AnalysisYear})"
                })
                .ToListAsync();

            if (SelectedSurveyId.HasValue)
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
                if (ComparisonType != "None")
                {
                    if (ComparisonType == "PreviousYear")
                    {
                        ComparisonResult = await _analyticsService.GetYearlyAnalysisAsync(
                            SelectedSurveyId.Value, SelectedYear - 1);
                        ComparisonLabel = "vs Önceki Yıl";
                    }
                    else if (ComparisonType == "PreviousMonth" && SelectedMonth.HasValue)
                    {
                        var prevMonth = SelectedMonth.Value == 1 ? 12 : SelectedMonth.Value - 1;
                        var prevYear = SelectedMonth.Value == 1 ? SelectedYear - 1 : SelectedYear;
                        ComparisonResult = await _analyticsService.GetMonthlyAnalysisAsync(
                            SelectedSurveyId.Value, prevYear, prevMonth);
                        ComparisonLabel = "vs Önceki Ay";
                    }
                }

                // NPS Dağılımı hesapla
                await CalculateNpsDistribution();

                // Aylık trend verisi
                await LoadMonthlyTrend();
            }
        }

        private async Task CalculateNpsDistribution()
        {
            if (!SelectedSurveyId.HasValue) return;

            var query = _context.Answers
                .Include(a => a.Question)
                .Where(a => a.Question.QuestionType == QuestionType.NpsScore
                    && a.Response.SurveyId == SelectedSurveyId.Value
                    && a.Response.ResponseYear == SelectedYear
                    && !a.Response.IsDeleted);

            if (SelectedMonth.HasValue)
                query = query.Where(a => a.Response.ResponseMonth == SelectedMonth.Value);

            var scores = await query
    .Where(a => a.NumericAnswer.HasValue)
    .Select(a => a.NumericAnswer!.Value) // ! eklendi
    .ToListAsync();

            NpsDistribution = new NpsDistribution
            {
                Promoters = scores.Count(x => x >= 9),
                Passives = scores.Count(x => x >= 7 && x <= 8),
                Detractors = scores.Count(x => x <= 6)
            };
        }

        private async Task LoadMonthlyTrend()
        {
            MonthlyLabels = new List<string>();
            MonthlyScores = new List<decimal>();

            for (int m = 1; m <= 12; m++)
            {
                MonthlyLabels.Add(System.Globalization.CultureInfo.CurrentCulture.DateTimeFormat.GetMonthName(m));

                var monthlyData = await _analyticsService.GetMonthlyAnalysisAsync(
                    SelectedSurveyId!.Value, SelectedYear, m);
                MonthlyScores.Add(monthlyData.AverageSatisfaction);
            }
        }

        public async Task<IActionResult> OnGetExportPdfAsync(int surveyId, int year, int? month)
        {
            var pdfBytes = await _analyticsService.ExportToPdfAsync(surveyId, year, month);
            return File(pdfBytes, "application/pdf",
                $"Analiz_Raporu_{year}{(month.HasValue ? $"_{month:D2}" : "")}.pdf");
        }
    }

    public class NpsDistribution
    {
        public int Promoters { get; set; }
        public int Passives { get; set; }
        public int Detractors { get; set; }
    }
}