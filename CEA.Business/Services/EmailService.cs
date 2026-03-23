using MailKit.Net.Smtp;
using MimeKit;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace CEA.Business.Services
{
    public interface IEmailService
    {
        Task SendComplaintNotificationAsync(string toEmail, string complaintNumber, string customerName, string description);
        Task SendSurveyInvitationAsync(string toEmail, string surveyTitle, string surveyUrl);
        Task SendPasswordResetAsync(string toEmail, string resetLink);
    }

    public class EmailService : IEmailService
    {
        private readonly IConfiguration _configuration;
        private readonly ILogger<EmailService> _logger;

        public EmailService(IConfiguration configuration, ILogger<EmailService> logger)
        {
            _configuration = configuration;
            _logger = logger;
        }

        private async Task SendEmailAsync(string toEmail, string subject, string htmlBody)
        {
            var emailSettings = _configuration.GetSection("EmailSettings");

            var message = new MimeMessage();
            message.From.Add(new MailboxAddress(
                emailSettings["SenderName"],
                emailSettings["SenderEmail"]
            ));
            message.To.Add(MailboxAddress.Parse(toEmail));
            message.Subject = subject;

            var bodyBuilder = new BodyBuilder { HtmlBody = htmlBody };
            message.Body = bodyBuilder.ToMessageBody();

            using var client = new SmtpClient();
            try
            {
                await client.ConnectAsync(
                    emailSettings["SmtpServer"],
                    int.Parse(emailSettings["SmtpPort"]!),
                    MailKit.Security.SecureSocketOptions.StartTls
                );

                await client.AuthenticateAsync(
                    emailSettings["Username"],
                    emailSettings["Password"]
                );

                await client.SendAsync(message);
                await client.DisconnectAsync(true);

                _logger.LogInformation("Email sent successfully to {Email}", toEmail);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to send email to {Email}", toEmail);
                throw;
            }
        }

        public async Task SendComplaintNotificationAsync(string toEmail, string complaintNumber,
            string customerName, string description)
        {
            var subject = $"Yeni Şikayet Kaydı: {complaintNumber}";
            var body = $@"
                <h2>Yeni Müşteri Şikayeti</h2>
                <p><strong>Şikayet No:</strong> {complaintNumber}</p>
                <p><strong>Müşteri:</strong> {customerName}</p>
                <p><strong>Açıklama:</strong> {description}</p>
                <p><strong>Tarih:</strong> {DateTime.Now:dd.MM.yyyy HH:mm}</p>
                <hr>
                <p>Şikayeti görüntülemek için <a href='{_configuration["AppSettings:BaseUrl"]}/Complaints/Details/{complaintNumber}'>tıklayınız</a>.</p>
            ";

            await SendEmailAsync(toEmail, subject, body);
        }

        public async Task SendSurveyInvitationAsync(string toEmail, string surveyTitle, string surveyUrl)
        {
            var subject = $"Anket Daveti: {surveyTitle}";
            var body = $@"
                <h2>Müşteri Deneyimi Anketi</h2>
                <p>Değerli müşterimiz,</p>
                <p>Hizmet kalitemizi artırmak için görüşlerinizi önemsiyoruz. 
                Aşağıdaki bağlantıdan <strong>{surveyTitle}</strong> anketimize katılabilirsiniz:</p>
                <p style='text-align: center; margin: 30px 0;'>
                    <a href='{surveyUrl}' style='background: #007bff; color: white; padding: 12px 24px; text-decoration: none; border-radius: 5px;'>Ankete Katıl</a>
                </p>
                <p>Anket yaklaşık 3-5 dakika sürecektir.</p>
                <p>Saygılarımızla,<br>{_configuration["AppSettings:CompanyName"]}</p>
            ";

            await SendEmailAsync(toEmail, subject, body);
        }

        public async Task SendPasswordResetAsync(string toEmail, string resetLink)
        {
            var subject = "Şifre Sıfırlama Talebi";
            var body = $@"
                <h2>Şifre Sıfırlama</h2>
                <p>Şifrenizi sıfırlamak için aşağıdaki bağlantıya tıklayın:</p>
                <p><a href='{resetLink}'>Şifremi Sıfırla</a></p>
                <p>Bu bağlantı 24 saat geçerlidir.</p>
                <p>Eğer bu talebi siz yapmadıysanız, bu e-postayı görmezden gelin.</p>
            ";

            await SendEmailAsync(toEmail, subject, body);
        }
    }
}