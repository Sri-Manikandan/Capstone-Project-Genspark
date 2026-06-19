using EMSModelLibrary.Models;

namespace EMSDALLibrary.Interfaces
{
    public interface IOrganizerRequestRepository : IRepository<OrganizerRequest>
    {
        Task<List<OrganizerRequest>> GetByStatus(string status);
        Task<OrganizerRequest?> GetPendingByUserId(int userId);
        Task<OrganizerRequest?> GetLatestByUserId(int userId);
        Task<(List<OrganizerRequest> Items, int TotalCount)> SearchPaged(string? status, int page, int pageSize);
    }
}
