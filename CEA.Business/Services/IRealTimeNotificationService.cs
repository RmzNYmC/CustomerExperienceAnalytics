namespace CEA.Business.Services
{
    public interface IRealTimeNotificationService
    {
        Task NotifyNewComplaint(int complaintId, string ticketNumber, string priority);
        Task NotifyComplaintAssigned(int complaintId, string assignedToUserId, string assignerName);
        Task NotifySlaBreach(int complaintId, string ticketNumber);
        Task BroadcastDashboardUpdate();
        Task NotifySurveyResponse(int surveyId, string surveyTitle);
    }
}