using CEA.Business.Services;
using CEA.Core.Entities;
using CEA.Core.Enum;
using CEA.Core.Enums;
using CEA.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;

namespace CEA.Web.Pages.Survey
{
    public class IndexModel : PageModel
    {
        private readonly ApplicationDbContext _context;
        private readonly IComplaintAutomationService _complaintService;
        private readonly ILogger<IndexModel> _logger;

        [BindProperty(SupportsGet = true)]
        public string Token { get; set; } = string.Empty;

        [BindProperty]
        public CustomerInfoInput CustomerInfo { get; set; } = new();

        [BindProperty]
        public List<AnswerInput> Answers { get; set; } = new();

        public Core.Entities.Survey? Survey { get; set; }
        public List<Question> Questions { get; set; } = new();
        public bool IsCompleted { get; set; } = false;
        public string? ComplaintTicket { get; set; }

        public IndexModel(
            ApplicationDbContext context,
            IComplaintAutomationService complaintService,
            ILogger<IndexModel> logger)
        {
            _context = context;
            _complaintService = complaintService;
            _logger = logger;
        }

        public async Task<IActionResult> OnGetAsync()
        {
            if (string.IsNullOrEmpty(Token))
                return NotFound();

            Survey = await _context.Surveys
                .Include(s => s.Questions)
                .ThenInclude(q => q.Options)
                .FirstOrDefaultAsync(s => s.PublicToken == Token && !s.IsDeleted);

            if (Survey == null || Survey.Status != SurveyStatus.Active)
                return Page();

            if (Survey.StartDate.HasValue && Survey.StartDate > DateTime.Now)
                return Page();

            if (Survey.EndDate.HasValue && Survey.EndDate < DateTime.Now)
                return Page();

            Questions = Survey.Questions
                .Where(q => !q.IsDeleted)
                .OrderBy(q => q.DisplayOrder)
                .ToList();

            if (!Survey.AllowMultipleResponses && !string.IsNullOrEmpty(CustomerInfo.Email))
            {
                var existing = await _context.SurveyResponses
                    .AnyAsync(r => r.SurveyId == Survey.Id &&
                                  r.CustomerEmail == CustomerInfo.Email &&
                                  !r.IsDeleted);
                if (existing)
                {
                    ModelState.AddModelError("", "Bu e-posta adresi ile zaten yanıt verilmiş.");
                }
            }

            return Page();
        }

        public async Task<IActionResult> OnPostAsync()
        {
            Survey = await _context.Surveys
                .Include(s => s.Questions)
                .FirstOrDefaultAsync(s => s.PublicToken == Token && !s.IsDeleted);

            if (Survey == null)
                return NotFound();

            Questions = Survey.Questions
                .Where(q => !q.IsDeleted)
                .OrderBy(q => q.DisplayOrder)
                .ToList();

            if (!ModelState.IsValid)
                return Page();

            var response = new SurveyResponse
            {
                SurveyId = Survey.Id,
                CustomerEmail = CustomerInfo.Email,
                CustomerName = CustomerInfo.Name,
                CustomerPhone = CustomerInfo.Phone,
                SubmittedAt = DateTime.Now,
                CompletedAt = DateTime.Now,
                ResponseYear = DateTime.Now.Year,
                ResponseMonth = DateTime.Now.Month,
                IpAddress = HttpContext.Connection.RemoteIpAddress?.ToString(),
                UserAgent = Request.Headers["User-Agent"].ToString()
            };

            foreach (var answerInput in Answers)
            {
                var question = Questions.FirstOrDefault(q => q.Id == answerInput.QuestionId);
                if (question == null) continue;

                var answer = new Answer
                {
                    QuestionId = question.Id,
                    CreatedAt = DateTime.Now
                };

                switch (question.QuestionType)
                {
                    case QuestionType.RatingScale:
                    case QuestionType.NpsScore:
                        answer.NumericAnswer = answerInput.NumericAnswer;
                        answer.Score = answerInput.NumericAnswer;
                        break;

                    case QuestionType.SingleChoice:
                    case QuestionType.MultipleChoice:
                        answer.SelectedOptionIds = answerInput.SelectedOptionIds;
                        if (!string.IsNullOrEmpty(answerInput.SelectedOptionIds))
                        {
                            var optionIds = answerInput.SelectedOptionIds.Split(',');
                            var options = await _context.QuestionOptions
                                .Where(o => optionIds.Contains(o.Id.ToString()))
                                .ToListAsync();
                            answer.Score = (decimal?)options.Average(o => o.ScoreValue ?? 0);
                        }
                        break;

                    case QuestionType.TextOpen:
                        answer.TextAnswer = answerInput.TextAnswer;
                        break;
                }

                response.Answers.Add(answer);
            }

            // DÜZELTİLDİ: Satır 141 - (decimal?) cast eklendi
            var numericAnswers = response.Answers.Where(a => a.Score.HasValue).Select(a => a.Score.Value);
            if (numericAnswers.Any())
            {
                response.OverallSatisfactionScore = (decimal?)numericAnswers.Average();
            }

            // DÜZELTİLDİ: Satır 154 - null kontrolü eklendi
            var npsAnswer = response.Answers
                .FirstOrDefault(a => a.Question != null && a.Question.QuestionType == QuestionType.NpsScore);

            if (npsAnswer != null && npsAnswer.NumericAnswer.HasValue)
            {
                response.NpsScore = npsAnswer.NumericAnswer.Value;
            }

            _context.SurveyResponses.Add(response);
            await _context.SaveChangesAsync();

            _logger.LogInformation("Anket yanıtı kaydedildi: ResponseId={ResponseId}, SurveyId={SurveyId}",
                response.Id, Survey.Id);

            try
            {
                await _complaintService.CheckAndCreateComplaintAsync(response.Id);

                var complaints = await _context.Complaints
                    .Where(c => c.SurveyResponseId == response.Id)
                    .ToListAsync();

                if (complaints.Any())
                {
                    ComplaintTicket = complaints.First().TicketNumber;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Şikayet kontrolü sırasında hata");
            }

            IsCompleted = true;
            return Page();
        }
    }

    public class CustomerInfoInput
    {
        public string? Name { get; set; }
        [EmailAddress]
        public string? Email { get; set; }
        public string? Phone { get; set; }
    }

    public class AnswerInput
    {
        public int QuestionId { get; set; }
        public int? NumericAnswer { get; set; }
        public string? SelectedOptionIds { get; set; }
        public string? TextAnswer { get; set; }
    }
}