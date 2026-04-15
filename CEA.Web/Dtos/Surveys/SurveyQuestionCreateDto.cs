using System.ComponentModel.DataAnnotations;

namespace CEA.Web.Dtos.Surveys;

public class SurveyQuestionCreateDto
{
    [Required]
    [StringLength(500)]
    public string Text { get; set; } = string.Empty;

    [StringLength(1000)]
    public string? Description { get; set; }

    public int QuestionType { get; set; }
    public bool IsRequired { get; set; }
    public bool TriggerComplaintOnLowRating { get; set; }
    public int ComplaintThreshold { get; set; }
    public int MinRating { get; set; }
    public int MaxRating { get; set; }

    public List<SurveyOptionCreateDto> Options { get; set; } = new();
}