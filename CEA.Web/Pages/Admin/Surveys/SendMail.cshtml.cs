using CEA.Business.Services;
using CEA.Core.Entities;
using CEA.Core.Enums;
using CEA.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;

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
        [Required(ErrorMessage = "Mail konusu zorunludur.")]
        public string MailSubject { get; set; } = string.Empty;

        [BindProperty]
        public string? PersonalMessage { get; set; }

        [BindProperty]
        public List<int> SelectedCustomerIds { get; set; } = new();

        [BindProperty]
        public bool SendReminder { get; set; }

        public string SurveyTitle { get; set; } = string.Empty;
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
            await LoadCustomersAsync(survey.Id);

            // Varsayılan konu
            MailSubject = $"Turkon Lojistik - {survey.Title} Anketi";

            return Page();
        }

        public async Task<IActionResult> OnPostAsync()
        {
            var survey = await _context.Surveys
                .FirstOrDefaultAsync(s => s.Id == SurveyId && !s.IsDeleted);

            if (survey == null)
                return NotFound();

            SurveyTitle = survey.Title;

            if (!ModelState.IsValid)
            {
                await LoadCustomersAsync(survey.Id);
                return Page();
            }

            if (SelectedCustomerIds == null || !SelectedCustomerIds.Any())
            {
                ErrorMessage = "Lütfen en az bir müşteri seçin.";
                await LoadCustomersAsync(survey.Id);
                return Page();
            }

            try
            {
                var customers = await _context.SurveyResponses
                    .Where(r => SelectedCustomerIds.Contains(r.Id) && !r.IsDeleted)
                    .Select(r => new { r.CustomerEmail, r.CustomerName })
                    .ToListAsync();

                var surveyUrl = $"{Request.Scheme}://{Request.Host}/s/{survey.PublicToken}";

                int sentCount = 0;
                foreach (var customer in customers)
                {
                    if (!string.IsNullOrEmpty(customer.CustomerEmail))
                    {
                        var body = BuildEmailBody(customer.CustomerName, surveyUrl);
                        await _emailService.SendEmailAsync(
                            customer.CustomerEmail,
                            MailSubject,
                            body);
                        sentCount++;
                    }
                }

                SuccessMessage = $"{sentCount} müşteriye başarıyla mail gönderildi.";
                await LoadCustomersAsync(survey.Id); // Listeyi yenile
                SelectedCustomerIds.Clear(); // Seçimleri temizle
            }
            catch (Exception ex)
            {
                ErrorMessage = "Mail gönderilirken bir hata oluştu: " + ex.Message;
                await LoadCustomersAsync(survey.Id);
            }

            return Page();
        }

        private async Task LoadCustomersAsync(int surveyId)
        {
            // Bu ankete daha önce yanıt verenleri bul
            var respondedIds = await _context.SurveyResponses
                .Where(r => r.SurveyId == surveyId && !r.IsDeleted)
                .Select(r => r.Id)
                .ToListAsync();

            // Tüm müşterileri getir (gerçek senaryoda müşteri tablon olmalı)
            // Şimdilik SurveyResponses tablosundan unique mailleri çekiyoruz
            Customers = await _context.SurveyResponses
                .Where(r => !string.IsNullOrEmpty(r.CustomerEmail) && !r.IsDeleted)
                .GroupBy(r => r.CustomerEmail)
                .Select(g => new CustomerViewModel
                {
                    Id = g.First().Id,
                    Email = g.Key,
                    Name = g.First().CustomerName ?? "İsimsiz",
                    HasResponded = respondedIds.Contains(g.First().Id)
                })
                .OrderBy(c => c.Name)
                .ToListAsync();
        }

        private string BuildEmailBody(string? customerName, string surveyUrl)
        {
            var name = customerName ?? "Değerli Müşterimiz";
            var message = string.IsNullOrEmpty(PersonalMessage)
                ? "Turkon Lojistik olarak sizden aldığımız hizmetlerimiz hakkında geri bildirim rica ediyoruz."
                : PersonalMessage;

            return $@"
                <html>
                <body style='font-family: Arial, sans-serif; line-height: 1.6; color: #333;'>
                    <div style='max-width: 600px; margin: 0 auto; padding: 20px;'>
                        <h2 style='color: #1e40af;'>Turkon Lojistik</h2>
                        <p>Sayın <strong>{name}</strong>,</p>
                        <p>{message}</p>
                        <p>Anketimize katılmak için aşağıdaki butona tıklayabilirsiniz:</p>
                        <div style='text-align: center; margin: 30px 0;'>
                            <a href='{surveyUrl}' 
                               style='background: #2563eb; color: white; padding: 12px 30px; 
                                      text-decoration: none; border-radius: 5px; display: inline-block;'>
                                Ankete Katıl
                            </a>
                        </div>
                        <p>Veya bağlantıyı tarayıcınıza kopyalayın:</p>
                        <p style='background: #f3f4f6; padding: 10px; border-radius: 5px; word-break: break-all;'>
                            {surveyUrl}
                        </p>
                        <hr style='margin: 30px 0; border: none; border-top: 1px solid #e5e7eb;' />
                        <p style='font-size: 12px; color: #6b7280;'>
                            Bu mail Turkon Lojistik CEA Sistemi tarafından otomatik olarak gönderilmiştir.
                        </p>
                    </div>
                </body>
                </html>";
        }
    }

    public class CustomerViewModel
    {
        public int Id { get; set; }
        public string Email { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public bool HasResponded { get; set; }
    }
}