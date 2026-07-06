using EMSDALLibrary.Contexts;
using EMSDALLibrary.Interfaces;
using EMSModelLibrary.Models;
using Microsoft.EntityFrameworkCore;

namespace EMSDALLibrary.Repositories
{
    public class SeatReservationRepository : AbstractRepository<SeatReservation>, ISeatReservationRepository
    {
        public SeatReservationRepository(EventContext context) : base(context) { }

        public async Task<SeatReservation?> GetActiveByScreeningAndSeat(int screeningId, int seatId)
        {
            var now = DateTime.UtcNow;
            return await _context.SeatReservations.FirstOrDefaultAsync(
                sr => sr.ScreeningId == screeningId && sr.SeatId == seatId
                   && sr.Status == "Active" && sr.ReservedUntil > now);
        }

        public async Task<List<SeatReservation>> GetByUserId(int userId)
        {
            return await _context.SeatReservations.Where(sr => sr.UserId == userId).ToListAsync();
        }

        public async Task<List<SeatReservation>> GetByScreeningId(int screeningId)
        {
            return await _context.SeatReservations.Where(sr => sr.ScreeningId == screeningId).ToListAsync();
        }

        public async Task DeleteExpired()
        {
            var now = DateTime.UtcNow;
            var expired = await _context.SeatReservations
                .Where(sr => sr.Status == "Active" && sr.ReservedUntil <= now)
                .ToListAsync();
            _context.SeatReservations.RemoveRange(expired);
            await _context.SaveChangesAsync();
        }
    }
}
