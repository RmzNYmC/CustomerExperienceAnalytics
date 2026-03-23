using CEA.Core.Entities;
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
        private const int PageSize = 20;

        [BindProperty(SupportsGet = true)]
        public string? FilterStatus { get; set; }

        [BindProperty(SupportsGet = true)]
        public string? FilterPriority { get; set; }

        [BindProperty(SupportsGet = true)]
        public string? FilterAssignedTo { get; set; }

        [BindProperty(SupportsGet = true)]
        public int CurrentPage { get; set; } = 1;

        public List<ComplaintViewModel> Complaints { get; set; } = new();
        public List<SelectListItem> UserList { get; set; } = new();
        public List<UserSelectItem> AvailableUsers { get; set; } = new();
        public int TotalPages { get; set; }

        public IndexModel(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task OnGetAsync()
        {
            // Kullanıcı listesi
            var users = await _context.Users
    .Cast<ApplicationUser>() // Cast ekle
    .Where(u => u.IsActive)
    .ToListAsync();

            UserList = users.Select(u => new SelectListItem
            {
                Value = u.Id,
                Text = $"{u.FirstName} {u.LastName}"
            }).ToList();

            AvailableUsers = users.Select(u => new UserSelectItem
            {
                Id = u.Id,
                Name = $"{u.FirstName} {u.LastName}",
                Department = u.Department ?? "Genel"
            }).ToList();

            // Şikayet sorgusu
            var query = _context.Complaints
                .Include(c => c.AssignedToUser)
                .Include(c => c.SurveyResponse)
                .Where(c => !c.IsDeleted)
                .AsQueryable();

            if (!string.IsNullOrEmpty(FilterStatus))
                query = query.Where(c => c.Status.ToString() == FilterStatus);

            if (!string.IsNullOrEmpty(FilterPriority))
                query = query.Where(c => c.Priority == FilterPriority);

            if (!string.IsNullOrEmpty(FilterAssignedTo))
                query = query.Where(c => c.AssignedToUserId == FilterAssignedTo);

            var totalCount = await query.CountAsync();
            TotalPages = (int)Math.Ceiling(totalCount / (double)PageSize);

            Complaints = await query
                .OrderByDescending(c => c.Priority == "Critical")
                .ThenByDescending(c => c.Priority == "High")
                .ThenBy(c => c.CreatedAt)
                .Skip((CurrentPage - 1) * PageSize)
                .Take(PageSize)
                .Select(c => new ComplaintViewModel
                {
                    Id = c.Id,
                    TicketNumber = c.TicketNumber,
                    CustomerName = c.CustomerName ?? "İsimsiz",
                    CustomerEmail = c.CustomerEmail,
                    Title = c.Title,
                    Category = c.Category,
                    Priority = c.Priority,
                    Status = c.Status.ToString(),
                    AssignedToName = c.AssignedToUser != null
                        ? $"{c.AssignedToUser.FirstName} {c.AssignedToUser.LastName}"
                        : null,
                    CreatedAt = c.CreatedAt,
                    DueDate = c.DueDate,
                    IsUnread = c.Status == ComplaintStatus.New
                })
                .ToListAsync();
        }
    }

    public class ComplaintViewModel
    {
        public int Id { get; set; }
        public string TicketNumber { get; set; } = string.Empty;
        public string CustomerName { get; set; } = string.Empty;
        public string CustomerEmail { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public string Category { get; set; } = string.Empty;
        public string Priority { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public string? AssignedToName { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? DueDate { get; set; }
        public bool IsUnread { get; set; }
    }

    public class UserSelectItem
    {
        public string Id { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Department { get; set; } = string.Empty;
    }
}