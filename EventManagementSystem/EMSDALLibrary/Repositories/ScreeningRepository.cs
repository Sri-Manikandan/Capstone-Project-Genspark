using EMSDALLibrary.Contexts;
using EMSDALLibrary.Interfaces;
using EMSModelLibrary.Models;
using Microsoft.EntityFrameworkCore;

namespace EMSDALLibrary.Repositories
{
    public class ScreeningRepository : AbstractRepository<Screening>, IScreeningRepository
    {
        public ScreeningRepository(EventContext context) : base(context) { }

        public async Task<List<Screening>> GetStartingBetween(DateTime from, DateTime to)
        {
            return await _context.Screenings
                .Where(s => s.StartTime >= from && s.StartTime < to)
                .ToListAsync();
        }

        public async Task<List<Screening>> GetByEventId(int eventId)
        {
            return await _context.Screenings
                .Where(s => s.EventId == eventId)
                .OrderBy(s => s.StartTime)
                .ToListAsync();
        }

        public async Task<bool> HasActivity(int screeningId)
        {
            var booked = await _context.Bookings
                .AnyAsync(b => b.ScreeningId == screeningId && b.BookingStatus != "Cancelled");
            if (booked) return true;

            var now = DateTime.UtcNow;
            return await _context.SeatReservations
                .AnyAsync(sr => sr.ScreeningId == screeningId
                             && (sr.Status == "Confirmed"
                                 || (sr.Status == "Active" && sr.ReservedUntil > now)));
        }
    }
}
