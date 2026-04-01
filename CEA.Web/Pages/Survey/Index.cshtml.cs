using CEA.Business.Services;  // ⭐ EKLENDİ
using CEA.Core.Entities;
using CEA.Core.Enum;
using CEA.Core.Enums;
using CEA.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace CEA.Web.Pages.Survey
{
    public class IndexModel : PageModel
    {
        private readonly ApplicationDbContext _context;
        private readonly IComplaintAutomationService _complaintService;  // ⭐ EKLENDİ

        // ⭐ EKLENDİ: Constructor'a service eklendi
        public IndexModel(ApplicationDbContext context, IComplaintAutomationService complaintService)
        {
            _context = context;
            _complaintService = complaintService;
        }

        // ... mevcut property'ler aynen kalıyor ...
        [BindProperty(SupportsGet = true)]
        public string Token { get; set; } = string.Empty;

        public CEA.Core.Entities.Survey? Survey { get; set; }
        public List<CEA.Core.Entities.Question> Questions { get; set; } = new();

        [BindProperty]
        public CEA.Core.Entities.SurveyResponse Response { get; set; } = new();

        [BindProperty]
        public List<AnswerViewModel> Answers { get; set; } = new();

        public string? ErrorMessage { get; set; }
        public string? SuccessMessage { get; set; }
        public bool ShowForm { get; set; } = true;

        public async Task<IActionResult> OnGetAsync()
        {
            if (string.IsNullOrEmpty(Token))
                return NotFound();

            Survey = await _context.Surveys
                .FirstOrDefaultAsync(s => s.PublicToken == Token && !s.IsDeleted
                    && s.Status == SurveyStatus.Active);

            if (Survey == null)
            {
                ErrorMessage = "Anket bulunamadı veya süresi dolmuş.";
                ShowForm = false;
                return Page();
            }

            Questions = await _context.Questions
                .Where(q => q.SurveyId == Survey.Id && !q.IsDeleted)
                .Include(q => q.Options)
                .OrderBy(q => q.DisplayOrder)
                .ToListAsync();

            ShowForm = true;
            return Page();
        }

        public async Task<IActionResult> OnPostAsync()
        {
            // Navigation property'leri validasyondan çıkar
            ModelState.Remove("Response.Survey");
            ModelState.Remove("Response.Customer");
            ModelState.Remove("Response.User");

            foreach (var key in ModelState.Keys.Where(k => k.StartsWith("Answers[") && k.EndsWith("].Value")).ToList())
            {
                ModelState.Remove(key);
            }

            Survey = await _context.Surveys
                .FirstOrDefaultAsync(s => s.PublicToken == Token && !s.IsDeleted);

            if (Survey == null)
                return NotFound();

            if (!ModelState.IsValid)
            {
                var errors = string.Join(", ", ModelState.Values
                    .SelectMany(v => v.Errors)
                    .Select(e => e.ErrorMessage));

                ErrorMessage = "Lütfen zorunlu alanları doldurun: " + errors;

                Questions = await _context.Questions
                    .Where(q => q.SurveyId == Survey.Id && !q.IsDeleted)
                    .Include(q => q.Options)
                    .OrderBy(q => q.DisplayOrder)
                    .ToListAsync();
                return Page();
            }

            try
            {
                // Aynı email kontrolü
                if (!string.IsNullOrEmpty(Response.CustomerEmail))
                {
                    var existingResponse = await _context.SurveyResponses
                        .FirstOrDefaultAsync(r => r.SurveyId == Survey.Id
                            && r.CustomerEmail == Response.CustomerEmail
                            && !r.IsDeleted);

                    if (existingResponse != null)
                    {
                        SuccessMessage = "Bu e-posta adresi ile anketi zaten tamamladınız. Değerli geri dönüşleriniz için teşekkür ederiz!";
                        ShowForm = false;
                        return Page();
                    }
                }

                // Yanıtı kaydet
                Response.SurveyId = Survey.Id;
                Response.SubmittedAt = DateTime.Now;
                Response.IpAddress = HttpContext.Connection.RemoteIpAddress?.ToString();
                Response.UserAgent = Request.Headers["User-Agent"].ToString();
                Response.ResponseYear = DateTime.Now.Year;
                Response.ResponseMonth = DateTime.Now.Month;

                // CustomerId'yi bul
                if (!string.IsNullOrEmpty(Response.CustomerEmail))
                {
                    var existingCustomer = await _context.Customers
                        .FirstOrDefaultAsync(c => c.Email == Response.CustomerEmail && !c.IsDeleted);

                    if (existingCustomer != null)
                    {
                        Response.CustomerId = existingCustomer.Id;
                    }
                }

                _context.SurveyResponses.Add(Response);
                await _context.SaveChangesAsync();

                // Cevapları kaydet
                if (Answers != null && Answers.Any())
                {
                    foreach (var answer in Answers)
                    {
                        if (string.IsNullOrWhiteSpace(answer.Value) && !answer.RatingValue.HasValue)
                            continue;

                        var newAnswer = new CEA.Core.Entities.Answer
                        {
                            ResponseId = Response.Id,
                            QuestionId = answer.QuestionId,
                            CreatedAt = DateTime.Now
                        };

                        if (answer.RatingValue.HasValue)
                        {
                            newAnswer.NumericAnswer = answer.RatingValue;
                            newAnswer.TextAnswer = answer.Value;
                        }
                        else
                        {
                            newAnswer.TextAnswer = answer.Value;
                        }

                        _context.Answers.Add(newAnswer);
                    }

                    await _context.SaveChangesAsync();
                }

                // ⭐⭐⭐ BURASI EKLENDİ: Şikayet kontrolü ⭐⭐⭐
                await _complaintService.CheckAndCreateComplaintAsync(Response.Id);

                return RedirectToPage("/Survey/ThankYou", new { token = Token });
            }
            catch (Exception ex)
            {
                ErrorMessage = "Bir hata oluştu: " + ex.Message;

                if (Survey != null)
                {
                    Questions = await _context.Questions
                        .Where(q => q.SurveyId == Survey.Id && !q.IsDeleted)
                        .Include(q => q.Options)
                        .OrderBy(q => q.DisplayOrder)
                        .ToListAsync();
                }
                return Page();
            }
        }
    }

    public class AnswerViewModel
    {
        public int QuestionId { get; set; }
        public string? Value { get; set; }
        public int? RatingValue { get; set; }
    }
}