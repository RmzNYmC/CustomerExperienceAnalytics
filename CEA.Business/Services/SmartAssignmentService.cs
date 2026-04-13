using CEA.Core.Entities;
using CEA.Core.Enum;
using CEA.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace CEA.Business.Services
{
    public class SmartAssignmentService : ISmartAssignmentService
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogger<SmartAssignmentService> _logger;
        private readonly ISlaCalculatorService _slaService;
        private readonly ISettingsService _settingsService;



        // Kategori - Departman/Uzmanlık eşleştirmesi
        private readonly Dictionary<string, string[]> _categoryExpertise = new()
        {
            ["Ürün"] = new[] { "Ürün", "Kalite", "Genel" },
            ["Hizmet"] = new[] { "Hizmet", "Müşteri İlişkileri", "Genel" },
            ["Personel"] = new[] { "İK", "İnsan Kaynakları", "Genel" },
            ["Lojistik"] = new[] { "Lojistik", "Operasyon", "Genel" },
            ["Genel"] = new[] { "Genel", "Müşteri İlişkileri" }
        };


        public SmartAssignmentService(
            ApplicationDbContext context,
            ILogger<SmartAssignmentService> logger,
            ISlaCalculatorService slaService,
            ISettingsService settingsService)
        {
            _context = context;
            _logger = logger;
            _slaService = slaService;
            _settingsService = settingsService;
        }

        public async Task<string?> FindBestAssigneeAsync(string category, string priority, int? excludeUserId = null)
        {
            // 1. Kategori uzmanlığına sahip aktif kullanıcıları bul
            var expertiseList = _categoryExpertise.GetValueOrDefault(category, new[] { "Genel" });
            var excludeUserIdStr = excludeUserId?.ToString();

            // 2. TEK SORGUDA workload'ları hesapla (N+1 FIX)
            var userWorkloads = await _context.Users
                .Where(u => u.IsActive && u.Id != excludeUserIdStr)
                .Where(u => expertiseList.Any(exp =>
                    u.Department != null &&
                    (u.Department.Contains(exp) || exp.Contains(u.Department))))
                .GroupJoin(
                    _context.Complaints.Where(c =>
                        c.Status != ComplaintStatus.Closed &&
                        c.Status != ComplaintStatus.Resolved &&
                        !c.IsDeleted),
                    u => u.Id,
                    c => c.AssignedToUserId,
                    (u, complaints) => new
                    {
                        User = u,
                        OpenCount = complaints.Count(),
                        CriticalCount = complaints.Count(c => c.Priority == "Critical")
                    })
                .ToListAsync();

            // Uzman bulunamazsa tüm aktif kullanıcılara bak
            if (!userWorkloads.Any())
            {
                userWorkloads = await _context.Users
                    .Where(u => u.IsActive && u.Id != excludeUserIdStr)
                    .GroupJoin(
                        _context.Complaints.Where(c =>
                            c.Status != ComplaintStatus.Closed &&
                            c.Status != ComplaintStatus.Resolved &&
                            !c.IsDeleted),
                        u => u.Id,
                        c => c.AssignedToUserId,
                        (u, complaints) => new
                        {
                            User = u,
                            OpenCount = complaints.Count(),
                            CriticalCount = complaints.Count(c => c.Priority == "Critical")
                        })
                    .ToListAsync();
            }

            if (!userWorkloads.Any()) return null;

            // 3. Skorlama algoritması
            var idleThresholdMinutes = await GetIdleThresholdAsync();

            var scoredUsers = await Task.WhenAll(userWorkloads.Select(async uw =>
            {
                var score = 100;
                score -= uw.OpenCount * 10;
                score -= uw.CriticalCount * 20;

                // Son atama zamanı kontrolü
                var lastAssignment = await _context.Complaints
                    .Where(c => c.AssignedToUserId == uw.User.Id && c.AssignedAt.HasValue)
                    .OrderByDescending(c => c.AssignedAt)
                    .Select(c => c.AssignedAt)
                    .FirstOrDefaultAsync();

                if (lastAssignment.HasValue &&
                    (DateTime.Now - lastAssignment.Value).TotalMinutes > idleThresholdMinutes)
                {
                    score += 15;
                }

                return new { uw.User, Score = score, uw.OpenCount };
            }));

            var bestMatch = scoredUsers.OrderByDescending(x => x.Score).FirstOrDefault();

            if (bestMatch != null)
            {
                _logger.LogInformation(
                    "Akıllı atama: {Category} -> {User} (Skor: {Score}, Mevcut İş: {Count})",
                    category, bestMatch.User.Email, bestMatch.Score, bestMatch.OpenCount);
            }

            return bestMatch?.User.Id;
        }

        public async Task<bool> AutoAssignComplaintAsync(int complaintId)
        {
            using var transaction = await _context.Database.BeginTransactionAsync();

            try
            {
                var complaint = await _context.Complaints.FindAsync(complaintId);
                if (complaint == null || !string.IsNullOrEmpty(complaint.AssignedToUserId))
                    return false;

                var assigneeId = await FindBestAssigneeAsync(complaint.Category, complaint.Priority);

                if (assigneeId == null)
                {
                    _logger.LogWarning("Otomatik atama başarısız: Uygun kullanıcı bulunamadı. Ticket: {Ticket}",
                        complaint.TicketNumber);
                    return false;
                }

                complaint.AssignedToUserId = assigneeId;
                complaint.AssignedAt = DateTime.Now;
                complaint.Status = ComplaintStatus.InProgress;
                complaint.DueDate = _slaService.CalculateDueDate(complaint.Priority, complaint.Category);

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                // Email bildirimi (varsa)
                try
                {
                    // Email gönderme kodu...
                }
                catch { /* Email hatası kritik değil */ }

                _logger.LogInformation("Şikayet {Ticket} otomatik atandı: {User}",
                    complaint.TicketNumber, assigneeId);

                return true;
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                _logger.LogError(ex, "Otomatik atama sırasında hata. Ticket ID: {Id}", complaintId);
                throw;
            }
        }

        public async Task<List<UserWorkload>> GetUserWorkloadsAsync()
        {
            var workloadData = await _context.Users
                .Where(u => u.IsActive)
                .Select(u => new
                {
                    User = u,
                    TotalAssigned = _context.Complaints.Count(c =>
                        c.AssignedToUserId == u.Id && !c.IsDeleted),
                    OpenCount = _context.Complaints.Count(c =>
                        c.AssignedToUserId == u.Id &&
                        c.Status != ComplaintStatus.Closed &&
                        c.Status != ComplaintStatus.Resolved &&
                        !c.IsDeleted),
                    CriticalCount = _context.Complaints.Count(c =>
                        c.AssignedToUserId == u.Id &&
                        c.Priority == "Critical" &&
                        c.Status != ComplaintStatus.Closed &&
                        !c.IsDeleted),
                    BreachedCount = _context.Complaints.Count(c =>
                        c.AssignedToUserId == u.Id &&
                        c.IsSlaBreached &&
                        c.Status != ComplaintStatus.Closed &&
                        !c.IsDeleted),
                    // DÜZELTME: (int?) cast eklendi
                    AvgResolutionTime = (int?)_context.Complaints
                        .Where(c => c.AssignedToUserId == u.Id &&
                                   c.ResolutionTimeMinutes.HasValue)
                        .Select(c => (double?)c.ResolutionTimeMinutes)
                        .Average()
                })
                .ToListAsync();

            return workloadData.Select(w => new UserWorkload
            {
                UserId = w.User.Id,
                UserName = $"{w.User.FirstName} {w.User.LastName}",
                Department = w.User.Department ?? "Belirtilmemiş",
                TotalAssigned = w.TotalAssigned,
                OpenCount = w.OpenCount,
                CriticalCount = w.CriticalCount,
                BreachedCount = w.BreachedCount,
                AvgResolutionTime = w.AvgResolutionTime
            }).OrderByDescending(w => w.OpenCount).ToList();
        }

        public async Task ReassignComplaintAsync(int complaintId, string newUserId, string reason)
        {
            using var transaction = await _context.Database.BeginTransactionAsync();

            try
            {
                var complaint = await _context.Complaints.FindAsync(complaintId);
                if (complaint == null) return;

                var oldUserId = complaint.AssignedToUserId;

                complaint.AssignedToUserId = newUserId;
                complaint.AssignedAt = DateTime.Now;

                var note = new ComplaintNote
                {
                    ComplaintId = complaintId,
                    Note = $"Yeniden atandı. Eski: {oldUserId} -> Yeni: {newUserId}. Sebep: {reason}",
                    UserId = "System",
                    IsInternal = true
                };

                _context.ComplaintNotes.Add(note);
                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                _logger.LogInformation("Şikayet {Ticket} yeniden atandı: {Reason}",
                    complaint.TicketNumber, reason);
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                _logger.LogError(ex, "Yeniden atama sırasında hata. Ticket ID: {Id}", complaintId);
                throw;
            }
        }

        private async Task<int> GetIdleThresholdAsync()
        {
            var thresholdStr = await _settingsService.GetSettingAsync("SmartAssignment_IdleThresholdMinutes", "30");
            return int.TryParse(thresholdStr, out var result) ? result : 30;
        }
    }
}