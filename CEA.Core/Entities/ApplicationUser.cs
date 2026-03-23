using Microsoft.AspNetCore.Identity;

namespace CEA.Core.Entities
{
    public class ApplicationUser : IdentityUser
    {
        public string FirstName { get; set; } = string.Empty;
        public string LastName { get; set; } = string.Empty;
        public string? Department { get; set; }
        public bool IsActive { get; set; } = true;
        public DateTime CreatedDate { get; set; } = DateTime.Now;

        // Navigation Properties
        public virtual ICollection<Survey> CreatedSurveys { get; set; } = new List<Survey>();
        public virtual ICollection<Complaint> AssignedComplaints { get; set; } = new List<Complaint>();
        public virtual ICollection<UserGroup> UserGroups { get; set; } = new List<UserGroup>();
    }
}