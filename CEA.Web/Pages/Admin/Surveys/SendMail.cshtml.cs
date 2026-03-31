using CEA.Business.Services;
using CEA.Core.Entities;
using CEA.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace CEA.Web.Pages.Admin.Surveys
{
    [Authorize(Policy = "CanCreateSurvey")]
    public class SendMailModel : PageModel
    {
        private readonly ApplicationDbContext _context;
        private readonly IEmailService _emailService;

        public SendMailModel(ApplicationDbContext context, IEmailService emailService)
        {
            _context = context;
            _emailService = emailService;
        }

        [BindProperty(SupportsGet = true)]
        public int SurveyId { get; set; }

        [BindProperty]
        public string MailSubject { get; set; } = string.Empty;

        [BindProperty]
        public string? PersonalMessage { get; set; }

        [BindProperty]
        public List<int> SelectedCustomerIds { get; set; } = new();

        public string SurveyTitle { get; set; } = string.Empty;
        public string? SurveyPublicToken { get; set; }

        [BindProperty(SupportsGet = true)]
        public string? SegmentFilter { get; set; }

        [BindProperty(SupportsGet = true)]
        public string? SearchTerm { get; set; }

        public List<CustomerViewModel> Customers { get; set; } = new();
        public string? SuccessMessage { get; set; }
        public string? ErrorMessage { get; set; }

        public async Task<IActionResult> OnGetAsync()
        {
            var survey = await _context.Surveys
                .FirstOrDefaultAsync(s => s.Id == SurveyId && !s.IsDeleted);

            if (survey == null)
                return NotFound();

            SurveyTitle = survey.Title;
            SurveyPublicToken = survey.PublicToken;
            MailSubject = $"Turkon Lojistik - {survey.Title} Anketi";

            await LoadCustomersAsync();
            return Page();
        }

        public async Task<IActionResult> OnPostAsync()
        {
            var survey = await _context.Surveys
                .FirstOrDefaultAsync(s => s.Id == SurveyId && !s.IsDeleted);

            if (survey == null)
                return NotFound();

            SurveyTitle = survey.Title;
            SurveyPublicToken = survey.PublicToken;

            if (string.IsNullOrWhiteSpace(MailSubject))
            {
                ErrorMessage = "Mail konusu zorunludur.";
                await LoadCustomersAsync();
                return Page();
            }

            if (SelectedCustomerIds == null || !SelectedCustomerIds.Any())
            {
                ErrorMessage = "Lütfen en az bir müşteri seçin.";
                await LoadCustomersAsync();
                return Page();
            }

            try
            {
                var customers = await _context.Customers
                    .Where(c => SelectedCustomerIds.Contains(c.Id) && !c.IsDeleted && !c.BounceEmail)
                    .ToListAsync();

                var surveyUrl = $"{Request.Scheme}://{Request.Host}/Survey/Index?token={survey.PublicToken}";
                int sentCount = 0;

                foreach (var customer in customers)
                {
                    if (!string.IsNullOrEmpty(customer.Email))
                    {
                        var body = BuildEmailBody(customer.Name, surveyUrl);
                        await _emailService.SendEmailAsync(customer.Email, MailSubject, body);
                        sentCount++;
                    }
                }

                SuccessMessage = $"{sentCount} müşteriye başarıyla mail gönderildi.";
                SelectedCustomerIds.Clear();
            }
            catch (Exception ex)
            {
                ErrorMessage = "Mail gönderilirken hata oluştu: " + ex.Message;
            }

            await LoadCustomersAsync();
            return Page();
        }

        private async Task LoadCustomersAsync()
        {
            var respondedCustomerIds = await _context.SurveyResponses
                .Where(r => r.SurveyId == SurveyId && !r.IsDeleted && r.CustomerId != null)
                .Select(r => r.CustomerId!.Value)
                .Distinct()
                .ToListAsync();

            var query = _context.Customers
                .Where(c => !c.IsDeleted && !c.BounceEmail)
                .AsQueryable();

            if (!string.IsNullOrWhiteSpace(SegmentFilter))
                query = query.Where(c => c.Segment == SegmentFilter);

            if (!string.IsNullOrWhiteSpace(SearchTerm))
            {
                var term = SearchTerm.ToLower();
                query = query.Where(c =>
                    c.Name.ToLower().Contains(term) ||
                    c.Email.ToLower().Contains(term) ||
                    (c.CompanyName != null && c.CompanyName.ToLower().Contains(term)));
            }

            Customers = await query
                .OrderBy(c => c.Name)
                .Select(c => new CustomerViewModel
                {
                    Id = c.Id,
                    Email = c.Email,
                    Name = c.Name,
                    CompanyName = c.CompanyName,
                    Segment = c.Segment,
                    Phone = c.Phone,
                    HasResponded = respondedCustomerIds.Contains(c.Id)
                })
                .ToListAsync();
        }

        private string BuildEmailBody(string? customerName, string surveyUrl)
        {
            var name = customerName ?? "Değerli Müşterimiz";
            var message = string.IsNullOrEmpty(PersonalMessage)
                ? "Turkon Lojistik olarak sizden aldığımız hizmetlerimiz hakkında geri bildirim rica ediyoruz."
                : PersonalMessage;

            return $@"<html><body style='font-family: Arial; line-height: 1.6;'>
                <div style='max-width: 600px; margin: 0 auto; padding: 20px;'>
                    <h2 style='color: #1e40af;'>Turkon Lojistik</h2>
                    <p>Sayın <strong>{name}</strong>,</p>
                    <p>{message}</p>
                    <div style='text-align: center; margin: 30px 0;'>
                        <a href='{surveyUrl}' style='background: #2563eb; color: white; padding: 12px 30px; text-decoration: none; border-radius: 5px; display: inline-block;'>Ankete Katıl</a>
                    </div>
                    <p style='font-size: 12px; color: #6b7280;'>Bu mail Turkon Lojistik CEA Sistemi tarafından otomatik olarak gönderilmiştir.</p>
                </div></body></html>";
        }
    }

    public class CustomerViewModel
    {
        public int Id { get; set; }
        public string Email { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string? CompanyName { get; set; }
        public string? Segment { get; set; }
        public string? Phone { get; set; }
        public bool HasResponded { get; set; }
    }
}