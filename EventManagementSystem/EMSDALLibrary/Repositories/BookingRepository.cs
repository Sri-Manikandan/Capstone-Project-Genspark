using EMSDALLibrary.Contexts;
using EMSDALLibrary.Interfaces;
using EMSModelLibrary.Models;
using Microsoft.EntityFrameworkCore;

namespace EMSDALLibrary.Repositories
{
    public class BookingRepository : AbstractRepository<Booking>, IBookingRepository
    {
        public BookingRepository(EventContext context) : base(context) { }

        public async Task<List<Booking>> GetByUserId(int userId)
        {
            return await _context.Bookings.Where(b => b.UserId == userId).ToListAsync();
        }

        public async Task<List<Booking>> GetByEventId(int eventId)
        {
            return await _context.Bookings.Where(b => b.EventId == eventId).ToListAsync();
        }

        public async Task<Booking?> GetByReference(string reference)
        {
            return await _context.Bookings.FirstOrDefaultAsync(b => b.BookingReference == reference);
        }

        public async Task<(List<Booking> Items, int TotalCount)> SearchByUserId(int userId, string? status, int page, int pageSize)
        {
            var q = _context.Bookings.Where(b => b.UserId == userId);
            if (!string.IsNullOrWhiteSpace(status))
                q = q.Where(b => b.BookingStatus == status);
            var total = await q.CountAsync();
            var items = await q.OrderByDescending(b => b.CreatedAt)
                               .Skip((page - 1) * pageSize)
                               .Take(pageSize)
                               .ToListAsync();
            return (items, total);
        }

        public async Task<(List<Booking> Items, int TotalCount)> SearchByEventId(int eventId, string? status, int page, int pageSize)
        {
            var q = _context.Bookings.Where(b => b.EventId == eventId);
            if (!string.IsNullOrWhiteSpace(status))
                q = q.Where(b => b.BookingStatus == status);
            var total = await q.CountAsync();
            var items = await q.OrderByDescending(b => b.CreatedAt)
                               .Skip((page - 1) * pageSize)
                               .Take(pageSize)
                               .ToListAsync();
            return (items, total);
        }
    }
}
