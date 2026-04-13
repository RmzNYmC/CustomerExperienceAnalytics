using CEA.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace CEA.Web.Pages.Admin
{
    [Authorize(Roles = "Admin")]
    public class DataCleanupModel : PageModel
    {
        private readonly ApplicationDbContext _context;

        public DataCleanupModel(ApplicationDbContext context)
        {
            _context = context;
        }

        public string Message { get; set; } = "";

        public async Task<IActionResult> OnPostDeleteLastResponsesAsync(int count, int? surveyId = null)
        {
            try
            {
                var query = _context.SurveyResponses.AsQueryable();

                // Belirli anket ise filtrele
                if (surveyId.HasValue)
                {
                    query = query.Where(r => r.SurveyId == surveyId.Value);
                }

                // Son eklenenleri al
                var responsesToDelete = await query
                    .OrderByDescending(r => r.CreatedAt)
                    .Take(count)
                    .ToListAsync();

                var responseIds = responsesToDelete.Select(r => r.Id).ToList();

                // Önce cevapları sil (Cascade delete varsa gerekmez ama güvenlik için)
                var answers = await _context.Answers
                    .Where(a => responseIds.Contains(a.ResponseId))
                    .ToListAsync();

                _context.Answers.RemoveRange(answers);
                await _context.SaveChangesAsync();

                // Sonra yanıtları sil
                _context.SurveyResponses.RemoveRange(responsesToDelete);
                await _context.SaveChangesAsync();

                Message = $"{responsesToDelete.Count} yanıt ve bağlı cevaplar silindi.";
            }
            catch (Exception ex)
            {
                Message = "Hata: " + ex.Message;
            }

            return Page();
        }

        public async Task OnGetAsync()
        {
            // Mevcut yanıt sayısını göster
            ViewData["TotalResponses"] = await _context.SurveyResponses.CountAsync();
            ViewData["Surveys"] = await _context.Surveys.Where(s => !s.IsDeleted).ToListAsync();
        }
    }
}