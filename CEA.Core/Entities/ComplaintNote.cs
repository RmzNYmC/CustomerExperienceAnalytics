using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CEA.Core.Entities
{
    public class ComplaintNote : BaseEntity
    {
        public string Note { get; set; } = string.Empty;
        public bool IsInternal { get; set; } = true;  // Müşteriye görünür mü?
        public string? AttachmentUrl { get; set; }

        // Foreign Keys
        public int ComplaintId { get; set; }
        public string UserId { get; set; } = string.Empty;

        // Navigation
        public Complaint Complaint { get; set; } = null!;
        public ApplicationUser User { get; set; } = null!;
    }
}
