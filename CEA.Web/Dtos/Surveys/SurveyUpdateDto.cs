using CEA.Core.Enum;

public class SurveyUpdateDto
{
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? WelcomeMessage { get; set; }
    public string? ThankYouMessage { get; set; }
    public DateTime? StartDate { get; set; }
    public DateTime? EndDate { get; set; }
    public string AnalysisType { get; set; } = "Yearly";
    public int AnalysisYear { get; set; } = DateTime.Now.Year;
    public int? AnalysisMonth { get; set; }
    public bool RequiresAuthentication { get; set; }
    public bool AllowMultipleResponses { get; set; }
    public SurveyStatus Status { get; set; }
}