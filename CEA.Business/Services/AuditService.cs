using CEA.Core.Entities;
using CEA.Data;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System.Security.Claims;
using System.Text.Json;

namespace CEA.Business.Services
{
    public class AuditService : IAuditService
    {
        private readonly ApplicationDbContext _context;
        private readonly IHttpContextAccessor _httpContextAccessor;
        private readonly ILogger<AuditService> _logger;

        public AuditService(
            ApplicationDbContext context,
            IHttpContextAccessor httpContextAccessor,
            ILogger<AuditService> logger)
        {
            _context = context;
            _httpContextAccessor = httpContextAccessor;
            _logger = logger;
        }

        public async Task LogActionAsync(string action, string entityType, int? entityId,
            object? oldValues = null, object? newValues = null,
            bool success = true, string? error = null)
        {
            try
            {
                var httpContext = _httpContextAccessor.HttpContext;
                var user = httpContext?.User;

                var auditLog = new AuditLog
                {
                    Action = action,
                    EntityType = entityType,
                    EntityId = entityId,
                    OldValues = oldValues != null ? JsonSerializer.Serialize(oldValues) : null,
                    NewValues = newValues != null ? JsonSerializer.Serialize(newValues) : null,
                    UserId = user?.FindFirstValue(ClaimTypes.NameIdentifier),
                    UserName = user?.Identity?.Name ?? "System",
                    IpAddress = httpContext?.Connection?.RemoteIpAddress?.ToString(),
                    Success = success,
                    ErrorMessage = error,
                    CreatedAt = DateTime.Now
                };

                _context.AuditLogs.Add(auditLog);
                await _context.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Audit log kaydedilemedi");
            }
        }

        public async Task LogComplaintCreatedAsync(int complaintId, string customerEmail)
        {
            await LogActionAsync("Create", "Complaint", complaintId,
                newValues: new { CustomerEmail = customerEmail, CreatedAt = DateTime.Now });
        }

        public async Task LogComplaintAssignedAsync(int complaintId, string? oldUserId, string newUserId)
        {
            await LogActionAsync("Assign", "Complaint", complaintId,
                oldValues: new { AssignedToUserId = oldUserId },
                newValues: new { AssignedToUserId = newUserId });
        }

        public async Task LogComplaintStatusChangedAsync(int complaintId, string oldStatus, string newStatus)
        {
            await LogActionAsync("StatusChange", "Complaint", complaintId,
                oldValues: new { Status = oldStatus },
                newValues: new { Status = newStatus });
        }

        public async Task LogSurveyResponseAsync(int surveyId, int responseId)
        {
            await LogActionAsync("Response", "Survey", surveyId,
                newValues: new { ResponseId = responseId, SubmittedAt = DateTime.Now });
        }

        public async Task<List<AuditLog>> GetRecentActivitiesAsync(int count = 20)
        {
            return await _context.AuditLogs
                .AsNoTracking()
                .OrderByDescending(a => a.CreatedAt)
                .Take(count)
                .ToListAsync();
        }

        public async Task<List<AuditLog>> GetEntityHistoryAsync(string entityType, int entityId)
        {
            return await _context.AuditLogs
                .AsNoTracking()
                .Where(a => a.EntityType == entityType && a.EntityId == entityId)
                .OrderByDescending(a => a.CreatedAt)
                .ToListAsync();
        }
    }
}