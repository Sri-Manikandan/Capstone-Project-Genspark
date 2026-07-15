using EMSDALLibrary.Contexts;
using EMSDALLibrary.Interfaces;
using EMSModelLibrary.Exceptions;
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

        // Seats store their screen as Section, so a ticket type tied to one screen counts only
        // that screen's seats of the type — not every screen's in the venue.
        public async Task<int> CountByVenueSectionAndType(int venueId, string section, string seatType)
        {
            return await _context.Seats
                .CountAsync(s => s.VenueId == venueId && s.Section == section && s.SeatType == seatType);
        }

        // Availability is per screening: a seat booked/reserved in one screening stays
        // free in another screening on the same physical screen.
        public async Task<List<SeatAvailability>> GetAvailableByScreeningId(int screeningId)
        {
            var screening = await _context.Screenings.FindAsync(screeningId);
            if (screening == null) return new List<SeatAvailability>();

            var eventEntity = await _context.Events.FindAsync(screening.EventId);
            if (eventEntity == null) return new List<SeatAvailability>();

            var now = DateTime.UtcNow;
            var bookedSeatIds = await _context.BookingItems
                .Join(_context.Bookings,
                    bi => bi.BookingId,
                    b => b.Id,
                    (bi, b) => new { bi.SeatId, b.ScreeningId, b.BookingStatus })
                .Where(x => x.ScreeningId == screeningId && x.BookingStatus != "Cancelled")
                .Select(x => x.SeatId)
                .ToListAsync();

            var reservedSeatIds = await _context.SeatReservations
                .Where(sr => sr.ScreeningId == screeningId && sr.ReservedUntil > now && sr.Status == "Active")
                .Select(sr => sr.SeatId)
                .ToListAsync();

            var unavailableSeatIds = bookedSeatIds.Union(reservedSeatIds).ToHashSet();

            // Return the whole venue seat grid for this screen, each seat flagged with its
            // availability for THIS screening. Ordering is explicit (Section→Row→SeatNumber)
            // so the layout never reshuffles as seats get booked — an unordered query lets the
            // database return rows in a different order once the result set changes. Taken seats
            // stay in the grid (drawn as "taken") rather than disappearing.
            var seats = await _context.Seats
                .Where(s => s.VenueId == eventEntity.VenueId && s.Section == screening.Screen)
                .OrderBy(s => s.Section)
                .ThenBy(s => s.Row)
                .ThenBy(s => s.SeatNumber)
                .ToListAsync();

            return seats
                .Select(s => new SeatAvailability
                {
                    Seat = s,
                    IsAvailable = !unavailableSeatIds.Contains(s.Id)
                })
                .ToList();
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
            await using var tx = await _context.Database.BeginTransactionAsync();

            if (await ScreenHasActiveSeatUsage(venueId, section))
                throw new ValidationException("Cannot edit a screen that already has bookings.");

            var existing = await _context.Seats
                .Where(s => s.VenueId == venueId && s.Section == section)
                .ToListAsync();
            _context.Seats.RemoveRange(existing);
            await _context.Seats.AddRangeAsync(seats);

            try
            {
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateException)
            {
                throw new ValidationException("Cannot edit a screen that already has bookings.");
            }

            await tx.CommitAsync();
        }
    }
}
