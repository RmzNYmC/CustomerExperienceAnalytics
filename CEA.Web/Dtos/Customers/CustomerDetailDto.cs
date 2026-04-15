namespace CEA.Web.Dtos.Customers;

public class CustomerDetailDto : CustomerListItemDto
{
    public string? Notes { get; set; }
    public int ResponseCount { get; set; }
}