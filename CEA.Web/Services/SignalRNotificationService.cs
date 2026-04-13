using CEA.Business.Services;
using CEA.Web.Hubs;
using Microsoft.AspNetCore.SignalR;

namespace CEA.Web.Services
{
    public class SignalRNotificationService : IRealTimeNotificationService
    {
        private readonly IHubContext<NotificationHub> _hubContext;
        private readonly ILogger<SignalRNotificationService> _logger;

        public SignalRNotificationService(
            IHubContext<NotificationHub> hubContext,
            ILogger<SignalRNotificationService> logger)
        {
            _hubContext = hubContext;
            _logger = logger;
        }

        public async Task NotifyNewComplaint(int complaintId, string ticketNumber, string priority)
        {
            var message = priority == "Critical"
                ? $"🚨 Kritik şikayet: {ticketNumber}"
                : $"Yeni şikayet: {ticketNumber}";

            await _hubContext.Clients.Group("Admins").SendAsync("ReceiveNotification", new
            {
                Title = priority == "Critical" ? "🚨 Kritik Şikayet" : "Yeni Şikayet",
                Message = message,
                Link = $"/Admin/Complaints/Edit?id={complaintId}",
                Type = priority == "Critical" ? "danger" : "info",
                Timestamp = DateTime.Now
            });

            _logger.LogInformation("SignalR: {Ticket} bildirimi gönderildi", ticketNumber);
        }

        public async Task NotifyComplaintAssigned(int complaintId, string assignedToUserId, string assignerName)
        {
            await _hubContext.Clients.User(assignedToUserId).SendAsync("ReceiveNotification", new
            {
                Title = "Şikayet Atandı",
                Message = $"{assignerName} size yeni bir şikayet atadı",
                Link = $"/Admin/Complaints/Edit?id={complaintId}",
                Type = "success",
                Timestamp = DateTime.Now
            });
        }

        public async Task NotifySlaBreach(int complaintId, string ticketNumber)
        {
            await _hubContext.Clients.Group("Admins").SendAsync("ReceiveNotification", new
            {
                Title = "⚠️ SLA Aşımı",
                Message = $"Şikayet {ticketNumber} SLA süresini aştı!",
                Link = $"/Admin/Complaints/Edit?id={complaintId}",
                Type = "warning",
                Timestamp = DateTime.Now
            });
        }

        public async Task BroadcastDashboardUpdate()
        {
            await _hubContext.Clients.All.SendAsync("DashboardUpdate", new
            {
                Timestamp = DateTime.Now,
                Message = "Dashboard güncellendi"
            });
        }

        public async Task NotifySurveyResponse(int surveyId, string surveyTitle)
        {
            await _hubContext.Clients.Group("Admins").SendAsync("ReceiveNotification", new
            {
                Title = "Yeni Anket Yanıtı",
                Message = $"\"{surveyTitle}\" anketine yeni yanıt geldi",
                Link = $"/Admin/Analytics?SelectedSurveyId={surveyId}",
                Type = "info",
                Timestamp = DateTime.Now
            });
        }
    }
}