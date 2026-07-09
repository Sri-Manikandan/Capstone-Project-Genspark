using EMSDALLibrary.Contexts;
using EMSDALLibrary.Interfaces;
using EMSModelLibrary.Models;
using Microsoft.EntityFrameworkCore;

namespace EMSDALLibrary.Repositories
{
    public class ScreeningNotificationRepository : AbstractRepository<ScreeningNotification>, IScreeningNotificationRepository
    {
        public ScreeningNotificationRepository(EventContext context) : base(context) { }

        public async Task<ScreeningNotification?> GetByScreeningAndUserId(int screeningId, int userId)
        {
            return await _context.ScreeningNotifications
                .FirstOrDefaultAsync(sn => sn.ScreeningId == screeningId && sn.UserId == userId);
        }

        public async Task<List<ScreeningNotification>> GetActiveNotificationsByScreeningId(int screeningId)
        {
            return await _context.ScreeningNotifications
                .Where(sn => sn.ScreeningId == screeningId && sn.Status == "Active")
                .ToListAsync();
        }
    }
}
