namespace CEA.Web.Dtos.Analytics;

public class NpsDistributionDto
{
    public int Promoters { get; set; }
    public int Passives { get; set; }
    public int Detractors { get; set; }
    public int Total { get; set; }
}