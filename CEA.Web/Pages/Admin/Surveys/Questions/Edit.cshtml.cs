using CEA.Core.Entities;
using CEA.Core.Enums;
using CEA.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace CEA.Web.Pages.Admin.Surveys.Questions
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
        public Question Question { get; set; } = new();

        [BindProperty]
        public List<string> OptionTexts { get; set; } = new();

        public async Task<IActionResult> OnGetAsync(int? id)
        {
            if (id == null) return NotFound();

            Question = await _context.Questions
                .Include(q => q.Options)
                .FirstOrDefaultAsync(q => q.Id == id && !q.IsDeleted);

            if (Question == null) return NotFound();

            return Page();
        }

        public async Task<IActionResult> OnPostAsync()
        {
            ModelState.Remove("Question.Survey");
            ModelState.Remove("Question.Options");

            if (!ModelState.IsValid)
            {
                // Options'ları tekrar yükle
                Question = await _context.Questions
                    .Include(q => q.Options)
                    .FirstOrDefaultAsync(q => q.Id == Question.Id);
                return Page();
            }

            var question = await _context.Questions.FindAsync(Question.Id);
            if (question == null) return NotFound();

            question.Text = Question.Text;
            question.QuestionType = Question.QuestionType;
            question.DisplayOrder = Question.DisplayOrder;
            question.IsRequired = Question.IsRequired;
            question.UpdatedAt = DateTime.Now;

            await _context.SaveChangesAsync();

            return RedirectToPage("/Admin/Surveys/Edit", new { id = question.SurveyId });
        }
    }
}