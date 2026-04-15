using CEA.Core.Entities;
using CEA.Core.Enum;
using CEA.Data;
using CEA.Web.Dtos.Surveys;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace CEA.Web.Controllers
{
    [Route("api/surveys")]
    [ApiController]
    [Authorize(Policy = "CanManageSurveys")]
    public class SurveysApiController : ControllerBase
    {
        private readonly ApplicationDbContext _context;

        public SurveysApiController(ApplicationDbContext context)
        {
            _context = context;
        }

        [HttpGet]
        public async Task<IActionResult> GetSurveys()
        {
            var surveys = await _context.Surveys
                .AsNoTracking()
                .Where(s => !s.IsDeleted)
                .OrderByDescending(s => s.CreatedAt)
                .Select(s => new SurveyListDto
                {
                    Id = s.Id,
                    Title = s.Title,
                    Description = s.Description,
                    Status = s.Status,
                    PublicToken = s.PublicToken,
                    AnalysisYear = s.AnalysisYear,
                    AnalysisMonth = s.AnalysisMonth,
                    StartDate = s.StartDate,
                    EndDate = s.EndDate,
                    RequiresAuthentication = s.RequiresAuthentication,
                    AllowMultipleResponses = s.AllowMultipleResponses,
                    ResponseCount = s.Responses.Count(r => !r.IsDeleted),
                    QuestionCount = s.Questions.Count(q => !q.IsDeleted),
                    CreatedAt = s.CreatedAt
                })
                .ToListAsync();

            return Ok(surveys);
        }

        [HttpGet("{id:int}")]
        public async Task<IActionResult> GetSurvey(int id)
        {
            var survey = await _context.Surveys
                .AsNoTracking()
                .Include(s => s.Questions.Where(q => !q.IsDeleted).OrderBy(q => q.DisplayOrder))
                .ThenInclude(q => q.Options.Where(o => !o.IsDeleted).OrderBy(o => o.DisplayOrder))
                .Include(s => s.Responses.Where(r => !r.IsDeleted))
                .FirstOrDefaultAsync(s => s.Id == id && !s.IsDeleted);

            if (survey == null)
                return NotFound(new { message = "Anket bulunamadı." });

            var result = new SurveyDetailDto
            {
                Id = survey.Id,
                Title = survey.Title,
                Description = survey.Description,
                WelcomeMessage = survey.WelcomeMessage,
                ThankYouMessage = survey.ThankYouMessage,
                Status = survey.Status,
                StartDate = survey.StartDate,
                EndDate = survey.EndDate,
                AnalysisYear = survey.AnalysisYear,
                AnalysisMonth = survey.AnalysisMonth,
                RequiresAuthentication = survey.RequiresAuthentication,
                AllowMultipleResponses = survey.AllowMultipleResponses,
                PublicToken = survey.PublicToken,
                CreatedAt = survey.CreatedAt,
                ResponseCount = survey.Responses.Count,
                Questions = survey.Questions.Select(q => new SurveyQuestionDto
                {
                    Id = q.Id,
                    Text = q.Text,
                    Description = q.Description,
                    QuestionType = q.QuestionType,
                    IsRequired = q.IsRequired,
                    DisplayOrder = q.DisplayOrder,
                    MinRating = q.MinRating,
                    MaxRating = q.MaxRating,
                    TriggerComplaintOnLowRating = q.TriggerComplaintOnLowRating,
                    ComplaintThreshold = q.ComplaintThreshold,
                    Options = q.Options.Select(o => new SurveyOptionDto
                    {
                        Id = o.Id,
                        Text = o.Text,
                        ScoreValue = o.ScoreValue ?? 0,
                        DisplayOrder = o.DisplayOrder
                    }).ToList()
                }).ToList()
            };

            return Ok(result);
        }

        [HttpPost]
        public async Task<IActionResult> CreateSurvey([FromBody] SurveyCreateDto request)
        {
            if (!ModelState.IsValid)
                return ValidationProblem(ModelState);

            if (request.Questions == null || request.Questions.Count == 0)
                return BadRequest(new { message = "En az bir soru eklemelisiniz." });

            var survey = new Survey
            {
                Title = request.Title.Trim(),
                Description = request.Description,
                WelcomeMessage = request.WelcomeMessage,
                ThankYouMessage = request.ThankYouMessage,
                StartDate = request.StartDate,
                EndDate = request.EndDate,
                Status = (SurveyStatus)request.Status,
                AnalysisYear = request.AnalysisYear,
                AnalysisMonth = request.AnalysisType == "Monthly" ? request.AnalysisMonth : null,
                RequiresAuthentication = request.RequiresAuthentication,
                AllowMultipleResponses = request.AllowMultipleResponses,
                PublicToken = Guid.NewGuid().ToString("N")[..10],
                CreatedByUserId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "unknown",
                CreatedAt = DateTime.Now,
                CreatedBy = User.Identity?.Name ?? "api"
            };

            int displayOrder = 0;
            foreach (var item in request.Questions)
            {
                var question = new Question
                {
                    Text = item.Text,
                    Description = item.Description,
                    QuestionType = (QuestionType)item.QuestionType,
                    IsRequired = item.IsRequired,
                    DisplayOrder = displayOrder++,
                    TriggerComplaintOnLowRating = item.TriggerComplaintOnLowRating,
                    ComplaintThreshold = item.ComplaintThreshold,
                    MinRating = (QuestionType)item.QuestionType == QuestionType.NpsScore ? 0 : item.MinRating,
                    MaxRating = (QuestionType)item.QuestionType == QuestionType.NpsScore ? 10 : item.MaxRating,
                    CreatedAt = DateTime.Now,
                    CreatedBy = User.Identity?.Name ?? "api"
                };

                if ((QuestionType)item.QuestionType == QuestionType.RatingScale)
                {
                    question.MinRating ??= 1;
                    question.MaxRating ??= 5;
                }

                int optionOrder = 0;
                foreach (var option in item.Options ?? new List<SurveyOptionCreateDto>())
                {
                    question.Options.Add(new QuestionOption
                    {
                        Text = option.Text,
                        ScoreValue = option.ScoreValue,
                        DisplayOrder = optionOrder++,
                        CreatedAt = DateTime.Now,
                        CreatedBy = User.Identity?.Name ?? "api"
                    });
                }

                survey.Questions.Add(question);
            }

            _context.Surveys.Add(survey);
            await _context.SaveChangesAsync();

            return CreatedAtAction(nameof(GetSurvey), new { id = survey.Id }, new
            {
                survey.Id,
                survey.Title,
                survey.PublicToken,
                message = "Anket başarıyla oluşturuldu."
            });
        }

        [HttpPut("{id:int}")]
        public async Task<IActionResult> UpdateSurvey(int id, [FromBody] SurveyUpdateDto request)
        {
            if (!ModelState.IsValid)
                return ValidationProblem(ModelState);

            var survey = await _context.Surveys.FirstOrDefaultAsync(s => s.Id == id && !s.IsDeleted);
            if (survey == null)
                return NotFound(new { message = "Anket bulunamadı." });

            survey.Title = request.Title.Trim();
            survey.Description = request.Description;
            survey.WelcomeMessage = request.WelcomeMessage;
            survey.ThankYouMessage = request.ThankYouMessage;
            survey.StartDate = request.StartDate;
            survey.EndDate = request.EndDate;
            survey.Status = request.Status;
            survey.AnalysisYear = request.AnalysisYear;
            survey.AnalysisMonth = request.AnalysisType == "Monthly" ? request.AnalysisMonth : null;
            survey.RequiresAuthentication = request.RequiresAuthentication;
            survey.AllowMultipleResponses = request.AllowMultipleResponses;
            survey.UpdatedAt = DateTime.Now;
            survey.UpdatedBy = User.Identity?.Name ?? "api";

            await _context.SaveChangesAsync();
            return Ok(new { message = "Anket başarıyla güncellendi." });
        }

        [HttpPatch("{id:int}/status")]
        public async Task<IActionResult> UpdateStatus(int id, [FromBody] SurveyStatusUpdateDto request)
        {
            var survey = await _context.Surveys.FirstOrDefaultAsync(s => s.Id == id && !s.IsDeleted);
            if (survey == null)
                return NotFound(new { message = "Anket bulunamadı." });

            survey.Status = (SurveyStatus)request.Status;
            survey.UpdatedAt = DateTime.Now;
            survey.UpdatedBy = User.Identity?.Name ?? "api";

            await _context.SaveChangesAsync();
            return Ok(new { message = "Anket durumu güncellendi." });
        }

        [HttpDelete("{id:int}")]
        public async Task<IActionResult> DeleteSurvey(int id)
        {
            var survey = await _context.Surveys
                .Include(s => s.Responses)
                .FirstOrDefaultAsync(s => s.Id == id && !s.IsDeleted);

            if (survey == null)
                return NotFound(new { message = "Anket bulunamadı." });

            survey.IsDeleted = true;
            survey.DeletedAt = DateTime.Now;
            survey.UpdatedAt = DateTime.Now;
            survey.UpdatedBy = User.Identity?.Name ?? "api";

            foreach (var response in survey.Responses.Where(r => !r.IsDeleted))
            {
                response.IsDeleted = true;
                response.DeletedAt = DateTime.Now;
                response.UpdatedAt = DateTime.Now;
                response.UpdatedBy = User.Identity?.Name ?? "api";
            }

            await _context.SaveChangesAsync();
            return Ok(new { message = "Anket ve ilişkili yanıtları silindi." });
        }
    }

    public class SurveyListDto
    {
        public int Id { get; set; }
        public string Title { get; set; } = string.Empty;
        public string? Description { get; set; }
        public SurveyStatus Status { get; set; }
        public string PublicToken { get; set; } = string.Empty;
        public int AnalysisYear { get; set; }
        public int? AnalysisMonth { get; set; }
        public DateTime? StartDate { get; set; }
        public DateTime? EndDate { get; set; }
        public bool RequiresAuthentication { get; set; }
        public bool AllowMultipleResponses { get; set; }
        public int ResponseCount { get; set; }
        public int QuestionCount { get; set; }
        public DateTime CreatedAt { get; set; }
    }

    public class SurveyDetailDto : SurveyListDto
    {
        public string? WelcomeMessage { get; set; }
        public string? ThankYouMessage { get; set; }
        public List<SurveyQuestionDto> Questions { get; set; } = new();
    }

    public class SurveyQuestionDto
    {
        public int Id { get; set; }
        public string Text { get; set; } = string.Empty;
        public string? Description { get; set; }
        public QuestionType QuestionType { get; set; }
        public bool IsRequired { get; set; }
        public int DisplayOrder { get; set; }
        public int? MinRating { get; set; }
        public int? MaxRating { get; set; }
        public bool TriggerComplaintOnLowRating { get; set; }
        public int? ComplaintThreshold { get; set; }
        public List<SurveyOptionDto> Options { get; set; } = new();
    }

    public class SurveyOptionDto
    {
        public int Id { get; set; }
        public string Text { get; set; } = string.Empty;
        public int ScoreValue { get; set; }
        public int DisplayOrder { get; set; }
    }
}
