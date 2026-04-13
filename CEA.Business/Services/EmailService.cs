using CEA.Core.Enum;        // ComplaintStatus için
using CEA.Data;             // ApplicationDbContext için
using MailKit.Net.Smtp;
using Microsoft.EntityFrameworkCore;  // ToListAsync için
using Microsoft.Extensions.Configuration;
using MimeKit;
using System.Net;
using System.Threading.Tasks;

namespace CEA.Business.Services
{
    public class EmailService : IEmailService
    {
        private readonly ISettingsService _settingsService;
        private readonly IConfiguration _configuration;
        private readonly ApplicationDbContext _context;  // ✅ EKLENDİ

        // ✅ CONSTRUCTOR GÜNCELLENDİ
        public EmailService(
            ISettingsService settingsService,
            IConfiguration configuration,
            ApplicationDbContext context)
        {
            _settingsService = settingsService;
            _configuration = configuration;
            _context = context;
        }

        public async Task SendEmailAsync(string to, string subject, string htmlBody)
        {
            var settings = await GetSmtpSettingsAsync();

            var email = new MimeMessage();
            email.From.Add(new MailboxAddress(settings.FromName, settings.FromEmail));
            email.To.Add(MailboxAddress.Parse(to));
            email.Subject = subject;

            var builder = new BodyBuilder { HtmlBody = htmlBody };
            email.Body = builder.ToMessageBody();

            using var smtp = new SmtpClient();
            var secureOption = settings.EnableSsl
                ? MailKit.Security.SecureSocketOptions.StartTls
                : MailKit.Security.SecureSocketOptions.None;

            await smtp.ConnectAsync(settings.Host, settings.Port, secureOption);

            if (!string.IsNullOrEmpty(settings.Username))
            {
                await smtp.AuthenticateAsync(settings.Username, settings.Password);
            }

            await smtp.SendAsync(email);
            await smtp.DisconnectAsync(true);
        }

        public void SendEmail(string to, string subject, string body)
        {
            SendEmailAsync(to, subject, body).GetAwaiter().GetResult();
        }

        public async Task SendComplaintNotificationAsync(string to, string ticketNumber, string customerName, string description)
        {
            // XSS Koruması - HTML Encoding
            var safeTicketNumber = WebUtility.HtmlEncode(ticketNumber);
            var safeCustomerName = WebUtility.HtmlEncode(customerName);
            var safeDescription = WebUtility.HtmlEncode(description).Replace("\n", "<br/>");

            var subject = $"Yeni Şikayet: {safeTicketNumber}";

            var htmlBody = $@"
    <html>
    <body style='font-family: Arial, sans-serif;'>
        <div style='border: 1px solid #dc2626; border-radius: 8px; padding: 20px;'>
            <h2 style='color: #dc2626;'>🚨 Yeni Şikayet Bildirimi</h2>
            
            <p><strong>Ticket:</strong> {safeTicketNumber}</p>
            <p><strong>Müşteri:</strong> {safeCustomerName}</p>
            
            <div style='background: #fef2f2; padding: 15px; border-left: 4px solid #dc2626;'>
                <h4>Şikayet Detayı:</h4>
                <p>{safeDescription}</p>
            </div>
        </div>
    </body>
    </html>";

            await SendEmailAsync(to, subject, htmlBody);
        }

        public async Task SendComplaintNotificationAsync(string to, string ticketNumber, string description)
        {
            await SendComplaintNotificationAsync(to, ticketNumber, "Belirtilmemiş", description);
        }

