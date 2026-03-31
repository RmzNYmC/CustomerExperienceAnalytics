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

        // MEVCUT seçenekler için - Form'dan gelen verileri karşıla
        [BindProperty]
        public List<int> ExistingOptionIds { get; set; } = new();

        [BindProperty]
        public List<string> ExistingOptionTexts { get; set; } = new();

        // Silinecek seçenekler
        [BindProperty]
        public List<int> DeletedOptionIds { get; set; } = new();

        // YENİ eklenecek seçenekler
        [BindProperty]
        public List<string> NewOptions { get; set; } = new();

        public async Task<IActionResult> OnGetAsync(int? id)
        {
            if (id == null) return NotFound();

            Question = await _context.Questions
                .Include(q => q.Options.Where(o => !o.IsDeleted))
                .FirstOrDefaultAsync(q => q.Id == id && !q.IsDeleted);

            if (Question == null) return NotFound();

            return Page();
        }

        public async Task<IActionResult> OnPostAsync()

        {

            // Debug için - Konsola yazdır (VS Output penceresinde görürsünüz)
            Console.WriteLine($"Gelen Silme ID'leri: {string.Join(", ", DeletedOptionIds ?? new List<int>())}");
            Console.WriteLine($"Adet: {DeletedOptionIds?.Count ?? 0}");

            ModelState.Remove("Question.Survey");
            ModelState.Remove("Question.Options");

            // Debug log
            Console.WriteLine($"Silinecek ID'ler: {string.Join(", ", DeletedOptionIds ?? new List<int>())}");

            if (!ModelState.IsValid)
            {
                Question = await _context.Questions
                    .Include(q => q.Options.Where(o => !o.IsDeleted))
                    .FirstOrDefaultAsync(q => q.Id == Question.Id);
                return Page();
            }

            // ✅ DÜZELTME: Include ile Options'ları yükle
            var question = await _context.Questions
                .Include(q => q.Options)
                .FirstOrDefaultAsync(q => q.Id == Question.Id);

            if (question == null) return NotFound();

            // 1. SORUYU GÜNCELLE
            question.Text = Question.Text;
            question.QuestionType = Question.QuestionType;
            question.DisplayOrder = Question.DisplayOrder;
            question.IsRequired = Question.IsRequired;
            question.MinRating = Question.MinRating;
            question.MaxRating = Question.MaxRating;
            question.UpdatedAt = DateTime.Now;

            // 2. SİLİNEN SEÇENEKLERİ İŞARETLE (Soft Delete) - NULL CHECK EKLE
            if (DeletedOptionIds != null && DeletedOptionIds.Any())
            {
                foreach (var deletedId in DeletedOptionIds)
                {
                    var optionToDelete = question.Options.FirstOrDefault(o => o.Id == deletedId);
                    if (optionToDelete != null)
                    {
                        optionToDelete.IsDeleted = true;
                        optionToDelete.UpdatedAt = DateTime.Now;
                        Console.WriteLine($"Seçenek silindi: {deletedId}"); // Debug
                    }
                    else
                    {
                        Console.WriteLine($"UYARI: ID {deletedId} bulunamadı!"); // Debug
                    }
                }
            }

            // 3. MEVCUT SEÇENEKLERİ GÜNCELLE
            if (question.QuestionType == QuestionType.SingleChoice ||
                question.QuestionType == QuestionType.MultipleChoice)
            {
                for (int i = 0; i < ExistingOptionIds.Count; i++)
                {
                    var option = question.Options.FirstOrDefault(o => o.Id == ExistingOptionIds[i]);
                    if (option != null && !option.IsDeleted)
                    {
                        option.Text = ExistingOptionTexts[i];
                        option.DisplayOrder = i;
                        option.UpdatedAt = DateTime.Now;
                    }
                }

                // 4. YENİ SEÇENEKLERİ EKLE
                int startOrder = question.Options.Count(o => !o.IsDeleted);
                for (int i = 0; i < NewOptions.Count; i++)
                {
                    if (!string.IsNullOrWhiteSpace(NewOptions[i]))
                    {
                        _context.QuestionOptions.Add(new QuestionOption
                        {
                            QuestionId = question.Id,
                            Text = NewOptions[i],
                            DisplayOrder = startOrder + i,
                            CreatedAt = DateTime.Now
                        });
                    }
                }
            }

            await _context.SaveChangesAsync();
            TempData["SuccessMessage"] = "Soru ve seçenekler başarıyla güncellendi.";

            return RedirectToPage("/Admin/Surveys/Edit", new { id = question.SurveyId });
        }
    }
}