using CEA.Business.Services;
using CEA.Core.Entities;
using CEA.Core.Enum;
using CEA.Data;
using Microsoft.EntityFrameworkCore;

namespace CEA.Web.BackgroundServices
{
    public class SlaMonitorBackgroundService : BackgroundService
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<SlaMonitorBackgroundService> _logger;

        public SlaMonitorBackgroundService(
            IServiceProvider serviceProvider,
            ILogger<SlaMonitorBackgroundService> logger)
        {
            _serviceProvider = serviceProvider;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("SLA Monitor Background Service başlatıldı.");

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    using var scope = _serviceProvider.CreateScope();
                    var slaService = scope.ServiceProvider.GetRequiredService<ISlaCalculatorService>();
                    var emailService = scope.ServiceProvider.GetRequiredService<IEmailService>();
                    var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

                    // 1. SLA aşımlarını kontrol et ve işaretle
                    await slaService.CheckAndMarkBreachedSlasAsync();

                    // 2. Kritik SLA aşımları için email bildirimi
                    await NotifyCriticalBreachesAsync(context, emailService);

                    _logger.LogInformation("SLA kontrolü tamamlandı: {Time}", DateTime.Now);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "SLA kontrolü sırasında hata oluştu.");
                }

                // Her 15 dakikada bir kontrol et
                await Task.Delay(TimeSpan.FromMinutes(15), stoppingToken);
            }
        }

        private async Task NotifyCriticalBreachesAsync(ApplicationDbContext context, IEmailService emailService)
        {
            // Son 15 dakika içinde aşılan kritik şikayetleri bul
            var recentBreaches = await context.Complaints
                .Include(c => c.AssignedToUser)
                .Where(c => c.IsSlaBreached
                    && c.Priority == "Critical"
                    && c.Status != ComplaintStatus.Closed
                    && c.Status != ComplaintStatus.Resolved
                    && c.UpdatedAt > DateTime.Now.AddMinutes(-15))
                .ToListAsync();

            foreach (var complaint in recentBreaches)
            {
                if (complaint.AssignedToUser != null && !string.IsNullOrEmpty(complaint.AssignedToUser.Email))
                {
                    await emailService.SendEmailAsync(
                        complaint.AssignedToUser.Email,
                        "🚨 Kritik SLA Aşımı Uyarısı",
                        $@"
                        <h2>SLA Aşımı Uyarısı</h2>
                        <p><strong>Ticket:</strong> {complaint.TicketNumber}</p>
                        <p><strong>Müşteri:</strong> {complaint.CustomerName}</p>
                        <p><strong>Açılma:</strong> {complaint.CreatedAt:dd.MM.yyyy HH:mm}</p>
                        <p><strong>SLA Hedef:</strong> {complaint.DueDate:dd.MM.yyyy HH:mm}</p>
                        <p style='color: red; font-weight: bold;'>Bu şikayetin SLA süresi aşılmıştır!</p>"
                    );
                }
            }
        }
    }
}