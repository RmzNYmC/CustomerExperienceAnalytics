using CEA.Business.Services;
using CEA.Core.Enum;
using CEA.Data;
using CEA.Web.Dtos.Analytics;
using CEA.Web.Dtos.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CEA.Web.Controllers
{
    [Route("api/analytics")]
    [ApiController]
    [Authorize(Policy = "CanViewAnalytics")]
    public class AnalyticsApiController : ControllerBase
    {
        private readonly IAnalyticsService _analyticsService;
        private readonly ApplicationDbContext _context;

        public AnalyticsApiController(IAnalyticsService analyticsService, ApplicationDbContext context)
        {
            _analyticsService = analyticsService;
            _context = context;
        }

        [HttpGet("dashboard-summary")]
        [AllowAnonymous]
        public async Task<IActionResult> GetDashboardSummary()
        {
            var summary = await _analyticsService.GetDashboardSummaryAsync();
            return Ok(summary);
        }

        [HttpGet("surveys/{surveyId:int}")]
        public async Task<IActionResult> GetSurveyAnalysis(int surveyId, [FromQuery] int year, [FromQuery] int? month)
        {
            var surveyExists = await _context.Surveys.AnyAsync(s => s.Id == surveyId && !s.IsDeleted);
            if (!surveyExists)
                return NotFound(ApiResponse<object>.Fail("Anket bulunamadı."));

            var result = month.HasValue
                ? await _analyticsService.GetMonthlyAnalysisAsync(surveyId, year, month.Value)
                : await _analyticsService.GetYearlyAnalysisAsync(surveyId, year);

            return Ok(result);
        }

        [HttpGet("surveys/{surveyId:int}/comparison")]
        public async Task<IActionResult> GetComparison(int surveyId, [FromQuery] int startYear, [FromQuery] int endYear)
        {
            if (endYear < startYear)
                return BadRequest(ApiResponse<object>.Fail("Bitiş yılı başlangıç yılından küçük olamaz."));

            var surveyExists = await _context.Surveys.AnyAsync(s => s.Id == surveyId && !s.IsDeleted);
            if (!surveyExists)
                return NotFound(ApiResponse<object>.Fail("Anket bulunamadı."));

            var result = await _analyticsService.GetComparisonAnalysisAsync(surveyId, startYear, endYear);
            return Ok(result);
        }

        [HttpGet("surveys/{surveyId:int}/nps-distribution")]
        public async Task<IActionResult> GetNpsDistribution(int surveyId, [FromQuery] int year, [FromQuery] int? month)
        {
            var query = _context.Answers
                .AsNoTracking()
                .Include(a => a.Question)
                .Where(a => a.Question.QuestionType == QuestionType.NpsScore
                    && a.Response.SurveyId == surveyId
                    && a.Response.ResponseYear == year
                    && !a.Response.IsDeleted);

            if (month.HasValue)
                query = query.Where(a => a.Response.ResponseMonth == month.Value);

            var scores = await query
                .Where(a => a.NumericAnswer.HasValue)
                .Select(a => a.NumericAnswer!.Value)
                .ToListAsync();

            var result = new NpsDistributionDto
            {
                Promoters = scores.Count(x => x >= 9),
                Passives = scores.Count(x => x >= 7 && x <= 8),
                Detractors = scores.Count(x => x <= 6),
                Total = scores.Count
            };

            return Ok(result);
        }

        [HttpGet("surveys/{surveyId:int}/export-pdf")]
        public async Task<IActionResult> ExportPdf(int surveyId, [FromQuery] int year, [FromQuery] int? month)
        {
            var pdfBytes = await _analyticsService.ExportToPdfAsync(surveyId, year, month);
            var fileName = $"Analiz_Raporu_{surveyId}_{year}{(month.HasValue ? $"_{month:00}" : string.Empty)}.pdf";
            return File(pdfBytes, "application/pdf", fileName);
        }

        [HttpDelete("surveys/{surveyId:int}/cache")]
        public async Task<IActionResult> InvalidateSurveyCache(int surveyId)
        {
            await _analyticsService.InvalidateCacheAsync(surveyId);
            return Ok(ApiResponse<object>.Ok(null, "Analitik cache temizlendi."));
        }
    }
}
