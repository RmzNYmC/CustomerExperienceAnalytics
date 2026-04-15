using System.ComponentModel.DataAnnotations;

namespace CEA.Web.Dtos.Surveys;

public class SurveyCreateDto
{
    [Required]
    [StringLength(250)]
    public string Title { get; set; } = string.Empty;

    [StringLength(2000)]
    public string? Description { get; set; }

    [StringLength(2000)]
    public string? WelcomeMessage { get; set; }

    [StringLength(2000)]
    public string? ThankYouMessage { get; set; }

    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }

    [StringLength(100)]
    public string? AnalysisType { get; set; }

    public int AnalysisYear { get; set; }
    public int? AnalysisMonth { get; set; }

    public bool RequiresAuthentication { get; set; }
    public bool AllowMultipleResponses { get; set; }
    public int Status { get; set; }

    public List<SurveyQuestionCreateDto> Questions { get; set; } = new();
}