using System.ComponentModel.DataAnnotations;

namespace CEA.Web.Dtos.Surveys;

public class SurveyStatusUpdateDto
{
    [Required]
    public int Status { get; set; }
}