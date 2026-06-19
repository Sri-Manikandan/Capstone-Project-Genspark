using EMSDALLibrary.Contexts;
using EMSDALLibrary.Interfaces;
using EMSModelLibrary.Models;
using Microsoft.EntityFrameworkCore;

namespace EMSDALLibrary.Repositories
{
    public class OrganizerRequestRepository : AbstractRepository<OrganizerRequest>, IOrganizerRequestRepository
    {
        public OrganizerRequestRepository(EventContext context) : base(context) { }

        public async Task<List<OrganizerRequest>> GetByStatus(string status) =>
            await _context.OrganizerRequests
                .Where(r => r.Status == status)
                .OrderByDescending(r => r.RequestedAt)
                .ToListAsync();

        public async Task<OrganizerRequest?> GetPendingByUserId(int userId) =>
            await _context.OrganizerRequests
                .FirstOrDefaultAsync(r => r.UserId == userId && r.Status == "Pending");

        public async Task<OrganizerRequest?> GetLatestByUserId(int userId) =>
            await _context.OrganizerRequests
                .Where(r => r.UserId == userId)
                .OrderByDescending(r => r.RequestedAt)
                .FirstOrDefaultAsync();

        public async Task<(List<OrganizerRequest> Items, int TotalCount)> SearchPaged(string? status, int page, int pageSize)
        {
            var q = _context.OrganizerRequests.AsQueryable();
            if (!string.IsNullOrWhiteSpace(status))
                q = q.Where(r => r.Status == status);
            var total = await q.CountAsync();
            var items = await q.OrderByDescending(r => r.RequestedAt)
                               .Skip((page - 1) * pageSize)
                               .Take(pageSize)
                               .ToListAsync();
            return (items, total);
        }
    }
}
