using CEA.Core.Entities;

namespace CEA.Business.Services
{
    public interface IAuditService
    {
        Task LogActionAsync(string action, string entityType, int? entityId,
            object? oldValues = null, object? newValues = null,
            bool success = true, string? error = null);

        Task LogComplaintCreatedAsync(int complaintId, string customerEmail);
        Task LogComplaintAssignedAsync(int complaintId, string? oldUserId, string newUserId);
        Task LogComplaintStatusChangedAsync(int complaintId, string oldStatus, string newStatus);
        Task LogSurveyResponseAsync(int surveyId, int responseId);

        Task<List<AuditLog>> GetRecentActivitiesAsync(int count = 20);
        Task<List<AuditLog>> GetEntityHistoryAsync(string entityType, int entityId);
    }
}