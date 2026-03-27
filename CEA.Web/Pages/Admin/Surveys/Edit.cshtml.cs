using CEA.Core.Entities;
using CEA.Core.Enum;
using CEA.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using SurveyEntity = CEA.Core.Entities.Survey;

namespace CEA.Web.Pages.Admin.Surveys
{
    [Authorize(Policy = "CanCreateSurvey")]
    public class EditModel : PageModel
    {
        private readonly ApplicationDbContext _context;

        public EditModel(ApplicationDbContext context)
        {
            _context = context;
        }

        [BindProperty]
        public SurveyEntity Survey { get; set; } = new();

        public SelectList StatusList { get; set; } = default!;

        public async Task<IActionResult> OnGetAsync(int? id)
        {
            if (id == null) return NotFound();

            Survey = await _context.Surveys
                .Include(s => s.Questions)
                .ThenInclude(q => q.Options)
                .FirstOrDefaultAsync(m => m.Id == id && !m.IsDeleted);

            if (Survey == null) return NotFound();

            Survey.Questions = Survey.Questions
                .Where(q => !q.IsDeleted)
                .OrderBy(q => q.DisplayOrder)
                .ToList();

            LoadStatusList();

            return Page();
        }

        public async Task<IActionResult> OnPostAsync()
        {
            // NAVIGATION PROPERTY ve READ-ONLY ALANLARIN VALIDASYONUNU TEMİZLE
            var keysToRemove = ModelState.Keys.Where(k =>
                k.Contains("CreatedByUser") ||
                k.Contains("Questions") ||
                k.Contains("Responses") ||
                k.Contains("DeletedAt") ||
                k.Contains("IsDeleted")
            ).ToList();

            foreach (var key in keysToRemove)
            {
                ModelState.Remove(key);
            }

            if (!ModelState.IsValid)
            {
                LoadStatusList();

                // Soruları tekrar yükle
                var surveyWithQuestions = await _context.Surveys
                    .Include(s => s.Questions)
                    .ThenInclude(q => q.Options)
                    .FirstOrDefaultAsync(s => s.Id == Survey.Id);

                if (surveyWithQuestions != null)
                    Survey.Questions = surveyWithQuestions.Questions.Where(q => !q.IsDeleted).ToList();

                return Page();
            }

            try
            {
                // Mevcut kaydı çek
                var existingSurvey = await _context.Surveys
                    .AsNoTracking()
                    .FirstOrDefaultAsync(s => s.Id == Survey.Id && !s.IsDeleted);

                if (existingSurvey == null)
                {
                    ModelState.AddModelError("", "Anket bulunamadı!");
                    LoadStatusList();
                    return Page();
                }

                // Korunacak alanları aktar
                Survey.CreatedAt = existingSurvey.CreatedAt;
                Survey.CreatedByUserId = existingSurvey.CreatedByUserId;
                Survey.PublicToken = existingSurvey.PublicToken;
                Survey.IsDeleted = existingSurvey.IsDeleted;
                Survey.DeletedAt = existingSurvey.DeletedAt;
                Survey.UpdatedAt = DateTime.Now;

                // Navigation properties'i null yap (ayrı yönetiliyor)
                Survey.Questions = null!;

                // Güncelle
                _context.Surveys.Update(Survey);
                await _context.SaveChangesAsync();

                TempData["SuccessMessage"] = "Anket başarıyla güncellendi.";
                return RedirectToPage("./Index");
            }
            catch (Exception ex)
            {
                ModelState.AddModelError("", $"Kayıt hatası: {ex.Message}");
                LoadStatusList();
                return Page();
            }
        }

        private bool SurveyExists(int id)
        {
            return _context.Surveys.Any(e => e.Id == id && !e.IsDeleted);
        }

        private void LoadStatusList()
        {
            StatusList = new SelectList(Enum.GetValues(typeof(SurveyStatus))
                .Cast<SurveyStatus>()
                .Select(s => new { Value = s, Text = s.ToString() }),
                "Value", "Text", Survey.Status);
        }
    }
}