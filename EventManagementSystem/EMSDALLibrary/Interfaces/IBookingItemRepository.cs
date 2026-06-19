using EMSModelLibrary.Models;

namespace EMSDALLibrary.Interfaces
{
    public interface IBookingItemRepository : IRepository<BookingItem>
    {
        Task<List<BookingItem>> GetByBookingId(int bookingId);
    }
}
