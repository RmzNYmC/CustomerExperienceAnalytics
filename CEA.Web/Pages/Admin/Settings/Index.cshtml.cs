using CEA.Business.Services;
using CEA.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;

namespace CEA.Web.Pages.Admin.Settings
{
    [Authorize(Roles = "Admin")]
    public class IndexModel : PageModel
    {
        private readonly ISettingsService _settingsService;
        private readonly IEmailService _emailService;
        private readonly ApplicationDbContext _context;
        private readonly ILogger<IndexModel> _logger;

        public IndexModel(
        ISettingsService settingsService,
        IEmailService emailService,
        ApplicationDbContext context,
        ILogger<IndexModel> logger)
        {
            _settingsService = settingsService;
            _emailService = emailService;
            _context = context;
            _logger = logger;
        }


        [BindProperty]
        public SmtpSettingsViewModel SmtpSettings { get; set; } = new();

        public List<CEA.Core.Entities.Survey> Surveys { get; set; } = new();

        [BindProperty]
        public string TestEmail { get; set; } = string.Empty; // Required kaldırıldı!

        [BindProperty]
        public string? TestMessage { get; set; }

        [TempData]
        public string? SuccessMessage { get; set; }

        [TempData]
        public string? ErrorMessage { get; set; }

        public async Task OnGetAsync()
        {
            Surveys = await _context.Surveys
        .Where(s => !s.IsDeleted)
        .OrderBy(s => s.Title)
        .ToListAsync();
            await LoadSettingsAsync();
        }

        private async Task LoadSettingsAsync()
        {
            SmtpSettings.Host = await _settingsService.GetSettingAsync("SMTP_Host", "smtp.gmail.com");
            SmtpSettings.Port = int.Parse(await _settingsService.GetSettingAsync("SMTP_Port", "587"));
            SmtpSettings.EnableSsl = bool.Parse(await _settingsService.GetSettingAsync("SMTP_EnableSsl", "true"));
            SmtpSettings.FromName = await _settingsService.GetSettingAsync("SMTP_FromName", "Turkon Lojistik");
            SmtpSettings.FromEmail = await _settingsService.GetSettingAsync("SMTP_From", "noreply@turkon.com");
            SmtpSettings.Username = await _settingsService.GetSettingAsync("SMTP_Username", "");
            SmtpSettings.Password = ""; // Şifreyi gösterme
        }

        public async Task<IActionResult> OnPostSaveSmtpAsync()
        {
            // Sadece SmtpSettings validasyonunu kontrol et
            if (!ModelState.IsValid)
            {
                // ModelState'ten sadece SmtpSettings ile ilgili hataları al
                var errors = ModelState
                    .Where(x => x.Key.StartsWith("SmtpSettings"))
                    .SelectMany(x => x.Value.Errors)
                    .Select(e => e.ErrorMessage);

                if (errors.Any())
                {
                    ErrorMessage = "Formda hata var: " + string.Join(", ", errors);
                    return Page();
                }
            }

            try
            {
                await _settingsService.SetSettingAsync("SMTP_Host", SmtpSettings.Host, "SMTP");
                await _settingsService.SetSettingAsync("SMTP_Port", SmtpSettings.Port.ToString(), "SMTP");
                await _settingsService.SetSettingAsync("SMTP_EnableSsl", SmtpSettings.EnableSsl.ToString(), "SMTP");
                await _settingsService.SetSettingAsync("SMTP_FromName", SmtpSettings.FromName, "SMTP");
                await _settingsService.SetSettingAsync("SMTP_From", SmtpSettings.FromEmail, "SMTP");
                await _settingsService.SetSettingAsync("SMTP_Username", SmtpSettings.Username, "SMTP");

                if (!string.IsNullOrWhiteSpace(SmtpSettings.Password))
                {
                    await _settingsService.SetSettingAsync("SMTP_Password", SmtpSettings.Password, "SMTP");
                }

                SuccessMessage = "SMTP ayarları başarıyla kaydedildi.";
                return RedirectToPage();
            }
            catch (Exception ex)
            {
                ErrorMessage = $"Kayıt hatası: {ex.Message}";
                return Page();
            }
        }

        public async Task<IActionResult> OnPostSendTestAsync()
        {
            // TestEmail boş mu kontrol et
            if (string.IsNullOrWhiteSpace(TestEmail))
            {
                ErrorMessage = "Test maili için e-posta adresi girin.";
                await LoadSettingsAsync();
                return Page();
            }

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
                SuccessMessage = $"Test maili gönderildi: {TestEmail}";
            }
            catch (Exception ex)
            {
                ErrorMessage = $"Mail gönderilemedi: {ex.Message}";
            }

