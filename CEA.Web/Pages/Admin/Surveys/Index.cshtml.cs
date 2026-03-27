using CEA.Core.Entities;
using CEA.Core.Enum;
using CEA.Core.Enums;
using CEA.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace CEA.Web.Pages.Admin.Surveys
{
    [Authorize(Policy = "CanCreateSurvey")]
    public class IndexModel : PageModel
    {
        private readonly ApplicationDbContext _context;

        public IndexModel(ApplicationDbContext context)
        {
            _context = context;
        }

        public List<SurveyViewModel> Surveys { get; set; } = new();

        public async Task OnGetAsync()
        {
            Surveys = await _context.Surveys
                .Where(s => !s.IsDeleted)
                .OrderByDescending(s => s.CreatedAt)
                .Select(s => new SurveyViewModel
                {
                    Id = s.Id,
                    Title = s.Title,
                    Status = s.Status.ToString(),
                    StatusClass = s.Status == SurveyStatus.Active ? "success" :
                                  s.Status == SurveyStatus.Draft ? "secondary" : "info",
                    PublicToken = s.PublicToken,
                    AnalysisYear = s.AnalysisYear,
                    AnalysisMonth = s.AnalysisMonth,
                    CreatedAt = s.CreatedAt,
                    ResponseCount = s.Responses.Count(r => !r.IsDeleted)
                })
                .ToListAsync();
        }
        public async Task<IActionResult> OnPostDeleteAsync(int id)
        {
            var survey = await _context.Surveys
                .Include(s => s.Responses)
                .FirstOrDefaultAsync(s => s.Id == id && !s.IsDeleted);

            if (survey == null)
            {
                TempData["ErrorMessage"] = "Anket bulunamadı.";
                return RedirectToPage();
            }

            try
            {
                // Soft delete
                survey.IsDeleted = true;
                survey.DeletedAt = DateTime.Now;

                // İlişkili yanıtları da soft delete yap (isteğe bağlı)
                foreach (var response in survey.Responses.Where(r => !r.IsDeleted))
                {
                    response.IsDeleted = true;
                    response.DeletedAt = DateTime.Now;
                }

                await _context.SaveChangesAsync();
                TempData["SuccessMessage"] = $"'{survey.Title}' anket başarıyla silindi.";
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = "Anket silinirken bir hata oluştu: " + ex.Message;
            }

            return RedirectToPage();
        }
    }

    public class SurveyViewModel
    {
        public int Id { get; set; }
        public string Title { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public string StatusClass { get; set; } = string.Empty;
        public string PublicToken { get; set; } = string.Empty;
        public int AnalysisYear { get; set; }
        public int? AnalysisMonth { get; set; }
        public DateTime CreatedAt { get; set; }
        public int ResponseCount { get; set; }
    }
}