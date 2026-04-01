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
            ISlaCalculatorService slaService)
        {
            _context = context;
            _logger = logger;
            _slaService = slaService;
        }

        public async Task<string?> FindBestAssigneeAsync(string category, string priority, int? excludeUserId = null)
        {
            // 1. Kategori uzmanlığına sahip aktif kullanıcıları bul
            var expertiseList = _categoryExpertise.GetValueOrDefault(category, new[] { "Genel" });

            var potentialUsers = await _context.Users
                .Where(u => u.IsActive && u.Id != excludeUserId.ToString())
                .Where(u => expertiseList.Any(exp =>
                    u.Department != null &&
                    (u.Department.Contains(exp) || exp.Contains(u.Department))))
                .ToListAsync();

            if (!potentialUsers.Any())
            {
                // Uzman bulunamazsa tüm aktif kullanıcılara bak
                potentialUsers = await _context.Users
                    .Where(u => u.IsActive && u.Id != excludeUserId.ToString())
                    .ToListAsync();
            }

            if (!potentialUsers.Any()) return null;

            // 2. Workload (mevcut açık şikayet sayısı) hesapla
            var userWorkloads = new List<(ApplicationUser User, int OpenComplaints, int CriticalCount)>();

            foreach (var user in potentialUsers)
            {
                var openComplaints = await _context.Complaints
                    .CountAsync(c => c.AssignedToUserId == user.Id
                        && c.Status != ComplaintStatus.Closed
                        && c.Status != ComplaintStatus.Resolved
                        && !c.IsDeleted);

                var criticalCount = await _context.Complaints
                    .CountAsync(c => c.AssignedToUserId == user.Id
                        && c.Priority == "Critical"
                        && c.Status != ComplaintStatus.Closed
                        && !c.IsDeleted);

                userWorkloads.Add((user, openComplaints, criticalCount));
            }

            // 3. Skorlama algoritması
            // Formül: Düşük workload + Düşük critical count + Uzmanlık eşleşmesi
            var scoredUsers = userWorkloads.Select(uw =>
            {
                var score = 100; // Başlangıç puanı

                // Workload cezası (her açık şikayet -10 puan)
                score -= uw.OpenComplaints * 10;

                // Critical şikayet ağırlığı (her biri -20 puan)
                score -= uw.CriticalCount * 20;

                // Son atama zamanı bonusu (30dk'dan eski ise +15)
                var lastAssignment = _context.Complaints
                    .Where(c => c.AssignedToUserId == uw.User.Id && c.AssignedAt.HasValue)
                    .OrderByDescending(c => c.AssignedAt)
                    .Select(c => c.AssignedAt)
                    .FirstOrDefault();

                if (lastAssignment.HasValue && (DateTime.Now - lastAssignment.Value).TotalMinutes > 30)
                    score += 15;

                return new { uw.User, Score = score, uw.OpenComplaints };
            }).OrderByDescending(x => x.Score).ToList();

            var bestMatch = scoredUsers.FirstOrDefault();

            if (bestMatch != null)
            {
                _logger.LogInformation(
                    "Akıllı atama: {Category} -> {User} (Skor: {Score}, Mevcut İş: {Count})",
                    category, bestMatch.User.Email, bestMatch.Score, bestMatch.OpenComplaints);
            }

            return bestMatch?.User.Id;
        }

        public async Task<bool> AutoAssignComplaintAsync(int complaintId)
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

            // Atama yap
            complaint.AssignedToUserId = assigneeId;
            complaint.AssignedAt = DateTime.Now;
            complaint.Status = ComplaintStatus.InProgress;
            complaint.DueDate = _slaService.CalculateDueDate(complaint.Priority, complaint.Category);

            await _context.SaveChangesAsync();

            _logger.LogInformation("Şikayet {Ticket} otomatik atandı: {User}",
                complaint.TicketNumber, assigneeId);

            return true;
        }

        public async Task<List<UserWorkload>> GetUserWorkloadsAsync()
        {
            var users = await _context.Users
                .Where(u => u.IsActive)
                .ToListAsync();

            var workloads = new List<UserWorkload>();

            foreach (var user in users)
            {
                var complaints = await _context.Complaints
                    .Where(c => c.AssignedToUserId == user.Id && !c.IsDeleted)
                    .ToListAsync();

                workloads.Add(new UserWorkload
                {
                    UserId = user.Id,
                    UserName = $"{user.FirstName} {user.LastName}",
                    Department = user.Department ?? "Belirtilmemiş",
                    TotalAssigned = complaints.Count,
                    OpenCount = complaints.Count(c => c.Status != ComplaintStatus.Closed && c.Status != ComplaintStatus.Resolved),
                    CriticalCount = complaints.Count(c => c.Priority == "Critical" && c.Status != ComplaintStatus.Closed),
                    BreachedCount = complaints.Count(c => c.IsSlaBreached && c.Status != ComplaintStatus.Closed),
                    AvgResolutionTime = complaints.Any(c => c.ResolutionTimeMinutes.HasValue)
                        ? (int?)complaints.Where(c => c.ResolutionTimeMinutes.HasValue).Average(c => c.ResolutionTimeMinutes.Value)
                        : null
                });
            }

            return workloads.OrderByDescending(w => w.OpenCount).ToList();
        }

        public async Task ReassignComplaintAsync(int complaintId, string newUserId, string reason)
        {
            var complaint = await _context.Complaints.FindAsync(complaintId);
            if (complaint == null) return;

            var oldUserId = complaint.AssignedToUserId;

            complaint.AssignedToUserId = newUserId;
            complaint.AssignedAt = DateTime.Now;

            // Not ekle
            var note = new ComplaintNote
            {
                ComplaintId = complaintId,
                Note = $"Yeniden atandı. Eski: {oldUserId} -> Yeni: {newUserId}. Sebep: {reason}", // ✅ Note kullan
                UserId = "System", // ✅ ComplaintNote'da UserId var
                CreatedAt = DateTime.Now, // BaseEntity'den geliyor varsayalım
                IsInternal = true
            };

            _context.ComplaintNotes.Add(note);
            await _context.SaveChangesAsync();

            _logger.LogInformation("Şikayet {Ticket} yeniden atandı: {Reason}",
                complaint.TicketNumber, reason);
        }
    }
}