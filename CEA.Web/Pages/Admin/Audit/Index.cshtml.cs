using CEA.Business.Services;
using CEA.Core.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace CEA.Web.Pages.Admin.Audit
{
    [Authorize(Roles = "Admin")]
    public class IndexModel : PageModel
    {
        private readonly IAuditService _auditService;

        public List<AuditLog> RecentActivities { get; set; } = new();

        public IndexModel(IAuditService auditService)
        {
            _auditService = auditService;
        }

        public async Task OnGetAsync()
        {
            RecentActivities = await _auditService.GetRecentActivitiesAsync(50);
        }
    }
}