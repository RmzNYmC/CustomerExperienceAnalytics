using CEA.Business.Services;
using CEA.Core.Entities;
using CEA.Core.Enum;
using CEA.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace CEA.Web.Pages.Admin.Complaints
{
    [Authorize(Policy = "CanHandleComplaints")]
    public class DetailsModel : PageModel
    {
        private readonly ApplicationDbContext _context;
        private readonly ISlaCalculatorService _slaService;
        private readonly ILogger<DetailsModel> _logger;

        public DetailsModel(ApplicationDbContext context, ISlaCalculatorService slaService, ILogger<DetailsModel> logger)
        {
            _context = context;
            _slaService = slaService;
            _logger = logger;
        }

        public ComplaintDetailViewModel Complaint { get; set; } = new();
        public List<ComplaintNoteViewModel> Notes { get; set; } = new();
        public List<SelectListItem> AvailableUsers { get; set; } = new();
        public List<SelectListItem> StatusList { get; set; } = new();

        [BindProperty]
        public string NewNote { get; set; } = string.Empty;

        [BindProperty]
        public bool IsInternalNote { get; set; } = true;

        public async Task<IActionResult> OnGetAsync(int id)
        {
            await LoadComplaintAsync(id);
            await LoadUsersAsync();
            LoadStatusList();

            if (Complaint.Id == 0)
                return NotFound();

            return Page();
        }

        private async Task LoadComplaintAsync(int id)
        {
            var complaint = await _context.Complaints
                .Include(c => c.AssignedToUser)
                .Include(c => c.SurveyResponse)
                .ThenInclude(sr => sr.Answers)
                .ThenInclude(a => a.Question)
                .Include(c => c.TriggerQuestion)
                .FirstOrDefaultAsync(c => c.Id == id && !c.IsDeleted);

            if (complaint == null) return;

            Complaint = new ComplaintDetailViewModel
            {
                Id = complaint.Id,
                TicketNumber = complaint.TicketNumber,
                Title = complaint.Title,
                Description = complaint.Description,
                Category = complaint.Category,
                SubCategory = complaint.SubCategory,
                Priority = complaint.Priority,
                Status = complaint.Status,
                Source = complaint.Source,
                CustomerEmail = complaint.CustomerEmail,
                CustomerName = complaint.CustomerName,
                CustomerPhone = complaint.CustomerPhone,
                CustomerNotified = complaint.CustomerNotified,
                AssignedToUserId = complaint.AssignedToUserId,
                AssignedToName = complaint.AssignedToUser != null
                    ? $"{complaint.AssignedToUser.FirstName} {complaint.AssignedToUser.LastName}"
                    : "Atanmamış",
                AssignedAt = complaint.AssignedAt,
                DueDate = complaint.DueDate,
                ResolvedAt = complaint.ResolvedAt,
                ClosedAt = complaint.ClosedAt,
                CreatedAt = complaint.CreatedAt,
                IsSlaBreached = complaint.IsSlaBreached,
                SlaStatus = complaint.SlaStatusDisplay,
                ResponseTime = complaint.ResponseTimeMinutes,
                ResolutionTime = complaint.ResolutionTimeMinutes,
                BreachReason = complaint.BreachReason,
                ResolutionNotes = complaint.ResolutionNotes,
                ResolutionType = complaint.ResolutionType,

                // Anket yanıt detayları
                SurveyTitle = complaint.SurveyResponse?.Survey?.Title ?? "Bilinmiyor",
                TriggerQuestion = complaint.TriggerQuestion?.Text ?? "Bilinmiyor",
                Answers = complaint.SurveyResponse?.Answers.Select(a => new AnswerDetail
                {
                    QuestionText = a.Question?.Text ?? "Soru",
                    AnswerText = a.TextAnswer ?? a.NumericAnswer?.ToString() ?? "-",
                    IsTrigger = a.QuestionId == complaint.TriggerQuestionId
                }).ToList() ?? new List<AnswerDetail>()
            };

            // Notları yükle
            Notes = await _context.ComplaintNotes
                .Where(n => n.ComplaintId == id)
                .Include(n => n.User)
                .OrderByDescending(n => n.CreatedAt)
                .Select(n => new ComplaintNoteViewModel
                {
                    Id = n.Id,
                    Note = n.Note,
                    CreatedAt = n.CreatedAt,
                    CreatedBy = n.User != null ? $"{n.User.FirstName} {n.User.LastName}" : "Sistem",
                    IsInternal = n.IsInternal
                })
                .ToListAsync();
        }

        private async Task LoadUsersAsync()
        {
            AvailableUsers = await _context.Users
                .Where(u => u.IsActive)
                .Select(u => new SelectListItem
                {
                    Value = u.Id,
                    Text = $"{u.FirstName} {u.LastName} ({u.Department ?? "Belirtilmemiş"})"
                })
                .ToListAsync();
        }

        private void LoadStatusList()
        {
            StatusList = Enum.GetValues<ComplaintStatus>()
                .Select(s => new SelectListItem
                {
                    Value = ((int)s).ToString(),
                    Text = GetStatusText(s)
                })
                .ToList();
        }

        // POST: Not Ekle
        public async Task<IActionResult> OnPostAddNoteAsync(int id)
        {
            if (string.IsNullOrWhiteSpace(NewNote))
            {
                TempData["ErrorMessage"] = "Not boş olamaz.";
                return RedirectToPage(new { id });
            }

            var complaint = await _context.Complaints.FindAsync(id);
            if (complaint == null) return NotFound();

            var note = new ComplaintNote
            {
                ComplaintId = id,
                Note = NewNote,
                UserId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value ?? "System",
                CreatedAt = DateTime.Now,
                IsInternal = IsInternalNote
            };

            _context.ComplaintNotes.Add(note);
            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = "Not eklendi.";
            return RedirectToPage(new { id });
        }

        // POST: Durum Değiştir
        public async Task<IActionResult> OnPostChangeStatusAsync(int id, ComplaintStatus newStatus)
        {
            var complaint = await _context.Complaints.FindAsync(id);
            if (complaint == null) return NotFound();

            var oldStatus = complaint.Status;
            if ((newStatus == ComplaintStatus.Resolved || newStatus == ComplaintStatus.Closed)
        && !complaint.ResolvedAt.HasValue)
            {
                complaint.ResolvedAt = DateTime.Now;
            }

            // Eğer Closed yapılıyorsa ve ClosedAt boşsa set et
            if (newStatus == ComplaintStatus.Closed && !complaint.ClosedAt.HasValue)
            {
                complaint.ClosedAt = DateTime.Now;
            }

            complaint.Status = newStatus;

            // SLA metriklerini güncelle
            await _slaService.UpdateSlaMetricsAsync(id);

            // Durum değişikliği notu ekle
            var note = new ComplaintNote
            {
                ComplaintId = id,
                Note = $"Durum değiştirildi: {GetStatusText(oldStatus)} → {GetStatusText(newStatus)}",
                UserId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value ?? "System",
                CreatedAt = DateTime.Now,
                IsInternal = true
            };
            _context.ComplaintNotes.Add(note);

            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = $"Durum '{GetStatusText(newStatus)}' olarak güncellendi.";
            return RedirectToPage(new { id });
        }

        // POST: Manuel Atama (Detay sayfasından)
        public async Task<IActionResult> OnPostAssignAsync(int id, string userId)
        {
            var complaint = await _context.Complaints.FindAsync(id);
            if (complaint == null) return NotFound();

            var user = await _context.Users.FindAsync(userId);
            if (user == null) return NotFound();

            var oldAssignee = complaint.AssignedToUserId;

            complaint.AssignedToUserId = userId;
            complaint.AssignedAt = DateTime.Now;

            if (complaint.Status == ComplaintStatus.New)
                complaint.Status = ComplaintStatus.InProgress;

            if (!complaint.DueDate.HasValue)
                complaint.DueDate = _slaService.CalculateDueDate(complaint.Priority, complaint.Category);

            var note = new ComplaintNote
            {
                ComplaintId = id,
                Note = $"Atama yapıldı. {(oldAssignee == null ? "Atanmamış" : "Önceki görevli")} → {user.FirstName} {user.LastName}",
                UserId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value ?? "System",
                CreatedAt = DateTime.Now,
                IsInternal = true
            };
            _context.ComplaintNotes.Add(note);

            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = $"Şikayet {user.FirstName} {user.LastName}'e atandı.";
            return RedirectToPage(new { id });
        }

        // POST: Şikayet Kapat
        public async Task<IActionResult> OnPostCloseAsync(int id, string resolutionNotes, string resolutionType)
        {
            var complaint = await _context.Complaints.FindAsync(id);
            if (complaint == null) return NotFound();

            complaint.Status = ComplaintStatus.Closed;
            complaint.ClosedAt = DateTime.Now;
            complaint.ResolutionNotes = resolutionNotes;
            complaint.ResolutionType = resolutionType;

            await _slaService.UpdateSlaMetricsAsync(id);

            var note = new ComplaintNote
            {
                ComplaintId = id,
                Note = $"Şikayet kapatıldı. Çözüm Tipi: {resolutionType}. Not: {resolutionNotes}",
                UserId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value ?? "System",
                CreatedAt = DateTime.Now,
                IsInternal = false // Müşteri de görebilir
            };
            _context.ComplaintNotes.Add(note);

            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = "Şikayet kapatıldı.";
            return RedirectToPage(new { id });
        }

        private static string GetStatusText(ComplaintStatus status) => status switch
        {
            ComplaintStatus.New => "Yeni",
            ComplaintStatus.InProgress => "İşlemde",
            ComplaintStatus.WaitingForInfo => "Bilgi Bekleniyor",
            ComplaintStatus.Resolved => "Çözüldü",
            ComplaintStatus.Closed => "Kapatıldı",
            ComplaintStatus.Escalated => "Üst Yönetime Aktarıldı",
            _ => status.ToString()
        };
    }

    public class ComplaintDetailViewModel
    {
        public int Id { get; set; }
        public string TicketNumber { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string Category { get; set; } = string.Empty;
        public string? SubCategory { get; set; }
        public string Priority { get; set; } = string.Empty;
        public ComplaintStatus Status { get; set; }
        public string? Source { get; set; }
        public string CustomerEmail { get; set; } = string.Empty;
        public string? CustomerName { get; set; }
        public string? CustomerPhone { get; set; }
        public bool CustomerNotified { get; set; }
        public string? AssignedToUserId { get; set; }
        public string AssignedToName { get; set; } = "Atanmamış";
        public DateTime? AssignedAt { get; set; }
        public DateTime? DueDate { get; set; }
        public DateTime? ResolvedAt { get; set; }
        public DateTime? ClosedAt { get; set; }
        public DateTime CreatedAt { get; set; }
        public bool IsSlaBreached { get; set; }
        public string SlaStatus { get; set; } = string.Empty;
        public int? ResponseTime { get; set; }
        public int? ResolutionTime { get; set; }
        public string? BreachReason { get; set; }
        public string? ResolutionNotes { get; set; }
        public string? ResolutionType { get; set; }
        public string SurveyTitle { get; set; } = string.Empty;
        public string TriggerQuestion { get; set; } = string.Empty;
        public List<AnswerDetail> Answers { get; set; } = new();
    }

    public class AnswerDetail
    {
        public string QuestionText { get; set; } = string.Empty;
        public string AnswerText { get; set; } = string.Empty;
        public bool IsTrigger { get; set; }
    }

    public class ComplaintNoteViewModel
    {
        public int Id { get; set; }
        public string Note { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
        public string CreatedBy { get; set; } = string.Empty;
        public bool IsInternal { get; set; }
    }
}