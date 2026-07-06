using EMSDALLibrary.Contexts;
using EMSDALLibrary.Interfaces;
using EMSModelLibrary.Models;
using Microsoft.EntityFrameworkCore;

namespace EMSDALLibrary.Repositories
{
    public class ChangeLogRepository : AbstractRepository<ChangeLog>, IChangeLogRepository
    {
        public ChangeLogRepository(EventContext context) : base(context) { }

        public async Task<(List<ChangeLog> Items, int TotalCount)> SearchPaged(
            string? entityName, int? userId, string? action, int page, int pageSize)
        {
            var q = _context.ChangeLogs.AsQueryable();

            if (!string.IsNullOrWhiteSpace(entityName))
                q = q.Where(c => c.EntityName == entityName);
            if (userId.HasValue)
                q = q.Where(c => c.UserId == userId.Value);
            if (!string.IsNullOrWhiteSpace(action))
                q = q.Where(c => c.Action == action);

            var total = await q.CountAsync();
            var items = await q.OrderByDescending(c => c.CreatedAt)
                               .Skip((page - 1) * pageSize)
                               .Take(pageSize)
                               .ToListAsync();
            return (items, total);
        }
    }
}
