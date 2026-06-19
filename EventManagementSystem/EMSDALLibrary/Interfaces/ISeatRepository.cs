using EMSModelLibrary.Models;

namespace EMSDALLibrary.Interfaces
{
    public interface ISeatRepository : IRepository<Seat>
    {
        Task<List<Seat>> GetByVenueId(int venueId);
        Task<List<Seat>> GetAvailableByEventId(int eventId);
        Task<int> CountByVenueAndType(int venueId, string seatType);
    }
}
