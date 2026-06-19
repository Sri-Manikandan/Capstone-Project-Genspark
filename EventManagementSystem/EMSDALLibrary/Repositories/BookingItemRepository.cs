using EMSDALLibrary.Contexts;
using EMSDALLibrary.Interfaces;
using EMSModelLibrary.Models;
using Microsoft.EntityFrameworkCore;

namespace EMSDALLibrary.Repositories
{
    public class BookingItemRepository : AbstractRepository<BookingItem>, IBookingItemRepository
    {
        public BookingItemRepository(EventContext context) : base(context) { }

        public async Task<List<BookingItem>> GetByBookingId(int bookingId)
        {
            return await _context.BookingItems.Where(bi => bi.BookingId == bookingId).ToListAsync();
        }
    }
}
