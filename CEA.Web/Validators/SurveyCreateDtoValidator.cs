using CEA.Web.Dtos.Surveys;
using FluentValidation;

namespace CEA.Web.Validators;

public class SurveyCreateDtoValidator : AbstractValidator<SurveyCreateDto>
{
    public SurveyCreateDtoValidator()
    {
        RuleFor(x => x.Title)
            .NotEmpty().WithMessage("Anket başlığı zorunludur.")
            .MaximumLength(250);

        RuleFor(x => x.EndDate)
            .GreaterThan(x => x.StartDate)
            .WithMessage("Bitiş tarihi başlangıç tarihinden büyük olmalıdır.");

        RuleFor(x => x.Questions)
            .NotEmpty().WithMessage("En az bir soru eklenmelidir.");

        RuleForEach(x => x.Questions).ChildRules(question =>
        {
            question.RuleFor(q => q.Text)
                .NotEmpty().WithMessage("Soru metni zorunludur.")
                .MaximumLength(500);

            question.RuleForEach(q => q.Options).ChildRules(option =>
            {
                option.RuleFor(o => o.Text)
                    .NotEmpty().WithMessage("Seçenek metni zorunludur.")
                    .MaximumLength(250);
            });
        });
    }
}