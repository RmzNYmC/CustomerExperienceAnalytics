using MailKit.Net.Smtp;
using MimeKit;
using Microsoft.Extensions.Configuration;
using System.Threading.Tasks;

namespace CEA.Business.Services
{
    public class EmailService : IEmailService
    {
        private readonly ISettingsService _settingsService;
        private readonly IConfiguration _configuration;

        public EmailService(ISettingsService settingsService, IConfiguration configuration)
        {
            _settingsService = settingsService;
            _configuration = configuration;
        }

        // Ana metod
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
            await smtp.ConnectAsync(settings.Host, settings.Port, MailKit.Security.SecureSocketOptions.StartTls);

            if (!string.IsNullOrEmpty(settings.Username))
            {
                await smtp.AuthenticateAsync(settings.Username, settings.Password);
            }

            await smtp.SendAsync(email);
            await smtp.DisconnectAsync(true);
        }

        // Eski sync metod
        public void SendEmail(string to, string subject, string body)
        {
            SendEmailAsync(to, subject, body).GetAwaiter().GetResult();
        }

        // ŞİKAYET BİLDİRİMİ - 4 Parametre (ComplaintAutomationService için)
        // 4 PARAMETRELİ (Ana implementasyon)
        public async Task SendComplaintNotificationAsync(string to, string ticketNumber, string customerName, string description)
        {
            var subject = $"Yeni Şikayet: {ticketNumber}";

            var htmlBody = $@"
        <html>
        <body style='font-family: Arial, sans-serif; line-height: 1.6; color: #333;'>
            <div style='max-width: 600px; margin: 0 auto; padding: 20px; border: 1px solid #dc2626; border-radius: 8px;'>
                <h2 style='color: #dc2626; margin-top: 0;'>
                    <i class='fas fa-exclamation-triangle'></i> Yeni Şikayet Bildirimi
                </h2>
                
                <table style='width: 100%; border-collapse: collapse;'>
                    <tr>
                        <td style='padding: 8px; border-bottom: 1px solid #eee; font-weight: bold; width: 120px;'>Ticket No:</td>
                        <td style='padding: 8px; border-bottom: 1px solid #eee;'>{ticketNumber}</td>
                    </tr>
                    <tr>
                        <td style='padding: 8px; border-bottom: 1px solid #eee; font-weight: bold;'>Müşteri:</td>
                        <td style='padding: 8px; border-bottom: 1px solid #eee;'>{customerName}</td>
                    </tr>
                </table>
                
                <div style='margin-top: 20px; padding: 15px; background-color: #fef2f2; border-left: 4px solid #dc2626; border-radius: 4px;'>
                    <h4 style='margin-top: 0; color: #991b1b;'>Şikayet Detayı:</h4>
                    <p style='margin-bottom: 0; white-space: pre-wrap;'>{description}</p>
                </div>
                
                <div style='margin-top: 20px; text-align: center;'>
                    <a href='#' style='display: inline-block; padding: 12px 24px; background-color: #dc2626; color: white; text-decoration: none; border-radius: 5px; font-weight: bold;'>
                        Şikayeti Görüntüle
                    </a>
                </div>
                
                <hr style='margin: 30px 0; border: none; border-top: 1px solid #e5e7eb;' />
                <p style='font-size: 12px; color: #6b7280; text-align: center;'>
                    Bu mail Turkon Lojistik CEA Sistemi tarafından otomatik olarak gönderilmiştir.<br>
                    Ticket: {ticketNumber}
                </p>
            </div>
        </body>
        </html>";

            await SendEmailAsync(to, subject, htmlBody);
        }

        // 3 PARAMETRELİ (4 parametreli olanı çağırır, müşteri adı "Belirtilmemiş" olarak gider)
        public async Task SendComplaintNotificationAsync(string to, string ticketNumber, string description)
        {
            await SendComplaintNotificationAsync(to, ticketNumber, "Belirtilmemiş", description);
        }

        // Yardımcı metod
        private async Task<SmtpSettings> GetSmtpSettingsAsync()
        {
            var portStr = await _settingsService.GetSettingAsync("SMTP_Port", "587");

            return new SmtpSettings
            {
                Host = await _settingsService.GetSettingAsync("SMTP_Host", "smtp.gmail.com"),
                Port = int.TryParse(portStr, out var port) ? port : 587,
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
        public string Username { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
        public string FromEmail { get; set; } = string.Empty;
        public string FromName { get; set; } = string.Empty;
    }
}