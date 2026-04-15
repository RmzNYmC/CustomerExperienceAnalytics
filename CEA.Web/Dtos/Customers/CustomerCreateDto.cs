using System.ComponentModel.DataAnnotations;

namespace CEA.Web.Dtos.Customers;

public class CustomerCreateDto
{
    [Required]
    [StringLength(200)]
    public string Name { get; set; } = string.Empty;

    [EmailAddress]
    [StringLength(200)]
    public string? Email { get; set; }

    [StringLength(200)]
    public string? CompanyName { get; set; }

    [Phone]
    [StringLength(50)]
    public string? Phone { get; set; }

    [StringLength(100)]
    public string? Segment { get; set; }

    [StringLength(1000)]
    public string? Notes { get; set; }

    public bool EmailVerified { get; set; }
    public bool BounceEmail { get; set; }
}