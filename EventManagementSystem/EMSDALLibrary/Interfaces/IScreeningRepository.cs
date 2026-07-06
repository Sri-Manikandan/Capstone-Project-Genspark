using EMSModelLibrary.Models;

namespace EMSDALLibrary.Interfaces
{
    public interface IScreeningRepository : IRepository<Screening>
    {
        Task<List<Screening>> GetByEventId(int eventId);
        // True when the screening already has non-cancelled bookings or active reservations.
        Task<bool> HasActivity(int screeningId);
    }
}
