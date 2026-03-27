using CEA.Core.Entities;
using CEA.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace CEA.Web.Pages.Admin.Surveys.Questions
{
    [Authorize(Policy = "CanCreateSurvey")]
    public class DeleteModel : PageModel
    {
        private readonly ApplicationDbContext _context;

        public DeleteModel(ApplicationDbContext context)
        {
            _context = context;
        }

        [BindProperty(SupportsGet = true)]
        public int Id { get; set; }

        [BindProperty(SupportsGet = true)]
        public int SurveyId { get; set; }

        public async Task<IActionResult> OnGetAsync()
        {
            var question = await _context.Questions
                .Include(q => q.Options)
                .FirstOrDefaultAsync(q => q.Id == Id && !q.IsDeleted);

            if (question == null)
            {
                TempData["ErrorMessage"] = "Soru bulunamadı.";
                return RedirectToPage("/Admin/Surveys/Edit", new { id = SurveyId });
            }

            // Soft delete - Soru
            question.IsDeleted = true;
            question.DeletedAt = DateTime.Now;

            // Soft delete - Seçenekler
            foreach (var option in question.Options.Where(o => !o.IsDeleted))
            {
                option.IsDeleted = true;
                option.DeletedAt = DateTime.Now;
            }

            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = "Soru başarıyla silindi.";
            return RedirectToPage("/Admin/Surveys/Edit", new { id = SurveyId });
        }

        public IActionResult OnPost()
        {
            // GET üzerinden çalışıyor
            return RedirectToPage("/Admin/Surveys/Edit", new { id = SurveyId });
        }
    }
}