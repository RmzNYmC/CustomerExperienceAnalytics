using CEA.Core.Entities;
using CEA.Core.Enums;
using CEA.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace CEA.Web.Pages.Admin.Surveys.Questions
{
    [Authorize(Policy = "CanCreateSurvey")]
    public class CreateModel : PageModel
    {
        private readonly ApplicationDbContext _context;

        public CreateModel(ApplicationDbContext context)
        {
            _context = context;
        }

        [BindProperty]
        public Question Question { get; set; } = new();

        [BindProperty]
        public List<QuestionOption> Options { get; set; } = new();

        [BindProperty(SupportsGet = true)]
        public int SurveyId { get; set; }

        public SelectList QuestionTypes { get; set; } = default!;

        public IActionResult OnGet(int surveyId)
        {
            var survey = _context.Surveys.FirstOrDefault(s => s.Id == surveyId && !s.IsDeleted);
            if (survey == null) return NotFound();

            SurveyId = surveyId;

            Question = new Question
            {
                SurveyId = surveyId,
                DisplayOrder = (_context.Questions
                    .Where(q => q.SurveyId == surveyId && !q.IsDeleted)
                    .Max(q => (int?)q.DisplayOrder) ?? 0) + 1,
                QuestionType = QuestionType.SingleChoice,
                IsRequired = false,
                CreatedAt = DateTime.Now
            };

            LoadQuestionTypes();
            return Page();
        }

        public async Task<IActionResult> OnPostAsync()
        {
            
            // Navigation property validasyon hatalarını temizle
            ModelState.Remove("Question.Survey");
            foreach (var key in ModelState.Keys.Where(k => k.StartsWith("Options")).ToList())
            {
                ModelState.Remove(key);
            }

            // Debug için logla
            Console.WriteLine($"Soru Tipi: {Question.QuestionType}");
            Console.WriteLine($"SurveyId: {Question.SurveyId}");
            Console.WriteLine($"Text: {Question.Text}");
            Console.WriteLine($"Soru Tipi: {Question.QuestionType}");
            Console.WriteLine($"MinRating: {Question.MinRating}"); // Buraya bak
            Console.WriteLine($"MaxRating: {Question.MaxRating}");

            if (Question.SurveyId == 0)
                Question.SurveyId = SurveyId;

            LoadQuestionTypes();

            if (!ModelState.IsValid)
            {
                Console.WriteLine("ModelState geçersiz!");
                foreach (var error in ModelState.Values.SelectMany(v => v.Errors))
                {
                    Console.WriteLine($"Hata: {error.ErrorMessage}");
                }
                return Page();
            }

            try
            {
                bool needsOptions = Question.QuestionType == QuestionType.SingleChoice ||
                                   Question.QuestionType == QuestionType.MultipleChoice;

                // Eğer seçenek gerekiyorsa ve seçenek yoksa hata ver
                if (needsOptions)
                {
                    Options = Options?.Where(o => !string.IsNullOrWhiteSpace(o.Text)).ToList()
                        ?? new List<QuestionOption>();

                    if (Options.Count < 2)
                    {
                        ModelState.AddModelError("", "Bu soru tipi için en az 2 seçenek gerekli!");
                        return Page();
                    }
                }

                // 1. SORUYU KAYDET (Tüm tipler için)
                Question.CreatedAt = DateTime.Now;
                _context.Questions.Add(Question);
                await _context.SaveChangesAsync(); // Burada Question.Id oluşur

                Console.WriteLine($"Soru kaydedildi. ID: {Question.Id}");

                // 2. SEÇENEKLERİ KAYDET (Sadece Single/Multiple Choice için)
                if (needsOptions && Options.Any())
                {
                    for (int i = 0; i < Options.Count; i++)
                    {
                        _context.QuestionOptions.Add(new QuestionOption
                        {
                            QuestionId = Question.Id,
                            Text = Options[i].Text,
                            DisplayOrder = i,
                            CreatedAt = DateTime.Now
                        });
                    }
                    await _context.SaveChangesAsync();
                    Console.WriteLine($"{Options.Count} seçenek kaydedildi.");
                }

                TempData["SuccessMessage"] = "Soru başarıyla eklendi.";
                return RedirectToPage("/Admin/Surveys/Edit", new { id = Question.SurveyId });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"HATA: {ex.Message}");
                Console.WriteLine($"Stack: {ex.StackTrace}");
                ModelState.AddModelError("", $"Kayıt hatası: {ex.Message}");
                return Page();
            }
        }

        private void LoadQuestionTypes()
        {
            QuestionTypes = new SelectList(Enum.GetValues(typeof(QuestionType))
                .Cast<QuestionType>()
                .Select(t => new { Value = t.ToString(), Text = t.ToString() }),
                "Value", "Text");
        }
    }
}