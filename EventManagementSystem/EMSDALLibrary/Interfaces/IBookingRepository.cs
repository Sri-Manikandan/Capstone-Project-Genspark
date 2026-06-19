using EMSModelLibrary.Models;

namespace EMSDALLibrary.Interfaces
{
    public interface IBookingRepository : IRepository<Booking>
    {
        Task<List<Booking>> GetByUserId(int userId);
        Task<List<Booking>> GetByEventId(int eventId);
        Task<Booking?> GetByReference(string reference);
        Task<(List<Booking> Items, int TotalCount)> SearchByUserId(int userId, string? status, int page, int pageSize);
        Task<(List<Booking> Items, int TotalCount)> SearchByEventId(int eventId, string? status, int page, int pageSize);
    }
}
