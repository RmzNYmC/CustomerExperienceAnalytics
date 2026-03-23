using CEA.Core.Entities; // Bu using zaten var olmalı
using CEA.Core.Enum;
using CEA.Core.Enums;
using CEA.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using System.ComponentModel.DataAnnotations;
using System.Text.Json;

namespace CEA.Web.Pages.Admin.Surveys
{
    [Authorize(Policy = "CanCreateSurvey")]
    public class CreateModel : PageModel
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogger<CreateModel> _logger;

        [BindProperty]
        public SurveyInput Input { get; set; } = new();

        // DÜZELTİLDİ: Tam namespace kullanarak çatışmayı önle
        public Core.Entities.Survey? Survey { get; set; }

        public CreateModel(ApplicationDbContext context, ILogger<CreateModel> logger)
        {
            _context = context;
            _logger = logger;
        }

        public void OnGet()
        {
        }

        public async Task<IActionResult> OnPostAsync()
        {
            if (!ModelState.IsValid)
            {
                return Page();
            }

            try
            {
                var questions = JsonSerializer.Deserialize<List<QuestionInput>>(Input.QuestionsJson);

                if (questions == null || !questions.Any())
                {
                    ModelState.AddModelError("", "En az bir soru eklemelisiniz.");
                    return Page();
                }

                // DÜZELTİLDİ: Tam namespace ile
                var survey = new CEA.Core.Entities.Survey
                {
                    Title = Input.Title,
                    Description = Input.Description,
                    WelcomeMessage = Input.WelcomeMessage,
                    ThankYouMessage = Input.ThankYouMessage,
                    StartDate = Input.StartDate,
                    EndDate = Input.EndDate,
                    Status = SurveyStatus.Draft,
                    AnalysisYear = Input.AnalysisYear,
                    AnalysisMonth = Input.AnalysisType == "Monthly" ? Input.AnalysisMonth : null,
                    RequiresAuthentication = Input.RequiresAuthentication,
                    AllowMultipleResponses = Input.AllowMultipleResponses,
                    PublicToken = Guid.NewGuid().ToString("N")[..10], // 10 karakterlik unique token
                    CreatedByUserId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value ?? "unknown",
                    CreatedAt = DateTime.Now
                };

                // Soruları ekle
                int order = 0;
                foreach (var qInput in questions)
                {
                    var question = new Question
                    {
                        Text = qInput.text,
                        QuestionType = (QuestionType)qInput.type,
                        IsRequired = qInput.required,
                        DisplayOrder = order++,
                        TriggerComplaintOnLowRating = qInput.triggerComplaint,
                        ComplaintThreshold = qInput.threshold,
                        CreatedAt = DateTime.Now,
                        CreatedBy = User.Identity?.Name ?? "unknown"
                    };

                    // Rating için min/max
                    if (question.QuestionType == QuestionType.RatingScale)
                    {
                        question.MinRating = 1;
                        question.MaxRating = 5;
                    }
                    else if (question.QuestionType == QuestionType.NpsScore)
                    {
                        question.MinRating = 0;
                        question.MaxRating = 10;
                    }

                    // Seçenekleri ekle
                    if (qInput.options != null)
                    {
                        int optOrder = 0;
                        foreach (var opt in qInput.options)
                        {
                            question.Options.Add(new QuestionOption
                            {
                                Text = opt.text,
                                ScoreValue = opt.score,
                                DisplayOrder = optOrder++,
                                CreatedAt = DateTime.Now,
                                CreatedBy = User.Identity?.Name ?? "unknown"
                            });
                        }
                    }

                    survey.Questions.Add(question);
                }

                _context.Surveys.Add(survey);
                await _context.SaveChangesAsync();

                _logger.LogInformation("Yeni anket oluşturuldu: {SurveyId} - {Title}", survey.Id, survey.Title);

                return RedirectToPage("/Admin/Surveys/Index");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Anket oluşturma hatası");
                ModelState.AddModelError("", "Anket kaydedilirken bir hata oluştu: " + ex.Message);
                return Page();
            }
            return RedirectToPage("/Admin/Surveys/Index");
        }
    }

    // ... (SurveyInput, QuestionInput, OptionInput sınıfları aynı kalır)
    public class SurveyInput
    {
        [Required(ErrorMessage = "Başlık zorunludur")]
        [StringLength(200)]
        public string Title { get; set; } = string.Empty;

        [StringLength(500)]
        public string? Description { get; set; }

        public string? WelcomeMessage { get; set; }
        public string? ThankYouMessage { get; set; }

        public DateTime? StartDate { get; set; }
        public DateTime? EndDate { get; set; }

        [Required]
        public string AnalysisType { get; set; } = "Yearly";

        public int AnalysisYear { get; set; } = DateTime.Now.Year;
        public int? AnalysisMonth { get; set; }

        public bool RequiresAuthentication { get; set; }
        public bool AllowMultipleResponses { get; set; }

        [Required]
        public string QuestionsJson { get; set; } = "[]";
    }

    public class QuestionInput
    {
        public string text { get; set; } = string.Empty;
        public int type { get; set; }
        public bool required { get; set; }
        public bool triggerComplaint { get; set; }
        public int? threshold { get; set; }
        public List<OptionInput> options { get; set; } = new();
    }

    public class OptionInput
    {
        public string text { get; set; } = string.Empty;
        public int score { get; set; }
    }
}