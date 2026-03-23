using CEA.Core.Entities;
using CEA.Core.Enum;
using CEA.Core.Enums;
using CEA.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace CEA.Business.Services
{
    public interface IComplaintAutomationService
    {
        Task CheckAndCreateComplaintAsync(int responseId);
        Task AutoAssignComplaintAsync(int complaintId);
    }

    public class ComplaintAutomationService : IComplaintAutomationService
    {
        private readonly ApplicationDbContext _context;
        private readonly IEmailService _emailService;
        private readonly ILogger<ComplaintAutomationService> _logger;

        public ComplaintAutomationService(
            ApplicationDbContext context,
            IEmailService emailService,
            ILogger<ComplaintAutomationService> logger)
        {
            _context = context;
            _emailService = emailService;
            _logger = logger;
        }

        public async Task CheckAndCreateComplaintAsync(int responseId)
        {
            var response = await _context.SurveyResponses
                .Include(r => r.Answers)
                .ThenInclude(a => a.Question)
                .Include(r => r.Survey)
                .FirstOrDefaultAsync(r => r.Id == responseId);

            if (response == null) return;

            foreach (var answer in response.Answers)
            {
                var question = answer.Question;

                if (!question.TriggerComplaintOnLowRating || !question.ComplaintThreshold.HasValue)
                    continue;

                bool shouldCreateComplaint = false;
                string complaintDescription = "";

                switch (question.QuestionType)
                {
                    case QuestionType.RatingScale:
                    case QuestionType.NpsScore:
                        if (answer.NumericAnswer.HasValue &&
                            answer.NumericAnswer.Value <= question.ComplaintThreshold.Value)
                        {
                            shouldCreateComplaint = true;
                            complaintDescription = $"Müşteri '{question.Text}' sorusuna " +
                                $"'{answer.NumericAnswer.Value}' puan verdi (Eşik: {question.ComplaintThreshold.Value}). " +
                                $"Beklentinin altında bir deneyim yaşamış olabilir.";
                        }
                        break;

                    case QuestionType.SingleChoice:
                        if (!string.IsNullOrEmpty(answer.SelectedOptionIds))
                        {
                            var selectedIds = answer.SelectedOptionIds.Split(',');
                            var options = await _context.QuestionOptions
                                .Where(o => selectedIds.Contains(o.Id.ToString()) && o.QuestionId == question.Id)
                                .ToListAsync();

                            var negativeOption = options.FirstOrDefault(o =>
                                o.Text.Contains("memnun değil") ||
                                o.Text.Contains("kötü") ||
                                o.Text.Contains("şikayet"));

                            if (negativeOption != null)
                            {
                                shouldCreateComplaint = true;
                                complaintDescription = $"Müşteri '{question.Text}' sorusunda " +
                                    $"'{negativeOption.Text}' seçeneğini işaretledi.";
                            }
                        }
                        break;
                }

                if (shouldCreateComplaint)
                {
                    await CreateComplaint(response, question, complaintDescription, answer);
                }
            }
        }

        private async Task CreateComplaint(
            SurveyResponse response,
            Question question,
            string description,
            Answer triggerAnswer)
        {
            var year = DateTime.Now.Year;
            var count = await _context.Complaints
                .CountAsync(c => c.CreatedAt.Year == year) + 1;
            var ticketNumber = $"CMP-{year}-{count:D4}";

            var complaint = new Complaint
            {
                TicketNumber = ticketNumber,
                Title = $"Otomatik Şikayet: {response.Survey.Title}",
                Description = description,
                Category = DetermineCategory(question.Text),
                Priority = DeterminePriority(triggerAnswer),
                CustomerEmail = response.CustomerEmail ?? "bilinmiyor@temp.com",
                CustomerName = response.CustomerName,
                CustomerPhone = response.CustomerPhone,
                SurveyResponseId = response.Id,
                TriggerQuestionId = question.Id,
                Status = ComplaintStatus.New,
                CreatedAt = DateTime.Now,
                CreatedBy = "System"
            };

            _context.Complaints.Add(complaint);
            await _context.SaveChangesAsync();

            _logger.LogInformation("Şikayet oluşturuldu: {TicketNumber}", ticketNumber);

            // Yöneticilere mail gönder
            await NotifyManagers(complaint);

            // Otomatik atama dene
            await AutoAssignComplaintAsync(complaint.Id);
        }

        private string DetermineCategory(string questionText)
        {
            if (questionText.Contains("ürün") || questionText.Contains("kalite"))
                return "Ürün";
            if (questionText.Contains("hizmet") || questionText.Contains("servis"))
                return "Hizmet";
            if (questionText.Contains("personel") || questionText.Contains("çalışan"))
                return "Personel";
            if (questionText.Contains("teslimat") || questionText.Contains("kargo"))
                return "Lojistik";
            return "Genel";
        }

        private string DeterminePriority(Answer answer)
        {
            if (answer.NumericAnswer.HasValue)
            {
                if (answer.NumericAnswer.Value <= 2) return "Critical";
                if (answer.NumericAnswer.Value <= 4) return "High";
            }
            return "Medium";
        }

        private async Task NotifyManagers(Complaint complaint)
        {
            // Şikayet yöneticisi yetkisine sahip kullanıcıları bul
            var managers = await _context.Users
                .Where(u => u.IsActive)
                .ToListAsync(); // Basitleştirildi - gerçek uygulamada role göre filtrele

            foreach (var user in managers)
            {
                // NULL KONTROLÜ EKLENDİ
                if (string.IsNullOrEmpty(user.Email))
                    continue;

                try
                {
                    await _emailService.SendComplaintNotificationAsync(
                        user.Email,  // Artık null değil garantisi var
                        complaint.TicketNumber,
                        complaint.CustomerName ?? "İsimsiz Müşteri",
                        complaint.Description
                    );
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Mail gönderimi başarısız: {Email}", user.Email);
                }
            }
        }

        public async Task AutoAssignComplaintAsync(int complaintId)
        {
            var complaint = await _context.Complaints.FindAsync(complaintId);
            if (complaint == null || !string.IsNullOrEmpty(complaint.AssignedToUserId))
                return;

            // En az şikayeti olan aktif kullanıcıyı bul
            var assignee = await _context.Users
                .Where(u => u.IsActive)
                .GroupJoin(
                    _context.Complaints.Where(c => c.Status != ComplaintStatus.Closed && !c.IsDeleted),
                    u => u.Id,
                    c => c.AssignedToUserId,
                    (u, complaints) => new { User = u, Count = complaints.Count() })
                .OrderBy(x => x.Count)
                .Select(x => x.User)
                .FirstOrDefaultAsync();

            if (assignee != null)
            {
                complaint.AssignedToUserId = assignee.Id;
                complaint.AssignedAt = DateTime.Now;
                complaint.Status = ComplaintStatus.InProgress;

                await _context.SaveChangesAsync();

                _logger.LogInformation("Şikayet {TicketNumber} otomatik olarak {User} kullanıcısına atandı.",
                    complaint.TicketNumber, assignee.Email);
            }
        }
    }
}