using EMSModelLibrary.Models;

namespace EMSDALLibrary.Interfaces
{
    public interface ISeatRepository : IRepository<Seat>
    {
        Task<List<Seat>> GetByVenueId(int venueId);
        Task<List<SeatAvailability>> GetAvailableByScreeningId(int screeningId);
        Task<int> CountByVenueAndType(int venueId, string seatType);
        Task<int> CountByVenueSectionAndType(int venueId, string section, string seatType);
        Task<bool> ScreenHasActiveSeatUsage(int venueId, string section);
        Task ReplaceScreenSeats(int venueId, string section, List<Seat> seats);
    }
}
