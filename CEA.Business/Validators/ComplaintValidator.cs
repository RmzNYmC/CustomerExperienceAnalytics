using CEA.Core.Entities;
using FluentValidation;

namespace CEA.Business.Validators
{
    public class ComplaintValidator : AbstractValidator<Complaint>
    {
        public ComplaintValidator()
        {
            RuleFor(c => c.Title)
                .NotEmpty().WithMessage("Başlık zorunludur.")
                .MaximumLength(200).WithMessage("Başlık en fazla 200 karakter olabilir.");

            RuleFor(c => c.Description)
                .NotEmpty().WithMessage("Açıklama zorunludur.")
                .MaximumLength(2000).WithMessage("Açıklama en fazla 2000 karakter olabilir.");

            RuleFor(c => c.CustomerEmail)
                .NotEmpty().WithMessage("E-posta zorunludur.")
                .EmailAddress().WithMessage("Geçerli bir e-posta adresi giriniz.");

            RuleFor(c => c.Category)
                .NotEmpty().WithMessage("Kategori zorunludur.")
                .Must(BeValidCategory).WithMessage("Geçersiz kategori.");

            RuleFor(c => c.Priority)
                .NotEmpty().WithMessage("Öncelik zorunludur.")
                .Must(BeValidPriority).WithMessage("Geçersiz öncelik.");
        }

        private bool BeValidCategory(string category)
        {
            var validCategories = new[] { "Ürün", "Hizmet", "Personel", "Lojistik", "Genel" };
            return validCategories.Contains(category);
        }

        private bool BeValidPriority(string priority)
        {
            var validPriorities = new[] { "Critical", "High", "Medium", "Low" };
            return validPriorities.Contains(priority);
        }
    }
}