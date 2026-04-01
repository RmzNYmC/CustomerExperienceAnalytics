using CEA.Core.Entities;

namespace CEA.Business.Services
{
    public interface ISlaCalculatorService
    {
        DateTime CalculateDueDate(string priority, string category);
        Task UpdateSlaMetricsAsync(int complaintId);
        Task CheckAndMarkBreachedSlasAsync();
        Task<SlaDashboardMetrics> GetSlaMetricsAsync();
    }

    public class SlaDashboardMetrics
    {
        public int TotalComplaints { get; set; }
        public int ResolvedCount { get; set; }
        public int BreachedCount { get; set; }
        public decimal SlaComplianceRate { get; set; }
        public int AverageResolutionTimeMinutes { get; set; }
        public int CriticalBreaches { get; set; }
        public Dictionary<string, CategorySlaMetrics> ByCategory { get; set; } = new();
    }

    public class CategorySlaMetrics
    {
        public int Total { get; set; }
        public int Breached { get; set; }
        public decimal AvgResolutionTime { get; set; }
    }
}