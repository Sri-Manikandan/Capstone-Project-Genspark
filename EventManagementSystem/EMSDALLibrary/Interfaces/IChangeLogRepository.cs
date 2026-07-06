using EMSModelLibrary.Models;

namespace EMSDALLibrary.Interfaces
{
    public interface IChangeLogRepository : IRepository<ChangeLog>
    {
        Task<(List<ChangeLog> Items, int TotalCount)> SearchPaged(
            string? entityName, int? userId, string? action, int page, int pageSize);
    }
}
