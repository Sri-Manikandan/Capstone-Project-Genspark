using EMSDALLibrary.Contexts;
using EMSDALLibrary.Interfaces;
using EMSModelLibrary.Models;
using Microsoft.EntityFrameworkCore;

namespace EMSDALLibrary.Repositories
{
    public class UserRepository : AbstractRepository<User>, IUserRepository
    {
        public UserRepository(EventContext context) : base(context) { }

        public async Task<User?> GetByEmail(string email)
        {
            return await _context.Users.FirstOrDefaultAsync(u => u.Email == email);
        }

        public async Task<bool> EmailExists(string email)
        {
            return await _context.Users.AnyAsync(u => u.Email == email);
        }

        public async Task<(List<User> Items, int TotalCount)> Search(string? query, string? role, bool? isActive, int page, int pageSize)
        {
            var q = _context.Users.AsQueryable();
            if (!string.IsNullOrWhiteSpace(query))
            {
                // ILIKE, not Contains: Contains maps to a case-sensitive LIKE on Postgres,
                // so searching "wilson" would miss "Emma Wilson".
                var pattern = LikePattern.Contains(query);
                q = q.Where(u => EF.Functions.ILike(u.Name, pattern) || EF.Functions.ILike(u.Email, pattern));
            }
            if (!string.IsNullOrWhiteSpace(role))
                q = q.Where(u => u.Role == role);
            if (isActive.HasValue)
                q = q.Where(u => u.IsActive == isActive.Value);
            var total = await q.CountAsync();
            var items = await q.OrderBy(u => u.Name)
                               .Skip((page - 1) * pageSize)
                               .Take(pageSize)
                               .ToListAsync();
            return (items, total);
        }

        public async Task<List<User>> GetAdmins()
        {
            return await _context.Users
                .Where(u => u.Role == "Admin" && u.IsActive)
                .ToListAsync();
        }
    }
}