            await LoadSettingsAsync();
            return Page();
        }

        public async Task<IActionResult> OnPostCleanupTestDataAsync(int deleteCount, int? surveyId = null)
        {
            try
            {
                var query = _context.SurveyResponses.AsQueryable();
                if (surveyId.HasValue) query = query.Where(r => r.SurveyId == surveyId.Value);

                var responsesToDelete = await query
                    .OrderByDescending(r => r.CreatedAt)
                    .Take(deleteCount)
                    .ToListAsync();

                if (!responsesToDelete.Any())
                {
                    TempData["WarningMessage"] = "Silinecek yanıt bulunamadı.";
                    return RedirectToPage();
                }

                var responseIds = responsesToDelete.Select(r => r.Id).ToList();
                _logger.LogInformation("Silinecek ID'ler: {Ids}", string.Join(", ", responseIds));

                // 1. Cevapları (Answers) sil
                var answers = await _context.Answers
                    .Where(a => responseIds.Contains(a.ResponseId))
                    .ToListAsync();

                if (answers.Any())
                {
                    _context.Answers.RemoveRange(answers);
                    await _context.SaveChangesAsync();
                    _logger.LogInformation("✅ {Count} cevap silindi", answers.Count);
                }

                // 2. Şikayet Notlarını (ComplaintNotes) sil - Varsa
                var complaintIds = await _context.Complaints
                    .Where(c => responseIds.Contains(c.SurveyResponseId))
                    .Select(c => c.Id)
                    .ToListAsync();

                if (complaintIds.Any())
                {
                    // Notları sil (varsa)
                    try
                    {
                        var notes = await _context.ComplaintNotes
                            .Where(n => complaintIds.Contains(n.ComplaintId))
                            .ToListAsync();

                        if (notes.Any())
                        {
                            _context.ComplaintNotes.RemoveRange(notes);
                            await _context.SaveChangesAsync();
                            _logger.LogInformation("✅ {Count} not silindi", notes.Count);
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Notlar silinirken hata (olmayabilir): {Message}", ex.Message);
                        // Devam et, kritik değil
                    }

                    // Şikayetleri sil
                    var complaints = await _context.Complaints
                        .Where(c => responseIds.Contains(c.SurveyResponseId))
                        .ToListAsync();

                    _context.Complaints.RemoveRange(complaints);
                    await _context.SaveChangesAsync();
                    _logger.LogInformation("✅ {Count} şikayet silindi", complaints.Count);
                }

                // 3. Yanıtları sil
                _context.SurveyResponses.RemoveRange(responsesToDelete);
                await _context.SaveChangesAsync();
                _logger.LogInformation("✅ {Count} yanıt silindi", responsesToDelete.Count);

                TempData["SuccessMessage"] = $"{responsesToDelete.Count} kayıt tamamen silindi.";
            }
            catch (DbUpdateException dbEx)
            {
                _logger.LogError(dbEx, "❌ DbUpdateException: {Message}", dbEx.Message);
                _logger.LogError(dbEx.InnerException, "Inner: {Inner}", dbEx.InnerException?.Message);
                TempData["ErrorMessage"] = "Veritabanı hatası: " + (dbEx.InnerException?.Message ?? dbEx.Message);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Hata: {Message}", ex.Message);
                TempData["ErrorMessage"] = "Hata: " + ex.Message;
            }

            return RedirectToPage();
        }
    }

    public class SmtpSettingsViewModel
    {
        [Required(ErrorMessage = "SMTP sunucu zorunludur")]
        public string Host { get; set; } = string.Empty;

        [Required(ErrorMessage = "Port zorunludur")]
        [Range(1, 65535, ErrorMessage = "Geçerli bir port numarası girin")]
        public int Port { get; set; } = 587;

        public bool EnableSsl { get; set; } = true;

        [Required(ErrorMessage = "Gönderici adı zorunludur")]
        public string FromName { get; set; } = string.Empty;

        [Required(ErrorMessage = "Gönderici e-posta zorunludur")]
        [EmailAddress(ErrorMessage = "Geçerli bir e-posta girin")]
        public string FromEmail { get; set; } = string.Empty;

        [Required(ErrorMessage = "Kullanıcı adı zorunludur")]
        public string Username { get; set; } = string.Empty;

        public string Password { get; set; } = string.Empty;
    }
}