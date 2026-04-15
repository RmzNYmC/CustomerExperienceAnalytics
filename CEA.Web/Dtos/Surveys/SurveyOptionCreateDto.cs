using System.ComponentModel.DataAnnotations;

namespace CEA.Web.Dtos.Surveys;

public class SurveyOptionCreateDto
{
    [Required]
    [StringLength(250)]
    public string Text { get; set; } = string.Empty;

    public int ScoreValue { get; set; }
}