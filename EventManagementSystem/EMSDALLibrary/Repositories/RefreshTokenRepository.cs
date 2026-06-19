using EMSDALLibrary.Contexts;
using EMSDALLibrary.Interfaces;
using EMSModelLibrary.Models;
using Microsoft.EntityFrameworkCore;

namespace EMSDALLibrary.Repositories
{
    public class RefreshTokenRepository : AbstractRepository<RefreshToken>, IRefreshTokenRepository
    {
        public RefreshTokenRepository(EventContext context) : base(context) { }

        public async Task<RefreshToken?> GetByToken(string token)
        {
            return await _context.RefreshTokens.FirstOrDefaultAsync(rt => rt.Token == token);
        }

        public async Task RevokeByUserId(int userId)
        {
            var tokens = await _context.RefreshTokens
                .Where(rt => rt.UserId == userId && rt.RevokedAt == null)
                .ToListAsync();
            foreach (var t in tokens)
                t.RevokedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();
        }
    }
}
