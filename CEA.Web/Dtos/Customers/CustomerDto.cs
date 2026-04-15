namespace CEA.Web.Dtos.Customers;

public class CustomerDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Email { get; set; }
    public string? CompanyName { get; set; }
    public string? Phone { get; set; }
    public string? Segment { get; set; }
    public string? Notes { get; set; }
    public bool EmailVerified { get; set; }
    public bool BounceEmail { get; set; }
    public int ResponseCount { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}