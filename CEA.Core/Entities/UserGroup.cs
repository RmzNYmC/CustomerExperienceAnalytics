using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CEA.Core.Entities
{
    public class UserGroup : BaseEntity
    {
        public string Name { get; set; } = string.Empty;
        public string? Description { get; set; }
        public bool CanCreateSurvey { get; set; } = false;
        public bool CanViewReports { get; set; } = false;
        public bool CanManageUsers { get; set; } = false;
        public bool CanHandleComplaints { get; set; } = false;

        // Navigation
        public ICollection<ApplicationUser> Users { get; set; } = new List<ApplicationUser>();
    }
}
