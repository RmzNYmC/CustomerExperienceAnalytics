using CEA.Core.Entities;
using CEA.Core.Enum;
using CEA.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace CEA.Business.Services
{
    public class SlaCalculatorService : ISlaCalculatorService
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogger<SlaCalculatorService> _logger;

        // SLA Süreleri (saat cinsinden)
        private readonly Dictionary<string, int> _prioritySlaHours = new()
        {
            ["Critical"] = 4,   // 4 saat
            ["High"] = 24,      // 1 gün
            ["Medium"] = 72,    // 3 gün
            ["Low"] = 168       // 1 hafta
        };

        // Kategori bazlı ek süreler (saat)
        private readonly Dictionary<string, int> _categoryAdditionalHours = new()
        {
            ["Ürün"] = 12,
            ["Hizmet"] = 8,
            ["Personel"] = 24,
            ["Lojistik"] = 16,
            ["Genel"] = 0
        };

        public SlaCalculatorService(ApplicationDbContext context, ILogger<SlaCalculatorService> logger)
        {
            _context = context;
            _logger = logger;
        }

        public DateTime CalculateDueDate(string priority, string category)
        {
            var baseHours = _prioritySlaHours.GetValueOrDefault(priority, 72);
            var additionalHours = _categoryAdditionalHours.GetValueOrDefault(category, 0);
            var totalHours = baseHours + additionalHours;

            // İş saatleri hesaplama (9:00 - 18:00, hafta içi)
            return AddBusinessHours(DateTime.Now, totalHours);
        }

        public async Task UpdateSlaMetricsAsync(int complaintId)
        {
            var complaint = await _context.Complaints.FindAsync(complaintId);
            if (complaint == null) return;

            var now = DateTime.Now;

            // İlk yanıt süresi (atama yapıldıysa)
            if (complaint.AssignedAt.HasValue && !complaint.ResponseTimeMinutes.HasValue)
            {
                var responseTime = complaint.AssignedAt.Value - complaint.CreatedAt;
                complaint.ResponseTimeMinutes = (int)responseTime.TotalMinutes;
            }

            // Çözüm süresi (çözüldüyse)
            if (complaint.ResolvedAt.HasValue && !complaint.ResolutionTimeMinutes.HasValue)
            {
                var resolutionTime = complaint.ResolvedAt.Value - complaint.CreatedAt;
                complaint.ResolutionTimeMinutes = (int)resolutionTime.TotalMinutes;

                // SLA aşımı kontrolü
                if (complaint.DueDate.HasValue && complaint.ResolvedAt > complaint.DueDate)
                {
                    complaint.IsSlaBreached = true;
                    complaint.BreachReason = $"Çözüm süresi aşıldı. Hedef: {complaint.DueDate:dd.MM.yyyy HH:mm}, Gerçekleşen: {complaint.ResolvedAt:dd.MM.yyyy HH:mm}";
                }
                else
                {
                    complaint.IsSlaBreached = false;
                    complaint.BreachReason = null;
                }

            }

            // Aktif şikayetlerde SLA aşımı kontrolü
            if (!complaint.ResolvedAt.HasValue && complaint.DueDate.HasValue && DateTime.Now > complaint.DueDate)
            {
                if (!complaint.IsSlaBreached)
                {
                    complaint.IsSlaBreached = true;
                    complaint.BreachReason = "SLA süresi doldu, çözüm bekleniyor";
                    _logger.LogWarning("SLA aşımı: Ticket {TicketNumber}", complaint.TicketNumber);
                }
            }
            // 4. Değişiklikleri kaydet (ÖNEMLİ!)
            await _context.SaveChangesAsync();
        }

        public async Task CheckAndMarkBreachedSlasAsync()
        {
            var now = DateTime.Now;
            var breachedComplaints = await _context.Complaints
                .Where(c => !c.IsDeleted
                    && c.Status != ComplaintStatus.Closed
                    && c.Status != ComplaintStatus.Resolved
                    && c.DueDate < now
                    && !c.IsSlaBreached)
                .ToListAsync();

            foreach (var complaint in breachedComplaints)
            {
                complaint.IsSlaBreached = true;
                complaint.BreachReason = "Otomatik: SLA süresi aşıldı";
                _logger.LogWarning("SLA aşımı tespit edildi: {Ticket}", complaint.TicketNumber);
            }

            await _context.SaveChangesAsync();
        }

        public async Task<SlaDashboardMetrics> GetSlaMetricsAsync()
        {
            var complaints = await _context.Complaints
                .Where(c => !c.IsDeleted && c.CreatedAt > DateTime.Now.AddMonths(-1))
                .ToListAsync();

            var resolved = complaints.Where(c => c.ResolvedAt.HasValue).ToList();

            return new SlaDashboardMetrics
            {
                TotalComplaints = complaints.Count,
                ResolvedCount = resolved.Count,
                BreachedCount = complaints.Count(c => c.IsSlaBreached),
                SlaComplianceRate = resolved.Any()
                    ? (decimal)resolved.Count(c => !c.IsSlaBreached) / resolved.Count * 100
                    : 0,
                AverageResolutionTimeMinutes = resolved.Any() && resolved.Any(c => c.ResolutionTimeMinutes.HasValue)
                    ? (int)resolved.Average(c => c.ResolutionTimeMinutes ?? 0)
                    : 0,
                CriticalBreaches = complaints.Count(c => c.IsSlaBreached && c.Priority == "Critical"),
                ByCategory = complaints
                    .GroupBy(c => c.Category)
                    .ToDictionary(g => g.Key, g => new CategorySlaMetrics
                    {
                        Total = g.Count(),
                        Breached = g.Count(c => c.IsSlaBreached),
                        AvgResolutionTime = g.Where(c => c.ResolutionTimeMinutes.HasValue)
                            .Average(c => (decimal?)c.ResolutionTimeMinutes) ?? 0
                    })
            };
        }

        private DateTime AddBusinessHours(DateTime start, int hours)
        {
            var current = start;
            var remainingHours = hours;

            while (remainingHours > 0)
            {
                // Hafta sonu kontrolü
                if (current.DayOfWeek == DayOfWeek.Saturday)
                    current = current.AddDays(2).Date.AddHours(9);
                else if (current.DayOfWeek == DayOfWeek.Sunday)
                    current = current.AddDays(1).Date.AddHours(9);
                // İş saati kontrolü (9:00 - 18:00)
                else if (current.Hour < 9)
                    current = current.Date.AddHours(9);
                else if (current.Hour >= 18)
                    current = current.AddDays(1).Date.AddHours(9);
                else
                {
                    current = current.AddHours(1);
                    remainingHours--;
                }
            }

            return current;
        }
    }
}