        // ✅ YENİ METOD - EKSİK OLDUĞU İÇİN HATA VERİYORDU
        public async Task SendDailySummaryAsync()
        {
            var yesterday = DateTime.Now.AddDays(-1);

            var newComplaints = await _context.Complaints
                .CountAsync(c => c.CreatedAt >= yesterday && !c.IsDeleted);

            var resolvedComplaints = await _context.Complaints
                .CountAsync(c => c.ResolvedAt >= yesterday && !c.IsDeleted);

            var breachedSlas = await _context.Complaints
                .CountAsync(c => c.IsSlaBreached
                    && c.Status != ComplaintStatus.Closed
                    && !c.IsDeleted);

            var newResponses = await _context.SurveyResponses
                .CountAsync(r => r.CreatedAt >= yesterday && !r.IsDeleted);

            var subject = $"Günlük Özet Rapor - {DateTime.Now:dd.MM.yyyy}";

            var htmlBody = $@"
            <html>
            <body style='font-family: Arial, sans-serif; line-height: 1.6; color: #333;'>
                <div style='max-width: 600px; margin: 0 auto; padding: 20px;'>
                    <h2 style='color: #2563eb; border-bottom: 2px solid #e5e7eb; padding-bottom: 10px;'>
                        📊 Günlük Özet Raporu
                    </h2>
                    <p style='color: #6b7280; font-size: 14px;'>{yesterday:dd.MM.yyyy} - {DateTime.Now:dd.MM.yyyy}</p>
                    
                    <table style='width: 100%; margin: 20px 0;'>
                        <tr>
                            <td style='background: #fef2f2; border-left: 4px solid #dc2626; padding: 15px; text-align: center; width: 50%;'>
                                <div style='font-size: 28px; font-weight: bold; color: #dc2626;'>{newComplaints}</div>
                                <div style='font-size: 12px; color: #991b1b;'>Yeni Şikayet</div>
                            </td>
                            <td style='background: #f0fdf4; border-left: 4px solid #16a34a; padding: 15px; text-align: center;'>
                                <div style='font-size: 28px; font-weight: bold; color: #16a34a;'>{resolvedComplaints}</div>
                                <div style='font-size: 12px; color: #166534;'>Çözülen Şikayet</div>
                            </td>
                        </tr>
                        <tr><td colspan='2' style='height: 10px;'></td></tr>
                        <tr>
                            <td style='background: #fff7ed; border-left: 4px solid #ea580c; padding: 15px; text-align: center;'>
                                <div style='font-size: 28px; font-weight: bold; color: #ea580c;'>{breachedSlas}</div>
                                <div style='font-size: 12px; color: #9a3412;'>Aşık SLA</div>
                            </td>
                            <td style='background: #eff6ff; border-left: 4px solid #2563eb; padding: 15px; text-align: center;'>
                                <div style='font-size: 28px; font-weight: bold; color: #2563eb;'>{newResponses}</div>
                                <div style='font-size: 12px; color: #1e40af;'>Yeni Anket Yanıtı</div>
                            </td>
                        </tr>
                    </table>
                    
                    <hr style='margin: 30px 0; border: none; border-top: 1px solid #e5e7eb;' />
                    <p style='font-size: 11px; color: #9ca3af; text-align: center;'>
                        Bu rapor otomatik olarak oluşturulmuştur.<br>
                        CEA - Customer Experience Analytics Sistemi
                    </p>
                </div>
            </body>
            </html>";

            // Admin'lere gönder
            var admins = await _context.Users
                .Where(u => u.IsActive && !string.IsNullOrEmpty(u.Email))
                .ToListAsync();

            foreach (var admin in admins)
            {
                try
                {
                    await SendEmailAsync(admin.Email!, subject, htmlBody);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Mail gönderilemedi: {admin.Email}, Hata: {ex.Message}");
                }
            }
        }

        private async Task<SmtpSettings> GetSmtpSettingsAsync()
        {
            var portStr = await _settingsService.GetSettingAsync("SMTP_Port", "587");
            var enableSslStr = await _settingsService.GetSettingAsync("SMTP_EnableSsl", "true");

            return new SmtpSettings
            {
                Host = await _settingsService.GetSettingAsync("SMTP_Host", "smtp.gmail.com"),
                Port = int.TryParse(portStr, out var port) ? port : 587,
                EnableSsl = bool.TryParse(enableSslStr, out var ssl) ? ssl : true,
                Username = await _settingsService.GetSettingAsync("SMTP_Username", ""),
                Password = await _settingsService.GetSettingAsync("SMTP_Password", ""),
                FromEmail = await _settingsService.GetSettingAsync("SMTP_From", "noreply@turkon.com"),
                FromName = await _settingsService.GetSettingAsync("SMTP_FromName", "Turkon Lojistik")
            };
        }
    }

    internal class SmtpSettings
    {
        public string Host { get; set; } = string.Empty;
        public int Port { get; set; }
        public bool EnableSsl { get; set; } = true;
        public string Username { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
        public string FromEmail { get; set; } = string.Empty;
        public string FromName { get; set; } = string.Empty;
    }
}