using EMSDALLibrary.Contexts;
using EMSDALLibrary.Interfaces;
using EMSModelLibrary.Models;
using Microsoft.EntityFrameworkCore;

namespace EMSDALLibrary.Repositories
{
    public class SeatRepository : AbstractRepository<Seat>, ISeatRepository
    {
        public SeatRepository(EventContext context) : base(context) { }

        public async Task<List<Seat>> GetByVenueId(int venueId)
        {
            return await _context.Seats.Where(s => s.VenueId == venueId).ToListAsync();
        }

        public async Task<int> CountByVenueAndType(int venueId, string seatType)
        {
            return await _context.Seats
                .CountAsync(s => s.VenueId == venueId && s.SeatType == seatType);
        }

        public async Task<List<Seat>> GetAvailableByEventId(int eventId)
        {
            var now = DateTime.UtcNow;
            var bookedSeatIds = await _context.BookingItems
                .Join(_context.Bookings,
                    bi => bi.BookingId,
                    b => b.Id,
                    (bi, b) => new { bi.SeatId, b.EventId, b.BookingStatus })
                .Where(x => x.EventId == eventId && x.BookingStatus != "Cancelled")
                .Select(x => x.SeatId)
                .ToListAsync();

            var reservedSeatIds = await _context.SeatReservations
                .Where(sr => sr.EventId == eventId && sr.ReservedUntil > now && sr.Status == "Active")
                .Select(sr => sr.SeatId)
                .ToListAsync();

            var unavailableSeatIds = bookedSeatIds.Union(reservedSeatIds).ToHashSet();

            var eventEntity = await _context.Events.FindAsync(eventId);
            if (eventEntity == null) return new List<Seat>();

            return await _context.Seats
                .Where(s => s.VenueId == eventEntity.VenueId
                            && !unavailableSeatIds.Contains(s.Id)
                            && (eventEntity.Screen == "" || s.Section == eventEntity.Screen))
                .ToListAsync();
        }

        public async Task<bool> ScreenHasActiveSeatUsage(int venueId, string section)
        {
            var seatIds = await _context.Seats
                .Where(s => s.VenueId == venueId && s.Section == section)
                .Select(s => s.Id)
                .ToListAsync();
            if (seatIds.Count == 0) return false;

            var booked = await _context.BookingItems
                .Join(_context.Bookings, bi => bi.BookingId, b => b.Id, (bi, b) => new { bi.SeatId, b.BookingStatus })
                .AnyAsync(x => seatIds.Contains(x.SeatId) && x.BookingStatus != "Cancelled");
            if (booked) return true;

            var now = DateTime.UtcNow;
            return await _context.SeatReservations
                .AnyAsync(sr => seatIds.Contains(sr.SeatId) && sr.Status == "Active" && sr.ReservedUntil > now);
        }

        public async Task ReplaceScreenSeats(int venueId, string section, List<Seat> seats)
        {
            var existing = await _context.Seats
                .Where(s => s.VenueId == venueId && s.Section == section)
                .ToListAsync();
            _context.Seats.RemoveRange(existing);
            await _context.Seats.AddRangeAsync(seats);
            await _context.SaveChangesAsync();
        }
    }
}
