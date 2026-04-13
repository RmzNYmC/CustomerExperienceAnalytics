using CEA.Core.Entities;
using CEA.Core.Enum;
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
        private readonly ISmartAssignmentService _smartAssignmentService;
        private readonly IRealTimeNotificationService _notificationService; // ✅ Zaten var
        private readonly IAuditService _auditService; // ✅ YENİ EKLENDİ
        private readonly ILogger<ComplaintAutomationService> _logger;

        public ComplaintAutomationService(
            ApplicationDbContext context,
            IEmailService emailService,
            ISmartAssignmentService smartAssignmentService,
            IRealTimeNotificationService notificationService,
            IAuditService auditService, // ✅ EKLENDİ
            ILogger<ComplaintAutomationService> logger)
        {
            _context = context;
            _emailService = emailService;
            _smartAssignmentService = smartAssignmentService;
            _notificationService = notificationService;
            _auditService = auditService;
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

            // ✅ 1. REAL-TIME BİLDİRİM (SignalR) - YENİ EKLENDİ
            await _notificationService.NotifyNewComplaint(
                complaint.Id,
                complaint.TicketNumber,
                complaint.Priority);

            // ✅ 2. AUDIT LOG - YENİ EKLENDİ
            await _auditService.LogComplaintCreatedAsync(
                complaint.Id,
                complaint.CustomerEmail);

            // 3. Email bildirimi (mevcut kod)
            await NotifyManagers(complaint);

            // 4. Otomatik atama (mevcut kod)
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
            var managers = await _context.Users
                .Where(u => u.IsActive)
                .ToListAsync();

            foreach (var user in managers)
            {
                if (string.IsNullOrEmpty(user.Email))
                    continue;

                try
                {
                    await _emailService.SendComplaintNotificationAsync(
                        user.Email,
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

        // ✅ GÜNCELLENDİ: SmartAssignmentService kullanıyor + SignalR + Audit
        public async Task AutoAssignComplaintAsync(int complaintId)
        {
            var complaint = await _context.Complaints.FindAsync(complaintId);
            if (complaint == null || !string.IsNullOrEmpty(complaint.AssignedToUserId))
                return;

            // SmartAssignmentService kullan (workload balancing + expertise matching)
            var assigneeId = await _smartAssignmentService.FindBestAssigneeAsync(
                complaint.Category,
                complaint.Priority);

            if (assigneeId == null)
            {
                _logger.LogWarning("Otomatik atama başarısız: Uygun kullanıcı bulunamadı. Ticket: {Ticket}",
                    complaint.TicketNumber);
                return;
            }

            // Atama işlemi
            complaint.AssignedToUserId = assigneeId;
            complaint.AssignedAt = DateTime.Now;
            complaint.Status = ComplaintStatus.InProgress;

            await _context.SaveChangesAsync();

            _logger.LogInformation("Şikayet {Ticket} otomatik atandı: {User}",
                complaint.TicketNumber, assigneeId);

            // ✅ REAL-TIME BİLDİRİM (SignalR) - YENİ EKLENDİ
            await _notificationService.NotifyComplaintAssigned(
                complaint.Id,
                assigneeId,
                "Sistem (Otomatik)");

            // ✅ AUDIT LOG - YENİ EKLENDİ
            await _auditService.LogComplaintAssignedAsync(
                complaint.Id,
                null,
                assigneeId);
        }
    }
}