using EMSModelLibrary.Models;

namespace EMSDALLibrary.Interfaces
{
    public interface ISeatReservationRepository : IRepository<SeatReservation>
    {
        Task<SeatReservation?> GetActiveByEventAndSeat(int eventId, int seatId);
        Task<List<SeatReservation>> GetByUserId(int userId);
        Task<List<SeatReservation>> GetByEventId(int eventId);
        Task DeleteExpired();
    }
}
