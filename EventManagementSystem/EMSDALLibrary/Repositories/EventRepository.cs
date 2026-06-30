using EMSDALLibrary.Contexts;
using EMSDALLibrary.Interfaces;
using EMSModelLibrary.Models;
using Microsoft.EntityFrameworkCore;

namespace EMSDALLibrary.Repositories
{
    public class EventRepository : AbstractRepository<Event>, IEventRepository
    {
        public EventRepository(EventContext context) : base(context) { }

        public async Task<(List<Event> Items, int TotalCount)> GetByOrganizerId(int organizerId, int page, int pageSize)
        {
            var q = _context.Events.Where(e => e.OrganizerId == organizerId);
            var total = await q.CountAsync();
            var items = await q.OrderByDescending(e => e.StartTime)
                               .Skip((page - 1) * pageSize)
                               .Take(pageSize)
                               .ToListAsync();
            return (items, total);
        }

        public async Task<List<Event>> GetByStatus(string status)
        {
            return await _context.Events.Where(e => e.Status == status).ToListAsync();
        }

        public async Task<List<Event>> GetByCategory(string category)
        {
            return await _context.Events.Where(e => e.Category == category).ToListAsync();
        }

        public async Task<List<string>> GetCategories(string status)
        {
            return await _context.Events
                .Where(e => e.Status == status)
                .Select(e => e.Category)
                .Distinct()
                .OrderBy(c => c)
                .ToListAsync();
        }

        public async Task<Event?> GetBySlug(string slug)
        {
            return await _context.Events.FirstOrDefaultAsync(e => e.Slug == slug);
        }

        public async Task<(List<Event> Items, int TotalCount)> Search(
            string? query, string? category, string? status,
            DateTime? startFrom, DateTime? startTo,
            string? sortBy, string? sortOrder,
            int page, int pageSize)
        {
            var q = _context.Events.AsQueryable();

            if (!string.IsNullOrWhiteSpace(query))
                q = q.Where(e => e.Title.Contains(query) || e.Description.Contains(query));
            if (!string.IsNullOrWhiteSpace(category))
                q = q.Where(e => e.Category == category);
            if (!string.IsNullOrWhiteSpace(status))
                q = q.Where(e => e.Status == status);
            if (startFrom.HasValue)
                q = q.Where(e => e.StartTime >= startFrom.Value);
            if (startTo.HasValue)
                q = q.Where(e => e.StartTime <= startTo.Value);

            var total = await q.CountAsync();

            bool descending = string.IsNullOrWhiteSpace(sortOrder) || sortOrder.ToLower() != "asc";
            q = sortBy?.ToLower() switch
            {
                "title"     => descending ? q.OrderByDescending(e => e.Title)     : q.OrderBy(e => e.Title),
                "createdat" => descending ? q.OrderByDescending(e => e.CreatedAt) : q.OrderBy(e => e.CreatedAt),
                _           => descending ? q.OrderByDescending(e => e.StartTime) : q.OrderBy(e => e.StartTime),
            };

            var items = await q.Skip((page - 1) * pageSize).Take(pageSize).ToListAsync();
            return (items, total);
        }
    }
}
