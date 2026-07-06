using EMSModelLibrary.Models;

namespace EMSDALLibrary.Interfaces
{
    public interface ISeatReservationRepository : IRepository<SeatReservation>
    {
        Task<SeatReservation?> GetActiveByScreeningAndSeat(int screeningId, int seatId);
        Task<List<SeatReservation>> GetByUserId(int userId);
        Task<List<SeatReservation>> GetByScreeningId(int screeningId);
        Task DeleteExpired();
    }
}
