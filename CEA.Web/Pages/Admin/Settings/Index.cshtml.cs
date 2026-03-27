using CEA.Business.Services;
using CEA.Core.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using System.ComponentModel.DataAnnotations;
using System.Threading.Tasks;

namespace CEA.Web.Pages.Admin.Settings
{
    [Authorize(Roles = "Admin")]
    public class IndexModel : PageModel
    {
        private readonly ISettingsService _settingsService;
        private readonly IEmailService _emailService;

        public IndexModel(ISettingsService settingsService, IEmailService emailService)
        {
            _settingsService = settingsService;
            _emailService = emailService;
        }

        [BindProperty]
        public SmtpSettingsViewModel SmtpSettings { get; set; } = new();

        [BindProperty]
        [EmailAddress]
        public string TestEmail { get; set; } = string.Empty;

        [BindProperty]
        public string? TestMessage { get; set; }

        public string? SuccessMessage { get; set; }

        public async Task OnGetAsync()
        {
            // Mevcut ayarları yükle
            SmtpSettings.Host = await _settingsService.GetSettingAsync("SMTP_Host", "smtp.gmail.com");
            SmtpSettings.Port = int.Parse(await _settingsService.GetSettingAsync("SMTP_Port", "587"));
            SmtpSettings.FromName = await _settingsService.GetSettingAsync("SMTP_FromName", "Turkon Lojistik");
            SmtpSettings.FromEmail = await _settingsService.GetSettingAsync("SMTP_From", "noreply@turkon.com");
            SmtpSettings.Username = await _settingsService.GetSettingAsync("SMTP_Username", "");
            // Şifreyi gösterme, boş bırak
            SmtpSettings.Password = await _settingsService.GetSettingAsync("SMTP_Password", "");
        }

        public async Task<IActionResult> OnPostSaveSmtpAsync()
        {
            if (!ModelState.IsValid)
            {
                return Page();
            }

            await _settingsService.SetSettingAsync("SMTP_Host", SmtpSettings.Host, "SMTP");
            await _settingsService.SetSettingAsync("SMTP_Port", SmtpSettings.Port.ToString(), "SMTP");
            await _settingsService.SetSettingAsync("SMTP_FromName", SmtpSettings.FromName, "SMTP");
            await _settingsService.SetSettingAsync("SMTP_From", SmtpSettings.FromEmail, "SMTP");
            await _settingsService.SetSettingAsync("SMTP_Username", SmtpSettings.Username, "SMTP");

            if (!string.IsNullOrEmpty(SmtpSettings.Password))
            {
                await _settingsService.SetSettingAsync("SMTP_Password", SmtpSettings.Password, "SMTP");
            }

            SuccessMessage = "SMTP ayarları başarıyla kaydedildi.";
            return RedirectToPage();
        }

        public async Task<IActionResult> OnPostSendTestAsync()
        {
            try
            {
                var subject = "Turkon Lojistik - Test Maili";
                var body = $@"
                    <html>
                    <body style='font-family: Arial, sans-serif;'>
                        <h2 style='color: #2563eb;'>Turkon Lojistik</h2>
                        <p>Bu bir test mailidir.</p>
                        <p><strong>Mesaj:</strong> {TestMessage}</p>
                        <hr>
                        <p style='color: #666;'>Tarih: {DateTime.Now}</p>
                    </body>
                    </html>";

                await _emailService.SendEmailAsync(TestEmail, subject, body);

                SuccessMessage = $"Test maili başarıyla gönderildi: {TestEmail}";
            }
            catch (Exception ex)
            {
                ModelState.AddModelError("", $"Mail gönderilemedi: {ex.Message}");
            }

            // SMTP ayarlarını tekrar yükle 
            await OnGetAsync();
            return Page();
        }
    }

    public class SmtpSettingsViewModel
    {
        [Required]
        public string Host { get; set; } = string.Empty;

        [Required]
        public int Port { get; set; } = 587;

        [Required]
        public string FromName { get; set; } = string.Empty;

        [Required]
        [EmailAddress]
        public string FromEmail { get; set; } = string.Empty;

        [Required]
        public string Username { get; set; } = string.Empty;

        public string Password { get; set; } = string.Empty;
    }
}