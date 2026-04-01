using CEA.Business.Services;
using CEA.Core.Enum;
using CEA.Core.Enums;
using CEA.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace CEA.Web.Pages.Admin.Complaints
{
    [Authorize(Policy = "CanHandleComplaints")]
    public class IndexModel : PageModel
    {
        private readonly ApplicationDbContext _context;
        private readonly ISmartAssignmentService _assignmentService;
        private readonly ISlaCalculatorService _slaService;
        private readonly ILogger<IndexModel> _logger;

        public IndexModel(
            ApplicationDbContext context,
            ISmartAssignmentService assignmentService,
            ISlaCalculatorService slaService,
            ILogger<IndexModel> logger)
        {
            _context = context;
            _assignmentService = assignmentService;
            _slaService = slaService;
            _logger = logger;
        }

        public List<ComplaintViewModel> Complaints { get; set; } = new();
        public List<UserWorkload> UserWorkloads { get; set; } = new();
        public SlaDashboardMetrics SlaMetrics { get; set; } = new();

        // Filtreler
        [BindProperty(SupportsGet = true)]
        public string? StatusFilter { get; set; }

        [BindProperty(SupportsGet = true)]
        public string? PriorityFilter { get; set; }

        [BindProperty(SupportsGet = true)]
        public string? CategoryFilter { get; set; }

        [BindProperty(SupportsGet = true)]
        public string? AssigneeFilter { get; set; }

        [BindProperty(SupportsGet = true)]
        public string? SlaFilter { get; set; }

        [BindProperty(SupportsGet = true)]
        public string? SearchTerm { get; set; }

        public List<SelectListItem> StatusList { get; set; } = new();
        public List<SelectListItem> PriorityList { get; set; } = new();
        public List<SelectListItem> CategoryList { get; set; } = new();
        public List<SelectListItem> AssigneeList { get; set; } = new();

        public async Task OnGetAsync()
        {
            await LoadFilterOptionsAsync();
            await LoadComplaintsAsync();
            UserWorkloads = await _assignmentService.GetUserWorkloadsAsync();
            SlaMetrics = await _slaService.GetSlaMetricsAsync();
        }

        private async Task LoadComplaintsAsync()
        {
            var query = _context.Complaints
                .Include(c => c.AssignedToUser)
                .Include(c => c.SurveyResponse)
                .Where(c => !c.IsDeleted)
                .AsQueryable();

            // Filtreler
            if (!string.IsNullOrEmpty(StatusFilter) && Enum.TryParse<ComplaintStatus>(StatusFilter, out var status))
                query = query.Where(c => c.Status == status);

            if (!string.IsNullOrEmpty(PriorityFilter))
                query = query.Where(c => c.Priority == PriorityFilter);

            if (!string.IsNullOrEmpty(CategoryFilter))
                query = query.Where(c => c.Category == CategoryFilter);

            if (!string.IsNullOrEmpty(AssigneeFilter))
            {
                if (AssigneeFilter == "unassigned")
                    query = query.Where(c => string.IsNullOrEmpty(c.AssignedToUserId));
                else
                    query = query.Where(c => c.AssignedToUserId == AssigneeFilter);
            }

            if (!string.IsNullOrEmpty(SlaFilter))
            {
                query = SlaFilter switch
                {
                    "Breached" => query.Where(c => c.IsSlaBreached),
                    "AtRisk" => query.Where(c => !c.IsSlaBreached && c.DueDate.HasValue && c.DueDate.Value.AddHours(-2) <= DateTime.Now),
                    "OnTrack" => query.Where(c => !c.IsSlaBreached && c.DueDate.HasValue && c.DueDate.Value.AddHours(-2) > DateTime.Now),
                    _ => query
                };
            }

            if (!string.IsNullOrEmpty(SearchTerm))
            {
                var term = SearchTerm.ToLower();
                query = query.Where(c =>
                    c.TicketNumber.ToLower().Contains(term) ||
                    (c.CustomerName != null && c.CustomerName.ToLower().Contains(term)) ||
                    c.CustomerEmail.ToLower().Contains(term) ||
                    c.Title.ToLower().Contains(term));
            }

            Complaints = await query
                .OrderByDescending(c => c.IsSlaBreached)
                .ThenBy(c => c.Priority == "Critical" ? 0 : c.Priority == "High" ? 1 : c.Priority == "Medium" ? 2 : 3)
                .ThenBy(c => c.DueDate)
                .Select(c => new ComplaintViewModel
                {
                    Id = c.Id,
                    TicketNumber = c.TicketNumber,
                    Title = c.Title,
                    CustomerName = c.CustomerName ?? "İsimsiz",
                    CustomerEmail = c.CustomerEmail,
                    Category = c.Category,
                    Priority = c.Priority,
                    PriorityBadgeClass = GetPriorityBadgeClass(c.Priority),
                    Status = c.Status.ToString(),
                    StatusBadgeClass = GetStatusBadgeClass(c.Status),
                    AssignedToName = c.AssignedToUser != null
                        ? $"{c.AssignedToUser.FirstName} {c.AssignedToUser.LastName}"
                        : "Atanmamış",
                    CreatedAt = c.CreatedAt,
                    DueDate = c.DueDate,
                    ResolvedAt = c.ResolvedAt,
                    IsSlaBreached = c.IsSlaBreached,
                    SlaStatus = c.SlaStatusDisplay,
                    TimeElapsed = c.TimeElapsedDisplay,
                    ResponseTimeMinutes = c.ResponseTimeMinutes,
                    CanAutoAssign = string.IsNullOrEmpty(c.AssignedToUserId) && c.Status == ComplaintStatus.New
                })
                .ToListAsync();
        }

        private async Task LoadFilterOptionsAsync()
        {
            StatusList = Enum.GetValues<ComplaintStatus>()
                .Select(s => new SelectListItem { Value = s.ToString(), Text = GetStatusText(s) })
                .ToList();
            StatusList.Insert(0, new SelectListItem { Value = "", Text = "Tüm Durumlar" });

            PriorityList = new List<SelectListItem>
            {
                new() { Value = "", Text = "Tüm Öncelikler" },
                new() { Value = "Critical", Text = "🔴 Kritik" },
                new() { Value = "High", Text = "🟠 Yüksek" },
                new() { Value = "Medium", Text = "🟡 Orta" },
                new() { Value = "Low", Text = "🟢 Düşük" }
            };

            var categories = await _context.Complaints
                .Where(c => !c.IsDeleted)
                .Select(c => c.Category)
                .Distinct()
                .ToListAsync();

            CategoryList = categories
                .Select(c => new SelectListItem { Value = c, Text = c })
                .ToList();
            CategoryList.Insert(0, new SelectListItem { Value = "", Text = "Tüm Kategoriler" });

            var users = await _context.Users
                .Where(u => u.IsActive)
                .ToListAsync();

            AssigneeList = users
                .Select(u => new SelectListItem
                {
                    Value = u.Id,
                    Text = $"{u.FirstName} {u.LastName}"
                })
                .ToList();
            AssigneeList.Insert(0, new SelectListItem { Value = "", Text = "Tüm Atananlar" });
            AssigneeList.Insert(1, new SelectListItem { Value = "unassigned", Text = "❗ Atanmamış" });
        }

        public async Task<IActionResult> OnPostAutoAssignAsync(int id)
        {
            try
            {
                var success = await _assignmentService.AutoAssignComplaintAsync(id);
                if (success)
                {
                    TempData["SuccessMessage"] = "Şikayet başarıyla otomatik atandı.";
                }
                else
                {
                    TempData["ErrorMessage"] = "Otomatik atama başarısız. Uygun kullanıcı bulunamadı.";
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Otomatik atama hatası");
                TempData["ErrorMessage"] = "Atama sırasında bir hata oluştu.";
            }

            return RedirectToPage();
        }

        public async Task<IActionResult> OnPostCheckSlaAsync()
        {
            await _slaService.CheckAndMarkBreachedSlasAsync();
            TempData["SuccessMessage"] = "SLA kontrolü tamamlandı.";
            return RedirectToPage();
        }

        public async Task<IActionResult> OnPostDeleteAsync(int id)
        {
            var complaint = await _context.Complaints.FindAsync(id);
            if (complaint != null)
            {
                complaint.IsDeleted = true;
                complaint.DeletedAt = DateTime.Now;
                await _context.SaveChangesAsync();
                TempData["SuccessMessage"] = $"Şikayet {complaint.TicketNumber} silindi.";
            }
            return RedirectToPage();
        }

        private static string GetPriorityBadgeClass(string priority) => priority switch
        {
            "Critical" => "danger",
            "High" => "warning",
            "Medium" => "info",
            "Low" => "success",
            _ => "secondary"
        };

        // ⭐ GÜNCELLENDİ: Tüm enum değerleri eklendi
        private static string GetStatusBadgeClass(ComplaintStatus status) => status switch
        {
            ComplaintStatus.New => "secondary",
            ComplaintStatus.InProgress => "primary",
            ComplaintStatus.WaitingForInfo => "warning",      // 🟡 Sarı
            ComplaintStatus.Resolved => "success",             // 🟢 Yeşil
            ComplaintStatus.Closed => "dark",                 // ⚫ Siyah
            ComplaintStatus.Escalated => "danger",            // 🔴 Kırmızı
            _ => "secondary"
        };

        // ⭐ GÜNCELLENDİ: Türkçe karşılıklar
        private static string GetStatusText(ComplaintStatus status) => status switch
        {
            ComplaintStatus.New => "Yeni",
            ComplaintStatus.InProgress => "İşlemde",
            ComplaintStatus.WaitingForInfo => "Bilgi Bekleniyor",    // ⭐
            ComplaintStatus.Resolved => "Çözüldü",
            ComplaintStatus.Closed => "Kapatıldı",
            ComplaintStatus.Escalated => "Üst Yönetime Aktarıldı",    // ⭐
            _ => status.ToString()
        };
    }

    public class ComplaintViewModel
    {
        public int Id { get; set; }
        public string TicketNumber { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public string CustomerName { get; set; } = string.Empty;
        public string CustomerEmail { get; set; } = string.Empty;
        public string Category { get; set; } = string.Empty;
        public string Priority { get; set; } = string.Empty;
        public string PriorityBadgeClass { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public string StatusBadgeClass { get; set; } = string.Empty;
        public string AssignedToName { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
        public DateTime? DueDate { get; set; }
        public DateTime? ResolvedAt { get; set; }
        public bool IsSlaBreached { get; set; }
        public string SlaStatus { get; set; } = string.Empty;
        public string TimeElapsed { get; set; } = string.Empty;
        public int? ResponseTimeMinutes { get; set; }
        public bool CanAutoAssign { get; set; }
    }
}