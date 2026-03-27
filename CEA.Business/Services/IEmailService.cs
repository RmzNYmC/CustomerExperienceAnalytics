using System.Threading.Tasks;

namespace CEA.Business.Services
{
    public interface IEmailService
    {
        // Ana async metod
        Task SendEmailAsync(string to, string subject, string htmlBody);

        // Eski sync metod
        void SendEmail(string to, string subject, string body);

        // Şikayet bildirimi - 4 Parametre (Senin gösterdiğin kullanım)
        Task SendComplaintNotificationAsync(string to, string ticketNumber, string customerName, string description);

        // Şikayet bildirimi - 3 Parametre (Eğer başka yerde böyle kullanılıyorsa)
        Task SendComplaintNotificationAsync(string to, string ticketNumber, string description);
    }
}