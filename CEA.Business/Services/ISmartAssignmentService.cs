namespace CEA.Business.Services
{
    public interface ISmartAssignmentService
    {
        Task<string?> FindBestAssigneeAsync(string category, string priority, int? excludeUserId = null);
        Task<bool> AutoAssignComplaintAsync(int complaintId);
        Task<List<UserWorkload>> GetUserWorkloadsAsync();
        Task ReassignComplaintAsync(int complaintId, string newUserId, string reason);
    }

    public class UserWorkload
    {
        public string UserId { get; set; } = string.Empty;
        public string UserName { get; set; } = string.Empty;
        public string Department { get; set; } = string.Empty;
        public int TotalAssigned { get; set; }
        public int OpenCount { get; set; }
        public int CriticalCount { get; set; }
        public int BreachedCount { get; set; }
        public int? AvgResolutionTime { get; set; }
        public bool IsOverloaded => OpenCount > 10; // 10'dan fazla açık şikayet
    }
}