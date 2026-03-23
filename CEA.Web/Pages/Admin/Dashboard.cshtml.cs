using CEA.Business.Services;
using CEA.Core.ViewModels;
using CEA.Data;
using CEA.Core;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace CEA.Web.Pages.Admin
{
    // DÜZELTİLDİ: Authorize attribute eklendi
    [Authorize]
    public class DashboardModel : PageModel
    {
        private readonly IAnalyticsService _analyticsService;
        private readonly ApplicationDbContext _context;

        public DashboardModel(IAnalyticsService analyticsService, ApplicationDbContext context)
        {
            _analyticsService = analyticsService;
            _context = context;
        }

        public DashboardSummary Summary { get; set; } = new();
        public List<RecentSurveyView> RecentSurveys { get; set; } = new();
        public List<RecentComplaintView> RecentComplaints { get; set; } = new();
        public List<ActivityView> RecentActivities { get; set; } = new();

        public async Task OnGetAsync()
        {
            Summary = await _analyticsService.GetDashboardSummaryAsync();

            RecentSurveys = await _context.Surveys
                .Where(s => !s.IsDeleted)
                .OrderByDescending(s => s.CreatedAt)
                .Take(5)
                .Select(s => new RecentSurveyView
                {
                    Title = s.Title,
                    Status = s.Status,
                    CreatedAt = s.CreatedAt,
                    ResponseCount = s.Responses.Count(r => !r.IsDeleted)
                })
                .ToListAsync();

            RecentComplaints = await _context.Complaints
                .Where(c => !c.IsDeleted)
                .OrderByDescending(c => c.CreatedAt)
                .Take(5)
                .Select(c => new RecentComplaintView
                {
                    Id = c.Id,
                    TicketNumber = c.TicketNumber,
                    CustomerName = c.CustomerName ?? "İsimsiz",
                    Category = c.Category,
                    Priority = c.Priority,
                    Status = c.Status.ToString()
                })
                .ToListAsync();

            RecentActivities = new List<ActivityView>
            {
                new() { Time = DateTime.Now.AddMinutes(-30), Description = "Yeni anket yanıtı alındı: #1234" },
                new() { Time = DateTime.Now.AddHours(-2), Description = "Şikayet çözüldü: CMP-2024-0005" },
                new() { Time = DateTime.Now.AddHours(-5), Description = "Yeni anket oluşturuldu: Memnuniyet Anketi 2024" }
            };
        }
    }

    public class RecentSurveyView
    {
        public string Title { get; set; } = string.Empty;
        public Core.Enum.SurveyStatus Status { get; set; }
        public DateTime CreatedAt { get; set; }
        public int ResponseCount { get; set; }
    }

    public class RecentComplaintView
    {
        public int Id { get; set; }
        public string TicketNumber { get; set; } = string.Empty;
        public string CustomerName { get; set; } = string.Empty;
        public string Category { get; set; } = string.Empty;
        public string Priority { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
    }

    public class ActivityView
    {
        public DateTime Time { get; set; }
        public string Description { get; set; } = string.Empty;
    }
}